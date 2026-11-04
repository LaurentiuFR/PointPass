using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;

namespace PointPass.Networking
{
    /// <summary>Read-only runtime measurements. Never changes simulation timing or ownership.</summary>
    public sealed class NetworkDiagnostics
    {
        private struct LossWindow { public double At; public ulong Expected; public ulong Lost; }
        private readonly Queue<LossWindow> lossWindows = new Queue<LossWindow>();
        private int frames;
        private double sampledAt;
        private uint previousSequence;
        private bool hasSequence;
        private ulong expected;
        private ulong lost;
        private int received;
        private double previousPing = -1;
        private double snapshotAt;
        public float Fps { get; private set; }
        public float FrameMilliseconds { get; private set; }
        public bool HasPeer { get; private set; }
        public bool HasPing { get; private set; }
        public float PingMilliseconds { get; private set; }
        public float JitterMilliseconds { get; private set; }
        public float PacketLossPercent { get; private set; }
        public float UploadKbps { get; private set; }
        public float DownloadKbps { get; private set; }
        public ulong PacketsSent { get; private set; }
        public ulong PacketsReceived { get; private set; }
        public float SnapshotHz { get; private set; }
        public float SnapshotLossPercent { get; private set; }
        public bool HasSnapshot => hasSequence;
        public float SnapshotAgeMilliseconds => hasSequence ? (float)((Time.unscaledTimeAsDouble - snapshotAt) * 1000) : 0;

        public void ResetConnection()
        {
            HasPeer = HasPing = hasSequence = false;
            previousPing = -1;
            PingMilliseconds = JitterMilliseconds = PacketLossPercent = 0;
            UploadKbps = DownloadKbps = SnapshotHz = SnapshotLossPercent = 0;
            PacketsSent = PacketsReceived = expected = lost = 0;
            received = 0;
            lossWindows.Clear();
        }

        public void ObservePing(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0 || milliseconds > 10000) return;
            if (previousPing >= 0)
                JitterMilliseconds += ((float)System.Math.Abs(milliseconds - previousPing) - JitterMilliseconds) / 16f;
            previousPing = milliseconds;
            PingMilliseconds = (float)milliseconds;
            HasPing = true;
        }

        public void ObserveSnapshot(uint sequence)
        {
            uint difference = hasSequence ? unchecked(sequence - previousSequence) : 1;
            if (hasSequence && unchecked((int)difference) <= 0) return;
            expected += difference;
            lost += difference - 1;
            received++;
            previousSequence = sequence;
            hasSequence = true;
            snapshotAt = Time.unscaledTimeAsDouble;
        }

        public void Tick(NetworkManager manager, UnityTransport transport, bool connected, ulong peerId)
        {
            frames++;
            double now = Time.unscaledTimeAsDouble;
            double elapsed = now - sampledAt;
            if (elapsed < 0.5) return;
            Fps = (float)(frames / elapsed);
            FrameMilliseconds = (float)(elapsed * 1000 / frames);
            frames = 0;
            sampledAt = now;
            SnapshotHz = (float)(received / elapsed);
            received = 0;
            lossWindows.Enqueue(new LossWindow { At = now, Expected = expected, Lost = lost });
            expected = lost = 0;
            while (lossWindows.Count > 0 && lossWindows.Peek().At < now - 5) lossWindows.Dequeue();
            ulong total = 0, missing = 0;
            foreach (LossWindow sample in lossWindows) { total += sample.Expected; missing += sample.Lost; }
            SnapshotLossPercent = total > 0 ? 100f * missing / total : 0;
            HasPeer = false;
            if (!connected || manager == null || transport == null) return;
            ref NetworkDriver driver = ref transport.GetNetworkDriver();
            if (!driver.IsCreated) return;
            ulong transportId = manager.GetTransportIdFromClientId(peerId);
            if (transportId == ulong.MaxValue || UnsafeUtility.SizeOf<NetworkConnection>() != sizeof(ulong)) return;
            // NGO's UnityTransport encodes this handle in its ulong transport ID.
            // Match the installed transport's conversion without private reflection or unsafe blocks.
            NetworkConnection connection = UnsafeUtility.As<ulong, NetworkConnection>(ref transportId);
            if (driver.GetConnectionState(connection) != NetworkConnection.State.Connected) return;
            var connectionStats = driver.GetConnectionStatistics(connection);
            var driverStats = driver.GetStatistics();
            // UTP's cumulative reliable-pipeline proxy includes resends caused by delay.
            // It is not an independent measurement of IP packet loss; snapshot gaps
            // separately measure missing gameplay state on the receiving client.
            PacketLossPercent = connectionStats.PacketLossPercent;
            UploadKbps = driverStats.TxBandwidth.Current;
            DownloadKbps = driverStats.RxBandwidth.Current;
            PacketsSent = driverStats.TxTotalPackets;
            PacketsReceived = driverStats.RxTotalPackets;
            HasPeer = true;
        }
    }
}
