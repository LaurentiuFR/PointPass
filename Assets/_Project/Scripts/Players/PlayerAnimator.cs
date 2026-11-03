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
        private bool replicatedMotion;
        private Vector2 replicatedVelocity;
        public void SetReplicatedMotion(Vector2 velocity) { replicatedMotion = true; replicatedVelocity = velocity; }
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
        private Transform leftElbow;
        private Transform rightElbow;
        private Vector2 lean;
        private float kickRemaining;
        private float kickStrength;
        private bool kickLeft;
        private float celebrationAge = 10;
        public bool IsCelebrating => celebrationAge < 2.6f;
        public void CelebrateGoal() { celebrationAge = 0; kickRemaining = 0; }
        public void ClearCelebration() { celebrationAge = 10; ResetPose(); }

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
            leftElbow = leftArm != null ? leftArm.Find("Elbow") : null;
            rightElbow = rightArm != null ? rightArm.Find("Elbow") : null;
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
            if (type == PlayerKicker.KickType.None || body == null || (!body.simulated && !replicatedMotion)) return;
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
            if (IsCelebrating)
            {
                AdvanceCelebration(Time.deltaTime);
                return;
            }
            if (!body.simulated && !replicatedMotion)
            {
                ResetPose();
                return;
            }

            Vector2 velocity = replicatedMotion ? replicatedVelocity : body.linearVelocity;
            float speed = velocity.magnitude;
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

            Vector2 localVelocity = Quaternion.Inverse(Quaternion.Euler(0, 0, body.rotation)) * velocity;
            lean = Vector2.Lerp(lean, Vector2.ClampMagnitude(localVelocity / 6.48f, 1f), 1f - Mathf.Exp(-14f * Time.deltaTime));
            // Project the shoulder swing along local forward/back, not out to the sides.
            // These children inherit facing, so the same gait works in every direction.
            float armStrength = Mathf.Lerp(1f, 1.25f, Mathf.Clamp01((speed - referenceSpeed) / 1.68f));
            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRest + new Vector3(lean.x * 0.012f,
                    -step * 0.018f * armStrength - leftStrike * 0.025f, 0);
                leftArm.localRotation = leftArmRotation * Quaternion.Euler((45f - 75f * Mathf.Sin(Phase)) * Intensity, 0,
                    step * 6f * armStrength + lean.x * 3f - leftStrike * 7f - kick * 5f);
            }
            if (rightArm != null)
            {
                rightArm.localPosition = rightArmRest + new Vector3(lean.x * 0.012f,
                    step * 0.018f * armStrength - rightStrike * 0.025f, 0);
                rightArm.localRotation = rightArmRotation * Quaternion.Euler((45f + 75f * Mathf.Sin(Phase)) * Intensity, 0,
                    -step * 6f * armStrength + lean.x * 3f + rightStrike * 7f + kick * 5f);
            }
            // Hands follow a bent elbow rather than rotating the whole arm like a paddle.
            // The forward arm folds more, and a shot opens both arms for balance.
            PoseElbow(leftElbow, -1, -step, armStrength, kick);
            PoseElbow(rightElbow, 1, step, armStrength, kick);
            SetHandLayer(leftElbow, -step);
            SetHandLayer(rightElbow, step);
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
            if (leftElbow != null) { leftElbow.localRotation = Quaternion.identity; leftElbow.localScale = Vector3.one; }
            if (rightElbow != null) { rightElbow.localRotation = Quaternion.identity; rightElbow.localScale = Vector3.one; }
            SetHandLayer(leftElbow,0); SetHandLayer(rightElbow,0);
        }

        private void PoseElbow(Transform elbow, float side, float swing, float strength, float kick)
        {
            if (elbow == null) return;
            float bend = (17f + 2f * strength + 16f * Mathf.Max(0,swing)) * Intensity + kick * 8f;
            elbow.localRotation = Quaternion.Euler(0, 0, -side * bend);
            elbow.localScale = new Vector3(1, 1f - 0.1f * Mathf.Max(0, swing), 1);
        }

        private static void SetHandLayer(Transform elbow,float swing)
        {
            if (elbow == null) return;
            SpriteRenderer hand = elbow.GetComponentInChildren<SpriteRenderer>();
            if (hand != null) hand.sortingOrder = swing > 0.12f ? 6 : 4;
        }

        public void AdvanceCelebration(float seconds)
        {
            if (!IsCelebrating || seconds<=0) return;
            celebrationAge += seconds;
            float blend = Mathf.SmoothStep(0,1,celebrationAge/0.16f)
                * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.15f,2.6f,celebrationAge)));
            float pump = Mathf.Max(0,Mathf.Sin(celebrationAge*8f))*blend;
            if (bodyVisual != null)
            {
                bodyVisual.localPosition = bodyRest + Vector3.up*(0.035f*pump);
                bodyVisual.localRotation = bodyRotation*Quaternion.Euler(0,0,-5f*blend+3f*pump);
            }
            if (leftArm != null)
            {
                leftArm.localPosition = leftArmRest + Vector3.up*(0.05f*blend);
                leftArm.localRotation = leftArmRotation*Quaternion.Euler(0,0,-110f*blend);
            }
            if (rightArm != null)
            {
                rightArm.localPosition = rightArmRest + Vector3.up*(0.08f*pump);
                rightArm.localRotation = rightArmRotation*Quaternion.Euler(0,0,(120f+25f*pump)*blend);
            }
            if (leftElbow!=null)
            {
                leftElbow.localRotation=Quaternion.Euler(0,0,-12f*blend); leftElbow.localScale=Vector3.one;
            }
            if (rightElbow!=null)
            {
                rightElbow.localRotation=Quaternion.Euler(0,0,12f*blend); rightElbow.localScale=Vector3.one;
            }
            SetHandLayer(leftElbow,1); SetHandLayer(rightElbow,1);
            if (!IsCelebrating) ResetPose();
        }

        private void OnDisable() => ClearCelebration();
    }
}
