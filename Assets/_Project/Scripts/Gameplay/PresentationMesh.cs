using System.Collections.Generic;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Small, batched 2D shapes for stadium equipment and touchline characters.</summary>
    public class PresentationMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        public Vector2 Origin { get; set; }
        public float Rotation { get; set; }

        private void Vertex(Vector2 point, Color color)
        {
            float angle = Rotation * Mathf.Deg2Rad;
            vertices.Add(Origin + new Vector2(point.x * Mathf.Cos(angle) - point.y * Mathf.Sin(angle),
                point.x * Mathf.Sin(angle) + point.y * Mathf.Cos(angle)));
            colors.Add(color);
        }

        public void Quad(Vector2 center, Vector2 size, Color color)
        {
            Vector2 half = size * 0.5f;
            Polygon(new[] { center + new Vector2(-half.x,-half.y), center + new Vector2(half.x,-half.y),
                center + half, center + new Vector2(-half.x,half.y) }, color);
        }

        public void Polygon(Vector2[] points, Color color)
        {
            int start = vertices.Count;
            foreach (Vector2 point in points) Vertex(point, color);
            for (int i = 1; i < points.Length - 1; i++)
            { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }

        public void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * width * 0.5f;
            Polygon(new[] { from-normal,to-normal,to+normal,from+normal }, color);
        }

        public void Ellipse(Vector2 center, Vector2 radius, Color color, bool soft = false, int segments = 24)
        {
            // A radial alpha falloff avoids hard-edged light pools and contact shadows.
            const int rings = 5;
            int count = soft ? rings : 1;
            int start = vertices.Count;
            Vertex(center, color);
            for (int ring = 1; ring <= count; ring++)
            {
                float t = (float)ring / count;
                Color edge = color;
                if (soft) edge.a *= (1f - t) * (1f - t);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    Vertex(center + new Vector2(Mathf.Cos(a)*radius.x, Mathf.Sin(a)*radius.y)*t, edge);
                }
            }
            for (int i = 0; i < segments; i++)
            { triangles.Add(start); triangles.Add(start+1+i); triangles.Add(start+2+i); }
            for (int ring = 1; ring < count; ring++)
                for (int i = 0; i < segments; i++)
                {
                    int a = start + 1 + (ring-1)*(segments+1)+i, b=a+segments+1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(b+1);
                    triangles.Add(a); triangles.Add(b+1); triangles.Add(a+1);
                }
        }

        public MeshRenderer Build(Transform parent, string name, Material material, int order)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            GameObject child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = order;
            return renderer;
        }

        public static void ReleaseMeshes(Transform root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (Application.isPlaying) Object.Destroy(filter.sharedMesh); else Object.DestroyImmediate(filter.sharedMesh);
            }
        }
    }
}
