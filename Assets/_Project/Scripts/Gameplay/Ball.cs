using UnityEngine;

namespace PointPass.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Ball : MonoBehaviour
    {
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float spinPerSpeed = 140f; // degrees per second, per unit of speed

        private Transform visual;

        public Rigidbody2D Body { get; private set; }
        public Collider2D Collider { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<Collider2D>();
        }

        public void SetVisual(Transform ballSprite)
        {
            visual = ballSprite;
        }

        private void Update()
        {
            if (visual != null && Body.simulated)
            {
                visual.Rotate(0f, 0f, -Body.linearVelocity.magnitude * spinPerSpeed * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (Body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                Body.linearVelocity = Body.linearVelocity.normalized * maxSpeed;
            }
        }

        public void ResetTo(Vector2 position)
        {
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            transform.position = position;
        }
    }
}
