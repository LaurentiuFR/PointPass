using PointPass.Core;
using PointPass.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PointPass.Networking
{
    /// <summary>Temporary connection controls, deliberately separate from the main menu.</summary>
    public sealed class NetworkDevelopmentPanel : MonoBehaviour
    {
        private NetworkSession session;
        private InputField address;
        private InputField port;
        private Text status;
        private Button host;
        private Button join;
        private Button disconnect;
        private Button restart;
        private Font font;
        private string validationError;
        private RectTransform diagnosticsRoot;
        private UnityEngine.UI.Text diagnosticsText;
        private float nextDiagnostics;

        public void Init(NetworkSession value)
        {
            session = value;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            GameObject canvasObject = new GameObject("Network Development UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            RectTransform panel = Rect(canvas.transform, "Connection Panel", new Vector2(450, 290), new Vector2(-18, -18));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-18, -18);
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.09f, 0.96f);
            Label(panel, "MULTIPLAYER · DEVELOPMENT", new Vector2(20, -15), new Vector2(410, 35), 24);
            address = Field(panel, "IPv4 Address", new Vector2(20, -65), 268, "127.0.0.1");
            port = Field(panel, "Port", new Vector2(300, -65), 128, "7777");
            port.onValueChanged.AddListener(_ => validationError = null);
            host = Action(panel, "Host", new Vector2(20, -120), 125, () => { if (ReadPort(out ushort p)) session.Host(p); });
            join = Action(panel, "Join", new Vector2(156, -120), 125, () => { if (ReadPort(out ushort p)) session.Join(address.text.Trim(), p); });
            disconnect = Action(panel, "Disconnect", new Vector2(292, -120), 136, () => session.Disconnect());
            status = Label(panel, "", new Vector2(20, -175), new Vector2(410, 55), 21);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            restart = Action(panel, "Restart match (host)", new Vector2(20, -239), 408, () => session.RestartMatch());
            diagnosticsRoot = Rect(canvas.transform, "Network Statistics", new Vector2(540, 170), new Vector2(18, -18));
            UnityEngine.UI.Image background = diagnosticsRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.025f, 0.045f, 0.09f, 0.91f);
            background.raycastTarget = false;
            Label(diagnosticsRoot, "PERFORMANCE & NETWORK  ·  F3 TO HIDE", new Vector2(16, -10), new Vector2(508, 28), 20);
            diagnosticsText = Label(diagnosticsRoot, "Sampling…", new Vector2(16, -42), new Vector2(508, 120), 21);
        }

        private bool ReadPort(out ushort value)
        {
            if (ushort.TryParse(port.text, out value) && value > 0) return true;
            validationError = "Port must be a number from 1 to 65535.";
            return false;
        }

        private void Update()
        {
            if (session == null || status == null) return;
            string message = validationError ?? session.Status;
            if (status.text != message) status.text = message;
            bool idle = !session.HasSession && !session.Busy;
            host.interactable = join.interactable = address.interactable = port.interactable = idle;
            disconnect.interactable = session.HasSession && !session.Busy;
            restart.interactable = session.IsHost && session.World != null;
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
                diagnosticsRoot.gameObject.SetActive(!diagnosticsRoot.gameObject.activeSelf);
            if (Time.unscaledTime >= nextDiagnostics)
            {
                nextDiagnostics = Time.unscaledTime + 0.5f;
                NetworkDiagnostics stats = session.Diagnostics;
                string performance = $"{stats.Fps:0} FPS  ·  {stats.FrameMilliseconds:0.0} ms/frame";
                string latency = stats.HasPeer && stats.HasPing
                    ? $"Ping {stats.PingMilliseconds:0} ms  ·  Jitter {stats.JitterMilliseconds:0.0} ms"
                    : "Ping —  ·  Jitter —";
                string traffic = stats.HasPeer
                    ? $"Loss est. {stats.PacketLossPercent:0.0}%  ·  ↑{stats.UploadKbps:0.0} ↓{stats.DownloadKbps:0.0} kb/s"
                    : "Loss est. —  ·  Upload / download —";
                string state = !session.IsHost && stats.HasSnapshot
                    ? $"State {stats.SnapshotHz:0} Hz  ·  Gaps {stats.SnapshotLossPercent:0.0}%  ·  Age {stats.SnapshotAgeMilliseconds:0} ms"
                    : session.World != null ? "Authoritative host  ·  State target 30 Hz" : "Waiting for a match";
                diagnosticsText.text = performance + "\n" + latency + "\n" + traffic + "\n" + state;
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            SharpText label = Rect(parent, text.Length == 0 ? "Status" : text, size, position).gameObject.AddComponent<SharpText>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = MatchPalette.Paper;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private InputField Field(Transform parent, string name, Vector2 position, float width, string initial)
        {
            RectTransform root = Rect(parent, name, new Vector2(width, 44), position);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.15f, 0.23f);
            InputField field = root.gameObject.AddComponent<InputField>();
            Text text = Label(root, "", new Vector2(10, -6), new Vector2(width - 20, 34), 24);
            text.supportRichText = false;
            field.textComponent = text;
            field.targetGraphic = image;
            field.characterLimit = 15;
            field.text = initial;
            return field;
        }

        private Button Action(Transform parent, string name, Vector2 position, float width, UnityEngine.Events.UnityAction callback)
        {
            RectTransform root = Rect(parent, name, new Vector2(width, 38), position);
            Image image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.13f, 0.22f, 0.32f);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text label = Label(root, name, new Vector2(4, -3), new Vector2(width - 8, 32), 21);
            label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(callback);
            return button;
        }
    }
}
