using System.Collections.Generic;
using PointPass.Core;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Static stadium dressing and markings, plus one batched animated crowd.</summary>
    public class StadiumBuilder : MonoBehaviour
    {
        public const float HorizontalViewPadding = 1.4f;
        public const float VerticalViewPadding = 1.5f;

        private readonly List<Mesh> meshes = new List<Mesh>();
        private Material material;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public static void Build(Transform parent)
        {
            GameObject root = new GameObject("StadiumPresentation");
            root.transform.SetParent(parent, false);
            StadiumBuilder builder = root.AddComponent<StadiumBuilder>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            builder.material = new Material(shader) { name = "Stadium Static Colour" };
            builder.BuildSurrounds();
            builder.Commit("StadiumSurrounds", -25);
            builder.BuildMarkings();
            builder.Commit("PitchMarkings", -8);
            root.AddComponent<StadiumCrowd>().Build(builder.material);
            Transform fixtures = new GameObject("StadiumFixtures").transform;
            fixtures.SetParent(root.transform, false);
            fixtures.gameObject.AddComponent<StadiumFixtures>().Build(builder.material);
            Transform referee = new GameObject("SidelineReferee").transform;
            referee.SetParent(root.transform, false);
            referee.localPosition = new Vector2(0.65f, -6.03f);
            referee.gameObject.AddComponent<SidelineReferee>().Build(builder.material);
            Transform confetti = new GameObject("GoalConfetti").transform;
            confetti.SetParent(root.transform, false);
            confetti.gameObject.AddComponent<GoalConfetti>().Build(builder.material);
            Transform photographers=new GameObject("StadiumPhotographers").transform;
            photographers.SetParent(root.transform,false);
            photographers.gameObject.AddComponent<StadiumPhotographers>().Build(builder.material);
        }

        private void BuildSurrounds()
        {
            Color concourse = MatchPalette.Ink;
            Color runoff = new Color(0.105f, 0.22f, 0.17f);
            Color deck = MatchPalette.Surface;
            Color aisle = new Color(0.2f, 0.24f, 0.25f);
            Quad(Vector2.zero, new Vector2(28, 18), concourse);
            Quad(Vector2.zero, new Vector2(23.1f, 13.05f), runoff);
            Quad(new Vector2(0, 6.31f), new Vector2(25.6f, 0.22f), aisle);
            Quad(new Vector2(0, -6.31f), new Vector2(25.6f, 0.22f), aisle);
            Quad(new Vector2(-11.21f, 0), new Vector2(0.22f, 12.8f), aisle);
            Quad(new Vector2(11.21f, 0), new Vector2(0.22f, 12.8f), aisle);

            // A low perimeter rail makes the playable grass read as a stadium enclosure.
            Color rail = new Color(0.68f, 0.75f, 0.7f);
            foreach (float sign in new[] { -1f, 1f })
            {
                Quad(new Vector2(0, sign * 6.08f), new Vector2(20.5f, 0.09f), rail);
                Quad(new Vector2(sign * 11.04f, 0), new Vector2(0.08f, 12.2f), rail);
                Quad(new Vector2(0, sign * 7.04f), new Vector2(26, 1.22f), deck);
                Quad(new Vector2(sign * 11.93f, 0), new Vector2(1.22f, 12.45f), deck);
            }

            // Seats stay static; the crowd above them uses one separate animated mesh.
            for (int row = 0; row < 3; row++)
            {
                float y = 6.6f + row * 0.28f;
                float x = 11.56f + row * 0.28f;
                for (int seat = 0; seat < 96; seat++)
                {
                    float along = -12.35f + seat * 0.26f;
                    if (seat % 24 < 2) continue;
                    Seat(new Vector2(along, y), seat, row, false);
                    Seat(new Vector2(along, -y), seat + 17, row, false);
                }
                for (int seat = 0; seat < 46; seat++)
                {
                    float along = -5.85f + seat * 0.26f;
                    if (seat % 16 < 2) continue;
                    Seat(new Vector2(x, along), seat + 31, row, true);
                    Seat(new Vector2(-x, along), seat + 47, row, true);
                }
            }

            foreach (float sign in new[] { -1f, 1f })
            {
                for (int section = 0; section < 5; section++)
                {
                    float x = -10.6f + section * 5.2f;
                    Quad(new Vector2(x, sign * 7.01f), new Vector2(0.25f, 1.18f), new Color(0.14f, 0.18f, 0.2f));
                    for (int step = 0; step < 5; step++)
                        Quad(new Vector2(x, sign * (6.52f + step * 0.23f)), new Vector2(0.25f, 0.025f), new Color(0.32f, 0.36f, 0.37f));
                }
                // Short segmented boards avoid fake brands or text while adding pitch-side structure.
                for (int panel = 0; panel < 12; panel++)
                {
                    float x = -9.15f + panel * 1.66f;
                    Color board = panel % 3 == 0 ? new Color(0.27f, 0.48f, 0.41f) : new Color(0.13f, 0.2f, 0.23f);
                    Quad(new Vector2(x, sign * 6.18f), new Vector2(1.56f, 0.13f), board);
                }
            }

        }

        private void Seat(Vector2 position, int index, int row, bool side)
        {
            Color seat = Color.Lerp(new Color(0.16f, 0.26f, 0.34f), new Color(0.25f, 0.37f, 0.44f), row * 0.35f);
            Quad(position, side ? new Vector2(0.19f, 0.21f) : new Vector2(0.21f, 0.19f), seat);
        }

        private void BuildMarkings()
        {
            float fw = MatchConfig.FieldHalfWidth;
            float fh = MatchConfig.FieldHalfHeight;
            Color paint = MatchPalette.Paper;
            paint.a = 0.92f;
            const float width = 0.038f;
            RectangleOutline(Vector2.zero, new Vector2(fw * 2, fh * 2), width, paint);
            Line(new Vector2(0, -fh), new Vector2(0, fh), width, paint);
            Arc(Vector2.zero, 1.45f, 0, 360, width, paint, 96);
            Disc(Vector2.zero, 0.055f, paint);
            foreach (float sign in new[] { -1f, 1f })
            {
                float line = fw * sign;
                float box = (fw - 3.05f) * sign;
                float smallBox = (fw - 1.12f) * sign;
                Line(new Vector2(line, -2.7f), new Vector2(box, -2.7f), width, paint);
                Line(new Vector2(box, -2.7f), new Vector2(box, 2.7f), width, paint);
                Line(new Vector2(box, 2.7f), new Vector2(line, 2.7f), width, paint);
                Line(new Vector2(line, -1.85f), new Vector2(smallBox, -1.85f), width, paint);
                Line(new Vector2(smallBox, -1.85f), new Vector2(smallBox, 1.85f), width, paint);
                Line(new Vector2(smallBox, 1.85f), new Vector2(line, 1.85f), width, paint);
                Vector2 penaltySpot = new Vector2((fw - 2.22f) * sign, 0);
                Disc(penaltySpot, 0.05f, paint);
                Arc(penaltySpot, 1.18f, sign > 0 ? 135 : -45, sign > 0 ? 225 : 45, width, paint, 24);
            }
            Arc(new Vector2(-fw, -fh), 0.3f, 0, 90, width, paint, 12);
            Arc(new Vector2(fw, -fh), 0.3f, 90, 180, width, paint, 12);
            Arc(new Vector2(fw, fh), 0.3f, 180, 270, width, paint, 12);
            Arc(new Vector2(-fw, fh), 0.3f, 270, 360, width, paint, 12);
        }

        private void RectangleOutline(Vector2 center, Vector2 size, float width, Color color)
        {
            Vector2 half = size * 0.5f;
            Line(center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y), width, color);
            Line(center + new Vector2(half.x, -half.y), center + new Vector2(half.x, half.y), width, color);
            Line(center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y), width, color);
            Line(center + new Vector2(-half.x, half.y), center + new Vector2(-half.x, -half.y), width, color);
        }

        private void Arc(Vector2 center, float radius, float from, float to, float width, Color color, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(from, to, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }

        private void Disc(Vector2 center, float radius, Color color, int segments = 16)
        {
            int start = vertices.Count;
            Vertex(center, color);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vertex(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
            }
            for (int i = 0; i < segments; i++) { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i + 2); }
        }

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
            Quad(from - side, to - side, to + side, from + side, color);
        }

        private void Quad(Vector2 center, Vector2 size, Color color)
        {
            Vector2 half = size * 0.5f;
            Quad(center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y),
                center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y), color);
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vertices.Count;
            Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private void Vertex(Vector2 point, Color color)
        {
            vertices.Add(point); colors.Add(color); uv.Add(Vector2.one * 0.5f);
        }

        private void Commit(string name, int sortingOrder)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            GameObject child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) Release(mesh);
            Release(material);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
