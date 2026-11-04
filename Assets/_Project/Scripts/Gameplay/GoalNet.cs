using System.Collections.Generic;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>A projected overhead net with anchored ropes and local, damped impact waves.</summary>
    public class GoalNet : MonoBehaviour
    {
        private struct ImpactWave
        {
            public Vector2 point;
            public Vector2 direction;
            public float strength;
            public float age;
        }

        private readonly List<ImpactWave> waves = new List<ImpactWave>(4);
        private readonly List<Vector3> rest = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private Vector3[] animated;
        private Mesh mesh;
        private Material material;
        private float sign;
        public const int NetSortingOrder = 12;
        public float ImpactStrength { get; private set; }
        public float Side => sign;
        public event System.Action<Vector2, float> Impacted;

        public static GoalNet Create(Transform parent, float side, bool presentation = true)
        {
            GameObject root = new GameObject(side < 0 ? "GoalLeft" : "GoalRight");
            root.transform.SetParent(parent, false);
            GoalNet net = root.AddComponent<GoalNet>();
            net.sign = Mathf.Sign(side);
            if (presentation) net.Build();
            else net.enabled = false; // Collision absorption remains callable without presentation updates.
            return net;
        }

        private void Build()
        {
            material = new Material(Shader.Find("Sprites/Default")) { name = "Goal ropes" };
            float front = MatchConfig.FieldHalfWidth * sign;
            float back = (MatchConfig.FieldHalfWidth + MatchConfig.GoalDepth) * sign;
            float half = MatchConfig.GoalWidth * 0.5f;
            SpriteLibrary.Create(transform, "GoalFloor", "shadow",
                new Vector2(MatchConfig.GoalDepth, MatchConfig.GoalWidth),
                new Vector2((front + back) * 0.5f, 0), -6, new Color(0, 0, 0, 0.15f), optional: true);

            // A fine translucent grid covers the recess, never the playable side of the line.
            Color rope = new Color(0.87f, 0.94f, 0.95f, 0.42f);
            for (int x = 0; x <= 8; x++)
            {
                float across = Mathf.Lerp(front, back, x / 8f);
                Rope(new Vector2(across, -half), new Vector2(across, half), 0.011f, rope, 30);
            }
            for (int y = 0; y <= 24; y++)
            {
                float along = Mathf.Lerp(-half, half, y / 24f);
                Rope(new Vector2(front, along), new Vector2(back, along), 0.011f, rope, 10);
            }
            Rope(new Vector2(back, -half), new Vector2(back, half), 0.026f, new Color(0.8f, 0.87f, 0.89f, 0.8f), 30);
            Rope(new Vector2(front, -half), new Vector2(back, -half), 0.026f, rope, 10);
            Rope(new Vector2(front, half), new Vector2(back, half), 0.026f, rope, 10);
            mesh = new Mesh { name = "Flexible goal net" };
            mesh.MarkDynamic();
            mesh.SetVertices(rest);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            animated = rest.ToArray();
            GameObject roof = new GameObject("OverheadNet", typeof(MeshFilter), typeof(MeshRenderer));
            roof.transform.SetParent(transform, false);
            roof.GetComponent<MeshFilter>().sharedMesh = mesh;
            roof.GetComponent<MeshRenderer>().sharedMaterial = material;
            roof.GetComponent<MeshRenderer>().sortingOrder = NetSortingOrder;

            // The front crossbar and posts stay rigid while the back and side ropes flex.
            rest.Clear(); colors.Clear(); indices.Clear();
            Color frame = new Color(0.94f, 0.97f, 0.98f);
            Rope(new Vector2(front, -half - 0.025f), new Vector2(front, half + 0.025f), 0.07f, frame, 1);
            Mesh frameMesh = new Mesh { name = "Goal frame" };
            frameMesh.SetVertices(rest); frameMesh.SetColors(colors); frameMesh.SetTriangles(indices, 0);
            GameObject frameObject = new GameObject("Crossbar", typeof(MeshFilter), typeof(MeshRenderer));
            frameObject.transform.SetParent(transform, false);
            frameObject.GetComponent<MeshFilter>().sharedMesh = frameMesh;
            frameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            frameObject.GetComponent<MeshRenderer>().sortingOrder = NetSortingOrder + 1;
            rest.Clear(); rest.AddRange(animated);
        }

        private void Rope(Vector2 from, Vector2 to, float width, Color color, int segments)
        {
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, (float)i / segments);
                Vector2 b = Vector2.Lerp(from, to, (float)(i + 1) / segments);
                int start = rest.Count;
                rest.Add(a - normal); rest.Add(b - normal); rest.Add(b + normal); rest.Add(a + normal);
                for (int v = 0; v < 4; v++) colors.Add(color);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }
        }

        public void Absorb(Ball ball, Collision2D collision)
        {
            if (collision.contactCount == 0) return;
            ContactPoint2D contact = collision.GetContact(0);
            Vector2 point = transform.InverseTransformPoint(contact.point);
            if (Mathf.Abs(point.y) > MatchConfig.GoalWidth * 0.5f + 0.08f
                || point.x * sign < MatchConfig.FieldHalfWidth + 0.05f) return;
            Vector2 velocity = collision.relativeVelocity;
            float normalSpeed = Mathf.Abs(Vector2.Dot(velocity, contact.normal));
            Impact(point, normalSpeed);
            // A net absorbs energy; it must not send the ball back like the perimeter wall.
            Vector2 tangent = velocity - Vector2.Dot(velocity, contact.normal) * contact.normal;
            ball.Body.linearVelocity = contact.normal * (normalSpeed * 0.1f) + tangent * 0.45f;
            ball.SettleInNet();
        }

        public void Impact(Vector2 localPoint, float speed)
        {
            if (speed < 0.15f) return;
            Impacted?.Invoke(localPoint, speed);
            if (mesh == null) return;
            Vector2 direction = Mathf.Abs(localPoint.y) > MatchConfig.GoalWidth * 0.5f - 0.1f
                ? Vector2.up * Mathf.Sign(localPoint.y) : Vector2.right * sign;
            ImpactStrength = Mathf.Clamp(speed * 0.024f, 0f, 0.38f);
            if (waves.Count == 4) waves.RemoveAt(0);
            waves.Add(new ImpactWave { point = localPoint, direction = direction, strength = ImpactStrength });
        }

        private void Update() => Advance(Time.deltaTime);

        // Also used by deterministic presentation previews and engine checks.
        public void Advance(float seconds)
        {
            if (mesh == null || waves.Count == 0 || seconds <= 0f) return;
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                ImpactWave wave = waves[i]; wave.age += seconds;
                if (wave.age > 1.8f) waves.RemoveAt(i);
                else waves[i] = wave;
            }
            for (int i = 0; i < animated.Length; i++)
            {
                Vector2 point = rest[i];
                Vector2 displacement = Vector2.zero;
                // Front edge attached to the rigid posts/crossbar.
                float anchor = Mathf.SmoothStep(0, 1, Mathf.Clamp01((point.x * sign - MatchConfig.FieldHalfWidth) / 0.25f));
                foreach (ImpactWave wave in waves)
                {
                    float distance = Vector2.Distance(point, wave.point);
                    float envelope = Mathf.Exp(-distance * distance * 1.6f) * Mathf.Exp(-wave.age * 3.8f);
                    float ripple = Mathf.Sin(wave.age * 19f - distance * 2f) * 0.2f;
                    float push = Mathf.Sin(Mathf.Min(wave.age * 16f, Mathf.PI)) * 0.8f;
                    displacement += wave.direction * (wave.strength * envelope * (push + ripple) * anchor);
                }
                animated[i] = point + Vector2.ClampMagnitude(displacement, 0.42f);
            }
            mesh.vertices = animated;
            mesh.RecalculateBounds();
        }

        public void ResetNet()
        {
            waves.Clear(); ImpactStrength = 0;
            if (mesh != null) { mesh.SetVertices(rest); mesh.RecalculateBounds(); }
        }

        private void OnDestroy()
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>()) Release(filter.sharedMesh);
            Release(material);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
