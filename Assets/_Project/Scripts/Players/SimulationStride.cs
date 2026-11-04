using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// Supplies the existing foot cadence to physics when no rendered animator exists.
    /// Its constants and update order match PlayerAnimator's current locomotion cadence.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SimulationStride : MonoBehaviour
    {
        private Rigidbody2D body;
        public float Phase { get; private set; }
        public float Intensity { get; private set; }

        private void Awake() => body = GetComponent<Rigidbody2D>();

        private void Update()
        {
            if (!body.simulated)
            {
                Phase = 0f;
                Intensity = 0f;
                return;
            }
            float speed = body.linearVelocity.magnitude;
            Intensity = Mathf.MoveTowards(Intensity, Mathf.Clamp01(speed / 4.8f), 10f * Time.deltaTime);
            Phase = Mathf.Repeat(Phase + speed * 2.6f * Time.deltaTime, Mathf.PI * 2f);
        }
    }
}
