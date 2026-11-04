using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.Server;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Runs actual fixed-step football in the presentation-free server composition.</summary>
    public static class HeadlessPlayMode
    {
        private static string Report => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Logs/OnlinePilotVerification/headless-playmode.txt");

        private static void Check(bool condition, string description)
        {
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + description + "\n");
            if (!condition) throw new InvalidOperationException(description);
        }

        private static async Task Wait(Func<bool> condition, float seconds, string description)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && watch.Elapsed.TotalSeconds < seconds) await Task.Delay(20);
            Check(condition(), description);
        }

        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Headless server composition Play Mode verification\n");
            var previousMatches = UnityEngine.Object.FindObjectsByType<MatchManager>(FindObjectsSortMode.None);
            foreach (MatchManager match in previousMatches) match.gameObject.SetActive(false);
            float savedTimeScale = Time.timeScale;
            float savedFixedStep = Time.fixedDeltaTime;
            ServerMatchWorld world = null;
            try
            {
                Time.timeScale = 1;
                Time.fixedDeltaTime = 0.02f;
                world = new ServerMatchWorld(11, 22);
                Check(world.Root.GetComponentsInChildren<Renderer>(true).Length == 0
                    && world.Root.GetComponentsInChildren<Camera>(true).Length == 0
                    && world.Root.GetComponentsInChildren<Canvas>(true).Length == 0
                    && world.Root.GetComponentsInChildren<AudioSource>(true).Length == 0,
                    "Server world contains no renderers, cameras, canvases or audio sources");
                Check(world.Root.GetComponentsInChildren<HumanPlayerInput>(true).Length == 0
                    && world.Root.GetComponentsInChildren<PlayerAnimator>(true).Length == 0
                    && world.Root.GetComponentsInChildren<GoalkeeperBrain>(true).Length == 0
                    && world.Root.GetComponentsInChildren<GoalReplayRecorder>(true).Length == 0,
                    "Server world has no local input, rendered animators, AI keepers or replay recorder");
                Check(world.Root.GetComponentsInChildren<Collider2D>(true).Length == 13,
                    "Shared pitch creates eight walls, two goal triggers, two players and one ball");
                Check(world.Players.All(player => player.GetComponent<SimulationStride>() != null),
                    "Headless dribbling retains the existing foot cadence without an animator");
                await Wait(() => world.Simulation.IsMatchLive, 4, "Server starts the normal kickoff coroutine");
                int startFrame = Time.frameCount;
                await Task.Delay(200);
                Check(Time.frameCount > startFrame + 2 && world.Simulation.Clock < MatchConfig.HalfSeconds,
                    "Actual game frames and authoritative clock advance");

                PlayerController blue = world.Players[0], red = world.Players[1];
                Vector2 blueAt = blue.Body.position, redAt = red.Body.position;
                blue.SetMove(Vector2.up); red.SetMove(Vector2.down); red.SetSprint(true);
                await Task.Delay(600);
                Check(blue.Body.position.y > blueAt.y + 1 && red.Body.position.y < redAt.y - 1,
                    "Each controller independently moves its opposing-team footballer");
                Check(red.Body.linearVelocity.magnitude > blue.Body.linearVelocity.magnitude * 1.2f,
                    "Server preserves held sprint movement");
                blue.SetMove(Vector2.zero); red.SetMove(Vector2.zero); red.SetSprint(false);

                world.Simulation.StartMatch();
                await Wait(() => world.Simulation.IsMatchLive, 4, "Clean match restart returns to playing");
                blue.GetComponent<PlayerMovement>().ResetTo(new Vector2(-0.64f, 0), -90);
                red.GetComponent<PlayerMovement>().ResetTo(new Vector2(5, -4), 90);
                world.Ball.ResetTo(Vector2.zero); Physics2D.SyncTransforms();
                await Wait(() => world.Possession.Owner == blue, 1, "Blue acquires the shared physical ball");
                Check(blue.BeginKick(PlayerKicker.KickType.Shot), "Blue begins an authoritative charged shot");
                await Task.Delay(350);
                blue.SetShotCurve(0.8f); blue.ReleaseKick(PlayerKicker.KickType.Shot);
                await Wait(() => world.Capture(1).Blue.KickSerial > 0, 1, "Charged curved shot executes through PlayerController");
                await Task.Delay(200);
                File.AppendAllText(Report, $"MEASURE curved shot position {world.Ball.Body.position}, velocity {world.Ball.Body.linearVelocity}, curve {world.Ball.Curve:F3}\n");
                Check(world.Ball.Body.position.x > 1 && world.Ball.Body.position.y > 0.02f && world.Ball.Curve > 0,
                    "Shot travels through physics and bends gradually");

                world.Simulation.StartMatch();
                await Wait(() => world.Simulation.IsMatchLive, 4, "Restart clears the first shot and possession");
                red.GetComponent<PlayerMovement>().ResetTo(new Vector2(0.64f, 0), 90);
                world.Ball.ResetTo(Vector2.zero); Physics2D.SyncTransforms();
                await Wait(() => world.Possession.Owner == red, 1, "Red acquires the same authoritative ball");
                Check(red.BeginKick(PlayerKicker.KickType.Pass), "Red begins an authoritative charged pass");
                await Task.Delay(250); red.ReleaseKick(PlayerKicker.KickType.Pass);
                await Wait(() => world.Capture(2).Red.KickSerial > 0, 1, "Red pass executes independently of Blue");
                Check(world.Ball.Body.linearVelocity.x < -10 && world.Ball.LastKickWasPass,
                    "Pass retains physical velocity and pass-reception metadata");

                world.Simulation.StartMatch();
                await Wait(() => world.Simulation.IsMatchLive, 4, "A second restart clears the completed pass");
                red.GetComponent<PlayerMovement>().ResetTo(new Vector2(0.64f, 0), 90);
                world.Ball.ResetTo(Vector2.zero); Physics2D.SyncTransforms();
                await Wait(() => world.Possession.Owner == red, 1, "Red regains possession for a shot");
                Check(red.BeginKick(PlayerKicker.KickType.Shot), "Red can also begin a charged shot");
                await Task.Delay(250); red.ReleaseKick(PlayerKicker.KickType.Shot);
                await Wait(() => world.Capture(3).Red.KickSerial > 1, 1, "Red shot executes through the same authoritative kicker");
                Check(world.Ball.Body.linearVelocity.x < -10 && !world.Ball.LastKickWasPass,
                    "Red shooting preserves physical travel and shot metadata");

                world.Possession.Clear(); blue.Dribbler.Release(); red.Dribbler.Release();
                world.Ball.ResetTo(new Vector2(MatchConfig.FieldHalfWidth - 0.4f, 0));
                world.Ball.RecordKick(blue, false);
                world.Ball.Body.linearVelocity = Vector2.right * 20;
                Physics2D.SyncTransforms();
                int reset = world.Simulation.ResetVersion;
                await Wait(() => world.Simulation.BlueScore == 1, 2, "Physical goal trigger updates the authoritative score");
                Check(world.Capture(3).Blue.Celebrating, "Snapshot records a logical scorer celebration without rendered animation");
                await Task.Delay(300);
                Check(world.Ball.Body.position.x > MatchConfig.FieldHalfWidth && world.Ball.Body.linearDamping == 5,
                    "Ball continues into the net and the shared absorption settles it");
                await Wait(() => world.Simulation.IsMatchLive && world.Simulation.ResetVersion > reset, 6,
                    "Goal celebration completes and kickoff consistently resets the match");
                Check(world.Simulation.BlueScore == 1 && world.Simulation.RedScore == 0
                    && world.Ball.Body.linearDamping == MatchConfig.PitchBallDamping,
                    "Kickoff preserves score and restores ordinary ball damping");
                File.AppendAllText(Report, "COMPLETE Headless gameplay checks passed. Networking, Linux execution and internet clients are separate tests.\n");
                return File.ReadAllText(Report);
            }
            finally
            {
                world?.Dispose();
                Time.timeScale = savedTimeScale;
                Time.fixedDeltaTime = savedFixedStep;
                foreach (MatchManager match in previousMatches) if (match != null) match.gameObject.SetActive(true);
            }
        }
    }
}
