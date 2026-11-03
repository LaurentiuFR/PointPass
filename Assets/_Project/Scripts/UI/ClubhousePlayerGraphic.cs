using System;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>The reference footballer as scalable vector geometry, with a quiet menu idle.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ClubhousePlayerGraphic : UnityEngine.UI.MaskableGraphic
    {
        [Serializable] private class Shape { public int part; public float[] color; public Vector2[] vertices; public int[] triangles; }
        [Serializable] private class Artwork { public float width, height; public Shape[] shapes; }
        private static Artwork artwork;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetArtwork() { artwork = null; }
        public bool HasArtwork => Load() != null;
        private static Artwork Load()
        {
            if (artwork == null)
            {
                var asset = Resources.Load<TextAsset>("Menu/ClubhouseFootballer");
                if (asset != null) artwork = JsonUtility.FromJson<Artwork>(asset.text);
            }
            return artwork;
        }
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; Load(); }
        private void Update() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); var art = Load(); if (art == null) return;
            float phase = Time.unscaledTime % 5.2f;
            float t = Mathf.Clamp01((phase - 3.8f) / 0.8f);
            float height = phase >= 3.8f && phase <= 4.6f ? 4f*t*(1f-t)*76f : 0;
            float breath = 1f + Mathf.Sin(Time.unscaledTime*2.1f)*0.003f;
            Vector2 scale = new Vector2(rectTransform.rect.width/art.width, rectTransform.rect.height/art.height);
            foreach (var shape in art.shapes)
            {
                if (shape.part == 6) continue; // The jersey number is editable, supersampled text.
                int start = mesh.currentVertCount;
                Color shade = new Color(shape.color[0],shape.color[1],shape.color[2],shape.color[3]) * color;
                foreach (var source in shape.vertices)
                {
                    Vector2 point=source;
                    if (shape.part == 0) point.y=26f+(point.y-26f)*breath;
                    if (shape.part == 2) point.y+=height;
                    point=Vector2.Scale(point-new Vector2(art.width*.5f,art.height*.5f),scale);
                    mesh.AddVert(point,shade,Vector2.zero);
                }
                for (int i=0;i+2<shape.triangles.Length;i+=3) mesh.AddTriangle(start+shape.triangles[i],start+shape.triangles[i+1],start+shape.triangles[i+2]);
            }
        }
    }
}
