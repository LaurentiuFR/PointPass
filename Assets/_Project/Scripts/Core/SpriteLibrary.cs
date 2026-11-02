using System.Collections.Generic;
using UnityEngine;

namespace PointPass.Core
{
    /// <summary>
    /// Loads sprites from any Resources/Art folder and sizes them in world units, so import
    /// settings never matter. If an image is missing it falls back to a plain coloured shape.
    /// </summary>
    public static class SpriteLibrary
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static Sprite whiteSquare;
        private static Sprite whiteCircle;

        public static Sprite Get(string spriteName, bool circleFallback = false)
        {
            Sprite sprite = Load(spriteName);
            if (sprite != null) return sprite;

            Debug.LogWarning($"[PointPass] Sprite 'Art/{spriteName}' not found in a Resources folder. Using a placeholder.");
            return circleFallback ? WhiteCircle() : WhiteSquare();
        }

        /// <summary>A stretchable rounded panel. Corners stay sharp at any size.</summary>
        public static Sprite GetSliced(string spriteName, float border)
        {
            string key = spriteName + "#sliced";
            if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Texture2D texture = Resources.Load<Texture2D>("Art/" + spriteName);
            if (texture == null)
            {
                Debug.LogWarning($"[PointPass] Panel image 'Art/{spriteName}' not found. Using a plain rectangle.");
                return WhiteSquare();
            }

            Sprite sprite = Sprite.Create(
                texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            cache[key] = sprite;
            return sprite;
        }

        public static bool IsPlaceholder(Sprite sprite)
        {
            return sprite == whiteSquare || sprite == whiteCircle;
        }

        /// <summary>Creates a child object with a SpriteRenderer scaled to worldSize (in world units).</summary>
        public static SpriteRenderer Create(
            Transform parent, string objectName, string spriteName, Vector2 worldSize,
            Vector2 localPosition, int sortingOrder, Color placeholderColor,
            bool circleFallback = false, bool optional = false)
        {
            Sprite sprite = Load(spriteName);
            if (sprite == null)
            {
                if (optional) return null;
                sprite = Get(spriteName, circleFallback);
            }

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = IsPlaceholder(sprite) ? placeholderColor : Color.white;

            Vector2 native = sprite.bounds.size;
            go.transform.localScale = new Vector3(worldSize.x / native.x, worldSize.y / native.y, 1f);
            return renderer;
        }

        private static Sprite Load(string spriteName)
        {
            if (cache.TryGetValue(spriteName, out Sprite cached) && cached != null) return cached;
            Sprite sprite = Resources.Load<Sprite>("Art/" + spriteName);
            if (sprite != null) cache[spriteName] = sprite;
            return sprite;
        }

        public static Sprite WhiteSquare()
        {
            if (whiteSquare != null) return whiteSquare;
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return whiteSquare;
        }

        public static Sprite WhiteCircle()
        {
            if (whiteCircle != null) return whiteCircle;
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            whiteCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return whiteCircle;
        }
    }
}
