using System;
using System.IO;
using System.Reflection;
using PointPass.Audio;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Exercises the approved offline features through actual runtime UI and physics.</summary>
    [InitializeOnLoad]
    public static class OfflineFeaturesPlayMode
    {
        private const string Key = "PointPass.OfflineFeatures.";
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../OfflineVerification"));
        private static string Report => Path.Combine(Folder, "playmode-tests.txt");
        static OfflineFeaturesPlayMode() { EditorApplication.update += Tick; }
        public static void Run()
        {
            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project.Name != "AppearancePreview" || project.Parent.Name != "Logs") throw new InvalidOperationException("Disposable project only.");
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Report, "Actual offline-feature Play Mode verification\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key + "active", true); SessionState.SetBool(Key + "finishing", false);
            SessionState.SetInt(Key + "stage", 0); SessionState.SetInt(Key + "frame", -1);
            SessionState.SetFloat(Key + "started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        private static T Find<T>() where T : UnityEngine.Object
        {
            foreach (T candidate in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                if (!(candidate is Component c) || c.GetComponentInParent<GoalReplayWorld>(true) == null) return candidate;
            return null;
        }
        private static void Check(bool passed, string description)
        {
            File.AppendAllText(Report, (passed ? "PASS " : "FAIL ") + description + "\n");
            if (!passed) throw new InvalidOperationException(description);
        }
        private static void Click(Transform parent, string name)
        {
            foreach (Button button in parent.GetComponentsInChildren<Button>())
                if (button.name == name && button.isActiveAndEnabled && button.IsInteractable()) { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("No active button " + name);
        }
        private static void Stage(int value)
        {
            SessionState.SetInt(Key + "stage", value);
            SessionState.SetFloat(Key + "stageTime", Time.unscaledTime);
        }
        private static void Finish(bool passed, string message)
        {
            if (SessionState.GetBool(Key + "preferences", false))
            {
                AudioPreferences.SetVolume(SessionState.GetFloat(Key + "volume", 0.65f));
                AudioPreferences.SetMuted(SessionState.GetBool(Key + "muted", false));
            }
            File.AppendAllText(Report, (passed ? "COMPLETE " : "FAILED ") + message + "\n");
            SessionState.SetBool(Key + "success", passed); SessionState.SetBool(Key + "finishing", true);
            EditorApplication.ExitPlaymode();
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (SessionState.GetBool(Key + "finishing", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key + "active", false);
                EditorApplication.Exit(SessionState.GetBool(Key + "success", false) ? 0 : 1); return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "started", 0) > 60)
                    throw new TimeoutException("Offline feature verification stage " + SessionState.GetInt(Key + "stage", -1));
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key + "frame", -1) == Time.frameCount) return;
                SessionState.SetInt(Key + "frame", Time.frameCount);
                MatchManager manager = Find<MatchManager>(); MatchMenu menu = Find<MatchMenu>();
                MatchHud hud = Find<MatchHud>(); PlayerController player = Find<PlayerController>(); Ball ball = Find<Ball>();
                if (manager == null || menu == null || hud == null || player == null || ball == null) return;
                PassingPractice practice = Find<PassingPractice>();
                switch (SessionState.GetInt(Key + "stage", 0))
                {
                    case 0:
                        if (!manager.IsInMainMenu) return;
                        Application.runInBackground = true;
                        SessionState.SetFloat(Key + "volume", AudioPreferences.Volume); SessionState.SetBool(Key + "muted", AudioPreferences.Muted);
                        SessionState.SetBool(Key + "preferences", true);
                        Click(menu.transform, "AudioSettings"); Stage(1); break;
                    case 1:
                        Check(menu.CurrentScreen == MatchMenu.Screen.Audio && manager.IsInMainMenu, "Sound settings open without starting a match");
                        menu.GetComponentInChildren<Slider>().value = 0.35f;
                        menu.GetComponentInChildren<Toggle>().isOn = true;
                        Check(Mathf.Abs(AudioPreferences.Volume - 0.35f) < 0.01f && AudioPreferences.Muted, "Audio slider and mute control update saved preferences");
                        Capture("sound-settings.png", menu.GetComponent<Canvas>());
                        Check(menu.TryBack() && menu.CurrentScreen == MatchMenu.Screen.Main, "Sound Back returns to the originating menu");
                        Stage(2); break;
                    case 2:
                        Click(menu.transform, "PassingPractice"); Stage(3); break;
                    case 3:
                        Check(manager.IsPracticing && practice.IsRunning && player.IsActive && !menu.IsOpen, "Practice starts immediately with active player control");
                        Check(practice.Attempts == 0 && practice.Hits == 0 && hud.transform.Find("SafeArea/PracticeHUD").gameObject.activeSelf, "Practice has its own visible readout and clean counters");
                        Capture("passing-practice.png", hud.GetComponent<Canvas>());
                        player.GetComponent<HumanPlayerInput>().enabled = false;
                        player.GetComponent<PlayerMovement>().ResetTo(new Vector2(1.4f, 0), -90);
                        practice.ResetBall(); Stage(4); break;
                    case 4:
                        if (!player.Dribbler.HasBall) return;
                        player.BeginKick(PlayerKicker.KickType.Pass); player.ReleaseKick(PlayerKicker.KickType.Pass);
                        Stage(5); break;
                    case 5:
                        if (practice.Hits == 0) return;
                        Check(practice.Hits == 1 && practice.Attempts == 1 && practice.ActiveGate == 1, "A physical pass crosses the active gate once and selects the opposite gate");
                        Capture("practice-gate-hit.png", hud.GetComponent<Canvas>());
                        manager.OnGoal(Team.Blue);
                        Check(manager.IsPracticing && practice.Hits == 1, "Pitch goals do not interrupt practice or start match celebrations");
                        manager.PauseMatch(); Stage(6); break;
                    case 6:
                        Check(manager.IsUserPaused && Time.timeScale == 0 && menu.CurrentScreen == MatchMenu.Screen.Pause, "Practice pause freezes the live session");
                        Check(menu.transform.Find("SafeArea/Content/Pause/PauseCard/Restart/Label").GetComponent<Text>().text == "RESTART PRACTICE", "Pause labels identify the current practice session");
                        Click(menu.transform, "Restart"); Stage(7); break;
                    case 7:
                        Check(manager.IsPracticing && !manager.IsUserPaused && practice.Attempts == 0 && practice.Hits == 0, "Restart Practice resets the drill and restores time");
                        player.GetComponent<HumanPlayerInput>().enabled = true;
                        practice.ResetBall(); Stage(8); break;
                    case 8:
                        if (!player.Dribbler.HasBall) return;
                        hud.ShootButton.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 91, position = new Vector2(100, 100) });
                        Stage(9); break;
                    case 9:
                        if (Time.unscaledTime - SessionState.GetFloat(Key + "stageTime", 0) < 0.35f) return;
                        Check(player.ChargingKick == PlayerKicker.KickType.Shot, "A touch hold starts a shot during practice");
                        hud.ShootButton.OnDrag(new PointerEventData(EventSystem.current) { pointerId = 91, position = new Vector2(100, 190) });
                        hud.SetCharge(player.ChargingKick, player.KickCharge, player.ShotCurve);
                        Capture("mobile-bend-cancel.png", hud.GetComponent<Canvas>());
                        Click(hud.transform, "CancelCharge");
                        hud.ShootButton.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 91, position = new Vector2(100, 100) });
                        Stage(10); break;
                    case 10:
                        Check(player.ChargingKick == PlayerKicker.KickType.None && player.Dribbler.HasBall, "Touch cancellation retains possession and suppresses release-to-kick");
                        manager.OpenMainMenu(); Stage(11); break;
                    case 11:
                        Check(!manager.IsPracticing && !practice.IsRunning && !hud.transform.Find("SafeArea/PracticeHUD").gameObject.activeSelf, "Returning home removes practice state and gate presentation");
                        Click(menu.transform, "PlayOffline"); Stage(12); break;
                    case 12:
                        if (!player.IsActive || player.Body.simulated == false) return;
                        Check(!manager.IsPracticing && !practice.IsRunning, "A normal timed match starts after leaving practice");
                        Check(Find<FootballAudio>() != null && Find<FootballAudio>().GetComponent<AudioSource>() != null, "Live bootstrap creates the football sound source");
                        manager.PauseMatch();
                        menu.SetReplay(ReplayValidation.Clip()); Click(menu.transform, "ReplayLastGoal");
                        GoalReplayView view = menu.GetComponentInChildren<GoalReplayView>(true);
                        view.Advance(0.3f);
                        view.SetPlaybackSpeed(1.5f); view.SetShowShotArrow(true); Click(menu.transform, "PauseReplay");
                        SessionState.SetFloat(Key + "playhead", view.PlaybackTime); Stage(13); break;
                    case 13:
                        GoalReplayView paused = menu.GetComponentInChildren<GoalReplayView>(true);
                        Check(paused.PlaybackTime == SessionState.GetFloat(Key + "playhead", -1), "Replay Pause holds the timeline through real rendered frames");
                        Click(menu.transform, "RestartReplay");
                        Check(paused.PlaybackTime == 0 && paused.ReplayIsPaused && paused.PlaybackSpeed == 1.5f && paused.ShowShotArrow, "Replay Restart preserves pause, speed and arrow preferences");
                        Capture("replay-paused.png", menu.GetComponent<Canvas>());
                        Click(menu.transform, "PauseReplay"); Stage(14); break;
                    case 14:
                        GoalReplayView resumed = menu.GetComponentInChildren<GoalReplayView>(true);
                        if (resumed.PlaybackTime < 0.15f) return;
                        Check(Time.timeScale == 0 && manager.IsUserPaused, "Replay Resume advances independently while the live match stays paused");
                        Finish(true, "Practice, sound settings, mobile cancellation and replay controls passed in actual Play Mode."); break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }
        private static void Capture(string name, Canvas canvas)
        {
            // Reuse the existing graphics review's restoring capture implementation.
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../MatchVisualReview")));
            typeof(MatchVisualReview).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "offline-" + name, 1920, 1080, canvas, null });
        }
    }
}
