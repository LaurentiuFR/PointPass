using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Small vector symbols, independent of the font's Unicode glyph coverage.</summary>
    public class LobbyIcon : UnityEngine.UI.Image
    {
        public enum Symbol { Home, Grid, User, Bag, People, Gear, Coin, Point, Plus, Lock, Trophy, Search, Arrow, Ball, Target, Signal, Shield, Pitch }
        public Symbol Glyph;
        protected override void OnEnable() { base.OnEnable(); sprite = SpriteLibrary.WhiteSquare(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper m)
        {
            m.Clear();
            switch (Glyph)
            {
                case Symbol.Shield:
                    Path(m,new[]{new Vector2(-.64f,.75f),new Vector2(.64f,.75f),new Vector2(.54f,-.28f),new Vector2(0,-.82f),new Vector2(-.54f,-.28f),new Vector2(-.64f,.75f)});
                    Path(m,new[]{new Vector2(-.3f,.05f),new Vector2(-.05f,-.2f),new Vector2(.35f,.3f)}); break;
                case Symbol.Pitch:
                    Box(m,Vector2.zero,new Vector2(.86f,.63f)); Line(m,0,-.63f,0,.63f);
                    Arc(m,Vector2.zero,Vector2.one*.27f,0,Mathf.PI*2);
                    Box(m,new Vector2(-.73f,0),new Vector2(.13f,.3f)); Box(m,new Vector2(.73f,0),new Vector2(.13f,.3f)); break;
                case Symbol.Home:
                    Line(m, -0.8f, 0, 0, 0.7f); Line(m, 0, 0.7f, 0.8f, 0);
                    Path(m, new[] { new Vector2(-0.58f, 0), new Vector2(-0.58f, -0.7f), new Vector2(0.58f, -0.7f), new Vector2(0.58f, 0) }); break;
                case Symbol.Grid:
                    for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
                        Box(m, new Vector2(x, y) * 0.42f, Vector2.one * 0.22f); break;
                case Symbol.User: Person(m, 0, 1); break;
                case Symbol.People: Person(m, -0.37f, 0.68f); Person(m, 0.37f, 0.68f); break;
                case Symbol.Bag:
                    Box(m, new Vector2(0, -0.2f), new Vector2(0.67f, 0.5f));
                    Arc(m, new Vector2(0, 0.28f), new Vector2(0.3f, 0.4f), 0, Mathf.PI); break;
                case Symbol.Gear:
                    Arc(m, Vector2.zero, Vector2.one * 0.48f, 0, Mathf.PI * 2);
                    Arc(m, Vector2.zero, Vector2.one * 0.17f, 0, Mathf.PI * 2);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Segment(m, Direction(a) * 0.51f, Direction(a) * 0.76f); } break;
                case Symbol.Coin: case Symbol.Point:
                    Arc(m, Vector2.zero, Vector2.one * 0.75f, 0, Mathf.PI * 2);
                    if (Glyph == Symbol.Coin) Arc(m, Vector2.zero, Vector2.one * 0.4f, 0.3f, Mathf.PI * 1.8f);
                    else { Line(m, 0, -0.45f, 0, 0.45f); Line(m, 0, 0.45f, 0.35f, 0.15f); Line(m, 0.35f, 0.15f, 0, -0.1f); } break;
                case Symbol.Plus: Line(m, -0.65f, 0, 0.65f, 0); Line(m, 0, -0.65f, 0, 0.65f); break;
                case Symbol.Lock:
                    Box(m, new Vector2(0, -0.3f), new Vector2(0.55f, 0.4f)); Arc(m, new Vector2(0, 0.1f), new Vector2(0.35f, 0.5f), 0, Mathf.PI); break;
                case Symbol.Trophy:
                    Path(m, new[] { new Vector2(-0.5f, 0.7f), new Vector2(-0.4f, 0.1f), new Vector2(0, -0.15f), new Vector2(0.4f, 0.1f), new Vector2(0.5f, 0.7f), new Vector2(-0.5f, 0.7f) });
                    Line(m, 0, -0.15f, 0, -0.65f); Line(m, -0.4f, -0.7f, 0.4f, -0.7f); break;
                case Symbol.Search:
                    Arc(m, new Vector2(-0.15f, 0.15f), Vector2.one * 0.5f, 0, Mathf.PI * 2); Line(m, 0.22f, -0.22f, 0.75f, -0.75f); break;
                case Symbol.Arrow: Line(m, -0.75f, 0, 0.7f, 0); Line(m, 0.2f, 0.5f, 0.7f, 0); Line(m, 0.7f, 0, 0.2f, -0.5f); break;
                case Symbol.Ball: case Symbol.Target:
                    Arc(m, Vector2.zero, Vector2.one * 0.72f, 0, Mathf.PI * 2);
                    Arc(m, Vector2.zero, Vector2.one * 0.3f, 0, Mathf.PI * 2);
                    if (Glyph == Symbol.Target) { Line(m, -0.9f, 0, -0.5f, 0); Line(m, 0.5f, 0, 0.9f, 0); Line(m, 0, 0.5f, 0, 0.9f); }
                    else for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 0.4f; Segment(m, Direction(a) * 0.35f, Direction(a) * 0.7f); } break;
                default: Line(m, -0.6f, -0.55f, 0.6f, 0.55f); break;
            }
        }
        private static Vector2 Direction(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        private void Person(UnityEngine.UI.VertexHelper m, float x, float scale)
        {
            Arc(m, new Vector2(x, 0.35f * scale), Vector2.one * (0.3f * scale), 0, Mathf.PI * 2);
            Arc(m, new Vector2(x, -0.6f * scale), new Vector2(0.58f, 0.58f) * scale, 0, Mathf.PI);
            Segment(m, new Vector2(x - 0.58f * scale, -0.6f * scale), new Vector2(x + 0.58f * scale, -0.6f * scale));
        }
        private void Box(UnityEngine.UI.VertexHelper m, Vector2 center, Vector2 half)
        {
            Path(m, new[] { center - half, center + new Vector2(half.x, -half.y), center + half, center + new Vector2(-half.x, half.y), center - half });
        }
        private void Path(UnityEngine.UI.VertexHelper m, Vector2[] points) { for (int i = 1; i < points.Length; i++) Segment(m, points[i - 1], points[i]); }
        private void Line(UnityEngine.UI.VertexHelper m, float x1, float y1, float x2, float y2) => Segment(m, new Vector2(x1, y1), new Vector2(x2, y2));
        private void Arc(UnityEngine.UI.VertexHelper m, Vector2 center, Vector2 radius, float from, float to)
        {
            const int count = 40;
            for (int i = 0; i < count; i++) Segment(m, center + Vector2.Scale(Direction(Mathf.Lerp(from, to, i / (float)count)), radius),
                center + Vector2.Scale(Direction(Mathf.Lerp(from, to, (i + 1) / (float)count)), radius));
        }
        private void Segment(UnityEngine.UI.VertexHelper m, Vector2 a, Vector2 b)
        {
            Vector2 size = rectTransform.rect.size * 0.5f;
            a = Vector2.Scale(a, size); b = Vector2.Scale(b, size);
            Vector2 normal = Vector2.Perpendicular((b - a).normalized);
            int n = m.currentVertCount;
            for (int i = 0; i < 4; i++)
            {
                float offset = i == 0 ? -1.5f : i == 1 ? -0.7f : i == 2 ? 0.7f : 1.5f;
                Color shade = color; if (i == 0 || i == 3) shade.a = 0;
                m.AddVert(a + normal * offset, shade, Vector2.zero); m.AddVert(b + normal * offset, shade, Vector2.zero);
            }
            for (int i = 0; i < 3; i++) { int s = n + i * 2; m.AddTriangle(s, s + 1, s + 3); m.AddTriangle(s, s + 3, s + 2); }
        }
    }
}
