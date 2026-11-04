using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PointPass.Networking;
using PointPass.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PointPass.Online
{
    /// <summary>Menu-independent Windows pilot connection flow; the menu only calls Open/Join/Leave.</summary>
    public sealed class OnlinePilotClient : MonoBehaviour
    {
        public const string ApiBase = "https://62.238.115.207";
        public static OnlinePilotClient Instance { get; private set; }
        public string Status { get; private set; } = "Private 1v1 pilot. Both players connect to the dedicated server.";
        public bool Busy { get; private set; }
        public bool HasSession => network != null;
        public bool IsPlaying => World != null;
        public int LocalSlot => admission != null ? admission.slot : -1;
        public bool PanelOpen { get; private set; }
        public MatchMenu Menu { get; private set; }
        public NetworkMatchWorld World { get; private set; }
        public NetworkMatchState LatestState { get; private set; }
        public NetworkDiagnostics Diagnostics { get; } = new NetworkDiagnostics();
        public event Action<NetworkMatchState> StateReceived;
        private readonly List<NetworkMatchState> snapshots = new List<NetworkMatchState>(32);
        private GameObject offline;
        private NetworkManager network;
        private UnityTransport transport;
        private OnlineApiContracts.Guest guest;
        private OnlineApiContracts.Admission admission;
        private string credential;
        private double latestArrival, nextSend, connectingAt, probeSentAt = -10;
        private float previousScale;
        private bool priorBackground, closing, waitingProbe;

        public static void Attach(GameObject offline, MatchMenu menu)
        {
#if !UNITY_SERVER
            if (Instance != null) return;
            var client = new GameObject("Online Pilot Connection").AddComponent<OnlinePilotClient>();
            client.offline = offline; client.Menu = menu; Instance = client;
            client.gameObject.AddComponent<OnlinePilotPanel>().Init(client);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            OnlinePilotTestDriver.TryAttach(client);
#endif
#endif
        }

        public void Open() { PanelOpen = true; }
        public void ClosePanel() { if (!Busy && !HasSession) PanelOpen = false; }
        public void Join(string invitation, string displayName)
        {
            if (Busy || HasSession) return;
            if (NetworkManager.Singleton != null)
            { Status = "Leave the development network session before joining Online Test."; PanelOpen = true; return; }
            StartCoroutine(JoinRoutine(invitation.Trim(), displayName.Trim()));
        }

        private IEnumerator JoinRoutine(string invitation, string displayName)
        {
            Busy = true; PanelOpen = true;
            Status = "Signing in to the private test service…";
            string error = null;
            try { credential = OnlineGuestStore.LoadOrCreate(ProfileArgument()); }
            catch (Exception exception) when (exception is System.IO.IOException || exception is PlatformNotSupportedException || exception is ArgumentException)
            { error = exception.Message; }
            if (error != null) { Status = error; Busy = false; yield break; }
            if (guest == null || !DateTime.TryParse(guest.expiresUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiry) || expiry.ToUniversalTime() < DateTime.UtcNow.AddMinutes(1))
            {
                yield return OnlineHttp.Post<OnlineApiContracts.Guest>(ApiBase + "/v1/guests/session", new OnlineApiContracts.SessionRequest { credential = credential },
                    null, null, (value, failure) => { guest = value; error = failure; });
                if (guest == null && invitation.Length > 0)
                    yield return OnlineHttp.Post<OnlineApiContracts.Guest>(ApiBase + "/v1/guests", new OnlineApiContracts.GuestRequest {
                        credential = credential, invitation = invitation, displayName = displayName }, null, null,
                        (value, failure) => { guest = value; error = failure; });
            }
            if (guest == null) { Status = invitation.Length == 0 ? "Enter your pilot invitation for the first sign-in. Existing guests can leave it blank." : error; Busy = false; yield break; }
            Status = "Reserving your place in the test room…";
            yield return OnlineHttp.Post<OnlineApiContracts.Admission>(ApiBase + "/v1/room/join", new OnlineApiContracts.JoinRequest(), guest.accessToken, null,
                (value, failure) => { admission = value; error = failure; });
            if (admission == null) { Status = error; Busy = false; yield break; }
            if (admission.slot < 0 || admission.slot > 1 || admission.host != "62.238.115.207" || admission.tlsServerName != admission.host
                || admission.port != 7777 || string.IsNullOrEmpty(admission.ticket))
            { Status = "The server returned an invalid room configuration."; Busy = false; yield break; }
            if (NetworkManager.Singleton != null)
            { Status = "Leave the development network session before joining Online Test."; Busy = false; yield break; }
            priorBackground = Application.runInBackground; Application.runInBackground = true;
            previousScale = Time.timeScale; Time.timeScale = 1;
            offline.SetActive(false); Menu.Hide();
            var root = new GameObject("Online encrypted transport");
            transport = root.AddComponent<UnityTransport>();
            transport.UseEncryption = true;
            transport.SetClientSecrets(admission.tlsServerName);
            transport.SetConnectionData(admission.host, (ushort)admission.port);
            network = root.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ForceSamePrefabs = false,
                ConnectionApproval = true, TickRate = 30, ClientConnectionBufferTimeout = 10,
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new OnlineApiContracts.Approval { ticket = admission.ticket })) };
            admission.ticket = null;
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnTransportFailure += Failed;
            connectingAt = Time.unscaledTimeAsDouble;
            Status = "Connecting securely to the dedicated server…";
            if (!network.StartClient()) { Busy = false; Leave("Could not start the encrypted connection."); yield break; }
            network.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.StateMessage, ReceiveState);
            network.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.ProbeMessage, Probe);
        }

        private void Connected(ulong client)
        {
            if (network == null || client != network.LocalClientId || closing) return;
            Busy = false; network.NetworkConfig.ConnectionData = Array.Empty<byte>();
            Status = "Connected as " + (admission.slot == 0 ? "BLUE" : "RED") + ". Waiting for the other player…";
            Diagnostics.ResetConnection();
        }

        private void Disconnected(ulong client)
        {
            if (closing || network == null || client != network.LocalClientId) return;
            string reason = network.DisconnectReason;
            Leave(string.IsNullOrWhiteSpace(reason) ? "Connection lost. Rejoin within 30 seconds to resume your match." : reason);
        }
        private void Failed() { if (!closing) Leave("Encrypted connection failed. Check internet access and try again."); }

        private void Update()
        {
            Diagnostics.Tick(network, transport, network != null && network.IsConnectedClient, NetworkManager.ServerClientId);
            if (network == null || closing) return;
            double now = Time.unscaledTimeAsDouble;
            if (!network.IsConnectedClient && now - connectingAt > 15) { Leave("Connection timed out. Obtain a fresh ticket and try again."); return; }
            if (!network.IsConnectedClient) return;
            if (World != null && now >= nextSend)
            {
                nextSend += 1.0 / 30;
                if (now - nextSend > 0.2) nextSend = now + 1.0 / 30;
                World.ClientInput.SendState();
            }
            if (now - probeSentAt >= 1)
            {
                probeSentAt = now; waitingProbe = true;
                OnlineProtocol.Send(network, OnlineProtocol.ProbeMessage, NetworkManager.ServerClientId, BitConverter.GetBytes(now), NetworkDelivery.Unreliable);
            }
        }

        private void LateUpdate()
        {
            if (World == null || snapshots.Count == 0) return;
            double target = snapshots[snapshots.Count - 1].ServerTime + Time.unscaledTimeAsDouble - latestArrival - 0.1;
            while (snapshots.Count > 2 && snapshots[1].ServerTime <= target) snapshots.RemoveAt(0);
            NetworkMatchState a = snapshots[0], b = snapshots.Count > 1 ? snapshots[1] : a;
            float t = b.ServerTime > a.ServerTime ? Mathf.Clamp01((float)((target - a.ServerTime) / (b.ServerTime - a.ServerTime))) : 1;
            NetworkMatchState frame = NetworkMatchState.Blend(a, b, t);
            // Discrete ownership/score/charge cues switch at their recorded sample, not early.
            if (t < 1 && a.ResetVersion == b.ResetVersion)
            {
                frame.OwnerSlot = a.OwnerSlot; frame.BlueScore = a.BlueScore; frame.RedScore = a.RedScore;
                frame.Playing = a.Playing; frame.FullTime = a.FullTime; frame.Message = a.Message;
                frame.Half = a.Half; frame.Overtime = a.Overtime;
                frame.Blue.KickSerial = a.Blue.KickSerial; frame.Blue.LastKick = a.Blue.LastKick;
                frame.Red.KickSerial = a.Red.KickSerial; frame.Red.LastKick = a.Red.LastKick;
                frame.Blue.Charging = a.Blue.Charging; frame.Blue.Charge = a.Blue.Charge; frame.Blue.Curve = a.Blue.Curve; frame.Blue.Celebrating = a.Blue.Celebrating;
                frame.Red.Charging = a.Red.Charging; frame.Red.Charge = a.Red.Charge; frame.Red.Curve = a.Red.Curve; frame.Red.Celebrating = a.Red.Celebrating;
            }
            World.Present(frame);
        }

        private void Send(NetworkInputCommand command)
        {
            if (network == null || closing || !network.IsConnectedClient || LatestState.Sequence == 0) return;
            OnlineProtocol.Send(network, OnlineProtocol.InputMessage, NetworkManager.ServerClientId,
                OnlineProtocol.EncodeInput(command, LatestState.Sequence), NetworkDelivery.ReliableSequenced);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SendValidationInput(byte[] payload)
        {
            if (payload == null || payload.Length > 64 || network == null || closing || !network.IsConnectedClient) return;
            OnlineProtocol.Send(network, OnlineProtocol.InputMessage, NetworkManager.ServerClientId, payload, NetworkDelivery.ReliableSequenced);
        }
#endif

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (closing || sender != NetworkManager.ServerClientId || !OnlineProtocol.TryState(OnlineProtocol.Read(reader, 384), out NetworkMatchState state)) return;
            if (snapshots.Count > 0 && unchecked((int)(state.Sequence - LatestState.Sequence)) <= 0) return;
            if (World == null)
            {
                World = new NetworkMatchWorld(Send, false, network.LocalClientId, admission.slot);
                PanelOpen = false;
            }
            if (snapshots.Count > 0 && state.ResetVersion != LatestState.ResetVersion)
            { snapshots.Clear(); World.Ball.GetComponentInChildren<TrailRenderer>()?.Clear(); }
            LatestState = state; Diagnostics.ObserveSnapshot(state.Sequence);
            snapshots.Add(state); if (snapshots.Count > 32) snapshots.RemoveAt(0);
            latestArrival = Time.unscaledTimeAsDouble;
            Status = "Connected · " + (admission.slot == 0 ? "BLUE" : "RED") + " · Dedicated server";
            StateReceived?.Invoke(state);
        }

        private void Probe(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != 8) return;
            double stamp = BitConverter.ToDouble(OnlineProtocol.Read(reader, 8), 0);
            if (waitingProbe && stamp == probeSentAt) { Diagnostics.ObservePing((Time.unscaledTimeAsDouble - stamp) * 1000); waitingProbe = false; }
        }

        public void Leave(string reason = "You left the test room.")
        {
            if (closing) return;
            Status = reason; PanelOpen = true; StartCoroutine(CloseSession());
        }
        private IEnumerator CloseSession()
        {
            closing = true; Busy = true;
            World?.Dispose(); World = null; snapshots.Clear(); LatestState = default;
            if (network != null)
            {
                network.OnClientConnectedCallback -= Connected; network.OnClientDisconnectCallback -= Disconnected; network.OnTransportFailure -= Failed;
                network.Shutdown();
                while (network != null && (network.IsListening || network.ShutdownInProgress)) yield return null;
                if (network != null) Destroy(network.gameObject);
                network = null; transport = null; yield return null;
            }
            if (admission != null && guest != null)
                yield return OnlineHttp.Post<OnlineApiContracts.Worker>(ApiBase + "/v1/room/leave", new OnlineApiContracts.LeaveRequest { matchId = admission.matchId },
                    guest.accessToken, null, (value, error) => { });
            admission = null; Diagnostics.ResetConnection();
            Application.runInBackground = priorBackground; Time.timeScale = previousScale;
            if (offline != null) offline.SetActive(true);
            Menu?.ShowMain(); Busy = false; closing = false;
        }

        private static string ProfileArgument()
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-pointpassProfile");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "default";
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            closing = true; World?.Dispose();
            if (network != null) { network.Shutdown(true); Destroy(network.gameObject); }
        }
    }
}
