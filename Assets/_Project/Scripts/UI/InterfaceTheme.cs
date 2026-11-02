using PointPass.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Recolours existing interface accents without rebuilding controls or changing state.</summary>
    public static class InterfaceTheme
    {
        public static void Refresh(Transform root, Color previous, Color accent)
        {
            string oldHex = ColorUtility.ToHtmlStringRGB(previous);
            string newHex = ColorUtility.ToHtmlStringRGB(accent);
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                Color color = graphic.color;
                if (Mathf.Abs(color.r - previous.r) < 0.003f && Mathf.Abs(color.g - previous.g) < 0.003f
                    && Mathf.Abs(color.b - previous.b) < 0.003f)
                    graphic.color = new Color(accent.r, accent.g, accent.b, color.a);
                if (graphic is Text label && label.supportRichText)
                    label.text = label.text.Replace("#" + oldHex, "#" + newHex);
                // Analytic pitch graphics embed their accent in vertex colours.
                graphic.SetVerticesDirty();
            }
        }
    }
}
