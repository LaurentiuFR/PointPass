using System;
using System.Collections.Generic;
using PointPass.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Modal presentation for the offline prototype. MatchManager owns match state.</summary>
    public class MatchMenu : MonoBehaviour, ICancelHandler
    {
        public enum Screen { Main, Pause, Results, Controls }

        private static readonly Color Ink = new Color(0.025f, 0.05f, 0.07f, 1f);
        private static readonly Color Surface = new Color(0.055f, 0.095f, 0.12f, 1f);
        private static readonly Color Paper = new Color(0.94f, 0.97f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.59f, 0.7f, 0.75f, 1f);
        private static readonly Color Mint = new Color(0.52f, 0.94f, 0.76f, 1f);
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
        private int lastActivationFrame = -1;

        public event Action PlayRequested;
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
            menu.ApplyLayout();
            menu.Hide();
            return menu;
        }

        private void Update()
        {
            if (UnityEngine.Screen.safeArea != lastSafeArea || UnityEngine.Screen.width != lastSize.x
                || UnityEngine.Screen.height != lastSize.y) ApplyLayout();
        }

        private void OnEnable()
        {
            if (safeArea != null) ApplyLayout();
        }

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
            Vector2 available = safeArea.rect.size - Vector2.one * 64;
            float scale = Mathf.Clamp(Mathf.Min(available.x / 1320f, available.y / 820f), 0.2f, 1f);
            content.localScale = Vector3.one * scale;
        }

        public void ShowMain() => Show(Screen.Main);
        public void ShowPause() => Show(Screen.Pause);

        public void ShowResults(int blue, int red)
        {
            resultTitle.text = blue == red ? "HONOURS EVEN" : blue > red ? "BLUE WINS" : "RED WINS";
            resultScore.text = $"<color=#7CAEFF>{blue}</color> <color=#627780>:</color> <color=#FF8E92>{red}</color>";
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
            backdrop.color = screen == Screen.Main ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.96f);
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
            if (CurrentScreen == Screen.Controls) { Show(controlsOrigin); return true; }
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
            Label(page, "Eyebrow", "F O O T B A L L .   T O G E T H E R .", 20, new Vector2(-342, 330), new Vector2(580, 40), Mint, false, TextAnchor.MiddleLeft);
            Label(page, "Wordmark", "POINT<color=#85EFC2>PASS</color>", 84, new Vector2(-342, 235), new Vector2(600, 110), Paper, true, TextAnchor.MiddleLeft);
            Label(page, "Headline", "MAKE EVERY\nPASS COUNT.", 43, new Vector2(-342, 100), new Vector2(580, 125), Paper, true, TextAnchor.MiddleLeft);
            Label(page, "Description", "Find your touch. Hold to charge.\nMove into space and finish the chance.", 26,
                new Vector2(-342, -20), new Vector2(580, 90), Muted, false, TextAnchor.MiddleLeft);
            Button play = ActionButton(page, "PlayOffline", "PLAY OFFLINE    >", new Vector2(-372, -151), new Vector2(520, 88), true,
                () => PlayRequested?.Invoke());
            ActionButton(page, "HowToPlay", "HOW TO PLAY", new Vector2(-372, -257), new Vector2(520, 78), false,
                () => ShowControls(Screen.Main));
            firstButtons.Add(Screen.Main, play);
            Label(page, "Mode", "OFFLINE PROTOTYPE  /  SINGLE PLAYER", 18, new Vector2(-342, -343), new Vector2(580, 32), Muted, false, TextAnchor.MiddleLeft);

            Image card = Panel(page, "HeroCard", new Color(0.055f, 0.11f, 0.13f), new Vector2(357, 30), new Vector2(550, 680));
            Label(card.transform, "HeroEyebrow", "THE PITCH IS YOURS", 20, new Vector2(0, 281), new Vector2(470, 40), Mint);
            PictureAt(card.transform, "PitchCircle", Ring(), new Color(0.45f, 0.88f, 0.69f, 0.15f), new Vector2(0, 30), new Vector2(390, 390));
            Bar(card.transform, "HalfwayLine", new Vector2(0, 27), new Vector2(460, 2), new Color(0.45f, 0.88f, 0.69f, 0.12f));
            RectTransform hero = Rect(card.transform, "Hero");
            Place(hero, new Vector2(-27, 39), new Vector2(330, 330));
            hero.localRotation = Quaternion.Euler(0, 0, -28);
            PictureAt(hero, "LeftBoot", SpriteLibrary.Get("boot_polished"), Color.white, new Vector2(-38, 108), new Vector2(96, 114));
            PictureAt(hero, "RightBoot", SpriteLibrary.Get("boot_polished"), Color.white, new Vector2(38, 108), new Vector2(96, 114));
            PictureAt(hero, "Player", SpriteLibrary.Get("player_blue_polished"), Color.white, Vector2.zero, new Vector2(330, 330));
            PictureAt(card.transform, "Football", SpriteLibrary.Get("ball_polished"), Color.white, new Vector2(133, 151), new Vector2(125, 125));
            Bar(card.transform, "Accent", new Vector2(0, -190), new Vector2(40, 3), Mint);
            Label(card.transform, "Rules", "TWO HALVES.\nONE GOLDEN CHANCE.", 30, new Vector2(0, -245), new Vector2(460, 90), Paper, true);
            Label(page, "Footer", "MOVEMENT  /  PASSING  /  SHOOTING", 17, new Vector2(357, -363), new Vector2(550, 32), Muted);
        }

        private void BuildPause()
        {
            RectTransform page = Page(Screen.Pause);
            Image card = Panel(page, "PauseCard", Surface, Vector2.zero, new Vector2(660, 730));
            Label(card.transform, "Eyebrow", "P O I N T P A S S", 19, new Vector2(0, 288), new Vector2(550, 40), Mint);
            Label(card.transform, "Title", "TAKE A BREATHER", 43, new Vector2(0, 206), new Vector2(580, 70), Paper, true);
            Label(card.transform, "Description", "Your match is paused.", 25, new Vector2(0, 143), new Vector2(580, 45), Muted);
            Button resume = ActionButton(card.transform, "Resume", "BACK TO THE PITCH", new Vector2(0, 44), new Vector2(520, 84), true,
                () => ResumeRequested?.Invoke());
            ActionButton(card.transform, "Controls", "HOW TO PLAY", new Vector2(0, -59), new Vector2(520, 76), false,
                () => ShowControls(Screen.Pause));
            ActionButton(card.transform, "Restart", "RESTART MATCH", new Vector2(0, -153), new Vector2(520, 76), false,
                () => RestartRequested?.Invoke());
            ActionButton(card.transform, "MainMenu", "MAIN MENU", new Vector2(0, -247), new Vector2(520, 76), false,
                () => MainMenuRequested?.Invoke());
            firstButtons.Add(Screen.Pause, resume);
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
        }

        private void BuildControls()
        {
            RectTransform page = Page(Screen.Controls);
            Panel(page, "ControlsCard", Surface, Vector2.zero, new Vector2(1270, 800));
            Label(page, "Eyebrow", "M A K E   Y O U R   M O V E", 19, new Vector2(0, 326), new Vector2(1120, 40), Mint);
            Label(page, "Title", "HOW TO PLAY", 49, new Vector2(0, 252), new Vector2(1120, 80), Paper, true);
            BuildControlColumn(page, "Keyboard", -405, "KEYBOARD",
                "<color=#85EFC2>WASD / ARROWS</color>\nMove and aim\n\n<color=#85EFC2>J / K</color>\nHold pass / shoot, release to kick\n\n<color=#85EFC2>Q / E WHILE SHOOTING</color>\nBend left / right\n\n<color=#85EFC2>SHIFT / ESC / P</color>\nSprint / cancel / pause");
            BuildControlColumn(page, "Touch", 0, "TOUCH",
                "<color=#85EFC2>LEFT JOYSTICK</color>\nMove and aim\n\n<color=#85EFC2>PASS / SHOOT</color>\nHold to charge, release to kick\n\n<color=#85EFC2>FLICK FROM SHOOT</color>\nFlick sideways as you release\nto bend toward that side\n\n<color=#85EFC2>SPRINT / PAUSE</color>\nRun faster / take a break");
            BuildControlColumn(page, "Gamepad", 405, "GAMEPAD",
                "<color=#85EFC2>LEFT STICK</color>\nMove and aim\n\n<color=#85EFC2>SOUTH / EAST</color>\nHold pass / shoot, release to kick\n\n<color=#85EFC2>RIGHT STICK</color>\nBend a shot toward that side\n\n<color=#85EFC2>RB / NORTH / START</color>\nSprint / cancel / pause");
            Button back = ActionButton(page, "Back", "BACK", new Vector2(0, -319), new Vector2(400, 72), false,
                () => Show(controlsOrigin));
            firstButtons.Add(Screen.Controls, back);
        }

        private static void BuildControlColumn(Transform parent, string name, float x, string title, string body)
        {
            Label(parent, name + "Title", title, 25, new Vector2(x, 163), new Vector2(345, 45), Paper, true);
            Label(parent, name + "Body", body, 22, new Vector2(x, -54), new Vector2(345, 380), Muted);
        }

        private Button ActionButton(Transform parent, string name, string title, Vector2 position, Vector2 size, bool primary, Action action)
        {
            Image image = Panel(parent, name, primary ? Mint : new Color(0.09f, 0.145f, 0.175f), position, size);
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
            Text text = Rect(parent, name).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment; text.color = color; text.text = value;
            text.supportRichText = true; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Place(text.rectTransform, position, bounds);
            return text;
        }

        private static Sprite Rounded()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Menu Rounded Panel";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 edge = new Vector2(Mathf.Abs(x + 0.5f - 32), Mathf.Abs(y + 0.5f - 32)) - Vector2.one * 15;
                    float distance = new Vector2(Mathf.Max(edge.x, 0), Mathf.Max(edge.y, 0)).magnitude
                        + Mathf.Min(Mathf.Max(edge.x, edge.y), 0) - 16;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance));
                }
            texture.SetPixels(pixels); texture.Apply();
            roundedSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), Vector2.one * 0.5f,
                100, 0, SpriteMeshType.FullRect, Vector4.one * 18);
            return roundedSprite;
        }

        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Menu Pitch Ring";
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 64);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(63 - radius) * Mathf.Clamp01(radius - 60));
                }
            texture.SetPixels(pixels); texture.Apply();
            ringSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), Vector2.one * 0.5f, 100);
            return ringSprite;
        }
    }
}
