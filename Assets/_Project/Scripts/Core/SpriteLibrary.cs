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
        private static Sprite softDisc;
        private static Material blueKit;
        private static Material redKit;

        public static Material KitMaterial(Team team)
        {
            Material cached = team == Team.Blue ? blueKit : redKit;
            if (cached != null) return cached;
            Shader shader = Resources.Load<Shader>("Art/ClubKit");
            if (shader == null) return null;
            cached = new Material(shader) { name = team + " club palette" };
            cached.SetColor("_ClubColor",MatchPalette.TeamColor(team));
            if (team == Team.Blue) blueKit = cached; else redKit = cached;
            return cached;
        }

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

        /// <summary>Reuses the original pixels for a jointed limb without another bitmap.</summary>
        public static Sprite GetRegion(string spriteName, Rect normalizedRect)
        {
            string key = spriteName + normalizedRect;
            if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Sprite source = Get(spriteName);
            Rect rect = source.rect;
            Sprite result = Sprite.Create(source.texture, new Rect(rect.x + rect.width * normalizedRect.x,
                rect.y + rect.height * normalizedRect.y, rect.width * normalizedRect.width,
                rect.height * normalizedRect.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            cache[key] = result;
            return result;
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
            if (spriteName.StartsWith("player_"))
            {
                Material kit = KitMaterial(spriteName.Contains("_red") ? Team.Red : Team.Blue);
                if (kit != null) renderer.sharedMaterial = kit;
            }

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
            const int size = 512;
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

        public static Sprite SoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2((x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1).magnitude;
                    float alpha = Mathf.Clamp01(1 - radius);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha * alpha);
                }
            texture.SetPixels(pixels); texture.Apply();
            softDisc = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            return softDisc;
        }
    }
}
