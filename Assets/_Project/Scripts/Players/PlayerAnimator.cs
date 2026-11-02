using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Running animation for the top-down footballer: the boots alternate forward and back
    /// in time with his speed, and the body bobs and sways slightly. Also exposes the stride
    /// phase so the dribbling can touch the ball in time with his feet.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        public const float BootSpread = 0.125f;
        public const float BootRestY = 0.22f;

        [SerializeField] private float radiansPerUnit = 2.6f; // stride length: smaller = longer strides
        [SerializeField] private float referenceSpeed = 6f;
        [SerializeField] private float stride = 0.13f;

        private Rigidbody2D body;
        private Transform leftBoot;
        private Transform rightBoot;
        private Transform bodyVisual;
        private Vector3 leftBase;
        private Vector3 rightBase;
        private Vector3 bodyBase;

        /// <summary>0 to 2π. sin(Phase) &gt; 0 means the left foot is forward.</summary>
        public float Phase { get; private set; }

        /// <summary>0 when standing, 1 at full run.</summary>
        public float Intensity { get; private set; }

        public void Init(Transform leftBoot, Transform rightBoot, Transform bodyVisual)
        {
            this.leftBoot = leftBoot;
            this.rightBoot = rightBoot;
            this.bodyVisual = bodyVisual;
            leftBase = leftBoot.localScale;
            rightBase = rightBoot.localScale;
            bodyBase = bodyVisual.localScale;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (leftBoot == null) return;

            float speed = body.simulated ? body.linearVelocity.magnitude : 0f;
            Intensity = Mathf.MoveTowards(Intensity, Mathf.Clamp01(speed / referenceSpeed), 8f * Time.deltaTime);
            Phase = Mathf.Repeat(Phase + speed * radiansPerUnit * Time.deltaTime, Mathf.PI * 2f);

            float step = Mathf.Sin(Phase) * Intensity;

            leftBoot.localPosition = new Vector3(-BootSpread, BootRestY + stride * step, 0f);
            rightBoot.localPosition = new Vector3(BootSpread, BootRestY - stride * step, 0f);
            leftBoot.localScale = leftBase * (1f + 0.15f * step);
            rightBoot.localScale = rightBase * (1f - 0.15f * step);

            bodyVisual.localScale = bodyBase * (1f + 0.03f * Intensity * Mathf.Cos(2f * Phase));
            bodyVisual.localRotation = Quaternion.Euler(0f, 0f, 4f * step);
        }
    }
}
