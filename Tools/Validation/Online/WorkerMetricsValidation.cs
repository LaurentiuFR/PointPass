using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PointPass.Gameplay;
using PointPass.Networking;
using PointPass.Online;
using PointPass.Server;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Disabled-component fixtures for worker metric classification; never starts transport or admission HTTP.</summary>
    public static class WorkerMetricsValidation
    {
        private static string Report => Path.GetFullPath("Logs/OnlinePilotVerification/worker-metrics-tests.txt");
        private static int checks;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Private).Invoke(target, arguments);
        private static long Count(object target, string name) => (long)Get(target, name);
        private static void Check(bool value, string label)
        { File.AppendAllText(Report, (value ? "PASS " : "FAIL ") + label + "\n"); if (!value) throw new InvalidOperationException(label); checks++; }
        private static void Message(OnlineMatchWorker worker, string method, ulong client, byte[] payload)
        {
            using (var reader = new FastBufferReader(payload, Allocator.Temp)) Invoke(worker, method, client, reader);
        }
        private static byte[] Input(uint sequence = 1, uint snapshot = 0, NetworkKickAction action = NetworkKickAction.None) =>
            OnlineProtocol.EncodeInput(new NetworkInputCommand { Sequence = sequence, Aim = Vector2.right, Action = action }, snapshot);
        private static object Peer(OnlineMatchWorker worker, ulong client, int slot)
        {
            var kind = typeof(OnlineMatchWorker).GetNestedType("Peer", BindingFlags.NonPublic);
            var peer = Activator.CreateInstance(kind, true);
            kind.GetField("Slot").SetValue(peer, slot);
            kind.GetField("Window").SetValue(peer, Time.unscaledTimeAsDouble);
            ((IDictionary)Get(worker, "peers")).Add(client, peer);
            return peer;
        }
        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report)); File.WriteAllText(Report, "Worker metric classification fixtures; no network connection or credentials\n"); checks = 0;
            var previous = UnityEngine.Object.FindObjectsByType<MatchManager>(FindObjectsSortMode.None);
            foreach (var manager in previous) manager.gameObject.SetActive(false);
            float scale = Time.timeScale; GameObject root = new GameObject("Disabled metrics fixture"); root.SetActive(false);
            var worker = root.AddComponent<OnlineMatchWorker>(); ServerMatchWorld world = null;
            try
            {
                Time.timeScale = 1;
                var peer = Peer(worker, 1, 0); var kind = peer.GetType();
                Message(worker, "Input", 1, new byte[] { 1, 2, 3 });
                Check(Count(worker, "malformedRejected") == 1 && Count(worker, "waitingHeartbeatsIgnored") == 0, "Malformed waiting input remains a malformed rejection");
                long rejected = Count(worker, "rejected");
                Message(worker, "Input", 1, Input());
                Check(Count(worker, "waitingHeartbeatsIgnored") == 1 && Count(worker, "rejected") == rejected, "Authenticated well-formed waiting heartbeat is ignored without a rejection");
                Check(!(bool)kind.GetField("HasSequence").GetValue(peer), "Waiting heartbeat does not consume the active input sequence");
                Set(worker, "submitting", true);
                Message(worker, "Input", 1, Input());
                Check(Count(worker, "closingHeartbeatsIgnored") == 1 && Count(worker, "rejected") == rejected, "Normal closing heartbeat is counted separately from rejected commands");
                Message(worker, "Input", 1, new byte[] { 1, 2 });
                Check(Count(worker, "malformedRejected") == 2, "Malformed input during closing is still rejected as malformed");
                Message(worker, "Input", 1, Input(action: NetworkKickAction.BeginShot));
                Check(Count(worker, "actionRejected") == 1 && Count(worker, "closingHeartbeatsIgnored") == 1, "Closing kick is not mislabelled as a normal heartbeat");
                Message(worker, "Input", 999, Input());
                Check(Count(worker, "unknownPeerRejected") == 1, "Unknown peer traffic has its own rejection category");

                Set(worker, "submitting", false); world = new ServerMatchWorld(11, 22); Set(worker, "world", world); Set(worker, "sequence", (uint)120);
                var timer = System.Diagnostics.Stopwatch.StartNew(); while (!world.Simulation.IsMatchLive && timer.Elapsed.TotalSeconds < 4) await Task.Delay(20);
                Check(world.Simulation.IsMatchLive, "Real local authoritative fixture reaches normal play");
                Message(worker, "Input", 1, Input(1, 120));
                Check(Count(worker, "commands") == 1, "Well-formed current-sequence input remains accepted");
                Message(worker, "Input", 1, Input(1, 120));
                Check(Count(worker, "orderingRejected") == 1 && Count(worker, "commands") == 1, "Duplicate input is classified without advancing accepted commands");
                Message(worker, "Input", 1, Input(2, 121));
                Message(worker, "Input", 1, Input(2, 0));
                Message(worker, "Input", 1, Input(2, 20));
                Check(Count(worker, "orderingRejected") == 4, "Future, missing and stale snapshot acknowledgements are ordering rejections");
                var malformed = Input(2, 120); malformed[malformed.Length - 1] = 255; Message(worker, "Input", 1, malformed);
                Check(Count(worker, "malformedRejected") == 3, "Unknown action encoding remains malformed");
                Message(worker, "Input", 1, Input(2, 120, NetworkKickAction.BeginShot));
                Check(Count(worker, "actionRejected") == 2 && world.Players[0].ChargingKick == PointPass.Players.PlayerKicker.KickType.None, "Non-owner action has its own rejection category and cannot start a charge");
                kind.GetField("Count").SetValue(peer, 120); kind.GetField("Window").SetValue(peer, Time.unscaledTimeAsDouble);
                Message(worker, "Input", 1, Input(3, 120));
                Check(Count(worker, "rateRejected") == 1, "Input rate overflow is distinguished from malformed payloads");
                kind.GetField("Count").SetValue(peer, 0);
                Message(worker, "Probe", 1, new byte[7]);
                Check(Count(worker, "probeRejected") == 1, "Malformed probe has its own rejection category");
                kind.GetField("Count").SetValue(peer, 125);
                Message(worker, "Probe", 1, new byte[8]);
                Check(Count(worker, "rateRejected") == 2, "Probe rate overflow is a rate rejection");
                var request = new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 99, Payload = new byte[0] };
                var response = new NetworkManager.ConnectionApprovalResponse(); Invoke(worker, "Approve", request, response);
                Check(Count(worker, "admissionRejected") == 1 && !response.Approved, "Unavailable admission is counted independently without issuing an HTTP request");

                Peer(worker, 2, 1); Message(worker, "Input", 2, Input(1, 120));
                var received = (long[])Get(worker, "inputBytesReceivedBySlot"); var probes = (long[])Get(worker, "probeBytesReceivedBySlot");
                Check(received[0] > received[1] && received[1] == OnlineProtocol.InputBytes && probes[0] == 15 && probes[1] == 0, "Per-slot application payload accounting separates inputs and probes");
                Check(Count(worker, "bytesReceived") == received[0] + received[1] + probes[0] + probes[1], "Authenticated received payload total equals its per-slot input/probe sums");
                Invoke(worker, "RecordPayloadSent", 0, 146, false); Invoke(worker, "RecordPayloadSent", 1, 150, false); Invoke(worker, "RecordPayloadSent", 0, 8, true);
                var sent = (long[])Get(worker, "stateBytesSentBySlot"); var probeSent = (long[])Get(worker, "probeBytesSentBySlot");
                Check(sent[0] == 146 && sent[1] == 150 && probeSent[0] == 8 && probeSent[1] == 0 && Count(worker, "bytesSent") == 304, "Per-slot sent state/probe payload totals stay distinct and add up");
                long sum = Count(worker, "admissionRejected") + Count(worker, "malformedRejected") + Count(worker, "orderingRejected")
                    + Count(worker, "actionRejected") + Count(worker, "rateRejected") + Count(worker, "probeRejected") + Count(worker, "unknownPeerRejected");
                Check(Count(worker, "rejected") == sum, "Legacy rejection total equals the bounded reason buckets and excludes ignored heartbeats");

                long steps = Count(worker, "activeFixedSteps"); Invoke(worker, "FixedUpdate");
                Check(Count(worker, "activeFixedSteps") == steps + 1, "Active ball-enabled physics callback increments the step counter");
                world.Ball.Body.simulated = false; Invoke(worker, "FixedUpdate");
                Check(Count(worker, "activeFixedSteps") == steps + 1, "Disabled match bodies do not count as active simulation");
                world.Ball.Body.simulated = true; Time.timeScale = 0; Invoke(worker, "FixedUpdate");
                Check(Count(worker, "activeFixedSteps") == steps + 1, "Paused worker callback does not count active simulation");
                Time.timeScale = 1; Set(worker, "world", null); Invoke(worker, "FixedUpdate");
                Check(Count(worker, "activeFixedSteps") == steps + 1, "Idle worker callback does not count active simulation"); Set(worker, "world", world);
                File.AppendAllText(Report, "COMPLETE " + checks + " metric classification checks passed. Linux counters, active rates and profiler availability require real server measurement.\n");
                return Report;
            }
            finally
            {
                Set(worker, "world", null); world?.Dispose(); UnityEngine.Object.Destroy(root); Time.timeScale = scale;
                foreach (var manager in previous) if (manager != null) manager.gameObject.SetActive(true);
            }
        }
    }
}
