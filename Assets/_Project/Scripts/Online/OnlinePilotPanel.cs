using System;
using PointPass.Core;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Online
{
    /// <summary>Replaceable pilot UI adapter. Revised menus can call OnlinePilotClient.Open directly.</summary>
    public sealed class OnlinePilotPanel : MonoBehaviour
    {
        private OnlinePilotClient client;
        private GameObject entry, modal, leave;
        private UnityEngine.UI.InputField invitation, displayName;
        private UnityEngine.UI.Text status, statistics;
        private UnityEngine.UI.Button join;
        private Font font;
        private double nextStats;
        private bool showStats;

        public void Init(OnlinePilotClient client)
        {
            this.client = client;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Online Test Adapter", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.pixelPerfect = true; canvas.sortingOrder = 150;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var entryButton = Button(canvas.transform, "OnlineTest", "ONLINE TEST", new Vector2(0, 36), new Vector2(260, 64), client.Open);
            entry = entryButton.gameObject;
            var entryRect = (RectTransform)entry.transform; entryRect.anchorMin = entryRect.anchorMax = new Vector2(0.5f, 0); entryRect.pivot = new Vector2(0.5f, 0);
            var leaveButton = Button(canvas.transform, "LeaveOnline", "LEAVE ONLINE", new Vector2(-20, 20), new Vector2(245, 60), () => client.Leave());
            leave = leaveButton.gameObject;
            var leaveRect = (RectTransform)leave.transform; leaveRect.anchorMin = leaveRect.anchorMax = new Vector2(1, 0); leaveRect.pivot = new Vector2(1, 0);
            RectTransform blocker = Rect(canvas.transform, "OnlineModal", Vector2.zero, Vector2.zero);
            blocker.anchorMin = Vector2.zero; blocker.anchorMax = Vector2.one; blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            blocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.01f, 0.025f, 0.05f, 0.8f);
            modal = blocker.gameObject;
            RectTransform panel = Rect(blocker, "PrivatePilot", Vector2.zero, new Vector2(850, 600));
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = MatchPalette.Ink;
            Label(panel, "Title", "ONLINE TEST", 34, new Vector2(0, 240), new Vector2(760, 55));
            Label(panel, "Subtitle", "PRIVATE 1v1  ·  DEDICATED SERVER", 21, new Vector2(0, 185), new Vector2(760, 42));
            Label(panel, "NameCaption", "PLAYER NAME", 20, new Vector2(-210, 121), new Vector2(330, 32));
            displayName = Field(panel, "DisplayName", new Vector2(-210, 68), new Vector2(330, 64), "Guest", false, 24);
            Label(panel, "InviteCaption", "INVITATION (FIRST SIGN-IN)", 20, new Vector2(180, 121), new Vector2(410, 32));
            invitation = Field(panel, "Invitation", new Vector2(180, 68), new Vector2(410, 64), "", true, 64);
            join = Button(panel, "Join", "JOIN TEST ROOM", new Vector2(0, -38), new Vector2(760, 72), () => {
                string code = invitation.text; invitation.text = ""; client.Join(code, displayName.text);
            });
            status = Label(panel, "Status", "", 23, new Vector2(0, -141), new Vector2(760, 115));
            Button(panel, "Back", "BACK / LEAVE", new Vector2(0, -247), new Vector2(360, 60), () => {
                if (client.HasSession) client.Leave(); else client.ClosePanel();
            });
            var stats = Rect(canvas.transform, "OnlineStatistics", new Vector2(16, -16), new Vector2(520, 140));
            stats.anchorMin = stats.anchorMax = stats.pivot = new Vector2(0, 1);
            statistics = Label(stats, "Statistics", "", 21, Vector2.zero, new Vector2(510, 130));
            statistics.alignment = TextAnchor.UpperLeft;
            statistics.color = MatchPalette.Paper;
            modal.SetActive(false); leave.SetActive(false);
        }

        private void Update()
        {
            if (client == null) return;
            bool menu = client.Menu != null && client.Menu.IsOpen && client.Menu.CurrentScreen == MatchMenu.Screen.Main;
            entry.SetActive(menu && !client.PanelOpen && !client.HasSession);
            modal.SetActive(client.PanelOpen);
            leave.SetActive(client.HasSession && !client.PanelOpen);
            join.interactable = !client.Busy && !client.HasSession;
            invitation.interactable = displayName.interactable = !client.Busy && !client.HasSession;
            status.text = client.Status;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) showStats = !showStats;
            statistics.transform.parent.gameObject.SetActive(showStats && client.HasSession);
            if (Time.unscaledTimeAsDouble >= nextStats)
            {
                nextStats = Time.unscaledTimeAsDouble + 0.5;
                var stats = client.Diagnostics;
                statistics.text = $"{stats.Fps:0} FPS  ·  {stats.FrameMilliseconds:0.0} ms\nPing {stats.PingMilliseconds:0} ms  ·  Jitter {stats.JitterMilliseconds:0.0} ms\nRetry/drop estimate {stats.PacketLossPercent:0.0}%  ·  Snapshot gaps {stats.SnapshotLossPercent:0.0}%\nUp {stats.UploadKbps:0.0} / Down {stats.DownloadKbps:0.0} kbps";
            }
        }

        private RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private UnityEngine.UI.Text Label(Transform parent, string name, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var label = Rect(parent, name, position, dimensions).gameObject.AddComponent<SharpText>();
            label.font = font; label.fontSize = size; label.text = text; label.color = MatchPalette.Paper;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private UnityEngine.UI.Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size, Action action)
        {
            var rect = Rect(parent, name, position, size);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = MatchPalette.Surface;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => action()); Label(rect, "Label", text, 24, Vector2.zero, size - new Vector2(20, 8)); return button;
        }
        private UnityEngine.UI.InputField Field(Transform parent, string name, Vector2 position, Vector2 size, string value, bool secret, int limit)
        {
            var rect = Rect(parent, name, position, size);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = MatchPalette.Surface;
            var field = rect.gameObject.AddComponent<UnityEngine.UI.InputField>();
            field.textComponent = Label(rect, "Text", "", 24, Vector2.zero, size - new Vector2(24, 10));
            field.textComponent.alignment = TextAnchor.MiddleLeft;
            field.contentType = secret ? UnityEngine.UI.InputField.ContentType.Password : UnityEngine.UI.InputField.ContentType.Standard;
            field.characterLimit = limit; field.text = value;
            return field;
        }
    }
}
