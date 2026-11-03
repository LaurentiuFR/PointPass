#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using PointPass.Players;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Profiling;

namespace PointPass.Networking
{
    /// <summary>Opt-in build verification. File commands become ordinary local controller inputs.
    /// Host-only fixture placement is explicitly separate from the gameplay tests.</summary>
    public sealed class NetworkDevelopmentTestDriver : MonoBehaviour
    {
        [Serializable]
        private class Command
        {
            public int sequence;
            public string action;
            public float x;
            public float y;
            public float curve;
            public bool sprint;
            public int slot;
            public float ballX;
            public float ballY;
            public float blueX;
            public float blueY;
            public float redX;
            public float redY;
        }

        [Serializable]
        private class Report
        {
            public int appliedCommand;
            public string status;
            public bool connected;
            public bool busy;
            public bool hasSession;
            public bool host;
            public int players;
            public int simulatedBodies;
            public uint stateSequence;
            public int resetVersion;
            public int blueScore;
            public int redScore;
            public bool playing;
            public float clock;
            public int owner;
            public Vector2 blue;
            public Vector2 red;
            public Vector2 ball;
            public Vector2 ballVelocity;
            public Vector2 blueVelocity,redVelocity;
            public float ballCurve;
            public int charging;
            public float charge;
            public uint blueKicks;
            public uint redKicks;
            public bool metricsConnected;
            public bool hasPing;
            public float pingMs;
            public float jitterMs;
            public float packetLossPercent;
            public float fps;
            public float uploadKbps;
            public float downloadKbps;
            public ulong packetsSent;
            public ulong packetsReceived;
            public float snapshotHz;
            public bool diagnosticsSelfCheck;
            public float blueStamina,redStamina,blueRecovery,redRecovery;
            public bool blueExhausted,redExhausted;
            public uint blueTackles,redTackles;
            public int blueTackleResult,redTackleResult;
            public int profileFrames;
            public float averageFrameMs,peakFrameMs,peakMainThreadMs;
            public long peakGcBytes;
        }

        private NetworkSession session;
        private string directory;
        private string role;
        private int applied;
        private float nextPoll;
        private Vector2 move;
        private bool sprint;
        private float curve;
        private StreamWriter snapshots;
        private bool previousBackground;
        private HumanPlayerInput humanInput;
        private bool diagnosticsSelfCheck;
        private ProfilerRecorder mainThreadRecorder,gcRecorder;
        private int profileFrames;
        private float sumFrameMs,peakFrameMs,peakMainThreadMs;
        private long peakGcBytes;
        private readonly CultureInfo invariant = CultureInfo.InvariantCulture;

        public void Init(NetworkSession value)
        {
            session = value;
            // Synthetic keyboard events must be processed by hidden test players too.
            // Normal players retain Unity's existing focus handling.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            directory = Path.GetFullPath(NetworkDevelopmentBootstrap.Argument("--pp-test-dir", "Logs/NetworkVerification"));
            Directory.CreateDirectory(directory);
            role = NetworkDevelopmentBootstrap.Argument("--pp-test-role", "player");
            snapshots = new StreamWriter(Path.Combine(directory, role + "-snapshots.csv"), false) { AutoFlush = true };
            snapshots.WriteLine("sequence,reset,blueScore,redScore,clock,owner,ballX,ballY,ballVx,ballVy,blueX,blueY,redX,redY,blueKicks,redKicks,curve,blueStamina,redStamina,blueExhausted,redExhausted,blueTackles,redTackles,blueTackleResult,redTackleResult");
            session.StateReceived += Record;
            mainThreadRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",16);
            gcRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",16);
        }

