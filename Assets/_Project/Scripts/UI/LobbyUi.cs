using System;
using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    internal static class LobbyUi
    {
        internal static Color Cyan => MatchPalette.Accent;
        internal static readonly Color Aqua = new Color(0.68f, 0.9f, 1f);
        internal static readonly Color Dim = new Color(0.52f, 0.66f, 0.75f);
        internal static readonly Color Glass = new Color(0.025f, 0.075f, 0.13f, 0.88f);
        internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = Vector2.one * 0.5f;
            r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        internal static UnityEngine.UI.Image Image(Transform parent, string name, Vector2 position, Vector2 size,
            Color color, Sprite sprite = null)
        {
            var image = Rect(parent, name, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite != null ? sprite : SpriteLibrary.WhiteSquare(); image.color = color; image.raycastTarget = false;
            return image;
        }
        internal static UnityEngine.UI.Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color? shade = null, bool border = false)
        {
            var image = Image(parent, name, position, size, shade ?? Glass, MatchMenu.Rounded());
            image.type = UnityEngine.UI.Image.Type.Sliced;
            if (border)
            {
                var edge = Rect(image.transform, "NeonFrame", Vector2.zero, size).gameObject.AddComponent<LobbyBorderGraphic>();
                Stretch(edge.rectTransform); edge.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
            }
            return image;
        }
        internal static UnityEngine.UI.Text Text(Transform parent, string name, string value, int size, Vector2 position,
            Vector2 bounds, Color? shade = null, bool bold = false, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var text = Rect(parent, name, position, bounds).gameObject.AddComponent<SharpText>();
            text.font = Resources.Load<Font>(bold ? "Fonts/BarlowCondensed-Bold" : "Fonts/BarlowCondensed-Regular")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.fontStyle = FontStyle.Normal; text.text = value; text.color = shade ?? MatchPalette.Paper;
            text.alignment = align; text.raycastTarget = false; text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        internal static UnityEngine.UI.Button Button(Transform parent, string name, string title, Vector2 position,
            Vector2 size, Action action, bool primary = false)
        {
            Color background = primary ? new Color(0.64f, 0.85f, 0.94f, 1) : new Color(0.08f, 0.17f, 0.23f, 0.96f);
            var image = Panel(parent, name, position, size, background);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<MenuButton>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.04f, 1.06f, 1.06f);
            colors.selectedColor = new Color(1.06f, 1.08f, 1.08f); colors.pressedColor = new Color(0.7f, 0.9f, 0.95f);
            colors.disabledColor = new Color(0.35f, 0.45f, 0.5f, 0.65f); colors.fadeDuration = 0.08f; button.colors = colors;
            Text(image.transform, "Label", title, 30, Vector2.zero, size - new Vector2(24, 6), primary ? MatchPalette.Ink : MatchPalette.Paper, true);
            button.onClick.AddListener(() => { if (button.isActiveAndEnabled && button.IsInteractable()) action?.Invoke(); });
            return button;
        }
        internal static LobbyIcon Icon(Transform parent, string name, LobbyIcon.Symbol symbol, Vector2 position, float size, Color? color = null)
        {
            var icon = Rect(parent, name, position, Vector2.one * size).gameObject.AddComponent<LobbyIcon>();
            icon.Glyph = symbol; icon.color = color ?? Cyan; icon.raycastTarget = false; return icon;
        }
    }
}
