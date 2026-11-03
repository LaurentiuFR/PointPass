using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PointPass.Networking
{
    /// <summary>Connection lifecycle and transport only; gameplay executes in the authoritative world.</summary>
    public sealed class NetworkSession : MonoBehaviour
    {
        private const string InputMessage = "PointPass/Input/2";
        private const string StateMessage = "PointPass/State/2";
        public const string Protocol = "PointPass-PC-2";
        private const string ProbeMessage = "PointPass/Diagnostics/1";
        private NetworkManager manager;
        private UnityTransport transport;
        private ulong peerId;
        private double probeSentAt = -10;
        private bool awaitingProbe;
        private readonly HashSet<ulong> reservations = new HashSet<ulong>();
        private readonly List<NetworkMatchState> snapshots = new List<NetworkMatchState>();
        private bool closing;
        private bool host;
        private float connectingSince;
        private double nextSend;
        private double latestArrival;
        private double lastInput;
        private double rateWindow;
        private int inputCount;
        private uint lastInputSequence;
        private uint stateSequence;
        private bool hasInput;
        private bool hadBackground;
        private bool previousRunInBackground;
        public string Status { get; private set; } = "Offline development test — host or join.";
        public bool Busy => closing;
        public bool IsHost => host && manager != null && manager.IsListening;
        public bool IsConnected => !closing && manager != null && manager.IsConnectedClient;
        public bool HasSession => manager != null;
        public NetworkMatchWorld World { get; private set; }
        public NetworkMatchState LatestState { get; private set; }
        public NetworkDiagnostics Diagnostics { get; } = new NetworkDiagnostics();
        public event Action<NetworkMatchState> StateReceived;

        public void Host(ushort port) => StartSession(true, "127.0.0.1", port);
        public void Join(string address, ushort port) => StartSession(false, address, port);

        private void StartSession(bool asHost, string address, ushort port)
        {
            if (HasSession || closing) return;
            if (port == 0 || !IPAddress.TryParse(address, out IPAddress parsed)
                || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { Status = "Enter a valid IPv4 address and port (1–65535)."; return; }
            host = asHost;
            hadBackground = true;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            GameObject networking = new GameObject("PointPass Network Transport");
            transport = networking.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, port, asHost ? "0.0.0.0" : null);
            manager = networking.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, EnableSceneManagement = false,
                ForceSamePrefabs = false, ConnectionApproval = true, TickRate = 30,
                ConnectionData = Encoding.UTF8.GetBytes(Protocol + "/" + Application.version)
            };
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            manager.OnClientStopped += ClientStopped;
            connectingSince = Time.unscaledTime;
            Status = asHost ? $"Hosting on UDP {port}. Waiting for one player…" : $"Connecting to {address}:{port}…";
            bool started = asHost ? manager.StartHost() : manager.StartClient();
            if (!started) { Disconnect("Could not start networking. Check the address and whether the port is already in use."); return; }
            manager.CustomMessagingManager.RegisterNamedMessageHandler(InputMessage, ReceiveInput);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ProbeMessage, ReceiveProbe);
            Debug.Log("[PointPass Network] " + Status);
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool compatible = Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>()) == Protocol + "/" + Application.version;
            bool full = request.ClientNetworkId != NetworkManager.ServerClientId && reservations.Count >= 1;
            response.Approved = compatible && !full;
            response.CreatePlayerObject = false;
            response.Reason = !compatible ? "Incompatible PointPass game version. Use the same build." : full ? "Server full (2/2 players)." : string.Empty;
            if (response.Approved && request.ClientNetworkId != NetworkManager.ServerClientId) reservations.Add(request.ClientNetworkId);
        }

        private void Connected(ulong clientId)
        {
            if (closing) return;
            if (host && clientId == NetworkManager.ServerClientId) return;
            if (!host && clientId != manager.LocalClientId) return;
            World?.Dispose();
            World = new NetworkMatchWorld(this, host, host ? clientId : manager.LocalClientId);
            peerId = host ? clientId : NetworkManager.ServerClientId;
            Diagnostics.ResetConnection();
            awaitingProbe = false;
            probeSentAt = -10;
            snapshots.Clear();
            hasInput = false;
            lastInput = Time.unscaledTimeAsDouble;
            stateSequence = 0;
            nextSend = 0;
            Status = host ? "Connected · BLUE · authoritative host" : "Connected · RED · client";
            Debug.Log("[PointPass Network] " + Status);
        }

        private void Disconnected(ulong clientId)
        {
            bool occupied = reservations.Remove(clientId);
            if (closing) return;
            if (host && clientId != NetworkManager.ServerClientId && !occupied) return;
            if (host && clientId != NetworkManager.ServerClientId)
            {
                World?.Dispose(); World = null;
                snapshots.Clear();
                LatestState = default;
                Diagnostics.ResetConnection();
                Status = "Client disconnected. Hosting; waiting for one player…";
            }
            else if (!host)
            {
                string reason = manager.DisconnectReason;
                Disconnect(string.IsNullOrWhiteSpace(reason) ? "Host disconnected or connection lost." : reason);
            }
        }

        private void ClientStopped(bool wasHost)
        {
            if (!closing && manager != null) Disconnect("Connection stopped. Check the host address and try again.");
        }

        private void TransportFailed() => Disconnect("Transport failure. Check the connection and port, then host or join again.");

        public void Disconnect(string reason = "Disconnected.")
        {
            if (closing) return;
            Status = reason;
            StartCoroutine(CloseSession());
        }

        private IEnumerator CloseSession()
        {
            closing = true;
            World?.Dispose(); World = null;
            snapshots.Clear();
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= Connected;
                manager.OnClientDisconnectCallback -= Disconnected;
                manager.OnTransportFailure -= TransportFailed;
                manager.OnClientStopped -= ClientStopped;
                manager.Shutdown();
                while (manager != null && (manager.ShutdownInProgress || manager.IsListening)) yield return null;
                if (manager != null) Destroy(manager.gameObject);
                manager = null;
                yield return null;
            }
            reservations.Clear();
            Diagnostics.ResetConnection();
            transport = null;
            LatestState = default;
            if (hadBackground) Application.runInBackground = previousRunInBackground;
            hadBackground = false;
            closing = false;
            Debug.Log("[PointPass Network] " + Status);
        }

        private void Update()
        {
            Diagnostics.Tick(manager, transport, !closing && World != null && IsConnected, peerId);
            if (closing || manager == null) return;
            if (!host && !IsConnected && Time.unscaledTime - connectingSince > 12f)
            { Disconnect("Connection timed out. Check the host IPv4 address, port and firewall."); return; }
            if (World == null) return;
            if (Time.unscaledTimeAsDouble - probeSentAt >= 1)
            {
                probeSentAt = Time.unscaledTimeAsDouble;
                awaitingProbe = true;
                SendProbe(probeSentAt, false);
            }
            if (host)
            {
                if (Time.unscaledTimeAsDouble - lastInput > 0.5)
                {
                    World.Players[1].SetMove(Vector2.zero);
                    World.Players[1].CancelActions();
                }
            }
            if (Time.unscaledTimeAsDouble >= nextSend)
            {
                nextSend = Time.unscaledTimeAsDouble + 1.0 / 30.0;
                if (host) BroadcastState(); else World.ClientInput.SendState();
            }
        }

        private void LateUpdate()
        {
            if (host || World == null || snapshots.Count == 0) return;
            double target = snapshots[snapshots.Count - 1].ServerTime + Time.unscaledTimeAsDouble - latestArrival - 0.1;
            while (snapshots.Count > 2 && snapshots[1].ServerTime <= target) snapshots.RemoveAt(0);
            NetworkMatchState a = snapshots[0];
            NetworkMatchState b = snapshots.Count > 1 ? snapshots[1] : a;
            float t = b.ServerTime > a.ServerTime ? (float)((target - a.ServerTime) / (b.ServerTime - a.ServerTime)) : 1f;
            World.Present(NetworkMatchState.Blend(a, b, Mathf.Clamp01(t)));
        }

        public void SendInput(NetworkInputCommand command)
        {
            if (host || closing || !IsConnected) return;
            using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteValueSafe(command);
                manager.CustomMessagingManager.SendNamedMessage(InputMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        private void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (!host || closing || World == null || !reservations.Contains(sender)) return;
            if (reader.Length - reader.Position != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<NetworkInputCommand>()) return;
            reader.ReadValueSafe(out NetworkInputCommand command);
            if (!command.IsValid || (hasInput && unchecked((int)(command.Sequence - lastInputSequence)) <= 0)) return;
            double now = Time.unscaledTimeAsDouble;
            if (now - rateWindow >= 1.0) { rateWindow = now; inputCount = 0; }
            if (++inputCount > 120) return;
            hasInput = true;
            lastInputSequence = command.Sequence;
            lastInput = now;
            command.Apply(World.Players[1]);
        }

        private void BroadcastState()
        {
            LatestState = World.Capture(++stateSequence);
            using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteValueSafe(LatestState);
                foreach (ulong client in reservations)
                    if (manager.ConnectedClients.ContainsKey(client))
                        manager.CustomMessagingManager.SendNamedMessage(StateMessage, client, writer, NetworkDelivery.UnreliableSequenced);
            }
            StateReceived?.Invoke(LatestState);
        }

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (host || closing || World == null || sender != NetworkManager.ServerClientId) return;
            if (reader.Length - reader.Position != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<NetworkMatchState>()) return;
            reader.ReadValueSafe(out NetworkMatchState state);
            if (snapshots.Count > 0 && unchecked((int)(state.Sequence - LatestState.Sequence)) <= 0) return;
            if (state.OwnerSlot < -1 || state.OwnerSlot > 1) return;
            if (snapshots.Count > 0 && state.ResetVersion != LatestState.ResetVersion)
            {
                snapshots.Clear();
                World.Ball.GetComponentInChildren<TrailRenderer>()?.Clear();
            }
            LatestState = state;
            Diagnostics.ObserveSnapshot(state.Sequence);
            snapshots.Add(state);
            if (snapshots.Count > 32) snapshots.RemoveAt(0);
            latestArrival = Time.unscaledTimeAsDouble;
            StateReceived?.Invoke(state);
        }

        public void RestartMatch() { if (IsHost && World != null) World.Simulation.StartMatch(); }

        private void SendProbe(double stamp, bool reply)
        {
            using (FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(stamp);
                writer.WriteValueSafe(reply);
                manager.CustomMessagingManager.SendNamedMessage(ProbeMessage, peerId, writer, NetworkDelivery.Unreliable);
            }
        }

        private void ReceiveProbe(ulong sender, FastBufferReader reader)
        {
            if (closing || World == null || sender != peerId || reader.Length - reader.Position != sizeof(double) + sizeof(bool)) return;
            reader.ReadValueSafe(out double stamp);
            reader.ReadValueSafe(out bool reply);
            if (double.IsNaN(stamp) || double.IsInfinity(stamp)) return;
            if (!reply) SendProbe(stamp, true);
            else if (awaitingProbe && stamp == probeSentAt)
            {
                Diagnostics.ObservePing((Time.unscaledTimeAsDouble - stamp) * 1000);
                awaitingProbe = false;
            }
        }

        private void OnDestroy()
        {
            closing = true;
            World?.Dispose();
            if (manager != null) { manager.Shutdown(true); Destroy(manager.gameObject); }
            if (hadBackground) Application.runInBackground = previousRunInBackground;
        }
    }
}
