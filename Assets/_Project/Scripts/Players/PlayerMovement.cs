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
        [SerializeField] private float maxSpeed = 4.8f;
        [SerializeField] private float steeringResponse = 0.12f;
        [SerializeField] private float brakingResponse = 0.09f;
        [SerializeField] private float turnDegreesPerSecond = 840f;
        [SerializeField] private float sprintMultiplier = 1.35f;

        private Rigidbody2D body;
        private Vector2 moveInput;
        private Vector2 lastAim;
        private bool hasAim;
        private bool sprinting;
        private Vector2 velocityChange;

        public void SetSprint(bool held) => sprinting = held;
        public void SetAimDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            lastAim = direction.normalized; hasAim = true;
        }

        public Rigidbody2D Body => body != null ? body : (body = GetComponent<Rigidbody2D>());
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Immediate directional intent, retained when the stick is released.</summary>
        public Vector2 AimDirection
        {
            get
            {
                if (hasAim) return lastAim;
                float angle = Body.rotation * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            }
        }

        public void SetMoveInput(Vector2 direction)
        {
            moveInput = direction.sqrMagnitude <= 0.01f ? Vector2.zero : Vector2.ClampMagnitude(direction, 1f);
            if (moveInput != Vector2.zero) { lastAim = moveInput.normalized; hasAim = true; }
        }

        public void ResetTo(Vector2 position, float angle)
        {
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            Body.rotation = angle;
            transform.position = position;
            moveInput = Vector2.zero;
            hasAim = false;
            sprinting = false;
            velocityChange = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (!Body.simulated) return;

            float topSpeed = maxSpeed * SpeedMultiplier * (sprinting ? sprintMultiplier : 1f);
            Vector2 targetVelocity = moveInput * topSpeed;
            bool hasInput = moveInput.sqrMagnitude > 0.01f;
            // Smooth acceleration itself so steering keeps a short, controllable arc rather
            // than abruptly replacing momentum. Reversals still brake more promptly.
            bool braking = !hasInput || Vector2.Dot(Body.linearVelocity, targetVelocity) < 0f
                || Body.linearVelocity.sqrMagnitude > targetVelocity.sqrMagnitude;
            Vector2 previousVelocity = Body.linearVelocity;
            Vector2 nextVelocity = Vector2.SmoothDamp(previousVelocity, targetVelocity,
                ref velocityChange, braking ? brakingResponse : steeringResponse,
                Mathf.Infinity, Time.fixedDeltaTime);
            // Prevent steering from creating speed. When sprint is released, the old speed
            // can coast down smoothly instead of being clamped instantly to walking speed.
            nextVelocity = Vector2.ClampMagnitude(nextVelocity, Mathf.Max(topSpeed, previousVelocity.magnitude));
            if (!hasInput && nextVelocity.sqrMagnitude < 0.0001f)
            {
                nextVelocity = Vector2.zero;
                velocityChange = Vector2.zero;
            }
            Body.linearVelocity = nextVelocity;

            if (hasAim)
            {
                // Finish the latest commanded turn even if the stick was released quickly.
                // Bounded angular speed settles exactly, without the tail of an exponential lerp.
                float angle = Mathf.Atan2(lastAim.y, lastAim.x) * Mathf.Rad2Deg - 90f;
                Body.rotation = Mathf.MoveTowardsAngle(Body.rotation, angle,
                    turnDegreesPerSecond * Time.fixedDeltaTime);
            }
        }
    }
}
