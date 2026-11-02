using PointPass.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Builds and drives the on-screen HUD: scoreboard, clock, joystick and big messages.</summary>
    public class MatchHud : MonoBehaviour
    {
        private const string BlueHex = "#6C9BFF";
        private const string RedHex = "#FF7B7B";
        private const string DashHex = "#7A859C";

        private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
        private static readonly Color BannerColor = new Color(0.03f, 0.05f, 0.08f, 0.72f);
        private static readonly Color ClockNormal = new Color(0.92f, 0.95f, 1f);
        private static readonly Color ClockUrgent = new Color(1f, 0.42f, 0.42f);

        private Sprite panelSprite;
        private Text scoreText;
        private Text clockText;
        private Text messageText;
        private RectTransform banner;

        public VirtualJoystick Joystick { get; private set; }

        public static string ScoreLine(int blue, int red)
        {
            return $"<color={BlueHex}>BLUE</color>  {blue} - {red}  <color={RedHex}>RED</color>";
        }

        public static MatchHud Create()
        {
            GameObject canvasObject = new GameObject("HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            MatchHud hud = canvasObject.AddComponent<MatchHud>();

            EnsureEventSystem();
            hud.panelSprite = SpriteLibrary.GetSliced("ui_panel", 44f);
            hud.BuildScoreboard();
            hud.BuildClock();
            hud.BuildJoystick();
            hud.BuildBanner();
            return hud;
        }

        public void SetScore(int blue, int red)
        {
            scoreText.text = $"{blue}<color={DashHex}>  -  </color>{red}";
        }

        public void SetClock(string label, float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            clockText.text = $"{label}   {total / 60}:{total % 60:00}";
            clockText.color = total <= 10 ? ClockUrgent : ClockNormal;
        }

        public void SetClockText(string text)
        {
            clockText.text = text;
            clockText.color = ClockNormal;
        }

        public void ShowMessage(string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            banner.gameObject.SetActive(visible);
            if (!visible) return;

            bool multiLine = text.Contains("\n");
            messageText.text = text;
            messageText.fontSize = multiLine ? 62 : 110;
            banner.sizeDelta = new Vector2(1200f, multiLine ? 380f : 260f);
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                oldModule.enabled = false;
                Destroy(oldModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                InputSystemUIInputModule module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        // ---- Scoreboard: dark panel, a coloured block per team, big score in the middle ----
        private void BuildScoreboard()
        {
            RectTransform root = MakeRect(transform, "Scoreboard");
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -24f);
            root.sizeDelta = new Vector2(840f, 112f);

            Image background = MakePanel(root, "Background", PanelColor);
            Stretch(background.rectTransform);
            Shadow shadow = background.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            shadow.effectDistance = new Vector2(0f, -6f);

            BuildTeamBlock(root, "BlueBlock", "BLUE", MatchConfig.BlueTeam, true);
            BuildTeamBlock(root, "RedBlock", "RED", MatchConfig.RedTeam, false);

            scoreText = MakeText(root, "Score", 78, Vector2.zero, Vector2.one);
            scoreText.rectTransform.offsetMin = new Vector2(270f, 0f);
            scoreText.rectTransform.offsetMax = new Vector2(-270f, 0f);
            SetScore(0, 0);
        }

        private void BuildTeamBlock(RectTransform parent, string objectName, string teamName, Color color, bool left)
        {
            Image block = MakePanel(parent, objectName, color);
            RectTransform rect = block.rectTransform;
            float anchorX = left ? 0f : 1f;
            rect.anchorMin = new Vector2(anchorX, 0.5f);
            rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = new Vector2(anchorX, 0.5f);
            rect.anchoredPosition = new Vector2(left ? 8f : -8f, 0f);
            rect.sizeDelta = new Vector2(250f, 96f);

            Text label = MakeText(rect, "Name", 46, Vector2.zero, Vector2.one);
            label.text = teamName;
        }

        private void BuildClock()
        {
            Image pill = MakePanel(transform, "ClockPill", PanelColor);
            RectTransform rect = pill.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -144f);
            rect.sizeDelta = new Vector2(330f, 58f);

            clockText = MakeText(rect, "Clock", 32, Vector2.zero, Vector2.one);
            SetClock("1ST HALF", MatchConfig.HalfSeconds);
        }

        private void BuildJoystick()
        {
            Image baseImage = MakeImage(transform, "JoystickBase", SpriteLibrary.Get("joystick_base", true), Color.white);
            RectTransform baseRect = baseImage.rectTransform;
            baseRect.anchorMin = Vector2.zero;
            baseRect.anchorMax = Vector2.zero;
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = new Vector2(300f, 300f);
            baseRect.sizeDelta = new Vector2(340f, 340f);

            Image handleImage = MakeImage(baseImage.transform, "JoystickHandle", SpriteLibrary.Get("joystick_handle", true), Color.white);
            handleImage.raycastTarget = false;
            RectTransform handleRect = handleImage.rectTransform;
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(140f, 140f);

            Joystick = baseImage.gameObject.AddComponent<VirtualJoystick>();
            Joystick.Init(baseRect, handleRect);
        }

        // ---- Big centre banner for GOAL!, HALF TIME, FULL TIME ----
        private void BuildBanner()
        {
            Image panel = MakePanel(transform, "Banner", BannerColor);
            banner = panel.rectTransform;
            banner.anchorMin = new Vector2(0.5f, 0.5f);
            banner.anchorMax = new Vector2(0.5f, 0.5f);
            banner.pivot = new Vector2(0.5f, 0.5f);
            banner.anchoredPosition = new Vector2(0f, 40f);
            banner.sizeDelta = new Vector2(1200f, 260f);

            messageText = MakeText(banner, "Message", 110, Vector2.zero, Vector2.one);
            Outline outline = messageText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);

            banner.gameObject.SetActive(false);
        }

        // ---- Small helpers ----
        private Image MakePanel(Transform parent, string objectName, Color color)
        {
            Image image = MakeImage(parent, objectName, panelSprite, color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform MakeRect(Transform parent, string objectName)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image MakeImage(Transform parent, string objectName, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Text MakeText(Transform parent, string objectName, int size, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
