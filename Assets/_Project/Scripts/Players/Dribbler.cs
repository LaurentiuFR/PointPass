using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Contact-based control: discrete boot touches preserve a free, interceptable ball.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Dribbler : MonoBehaviour
    {
        [Header("Pick-up")]
        [SerializeField] private float acquireRadius = 0.25f;
        [SerializeField] private float releaseRadius = 0.64f;
        [SerializeField] private float maxPickupSpeed = 11f;
        [SerializeField] private float pickupCooldown = 0.25f;

        [Header("Feet")]
        [SerializeField] private float baseLead = 0.53f;
        [SerializeField] private float touchLead = 0.035f;
        [SerializeField] private float sway = 0.025f;
        [SerializeField] private float maxBallSpeed = 14f;
        [SerializeField] private float speedWhileDribbling = 0.92f;

        private Ball ball;
        private PossessionSystem possession;
        private PlayerAnimator animator;
        private PlayerController controller;
        private PlayerMovement movement;
        private Rigidbody2D body;
        private Collider2D ownCollider;
        private bool carrying;          // local mirror of "possession says I own the ball"
        private float noPickupUntil;
        private bool waitingForSeparation;
        private float restoreCollisionAt;
        private float touchWait;
        public bool AutomaticPickup { get; set; } = true;

        public bool HasBall => possession != null && possession.IsOwner(controller);

        public void Init(Ball ball, PossessionSystem possession, PlayerAnimator animator)
        {
            this.ball = ball;
            this.possession = possession;
            this.animator = animator;
        }

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            movement = GetComponent<PlayerMovement>();
            body = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
        }

        private void FixedUpdate()
        {
            if (ball == null || possession == null) return;
            touchWait = Mathf.Max(0, touchWait - Time.fixedDeltaTime);

            if (waitingForSeparation && (Time.time >= restoreCollisionAt
                || ownCollider.Distance(ball.Collider).distance > 0.02f)) RestoreCollision();

            // Match paused or frozen: let go of everything.
            if (!controller.IsActive)
            {
                if (carrying) Release();
                return;
            }

            // Someone else (or a reset) took ownership away from us.
            if (carrying && !possession.IsOwner(controller))
            {
                StopCarrying();
                return;
            }

            if (!carrying && AutomaticPickup) TryPickUp();
            if (carrying) Carry();
        }

        /// <summary>Lets go of the ball (kicks, tackles, resets and pauses all end up here).</summary>
        public void Release()
        {
            possession?.Release(controller);
            StopCarrying();
            RestoreCollision();
        }

        public void ReleaseForKick()
        {
            if (!HasBall) return;
            possession.Release(controller);
            carrying = false;
            movement.SpeedMultiplier = 1f;
            noPickupUntil = Time.time + pickupCooldown;
            waitingForSeparation = true;
            restoreCollisionAt = Time.time + pickupCooldown;
            Physics2D.IgnoreCollision(ownCollider, ball.Collider, true);
        }

        public void Dispossess(float recoverySeconds)
        {
            Release();
            noPickupUntil = Time.time + Mathf.Max(.05f, recoverySeconds);
        }

        private void RestoreCollision()
        {
            if (ball != null && ownCollider != null) Physics2D.IgnoreCollision(ownCollider, ball.Collider, false);
            waitingForSeparation = false;
        }

        private void OnDisable() => Release();

        private void TryPickUp()
        {
            if (waitingForSeparation || Time.time < noPickupUntil) return;

            Vector2 ballPosition = ball.Body.position;
            Vector2 toBall = ballPosition - body.position;

            // The player's actual contact envelope must reach the ball, rather than a large
            // acquisition sphere ahead of the player sucking it towards a tracking point.
            if (toBall.sqrMagnitude > .75f * .75f || Vector2.Distance(ballPosition, FeetTarget()) > acquireRadius) return;
            if (toBall.sqrMagnitude > 0.0001f && Vector2.Dot(toBall.normalized, Facing()) < 0.1f) return; // must be in front
            float receiveLimit = ball.LastKickWasPass ? 22f : maxPickupSpeed;
            if ((ball.Body.linearVelocity - body.linearVelocity).magnitude > receiveLimit) return;

            if (!possession.TryAcquire(controller)) return; // somebody else already has it

            carrying = true;
            ball.RecordTouch(controller);
            ball.SetCurve(0f);
            ball.Body.linearVelocity = Vector2.Lerp(ball.Body.linearVelocity, body.linearVelocity, .78f);
            touchWait = 0;
            Physics2D.IgnoreCollision(ownCollider, ball.Collider, true);
            movement.SpeedMultiplier = speedWhileDribbling;
        }

        private void Carry()
        {
            Vector2 error = FeetTarget() - ball.Body.position;
            Vector2 toBall = ball.Body.position - body.position;
            float forward = Vector2.Dot(toBall.normalized, Facing());
            float leash = movement.IsSprinting ? releaseRadius + .16f : releaseRadius;
            if (error.sqrMagnitude > leash * leash || forward < -.12f)
            {
                Release();
                return;
            }

            if (touchWait > 0 || toBall.sqrMagnitude > .80f * .80f || forward < .15f) return;
            touchWait = movement.IsSprinting ? .12f : .16f;
            Vector2 facing = Facing();
            Vector2 right = new Vector2(facing.y, -facing.x);
            // A small push and limited lateral foot correction. There is no continuous
            // positional spring pulling an escaping ball backwards around a sharp turn.
            float push = body.linearVelocity.sqrMagnitude > .05f ? (movement.IsSprinting ? 1.45f : .65f) : 0;
            Vector2 desired = body.linearVelocity + facing * push
                + right * Mathf.Clamp(Vector2.Dot(error,right)*3f,-.65f,.65f);
            Vector2 impulse = Vector2.ClampMagnitude(desired - ball.Body.linearVelocity, 4.2f);
            ball.Body.linearVelocity = Vector2.ClampMagnitude(ball.Body.linearVelocity + impulse, maxBallSpeed);
        }

        private void StopCarrying()
        {
            if (carrying)
            {
                Physics2D.IgnoreCollision(ownCollider, ball.Collider, false);
                carrying = false;
            }
            movement.SpeedMultiplier = 1f;
            noPickupUntil = Time.time + pickupCooldown;
        }

        // The spot the ball is steered toward, in world space.
        private Vector2 FeetTarget()
        {
            float phase = animator != null ? animator.Phase : 0f;
            float intensity = animator != null ? animator.Intensity : 0f;

            // Push peaks each time a boot is furthest forward (twice per stride cycle).
            float lead = baseLead + (movement.IsSprinting ? .09f : 0) + touchLead * intensity * -Mathf.Cos(2f * phase);
            float side = -sway * intensity * Mathf.Sin(phase);

            Vector2 forward = Facing();
            Vector2 right = new Vector2(forward.y, -forward.x);
            return body.position + forward * lead + right * side;
        }

        private Vector2 Facing()
        {
            float angle = body.rotation * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
        }
    }
}
