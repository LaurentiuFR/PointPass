using UnityEngine;
#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PointPass.Networking;
using PointPass.Online;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
#endif

namespace PointPass.Server
{
    /// <summary>One bounded dedicated room. HTTP admission never runs inside a physics step.</summary>
    public sealed class OnlineMatchWorker : MonoBehaviour
    {
        public static bool TryStart()
        {
#if UNITY_SERVER && !UNITY_EDITOR
            new GameObject("PointPass Dedicated Worker").AddComponent<OnlineMatchWorker>();
            return true;
#else
            return false;
#endif
        }

#if UNITY_SERVER || UNITY_EDITOR
        private enum Rejection { Admission, Malformed, Ordering, Action, Rate, Probe, UnknownPeer }
        private sealed class Peer
        {
            public string PlayerId;
            public int Slot, Count;
            public uint Sequence;
            public bool HasSequence;
            public double LastInput, Window;
        }
        private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        private readonly Dictionary<int, ulong> slots = new Dictionary<int, ulong>();
        private readonly Dictionary<string, OnlineApiContracts.Departed> departures = new Dictionary<string, OnlineApiContracts.Departed>();
        private NetworkManager network;
        private UnityTransport transport;
        private ServerMatchWorld world;
        private string instanceId, api, workerKey, certificatePath, privateKeyPath, statePath, matchId;
        private ushort port;
        private uint sequence;
        private int pendingApprovals;
        private double nextSnapshot, missingSince = -1, lastApiSuccess, nextSummary, simulationSeconds;
        private long commands, rejected, bytesSent, bytesReceived, snapshots, activeFixedSteps;
        private long admissionRejected, malformedRejected, orderingRejected, actionRejected, rateRejected, probeRejected, unknownPeerRejected;
        private long waitingHeartbeatsIgnored, closingHeartbeatsIgnored;
        private readonly long[] inputBytesReceivedBySlot = new long[2], probeBytesReceivedBySlot = new long[2];
        private readonly long[] stateBytesSentBySlot = new long[2], probeBytesSentBySlot = new long[2];
        private double summaryAt, previousFrameAt, frameSecondsSum, frameSecondsPeak;
        private long frameSamples, previousStepCount;
        private ProfilerRecorder physicsRecorder;
        private long physicsFrameSamples, physicsCalls, physicsNanosecondsSum, physicsNanosecondsPeak;
        private bool stopping, submitting, registering, apiHealthy;
        private Coroutine heartbeat;
        private bool departureSending;
        private double nextDeparture;

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            Physics2D.gravity = Vector2.zero;
            Time.fixedDeltaTime = 0.02f;
            Time.timeScale = 0;
            instanceId = Guid.NewGuid().ToString("D");
            api = Environment.GetEnvironmentVariable("POINTPASS_API_BASE") ?? "http://127.0.0.1:5000";
            workerKey = Environment.GetEnvironmentVariable("POINTPASS_WORKER_KEY");
            certificatePath = Environment.GetEnvironmentVariable("POINTPASS_TLS_CERT");
            privateKeyPath = Environment.GetEnvironmentVariable("POINTPASS_TLS_KEY");
            statePath = Environment.GetEnvironmentVariable("POINTPASS_STATE_PATH");
            string version = Environment.GetEnvironmentVariable("POINTPASS_GAME_VERSION");
            if (version != OnlineProtocol.GameVersion || string.IsNullOrWhiteSpace(workerKey)
                || !Uri.TryCreate(api, UriKind.Absolute, out Uri uri) || !uri.IsLoopback
                || !ushort.TryParse(Environment.GetEnvironmentVariable("POINTPASS_GAME_PORT") ?? "7777", out port) || port == 0
                || string.IsNullOrEmpty(statePath) || !File.Exists(certificatePath) || !File.Exists(privateKeyPath))
            { Debug.LogError("[PointPass Worker] Invalid protected server configuration; refusing to listen."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(statePath);
            // A result from a prior crashed worker must be submitted before replacing its lease.
            string outbox = Path.Combine(statePath, "pending-result.json");
            if (File.Exists(outbox))
            {
                OnlineApiContracts.Result result = null;
                try { if (new FileInfo(outbox).Length <= 1024) result = JsonUtility.FromJson<OnlineApiContracts.Result>(File.ReadAllText(outbox)); }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException) { }
                if (result == null || !Guid.TryParse(result.instanceId, out _) || !Guid.TryParse(result.matchId, out _)
                    || result.blueScore < 0 || result.redScore < 0 || result.blueScore > 1000 || result.redScore > 1000)
                { Debug.LogError("[PointPass Worker] Invalid result outbox; operator review required."); Application.Quit(3); yield break; }
                bool saved = false;
                double nextWarning = 0;
                while (!saved)
                {
                    yield return Post<OnlineApiContracts.Worker>("/internal/matches/result", result, (value, error) => saved = error == null);
                    if (!saved)
                    {
                        if (Time.unscaledTimeAsDouble >= nextWarning)
                        { nextWarning = Time.unscaledTimeAsDouble + 60; Debug.LogWarning("[PointPass Worker] Result recovery pending; admission stays closed. Review API/worker lease if retries persist."); }
                        yield return new WaitForSecondsRealtime(5);
                    }
                }
                File.Delete(outbox);
            }
            while (!apiHealthy)
            {
                registering = true;
                yield return Post<OnlineApiContracts.Worker>("/internal/worker/register", new OnlineApiContracts.Register { instanceId = instanceId },
                    (value, error) => { apiHealthy = error == null && value != null && value.instanceId == instanceId; });
                registering = false;
                if (!apiHealthy) yield return new WaitForSecondsRealtime(5);
            }
            StartTransport();
            heartbeat = StartCoroutine(Heartbeat());
        }

