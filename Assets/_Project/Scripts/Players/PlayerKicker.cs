using System;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>Controller-independent charged kicks. Input queues a kick; physics executes it.</summary>
    [RequireComponent(typeof(PlayerController), typeof(Dribbler))]
    public class PlayerKicker : MonoBehaviour
    {
        public enum KickType { None, Pass, Shot }

        [SerializeField] private float chargeSeconds = 1f;
        [SerializeField] private float passChargeSeconds = 0.65f;
        [SerializeField] private Vector2 passSpeed = new Vector2(8f, 14f);
        [SerializeField] private Vector2 shotSpeed = new Vector2(10f, 20f);
        [SerializeField, Range(0f, 1f)] private float forwardMomentum = 0.25f;

        private PlayerController controller;
        private Ball ball;
        private PossessionSystem possession;
        private KickType charging;
        private float chargeStarted;
        private bool pending;
        private Vector2 pendingVelocity;
        private KickType pendingType;
        private float shotCurve;
        private float pendingCurve;

        /// <summary>Presentation listens after the physical kick has executed.</summary>
        public event Action<KickType> Kicked;

        public KickType Charging => charging;
        public float ShotCurve => charging == KickType.Shot ? shotCurve : 0f;
        public float Charge => charging == KickType.None ? 0f
            : Mathf.Clamp01((Time.time - chargeStarted)
                / Mathf.Max(0.01f, charging == KickType.Pass ? passChargeSeconds : chargeSeconds));

        public void Init(Ball ball, PossessionSystem possession)
        {
            this.ball = ball;
            this.possession = possession;
            possession.OwnerChanged += OnOwnerChanged;
        }

        private void Awake() => controller = GetComponent<PlayerController>();

        public void Begin(KickType type)
        {
            if (type == KickType.None || charging != KickType.None || pending || !CanKick()) return;
            charging = type;
            shotCurve = 0f;
            chargeStarted = Time.time;
        }

        /// <summary>Sets the bend for the current charged shot; other actions ignore it.</summary>
        public void SetShotCurve(float curve)
        {
            if (charging != KickType.Shot || !CanKick()) return;
            shotCurve = float.IsNaN(curve) || float.IsInfinity(curve) ? 0f : Mathf.Clamp(curve, -1f, 1f);
        }

        public void Release(KickType type)
        {
            if (charging == KickType.None || charging != type) return;
            if (!CanKick()) { Cancel(); return; }

            Vector2 range = type == KickType.Pass ? passSpeed : shotSpeed;
            // Use the command direction immediately: visual turning must not delay a pass.
            Vector2 direction = controller.AimDirection;
            float followThrough = Mathf.Max(0f, Vector2.Dot(controller.Body.linearVelocity, direction)) * forwardMomentum;
            pendingVelocity = direction * (Mathf.Lerp(range.x, range.y, Charge) + followThrough);
            pendingCurve = type == KickType.Shot ? shotCurve : 0f;
            shotCurve = 0f;
            charging = KickType.None;
            pendingType = type;
            pending = true;
        }

        public void Cancel()
        {
            charging = KickType.None;
            pending = false;
            shotCurve = 0f;
            pendingCurve = 0f;
        }

        private bool CanKick() => controller.IsActive && ball != null && ball.Body.simulated
            && possession != null && possession.IsOwner(controller);

        private void FixedUpdate()
        {
            if (!CanKick()) { Cancel(); return; }
            if (!pending) return;
            pending = false;
            KickType type = pendingType;
            Vector2 velocity = pendingVelocity;
            float curve = pendingCurve;
            controller.Dribbler.ReleaseForKick();
            ball.Body.linearVelocity = velocity;
            ball.Body.angularVelocity = 0f;
            ball.SetCurve(type == KickType.Shot ? curve : 0f);
            Kicked?.Invoke(type);
        }

        private void OnOwnerChanged(PlayerController owner)
        {
            if (owner != controller) Cancel();
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (possession != null) possession.OwnerChanged -= OnOwnerChanged;
        }
    }
}
