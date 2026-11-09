#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !UNITY_SERVER
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PointPass.Networking;
using PointPass.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointPass.Online
{
    /// <summary>
    /// Explicitly opted-in development-client test adapter. It submits ordinary local commands;
    /// it cannot place actors, award possession, alter scores or run the server simulation.
    /// </summary>
    public sealed class OnlinePilotTestDriver : MonoBehaviour
    {
        [Serializable]
        private sealed class Command
        {
            public int sequence;
            public string action;
            public float x, y, curve;
            public bool sprint;
            public string data;
        }

        [Serializable]
        private sealed class Report
        {
            public int appliedCommand, localSlot, players, simulatedBodies;
            public string status, testError;
            public bool connected, busy, hasSession, hasWorld, playing, fullTime;
            public uint stateSequence, blueKicks, redKicks;
            public int resetVersion, blueScore, redScore, half, owner, charging;
            public float clock, charge, ballCurve, blueCharge, redCharge;
            public int blueCharging, redCharging;
            public Vector2 blue, red, ball, ballVelocity;
            public bool metricsConnected, hasPing;
            public float pingMs, jitterMs, packetLossPercent, snapshotLossPercent, fps, uploadKbps, downloadKbps, snapshotHz;
            public ulong packetsSent, packetsReceived;
            public int recordedSnapshots;
            public bool rawValidationHook;
        }

        private OnlinePilotClient client;
        private string directory, role, invitationPath, error;
        private int applied, recorded;
        private double nextPoll, recordUntil;
        private Vector2 move, aim;
        private float curve;
        private bool sprint, priorBackground;
        private bool invitationStdin, invitationRead;
        private InputSettings.BackgroundBehavior priorInputBehavior;
        private StreamWriter snapshots;
        private readonly CultureInfo invariant = CultureInfo.InvariantCulture;

        public static void TryAttach(OnlinePilotClient client)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-pointpassOnlineTest") < 0) return;
            client.gameObject.AddComponent<OnlinePilotTestDriver>().Init(client, args);
        }

        private void Init(OnlinePilotClient value, string[] args)
        {
            client = value;
            role = Argument(args, "-pointpassProfile", "test");
            if (role.Length < 1 || role.Length > 24) throw new ArgumentException("Invalid online test profile.");
            foreach (char letter in role) if (!char.IsLetterOrDigit(letter) && letter != '-') throw new ArgumentException("Invalid online test profile.");
            directory = Path.GetFullPath(Argument(args, "-pointpassTestDirectory", "Logs/OnlinePilotVerification/Clients"));
            invitationPath = Argument(args, "-pointpassInvitationFile", "");
            invitationStdin = Array.IndexOf(args, "-pointpassInvitationStdin") >= 0;
            if (invitationStdin && !string.IsNullOrEmpty(invitationPath)) throw new ArgumentException("Select one test invitation input mode.");
            Directory.CreateDirectory(directory);
            priorBackground = Application.runInBackground;
            priorInputBehavior = InputSystem.settings.backgroundBehavior;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            recordUntil = Time.unscaledTimeAsDouble + 600;
            snapshots = new StreamWriter(Path.Combine(directory, role + "-snapshots.csv"), false) { AutoFlush = true };
            snapshots.WriteLine("sequence,reset,blueScore,redScore,clock,playing,owner,ballX,ballY,ballVx,ballVy,blueX,blueY,redX,redY,blueKicks,redKicks,curve");
            client.StateReceived += Record;
            Join();
        }

        private static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private void Join()
        {
            string invitation = "";
            try
            {
                if (!invitationRead && invitationStdin)
                {
                    invitationRead = true;
                    if (!Console.IsInputRedirected) { error = "invitation_stdin_not_redirected"; return; }
                    // The harness preloads a single line and closes its private pipe.
                    // Bound both the data length and startup wait; never echo input.
                    Task<string> read = Task.Run(() => ReadPrivateInvitation(Console.In));
                    if (!read.Wait(TimeSpan.FromSeconds(10))) { error = "invitation_stdin_timeout"; return; }
                    invitation = read.Result;
                }
                else if (!invitationRead && !string.IsNullOrEmpty(invitationPath) && File.Exists(invitationPath))
                {
                    invitationRead = true;
                    if (new FileInfo(invitationPath).Length > 256) { error = "invitation_file_too_large"; return; }
                    invitation = File.ReadAllText(invitationPath).Trim();
                }
                // Rejoins use the persisted guest identity, not the one-use invitation.
                client.Join(invitation, role);
            }
            catch (IOException) { error = invitationStdin ? "invitation_stdin_unavailable" : "invitation_file_unavailable"; return; }
            catch (AggregateException) { error = "invitation_stdin_unavailable"; return; }
            catch (InvalidOperationException) { error = "invitation_stdin_unavailable"; return; }
            catch (NotSupportedException) { error = "invitation_stdin_unavailable"; return; }
            finally { invitation = null; }
        }

        private static string ReadPrivateInvitation(TextReader input)
        {
            var buffer = new char[43]; int count = 0;
            try
            {
                while (true)
                {
                    int value = input.Read();
                    if (value < 0 || value == '\n') break;
                    if (value == '\r')
                    {
                        int next = input.Read();
                        if (next != '\n' && next >= 0) throw new InvalidDataException("Invalid private invitation line.");
                        break;
                    }
                    if (count >= buffer.Length || !((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z')
                        || (value >= '0' && value <= '9') || value == '-' || value == '_'))
                        throw new InvalidDataException("Invalid private invitation line.");
                    buffer[count++] = (char)value;
                }
                if (count != 0 && count != buffer.Length) throw new InvalidDataException("Invalid private invitation line.");
                return count == 0 ? "" : new string(buffer);
            }
            finally { Array.Clear(buffer, 0, buffer.Length); }
        }

        private void Record(NetworkMatchState state)
        {
            if (snapshots == null || recorded >= 18000 || Time.unscaledTimeAsDouble > recordUntil) return;
            recorded++;
            snapshots.WriteLine(string.Join(",", state.Sequence, state.ResetVersion, state.BlueScore, state.RedScore,
                state.Clock.ToString("R", invariant), state.Playing ? 1 : 0, state.OwnerSlot,
                state.BallPosition.x.ToString("R", invariant), state.BallPosition.y.ToString("R", invariant),
                state.BallVelocity.x.ToString("R", invariant), state.BallVelocity.y.ToString("R", invariant),
                state.Blue.Position.x.ToString("R", invariant), state.Blue.Position.y.ToString("R", invariant),
                state.Red.Position.x.ToString("R", invariant), state.Red.Position.y.ToString("R", invariant),
                state.Blue.KickSerial, state.Red.KickSerial, state.BallCurve.ToString("R", invariant)));
        }

        private void Update()
        {
            if (client == null || Time.unscaledTimeAsDouble < nextPoll) return;
            nextPoll = Time.unscaledTimeAsDouble + 0.05;
            PlayerController local = null;
            if (client.World != null && client.LocalSlot >= 0 && client.LocalSlot < 2)
            {
                local = client.World.Players[client.LocalSlot];
                foreach (HumanPlayerInput input in client.World.Root.GetComponentsInChildren<HumanPlayerInput>()) input.enabled = false;
            }
            string path = Path.Combine(directory, role + "-command.json");
            if (File.Exists(path))
            {
                try
                {
                    if (new FileInfo(path).Length <= 1024)
                    {
                        Command command;
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var reader = new StreamReader(stream)) command = JsonUtility.FromJson<Command>(reader.ReadToEnd());
                        if (command != null && command.sequence > applied && Apply(command, local)) applied = command.sequence;
                    }
                }
                catch (IOException) { }
                catch (ArgumentException) { error = "invalid_command_file"; }
            }
            if (local != null && client.World != null)
            {
                if (aim != Vector2.zero) local.SetAim(aim);
                local.SetMove(move); local.SetSprint(sprint); local.SetShotCurve(curve);
                client.World.Hud.SetPlayerState(local.IsActive, local.Dribbler.HasBall, false);
                client.World.Hud.SetCharge(local.ChargingKick, local.KickCharge, local.ShotCurve);
            }
            WriteReport();
        }

        private bool Apply(Command command, PlayerController local)
        {
            if (command.action == "quit") { client.Leave(); Application.Quit(); return true; }
            if (command.action == "leave") { move = aim = Vector2.zero; sprint = false; curve = 0; client.Leave(); return true; }
            if (command.action == "join") { if (client.Busy || client.HasSession) return false; Join(); return true; }
            if (command.action == "capture") { ScreenCapture.CaptureScreenshot(Path.Combine(directory, role + ".png")); return true; }
            if (command.action == "rawMalformed")
            {
                MethodInfo hook = typeof(OnlinePilotClient).GetMethod("SendValidationInput", BindingFlags.Instance | BindingFlags.Public);
                if (hook == null) { error = "validation_hook_unavailable"; return true; }
                byte[] bytes;
                try { bytes = Convert.FromBase64String(command.data ?? ""); }
                catch (FormatException) { error = "invalid_validation_payload"; return true; }
                if (bytes.Length < 1 || bytes.Length > 128) { error = "invalid_validation_payload_size"; return true; }
                hook.Invoke(client, new object[] { bytes }); return true;
            }
            if (local == null) return false;
            if (float.IsNaN(command.x) || float.IsInfinity(command.x) || float.IsNaN(command.y) || float.IsInfinity(command.y)
                || float.IsNaN(command.curve) || float.IsInfinity(command.curve)) { error = "nonfinite_test_command"; return true; }
            if (command.action == "move") { move = Vector2.ClampMagnitude(new Vector2(command.x, command.y), 1); sprint = command.sprint; return true; }
            if (command.action == "aim") { aim = new Vector2(command.x, command.y).normalized; local.SetAim(aim); local.SetMove(Vector2.zero); move = Vector2.zero; return true; }
            if (command.action == "sprint") { sprint = command.sprint; return true; }
            if (command.action == "curve") { curve = Mathf.Clamp(command.curve, -1, 1); return true; }
            // A snapshot can arrive before its interpolated possession sample is
            // presented. Keep this opted-in press pending until the local button
            // would accept it, rather than silently losing the test action.
            if (command.action == "passDown") return local.BeginKick(PlayerKicker.KickType.Pass);
            if (command.action == "shotDown") return local.BeginKick(PlayerKicker.KickType.Shot);
            if (command.action == "passUp") { local.ReleaseKick(PlayerKicker.KickType.Pass); return true; }
            if (command.action == "shotUp") { local.SetShotCurve(Mathf.Clamp(command.curve, -1, 1)); local.ReleaseKick(PlayerKicker.KickType.Shot); curve = 0; return true; }
            if (command.action == "cancel") { local.CancelActions(); curve = 0; return true; }
            error = "unsupported_test_action"; return true;
        }

        private void WriteReport()
        {
            NetworkMatchState state = client.LatestState;
            NetworkPlayerState local = client.LocalSlot == 0 ? state.Blue : state.Red;
            NetworkDiagnostics metrics = client.Diagnostics;
            var report = new Report
            {
                appliedCommand = applied, localSlot = client.LocalSlot, status = client.Status, testError = error,
                connected = client.HasSession && metrics.HasPeer, busy = client.Busy, hasSession = client.HasSession, hasWorld = client.World != null,
                players = client.World?.Players.Count ?? 0, stateSequence = state.Sequence, resetVersion = state.ResetVersion,
                blueScore = state.BlueScore, redScore = state.RedScore, half = state.Half, clock = state.Clock,
                playing = state.Playing, fullTime = state.FullTime, owner = state.OwnerSlot,
                blue = state.Blue.Position, red = state.Red.Position, ball = state.BallPosition, ballVelocity = state.BallVelocity, ballCurve = state.BallCurve,
                charging = (int)local.Charging, charge = local.Charge, blueCharge = state.Blue.Charge, redCharge = state.Red.Charge,
                blueCharging = (int)state.Blue.Charging, redCharging = (int)state.Red.Charging, blueKicks = state.Blue.KickSerial, redKicks = state.Red.KickSerial,
                metricsConnected = metrics.HasPeer, hasPing = metrics.HasPing, pingMs = metrics.PingMilliseconds, jitterMs = metrics.JitterMilliseconds,
                packetLossPercent = metrics.PacketLossPercent, snapshotLossPercent = metrics.SnapshotLossPercent, fps = metrics.Fps,
                uploadKbps = metrics.UploadKbps, downloadKbps = metrics.DownloadKbps, snapshotHz = metrics.SnapshotHz,
                packetsSent = metrics.PacketsSent, packetsReceived = metrics.PacketsReceived, recordedSnapshots = recorded,
                rawValidationHook = typeof(OnlinePilotClient).GetMethod("SendValidationInput", BindingFlags.Instance | BindingFlags.Public) != null
            };
            if (client.World != null)
                foreach (Rigidbody2D body in client.World.Root.GetComponentsInChildren<Rigidbody2D>()) if (body.simulated) report.simulatedBodies++;
            string temporary = Path.Combine(directory, role + "-state.tmp");
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(report));
                string path = Path.Combine(directory, role + "-state.json");
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            catch (IOException) { }
        }

        private void OnDestroy()
        {
            if (client != null) client.StateReceived -= Record;
            snapshots?.Dispose();
            Application.runInBackground = priorBackground;
            if (InputSystem.settings != null) InputSystem.settings.backgroundBehavior = priorInputBehavior;
        }
    }
}
#endif
