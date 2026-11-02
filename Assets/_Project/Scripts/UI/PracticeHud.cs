using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Practice-only readout and reset action, inside the existing HUD safe area.</summary>
    public class PracticeHud : MonoBehaviour
    {
        private PassingPractice practice;
        private UnityEngine.UI.Text stats;
        private UnityEngine.UI.Text instruction;

        public static PracticeHud Create(Transform safeArea, PassingPractice session)
        {
            RectTransform root = Rect(safeArea, "PracticeHUD", Vector2.zero, Vector2.zero, Vector2.one * 0.5f);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            PracticeHud view = root.gameObject.AddComponent<PracticeHud>();
            view.practice = session;
            UnityEngine.UI.Image plate = Panel(root, "PracticeReadout", new Vector2(0, -26), new Vector2(650, 94), new Vector2(0.5f, 1));
            Label(plate.transform, "Title", "PASSING PRACTICE", 19, new Vector2(0, 26), new Vector2(600, 32), MatchPalette.Accent);
            view.stats = Label(plate.transform, "Stats", "", 27, new Vector2(0, -16), new Vector2(610, 44), MatchPalette.Paper);
            UnityEngine.UI.Image guide = Panel(root, "PracticeInstruction", new Vector2(0, -130), new Vector2(810, 42), new Vector2(0.5f, 1));
            view.instruction = Label(guide.transform, "Instruction", "", 18, Vector2.zero, new Vector2(790, 38), MatchPalette.Paper);
            UnityEngine.UI.Image reset = Panel(root, "ResetPracticeBall", new Vector2(30, -26), new Vector2(230, 66), new Vector2(0, 1));
            reset.rectTransform.pivot = new Vector2(0, 1);
            reset.raycastTarget = true;
            UnityEngine.UI.Button button = reset.gameObject.AddComponent<MenuButton>();
            button.targetGraphic = reset;
            button.onClick.AddListener(() => { if (view.isActiveAndEnabled) session.ResetBall(); });
            Label(reset.transform, "Label", "RESET BALL", 22, Vector2.zero, new Vector2(214, 54), MatchPalette.Paper);
            session.Changed += view.Refresh;
            view.Refresh();
            root.gameObject.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible) Refresh();
        }
        private void Refresh()
        {
            if (this == null) { if (practice != null) practice.Changed -= Refresh; return; }
            stats.text = $"HITS {practice.Hits}  /  PASSES {practice.Attempts}  /  GATE {practice.ActiveGate + 1}";
            instruction.text = practice.Feedback;
        }
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static UnityEngine.UI.Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            UnityEngine.UI.Image image = Rect(parent, name, position, size, anchor).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = SpriteLibrary.WhiteSquare(); image.color = MatchPalette.Ink; image.raycastTarget = false;
            return image;
        }
        private static UnityEngine.UI.Text Label(Transform parent, string name, string text, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            RectTransform rect = Rect(parent, name, position, dimensions, Vector2.one * 0.5f); rect.pivot = Vector2.one * 0.5f;
            SharpText label = rect.gameObject.AddComponent<SharpText>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size;
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
            label.text = text; label.color = color; label.raycastTarget = false;
            return label;
        }
        private void OnDestroy() { if (practice != null) practice.Changed -= Refresh; }
    }
}
