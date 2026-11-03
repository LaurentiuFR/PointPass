using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PointPass.Audio;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Run through Pipeline script run in the live project's Play Mode.</summary>
    public static class GoalkeeperPlayMode
    {
        private static string Folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/KeeperVerification");
        private static string Report => Path.Combine(Folder, "playmode-tests.txt");
        private static MatchManager manager;
        private static Ball ball;
        private static PlayerController human;
        private static PossessionSystem possession;
        private static GoalkeeperBrain[] keepers;
        private static bool testingPause;
        private static void Check(bool ok, string name)
        {
            File.AppendAllText(Report, (ok ? "PASS " : "FAIL ") + name + "\n");
            if (!ok) throw new InvalidOperationException(name);
        }
        private static async Task Wait(Func<bool> condition, float timeout, string description)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && watch.Elapsed.TotalSeconds < timeout) await Task.Delay(20);
            if (!condition() && keepers != null)
            {
                File.AppendAllText(Report, $"DIAGNOSTIC live={manager.IsMatchLive} paused={manager.IsUserPaused} scale={Time.timeScale}\n");
                foreach (var keeper in keepers)
                {
                    var player = keeper.GetComponent<PlayerController>();
                    File.AppendAllText(Report, $"DIAGNOSTIC {keeper.name} state={keeper.State} age={keeper.StateAge:F2} active={player.IsActive} charge={player.ChargingKick} owner={possession.IsOwner(player)} pending={Field<bool>(player.GetComponent<PlayerKicker>(), "pending")} distributions={keeper.Distributions}\n");
                }
            }
            Check(condition(), description);
        }
        private static async Task Delay(float seconds) => await Task.Delay(Mathf.RoundToInt(seconds * 1000));
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Click(MatchMenu menu, string name)
        {
            var button = menu.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name);
            button.onClick.Invoke();
        }
        private static async Task Capture(string name)
        {
            await Delay(0.12f);
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }
        private static void ResetKeeper(GoalkeeperBrain keeper, float y = 0)
        {
            var player = keeper.GetComponent<PlayerController>();
            player.ResetToKickoff(); player.Body.position = new Vector2(keeper.Side * (MatchConfig.FieldHalfWidth - 0.65f), y);
            player.transform.position = player.Body.position; keeper.ResetKeeper();
        }
        private static void Serve(Vector2 origin, Vector2 velocity)
        {
            possession.Clear(); ball.ResetTo(origin); ball.RecordKick(human, false);
            ball.Body.linearVelocity = velocity; Physics2D.SyncTransforms();
        }

        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Folder); File.WriteAllText(Report, "Live Unity goalkeeper, passing, sound and menu checks\n");
            manager = UnityEngine.Object.FindFirstObjectByType<MatchManager>();
            ball = UnityEngine.Object.FindFirstObjectByType<Ball>();
            var roster = Field<List<PlayerController>>(manager, "players"); human = roster[0];
            possession = Field<PossessionSystem>(manager, "possession");
            keepers = roster.Select(p => p.GetComponent<GoalkeeperBrain>()).Where(k => k != null).ToArray();
            var input = human.GetComponent<HumanPlayerInput>(); bool inputWasEnabled = input.enabled;
            var audio = UnityEngine.Object.FindFirstObjectByType<FootballAudio>();
            float savedVolume = AudioPreferences.Volume; bool savedMute = AudioPreferences.Muted;
            PlayerController receiver = null;
            var menu = UnityEngine.Object.FindFirstObjectByType<MatchMenu>(FindObjectsInactive.Include);
            // Production still pauses on focus loss. The automated review resumes only
            // incidental editor focus pauses, while preserving its explicit pause checks.
            UnityEditor.EditorApplication.CallbackFunction keepReviewLive = () =>
            {
                if (!testingPause && manager != null && manager.IsUserPaused) manager.ResumeMatch();
                if (!testingPause && audio != null && !Field<bool>(audio, "focused"))
                    audio.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            };
            testingPause = false;
            UnityEditor.EditorApplication.update += keepReviewLive;
            try
            {
                input.enabled = false;
                manager.OpenMainMenu(); await Delay(0.65f);
                Check(menu.IsOpen && menu.CurrentScreen == MatchMenu.Screen.Main, "Main menu opens normally");
                Check(menu.GetComponentsInChildren<Transform>(true).All(t => t.name != "HeroCard")
                    && menu.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ColourScheme"),
                    "Clubhouse replaces the old promotional card while preserving colour choices");
                Check(menu.Lobby != null, "Clubhouse presentation is present");
                await Capture("main-menu");
                Click(menu, "Settings"); await Delay(0.1f);
                Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.Settings && menu.GetComponentInChildren<UnityEngine.UI.Slider>() != null,
                    "Dedicated sound settings are accessible from the main menu");
                await Capture("sound-settings"); menu.TryBack(); await Delay(0.1f);
                Click(menu, "Play"); Click(menu, "Training"); Click(menu, "AiBotMatches");
                Click(menu, "Difficulty1"); Click(menu, "PlayOffline");
                await Wait(() => manager.IsMatchLive, 5, "Play Match starts a live match through the menu");
                Check(keepers.Length == 2 && keepers.Select(k => k.GetComponent<PlayerController>().Team).Distinct().Count() == 2,
                    "Both teams have an active AI goalkeeper");
                int frame = Time.frameCount; await Delay(0.12f);
                Check(Time.frameCount > frame + 1, "Actual player-loop frames advance during gameplay");
                testingPause = true; manager.PauseMatch(); Vector2 paused = keepers[1].GetComponent<PlayerController>().Body.position;
                await Delay(0.15f);
                Check(Time.timeScale == 0 && keepers[1].GetComponent<PlayerController>().Body.position == paused,
                    "Pause freezes goalkeeper decisions and physics");
                Click(menu, "AudioSettings"); await Delay(0.1f);
                Check(menu.CurrentScreen == MatchMenu.Screen.Audio, "Pause menu exposes sound settings directly");
                menu.TryBack(); manager.ResumeMatch(); testingPause = false;

                var factory = typeof(MatchBootstrap).GetMethod("CreatePlayer", BindingFlags.NonPublic | BindingFlags.Static);
                receiver = (PlayerController)factory.Invoke(null, new object[] { manager.transform, Team.Blue, ball, possession, new Vector2(4, 0), 90f });
                foreach (bool charged in new[] { false, true })
                {
                    possession.Clear(); human.ResetToKickoff(); human.Body.position = new Vector2(charged ? -7 : -6, 0);
                    human.transform.position = human.Body.position; human.SetAim(Vector2.right);
                    receiver.ResetToKickoff(); receiver.Body.position = new Vector2(charged ? 7 : 4, 0);
                    receiver.transform.position = receiver.Body.position;
                    ball.ResetTo(human.Body.position + Vector2.right * 0.64f); possession.TryAcquire(human);
                    human.BeginKick(PlayerKicker.KickType.Pass); if (charged) await Delay(0.67f);
                    float kickAt = Time.time; human.ReleaseKick(PlayerKicker.KickType.Pass);
                    await Wait(() => ball.LastKicker == human && !possession.IsOwner(human), 0.3f, "Pass leaves the foot on the next physics tick");
                    float launchSpeed = ball.Body.linearVelocity.magnitude;
                    Check(launchSpeed > (charged ? 19 : 10.5f), charged ? "Charged pass has strong long-range pace" : "Tap pass is quick and controlled");
                    await Wait(() => possession.IsOwner(receiver), 2.2f, charged ? "Charged pass reaches and is controlled by a teammate 14m away" : "Tap pass reaches and is controlled by a teammate 10m away");
                    Check(Time.time - kickAt < (charged ? 1.5f : 1.7f), "Pass arrives promptly over a meaningful distance");
                    await Delay(0.3f);
                    Check(ball.Body.linearVelocity.magnitude < 2f, "Receiver settles the fast pass without losing possession");
                    File.AppendAllText(Report, $"MEASURE {(charged ? "long" : "short")} pass launch {launchSpeed:F2}m/s\n");
                    receiver.Dribbler.Release();
                }
                receiver.SetActive(false); UnityEngine.Object.Destroy(receiver.gameObject); receiver = null;
                human.Body.position = Vector2.zero; human.transform.position = Vector2.zero;

                foreach (GoalkeeperBrain keeper in keepers)
                {
                    ResetKeeper(keeper); int catches = keeper.Catches, distributions = keeper.Distributions;
                    Serve(new Vector2(keeper.Side * 8.75f, 0), new Vector2(keeper.Side * 3f, 0));
                    await Wait(() => keeper.Catches > catches, 1, keeper.name + " catches a loose slow ball");
                    Check(possession.IsOwner(keeper.GetComponent<PlayerController>()), "Catch gives the goalkeeper controlled possession");
                    if (keeper.Side > 0)
                    {
                        await Wait(() => keeper.State == GoalkeeperBrain.KeeperState.Distributing, 1.5f, "Keeper prepares to distribute possession");
                        testingPause = true; manager.PauseMatch(); await Delay(0.1f);
                        Check(possession.IsOwner(keeper.GetComponent<PlayerController>()), "Pausing a distribution preserves keeper possession");
                        manager.ResumeMatch(); testingPause = false;
                    }
                    await Wait(() => keeper.Distributions > distributions && !possession.IsOwner(keeper.GetComponent<PlayerController>()),
                        2.5f, keeper.name + " releases a controlled distribution");
                    Check(ball.Body.linearVelocity.x * keeper.Side < -9f, "Keeper distributes toward the pitch, away from their own goal");
                    await Delay(0.3f);
                    Check(!Physics2D.GetIgnoreCollision(keeper.GetComponent<Collider2D>(), ball.Collider),
                        "Distribution restores ball collision after separation");
                }

                var redKeeper = keepers.First(k => k.Side > 0);
                ResetKeeper(redKeeper); int parries = redKeeper.Parries;
                Serve(new Vector2(7.8f, 0.1f), Vector2.right * 20);
                await Wait(() => redKeeper.Parries > parries, 0.8f, "Goalkeeper parries a hard central shot");
                Check(ball.Body.linearVelocity.x < 0 && ball.Body.linearVelocity.y > 0, "Parry sends the ball out and wide instead of dead-stopping it");
                Check(ball.GoalScorer(Team.Blue) == human, "A keeper deflection preserves attacking scorer attribution");
                await Delay(0.6f); ResetKeeper(redKeeper);
                Serve(new Vector2(6.2f, 1.1f), Vector2.right * 13);
                float shotAt = Time.time;
                await Delay(0.10f);
                Check(redKeeper.State != GoalkeeperBrain.KeeperState.Diving, "Keeper does not dive before the reaction delay");
                await Wait(() => redKeeper.State == GoalkeeperBrain.KeeperState.Diving, 0.3f, "Keeper commits a lateral dive after reading an angled shot");
                Check(Time.time - shotAt >= 0.18f, "Dive respects the human-scale reaction delay");
                await Capture("keeper-dive"); await Delay(0.6f);

                ResetKeeper(redKeeper, 0.9f); ResetKeeper(keepers.First(k => k.Side < 0));
                int goals = 0; Action<Team> goal = team => goals++;
                manager.GoalScored += goal;
                AudioPreferences.SetMuted(false); AudioPreferences.SetVolume(1);
                Serve(new Vector2(8.25f, -1.0f), Vector2.right * 20);
                await Wait(() => goals == 1, 1f, "A well-placed close-range corner shot can beat the goalkeeper");
                manager.GoalScored -= goal;
                await Delay(0.45f);
                var voices = audio.GetComponents<AudioSource>();
                AudioSource cheer = voices.First(v => v.clip != null && v.clip.name == "goal_cheer");
                AudioSource groan = voices.First(v => v.clip != null && v.clip.name == "crowd_disappointed");
                Check(cheer.isPlaying && groan.isPlaying && cheer.panStereo < 0 && groan.panStereo > 0 && cheer.volume > groan.volume * 3,
                    "Scoring supporters cheer strongly while opposition groans from the other section");
                int variant = audio.CrowdVariation;
                Check(voices.Length == 5 && audio.CrowdCelebrations > 0, "Goal layers use a bounded five-voice mix");
                var sting = voices.First(v => v.clip != null && v.clip.name.Contains("Goal"));
                audio.Play(FootballAudio.Cue.Pass);
                Check(sting.isPlaying, "A ball cue does not interrupt the separate goal sting");
                float peak = 0, energy = 0; float[] output = new float[1024];
                for (int i = 0; i < 16; i++)
                {
                    AudioListener.GetOutputData(output, 0);
                    foreach (float sample in output) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
                    await Delay(0.02f);
                }
                Check(peak > 0.01f && peak < 0.98f, "Full-volume goal mix is audible and remains below digital clipping");
                File.AppendAllText(Report, $"MEASURE live audio peak {peak:F4}, energy {energy:F4}\n");
                await Capture("goal-celebration");
                var previousClip = manager.GetComponent<GoalReplayRecorder>().Latest;
                await Wait(() => manager.GetComponent<GoalReplayRecorder>().Latest != previousClip, 5, "Goal produces a new playable in-game highlight");
                var clip = manager.GetComponent<GoalReplayRecorder>().Latest;
                Check(clip.goalkeepers != null && clip.goalkeepers.Count(k => k) == 2 && clip.frames[0].players.Length == 3,
                    "Highlight retains both goalkeeper roles and recorded poses");
                await Wait(() => manager.IsMatchLive, 4, "Kick-off restores both keepers after a goal");
                testingPause = true; manager.PauseMatch(); Click(menu, "ReplayLastGoal"); await Delay(0.2f);
                Check(menu.CurrentScreen == MatchMenu.Screen.Replay && UnityEngine.Object.FindObjectsByType<GoalkeeperBrain>(FindObjectsSortMode.None).Length >= 4,
                    "Replay constructs both goalkeeper kits and glove rigs");
                await Capture("keeper-replay"); menu.TryBack(); manager.ResumeMatch(); testingPause = false;

                // Score at the other end to check mirrored sound sections and an actual new variant.
                foreach (GoalkeeperBrain keeper in keepers) ResetKeeper(keeper, keeper.Side < 0 ? -0.9f : 0);
                goals = 0; manager.GoalScored += goal;
                Serve(new Vector2(-8.25f, 1f), Vector2.left * 20);
                await Wait(() => goals == 1, 1f, "A corner shot also scores at the opposite goal"); manager.GoalScored -= goal;
                await Delay(0.4f);
                Check(audio.CelebratingTeam == Team.Red && audio.CrowdVariation != variant && cheer.panStereo > 0 && groan.panStereo < 0,
                    "Next goal changes crowd variation and swaps celebrating/opposing sections");
                manager.StartPractice(); await Delay(0.1f);
                Check(manager.IsPracticing && keepers.All(k => !k.gameObject.activeSelf), "Passing practice excludes keepers and their collisions");
                manager.StartMatch(); await Wait(() => manager.IsMatchLive, 4, "Returning from practice restores normal match flow");
                Check(keepers.All(k => k.gameObject.activeSelf && k.State == GoalkeeperBrain.KeeperState.Tracking),
                    "Both keepers return to tracking at their kickoff positions");
                await Capture("match-with-keepers");
                var timings = new List<float>(); var allocations = new List<long>();
                using (var gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame"))
                {
                    int lastFrame = Time.frameCount;
                    for (int i = 0; i < 90; i++)
                    {
                        while (Time.frameCount == lastFrame) await Task.Delay(2);
                        lastFrame = Time.frameCount;
                        human.SetMove(i < 45 ? Vector2.up : Vector2.down); human.SetSprint(i >= 45);
                        if (i > 10) { timings.Add(Time.unscaledDeltaTime * 1000); if (gc.Valid) allocations.Add(gc.LastValue); }
                    }
                }
                human.SetMove(Vector2.zero); human.SetSprint(false); timings.Sort(); allocations.Sort();
                File.AppendAllText(Report, $"MEASURE editor frame time median {timings[timings.Count / 2]:F2}ms, p95 {timings[(int)(timings.Count * 0.95f)]:F2}ms; view {Screen.width}x{Screen.height}\n");
                if (allocations.Count > 0) File.AppendAllText(Report, $"MEASURE editor GC/frame median {allocations[allocations.Count / 2]} bytes; includes editor, replay recording and async validation overhead\n");
                manager.OpenMainMenu(); await Delay(0.6f); await Capture("main-menu");
                File.AppendAllText(Report, "COMPLETE All live gameplay checks passed.\n");
                return File.ReadAllText(Report);
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, error + "\n"); throw;
            }
            finally
            {
                UnityEditor.EditorApplication.update -= keepReviewLive;
                if (receiver != null) UnityEngine.Object.Destroy(receiver.gameObject);
                AudioPreferences.SetVolume(savedVolume); AudioPreferences.SetMuted(savedMute);
                if (human != null) input.enabled = inputWasEnabled;
                if (manager != null) manager.OpenMainMenu();
            }
        }
    }
}
