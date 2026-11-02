using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Rasterizes dynamic font glyphs at twice the canvas density, retaining logical layout.</summary>
    public class SharpText : Text
    {
        private readonly UIVertex[] quad = new UIVertex[4];

        protected override void OnPopulateMesh(VertexHelper output)
        {
            output.Clear();
            if (font == null) return;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
                settings.scaleFactor *= 2f;
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                // Font atlas growth can rebuild another label with Unity's shared VertexHelper.
                // Clear after glyph generation so a nested rebuild cannot leave stray geometry.
                output.Clear();
                var vertices = cachedTextGenerator.verts;
                float units = 1f / (pixelsPerUnit * 2f);
                for (int i = 0; i < vertices.Count; i++)
                {
                    int corner = i & 3;
                    quad[corner] = vertices[i];
                    quad[corner].position *= units;
                    if (corner == 3) output.AddUIVertexQuad(quad);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }
    }
}
