using System;
using System.Reflection;
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
    /// <summary>Checks touch gesture ownership, scaling and shot-release ordering.</summary>
    public static class FlickValidation
    {
        private static void Call(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        public static void Run(Action<bool, string> check)
        {
            ButtonGestures(check);
            ShotRouting(check);
        }

        private static void ButtonGestures(Action<bool, string> check)
        {
            GameObject eventsObject = new GameObject("Flick events", typeof(EventSystem));
            GameObject canvasObject = new GameObject("Flick canvas", typeof(Canvas));
            GameObject buttonObject = new GameObject("Flick button", typeof(RectTransform), typeof(HoldActionButton));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            try
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.scaleFactor = 2;
                HoldActionButton button = buttonObject.GetComponent<HoldActionButton>();
                Call(button, "Awake");
                EventSystem system = eventsObject.GetComponent<EventSystem>();
                PointerEventData owner = new PointerEventData(system) { pointerId = 1, position = Vector2.zero };
                PointerEventData other = new PointerEventData(system) { pointerId = 2, position = Vector2.zero };
                int releases = 0;
                int cancels = 0;
                Vector2 releasedFlick = Vector2.zero;
                button.Released += () => { releases++; releasedFlick = button.Flick; };
                button.Cancelled += () => cancels++;

                button.OnPointerDown(owner);
                other.position = Vector2.up * 180;
                button.OnDrag(other);
                button.OnPointerUp(other);
                check(button.IsHeld && button.Flick == Vector2.zero && releases == 0,
                    "Other fingers cannot change or release the owning flick gesture");
                owner.position = Vector2.up * 180;
                button.OnDrag(owner);
                check(Vector2.Distance(button.Flick, Vector2.up) < 0.001f,
                    "Flick strength is normalized by canvas scale rather than screen resolution");
                button.OnPointerUp(owner);
                check(releases == 1 && Vector2.Distance(releasedFlick, Vector2.up) < 0.001f,
                    "Release listeners see the flick before gesture cleanup");
                check(button.Flick == Vector2.zero && !button.IsHeld,
                    "Released touch clears its flick and pointer ownership");

                owner.position = Vector2.zero;
                button.OnPointerDown(owner);
                owner.position = Vector2.down * 180;
                button.OnPointerUp(owner);
                check(Vector2.Distance(releasedFlick, Vector2.down) < 0.001f,
                    "A downward release gesture is recognized without a separate drag callback");

                owner.position = Vector2.zero;
                button.OnPointerDown(owner);
                owner.position = Vector2.up * 10;
                button.OnPointerUp(owner);
                check(releasedFlick == Vector2.zero, "Small thumb jitter does not count as a shot flick");

                owner.position = Vector2.zero;
                button.OnPointerDown(owner);
                owner.position = Vector2.up * 180;
                button.OnDrag(owner);
                Set(button, "lastMovedAt", Time.unscaledTime - 0.2f);
                Call(button, "Update");
                check(button.Flick == Vector2.zero, "Holding still clears an old flick preview");
                int priorReleases = releases;
                button.Cancel();
                button.OnPointerUp(owner);
                check(cancels == 1 && releases == priorReleases && button.Flick == Vector2.zero,
                    "Gesture cancellation clears bend without firing a release");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buttonObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(eventsObject);
            }
        }

        private static void ShotRouting(Action<bool, string> check)
        {
            MatchHud hud = MatchHud.Create();
            GameObject playerObject = new GameObject("Flick shot player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker), typeof(HumanPlayerInput));
            GameObject ballObject = new GameObject("Flick shot ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
            try
            {
                Ball ball = ballObject.GetComponent<Ball>();
                PlayerController controller = playerObject.GetComponent<PlayerController>();
                Dribbler dribbler = playerObject.GetComponent<Dribbler>();
                PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>();
                HumanPlayerInput input = playerObject.GetComponent<HumanPlayerInput>();
                Call(ball, "Awake");
                Call(dribbler, "Awake");
                Call(kicker, "Awake");
                Call(input, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null);
                kicker.Init(ball, possession);
                input.Init(hud);
                Call(input, "OnEnable");
                hud.GetComponent<CanvasScaler>().enabled = false;
                hud.GetComponent<Canvas>().scaleFactor = 1;
                Call(hud.ShootButton, "Awake");
                Call(hud.PassButton, "Awake");
                EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
                PointerEventData finger = new PointerEventData(system) { pointerId = 7, position = new Vector2(200, 200) };
                PointerEventData second = new PointerEventData(system) { pointerId = 8, position = new Vector2(200, 100) };

                possession.TryAcquire(controller);
                controller.SetMove(Vector2.right);
                hud.ShootButton.OnPointerDown(finger);
                finger.position += Vector2.up * 90;
                hud.ShootButton.OnDrag(finger);
                check(controller.ChargingKick == PlayerKicker.KickType.Shot && controller.ShotCurve > 0.99f,
                    "Upward Shoot flick with rightward aim routes a left bend through PlayerController");
                hud.ShootButton.OnDrag(second);
                hud.ShootButton.OnPointerUp(second);
                check(controller.ShotCurve > 0.99f && hud.ShootButton.IsHeld,
                    "Another finger cannot replace a charged shot's curve");
                hud.ShootButton.OnPointerUp(finger);
                check(controller.ChargingKick == PlayerKicker.KickType.None && ball.Curve == 0 && possession.IsOwner(controller),
                    "Touch shot release queues bend without early physical ownership release");
                Call(kicker, "FixedUpdate");
                check(ball.Curve > 0.99f && !possession.HasOwner,
                    "Gesture cleanup after release does not erase the queued shot bend");

                ball.ResetTo(Vector2.zero);
                possession.TryAcquire(controller);
                finger.position = new Vector2(200, 200);
                hud.ShootButton.OnPointerDown(finger);
                finger.position += Vector2.down * 90;
                hud.ShootButton.OnPointerUp(finger);
                Call(kicker, "FixedUpdate");
                check(ball.Curve < -0.99f,
                    "Downward Shoot flick with rightward aim routes a right bend");

                ball.ResetTo(Vector2.zero);
                possession.TryAcquire(controller);
                finger.position = new Vector2(200, 200);
                hud.ShootButton.OnPointerDown(finger);
                hud.ShootButton.OnPointerUp(finger);
                Call(kicker, "FixedUpdate");
                check(ball.Curve == 0 && ball.Body.linearVelocity.x > 0 && Mathf.Abs(ball.Body.linearVelocity.y) < 0.001f,
                    "Releasing Shoot without a flick produces a straight physical shot");

                ball.ResetTo(Vector2.zero);
                possession.TryAcquire(controller);
                finger.position = new Vector2(200, 200);
                hud.ShootButton.OnPointerDown(finger);
                finger.position += Vector2.up * 90;
                hud.ShootButton.OnDrag(finger);
                hud.ShootButton.Cancel();
                Call(kicker, "FixedUpdate");
                check(controller.ChargingKick == PlayerKicker.KickType.None && controller.ShotCurve == 0
                    && ball.Curve == 0 && possession.IsOwner(controller) && ball.Body.linearVelocity == Vector2.zero,
                    "Cancelling a curved touch charge keeps the ball and clears its bend");

                finger.position = new Vector2(200, 200);
                hud.PassButton.OnPointerDown(finger);
                finger.position += Vector2.up * 90;
                hud.PassButton.OnDrag(finger);
                hud.PassButton.OnPointerUp(finger);
                Call(kicker, "FixedUpdate");
                check(ball.Curve == 0 && !possession.HasOwner,
                    "Flicking the Pass button cannot apply shot curve to a pass");
                DeviceBend(check, controller, kicker, input, ball, possession);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(ballObject);
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
            }
        }

        private static void DeviceBend(Action<bool, string> check, PlayerController controller,
            PlayerKicker kicker, HumanPlayerInput input, Ball ball, PossessionSystem possession)
        {
            Keyboard previousKeyboard = Keyboard.current;
            Gamepad previousPad = Gamepad.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            keyboard.MakeCurrent();
            pad.MakeCurrent();
            Action<KeyboardState> keys = state =>
            {
                InputSystem.QueueStateEvent(keyboard, state);
                InputSystem.Update();
                Call(input, "Update");
            };
            Action<GamepadState> gamepad = state =>
            {
                InputSystem.QueueStateEvent(pad, state);
                InputSystem.Update();
                Call(input, "Update");
            };
            try
            {
                keys(new KeyboardState());
                ball.ResetTo(Vector2.zero);
                possession.TryAcquire(controller);
                keys(new KeyboardState(Key.D, Key.K, Key.Q));
                keys(new KeyboardState(Key.D, Key.K, Key.Q));
                check(controller.ChargingKick == PlayerKicker.KickType.Shot && controller.ShotCurve > 0.99f,
                    "Keyboard Q bends the current charged shot left");
                keys(new KeyboardState(Key.D, Key.K, Key.E));
                check(controller.ShotCurve < -0.99f, "Keyboard E changes the charged shot to right bend");
                keys(new KeyboardState(Key.D, Key.E));
                Call(kicker, "FixedUpdate");
                check(ball.Curve < -0.99f, "Keyboard release retains its selected shot bend through the physics queue");

                keys(new KeyboardState());
                ball.ResetTo(Vector2.zero);
                possession.TryAcquire(controller);
                GamepadState up = new GamepadState { leftStick = Vector2.right, rightStick = Vector2.up }
                    .WithButton(GamepadButton.East);
                gamepad(up);
                gamepad(up);
                check(controller.ChargingKick == PlayerKicker.KickType.Shot && controller.ShotCurve > 0.99f,
                    "Gamepad right stick sets left bend relative to current aim");
                gamepad(new GamepadState { leftStick = Vector2.right, rightStick = Vector2.down }.WithButton(GamepadButton.East));
                check(controller.ShotCurve < -0.99f, "Gamepad right-stick reversal changes shot bend direction");
                gamepad(new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.East));
                check(controller.ShotCurve == 0, "Centring the gamepad right stick restores a straight shot");
                gamepad(new GamepadState { leftStick = Vector2.right, rightStick = Vector2.up });
                Call(kicker, "FixedUpdate");
                check(ball.Curve > 0.99f, "Gamepad release samples the latest right-stick bend");
            }
            finally
            {
                if (pad.added) InputSystem.RemoveDevice(pad);
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
        }
    }
}
