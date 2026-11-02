using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Stride, running lean and kick recovery for the top-down footballer. These offsets are
    /// visual only; the exposed stride phase also lets dribbling stay in time with his feet.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        public const float BootSpread = 0.15f;
        public const float BootRestY = 0.32f;

        [SerializeField] private float radiansPerUnit = 2.6f; // stride length: smaller = longer strides
        [SerializeField] private float referenceSpeed = 4.8f;
        [SerializeField] private float stride = 0.15f;
        [SerializeField] private float kickRecoverySeconds = 0.18f;

        private Rigidbody2D body;
        private Transform leftBoot;
        private Transform rightBoot;
        private Transform bodyVisual;
        private Transform leftArm;
        private Transform rightArm;
        private Vector3 leftBase;
        private Vector3 rightBase;
        private Vector3 bodyBase;
        private Vector3 leftRest;
        private Vector3 rightRest;
        private Vector3 bodyRest;
        private Quaternion leftRotation;
        private Quaternion rightRotation;
        private Quaternion bodyRotation;
        private Vector3 leftArmRest;
        private Vector3 rightArmRest;
        private Quaternion leftArmRotation;
        private Quaternion rightArmRotation;
        private Vector2 lean;
        private float kickRemaining;
        private float kickStrength;
        private bool kickLeft;

        /// <summary>0 to 2π. sin(Phase) &gt; 0 means the left foot is forward.</summary>
        public float Phase { get; private set; }

        /// <summary>0 when standing, 1 at full run.</summary>
        public float Intensity { get; private set; }

        public void Init(Transform leftBoot, Transform rightBoot, Transform bodyVisual,
            Transform leftArm = null, Transform rightArm = null)
        {
            this.leftBoot = leftBoot;
            this.rightBoot = rightBoot;
            this.bodyVisual = bodyVisual;
            this.leftArm = leftArm;
            this.rightArm = rightArm;
            if (leftBoot != null)
            {
                leftBase = leftBoot.localScale;
                leftRest = leftBoot.localPosition;
                leftRotation = leftBoot.localRotation;
            }
            if (rightBoot != null)
            {
                rightBase = rightBoot.localScale;
                rightRest = rightBoot.localPosition;
                rightRotation = rightBoot.localRotation;
            }
            if (bodyVisual != null)
            {
                bodyBase = bodyVisual.localScale;
                bodyRest = bodyVisual.localPosition;
                bodyRotation = bodyVisual.localRotation;
            }
            if (leftArm != null)
            {
                leftArmRest = leftArm.localPosition;
                leftArmRotation = leftArm.localRotation;
            }
            if (rightArm != null)
            {
                rightArmRest = rightArm.localPosition;
                rightArmRotation = rightArm.localRotation;
            }
        }

        /// <summary>Called after a physical kick; never moves the player or the ball.</summary>
        public void PlayKick(PlayerKicker.KickType type)
        {
            if (type == PlayerKicker.KickType.None || body == null || !body.simulated) return;
            kickLeft = Mathf.Sin(Phase) >= 0f;
            kickStrength = type == PlayerKicker.KickType.Shot ? 1f : 0.7f;
            kickRemaining = Mathf.Max(0.01f, kickRecoverySeconds);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (body == null) return;
            if (!body.simulated)
            {
                ResetPose();
                return;
            }

            float speed = body.linearVelocity.magnitude;
            Intensity = Mathf.MoveTowards(Intensity, Mathf.Clamp01(speed / Mathf.Max(0.01f, referenceSpeed)), 10f * Time.deltaTime);
            Phase = Mathf.Repeat(Phase + speed * radiansPerUnit * Time.deltaTime, Mathf.PI * 2f);

            float step = Mathf.Sin(Phase) * Intensity;
            float recovery = Mathf.Clamp01(kickRemaining / Mathf.Max(0.01f, kickRecoverySeconds));
            float kick = recovery * recovery * kickStrength;
            kickRemaining = Mathf.Max(0f, kickRemaining - Time.deltaTime);
            float leftStrike = kickLeft ? kick : -kick * 0.2f;
            float rightStrike = kickLeft ? -kick * 0.2f : kick;

            if (leftBoot != null)
            {
                leftBoot.localPosition = leftRest + Vector3.up * (stride * step + leftStrike * 0.22f);
                leftBoot.localScale = leftBase * (1f + 0.12f * step + Mathf.Max(0, leftStrike) * 0.08f);
                leftBoot.localRotation = leftRotation * Quaternion.Euler(0, 0, -4f * step - leftStrike * 9f);
            }
            if (rightBoot != null)
            {
                rightBoot.localPosition = rightRest + Vector3.up * (-stride * step + rightStrike * 0.22f);
                rightBoot.localScale = rightBase * (1f - 0.12f * step + Mathf.Max(0, rightStrike) * 0.08f);
                rightBoot.localRotation = rightRotation * Quaternion.Euler(0, 0, -4f * step + rightStrike * 9f);
            }

            Vector2 localVelocity = Quaternion.Inverse(Quaternion.Euler(0, 0, body.rotation)) * body.linearVelocity;
            lean = Vector2.Lerp(lean, Vector2.ClampMagnitude(localVelocity / 6.48f, 1f), 1f - Mathf.Exp(-14f * Time.deltaTime));
            // Arms counter-swing against the legs. A little more travel at sprint speed
            // makes the run readable without moving the player's physics or hitbox.
            float armStrength = Mathf.Lerp(1f, 1.25f, Mathf.Clamp01((speed - referenceSpeed) / 1.68f));
            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRest + new Vector3(lean.x * 0.012f,
                    -step * 0.065f * armStrength - leftStrike * 0.045f, 0);
                leftArm.localRotation = leftArmRotation * Quaternion.Euler(0, 0,
                    step * 19f * armStrength + lean.x * 5f - leftStrike * 12f);
            }
            if (rightArm != null)
            {
                rightArm.localPosition = rightArmRest + new Vector3(lean.x * 0.012f,
                    step * 0.065f * armStrength - rightStrike * 0.045f, 0);
                rightArm.localRotation = rightArmRotation * Quaternion.Euler(0, 0,
                    -step * 19f * armStrength + lean.x * 5f + rightStrike * 12f);
            }
            if (bodyVisual != null)
            {
                float bounce = 0.014f * Intensity * Mathf.Cos(2f * Phase);
                bodyVisual.localPosition = bodyRest + new Vector3(lean.x * 0.025f, lean.y * 0.035f + bounce - kick * 0.035f, 0);
                bodyVisual.localScale = bodyBase * (1f + 0.018f * Intensity * Mathf.Cos(2f * Phase));
                bodyVisual.localRotation = bodyRotation * Quaternion.Euler(0, 0, 3f * step - lean.x * 5f + (kickLeft ? -1f : 1f) * kick * 6f);
            }
        }

        private void ResetPose()
        {
            Phase = 0f;
            Intensity = 0f;
            kickRemaining = 0f;
            lean = Vector2.zero;
            if (leftBoot != null) { leftBoot.localPosition = leftRest; leftBoot.localScale = leftBase; leftBoot.localRotation = leftRotation; }
            if (rightBoot != null) { rightBoot.localPosition = rightRest; rightBoot.localScale = rightBase; rightBoot.localRotation = rightRotation; }
            if (bodyVisual != null) { bodyVisual.localPosition = bodyRest; bodyVisual.localScale = bodyBase; bodyVisual.localRotation = bodyRotation; }
            if (leftArm != null) { leftArm.localPosition = leftArmRest; leftArm.localRotation = leftArmRotation; }
            if (rightArm != null) { rightArm.localPosition = rightArmRest; rightArm.localRotation = rightArmRotation; }
        }

        private void OnDisable() => ResetPose();
    }
}
