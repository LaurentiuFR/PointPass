using UnityEngine;

namespace PointPass.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Ball : MonoBehaviour
    {
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float spinPerSpeed = 140f; // degrees per second, per unit of speed
        [SerializeField] private float curveDegreesPerSecond = 72f;
        [SerializeField] private float curveDuration = 1.1f;

        private Transform visual;
        private TrailRenderer speedTrail;
        private Material trailMaterial;
        private float rollDirection = 1f;
        private float curveStrength;
        private float curveRemaining;
        private bool settlingInNet;
        private float fieldDamping;

        public Rigidbody2D Body { get; private set; }
        public Collider2D Collider { get; private set; }
        public float Curve => curveRemaining > 0f ? curveStrength * curveRemaining / Mathf.Max(0.01f, curveDuration) : 0f;

        /// <summary>Predicted heading change in degrees for a new shot, before collisions or drag.</summary>
        public float GetCurveAngle(float signedCurve, float seconds)
        {
            float duration = Mathf.Max(0.01f, curveDuration);
            float elapsed = Mathf.Clamp(seconds, 0f, duration);
            return curveDegreesPerSecond * Mathf.Clamp(signedCurve, -1f, 1f)
                * (elapsed - elapsed * elapsed / (2f * duration));
        }

        /// <summary>Signed bend: positive curves left relative to travel, negative right.</summary>
        public void SetCurve(float signedCurve)
        {
            curveStrength = float.IsNaN(signedCurve) || float.IsInfinity(signedCurve)
                ? 0f : Mathf.Clamp(signedCurve, -1f, 1f);
            curveRemaining = Mathf.Abs(curveStrength) > 0.001f ? Mathf.Max(0.01f, curveDuration) : 0f;
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<Collider2D>();
        }

        public void SetVisual(Transform ballSprite)
        {
            visual = ballSprite;
            if (speedTrail != null || visual == null) return;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            trailMaterial = new Material(shader);
            GameObject trailObject = new GameObject("SpeedTrail");
            trailObject.transform.SetParent(transform, false);
            speedTrail = trailObject.AddComponent<TrailRenderer>();
            speedTrail.sharedMaterial = trailMaterial;
            speedTrail.time = 0.075f;
            speedTrail.minVertexDistance = 0.04f;
            speedTrail.startWidth = 0.14f;
            speedTrail.endWidth = 0.025f;
            speedTrail.startColor = new Color(0.93f, 0.97f, 1f, 0.2f);
            speedTrail.endColor = new Color(0.93f, 0.97f, 1f, 0f);
            speedTrail.sortingOrder = 3;
            speedTrail.numCapVertices = 3;
            speedTrail.emitting = false;
        }

        private void Update()
        {
            if (visual != null && Body.simulated)
            {
                Vector2 velocity = Body.linearVelocity;
                float projectedRoll = velocity.x + velocity.y * 0.35f;
                if (Mathf.Abs(projectedRoll) > 0.05f) rollDirection = Mathf.Sign(projectedRoll);
                visual.Rotate(0f, 0f, -velocity.magnitude * rollDirection * spinPerSpeed * Time.deltaTime);
            }
            if (speedTrail != null) speedTrail.emitting = Body.simulated && Body.linearVelocity.sqrMagnitude > 90.25f;
        }

        private void FixedUpdate()
        {
            if (!Body.simulated) return;
            if (Body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                Body.linearVelocity = Body.linearVelocity.normalized * maxSpeed;
            }
            if (curveRemaining <= 0f) return;
            if (Body.linearVelocity.sqrMagnitude < 0.09f) { SetCurve(0f); return; }

            // Rotate velocity gradually, preserving its magnitude. Integrating the linear fade
            // over the timestep makes bending consistent at different physics rates.
            float step = Mathf.Min(Time.fixedDeltaTime, curveRemaining);
            float nextRemaining = curveRemaining - step;
            float averageFade = (curveRemaining + nextRemaining) * 0.5f / Mathf.Max(0.01f, curveDuration);
            float angle = curveDegreesPerSecond * curveStrength * averageFade * step * Mathf.Deg2Rad;
            Vector2 velocity = Body.linearVelocity;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            Body.linearVelocity = new Vector2(velocity.x * cosine - velocity.y * sine,
                velocity.x * sine + velocity.y * cosine);
            curveRemaining = nextRemaining;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            SetCurve(0f);
            if (collision == null || collision.collider == null) return;
            GoalNet net = collision.collider.GetComponentInParent<GoalNet>();
            if (net != null) net.Absorb(this, collision);
        }

        public void ResetTo(Vector2 position)
        {
            if (settlingInNet) Body.linearDamping = fieldDamping;
            settlingInNet = false;
            SetCurve(0f);
            if (speedTrail != null) speedTrail.emitting = false;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            transform.position = position;
            if (visual != null) visual.localRotation = Quaternion.identity;
            // Moving a TrailRenderer can create a final endpoint even after emission stops.
            // Clear after the teleport so no old-to-new streak survives a kickoff reset.
            ClearTrail();
        }

        public void SettleInNet()
        {
            if (!settlingInNet) fieldDamping = Body.linearDamping;
            settlingInNet = true;
            Body.linearDamping = 5f;
        }

        private void ClearTrail()
        {
            if (speedTrail == null) return;
            speedTrail.emitting = false;
            speedTrail.Clear();
        }

        private void OnDisable()
        {
            SetCurve(0f);
            ClearTrail();
        }

        private void OnDestroy()
        {
            if (trailMaterial == null) return;
            if (Application.isPlaying) Destroy(trailMaterial);
            else DestroyImmediate(trailMaterial);
        }
    }
}
