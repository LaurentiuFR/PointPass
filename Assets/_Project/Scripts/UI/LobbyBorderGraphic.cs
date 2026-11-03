using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Only draws the frame, avoiding filled Outline copies behind translucent glass.</summary>
    public class LobbyBorderGraphic : UnityEngine.UI.Image
    {
        protected override void OnEnable() { base.OnEnable(); sprite = SpriteLibrary.WhiteSquare(); raycastTarget = false; }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); Vector2 half = rectTransform.rect.size * 0.5f;
            float radius = Mathf.Min(12, half.x * 0.5f, half.y * 0.5f);
            const int steps = 16, corners = 4, perCorner = steps + 1;
            for (int q = 0; q < corners; q++)
            {
                Vector2 center = new Vector2((q == 0 || q == 3 ? 1 : -1) * (half.x - radius), (q < 2 ? 1 : -1) * (half.y - radius));
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (q + i / (float)steps) * Mathf.PI * 0.5f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    for (int edge = 0; edge < 4; edge++)
                    {
                        float offset = edge == 0 ? 1.2f : edge == 1 ? 0.55f : edge == 2 ? -0.55f : -1.2f;
                        Color shade = color; if (edge == 0 || edge == 3) shade.a = 0;
                        mesh.AddVert(center + direction * (radius + offset), shade, Vector2.zero);
                    }
                }
            }
            int count = corners * perCorner;
            for (int i = 0; i < count; i++)
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = i * 4 + edge, b = (i + 1) % count * 4 + edge;
                    mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(a, b + 1, a + 1);
                }
        }
    }
}
