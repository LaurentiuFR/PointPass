using System;
using System.Reflection;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PointPass.Validation
{
    /// <summary>Synthetic device checks executed only in the isolated editor validation project.</summary>
    public static class InputFeelValidation
    {
        private static void Call(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

        public static void Run(Action<bool, string> check)
        {
            Gamepad previousPad = Gamepad.current;
            Keyboard previousKeyboard = Keyboard.current;
            Gamepad pad = null;
            Keyboard keyboard = null;
            GameObject playerObject = null;
            GameObject ballObject = null;
            MatchHud hud = null;
            try
            {
                pad = InputSystem.AddDevice<Gamepad>();
                keyboard = InputSystem.AddDevice<Keyboard>();
                pad.MakeCurrent();
                keyboard.MakeCurrent();
                hud = MatchHud.Create();
                ballObject = new GameObject("Input validation ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                Ball ball = ballObject.GetComponent<Ball>();
                Call(ball, "Awake");
                ball.Body.gravityScale = 0f;
                playerObject = new GameObject("Input validation player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker), typeof(HumanPlayerInput));
                PlayerController controller = playerObject.GetComponent<PlayerController>();
                Dribbler dribbler = playerObject.GetComponent<Dribbler>();
                PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>();
                HumanPlayerInput input = playerObject.GetComponent<HumanPlayerInput>();
                Call(dribbler, "Awake");
                Call(kicker, "Awake");
                Call(input, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null);
                kicker.Init(ball, possession);
                input.Init(hud);
                Call(input, "OnEnable");

                Action<GamepadState> sendPad = state =>
                {
                    InputSystem.QueueStateEvent(pad, state);
                    InputSystem.Update();
                    Call(input, "Update");
                };
                Action<KeyboardState> sendKeys = state =>
                {
                    InputSystem.QueueStateEvent(keyboard, state);
                    InputSystem.Update();
                    Call(input, "Update");
                };

                possession.TryAcquire(controller);
                sendPad(new GamepadState().WithButton(GamepadButton.South));
                check(controller.ChargingKick == PlayerKicker.KickType.Pass, "Gamepad south begins pass charge");
                sendPad(new GamepadState());
                Call(kicker, "FixedUpdate");
                check(!possession.HasOwner && ball.Body.linearVelocity.magnitude > 0f, "Gamepad south release executes physical pass");

                possession.TryAcquire(controller);
                sendPad(new GamepadState().WithButton(GamepadButton.East));
                check(controller.ChargingKick == PlayerKicker.KickType.Shot, "Gamepad east begins shot charge");
                sendPad(new GamepadState().WithButton(GamepadButton.East).WithButton(GamepadButton.North));
                check(controller.ChargingKick == PlayerKicker.KickType.None && possession.IsOwner(controller),
                    "Gamepad north cancels charge without releasing possession");
                sendPad(new GamepadState());
                sendKeys(new KeyboardState(Key.J));
                check(controller.ChargingKick == PlayerKicker.KickType.Pass, "Keyboard J begins pass charge");
                sendKeys(new KeyboardState(Key.J, Key.Escape));
                check(controller.ChargingKick == PlayerKicker.KickType.None && possession.IsOwner(controller),
                    "Escape cancels charge without a kick");
                sendKeys(new KeyboardState(Key.J));
                check(controller.ChargingKick == PlayerKicker.KickType.None, "Cancelled held key cannot restart charge");
                sendKeys(new KeyboardState());

                sendPad(new GamepadState().WithButton(GamepadButton.South));
                Call(input, "OnApplicationFocus", false);
                Call(input, "OnApplicationFocus", true);
                Call(input, "Update");
                check(controller.ChargingKick == PlayerKicker.KickType.None, "Held gamepad charge is suppressed after focus restoration");
                sendPad(new GamepadState());
                sendPad(new GamepadState().WithButton(GamepadButton.South));
                check(controller.ChargingKick == PlayerKicker.KickType.Pass, "Fresh gamepad press works after focus restoration");
                InputSystem.RemoveDevice(pad);
                pad = null;
                InputSystem.Update();
                Call(input, "Update");
                Call(kicker, "FixedUpdate");
                check(controller.ChargingKick == PlayerKicker.KickType.None && possession.IsOwner(controller),
                    "Unplugging gamepad cancels charge instead of firing");
            }
            finally
            {
                if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
                if (ballObject != null) UnityEngine.Object.DestroyImmediate(ballObject);
                if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            }
        }
    }
}
