using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Close control. When the ball is in front of the player's feet he asks the PossessionSystem
    /// for it; if he gets it, the ball follows a point just ahead of his boots and is nudged in
    /// time with his stride. Ownership lives in PossessionSystem, never here, and nothing in this
    /// class depends on who is controlling the player.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Dribbler : MonoBehaviour
    {
        [Header("Pick-up")]
        [SerializeField] private float acquireRadius = 0.85f;
        [SerializeField] private float releaseRadius = 1.55f;
        [SerializeField] private float maxPickupSpeed = 11f;
        [SerializeField] private float pickupCooldown = 0.25f;

        [Header("Feet")]
        [SerializeField] private float baseLead = 0.64f;   // ball distance in front of his centre
        [SerializeField] private float touchLead = 0.09f;  // extra push on each touch
        [SerializeField] private float sway = 0.07f;       // side-to-side movement toward the active foot
        [SerializeField] private float followGain = 20f;   // how tightly the ball tracks his feet
        [SerializeField] private float maxBallSpeed = 14f;
        [SerializeField] private float speedWhileDribbling = 0.92f;

        private Ball ball;
        private PossessionSystem possession;
        private PlayerAnimator animator;
        private SimulationStride simulationStride;
        private PlayerController controller;
        private PlayerMovement movement;
        private Rigidbody2D body;
        private Collider2D ownCollider;
        private bool carrying;          // local mirror of "possession says I own the ball"
        private float noPickupUntil;
        private bool waitingForSeparation;
        private float restoreCollisionAt;
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
            simulationStride = GetComponent<SimulationStride>();
        }

        private void FixedUpdate()
        {
            if (ball == null || possession == null) return;

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

            if (Vector2.Distance(ballPosition, FeetTarget()) > acquireRadius) return;
            if (toBall.sqrMagnitude > 0.0001f && Vector2.Dot(toBall.normalized, Facing()) < 0.1f) return; // must be in front
            float receiveLimit = ball.LastKickWasPass ? 22f : maxPickupSpeed;
            if ((ball.Body.linearVelocity - body.linearVelocity).magnitude > receiveLimit) return;

            if (!possession.TryAcquire(controller)) return; // somebody else already has it

            carrying = true;
            ball.RecordTouch(controller);
            ball.SetCurve(0f);
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
            float phase = animator != null ? animator.Phase : simulationStride != null ? simulationStride.Phase : 0f;
            float intensity = animator != null ? animator.Intensity : simulationStride != null ? simulationStride.Intensity : 0f;

            // Push peaks each time a boot is furthest forward (twice per stride cycle).
            float lead = baseLead + touchLead * intensity * -Mathf.Cos(2f * phase);
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
