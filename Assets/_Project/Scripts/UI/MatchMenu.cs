using System;
using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Modal presentation for the offline prototype. MatchManager owns match state.</summary>
    public class MatchMenu : MonoBehaviour, ICancelHandler
    {
        public enum Screen { Main, Pause, Results, Controls, Replay, Audio }

        private static Color Ink => MatchPalette.Ink;
        private static Color Surface => MatchPalette.Surface;
        private static Color Paper => MatchPalette.Paper;
        private static Color Muted => MatchPalette.Muted;
        private static Color Mint => MatchPalette.Accent;
        private static Sprite roundedSprite;
        private static Sprite ringSprite;

        private readonly Dictionary<Screen, GameObject> screens = new Dictionary<Screen, GameObject>();
        private readonly Dictionary<Screen, Button> firstButtons = new Dictionary<Screen, Button>();
        private RectTransform safeArea;
        private RectTransform content;
        private Image backdrop;
        private Text resultTitle;
        private Text resultScore;
        private Rect lastSafeArea;
        private Vector2Int lastSize;
        private Screen controlsOrigin;
        private Screen audioOrigin;
        private bool practiceMode;
        private Text pauseDescription, pauseRestartLabel;
        private int lastActivationFrame = -1;
        private LobbyMenuView lobbyView;
        private GoalReplayClip latestReplay;
        private GoalReplayView replayView;
        private Screen replayOrigin;
        private Button pauseReplay;
        private Button resultsReplay;

        public event Action PlayRequested;
        public event Action PracticeRequested;
        public event Action FreePlayRequested;
        public event Action<bool> ScenarioRequested;
        public int KeeperDifficulty { get; private set; } = 1;
        public LobbyMenuView Lobby => lobbyView;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action MainMenuRequested;

        public bool IsOpen => gameObject.activeSelf;
        public Screen CurrentScreen { get; private set; }

        public static MatchMenu Create()
        {
            GameObject root = new GameObject("MatchMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;

            MatchMenu menu = root.AddComponent<MatchMenu>();
            menu.backdrop = Picture(root.transform, "ModalBackdrop", SpriteLibrary.WhiteSquare(), Ink);
            Stretch(menu.backdrop.rectTransform);
            menu.backdrop.raycastTarget = true;
            menu.safeArea = Rect(root.transform, "SafeArea");
            Stretch(menu.safeArea);
            menu.content = Rect(menu.safeArea, "Content");
            Place(menu.content, Vector2.zero, new Vector2(1320, 820));
            menu.EnsureEventSystem();
            menu.BuildMain();
            menu.BuildPause();
            menu.BuildResults();
            menu.BuildControls();
            menu.BuildReplay();
            menu.BuildAudio();
            InterfaceTheme.Refresh(menu.transform, new Color(0.718f, 0.957f, 0.2f), Mint);
            MatchPalette.AccentChanged += menu.OnAccentChanged;
            menu.ApplyLayout();
            menu.Hide();
            return menu;
        }

        private void Update()
        {
            if (UnityEngine.Screen.safeArea != lastSafeArea || UnityEngine.Screen.width != lastSize.x
                || UnityEngine.Screen.height != lastSize.y) ApplyLayout();

        }

        // Kept for existing appearance-review callers; the native match preview is static.
        public void AdvanceMotion(float seconds) { }

        private void OnEnable()
        {
            if (safeArea != null) ApplyLayout();
        }

        public void RefreshLayout() => ApplyLayout();

        private void ApplyLayout()
        {
            int width = UnityEngine.Screen.width;
            int height = UnityEngine.Screen.height;
            if (width <= 0 || height <= 0 || safeArea == null) return;
            lastSafeArea = UnityEngine.Screen.safeArea;
            lastSize = new Vector2Int(width, height);
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / width, lastSafeArea.yMin / height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / width, lastSafeArea.yMax / height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            Vector2 design = CurrentScreen == Screen.Main ? (lobbyView != null ? lobbyView.DesignSize : new Vector2(1824, 844)) : new Vector2(1320, 820);
            content.sizeDelta = design;
            Vector2 available = safeArea.rect.size - Vector2.one * (CurrentScreen == Screen.Main ? 16 : 40);
            float scale = Mathf.Max(0.2f, Mathf.Min(available.x / design.x, available.y / design.y));
            content.localScale = Vector3.one * scale;
        }

        public void ShowMain() { lobbyView?.ShowPage(LobbyMenuView.Page.MainLobby); Show(Screen.Main); }
        public void RequestOfflineMatch(int difficulty = 1) { KeeperDifficulty = Mathf.Clamp(difficulty, 0, 2); lobbyView?.CloseModal(); PlayRequested?.Invoke(); }
        public void RequestPassingPractice() { lobbyView?.CloseModal(); PracticeRequested?.Invoke(); }
        public void RequestFreePlay() { lobbyView?.CloseModal(); FreePlayRequested?.Invoke(); }
        public void RequestScenario(bool penalty) { lobbyView?.CloseModal(); ScenarioRequested?.Invoke(penalty); }
        public void RequestControls() => lobbyView.ShowGuide();
        public void ShowPause() => Show(Screen.Pause);

        public void ShowResults(int blue, int red)
        {
            resultTitle.text = blue == red ? "HONOURS EVEN" : blue > red ? "BLUE WINS" : "RED WINS";
            resultScore.text = $"<color=#4085DB>{blue}</color> <color=#627780>:</color> <color=#E0594A>{red}</color>";
            Show(Screen.Results);
        }

        public void Hide()
        {
            ClearOwnedSelection();
            gameObject.SetActive(false);
        }

        private void Show(Screen screen)
        {
            CurrentScreen = screen;
            gameObject.SetActive(true);
            foreach (KeyValuePair<Screen, GameObject> entry in screens) entry.Value.SetActive(entry.Key == screen);
            backdrop.color = screen == Screen.Main ? LobbyMenuView.ClubhouseNavy : new Color(Ink.r, Ink.g, Ink.b, 0.96f);
            ApplyLayout();
            EventSystem system = EventSystem.current;
            if (system != null)
            {
                system.SetSelectedGameObject(null);
                system.SetSelectedGameObject(firstButtons[screen].gameObject);
            }
        }

        private void ShowControls(Screen origin)
        {
            controlsOrigin = origin;
            Show(Screen.Controls);
        }

        /// <summary>UI Cancel goes back one level without changing match state directly.</summary>
        public bool TryBack()
        {
            if (!IsOpen) return false;
            if (CurrentScreen == Screen.Main) return lobbyView != null && lobbyView.TryBack();
            if (CurrentScreen == Screen.Controls) { Show(controlsOrigin); return true; }
            if (CurrentScreen == Screen.Audio) { Show(audioOrigin); return true; }
            if (CurrentScreen == Screen.Replay) { Show(replayOrigin); return true; }
            if (CurrentScreen == Screen.Pause) { ResumeRequested?.Invoke(); return true; }
            return false;
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (TryBack()) eventData.Use();
        }

        private void ClearOwnedSelection()
        {
            EventSystem system = EventSystem.current;
            if (system != null && system.currentSelectedGameObject != null
                && system.currentSelectedGameObject.transform.IsChildOf(transform)) system.SetSelectedGameObject(null);
        }

        private RectTransform Page(Screen screen)
        {
            RectTransform page = Rect(content, screen.ToString());
            Stretch(page);
            screens.Add(screen, page.gameObject);
            page.gameObject.SetActive(false);
            return page;
        }

        private void BuildMain()
        {
            RectTransform page = Page(Screen.Main);
            lobbyView = page.gameObject.AddComponent<LobbyMenuView>();
            lobbyView.Build(this);
            firstButtons.Add(Screen.Main, lobbyView.FirstButton);
        }

        private void BuildPause()
        {
            RectTransform page = Page(Screen.Pause);
            Image card = Panel(page, "PauseCard", Surface, Vector2.zero, new Vector2(660, 730));
            Label(card.transform, "Eyebrow", "P O I N T P A S S", 19, new Vector2(0, 288), new Vector2(550, 40), Mint);
            Label(card.transform, "Title", "TAKE A BREATHER", 43, new Vector2(0, 206), new Vector2(580, 70), Paper, true);
            pauseDescription = Label(card.transform, "Description", "Your match is paused.", 25, new Vector2(0, 143), new Vector2(580, 45), Muted);
            Button resume = ActionButton(card.transform, "Resume", "BACK TO THE PITCH", new Vector2(0, 44), new Vector2(520, 84), true,
                () => ResumeRequested?.Invoke());
            ActionButton(card.transform, "Controls", "HOW TO PLAY", new Vector2(0, -59), new Vector2(520, 76), false,
                () => ShowControls(Screen.Pause));
            Button restart = ActionButton(card.transform, "Restart", "RESTART MATCH", new Vector2(0, -153), new Vector2(520, 76), false,
                () => RestartRequested?.Invoke());
            pauseRestartLabel = restart.GetComponentInChildren<Text>();
            ActionButton(card.transform, "MainMenu", "MAIN MENU", new Vector2(0, -247), new Vector2(520, 76), false,
                () => MainMenuRequested?.Invoke());
            ActionButton(page, "AudioSettings", "SOUND SETTINGS", new Vector2(515, 310), new Vector2(270, 54), false,
                () => ShowAudio(Screen.Pause));
            firstButtons.Add(Screen.Pause, resume);
            pauseReplay=ActionButton(card.transform,"ReplayLastGoal","WATCH LAST GOAL",new Vector2(0,-328),new Vector2(520,54),false,()=>ShowReplay(Screen.Pause));
            pauseReplay.gameObject.SetActive(false);
        }

        private void BuildResults()
        {
            RectTransform page = Page(Screen.Results);
            Image card = Panel(page, "ResultsCard", Surface, Vector2.zero, new Vector2(720, 730));
            Label(card.transform, "Eyebrow", "F U L L   T I M E", 20, new Vector2(0, 289), new Vector2(600, 40), Mint);
            resultTitle = Label(card.transform, "Title", "BLUE WINS", 46, new Vector2(0, 213), new Vector2(650, 75), Paper, true);
            resultScore = Label(card.transform, "Score", "0 : 0", 104, new Vector2(0, 76), new Vector2(620, 150), Paper, true);
            Label(card.transform, "Teams", "BLUE                               RED", 20, new Vector2(0, -18), new Vector2(600, 40), Muted);
            Bar(card.transform, "Divider", new Vector2(0, -68), new Vector2(560, 1), new Color(1, 1, 1, 0.1f));
            Button again = ActionButton(card.transform, "PlayAgain", "PLAY AGAIN", new Vector2(0, -144), new Vector2(550, 84), true,
                () => RestartRequested?.Invoke());
            ActionButton(card.transform, "MainMenu", "MAIN MENU", new Vector2(0, -247), new Vector2(550, 76), false,
                () => MainMenuRequested?.Invoke());
            firstButtons.Add(Screen.Results, again);
            resultsReplay=ActionButton(card.transform,"ReplayLastGoal","WATCH LAST GOAL",new Vector2(0,-62),new Vector2(400,48),false,()=>ShowReplay(Screen.Results));
            resultsReplay.gameObject.SetActive(false);
        }

        public void SetReplay(GoalReplayClip clip)
        {
            latestReplay=clip;
            pauseReplay.gameObject.SetActive(clip!=null && !practiceMode); resultsReplay.gameObject.SetActive(clip!=null);
        }

        public void SetPracticeMode(bool enabled)
        {
            practiceMode = enabled;
            if (pauseDescription != null) pauseDescription.text = enabled ? "Your practice is paused." : "Your match is paused.";
            if (pauseRestartLabel != null) pauseRestartLabel.text = enabled ? "RESTART PRACTICE" : "RESTART MATCH";
            if (pauseReplay != null) pauseReplay.gameObject.SetActive(latestReplay != null && !enabled);
        }

        private void ShowReplay(Screen origin)
        {
            if (latestReplay==null) return;
            replayOrigin=origin; replayView.Play(latestReplay); Show(Screen.Replay);
        }

        private void BuildReplay()
        {
            RectTransform page=Page(Screen.Replay);
            Panel(page,"ReplayCard",Surface,Vector2.zero,new Vector2(1270,800));
            replayView=page.gameObject.AddComponent<GoalReplayView>(); replayView.Build();
            Button back=ActionButton(page,"ReplayBack","BACK TO MATCH MENU",new Vector2(0,-347),new Vector2(450,62),false,()=>Show(replayOrigin));
            firstButtons.Add(Screen.Replay,back);
        }

        private void BuildControls()
        {
            RectTransform page = Page(Screen.Controls);
            Panel(page, "ControlsCard", Surface, Vector2.zero, new Vector2(1270, 800));
            Label(page, "Eyebrow", "M A K E   Y O U R   M O V E", 19, new Vector2(0, 326), new Vector2(1120, 40), Mint);
            Label(page, "Title", "HOW TO PLAY", 49, new Vector2(0, 252), new Vector2(1120, 80), Paper, true);
            BuildControlColumn(page, "Keyboard", -405, "KEYBOARD",
                "<color=#7DD3FC>WASD / ARROWS</color>\nMove and aim\n\n<color=#7DD3FC>J / K</color>\nHold pass / shoot, release to kick\n\n<color=#7DD3FC>Q / E WHILE SHOOTING</color>\nBend left / right\n\n<color=#7DD3FC>SHIFT / ESC / P</color>\nSprint / cancel / pause");
            BuildControlColumn(page, "Touch", 0, "TOUCH",
                "<color=#7DD3FC>LEFT JOYSTICK</color>\nMove and aim\n\n<color=#7DD3FC>PASS / SHOOT</color>\nHold to charge, release to kick\n\n<color=#7DD3FC>FLICK / CANCEL</color>\nFlick from Shoot to bend\nTap Cancel to stop the charge\n\n<color=#7DD3FC>SPRINT / PAUSE</color>\nRun faster / take a break");
            BuildControlColumn(page, "Gamepad", 405, "GAMEPAD",
                "<color=#7DD3FC>LEFT STICK</color>\nMove and aim\n\n<color=#7DD3FC>SOUTH / EAST</color>\nHold pass / shoot, release to kick\n\n<color=#7DD3FC>RIGHT STICK</color>\nBend a shot toward that side\n\n<color=#7DD3FC>RB / NORTH / START</color>\nSprint / cancel / pause");
            Button back = ActionButton(page, "Back", "BACK", new Vector2(-465, -319), new Vector2(260, 72), false,
                () => Show(controlsOrigin));
            ActionButton(page, "AudioSettings", "SOUND", new Vector2(-185, -319), new Vector2(260, 72), false,
                () => ShowAudio(Screen.Controls));

            firstButtons.Add(Screen.Controls, back);
        }

        private void ShowAudio(Screen origin)
        {
            audioOrigin = origin;
            Show(Screen.Audio);
        }

        private void BuildAudio()
        {
            RectTransform page = Page(Screen.Audio);
            Panel(page, "AudioCard", Surface, Vector2.zero, new Vector2(1000, 800));
            Label(page, "Title", "SOUND SETTINGS", 49, new Vector2(0, 290), new Vector2(850, 80), Paper, true);
            page.gameObject.AddComponent<AudioSettingsView>().Build();
            Button back = ActionButton(page, "AudioBack", "BACK", new Vector2(0, -319), new Vector2(400, 72), false,
                () => Show(audioOrigin));
            firstButtons.Add(Screen.Audio, back);
        }

        private void OnAccentChanged(Color previous, Color accent)
        {
            if (this == null) { MatchPalette.AccentChanged -= OnAccentChanged; return; }
            InterfaceTheme.Refresh(transform, previous, accent);

        }

        private void OnDestroy() => MatchPalette.AccentChanged -= OnAccentChanged;

        private static void BuildControlColumn(Transform parent, string name, float x, string title, string body)
        {
            Label(parent, name + "Title", title, 25, new Vector2(x, 163), new Vector2(345, 45), Paper, true);
            Label(parent, name + "Body", body, 22, new Vector2(x, -54), new Vector2(345, 380), Muted);
        }

        private Button ActionButton(Transform parent, string name, string title, Vector2 position, Vector2 size, bool primary, Action action)
        {
            Image image = Panel(parent, name, primary ? Mint : Color.Lerp(Surface,Paper,0.045f), position, size);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<MenuButton>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1, 1, 1, 1) : new Color(1.25f, 1.4f, 1.4f);
            colors.selectedColor = primary ? new Color(1.1f, 1.1f, 1.1f) : new Color(1.5f, 1.65f, 1.65f);
            colors.pressedColor = new Color(0.78f, 0.88f, 0.86f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            Label(image.transform, "Label", title, 25, Vector2.zero, size - new Vector2(36, 8), primary ? Ink : Paper, true);
            button.onClick.AddListener(() =>
            {
                // Reject stale callbacks from a page that was hidden during event dispatch.
                if (!IsOpen || !button.isActiveAndEnabled || !button.IsInteractable()) return;
                // A submit event and pointer click can arrive together; dispatch only once.
                if (lastActivationFrame == Time.frameCount) return;
                lastActivationFrame = Time.frameCount;
                action?.Invoke();
            });
            return button;
        }

        private void EnsureEventSystem()
        {
            EventSystem system = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (system == null) system = new GameObject("EventSystem").AddComponent<EventSystem>();
            if (system.GetComponent<InputSystemUIInputModule>() == null)
                system.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            // World kit shaders do not implement Canvas clipping or UI tint semantics.
            // The menu uses the source sprite with Unity's UI material so skin and fabric
            // shading remain faithful and the hero does not pick up stadium illumination.
            return image;
        }

        private static Image PictureAt(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            Image image = Picture(parent, name, sprite, color);
            Place(image.rectTransform, position, size);
            return image;
        }

        private static Image Panel(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            Image image = PictureAt(parent, name, Rounded(), color, position, size);
            image.type = Image.Type.Sliced;
            return image;
        }

        private static void Bar(Transform parent, string name, Vector2 position, Vector2 size, Color color) =>
            PictureAt(parent, name, SpriteLibrary.WhiteSquare(), color, position, size);

        private static Text Label(Transform parent, string name, string value, int size, Vector2 position, Vector2 bounds,
            Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = Rect(parent, name).gameObject.AddComponent<SharpText>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment; text.color = color; text.text = value;
            text.supportRichText = true; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Place(text.rectTransform, position, bounds);
            return text;
        }

        internal static Sprite Rounded()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Menu Rounded Panel";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 edge = new Vector2(Mathf.Abs(x + 0.5f - 64), Mathf.Abs(y + 0.5f - 64)) - Vector2.one * 30;
                    float distance = new Vector2(Mathf.Max(edge.x, 0), Mathf.Max(edge.y, 0)).magnitude
                        + Mathf.Min(Mathf.Max(edge.x, edge.y), 0) - 32;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance));
                }
            texture.SetPixels(pixels); texture.Apply();
            roundedSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), Vector2.one * 0.5f,
                200, 0, SpriteMeshType.FullRect, Vector4.one * 36);
            return roundedSprite;
        }

        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 512;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Menu Pitch Ring";
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 256);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(255 - radius) * Mathf.Clamp01(radius - 247));
                }
            texture.SetPixels(pixels); texture.Apply();
            ringSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), Vector2.one * 0.5f, 100);
            return ringSprite;
        }
    }
}