        private void StartTransport()
        {
            StartPhysicsRecorder();
            summaryAt = Time.realtimeSinceStartupAsDouble; previousFrameAt = 0;
            previousStepCount = activeFixedSteps; nextSummary = 0;
            var root = new GameObject("Dedicated encrypted transport");
            transport = root.AddComponent<UnityTransport>();
            transport.UseEncryption = true;
            transport.SetServerSecrets(File.ReadAllText(certificatePath), File.ReadAllText(privateKeyPath));
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ForceSamePrefabs = false, ConnectionApproval = true, TickRate = 30, ClientConnectionBufferTimeout = 8 };
            network.ConnectionApprovalCallback = Approve;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += () => { Debug.LogError("[PointPass Worker] Transport failed; supervisor restart required."); Application.Quit(4); };
            if (!network.StartServer()) { Debug.LogError("[PointPass Worker] Could not start encrypted transport."); Application.Quit(4); return; }
            network.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.InputMessage, Input);
            network.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.ProbeMessage, Probe);
            Debug.Log("[PointPass Worker] Ready; encrypted UDP " + port + "; capacity 2; physics 50 Hz; snapshots 30 Hz; idle simulation suspended.");
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Approved = false;
            response.Reason = "Admission unavailable. Obtain a fresh ticket and retry.";
            if (stopping || !apiHealthy || departures.Count >= 16 || pendingApprovals >= 2 || peers.Count + pendingApprovals >= 2
                || request.Payload == null || request.Payload.Length > 256 || request.Payload.Length < 60) { Reject(Rejection.Admission); return; }
            OnlineApiContracts.Approval payload = null;
            try { payload = JsonUtility.FromJson<OnlineApiContracts.Approval>(new UTF8Encoding(false, true).GetString(request.Payload)); }
            catch (Exception exception) when (exception is ArgumentException || exception is DecoderFallbackException) { }
            if (payload == null || payload.gameVersion != OnlineProtocol.GameVersion || !ValidTicket(payload.ticket))
            { response.Reason = "Invalid ticket or incompatible game version."; Reject(Rejection.Admission); return; }
            response.Pending = true;
            pendingApprovals++;
            StartCoroutine(Redeem(request.ClientNetworkId, payload, response));
        }

        private IEnumerator Redeem(ulong client, OnlineApiContracts.Approval payload, NetworkManager.ConnectionApprovalResponse response)
        {
            OnlineApiContracts.Redeemed admitted = null;
            string error = null;
            yield return Post<OnlineApiContracts.Redeemed>("/internal/tickets/redeem", new OnlineApiContracts.Redeem {
                instanceId = instanceId, ticket = payload.ticket, gameVersion = payload.gameVersion, connectionId = client.ToString() },
                (value, failure) => { admitted = value; error = failure; });
            pendingApprovals--;
            bool accepted = !stopping && network != null && network.PendingClients.ContainsKey(client)
                && error == null && admitted != null && admitted.slot >= 0 && admitted.slot < 2
                && !slots.ContainsKey(admitted.slot) && (matchId == null || matchId == admitted.matchId);
            if (accepted)
            {
                matchId = admitted.matchId;
                slots[admitted.slot] = client;
                peers[client] = new Peer { PlayerId = admitted.playerId, Slot = admitted.slot, LastInput = Time.unscaledTimeAsDouble };
            }
            else
            {
                Reject(Rejection.Admission);
                if (error == null && admitted != null)
                {
                    if (matchId == null) matchId = admitted.matchId;
                    if (missingSince < 0) missingSince = Time.unscaledTimeAsDouble;
                    QueueDeparture(admitted.matchId, admitted.playerId, client);
                }
            }
            response.Reason = accepted ? "" : "Ticket expired, already used, room full or admission unavailable.";
            response.Approved = accepted;
            response.Pending = false;
        }

        private void Connected(ulong client)
        {
            if (!peers.ContainsKey(client) || stopping) return;
            if (world == null) missingSince = -1;
            if (slots.Count != 2 || !network.ConnectedClients.ContainsKey(slots[0]) || !network.ConnectedClients.ContainsKey(slots[1])) return;
            if (world == null) world = new ServerMatchWorld(slots[0], slots[1]);
            else { world.SetConnection(0, slots[0]); world.SetConnection(1, slots[1]); }
            missingSince = -1;
            Time.timeScale = 1;
            Application.targetFrameRate = 60;
            Debug.Log("[PointPass Worker] Two authenticated players; match simulation running.");
        }

        private void Disconnected(ulong client)
        {
            if (!peers.TryGetValue(client, out Peer peer)) return;
            peers.Remove(client); slots.Remove(peer.Slot);
            QueueDeparture(matchId, peer.PlayerId, client);
            if (world != null)
            {
                foreach (var player in world.Players) { player.SetMove(Vector2.zero); player.CancelActions(); }
            }
            if (missingSince < 0) missingSince = Time.unscaledTimeAsDouble;
            Time.timeScale = 0; Application.targetFrameRate = 30;
        }

        private void Update()
        {
            if (network == null || stopping) return;
            double now = Time.unscaledTimeAsDouble;
            RecordFrameMetrics(Time.realtimeSinceStartupAsDouble);
            if (!departureSending && departures.Count > 0 && now >= nextDeparture) StartCoroutine(SendDeparture());
            if (now - lastApiSuccess > 30 && !registering) { apiHealthy = false; Time.timeScale = 0; }
            if (matchId != null && missingSince >= 0 && now - missingSince >= 30 && !submitting)
                StartCoroutine(Finish(false, "Player disconnected."));
            if (world != null)
            {
                foreach (Peer peer in peers.Values)
                    if (now - peer.LastInput > 0.5) { world.Players[peer.Slot].SetMove(Vector2.zero); world.Players[peer.Slot].CancelActions(); }
                if (world.Simulation.IsFullTime && !submitting) StartCoroutine(Finish(true, "Match complete."));
                if (now >= nextSnapshot)
                {
                    nextSnapshot += 1.0 / 30;
                    if (now - nextSnapshot > 0.2) nextSnapshot = now + 1.0 / 30;
                    NetworkMatchState snapshot = world.Capture(++sequence);
                    if (missingSince >= 0 || !apiHealthy)
                    {
                        snapshot.Playing = false;
                        snapshot.Message = missingSince >= 0 ? "Waiting for opponent to reconnect (30 seconds)…" : "Test service unavailable. Match paused.";
                    }
                    byte[] state = OnlineProtocol.EncodeState(snapshot);
                    foreach (var peer in peers)
                        if (network.ConnectedClients.ContainsKey(peer.Key))
                        {
                            OnlineProtocol.Send(network, OnlineProtocol.StateMessage, peer.Key, state, NetworkDelivery.UnreliableSequenced);
                            RecordPayloadSent(peer.Value.Slot, state.Length, false);
                        }
                    snapshots++;
                }
            }
            else if (peers.Count == 0 && pendingApprovals == 0 && File.Exists(Path.Combine(statePath, "tls-reload.request"))) StartCoroutine(ReloadTls());
            if (now >= nextSummary)
            {
                nextSummary = now + 60;
                WriteMetrics(Time.realtimeSinceStartupAsDouble);
            }
        }

        private void FixedUpdate()
        {
            // This counts enabled match-physics callbacks, including ball/net settling.
            // It is not a measurement of the CPU time spent in the physics engine.
            if (world == null || !world.Ball.Body.simulated || Time.timeScale <= 0 || submitting) return;
            activeFixedSteps++; simulationSeconds += Time.fixedDeltaTime;
        }

        private void Reject(Rejection reason)
        {
            rejected++;
            switch (reason)
            {
                case Rejection.Admission: admissionRejected++; break;
                case Rejection.Malformed: malformedRejected++; break;
                case Rejection.Ordering: orderingRejected++; break;
                case Rejection.Action: actionRejected++; break;
                case Rejection.Rate: rateRejected++; break;
                case Rejection.Probe: probeRejected++; break;
                case Rejection.UnknownPeer: unknownPeerRejected++; break;
            }
        }
        private void RecordPayloadSent(int slot, int bytes, bool probe)
        {
            bytesSent += bytes;
            if (probe) probeBytesSentBySlot[slot] += bytes;
            else stateBytesSentBySlot[slot] += bytes;
        }
        private void StartPhysicsRecorder()
        {
            if (physicsRecorder.Valid) return;
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name != "Physics2D.Simulate" || description.UnitType.ToString() != "TimeNanoseconds") continue;
                try { physicsRecorder = ProfilerRecorder.StartNew(description.Category, description.Name, 16, ProfilerRecorderOptions.Default); }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                { Debug.LogWarning("[PointPass Worker] Physics CPU recorder unavailable; no timing will be inferred from frame intervals."); }
                break;
            }
        }
        private void RecordFrameMetrics(double now)
        {
            if (previousFrameAt > 0)
            {
                double interval = Math.Max(0, now - previousFrameAt);
                frameSecondsSum += interval; frameSecondsPeak = Math.Max(frameSecondsPeak, interval); frameSamples++;
            }
            previousFrameAt = now;
            if (!physicsRecorder.Valid) return;
            for (int i = 0; i < physicsRecorder.Count; i++)
            {
                var sample = physicsRecorder.GetSample(i);
                if (sample.Count <= 0 || sample.Value < 0) continue;
                physicsFrameSamples++; physicsCalls += sample.Count;
                physicsNanosecondsSum += sample.Value; physicsNanosecondsPeak = Math.Max(physicsNanosecondsPeak, sample.Value);
            }
            physicsRecorder.Reset();
        }
        private void WriteMetrics(double now)
        {
            double interval = Math.Max(.001, now - summaryAt);
            string physics = !physicsRecorder.Valid ? "physics2dCpuStatus=unavailable"
                : physicsFrameSamples == 0 ? "physics2dCpuStatus=no_samples"
                : FormattableString.Invariant($"physics2dCpuStatus=measured physics2dSampleFrames={physicsFrameSamples} physics2dMarkerCalls={physicsCalls} physics2dSampleFrameCpuMeanMs={physicsNanosecondsSum / (double)physicsFrameSamples / 1000000:F4} physics2dSampleFrameCpuPeakMs={physicsNanosecondsPeak / 1000000d:F4}");
            Debug.Log(FormattableString.Invariant($"[PointPass Worker] metrics peers={peers.Count} simulatedSeconds={simulationSeconds:F2} activeFixedSteps={activeFixedSteps} activeStepsPerWallSecond={(activeFixedSteps - previousStepCount) / interval:F2} sampleWallSeconds={interval:F2} workerWallFrameSamples={frameSamples} workerWallFrameMeanMs={(frameSamples > 0 ? frameSecondsSum / frameSamples * 1000 : 0):F3} workerWallFramePeakMs={frameSecondsPeak * 1000:F3} commands={commands} rejected={rejected} admissionRejected={admissionRejected} malformedRejected={malformedRejected} orderingRejected={orderingRejected} actionRejected={actionRejected} rateRejected={rateRejected} probeRejected={probeRejected} unknownPeerRejected={unknownPeerRejected} waitingHeartbeatsIgnored={waitingHeartbeatsIgnored} closingHeartbeatsIgnored={closingHeartbeatsIgnored} snapshots={snapshots} payloadBytesSent={bytesSent} payloadBytesReceived={bytesReceived} slot0InputRx={inputBytesReceivedBySlot[0]} slot1InputRx={inputBytesReceivedBySlot[1]} slot0ProbeRx={probeBytesReceivedBySlot[0]} slot1ProbeRx={probeBytesReceivedBySlot[1]} slot0StateTx={stateBytesSentBySlot[0]} slot1StateTx={stateBytesSentBySlot[1]} slot0ProbeTx={probeBytesSentBySlot[0]} slot1ProbeTx={probeBytesSentBySlot[1]} ") + physics);
            // Timing samples cover a summary window; payload/rejection totals are
            // worker-lifetime counters. Payload totals exclude NGO/UDP/DTLS overhead,
            // retransmits and HTTP/admission traffic. Frame intervals are not CPU time.
            summaryAt = now; previousStepCount = activeFixedSteps;
            frameSecondsSum = frameSecondsPeak = 0; frameSamples = 0;
            physicsFrameSamples = physicsCalls = physicsNanosecondsSum = physicsNanosecondsPeak = 0;
        }

        private void QueueDeparture(string departedMatch, string player, ulong connection)
        {
            string key = departedMatch + "/" + connection;
            if (!departures.ContainsKey(key) && departures.Count >= 16)
            { Debug.LogError("[PointPass Worker] Lifecycle queue full; stopping for clean supervised recovery."); Application.Quit(5); return; }
            departures[key] = new OnlineApiContracts.Departed { instanceId = instanceId, matchId = departedMatch,
                playerId = player, connectionId = connection.ToString() };
        }

        private IEnumerator SendDeparture()
        {
            departureSending = true;
            string key = null; OnlineApiContracts.Departed departed = null;
            foreach (var pending in departures) { key = pending.Key; departed = pending.Value; break; }
            bool saved = false;
            yield return Post<OnlineApiContracts.Worker>("/internal/players/disconnect", departed, (value, error) => saved = error == null);
            if (saved) departures.Remove(key);
            nextDeparture = Time.unscaledTimeAsDouble + (saved ? 0.25 : 1);
            departureSending = false;
        }

        private void Input(ulong client, FastBufferReader reader)
        {
            if (!peers.TryGetValue(client, out Peer peer)) { Reject(Rejection.UnknownPeer); return; }
            int received = Math.Max(0, reader.Length - reader.Position);
            bytesReceived += received; inputBytesReceivedBySlot[peer.Slot] += received;
            if (world == null || submitting)
            {
                // Do not turn ordinary heartbeats during admission/result cleanup
                // into apparent attacks. Malformed data remains malformed in every phase.
                if (!OnlineProtocol.TryInput(OnlineProtocol.Read(reader, OnlineProtocol.InputBytes), out NetworkInputCommand idle, out _))
                    Reject(Rejection.Malformed);
                else if (idle.Action != NetworkKickAction.None) Reject(Rejection.Action);
                else if (submitting) closingHeartbeatsIgnored++;
                else waitingHeartbeatsIgnored++;
                return;
            }
            double now = Time.unscaledTimeAsDouble;
            if (now - peer.Window >= 1) { peer.Window = now; peer.Count = 0; }
            if (++peer.Count > 120) { Reject(Rejection.Rate); return; }
            if (!OnlineProtocol.TryInput(OnlineProtocol.Read(reader, OnlineProtocol.InputBytes), out NetworkInputCommand command, out uint acknowledged))
            { Reject(Rejection.Malformed); return; }
            if ((peer.HasSequence && unchecked((int)(command.Sequence - peer.Sequence)) <= 0)
                || acknowledged == 0 || unchecked((int)(sequence - acknowledged)) < 0 || unchecked((int)(sequence - acknowledged)) > 90)
            { Reject(Rejection.Ordering); return; }
            peer.Sequence = command.Sequence; peer.HasSequence = true; peer.LastInput = now; commands++;
            if (Time.timeScale == 0 || !world.Simulation.IsPlaying) return;
            var player = world.Players[peer.Slot];
            bool begin = command.Action == NetworkKickAction.BeginPass || command.Action == NetworkKickAction.BeginShot;
            if (begin && (!player.Dribbler.HasBall || player.ChargingKick != PointPass.Players.PlayerKicker.KickType.None))
            { Reject(Rejection.Action); command.Action = NetworkKickAction.None; }
            command.Apply(player);
        }

        private void Probe(ulong client, FastBufferReader reader)
        {
            if (!peers.TryGetValue(client, out Peer peer)) { Reject(Rejection.UnknownPeer); return; }
            int received = Math.Max(0, reader.Length - reader.Position);
            bytesReceived += received; probeBytesReceivedBySlot[peer.Slot] += received;
            double now = Time.unscaledTimeAsDouble;
            if (now - peer.Window >= 1) { peer.Window = now; peer.Count = 0; }
            if (++peer.Count > 125) { Reject(Rejection.Rate); return; }
            if (reader.Length - reader.Position != 8) { Reject(Rejection.Probe); return; }
            OnlineProtocol.Send(network, OnlineProtocol.ProbeMessage, client, OnlineProtocol.Read(reader, 8), NetworkDelivery.Unreliable);
            RecordPayloadSent(peer.Slot, 8, true);
        }

        private IEnumerator Heartbeat()
        {
            while (!stopping)
            {
                yield return new WaitForSecondsRealtime(10);
                yield return Post<OnlineApiContracts.Worker>("/internal/worker/heartbeat", new OnlineApiContracts.Heartbeat { instanceId = instanceId },
                    (value, error) => {
                        apiHealthy = error == null && value != null && value.instanceId == instanceId;
                        if (!apiHealthy) Time.timeScale = 0;
                        else
                        {
                            ReconcileAdmissions(value);
                            if (world != null && slots.Count == 2 && !submitting
                                && network.ConnectedClients.ContainsKey(slots[0]) && network.ConnectedClients.ContainsKey(slots[1])) Time.timeScale = 1;
                        }
                    });
            }
        }

        private void ReconcileAdmissions(OnlineApiContracts.Worker registered)
        {
            if (registered.matchId == null || registered.players == null) return;
            foreach (OnlineApiContracts.Player player in registered.players)
            {
                if (!player.connected || !ulong.TryParse(player.connectionId, out ulong connection)) continue;
                if (peers.TryGetValue(connection, out Peer peer) && peer.PlayerId == player.playerId) continue;
                if (network.PendingClients.ContainsKey(connection)) continue;
                // Redemption may have committed even when its HTTP response was lost.
                // Heartbeats reconcile it without allowing a ticket to be consumed twice.
                if (matchId == null) matchId = registered.matchId;
                if (missingSince < 0) missingSince = Time.unscaledTimeAsDouble;
                QueueDeparture(registered.matchId, player.playerId, connection);
            }
        }

        private IEnumerator Finish(bool result, string reason)
        {
            submitting = true; Time.timeScale = 0;
            string endpoint;
            object payload;
            string outbox = Path.Combine(statePath, "pending-result.json");
            if (result)
            {
                payload = new OnlineApiContracts.Result { instanceId = instanceId, matchId = matchId, blueScore = world.Simulation.BlueScore, redScore = world.Simulation.RedScore };
                File.WriteAllText(outbox + ".tmp", JsonUtility.ToJson(payload));
                File.Move(outbox + ".tmp", outbox);
                endpoint = "/internal/matches/result";
            }
            else { endpoint = "/internal/matches/abandon"; payload = new OnlineApiContracts.Abandon { instanceId = instanceId, matchId = matchId, reason = "player_disconnected" }; }
            bool saved = false;
            while (!saved && !stopping)
            {
                yield return Post<OnlineApiContracts.Worker>(endpoint, payload, (value, error) => saved = error == null);
                if (!saved) yield return new WaitForSecondsRealtime(5);
            }
            if (!saved) yield break;
            if (result && File.Exists(outbox)) File.Delete(outbox);
            yield return new WaitForSecondsRealtime(result ? 4 : 0);
            var connected = new List<ulong>(peers.Keys);
            foreach (ulong client in connected) network.DisconnectClient(client, reason);
            peers.Clear(); slots.Clear();
            world?.Dispose(); world = null; matchId = null; missingSince = -1; submitting = false;
            Application.targetFrameRate = 30;
        }

        private IEnumerator ReloadTls()
        {
            stopping = true;
            if (heartbeat != null) StopCoroutine(heartbeat);
            network.Shutdown();
            while (network.ShutdownInProgress || network.IsListening) yield return null;
            Destroy(network.gameObject); yield return null;
            File.Delete(Path.Combine(statePath, "tls-reload.request"));
            stopping = false; StartTransport(); heartbeat = StartCoroutine(Heartbeat());
            Debug.Log("[PointPass Worker] Renewed certificate loaded while idle.");
        }

        private IEnumerator Post<T>(string path, object payload, Action<T, string> completed) where T : class
            => OnlineHttp.Post<T>(api + path, payload, null, workerKey, (value, error) => {
                if (error == null) lastApiSuccess = Time.unscaledTimeAsDouble;
                completed(value, error);
            }, 3);

        private static bool ValidTicket(string ticket)
        {
            if (ticket == null || ticket.Length != 43) return false;
            foreach (char value in ticket) if (!(value >= 'A' && value <= 'Z') && !(value >= 'a' && value <= 'z')
                && !(value >= '0' && value <= '9') && value != '-' && value != '_') return false;
            return true;
        }

        private void OnApplicationQuit()
        {
            stopping = true;
            if (network != null) network.Shutdown();
            world?.Dispose();
        }
        private void OnDestroy() { if (physicsRecorder.Valid) physicsRecorder.Dispose(); }
#endif
    }
}
