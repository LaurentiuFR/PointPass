using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>A crisp, code-drawn pitch-level stadium scene rather than a promotional card.</summary>
    public class StadiumMenuGraphic : UnityEngine.UI.Image
    {
        protected override void OnEnable()
        {
            base.OnEnable(); sprite = SpriteLibrary.WhiteSquare(); color = Color.white;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            // All points use the 650 x 740 design space and scale with the menu canvas.
            Color sky = new Color(0.08f, 0.17f, 0.29f);
            Ellipse(mesh, new Vector2(25, 25), new Vector2(325, 315), sky, new Color(sky.r, sky.g, sky.b, 0));
            Quad(mesh, new Vector2(-265, 80), new Vector2(290, 80), new Vector2(255, 211), new Vector2(-220, 211),
                new Color(0.075f, 0.14f, 0.23f), new Color(0.12f, 0.23f, 0.34f));
            for (int row = 0; row < 9; row++)
            {
                float y = 91 + row * 12, inset = row * 4;
                Line(mesh, new Vector2(-263 + inset, y), new Vector2(288 - inset, y), 1,
                    new Color(0.49f, 0.827f, 0.988f, 0.12f));
                for (int seat = 0; seat < 40 - row; seat++)
                {
                    float x = -254 + inset + seat * (530 - inset * 2) / (39 - row);
                    Color supporter = (seat + row * 3) % 5 == 0 ? MatchPalette.Red : MatchPalette.Accent;
                    supporter.a = 0.2f + 0.13f * Mathf.PerlinNoise(seat * 1.9f, row * 2.3f);
                    Line(mesh, new Vector2(x, y + 3), new Vector2(x, y + 6), 2, supporter);
                }
            }
            Line(mesh, new Vector2(-275, 218), new Vector2(270, 218), 6, new Color(0.18f, 0.3f, 0.42f));
            Line(mesh, new Vector2(-275, 224), new Vector2(270, 224), 1.5f, new Color(0.49f, 0.827f, 0.988f, 0.55f));
            Quad(mesh, new Vector2(-320, -294), new Vector2(322, -294), new Vector2(270, 74), new Vector2(-244, 74),
                new Color(0.07f, 0.16f, 0.18f), new Color(0.15f, 0.27f, 0.26f));
            for (int i = 0; i < 9; i++)
            {
                float t = i / 9f, next = (i + 1) / 9f;
                if (i % 2 == 0) Quad(mesh, Vector2.Lerp(new Vector2(-320, -294), new Vector2(-244, 74), t),
                    Vector2.Lerp(new Vector2(322, -294), new Vector2(270, 74), t),
                    Vector2.Lerp(new Vector2(322, -294), new Vector2(270, 74), next),
                    Vector2.Lerp(new Vector2(-320, -294), new Vector2(-244, 74), next),
                    new Color(0.27f, 0.4f, 0.35f, 0.07f), new Color(0.27f, 0.4f, 0.35f, 0.07f));
            }
            Color chalk = new Color(0.8f, 0.9f, 0.9f, 0.65f);
            Line(mesh, new Vector2(-290, -264), new Vector2(300, -264), 2, chalk);
            Line(mesh, new Vector2(-218, 48), new Vector2(247, 48), 1.5f, chalk);
            Line(mesh, new Vector2(-290, -264), new Vector2(-218, 48), 2, chalk);
            Line(mesh, new Vector2(300, -264), new Vector2(247, 48), 2, chalk);
            // Goal seen from pitch level, with actual layered mesh and upright posts.
            Color net = new Color(0.61f, 0.75f, 0.83f, 0.27f);
            for (int x = 0; x <= 10; x++)
                Line(mesh, new Vector2(-62 + x * 15, 73), new Vector2(-62 + x * 15, 131), 0.7f, net);
            for (int y = 0; y < 6; y++) Line(mesh, new Vector2(-62, 74 + y * 11), new Vector2(88, 74 + y * 11), 0.7f, net);
            Line(mesh, new Vector2(-66, 70), new Vector2(-66, 135), 3, chalk);
            Line(mesh, new Vector2(-66, 135), new Vector2(92, 135), 3, chalk);
            Line(mesh, new Vector2(92, 135), new Vector2(92, 70), 3, chalk);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 lamp = new Vector2(side * 245, 261);
                Line(mesh, new Vector2(side * 245, 85), lamp, 5, new Color(0.19f, 0.29f, 0.4f));
                Quad(mesh, new Vector2(lamp.x - 31, lamp.y - 15), new Vector2(lamp.x + 31, lamp.y - 15),
                    new Vector2(lamp.x + 31, lamp.y + 15), new Vector2(lamp.x - 31, lamp.y + 15),
                    new Color(0.25f, 0.38f, 0.47f), new Color(0.25f, 0.38f, 0.47f));
                for (int l = 0; l < 6; l++)
                    Ellipse(mesh, lamp + new Vector2((l % 3 - 1) * 19, l < 3 ? -7 : 7), new Vector2(6, 4),
                        new Color(0.86f, 0.95f, 1f), new Color(0.49f, 0.827f, 0.988f, 0.1f));
                Ellipse(mesh, lamp, new Vector2(82, 55), new Color(0.49f, 0.827f, 0.988f, 0.09f), Color.clear);
                Ellipse(mesh, new Vector2(side * 155, -95), new Vector2(150, 158), new Color(0.73f, 0.88f, 0.94f, 0.08f), Color.clear);
            }
            Ellipse(mesh, new Vector2(128, -286), new Vector2(104, 22), new Color(0, 0.02f, 0.05f, 0.6f), Color.clear);
        }

        private void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color bottom, Color top)
        {
            int n = mesh.currentVertCount;
            Vertex(mesh, a, bottom); Vertex(mesh, b, bottom); Vertex(mesh, c, top); Vertex(mesh, d, top);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
        private void Vertex(UnityEngine.UI.VertexHelper mesh, Vector2 point, Color shade)
        {
            Vector2 size = rectTransform.rect.size;
            mesh.AddVert(new Vector3(point.x * size.x / 650f, point.y * size.y / 740f), shade, Vector2.zero);
        }
        private void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color shade)
        {
            Vector2 normal = Vector2.Perpendicular((b - a).normalized) * width * 0.5f;
            Color transparent = new Color(shade.r, shade.g, shade.b, 0);
            Vector2 feather = normal.normalized * 0.65f;
            Quad(mesh, a - normal, b - normal, b + normal, a + normal, shade, shade);
            Quad(mesh, a + normal, b + normal, b + normal + feather, a + normal + feather, shade, transparent);
            Quad(mesh, a - normal - feather, b - normal - feather, b - normal, a - normal, transparent, shade);
        }
        private void Ellipse(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 radius, Color middle, Color edge)
        {
            int n = mesh.currentVertCount; Vertex(mesh, center, middle);
            for (int i = 0; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                Vertex(mesh, center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), edge);
                if (i > 0) mesh.AddTriangle(n, n + i, n + i + 1);
            }
        }
    }
}
