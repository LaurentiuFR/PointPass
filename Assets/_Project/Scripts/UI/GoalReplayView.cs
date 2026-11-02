using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Isolated world replay, driven by one unscaled playback clock.</summary>
    public class GoalReplayView : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0.5f, 1f, 1.5f, 2f };
        private GoalReplayClip clip;
        private GoalReplayWorld world;
        private RawImage viewport;
        private RenderTexture texture;
        private float playhead;
        private readonly Image[] speedImages = new Image[4];
        private readonly Text[] speedLabels = new Text[4];
        private Text arrowLabel, phaseLabel, pauseLabel, elapsedLabel;
        private Image arrowImage;
        private bool showArrow;
        public float PlaybackTime => playhead;
        public float PlaybackSpeed { get; private set; } = 1f;
        public bool ShowShotArrow => showArrow;
        public bool ReplayIsPaused { get; private set; }
        public float PlaybackDuration => clip != null ? clip.Duration : 0;
        public GoalReplayWorld World => world;

        public void Build()
        {
            viewport = Rect("ReplayViewport", transform, new Vector2(0, 70), new Vector2(1180, 480)).gameObject.AddComponent<RawImage>();
            viewport.raycastTarget = false; viewport.color = Color.white;
            Label("ReplayLabel", transform, "GOAL HIGHLIGHT", 28, new Vector2(-370, 348), new Vector2(440, 50), MatchPalette.Paper);
            phaseLabel = Label("ReplayPhase", transform, "BUILD-UP", 20, new Vector2(420, 348), new Vector2(340, 45), MatchPalette.Accent);
            Label("SpeedCaption", transform, "PLAYBACK", 17, new Vector2(-495, -207), new Vector2(170, 50), MatchPalette.Muted);
            for (int i = 0; i < Speeds.Length; i++)
            {
                int index = i;
                Button button = Button("Speed" + i, new Vector2(-350 + i * 115, -207), new Vector2(106, 50),
                    Speeds[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "×", () => SetPlaybackSpeed(Speeds[index]));
                speedImages[i] = button.GetComponent<Image>(); speedLabels[i] = button.GetComponentInChildren<Text>();
            }
            Button arrow = Button("ShowShotArrow", new Vector2(355, -207), new Vector2(360, 50), "[ ] SHOW SHOT ARROW", () => SetShowShotArrow(!showArrow));
            arrowLabel = arrow.GetComponentInChildren<Text>();
            arrowImage = arrow.GetComponent<Image>();
            Button pause = Button("PauseReplay", new Vector2(-402, -273), new Vector2(230, 50), "PAUSE", () => SetPaused(!ReplayIsPaused));
            pauseLabel = pause.GetComponentInChildren<Text>();
            Button("RestartReplay", new Vector2(-147, -273), new Vector2(230, 50), "RESTART", Restart);
            elapsedLabel = Label("ReplayElapsed", transform, "00:00.0 / 00:00.0", 22,
                new Vector2(345, -273), new Vector2(380, 50), MatchPalette.Paper);
            RefreshControls();
        }

        public void Play(GoalReplayClip replay)
        {
            if (!GoalReplayRecorder.IsValid(replay)) return;
            clip = replay; playhead = 0; PlaybackSpeed = 1; showArrow = false; ReplayIsPaused = false;
            ReleaseWorld();
            // Match the actual display density, capped at native 1080p for mobile cost.
            Canvas canvas = GetComponentInParent<Canvas>();
            float density = canvas != null ? Mathf.Max(1, canvas.scaleFactor) : 1;
            int width = Mathf.Clamp(Mathf.CeilToInt(1180 * density), 1180, 1920);
            int height = Mathf.CeilToInt(width * 480f / 1180f);
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "Goal replay native scene", antiAliasing = 4, filterMode = FilterMode.Bilinear, useMipMap = false };
            texture.Create(); viewport.texture = texture;
            world = new GameObject("Isolated Goal Replay", typeof(GoalReplayWorld)).GetComponent<GoalReplayWorld>();
            world.Build(clip, texture); world.gameObject.SetActive(isActiveAndEnabled);
            RefreshControls(); Present();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if (clip == null || world == null || ReplayIsPaused || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            // Hold the final celebration frame briefly, then start the same continuous clip.
            playhead = Mathf.Repeat(playhead + seconds * PlaybackSpeed, clip.Duration + 0.8f);
            Present();
        }
        public void SetPlaybackSpeed(float speed)
        {
            foreach (float allowed in Speeds)
                if (Mathf.Approximately(speed, allowed)) { PlaybackSpeed = allowed; RefreshControls(); Present(); return; }
        }
        public void SetPaused(bool paused) { ReplayIsPaused = paused; RefreshControls(); Present(); }
        /// <summary>Return to the recorded opening frame without changing the viewer's preferences.</summary>
        public void Restart() { playhead = 0; RefreshControls(); Present(); }
        public void SetShowShotArrow(bool visible) { showArrow = visible; RefreshControls(); Present(); }
        private void Present()
        {
            if (clip == null || world == null) return;
            float time = clip.frames[0].time + Mathf.Min(playhead, clip.Duration);
            world.Present(time, showArrow);
            if (phaseLabel != null) phaseLabel.text = time < clip.goalTime ? "BUILD-UP  /  " + PlaybackSpeed.ToString("0.#") + "×"
                : "GOAL + CELEBRATION  /  " + PlaybackSpeed.ToString("0.#") + "×";
            if (elapsedLabel != null) elapsedLabel.text = FormatTime(Mathf.Min(playhead, clip.Duration)) + " / " + FormatTime(clip.Duration);
        }
        private static string FormatTime(float seconds)
        {
            int tenths = Mathf.FloorToInt(Mathf.Max(0, seconds) * 10);
            return $"{tenths / 600:00}:{tenths / 10 % 60:00}.{tenths % 10}";
        }
        private void RefreshControls()
        {
            for (int i = 0; i < speedImages.Length; i++)
            {
                bool selected = Mathf.Approximately(PlaybackSpeed, Speeds[i]);
                if (speedImages[i] != null) speedImages[i].color = selected ? MatchPalette.Accent : MatchPalette.Surface;
                if (speedLabels[i] != null) speedLabels[i].color = selected ? MatchPalette.Ink : MatchPalette.Paper;
            }
            if (arrowLabel != null) arrowLabel.text = (showArrow ? "[✓] " : "[ ] ") + "SHOW SHOT ARROW";
            if (arrowImage != null) arrowImage.color = showArrow ? MatchPalette.Accent : MatchPalette.Surface;
            if (arrowLabel != null) arrowLabel.color = showArrow ? MatchPalette.Ink : MatchPalette.Paper;
            if (pauseLabel != null) pauseLabel.text = ReplayIsPaused ? "RESUME" : "PAUSE";
        }
        private Button Button(string name, Vector2 position, Vector2 size, string text, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, transform, position, size);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = SpriteLibrary.WhiteSquare();
            image.type = Image.Type.Simple; image.color = MatchPalette.Surface;
            Button button = rect.gameObject.AddComponent<MenuButton>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.75f, 0.8f, 0.85f); button.colors = colors;
            Label("Label", rect, text, 19, Vector2.zero, size - new Vector2(12, 4), MatchPalette.Paper);
            button.onClick.AddListener(() => { if (isActiveAndEnabled) action(); });
            return button;
        }
        private static Text Label(string name, Transform parent, string text, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            SharpText label = Rect(name, parent, position, dimensions).gameObject.AddComponent<SharpText>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size;
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter; label.color = color;
            label.text = text; label.raycastTarget = false; return label;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private void OnEnable()
        {
            MatchPalette.AccentChanged += OnAccentChanged;
            OnAccentChanged(MatchPalette.Accent, MatchPalette.Accent);
            if (world != null) { world.Camera.enabled = true; world.gameObject.SetActive(true); }
        }
        private void OnDisable()
        {
            MatchPalette.AccentChanged -= OnAccentChanged;
            if (world != null) { world.Camera.enabled = false; world.gameObject.SetActive(false); }
        }
        private void OnAccentChanged(Color previous, Color next)
        {
            if (this == null) { MatchPalette.AccentChanged -= OnAccentChanged; return; }
            RefreshControls();
            if (phaseLabel != null) phaseLabel.color = next;
            world?.RefreshAccent();
        }
        private void ReleaseWorld()
        {
            if (world != null) { world.gameObject.SetActive(false); Release(world.gameObject); world = null; }
            if (texture != null) { texture.Release(); Release(texture); texture = null; }
            if (viewport != null) viewport.texture = null;
        }
        private static void Release(Object resource) { if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource); }
        private void OnDestroy()
        {
            MatchPalette.AccentChanged -= OnAccentChanged;
            ReleaseWorld();
        }
    }
}
