using System.Collections.Generic;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Individual silhouettes, varied clothes/skin/hair, and staggered goal reactions in one draw.</summary>
    public class StadiumCrowd : MonoBehaviour
    {
        private struct Fan
        {
            public int start, head, left, right, end;
            public Vector2 position;
            public float phase, enthusiasm, delay;
            public Team team;
        }

        private readonly List<Fan> fans = new List<Fan>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private Vector3[] rest, posed;
        private Mesh mesh;
        private float elapsed, celebrationAge = 10f, tick;
        private Team scoringTeam;
        public bool IsCelebrating => celebrationAge < 3.2f;
        public int SpectatorCount => fans.Count;
        public static Team SupporterTeamAt(Vector2 position) => position.x < 0 ? Team.Blue : Team.Red;

        public void Build(Material material)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int seat = 0; seat < 96; seat++)
                {
                    if (seat % 24 < 2) continue;
                    AddFan(new Vector2(-12.35f + seat * 0.26f, 6.6f + row * 0.28f), seat, row);
                    AddFan(new Vector2(-12.35f + seat * 0.26f, -6.6f - row * 0.28f), seat + 117, row);
                }
                for (int seat = 0; seat < 46; seat++)
                {
                    if (seat % 16 < 2) continue;
                    AddFan(new Vector2(11.56f + row * 0.28f, -5.85f + seat * 0.26f), seat + 231, row);
                    AddFan(new Vector2(-11.56f - row * 0.28f, -5.85f + seat * 0.26f), seat + 347, row);
                }
            }
            mesh = new Mesh { name = "Stadium spectators" };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            rest = vertices.ToArray(); posed = vertices.ToArray();
            GameObject child = new GameObject("Spectators", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().sharedMaterial = material;
            child.GetComponent<MeshRenderer>().sortingOrder = -24;
        }

        private static float Hash(int index, float salt) => Mathf.Repeat(Mathf.Sin(index * 17.13f + salt) * 4375.545f, 1f);

        private void AddFan(Vector2 seat, int index, int row)
        {
            int id = index + row * 503;
            if (Hash(id, 1) < 0.2f) return;
            Vector2 position = seat + new Vector2((Hash(id, 2) - 0.5f) * 0.035f, 0);
            Team team = SupporterTeamAt(position);
            Color shirt = MatchPalette.TeamColor(team);
            if (Hash(id, 4) < 0.32f) shirt = Color.Lerp(new Color(0.24f, 0.29f, 0.3f), new Color(0.85f, 0.83f, 0.72f), Hash(id, 5));
            shirt *= Mathf.Lerp(0.8f, 1.15f, Hash(id, 6)); shirt.a = 1;
            Color skin = Color.Lerp(new Color(0.31f, 0.18f, 0.12f), new Color(0.94f, 0.73f, 0.54f), Hash(id, 7));
            Color hair = Color.Lerp(new Color(0.07f, 0.055f, 0.045f), new Color(0.53f, 0.36f, 0.16f), Hash(id, 8));
            Fan fan = new Fan { start = vertices.Count, position = position, team = team,
                phase = Hash(id, 9) * 6.28f, enthusiasm = Mathf.Lerp(0.65f, 1f, Hash(id, 10)), delay = Hash(id, 11) * 0.45f };
            Color trousers = new Color(0.1f, 0.14f, 0.2f);
            Line(position + new Vector2(-0.026f, -0.015f), position + new Vector2(-0.033f, -0.085f), 0.035f, trousers);
            Line(position + new Vector2(0.026f, -0.015f), position + new Vector2(0.033f, -0.085f), 0.035f, trousers);
            Ellipse(position, new Vector2(0.066f, 0.065f), shirt, 10);
            fan.head = vertices.Count;
            Ellipse(position + new Vector2(0, 0.073f), new Vector2(0.038f, 0.044f), skin, 12);
            Ellipse(position + new Vector2(0, 0.098f), new Vector2(0.039f, 0.023f), hair, 10);
            fan.left = vertices.Count;
            Arm(position, -1, shirt, skin);
            fan.right = vertices.Count;
            Arm(position, 1, shirt, skin);
            fan.end = vertices.Count;
            fans.Add(fan);
        }

        private void Arm(Vector2 position, float side, Color shirt, Color skin)
        {
            Vector2 shoulder = position + new Vector2(side * 0.05f, 0.025f);
            Vector2 elbow = shoulder + new Vector2(side * 0.024f, -0.036f);
            Vector2 hand = elbow + new Vector2(side * 0.006f, -0.038f);
            Line(shoulder, elbow, 0.031f, shirt);
            Line(elbow, hand, 0.023f, skin);
            Ellipse(hand, Vector2.one * 0.014f, skin, 6);
        }

        public void Celebrate(Team team) { scoringTeam = team; celebrationAge = 0; }

        public void ResetCelebration()
        {
            celebrationAge = 10f;
            if (mesh != null) mesh.vertices = rest;
        }

        private void Update()
        {
            tick += Time.deltaTime;
            if (tick < 1f / 30f) return;
            Advance(tick); tick = 0;
        }

        public void Advance(float seconds)
        {
            if (mesh == null || seconds <= 0) return;
            elapsed += seconds; celebrationAge += seconds;
            foreach (Fan fan in fans)
            {
                float age = celebrationAge - fan.delay;
                float reaction = age > 0 ? Mathf.SmoothStep(0, 1, age / 0.18f)
                    * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.2f, 3.2f, age))) : 0;
                bool winning = fan.team == scoringTeam;
                float cheer = reaction * fan.enthusiasm;
                float bounce = winning ? Mathf.Abs(Mathf.Sin(age * 9f + fan.phase)) * cheer * 0.065f : -cheer * 0.012f;
                float idle = Mathf.Sin(elapsed * 1.8f + fan.phase) * 0.003f;
                // Mix overhead waving with clapping, and stagger reactions across sections.
                float armAngle = cheer * (winning ? (fan.phase < 2.1f
                    ? 87f + 20f * Mathf.Sin(age * 18f + fan.phase)
                    : 145f + 12f * Mathf.Sin(age * 13f + fan.phase))
                    : fan.phase < 2.1f ? -132f : fan.phase < 4.2f ? -8f : -38f) * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(armAngle), sine = Mathf.Sin(armAngle);
                for (int i = fan.start; i < fan.end; i++)
                {
                    Vector2 point = rest[i];
                    if (!winning && i >= fan.head && i < fan.left)
                        point += new Vector2(Mathf.Sin(age * 7f + fan.phase)*cheer*0.005f,-cheer*0.026f);
                    if (i >= fan.left)
                    {
                        float side = i < fan.right ? -1 : 1;
                        Vector2 shoulder = fan.position + new Vector2(side * 0.05f, 0.025f);
                        Vector2 offset = point - shoulder;
                        point = shoulder + new Vector2(offset.x * cosine - offset.y * sine * side,
                            offset.x * sine * side + offset.y * cosine);
                    }
                    posed[i] = point + new Vector2(idle, bounce + idle);
                }
            }
            mesh.vertices = posed;
            // Bounds include raised hands and jumps, without a per-frame bounds scan.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(28, 18, 1));
        }

        private void Ellipse(Vector2 center, Vector2 radius, Color color, int segments)
        {
            int start = vertices.Count;
            vertices.Add(center); colors.Add(MatchPalette.WorldColor(color));
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vertices.Add(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y)); colors.Add(MatchPalette.WorldColor(color));
            }
            for (int i = 0; i < segments; i++) { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i + 2); }
        }

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            int start = vertices.Count;
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * width * 0.5f;
            vertices.Add(from - normal); vertices.Add(to - normal); vertices.Add(to + normal); vertices.Add(from + normal);
            for (int i = 0; i < 4; i++) colors.Add(MatchPalette.WorldColor(color));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
