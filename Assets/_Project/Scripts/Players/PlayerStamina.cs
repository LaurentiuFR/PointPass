using UnityEngine;

namespace PointPass.Players
{
    /// <summary>Fixed-step sprint budget. Only the authority advances it; clients receive snapshots.</summary>
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private float sprintSeconds = 5f;
        [SerializeField] private float refillSeconds = 2.8f;
        [SerializeField] private float recoveryDelay = .65f;
        private float remaining = 1f, recoveryWait;
        private bool exhausted;
        public float Remaining => remaining;
        public bool Exhausted => exhausted;
        public bool IsSprinting { get; private set; }

        public void Step(bool requested, bool moving, float delta)
        {
            delta = Mathf.Max(0, delta);
            // Releasing sprint and recovering a useful reserve prevents exhausted auto-sprint pulses.
            if (exhausted && !requested && remaining >= .25f) exhausted = false;
            IsSprinting = requested && moving && !exhausted && remaining > 0;
            if (IsSprinting)
            {
                remaining = Mathf.Max(0, remaining - delta / Mathf.Max(.1f, sprintSeconds));
                recoveryWait = recoveryDelay;
                if (remaining <= .00001f) { remaining = 0; exhausted = true; IsSprinting = false; }
            }
            else
            {
                float rechargeTime = Mathf.Max(0, delta - recoveryWait);
                recoveryWait = Mathf.Max(0, recoveryWait - delta);
                remaining = Mathf.Min(1, remaining + rechargeTime / Mathf.Max(.1f, refillSeconds));
            }
        }
        public void ResetBudget() { remaining = 1; exhausted = false; recoveryWait = 0; IsSprinting = false; }
        public void SetReplicated(float value, bool unavailable, bool sprinting)
        {
            remaining = Mathf.Clamp01(value); exhausted = unavailable; IsSprinting = sprinting;
        }
    }
}
