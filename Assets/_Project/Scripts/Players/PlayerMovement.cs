using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Moves a footballer with momentum. It knows nothing about who is controlling him:
    /// it just executes the direction it is given through SetMoveInput.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float deceleration = 35f;
        [SerializeField] private float turnSpeed = 15f;

        private Rigidbody2D body;
        private Vector2 moveInput;

        public Rigidbody2D Body => body != null ? body : (body = GetComponent<Rigidbody2D>());
        public float SpeedMultiplier { get; set; } = 1f;

        public void SetMoveInput(Vector2 direction)
        {
            moveInput = Vector2.ClampMagnitude(direction, 1f);
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

        private void FixedUpdate()
        {
            if (!Body.simulated) return;

            Vector2 targetVelocity = moveInput * (maxSpeed * SpeedMultiplier);
            bool hasInput = moveInput.sqrMagnitude > 0.01f;
            float rate = hasInput ? acceleration : deceleration;

            Body.linearVelocity = Vector2.MoveTowards(
                Body.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);

            if (hasInput)
            {
                // Sprite "up" is the facing direction.
                float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
                Body.rotation = Mathf.LerpAngle(Body.rotation, angle, turnSpeed * Time.fixedDeltaTime);
            }
        }
    }
}
