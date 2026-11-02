using System;
using System.IO;
using System.Reflection;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Actual Play Mode/coroutine smoke test, restricted to the disposable preview project.</summary>
    [InitializeOnLoad]
    public static class MenuPlayModeVerification
    {
        private const string Key = "PointPass.MenuPlayModeVerification.";
        private static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../AppearanceVerification/playmode-tests.txt"));

        static MenuPlayModeVerification()
        {
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project.Name != "AppearancePreview" || project.Parent.Name != "Logs")
                throw new InvalidOperationException("Play Mode verification only runs in Logs/AppearancePreview.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start verification outside Play Mode.");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, "Actual Unity Play Mode menu verification\n");
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "finishing", false);
            SessionState.SetBool(Key + "success", false);
            SessionState.SetInt(Key + "stage", 0);
            SessionState.SetInt(Key + "frame", -1);
            SessionState.SetFloat(Key + "started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }

        private static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Write(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Check(bool condition, string name)
        {
            File.AppendAllText(ReportPath, (condition ? "PASS " : "FAIL ") + name + "\n");
            if (!condition) throw new InvalidOperationException(name);
        }

        private static T Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static void Click(MatchMenu menu, string name)
        {
            foreach (Button button in menu.GetComponentsInChildren<Button>())
            {
                if (button.name != name || !button.isActiveAndEnabled || !button.IsInteractable()) continue;
                button.onClick.Invoke();
                return;
            }
            throw new InvalidOperationException("No active menu button: " + name);
        }

        private static void Advance(int stage)
        {
            SessionState.SetInt(Key + "stage", stage);
            SessionState.SetFloat(Key + "stageStarted", (float)EditorApplication.timeSinceStartup);
        }

        private static void Finish(bool success, string detail)
        {
            File.AppendAllText(ReportPath, (success ? "COMPLETE " : "FAILED ") + detail + "\n");
            SessionState.SetBool(Key + "success", success);
            SessionState.SetBool(Key + "finishing", true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (SessionState.GetBool(Key + "finishing", false))
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool success = SessionState.GetBool(Key + "success", false);
                SessionState.SetBool(Key + "active", false);
                SessionState.SetBool(Key + "finishing", false);
                EditorApplication.Exit(success ? 0 : 1);
                return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "started", 0) > 45)
                    throw new TimeoutException("Menu Play Mode verification exceeded 45 seconds at stage "
                        + SessionState.GetInt(Key + "stage", -1));
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                // EditorApplication.update can run multiple times per game frame, including while paused.
                if (SessionState.GetInt(Key + "frame", -1) == Time.frameCount) return;
                SessionState.SetInt(Key + "frame", Time.frameCount);

                MatchManager manager = Find<MatchManager>();
                MatchMenu menu = Find<MatchMenu>();
                PlayerController player = Find<PlayerController>();
                Ball ball = Find<Ball>();
                if (manager == null || menu == null || player == null || ball == null) return;
                PossessionSystem possession = Read<PossessionSystem>(manager, "possession");
                string phase = Read<object>(manager, "phase").ToString();
                int stage = SessionState.GetInt(Key + "stage", 0);
                switch (stage)
                {
                    case 0:
                        if (!manager.IsInMainMenu || !menu.IsOpen) return;
                        Check(menu.CurrentScreen == MatchMenu.Screen.Main && !player.IsActive && !ball.Body.simulated,
                            "Actual runtime bootstrap opens Main with match frozen");
                        Check(UnityEngine.Object.FindObjectsByType<MatchManager>(FindObjectsSortMode.None).Length == 1,
                            "Runtime bootstrap creates one match manager");
                        Advance(1);
                        break;
                    case 1:
                        Click(menu, "PlayOffline");
                        Check(!menu.IsOpen && !player.IsActive, "Play button enters the real frozen kickoff countdown");
                        Advance(2);
                        break;
                    case 2:
                        if (phase != "Playing" || Read<float>(manager, "clock") > 89.9f) return;
                        Check(player.IsActive && ball.Body.simulated && !menu.IsOpen,
                            "Actual kickoff coroutine enables gameplay and timer advances");
                        ball.ResetTo(player.Body.position + player.AimDirection * 0.64f);
                        possession.TryAcquire(player);
                        player.Body.linearVelocity = Vector2.right * 1.2f;
                        ball.Body.linearVelocity = Vector2.right * 1.2f;
                        player.BeginKick(PlayerKicker.KickType.Pass);
                        manager.PauseMatch();
                        Check(manager.IsUserPaused && menu.CurrentScreen == MatchMenu.Screen.Pause && Time.timeScale == 0,
                            "Pause opens the real modal and stops game time");
                        Check(possession.IsOwner(player) && player.ChargingKick == PlayerKicker.KickType.None && !player.IsActive,
                            "Pause retains possession while cancelling charge and gameplay input");
                        SessionState.SetFloat(Key + "clock", Read<float>(manager, "clock"));
                        SessionState.SetFloat(Key + "px", player.Body.position.x);
                        SessionState.SetFloat(Key + "py", player.Body.position.y);
                        SessionState.SetFloat(Key + "bx", ball.Body.position.x);
                        SessionState.SetFloat(Key + "by", ball.Body.position.y);
                        Advance(3);
                        break;
                    case 3:
                        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "stageStarted", 0) < 0.35) return;
                        Check(Mathf.Abs(Read<float>(manager, "clock") - SessionState.GetFloat(Key + "clock", -1)) < 0.0001f,
                            "Clock remains unchanged through real paused frames");
                        Check(player.Body.position == new Vector2(SessionState.GetFloat(Key + "px", 0), SessionState.GetFloat(Key + "py", 0))
                            && ball.Body.position == new Vector2(SessionState.GetFloat(Key + "bx", 0), SessionState.GetFloat(Key + "by", 0))
                            && possession.IsOwner(player), "Real paused physics retains player, ball and possession");
                        Click(menu, "Resume");
                        Check(!manager.IsUserPaused && !menu.IsOpen && Time.timeScale == 1 && possession.IsOwner(player),
                            "Resume button restores game time and retains the match");
                        Advance(4);
                        break;
                    case 4:
                        if (Read<float>(manager, "clock") >= SessionState.GetFloat(Key + "clock", -1) - 0.1f) return;
                        Check(true, "Timer continues after actual resume");
                        manager.PauseMatch();
                        Advance(5);
                        break;
                    case 5:
                        Click(menu, "Restart");
                        Check(!menu.IsOpen && !manager.IsUserPaused && Time.timeScale == 1 && Read<float>(manager, "clock") == 90,
                            "Restart button restores time and starts fresh kickoff");
                        Check(!possession.HasOwner && Read<int>(manager, "blueScore") == 0 && Read<int>(manager, "redScore") == 0,
                            "Restart clears possession and both scores");
                        Advance(6);
                        break;
                    case 6:
                        if (phase != "Playing") return;
                        Check(player.IsActive, "Restart kickoff coroutine completes normally");
                        manager.PauseMatch();
                        Advance(7);
                        break;
                    case 7:
                        Click(menu, "MainMenu");
                        Check(manager.IsInMainMenu && menu.IsOpen && menu.CurrentScreen == MatchMenu.Screen.Main
                            && !manager.IsUserPaused && Time.timeScale == 1 && !player.IsActive,
                            "Pause Main Menu abandons match and restores global game time");
                        Advance(8);
                        break;
                    case 8:
                        Click(menu, "HowToPlay");
                        Check(menu.CurrentScreen == MatchMenu.Screen.Controls && manager.IsInMainMenu,
                            "How to Play opens without starting gameplay");
                        Advance(9);
                        break;
                    case 9:
                        Click(menu, "Back");
                        Check(menu.CurrentScreen == MatchMenu.Screen.Main, "Controls Back returns to Main");
                        Advance(10);
                        break;
                    case 10:
                        Click(menu, "PlayOffline");
                        Advance(11);
                        break;
                    case 11:
                        if (phase != "Playing") return;
                        // Keep the actual period-end coroutine; only shorten the match fixture's clock.
                        Write(manager, "half", 2);
                        Write(manager, "blueScore", 2);
                        Write(manager, "redScore", 1);
                        Write(manager, "clock", 0.05f);
                        Advance(12);
                        break;
                    case 12:
                        if (phase != "FullTime" || !menu.IsOpen) return;
                        Check(menu.CurrentScreen == MatchMenu.Screen.Results && !player.IsActive && !ball.Body.simulated,
                            "Actual final-period coroutine opens Results with gameplay frozen");
                        Check(Read<Text>(menu, "resultTitle").text == "BLUE WINS"
                            && Read<Text>(menu, "resultScore").text.Contains("2")
                            && Read<Text>(menu, "resultScore").text.Contains("1"),
                            "Results displays the final winner and score");
                        Advance(13);
                        break;
                    case 13:
                        Click(menu, "PlayAgain");
                        Check(!menu.IsOpen && Read<float>(manager, "clock") == 90
                            && Read<int>(manager, "blueScore") == 0 && Read<int>(manager, "redScore") == 0,
                            "Results Play Again starts a clean rematch");
                        Advance(14);
                        break;
                    case 14:
                        if (phase != "Playing") return;
                        Check(player.IsActive && !manager.IsUserPaused && !menu.IsOpen && Time.timeScale == 1,
                            "Rematch kickoff completes and gameplay is active");
                        Finish(true, "Main, Play, pause, resume, restart, controls, results and rematch passed in real Play Mode.");
                        break;
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }
    }
}
