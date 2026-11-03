using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    public class LobbyPodGraphic : UnityEngine.UI.Image
    {
        private bool available = true;
        private Color accent = LobbyUi.Cyan;
        public Color Accent { get => accent; set { if (accent == value) return; accent = value; SetVerticesDirty(); } }
        public bool Captain;
        public bool Available { get => available; set { if (available == value) return; available = value; SetVerticesDirty(); } }
        protected override void OnEnable() { base.OnEnable(); sprite = SpriteLibrary.WhiteSquare(); color = Color.white; }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper m)
        {
            m.Clear(); Color cyan = accent; cyan.a = available ? 0.7f : 0.16f;
            Fill(m, new Vector2(0, 15), new Vector2(100, 100), new Color(0.025f, 0.065f, 0.12f, available ? 0.76f : 0.15f), new Color(0.025f, 0.065f, 0.12f, available ? 0.76f : 0.15f));
            Ring(m, new Vector2(0, 15), new Vector2(100, 100), cyan, Captain ? 2 : 1);
            Color inner = cyan; inner.a *= 0.4f;
            Ring(m, new Vector2(0, 15), new Vector2(89, 89), inner, 1);
            Line(m, new Vector2(-85, -168), new Vector2(85, -168), inner, 1);
        }
        private static void Quad(UnityEngine.UI.VertexHelper m, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color shade)
        {
            int n = m.currentVertCount; m.AddVert(a, shade, Vector2.zero); m.AddVert(b, shade, Vector2.zero);
            m.AddVert(c, shade, Vector2.zero); m.AddVert(d, shade, Vector2.zero); m.AddTriangle(n, n + 1, n + 2); m.AddTriangle(n, n + 2, n + 3);
        }
        private static void Line(UnityEngine.UI.VertexHelper m, Vector2 a, Vector2 b, Color shade, float width)
        {
            Vector2 n = Vector2.Perpendicular((b - a).normalized) * width * 0.5f;
            Quad(m, a - n, b - n, b + n, a + n, shade);
            Color clear = new Color(shade.r, shade.g, shade.b, 0); Vector2 feather = n.normalized * 0.7f;
            int i = m.currentVertCount;
            m.AddVert(a + n, shade, Vector2.zero); m.AddVert(b + n, shade, Vector2.zero);
            m.AddVert(b + n + feather, clear, Vector2.zero); m.AddVert(a + n + feather, clear, Vector2.zero);
            m.AddTriangle(i, i + 1, i + 2); m.AddTriangle(i, i + 2, i + 3);
        }
        private static void Ring(UnityEngine.UI.VertexHelper m, Vector2 center, Vector2 radius, Color shade, float width)
        {
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
                Line(m, center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                    center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), shade, width);
            }
        }
        private static void Fill(UnityEngine.UI.VertexHelper m, Vector2 center, Vector2 radius, Color shade, Color edge)
        {
            int n = m.currentVertCount; m.AddVert(center, shade, Vector2.zero);
            for (int i = 0; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                m.AddVert(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y), edge, Vector2.zero);
                if (i > 0) m.AddTriangle(n, n + i, n + i + 1);
            }
        }
    }
}
