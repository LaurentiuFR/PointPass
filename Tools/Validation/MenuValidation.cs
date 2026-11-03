using System;
using System.Collections.Generic;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Menu/pause contracts exercised in the isolated editor project.</summary>
    public static class MenuValidation
    {
        private static void Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static T Get<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetPhase(MatchManager manager, string phase)
        {
            FieldInfo field = typeof(MatchManager).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(manager, Enum.Parse(field.FieldType, phase));
        }

        private static int Subscribers(MatchMenu menu, string name)
        {
            Delegate handler = typeof(MatchMenu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(menu) as Delegate;
            return handler?.GetInvocationList().Length ?? 0;
        }

        public static void Run(Action<bool, string> check)
        {
            float originalScale = Time.timeScale;
            GameObject ballObject = null;
            GameObject playerObject = null;
            GameObject managerObject = null;
            MatchHud hud = null;
            MatchMenu menu = null;
            try
            {
                ballObject = new GameObject("Menu test ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                Ball ball = ballObject.GetComponent<Ball>();
                Call(ball, "Awake");
                ball.Body.gravityScale = 0;
                playerObject = new GameObject("Menu test player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
                PlayerController player = playerObject.GetComponent<PlayerController>();
                Dribbler dribbler = playerObject.GetComponent<Dribbler>();
                PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>();
                Call(dribbler, "Awake");
                Call(kicker, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null);
                kicker.Init(ball, possession);
                player.Init(Team.Blue, new Vector2(-3, 0), -90);
                hud = MatchHud.Create();
                menu = MatchMenu.Create();
                managerObject = new GameObject("Menu test manager", typeof(MatchManager));
                MatchManager manager = managerObject.GetComponent<MatchManager>();
                manager.Init(ball, possession, new List<PlayerController> { player }, hud);
                manager.SetMenu(menu);
                manager.SetMenu(menu);
                check(Subscribers(menu, "PlayRequested") == 1 && Subscribers(menu, "ResumeRequested") == 1,
                    "Reattaching a menu does not duplicate action subscriptions");

                manager.OpenMainMenu();
                check(manager.IsInMainMenu && menu.IsOpen && !ball.Body.simulated && !player.IsActive,
                    "Main menu freezes the prototype and blocks gameplay input");
                check(!possession.HasOwner, "Main menu leaves no stale ball ownership");

                // Seed an in-progress match without starting editor-mode coroutines.
                SetPhase(manager, "Playing");
                Call(manager, "SetPlaying", true);
                player.SetInputEnabled(true);
                menu.Hide();
                Set(manager, "blueScore", 2);
                Set(manager, "redScore", 1);
                Set(manager, "half", 2);
                Set(manager, "clock", 63.25f);
                player.Body.position = new Vector2(1, 1);
                player.Body.linearVelocity = Vector2.right * 3;
                ball.Body.position = new Vector2(1.5f, 1);
                ball.Body.linearVelocity = Vector2.up * 7;
                possession.TryAcquire(player);
                player.BeginKick(PlayerKicker.KickType.Pass);
                Time.timeScale = 0.75f;
                manager.PauseMatch();
                manager.PauseMatch();
                check(manager.IsUserPaused && Time.timeScale == 0 && menu.IsOpen,
                    "Pause is idempotent and stops scaled match time");
                check(!player.IsActive && player.Body.simulated && ball.Body.simulated,
                    "Pause gates input while preserving the simulated bodies");
                check(possession.IsOwner(player) && player.Body.position == new Vector2(1, 1)
                    && ball.Body.position == new Vector2(1.5f, 1), "Pause preserves possession and physical positions");
                check(player.Body.linearVelocity == Vector2.right * 3 && ball.Body.linearVelocity == Vector2.up * 7,
                    "Pause preserves player and ball momentum");
                check(player.ChargingKick == PlayerKicker.KickType.None, "Pause cancels a held kick charge");
                Call(manager, "Update");
                manager.OnGoal(Team.Red);
                check(Mathf.Abs(Get<float>(manager, "clock") - 63.25f) < 0.001f
                    && Get<int>(manager, "blueScore") == 2 && Get<int>(manager, "redScore") == 1,
                    "Paused update and goal callbacks cannot advance clock or score");

                manager.ResumeMatch();
                manager.ResumeMatch();
                check(!manager.IsUserPaused && Mathf.Abs(Time.timeScale - 0.75f) < 0.001f && !menu.IsOpen,
                    "Resume is idempotent and restores the prior time scale");
                check(player.IsActive && possession.IsOwner(player) && Get<int>(manager, "half") == 2,
                    "Resume retains match period and possession");

                player.BeginKick(PlayerKicker.KickType.Shot);
                player.ReleaseKick(PlayerKicker.KickType.Shot);
                Vector2 prePauseBallVelocity = ball.Body.linearVelocity;
                manager.PauseMatch();
                manager.ResumeMatch();
                Call(kicker, "FixedUpdate");
                check(possession.IsOwner(player) && ball.Body.linearVelocity == prePauseBallVelocity,
                    "A queued kick cannot fire after pausing and resuming");

                ResumeInputConsumption(check, manager, player, playerObject, hud, possession);

                // User pause must not convert a goal/halftime freeze into active play.
                SetPhase(manager, "Paused");
                Call(manager, "SetPlaying", false);
                manager.PauseMatch();
                manager.ResumeMatch();
                check(!ball.Body.simulated && !player.IsActive,
                    "Resuming a match intermission keeps its physics freeze intact");

                SetPhase(manager, "Playing");
                Call(manager, "SetPlaying", true);
                player.SetInputEnabled(true);
                possession.TryAcquire(player);
                manager.PauseMatch();
                manager.OpenMainMenu();
                check(manager.IsInMainMenu && !manager.IsUserPaused && Mathf.Abs(Time.timeScale - 0.75f) < 0.001f,
                    "Returning home from pause releases the global time-scale lock");
                check(!possession.HasOwner && !ball.Body.simulated && !player.IsActive,
                    "Returning home cleans possession and freezes the match");

                SetPhase(manager, "Playing");
                Call(manager, "SetPlaying", true);
                player.SetInputEnabled(true);
                Call(manager, "EndMatch");
                check(menu.IsOpen && !ball.Body.simulated && !player.IsActive,
                    "Full time opens an explicit results menu and freezes gameplay");
                int scoreBefore = Get<int>(manager, "blueScore");
                Call(manager, "Update");
                check(Get<int>(manager, "blueScore") == scoreBefore && menu.IsOpen,
                    "Full-time update waits for a menu action instead of auto-restarting");

                SetPhase(manager, "Playing");
                Call(manager, "SetPlaying", true);
                player.SetInputEnabled(true);
                menu.Hide();
                manager.PauseMatch();
                Call(manager, "OnDisable");
                check(Mathf.Abs(Time.timeScale - 0.75f) < 0.001f,
                    "Disabling a paused match restores the previous time scale");

                MenuNavigation(check);
            }
            finally
            {
                if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
                if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
                if (ballObject != null) UnityEngine.Object.DestroyImmediate(ballObject);
                if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
                if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
                Time.timeScale = originalScale;
            }
        }

        private static void ResumeInputConsumption(Action<bool, string> check, MatchManager manager,
            PlayerController player, GameObject playerObject, MatchHud hud, PossessionSystem possession)
        {
            Gamepad previousPad = Gamepad.current;
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            HumanPlayerInput input = playerObject.AddComponent<HumanPlayerInput>();
            Call(input, "Awake");
            input.Init(hud);
            Call(input, "OnEnable");
            try
            {
                pad.MakeCurrent();
                manager.PauseMatch();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                Call(input, "Update");
                manager.ResumeMatch();
                input.SuppressUntilRelease();
                Call(input, "Update");
                Call(input, "Update");
                check(player.ChargingKick == PlayerKicker.KickType.None && possession.IsOwner(player),
                    "A held gamepad menu-submit button cannot start a pass on resume");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.Update();
                Call(input, "Update");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                Call(input, "Update");
                check(player.ChargingKick == PlayerKicker.KickType.Pass,
                    "Releasing menu submit and making a fresh press starts a pass");
                player.CancelKick();
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.Update();
                Call(input, "Update");

                input.SuppressUntilRelease();
                PointerEventData finger = new PointerEventData(UnityEngine.Object.FindFirstObjectByType<EventSystem>()) { pointerId = 77 };
                hud.PassButton.OnPointerDown(finger);
                check(player.ChargingKick == PlayerKicker.KickType.None,
                    "Same-frame touch after closing a menu cannot charge through the overlay");
                hud.PassButton.OnPointerUp(finger);
                Call(input, "Update");
                hud.PassButton.OnPointerDown(finger);
                check(player.ChargingKick == PlayerKicker.KickType.Pass,
                    "A fresh touch works after the resumed input frame is consumed");
                hud.PassButton.Cancel();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(input);
                if (pad.added) InputSystem.RemoveDevice(pad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
        }

        private static void Click(MatchMenu menu, Button button)
        {
            // Each call represents a discrete user action; editor tests run within one frame.
            Set(menu, "lastActivationFrame", -1);
            button.onClick.Invoke();
        }

        private static void MenuNavigation(Action<bool, string> check)
        {
            MatchMenu menu = MatchMenu.Create();
            EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            EventSystem previousSystem = EventSystem.current;
            EventSystem.current = system;
            bool registeredForTest = false;
            if (EventSystem.current != system)
            {
                // EventSystem is not ExecuteAlways: AddComponent in an editor-only fixture
                // does not necessarily register it in the static active-system list.
                Call(system, "OnEnable");
                registeredForTest = true;
                EventSystem.current = system;
            }
            try
            {
                int plays = 0;
                int resumes = 0;
                menu.PlayRequested += () => plays++;
                menu.ResumeRequested += () => resumes++;
                Button play = menu.transform.Find("SafeArea/Content/Main/CommunityHubPage/CommunityCard2").GetComponent<Button>();
                Button mainControls = menu.transform.Find("SafeArea/Content/Main/SettingsPage/Options/HowToPlay").GetComponent<Button>();
                Button pauseControls = menu.transform.Find("SafeArea/Content/Pause/PauseCard/Controls").GetComponent<Button>();

                menu.ShowMain();
                check(menu.IsOpen && menu.CurrentScreen == MatchMenu.Screen.Main
                    && system.currentSelectedGameObject == menu.Lobby.FirstButton.gameObject,
                    "Main menu selects Main Lobby for keyboard/gamepad navigation");
                menu.Lobby.ShowPage(LobbyMenuView.Page.Settings);
                Click(menu, mainControls);
                check(menu.IsOpen && menu.CurrentScreen == MatchMenu.Screen.Controls,
                    "How to Play opens a modal controls page");
                BaseEventData back = new BaseEventData(system);
                bool dispatched = ExecuteEvents.Execute(system.currentSelectedGameObject, back, ExecuteEvents.cancelHandler);
                check(dispatched && back.used && menu.CurrentScreen == MatchMenu.Screen.Main && resumes == 0,
                    "Cancel on the selected controls button returns to Main without resuming");

                menu.ShowPause();
                Click(menu, pauseControls);
                back = new BaseEventData(system);
                ExecuteEvents.Execute(system.currentSelectedGameObject, back, ExecuteEvents.cancelHandler);
                check(menu.CurrentScreen == MatchMenu.Screen.Pause && resumes == 0,
                    "Controls opened from Pause returns to Pause without emitting Resume");
                back = new BaseEventData(system);
                ExecuteEvents.Execute(system.currentSelectedGameObject, back, ExecuteEvents.cancelHandler);
                check(resumes == 1 && back.used, "Cancel on a selected Pause button emits one Resume request");

                Click(menu, play);
                check(plays == 0, "A hidden-page button cannot invoke its menu action");
                menu.ShowMain();
                menu.Hide();
                Click(menu, play);
                check(plays == 0 && system.currentSelectedGameObject == null,
                    "A hidden menu clears its selection and ignores stale button callbacks");

                menu.ShowResults(3, 1);
                Text title = menu.transform.Find("SafeArea/Content/Results/ResultsCard/Title").GetComponent<Text>();
                Text score = menu.transform.Find("SafeArea/Content/Results/ResultsCard/Score").GetComponent<Text>();
                check(menu.CurrentScreen == MatchMenu.Screen.Results && title.text == "BLUE WINS"
                    && score.text.Contains(">3<") && score.text.Contains(">1<"),
                    "Results display the actual winning side and score");
                menu.ShowResults(0, 2);
                check(title.text == "RED WINS", "Results identify a red-team win");
                menu.ShowResults(2, 2);
                check(title.text == "HONOURS EVEN", "Results represent a drawn match correctly");
                check(menu.transform.Find("ModalBackdrop").GetComponent<Image>().raycastTarget,
                    "Menu backdrop blocks pointer input from reaching the pitch");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menu.gameObject);
                if (registeredForTest) Call(system, "OnDisable");
                if (previousSystem != null) EventSystem.current = previousSystem;
            }
        }
    }
}
