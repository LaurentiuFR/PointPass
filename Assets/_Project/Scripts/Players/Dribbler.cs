using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Close control. When the ball is in front of the player's feet he picks it up: the ball
    /// follows a point just ahead of his boots and is nudged forward and side to side in time
    /// with his stride, like a real dribble. Stand still and it rests at his feet. A ball behind
    /// him or struck hard is not picked up and just bounces off him.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class Dribbler : MonoBehaviour
    {
        [Header("Pick-up")]
        [SerializeField] private float acquireRadius = 0.85f;
        [SerializeField] private float releaseRadius = 1.3f;
        [SerializeField] private float maxPickupSpeed = 9f;
        [SerializeField] private float pickupCooldown = 0.25f;

        [Header("Feet")]
        [SerializeField] private float baseLead = 0.64f;   // ball distance in front of his centre
        [SerializeField] private float touchLead = 0.09f;  // extra push on each touch
        [SerializeField] private float sway = 0.07f;       // side-to-side movement toward the active foot
        [SerializeField] private float followGain = 16f;   // how tightly the ball tracks his feet
        [SerializeField] private float maxBallSpeed = 12f;
        [SerializeField] private float speedWhileDribbling = 0.92f;

        private Ball ball;
        private PlayerAnimator animator;
        private PlayerMovement movement;
        private Rigidbody2D body;
        private Collider2D ownCollider;
        private float noPickupUntil;

        public bool HasBall { get; private set; }

        public void Init(Ball ball, PlayerAnimator animator)
        {
            this.ball = ball;
            this.animator = animator;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
            movement = GetComponent<PlayerMovement>();
        }

        private void FixedUpdate()
        {
            if (ball == null) return;

            if (!movement.InputEnabled)
            {
                if (HasBall) Release();
                return;
            }

            if (!HasBall) TryPickUp();
            if (HasBall) Carry();
        }

        /// <summary>Lets go of the ball (used for kicks, tackles and resets).</summary>
        public void Release()
        {
            if (HasBall && ball != null && ownCollider != null)
            {
                Physics2D.IgnoreCollision(ownCollider, ball.Collider, false);
            }
            HasBall = false;
            movement.SpeedMultiplier = 1f;
            noPickupUntil = Time.time + pickupCooldown;
        }

        private void TryPickUp()
        {
            if (Time.time < noPickupUntil) return;

            Vector2 ballPosition = ball.Body.position;
            Vector2 toBall = ballPosition - body.position;

            if (Vector2.Distance(ballPosition, FeetTarget()) > acquireRadius) return;
            if (toBall.sqrMagnitude > 0.0001f && Vector2.Dot(toBall.normalized, transform.up) < 0.1f) return; // must be in front
            if ((ball.Body.linearVelocity - body.linearVelocity).magnitude > maxPickupSpeed) return;

            HasBall = true;
            Physics2D.IgnoreCollision(ownCollider, ball.Collider, true);
            movement.SpeedMultiplier = speedWhileDribbling;
        }

        private void Carry()
        {
            Vector2 error = FeetTarget() - ball.Body.position;
            if (error.magnitude > releaseRadius)
            {
                Release();
                return;
            }

            Vector2 desired = body.linearVelocity + error * followGain;
            ball.Body.linearVelocity = Vector2.ClampMagnitude(desired, maxBallSpeed);
        }

        // The spot the ball is steered toward, in world space.
        private Vector2 FeetTarget()
        {
            float phase = animator != null ? animator.Phase : 0f;
            float intensity = animator != null ? animator.Intensity : 0f;

            // Push peaks each time a boot is furthest forward (twice per stride cycle).
            float lead = baseLead + touchLead * intensity * -Mathf.Cos(2f * phase);
            float side = -sway * intensity * Mathf.Sin(phase);

            return body.position + (Vector2)transform.up * lead + (Vector2)transform.right * side;
        }
    }
}
