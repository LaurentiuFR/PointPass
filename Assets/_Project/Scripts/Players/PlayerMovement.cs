using PointPass.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PointPass.Players
{
    /// <summary>
    /// Moves a footballer with momentum. Reads the on-screen joystick (phones),
    /// plus keyboard (WASD / arrows) and gamepad for testing in the editor.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float deceleration = 35f;
        [SerializeField] private float turnSpeed = 15f;

        private VirtualJoystick joystick;
        private Rigidbody2D body;
        private Vector2 moveInput;

        public Rigidbody2D Body => body != null ? body : (body = GetComponent<Rigidbody2D>());
        public bool InputEnabled { get; set; } = true;
        public float SpeedMultiplier { get; set; } = 1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public void Bind(VirtualJoystick stick)
        {
            joystick = stick;
        }

        public void ResetTo(Vector2 position, float angle)
        {
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            Body.rotation = angle;
            transform.position = position;
            moveInput = Vector2.zero;
        }

        private void Update()
        {
            moveInput = InputEnabled ? ReadInput() : Vector2.zero;
        }

        private void FixedUpdate()
        {
            Vector2 targetVelocity = moveInput * (maxSpeed * SpeedMultiplier);
            bool hasInput = moveInput.sqrMagnitude > 0.01f;
            float rate = hasInput ? acceleration : deceleration;

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            if (hasInput)
            {
                // Sprite "up" is the facing direction.
                float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
                body.rotation = Mathf.LerpAngle(body.rotation, angle, turnSpeed * Time.fixedDeltaTime);
            }
        }

        private Vector2 ReadInput()
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
