using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;

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

        public void Init(VirtualJoystick joystick)
        {
            this.joystick = joystick;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            controller.SetMove(controller.IsActive ? ReadMove() : Vector2.zero);
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