        private void Record(NetworkMatchState state)
        {
            snapshots.WriteLine(string.Join(",", state.Sequence, state.ResetVersion, state.BlueScore, state.RedScore,
                state.Clock.ToString("R", invariant), state.OwnerSlot,
                state.BallPosition.x.ToString("R", invariant), state.BallPosition.y.ToString("R", invariant),
                state.BallVelocity.x.ToString("R", invariant), state.BallVelocity.y.ToString("R", invariant),
                state.Blue.Position.x.ToString("R", invariant), state.Blue.Position.y.ToString("R", invariant),
                state.Red.Position.x.ToString("R", invariant), state.Red.Position.y.ToString("R", invariant),
                state.Blue.KickSerial, state.Red.KickSerial, state.BallCurve.ToString("R", invariant),
                state.Blue.Stamina.ToString("R",invariant),state.Red.Stamina.ToString("R",invariant),state.Blue.Exhausted,state.Red.Exhausted,
                state.Blue.TackleSerial,state.Red.TackleSerial,(int)state.Blue.TackleResult,(int)state.Red.TackleResult));
        }

        private void Update()
        {
            float frameMs=Time.unscaledDeltaTime*1000;profileFrames++;sumFrameMs+=frameMs;
            peakFrameMs=Mathf.Max(peakFrameMs,frameMs);
            if(mainThreadRecorder.Valid)peakMainThreadMs=Mathf.Max(peakMainThreadMs,mainThreadRecorder.LastValue/1000000f);
            if(gcRecorder.Valid)peakGcBytes=Math.Max(peakGcBytes,gcRecorder.LastValue);
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.05f;
            string path = Path.Combine(directory, role + "-command.json");
            if (File.Exists(path))
            {
                try
                {
                    Command command;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader input = new StreamReader(stream))
                        command = JsonUtility.FromJson<Command>(input.ReadToEnd());
                    if (command != null && command.sequence > applied) { Apply(command); applied = command.sequence; }
                }
                catch (IOException) { /* The test runner may be replacing the command file. */ }
            }
            if (session.World != null)
            {
                PlayerController local = session.World.Players[session.IsHost ? 0 : 1];
                if(humanInput==null)
                { local.SetMove(move); local.SetSprint(sprint); local.SetShotCurve(curve); }
                session.World.Hud.SetPlayerState(local.IsActive, local.Dribbler.HasBall, false);
                session.World.Hud.SetCharge(local.ChargingKick, local.KickCharge, local.ShotCurve);
                session.World.Hud.SetAthleticState(local.Stamina,local.Tackler);
            }
            NetworkMatchState state = session.LatestState;
            Report report = new Report
            {
                appliedCommand = applied, status = session.Status, connected = session.IsConnected, host = session.IsHost,
                busy = session.Busy, hasSession = session.HasSession,
                players = session.World?.Players.Count ?? 0,
                stateSequence = state.Sequence, resetVersion = state.ResetVersion, blueScore = state.BlueScore,
                redScore = state.RedScore, playing = state.Playing, clock = state.Clock, owner = state.OwnerSlot,
                blue = state.Blue.Position, red = state.Red.Position, ball = state.BallPosition,
                ballVelocity = state.BallVelocity, ballCurve = state.BallCurve,
                blueVelocity=state.Blue.Velocity,redVelocity=state.Red.Velocity,
                charging = (int)state.Red.Charging, charge = state.Red.Charge,
                blueKicks = state.Blue.KickSerial, redKicks = state.Red.KickSerial,
                blueStamina=state.Blue.Stamina,redStamina=state.Red.Stamina,
                blueExhausted=state.Blue.Exhausted,redExhausted=state.Red.Exhausted,
                blueRecovery=state.Blue.TackleRecovery,redRecovery=state.Red.TackleRecovery,
                blueTackles=state.Blue.TackleSerial,redTackles=state.Red.TackleSerial,
                blueTackleResult=(int)state.Blue.TackleResult,redTackleResult=(int)state.Red.TackleResult,
                profileFrames=profileFrames,averageFrameMs=profileFrames>0?sumFrameMs/profileFrames:0,
                peakFrameMs=peakFrameMs,peakMainThreadMs=peakMainThreadMs,peakGcBytes=peakGcBytes
            };
            NetworkDiagnostics metrics = session.Diagnostics;
            report.metricsConnected = metrics.HasPeer;
            report.hasPing = metrics.HasPing;
            report.pingMs = metrics.PingMilliseconds;
            report.jitterMs = metrics.JitterMilliseconds;
            report.packetLossPercent = metrics.PacketLossPercent;
            report.fps = metrics.Fps;
            report.uploadKbps = metrics.UploadKbps;
            report.downloadKbps = metrics.DownloadKbps;
            report.packetsSent = metrics.PacketsSent;
            report.packetsReceived = metrics.PacketsReceived;
            report.snapshotHz = metrics.SnapshotHz;
            report.diagnosticsSelfCheck = diagnosticsSelfCheck;
            if (session.World != null)
                foreach (Rigidbody2D body in session.World.Root.GetComponentsInChildren<Rigidbody2D>())
                    if (body.simulated) report.simulatedBodies++;
            string temporary = Path.Combine(directory, role + "-state.tmp");
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(report));
                string reportPath = Path.Combine(directory, role + "-state.json");
                if (File.Exists(reportPath)) File.Replace(temporary, reportPath, null);
                else File.Move(temporary, reportPath);
            }
            catch (IOException) { /* Retry on the next poll if a reader briefly owns the report. */ }
        }

        private void Apply(Command command)
        {
            if(command.action=="resetProfile") {profileFrames=0;sumFrameMs=peakFrameMs=peakMainThreadMs=0;peakGcBytes=0;return;}
            if (command.action == "quit") { session.Disconnect(); Application.Quit(); return; }
            if (command.action == "disconnect") { session.Disconnect(); return; }
            if (command.action == "host") { session.Host((ushort)command.slot); return; }
            if (command.action == "join") { session.Join("127.0.0.1", (ushort)command.slot); return; }
            if (command.action == "capture") { Capture(); return; }
            if (command.action == "restart") { session.RestartMatch(); return; }
            if (command.action == "diagnosticsCheck")
            {
                NetworkDiagnostics sample = new NetworkDiagnostics();
                sample.ObserveSnapshot(100); sample.ObserveSnapshot(101); sample.ObserveSnapshot(103);
                sample.ObserveSnapshot(103); sample.ObserveSnapshot(102);
                sample.Tick(null, null, false, 0);
                bool gaps = Mathf.Abs(sample.SnapshotLossPercent - 25) < 0.001f;
                sample.ObservePing(20); sample.ObservePing(40); sample.ObservePing(double.NaN);
                bool ping = sample.HasPing && sample.PingMilliseconds == 40 && sample.JitterMilliseconds > 0;
                sample.ResetConnection();
                bool reset = !sample.HasSnapshot && !sample.HasPing && sample.SnapshotLossPercent == 0;
                NetworkDiagnostics wrapped = new NetworkDiagnostics();
                wrapped.ObserveSnapshot(uint.MaxValue); wrapped.ObserveSnapshot(0);
                wrapped.Tick(null, null, false, 0);
                diagnosticsSelfCheck = gaps && ping && reset && wrapped.SnapshotLossPercent == 0;
                return;
            }
            NetworkMatchWorld world = session.World;
            if (world == null) return;
            PlayerController player = world.Players[session.IsHost ? 0 : 1];
            if(command.action=="tackle") {player.RequestTackle();return;}
            if(command.action=="tackleBurst") {for(int i=0;i<60;i++)player.RequestTackle();return;}
            if (command.action == "move") { move = new Vector2(command.x, command.y); sprint = command.sprint; return; }
            if (command.action == "cancelBurst") { for (int i = 0; i < 300; i++) player.CancelKick(); return; }
            if (command.action == "humanOff")
            {
                if (humanInput != null) { humanInput.enabled = false; Destroy(humanInput); humanInput = null; }
                if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                return;
            }
            if (command.action.StartsWith("touch") || command.action.StartsWith("keyShot"))
            {
                if (humanInput == null) { humanInput = player.gameObject.AddComponent<HumanPlayerInput>(); humanInput.Init(world.Hud); }
                humanInput.SendMessage("OnApplicationFocus", true);
                if (command.action.StartsWith("keyShot"))
                {
                    if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
                    InputSystem.EnableDevice(Keyboard.current);
                    InputSystem.QueueStateEvent(Keyboard.current, command.action == "keyShotDown" ? new KeyboardState(Key.K) : new KeyboardState());
                    InputSystem.Update();
                    return;
                }
                PointPass.UI.HoldActionButton button = command.action.StartsWith("touchPass") ? world.Hud.PassButton
                    : command.action.StartsWith("touchTackle") ? world.Hud.TackleButton : command.action.StartsWith("touchSprint") ? world.Hud.SprintButton : world.Hud.ShootButton;
                PointerEventData pointer = new PointerEventData(EventSystem.current) { pointerId = 42, position = new Vector2(900, 400) };
                if (command.action.EndsWith("Tap")) { button.OnPointerDown(pointer); button.OnPointerUp(pointer); }
                else if (command.action.EndsWith("Down")) button.OnPointerDown(pointer);
                else button.OnPointerUp(pointer);
                return;
            }
            if (command.action == "aimTap")
            {
                player.SetMove(new Vector2(command.x, command.y));
                player.SetMove(Vector2.zero);
                return;
            }
            if (command.action == "passDown") player.BeginKick(PlayerKicker.KickType.Pass);
            if (command.action == "shotDown") player.BeginKick(PlayerKicker.KickType.Shot);
            if (command.action == "cancel") player.CancelKick();
            if (command.action == "passUp") player.ReleaseKick(PlayerKicker.KickType.Pass);
            if (command.action == "shotUp")
            {
                curve = command.curve;
                player.SetShotCurve(curve);
                player.ReleaseKick(PlayerKicker.KickType.Shot);
                curve = 0;
            }
            if (command.action == "fixture" && session.IsHost)
            {
                move = Vector2.zero; sprint = false; curve = 0;
                world.Possession.Clear();
                world.Ball.ResetTo(new Vector2(command.ballX, command.ballY));
                world.Players[0].ResetToKickoff();
                world.Players[1].ResetToKickoff();
                Place(world.Players[0], new Vector2(command.blueX, command.blueY), -90);
                Place(world.Players[1], new Vector2(command.redX, command.redY), 90);
            }
        }

        private static void Place(PlayerController player, Vector2 position, float angle)
        {
            player.GetComponent<PlayerMovement>().ResetTo(position, angle);
            player.transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void Capture()
        {
            Camera camera = Camera.main;
            const int width = 1280, height = 720;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float aspect = camera.aspect;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            RenderMode[] modes = new RenderMode[canvases.Length];
            Camera[] cameras = new Camera[canvases.Length];
            float[] distances = new float[canvases.Length];
            float[] scales = new float[canvases.Length];
            bool[] scaling = new bool[canvases.Length];
            Texture2D pixels = null;
            try
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    modes[i] = canvas.renderMode; cameras[i] = canvas.worldCamera;
                    distances[i] = canvas.planeDistance; scales[i] = canvas.scaleFactor;
                    UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                    scaling[i] = scaler != null && scaler.enabled;
                    if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                    canvas.planeDistance = 1; canvas.scaleFactor = height / 1080f;
                }
                camera.targetTexture = target; camera.aspect = (float)width / height;
                Canvas.ForceUpdateCanvases();
                foreach (Canvas canvas in canvases)
                    foreach (UnityEngine.UI.Graphic graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                        graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, role + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive; camera.targetTexture = previousTarget; camera.aspect = aspect;
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    canvas.renderMode = modes[i]; canvas.worldCamera = cameras[i];
                    canvas.planeDistance = distances[i]; canvas.scaleFactor = scales[i];
                    UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                    if (scaler != null) scaler.enabled = scaling[i];
                }
                Canvas.ForceUpdateCanvases();
                if (pixels != null) Destroy(pixels);
                target.Release(); Destroy(target);
            }
        }

        private void OnDestroy()
        {
            if (session != null) session.StateReceived -= Record;
            snapshots?.Dispose();
            mainThreadRecorder.Dispose();gcRecorder.Dispose();
            Application.runInBackground = previousBackground;
        }
    }
}
#endif
