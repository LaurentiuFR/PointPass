using PointPass.Audio;
using PointPass.Core;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Local sound settings inside the existing modal menu and its navigation system.</summary>
    public class AudioSettingsView : MonoBehaviour
    {
        private UnityEngine.UI.Toggle muted;
        private UnityEngine.UI.Slider volume;
        private UnityEngine.UI.Text percentage;
        private UnityEngine.UI.Image fill, check;

        public void Build()
        {
            if (volume != null) return;
            Label("AudioDescription", "Football sounds. Set the level that suits you.", 25,
                new Vector2(0, 178), new Vector2(1040, 56), MatchPalette.Muted);
            RectTransform muteRoot = Rect("SoundMuted", transform, new Vector2(0, 77), new Vector2(700, 76));
            UnityEngine.UI.Image background = muteRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = SpriteLibrary.WhiteSquare(); background.color = MatchPalette.Ink;
            muted = muteRoot.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            muted.targetGraphic = background;
            UnityEngine.UI.Image outline = Image("Checkbox", muteRoot, new Vector2(-302, 0), Vector2.one * 34, MatchPalette.Muted);
            check = Image("Check", outline.transform, Vector2.zero, Vector2.one * 24, MatchPalette.Accent);
            check.raycastTarget = false;
            muted.graphic = check;
            UnityEngine.UI.Text muteLabel = Label("MuteLabel", "MUTE SOUNDS", 24,
                new Vector2(22, 0), new Vector2(590, 60), MatchPalette.Paper, muteRoot);
            muteLabel.alignment = TextAnchor.MiddleLeft;

            Label("VolumeTitle", "VOLUME", 22, new Vector2(-235, -22), new Vector2(220, 45), MatchPalette.Paper);
            percentage = Label("SoundVolumePercent", "", 24, new Vector2(285, -22), new Vector2(130, 45), MatchPalette.Accent);
            RectTransform sliderRoot = Rect("SoundVolume", transform, new Vector2(0, -83), new Vector2(700, 64));
            UnityEngine.UI.Image sliderTouchArea = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            sliderTouchArea.color = Color.clear;
            volume = sliderRoot.gameObject.AddComponent<UnityEngine.UI.Slider>();
            volume.minValue = 0; volume.maxValue = 1; volume.wholeNumbers = false;
            Image("Track", sliderRoot, Vector2.zero, new Vector2(670, 12), MatchPalette.Ink).raycastTarget = false;
            RectTransform fillArea = Rect("FillArea", sliderRoot, Vector2.zero, new Vector2(670, 12));
            fill = Image("Fill", fillArea, Vector2.zero, new Vector2(670, 12), MatchPalette.Accent);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            RectTransform handleArea = Rect("HandleArea", sliderRoot, Vector2.zero, new Vector2(670, 64));
            UnityEngine.UI.Image handle = Image("Handle", handleArea, Vector2.zero, new Vector2(28, 44), MatchPalette.Paper);
            // Slider stretches the handle anchors vertically; keep its final height at 44.
            handle.rectTransform.sizeDelta = new Vector2(28, -20);
            volume.fillRect = fill.rectTransform;
            volume.handleRect = handle.rectTransform;
            volume.targetGraphic = handle;
            volume.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            Label("AudioHint", "Sound settings are saved on this device.", 20,
                new Vector2(0, -170), new Vector2(1040, 48), MatchPalette.Muted);
            muted.onValueChanged.AddListener(AudioPreferences.SetMuted);
            volume.onValueChanged.AddListener(AudioPreferences.SetVolume);
            Refresh();
        }

        private void OnEnable()
        {
            AudioPreferences.Changed += Refresh;
            MatchPalette.AccentChanged += OnAccent;
            Refresh();
        }
        private void OnDisable()
        {
            AudioPreferences.Changed -= Refresh;
            MatchPalette.AccentChanged -= OnAccent;
        }
        private void Refresh()
        {
            if (this == null) { AudioPreferences.Changed -= Refresh; return; }
            if (volume == null) return;
            volume.SetValueWithoutNotify(AudioPreferences.Volume);
            muted.SetIsOnWithoutNotify(AudioPreferences.Muted);
            percentage.text = Mathf.RoundToInt(AudioPreferences.Volume * 100) + "%";
        }
        private void OnAccent(Color previous, Color next)
        {
            if (this == null) { MatchPalette.AccentChanged -= OnAccent; return; }
            InterfaceTheme.Refresh(transform, previous, next);
            if (fill != null) fill.color = next;
            if (check != null) check.color = next;
            if (percentage != null) percentage.color = next;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            UnityEngine.UI.Image image = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = SpriteLibrary.WhiteSquare(); image.color = color;
            return image;
        }
        private UnityEngine.UI.Text Label(string name, string text, int size, Vector2 position, Vector2 dimensions,
            Color color, Transform parent = null)
        {
            SharpText label = Rect(name, parent != null ? parent : transform, position, dimensions).gameObject.AddComponent<SharpText>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.color = color; label.text = text;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            return label;
        }
        private void OnDestroy()
        {
            AudioPreferences.Changed -= Refresh;
            MatchPalette.AccentChanged -= OnAccent;
        }
    }
}
