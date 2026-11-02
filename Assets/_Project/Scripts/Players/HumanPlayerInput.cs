using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PointPass.Players
{
    /// <summary>
    /// Turns a person's input (on-screen joystick, WASD/arrows, gamepad) into commands for one
    /// player. Only the human-controlled player has this component.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class HumanPlayerInput : MonoBehaviour
    {
        private PlayerController controller;
        private VirtualJoystick joystick;
        private MatchHud hud;
        private bool passHeld;
        private bool shotHeld;
        private bool gamepadPassHeld;
        private bool gamepadShotHeld;
        private bool cancelHeld;
        private Gamepad actionGamepad;
        private bool sprintReady;
        private bool focused = true;
        private bool applicationPaused;
        private bool usingGamepad;
        private bool wasControllerActive;
        private enum KickSource { None, KeyboardPass, KeyboardShot, TouchPass, TouchShot, GamepadPass, GamepadShot }
        private KickSource kickSource;

        public void Init(MatchHud hud)
        {
            this.hud = hud;
            joystick = hud.Joystick;
            hud.PassButton.Pressed += OnPassDown;
            hud.PassButton.Released += OnPassUp;
            hud.PassButton.Cancelled += OnPassCancelled;
            hud.ShootButton.Pressed += OnShotDown;
            hud.ShootButton.Released += OnShotUp;
            hud.ShootButton.Cancelled += OnShotCancelled;
            hud.ShootButton.FlickChanged += OnShotFlick;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            wasControllerActive = controller.IsActive;
        }

        private void OnEnable()
        {
            if (controller == null) return;
            ClearActions();
            controller.SetMove(Vector2.zero);
            SuppressHeldKeys();
            wasControllerActive = controller.IsActive;
        }

        /// <summary>Consume menu-submit/held actions before returning control to gameplay.</summary>
        public void SuppressUntilRelease()
        {
            if (controller == null) return;
            ClearActions();
            controller.SetMove(Vector2.zero);
            SuppressHeldKeys();
            wasControllerActive = false;
        }

        private void SuppressHeldKeys()
        {
            passHeld = Keyboard.current != null && Keyboard.current.jKey.isPressed;
            shotHeld = Keyboard.current != null && Keyboard.current.kKey.isPressed;
            gamepadPassHeld = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
            gamepadShotHeld = Gamepad.current != null && Gamepad.current.buttonEast.isPressed;
            cancelHeld = (Keyboard.current != null && Keyboard.current.escapeKey.isPressed)
                || (Gamepad.current != null && Gamepad.current.buttonNorth.isPressed);
            actionGamepad = Gamepad.current;
            sprintReady = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.leftStick.ReadValue().sqrMagnitude > 0.04f
                || gamepad.buttonSouth.isPressed || gamepad.buttonEast.isPressed || gamepad.rightShoulder.isPressed))
                usingGamepad = true;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (joystick != null && joystick.Value.sqrMagnitude > 0.01f)
                || (hud != null && (hud.PassButton.IsHeld || hud.ShootButton.IsHeld || hud.SprintButton.IsHeld)))
                usingGamepad = false;
            bool pass = keyboard != null && keyboard.jKey.isPressed;
            bool shot = keyboard != null && keyboard.kKey.isPressed;
            bool padPass = gamepad != null && gamepad.buttonSouth.isPressed;
            bool padShot = gamepad != null && gamepad.buttonEast.isPressed;
            bool cancelDown = (keyboard != null && keyboard.escapeKey.isPressed)
                || (gamepad != null && gamepad.buttonNorth.isPressed);
            if (gamepad != actionGamepad)
            {
                // A replacement pad must not release the old pad's kick or inherit its held buttons.
                if (kickSource == KickSource.GamepadPass || kickSource == KickSource.GamepadShot)
                    CancelKick(kickSource);
                gamepadPassHeld = padPass;
                gamepadShotHeld = padShot;
                sprintReady = false;
                actionGamepad = gamepad;
            }
            bool sprint = (keyboard != null && keyboard.leftShiftKey.isPressed)
                || (gamepad != null && gamepad.rightShoulder.isPressed)
                || (hud != null && hud.SprintButton.IsHeld);
            bool sprintActive = false;
            bool reactivated = controller.IsActive && !wasControllerActive;
            wasControllerActive = controller.IsActive;
            if (reactivated) SuppressHeldKeys();

            if (!controller.IsActive || !focused || applicationPaused || reactivated)
            {
                ClearActions();
                controller.SetMove(Vector2.zero);
            }
            else
            {
                controller.SetMove(ReadMove());
                UpdateShotCurve(keyboard, gamepad);
                bool cancel = !cancelHeld && (cancelDown
                    || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                    || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame));
                if (cancel) CancelCurrentKick();
                else
                {
                    ProcessKickButton(keyboard?.jKey, passHeld, PlayerKicker.KickType.Pass, KickSource.KeyboardPass);
                    ProcessKickButton(keyboard?.kKey, shotHeld, PlayerKicker.KickType.Shot, KickSource.KeyboardShot);
                    ProcessKickButton(gamepad?.buttonSouth, gamepadPassHeld, PlayerKicker.KickType.Pass, KickSource.GamepadPass);
                    ProcessKickButton(gamepad?.buttonEast, gamepadShotHeld, PlayerKicker.KickType.Shot, KickSource.GamepadShot);
                }
                if (!sprint) sprintReady = true;
                sprintActive = sprintReady && sprint;
                controller.SetSprint(sprintActive);
            }
            passHeld = pass;
            shotHeld = shot;
            gamepadPassHeld = padPass;
            gamepadShotHeld = padShot;
            cancelHeld = cancelDown;
            if (hud != null)
            {
                hud.SetCharge(controller.ChargingKick, controller.KickCharge, controller.ShotCurve);
                hud.SetPlayerState(controller.IsActive && focused && !applicationPaused,
                    controller.Dribbler != null && controller.Dribbler.HasBall, usingGamepad);
                hud.PassButton.SetCommandHeld(controller.ChargingKick == PlayerKicker.KickType.Pass);
                hud.ShootButton.SetCommandHeld(controller.ChargingKick == PlayerKicker.KickType.Shot);
                hud.SprintButton.SetCommandHeld(sprintActive);
            }
        }

        private void ProcessKickButton(ButtonControl button, bool wasHeld, PlayerKicker.KickType type, KickSource source)
        {
            // Losing a device cancels its charge instead of treating disconnect as a kick release.
            if (button == null) { CancelKick(source); return; }
            if (!wasHeld && (button.isPressed || button.wasPressedThisFrame)) BeginKick(type, source);
            if ((!button.isPressed && wasHeld) || button.wasReleasedThisFrame) ReleaseKick(type, source);
        }

        private void CancelCurrentKick()
        {
            controller.CancelKick();
            kickSource = KickSource.None;
            if (hud == null) return;
            hud.PassButton.Cancel();
            hud.ShootButton.Cancel();
        }

        private void BeginKick(PlayerKicker.KickType type, KickSource source)
        {
            if (!isActiveAndEnabled || !focused || applicationPaused || !controller.IsActive || !wasControllerActive
                || controller.ChargingKick != PlayerKicker.KickType.None) return;
            controller.BeginKick(type);
            if (controller.ChargingKick == type) kickSource = source;
        }

        private void ReleaseKick(PlayerKicker.KickType type, KickSource source)
        {
            if (kickSource != source) return;
            UpdateShotCurve(Keyboard.current, Gamepad.current);
            controller.ReleaseKick(type);
            kickSource = KickSource.None;
        }

        private void CancelKick(KickSource source)
        {
            if (kickSource != source) return;
            controller.CancelKick();
            kickSource = KickSource.None;
        }

        private void OnPassDown() => BeginKick(PlayerKicker.KickType.Pass, KickSource.TouchPass);
        private void OnPassUp() => ReleaseKick(PlayerKicker.KickType.Pass, KickSource.TouchPass);
        private void OnPassCancelled() => CancelKick(KickSource.TouchPass);
        private void OnShotDown() => BeginKick(PlayerKicker.KickType.Shot, KickSource.TouchShot);
        private void OnShotUp() => ReleaseKick(PlayerKicker.KickType.Shot, KickSource.TouchShot);
        private void OnShotCancelled() => CancelKick(KickSource.TouchShot);

        private void OnShotFlick(Vector2 gesture)
        {
            if (kickSource != KickSource.TouchShot || !controller.IsActive) return;
            Vector2 aim = controller.AimDirection;
            controller.SetShotCurve(aim.x * gesture.y - aim.y * gesture.x);
        }

        private void UpdateShotCurve(Keyboard keyboard, Gamepad gamepad)
        {
            if (controller.ChargingKick != PlayerKicker.KickType.Shot) return;
            if (kickSource == KickSource.KeyboardShot)
                controller.SetShotCurve((keyboard != null && keyboard.qKey.isPressed ? 1f : 0f)
                    - (keyboard != null && keyboard.eKey.isPressed ? 1f : 0f));
            else if (kickSource == KickSource.GamepadShot)
            {
                Vector2 bend = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
                Vector2 aim = controller.AimDirection;
                controller.SetShotCurve(aim.x * bend.y - aim.y * bend.x);
            }
        }

        private void ClearActions()
        {
            controller.CancelActions();
            kickSource = KickSource.None;
            sprintReady = false;
            if (joystick != null) joystick.ResetInput();
            if (hud == null) return;
            hud.PassButton.Cancel();
            hud.ShootButton.Cancel();
            hud.SprintButton.Cancel();
            hud.PassButton.SetCommandHeld(false);
            hud.ShootButton.SetCommandHeld(false);
            hud.SprintButton.SetCommandHeld(false);
            hud.SetPlayerState(false, false, usingGamepad);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            ClearActions();
            controller.SetMove(Vector2.zero);
            wasControllerActive = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) OnDisable();
            else
            {
                SuppressHeldKeys();
                wasControllerActive = controller != null && controller.IsActive;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) OnDisable();
            else
            {
                SuppressHeldKeys();
                wasControllerActive = controller != null && controller.IsActive;
            }
        }

        private void OnDestroy()
        {
            if (hud == null) return;
            hud.PassButton.Pressed -= OnPassDown;
            hud.PassButton.Released -= OnPassUp;
            hud.PassButton.Cancelled -= OnPassCancelled;
            hud.ShootButton.Pressed -= OnShotDown;
            hud.ShootButton.Released -= OnShotUp;
            hud.ShootButton.Cancelled -= OnShotCancelled;
            hud.ShootButton.FlickChanged -= OnShotFlick;
        }

        private Vector2 ReadMove()
        {
            Vector2 result = Vector2.zero;

            if (joystick != null)
            {
                result = joystick.Value;
            }

            if (result.sqrMagnitude < 0.01f)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) result.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) result.y -= 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) result.x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) result.x += 1f;
                }

                Gamepad gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    Vector2 stick = gamepad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > result.sqrMagnitude) result = stick;
                }
            }

            return Vector2.ClampMagnitude(result, 1f);
        }
    }
}
