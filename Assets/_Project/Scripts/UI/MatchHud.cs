using System;
using PointPass.Core;
using PointPass.Players;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Runtime match presentation. Gameplay and input remain in their own components.</summary>
    public class MatchHud : MonoBehaviour
    {
        private static Color Ink => MatchPalette.Ink;
        private static Color Paper => MatchPalette.Paper;
        private static Color Muted => MatchPalette.Muted;
        private static Color Mint => MatchPalette.Accent;
        private static Color Gold => MatchPalette.Gold;
        private static Sprite ringSprite;
        private static Sprite roundedSprite;

        private Sprite panelSprite;
        private Text scoreText;
        private Text clockText;
        private Text phaseText;
        private Text messageText;
        private Text messageDetail;
        private RectTransform messageEyebrow;
        private RectTransform messageAccent;
        private RectTransform banner;
        private CanvasGroup bannerGroup;
        private bool bannerVisible;
        private RectTransform scoreRoot;
        private float scorePulse;
        private int lastBlue;
        private int lastRed;
        private Image chargeFill;
        private Image passCharge;
        private Image shotCharge;
        private Text chargeLabel;
        private Text bendLabel;
        private bool practiceMode;
        private RectTransform safeAreaRoot;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private CanvasGroup passAvailability;
        private CanvasGroup shotAvailability;
        private CanvasGroup sprintAvailability;
        private Text controlHint;
        private float kickAvailabilityTarget = 0.3f;
        private float sprintAvailabilityTarget = 0.3f;
        private bool playerStateInitialized;
        private bool playerActive;
        private bool playerHasBall;
        private bool playerUsingGamepad;
        private Image replayCue;
        private int displayedSeconds = -1;
        private string displayedPhase;

        public VirtualJoystick Joystick { get; private set; }
        public HoldActionButton PassButton { get; private set; }
        public HoldActionButton ShootButton { get; private set; }
        public HoldActionButton SprintButton { get; private set; }
        public Button CancelChargeButton { get; private set; }
        public event Action PauseRequested;
        public event Action CancelChargeRequested;

        public static string ScoreLine(int blue, int red) =>
            $"<color=#4085DB>BLUE</color>  {blue} - {red}  <color=#E0594A>RED</color>";

        public static MatchHud Create()
        {
            GameObject go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().pixelPerfect = true;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Fit the shorter landscape dimension, retaining comfortable thumb targets on wide phones.
            scaler.matchWidthOrHeight = 1f;
            MatchHud hud = go.AddComponent<MatchHud>();
            hud.safeAreaRoot = Rect(go.transform, "SafeArea");
            hud.ApplySafeArea();
            EnsureEventSystem();
            hud.panelSprite = RoundedPanel();
            hud.BuildScoreboard();
            hud.BuildJoystick();
            hud.BuildActions();
            hud.BuildControlHint();
            hud.BuildBanner();
            hud.BuildPauseButton();
            hud.replayCue=hud.Panel(hud.safeAreaRoot,"GoalReplayCue",Ink,new Vector2(0,-185),new Vector2(490,54));
            hud.replayCue.rectTransform.anchorMin=hud.replayCue.rectTransform.anchorMax=new Vector2(0.5f,1);
            Label(hud.replayCue.transform,"ReplayStatus","",19,Vector2.zero,new Vector2(470,46),Mint,true);
            hud.replayCue.gameObject.SetActive(false);
            hud.SetPlayerState(false, false, false);
            MatchPalette.AccentChanged += hud.OnAccentChanged;
            return hud;
        }

        private void OnAccentChanged(Color previous, Color accent)
        {
            // Editor fixtures can destroy an inactive component without invoking OnDestroy.
            if (this == null) { MatchPalette.AccentChanged -= OnAccentChanged; return; }
            InterfaceTheme.Refresh(transform, previous, accent);
        }
        private void OnDestroy() => MatchPalette.AccentChanged -= OnAccentChanged;

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x
                || Screen.height != lastScreenSize.y) ApplySafeArea();

            float feedbackStep = Time.unscaledDeltaTime * 5f;
            if (passAvailability != null) passAvailability.alpha = Mathf.MoveTowards(passAvailability.alpha, kickAvailabilityTarget, feedbackStep);
            if (shotAvailability != null) shotAvailability.alpha = Mathf.MoveTowards(shotAvailability.alpha, kickAvailabilityTarget, feedbackStep);
            if (sprintAvailability != null) sprintAvailability.alpha = Mathf.MoveTowards(sprintAvailability.alpha, sprintAvailabilityTarget, feedbackStep);

            scorePulse = Mathf.MoveTowards(scorePulse, 0f, Time.unscaledDeltaTime * 3f);
            if (scoreRoot != null) scoreRoot.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(scorePulse * Mathf.PI));
            if (bannerGroup == null || !banner.gameObject.activeSelf) return;
            bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, bannerVisible ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            banner.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, bannerGroup.alpha);
            if (!bannerVisible && bannerGroup.alpha <= 0f) banner.gameObject.SetActive(false);
        }

        private void ApplySafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            safeAreaRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeAreaRoot.offsetMin = safeAreaRoot.offsetMax = Vector2.zero;
        }

        public void SetScore(int blue, int red)
        {
            if (blue != lastBlue || red != lastRed) scorePulse = 1f;
            lastBlue = blue; lastRed = red;
            scoreText.text = $"{blue} <color=#637780>:</color> {red}";
        }

        public void SetClock(string label, float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0, seconds));
            if (total == displayedSeconds && label == displayedPhase) return;
            displayedSeconds = total; displayedPhase = label;
            phaseText.text = label;
            clockText.text = $"{total / 60}:{total % 60:00}";
            clockText.color = total <= 10 ? Gold : Paper;
        }

        public void SetClockText(string text)
        {
            displayedSeconds = -1; displayedPhase = null;
            phaseText.text = text;
            clockText.text = "--:--";
            clockText.color = Muted;
        }

        public void ShowMessage(string text)
        {
            messageAccent.GetComponent<Image>().color = Mint;
            Text eyebrow=messageEyebrow.GetComponent<Text>();
            eyebrow.text="P O I N T P A S S"; eyebrow.color=Mint;
            bannerVisible = !string.IsNullOrEmpty(text);
            if (!bannerVisible) return;
            if (!banner.gameObject.activeSelf) bannerGroup.alpha = 0f;
            banner.gameObject.SetActive(true);
            int lineBreak = text.IndexOf('\n');
            messageText.text = lineBreak < 0 ? text : text.Substring(0, lineBreak);
            messageDetail.text = lineBreak < 0 ? "" : text.Substring(lineBreak + 1);
            banner.sizeDelta = new Vector2(660, lineBreak < 0 ? 164 : 270);
            messageEyebrow.anchoredPosition = new Vector2(0, lineBreak < 0 ? 48 : 94);
            messageAccent.anchoredPosition = new Vector2(0, lineBreak < 0 ? -56 : 119);
            messageText.rectTransform.anchoredPosition = new Vector2(0, lineBreak < 0 ? 0 : 35);
            messageDetail.rectTransform.anchoredPosition = new Vector2(0, -60);
        }

        public void ShowGoalMessage(Team team,bool ownGoal)
        {
            string name=team==Team.Blue ? "BLUE":"RED";
            ShowMessage("GOAL!\n"+name+(ownGoal ? " SCORE  /  OWN GOAL":" ON THE SCORESHEET"));
            messageAccent.GetComponent<Image>().color=MatchPalette.TeamColor(team);
            Text eyebrow=messageEyebrow.GetComponent<Text>();
            eyebrow.text=name+"  /  GOAL MOMENT";
            eyebrow.color=Color.Lerp(MatchPalette.TeamColor(team),Paper,0.35f);
        }

        public void ShowGoalReplayCue(bool saved)
        {
            if (practiceMode) return;
            replayCue.gameObject.SetActive(true);
            replayCue.GetComponentInChildren<Text>().text = saved ? "GOAL REPLAY SAVED  /  WATCH FROM PAUSE" : "REPLAY READY  /  WATCH FROM PAUSE";
        }
        public void ClearGoalReplayCue() { if (replayCue!=null) replayCue.gameObject.SetActive(false); }

        /// <summary>Practice owns its own heading; the common movement and action controls remain available.</summary>
        public void SetPracticeMode(bool enabled)
        {
            practiceMode = enabled;
            if (scoreRoot != null) scoreRoot.gameObject.SetActive(!enabled);
            if (phaseText != null) phaseText.transform.parent.gameObject.SetActive(!enabled);
            if (enabled) ClearGoalReplayCue();
        }

        private void BuildScoreboard()
        {
            scoreRoot = Rect(safeAreaRoot, "Scoreboard");
            Place(scoreRoot, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(600, 94), new Vector2(0.5f, 1));
            Panel(scoreRoot, "Background", Ink, Vector2.zero, new Vector2(600, 94));
            // Team accents are deliberately small: colour identifies the sides without filling the HUD.
            Bar(scoreRoot, "BlueAccent", new Vector2(-284, 0), new Vector2(4, 38), MatchPalette.Blue);
            Bar(scoreRoot, "RedAccent", new Vector2(284, 0), new Vector2(4, 38), MatchPalette.Red);
            Label(scoreRoot, "BlueName", "BLUE", 28, new Vector2(-194, 0), new Vector2(130, 42), Paper);
            Label(scoreRoot, "RedName", "RED", 28, new Vector2(194, 0), new Vector2(130, 42), Paper);
            scoreText = Label(scoreRoot, "Score", "0 : 0", 52, Vector2.zero, new Vector2(210, 74), Paper, true);
            Image divider = Bar(scoreRoot, "LeftDivider", new Vector2(-110, 0), new Vector2(1, 34), new Color(1, 1, 1, 0.12f));
            Bar(scoreRoot, "RightDivider", new Vector2(110, 0), new Vector2(1, 34), divider.color);

            Image clockPanel = Panel(safeAreaRoot, "ClockPill", Ink, Vector2.zero, new Vector2(300, 46));
            Place(clockPanel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(300, 46), new Vector2(0.5f, 1));
            phaseText = Label(clockPanel.transform, "Phase", "1ST HALF", 20, new Vector2(-54, 0), new Vector2(156, 36), Muted);
            Bar(clockPanel.transform, "Divider", new Vector2(36, 0), new Vector2(1, 20), new Color(1, 1, 1, 0.15f));
            clockText = Label(clockPanel.transform, "Time", "1:30", 25, new Vector2(92, 0), new Vector2(92, 36), Paper, true);
        }

        private void BuildPauseButton()
        {
            Image face = Panel(safeAreaRoot, "PauseButton", Ink, Vector2.zero, new Vector2(72, 64));
            Place(face.rectTransform, new Vector2(1, 1), new Vector2(-62, -58), new Vector2(72, 64));
            face.raycastTarget = true;
            Bar(face.transform, "LeftBar", new Vector2(-8, 0), new Vector2(5, 24), Paper);
            Bar(face.transform, "RightBar", new Vector2(8, 0), new Vector2(5, 24), Paper);
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(() => PauseRequested?.Invoke());
            // The HUD pause button is pointer/shortcut operated; menu buttons own pad focus.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void BuildJoystick()
        {
            Image baseImage = Circle(safeAreaRoot, "JoystickBase", new Color(Ink.r, Ink.g, Ink.b, 0.28f), Vector2.zero, 264);
            Place(baseImage.rectTransform, Vector2.zero, new Vector2(206, 194), new Vector2(264, 264));
            Image rim = Image(baseImage.transform, "Rim", Ring(), new Color(1, 1, 1, 0.26f));
            Stretch(rim.rectTransform);
            Circle(baseImage.transform, "InnerGuide", new Color(1, 1, 1, 0.04f), Vector2.zero, 178);
            Image handle = Circle(baseImage.transform, "JoystickHandle", new Color(Paper.r, Paper.g, Paper.b, 0.75f), Vector2.zero, 96);
            Circle(handle.transform, "Centre", new Color(1, 1, 1, 0.35f), Vector2.zero, 14);
            baseImage.raycastTarget = true;
            Joystick = baseImage.gameObject.AddComponent<VirtualJoystick>();
            Joystick.Init(baseImage.rectTransform, handle.rectTransform);
        }

        private void BuildActions()
        {
            PassButton = BuildAction("Pass", new Vector2(-338, 170), 168, Mint, 0, out passCharge);
            ShootButton = BuildAction("Shoot", new Vector2(-158, 314), 184, Mint, 1, out shotCharge);
            SprintButton = BuildAction("Sprint", new Vector2(-132, 108), 140, Paper, 2, out _);
            // Only opacity changes. Touch ownership and release/cancel events remain intact.
            passAvailability = PassButton.gameObject.AddComponent<CanvasGroup>();
            shotAvailability = ShootButton.gameObject.AddComponent<CanvasGroup>();
            sprintAvailability = SprintButton.gameObject.AddComponent<CanvasGroup>();
            Image track = Panel(safeAreaRoot, "Charge", Ink, Vector2.zero, new Vector2(324, 56));
            Place(track.rectTransform, new Vector2(1, 0), new Vector2(-68, 444), new Vector2(324, 56), new Vector2(1, 0));
            chargeLabel = Label(track.transform, "Label", "PASS", 20, new Vector2(0, 6), new Vector2(280, 30), Paper);
            Bar(track.transform, "Track", new Vector2(0, -15), new Vector2(276, 3), new Color(1, 1, 1, 0.12f));
            chargeFill = Bar(track.transform, "Fill", new Vector2(0, -15), new Vector2(276, 3), Mint);
            chargeFill.type = UnityEngine.UI.Image.Type.Filled;
            chargeFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            chargeFill.fillOrigin = 0;
            Image cancel = Panel(safeAreaRoot, "CancelCharge", Ink, Vector2.zero, new Vector2(132, 56));
            Place(cancel.rectTransform, new Vector2(1, 0), new Vector2(-462, 444), new Vector2(132, 56));
            cancel.raycastTarget = true;
            CancelChargeButton = cancel.gameObject.AddComponent<Button>();
            CancelChargeButton.targetGraphic = cancel;
            CancelChargeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(cancel.transform, "Label", "CANCEL", 20, Vector2.zero, new Vector2(124, 42), Paper, true);
            CancelChargeButton.onClick.AddListener(() =>
            {
                if (isActiveAndEnabled && CancelChargeButton.gameObject.activeInHierarchy) CancelChargeRequested?.Invoke();
            });
            bendLabel = Label(ShootButton.transform, "BendStrength", "STRAIGHT", 17,
                new Vector2(0, -72), new Vector2(146, 24), Mint, true);
            SetCharge(PlayerKicker.KickType.None, 0);
        }

        private void BuildControlHint()
        {
            Image plate = Panel(safeAreaRoot, "ControlHint", new Color(Ink.r, Ink.g, Ink.b, 0.72f), Vector2.zero, new Vector2(660, 38));
            Place(plate.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(660, 38));
            controlHint = Label(plate.transform, "Hint", "", 19, Vector2.zero, new Vector2(630, 32), Muted);
        }

        /// <summary>Presentation only: explains possession and shows which actions are useful.</summary>
        public void SetPlayerState(bool active, bool hasBall, bool usingGamepad)
        {
            if (playerStateInitialized && playerActive == active && playerHasBall == hasBall
                && playerUsingGamepad == usingGamepad) return;
            playerStateInitialized = true;
            playerActive = active;
            playerHasBall = hasBall;
            playerUsingGamepad = usingGamepad;
            kickAvailabilityTarget = !active ? 0.25f : hasBall ? 1f : 0.42f;
            sprintAvailabilityTarget = active ? 1f : 0.25f;
            controlHint.transform.parent.gameObject.SetActive(active);
            if (!active) SetCharge(PlayerKicker.KickType.None, 0);
            if (!active) return;

            controlHint.color = hasBall ? Mint : Muted;
            if (Application.isMobilePlatform)
                controlHint.text = hasBall ? "HOLD TO CHARGE  /  FLICK FROM SHOOT TO BEND" : "COLLECT THE BALL  /  SPRINT TO GET THERE";
            else if (usingGamepad)
                controlHint.text = hasBall ? "HOLD SOUTH: PASS  /  EAST: SHOOT  /  RIGHT STICK: BEND" : "COLLECT THE BALL  /  RB: SPRINT";
            else
                controlHint.text = hasBall ? "HOLD J: PASS  /  K: SHOOT  /  Q-E: BEND  /  ESC: CANCEL" : "COLLECT THE BALL  /  SHIFT: SPRINT";
        }

        private HoldActionButton BuildAction(string name, Vector2 position, float diameter, Color accent, int icon, out Image charge)
        {
            Image face = Circle(safeAreaRoot, name + "Button", new Color(Ink.r, Ink.g, Ink.b, 0.72f), Vector2.zero, diameter);
            Place(face.rectTransform, new Vector2(1, 0), position, Vector2.one * diameter);
            Image rim = Image(face.transform, "Rim", Ring(), new Color(accent.r, accent.g, accent.b, 0.5f));
            Stretch(rim.rectTransform);
            charge = Image(face.transform, "PowerRing", Ring(), accent);
            Stretch(charge.rectTransform);
            charge.type = UnityEngine.UI.Image.Type.Filled;
            charge.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            charge.fillOrigin = 2;
            charge.fillAmount = 0;
            Label(face.transform, "Label", name.ToUpperInvariant(), 23, new Vector2(0, -diameter * 0.22f), new Vector2(diameter, 34), Paper, true);
            RectTransform symbol = Rect(face.transform, "Symbol");
            Place(symbol, Vector2.one * 0.5f, new Vector2(0, 17), new Vector2(64, 54));
            if (icon == 0)
            {
                Line(symbol, new Vector2(-22, -11), new Vector2(23, 13), 4, accent);
                Line(symbol, new Vector2(23, 13), new Vector2(5, 16), 4, accent);
                Line(symbol, new Vector2(23, 13), new Vector2(16, -5), 4, accent);
                Line(symbol, new Vector2(-25, -22), new Vector2(-7, -13), 3, accent * new Color(1, 1, 1, 0.5f));
            }
            else if (icon == 1)
            {
                Image target = Image(symbol, "Target", Ring(), accent);
                Place(target.rectTransform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 43);
                Circle(symbol, "Centre", accent, Vector2.zero, 8);
                Line(symbol, new Vector2(-29, 0), new Vector2(-17, 0), 3, accent);
                Line(symbol, new Vector2(17, 0), new Vector2(29, 0), 3, accent);
                Line(symbol, new Vector2(0, -29), new Vector2(0, -17), 3, accent);
                Line(symbol, new Vector2(0, 17), new Vector2(0, 29), 3, accent);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    float y = i * 18 - 14;
                    Line(symbol, new Vector2(-17, y), new Vector2(0, y + 13), 4, accent);
                    Line(symbol, new Vector2(0, y + 13), new Vector2(17, y), 4, accent);
                }
            }
            face.raycastTarget = true;
            return face.gameObject.AddComponent<HoldActionButton>();
        }

        public void SetCharge(PlayerKicker.KickType type, float charge, float curve = 0f)
        {
            bool shooting = type == PlayerKicker.KickType.Shot;
            bool charging = type != PlayerKicker.KickType.None;
            if (CancelChargeButton != null) CancelChargeButton.gameObject.SetActive(charging);
            if (bendLabel != null)
            {
                bendLabel.gameObject.SetActive(shooting);
                float strength = float.IsNaN(curve) || float.IsInfinity(curve) ? 0 : Mathf.Clamp(curve, -1, 1);
                bendLabel.text = Mathf.Abs(strength) < 0.1f ? "STRAIGHT"
                    : $"{(strength > 0 ? "LEFT" : "RIGHT")} {Mathf.RoundToInt(Mathf.Abs(strength) * 100)}%";
                bendLabel.color = Mint;
            }
            if (!charging && !chargeFill.transform.parent.gameObject.activeSelf) return;
            chargeFill.transform.parent.gameObject.SetActive(charging);
            charge = Mathf.Clamp01(charge);
            chargeFill.fillAmount = charge;
            chargeFill.color = Mint;
            passCharge.fillAmount = type == PlayerKicker.KickType.Pass ? charge : 0;
            shotCharge.fillAmount = shooting ? charge : 0;
            if (shooting && Mathf.Abs(curve) > 0.1f)
                chargeLabel.text = $"SHOT {Mathf.RoundToInt(charge * 100):00}%  /  BEND {(curve > 0 ? "LEFT" : "RIGHT")}";
            else chargeLabel.text = charge >= 1 ? "FULL POWER  /  RELEASE" : $"{(shooting ? "SHOT" : "PASS")}  /  {Mathf.RoundToInt(charge * 100):00}%";
        }

        private void OnDisable()
        {
            if (chargeFill != null) SetCharge(PlayerKicker.KickType.None, 0);
        }

        private void BuildBanner()
        {
            Image panel = Panel(safeAreaRoot, "Banner", Ink, new Vector2(0, 26), new Vector2(660, 164));
            banner = panel.rectTransform;
            bannerGroup = panel.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            messageEyebrow = Label(banner, "Eyebrow", "P O I N T P A S S", 17, new Vector2(0, 48), new Vector2(500, 30), Mint).rectTransform;
            messageAccent = Bar(banner, "Accent", new Vector2(0, -56), new Vector2(38, 3), Mint).rectTransform;
            messageText = Label(banner, "Message", "", 54, new Vector2(0, 0), new Vector2(610, 76), Paper, true);
            messageDetail = Label(banner, "Detail", "", 27, new Vector2(0, -79), new Vector2(610, 96), Muted);
            banner.gameObject.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            EventSystem system = FindFirstObjectByType<EventSystem>();
            if (system == null) system = new GameObject("EventSystem").AddComponent<EventSystem>();
            StandaloneInputModule old = system.GetComponent<StandaloneInputModule>();
            if (old != null) { old.enabled = false; Destroy(old); }
            if (system.GetComponent<InputSystemUIInputModule>() == null)
                system.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private Image Panel(Transform parent, string name, Color color, Vector2 position, Vector2 size)
        {
            Image image = Image(parent, name, panelSprite, color);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            Place(image.rectTransform, Vector2.one * 0.5f, position, size);
            return image;
        }

        private static Image Circle(Transform parent, string name, Color color, Vector2 position, float diameter)
        {
            Image image = Image(parent, name, SpriteLibrary.WhiteCircle(), color);
            Place(image.rectTransform, Vector2.one * 0.5f, position, Vector2.one * diameter);
            return image;
        }

        private static Image Bar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            Image image = Image(parent, name, SpriteLibrary.WhiteSquare(), color);
            Place(image.rectTransform, Vector2.one * 0.5f, position, size);
            return image;
        }

        private static void Line(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            Image line = Bar(parent, "Stroke", (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to), width), color);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, string name, string content, int size, Vector2 position, Vector2 bounds, Color color, bool bold = false)
        {
            Text text = Rect(parent, name).gameObject.AddComponent<SharpText>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter; text.color = color;
            text.supportRichText = true; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = content;
            Place(text.rectTransform, Vector2.one * 0.5f, position, bounds);
            return text;
        }

        // Generated once at runtime. No imported artwork or per-frame texture allocations.
        private static Sprite RoundedPanel()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 128;
            const float cornerRadius = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "HUD Panel";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new Vector2(Mathf.Abs(x + 0.5f - size * 0.5f), Mathf.Abs(y + 0.5f - size * 0.5f));
                    Vector2 edge = offset - Vector2.one * (size * 0.5f - cornerRadius - 1);
                    float distance = new Vector2(Mathf.Max(edge.x, 0), Mathf.Max(edge.y, 0)).magnitude
                        + Mathf.Min(Mathf.Max(edge.x, edge.y), 0) - cornerRadius;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - distance));
                }
            texture.SetPixels(pixels); texture.Apply();
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f,
                200, 0, SpriteMeshType.FullRect, Vector4.one * 36);
            return roundedSprite;
        }

        private static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 512;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "HUD Ring";
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 256);
                    float alpha = Mathf.Clamp01(255 - radius) * Mathf.Clamp01(radius - 247);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels); texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100);
            return ringSprite;
        }
    }
}
