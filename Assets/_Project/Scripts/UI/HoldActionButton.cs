using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>One finger owns a hold. Cancellation never counts as a release-to-kick.</summary>
    public class HoldActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private int? pointerId;
        private Image image;
        private Color idleColor;
        private Transform symbol;
        private float pressBlend;
        private bool commandHeld;
        private Canvas canvas;
        private Vector2 flickStart;
        private Vector2 lastPosition;
        private float flickStartedAt;
        private float lastMovedAt;
        public Vector2 Flick { get; private set; }
        public bool IsHeld => pointerId.HasValue;
        public event Action Pressed;
        public event Action Released;
        public event Action Cancelled;
        /// <summary>Recent release gesture in screen axes, normalized by the canvas scale.</summary>
        public event Action<Vector2> FlickChanged;

        /// <summary>Keep keyboard/gamepad feedback consistent with the touch controls.</summary>
        public void SetCommandHeld(bool held) => commandHeld = held;

        private void Awake()
        {
            image = GetComponent<Image>();
            if (image != null) idleColor = image.color;
            symbol = transform.Find("Symbol");
            canvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            if (IsHeld && Flick != Vector2.zero && Time.unscaledTime - lastMovedAt > 0.18f) SetFlick(Vector2.zero);
            pressBlend = Mathf.MoveTowards(pressBlend, IsHeld || commandHeld ? 1f : 0f, Time.unscaledDeltaTime * 12f);
            if (image != null)
            {
                Color pressed = Color.Lerp(idleColor, Color.white, 0.12f);
                pressed.a = Mathf.Min(1f, idleColor.a + 0.18f);
                image.color = Color.Lerp(idleColor, pressed, pressBlend);
            }
            if (symbol != null) symbol.localScale = Vector3.one * (1f + pressBlend * 0.08f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld) return;
            pointerId = eventData.pointerId;
            flickStart = lastPosition = eventData.position;
            flickStartedAt = lastMovedAt = Time.unscaledTime;
            SetFlick(Vector2.zero);
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            UpdateFlick(eventData.position);
            pointerId = null;
            Released?.Invoke();
            SetFlick(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) UpdateFlick(eventData.position);
        }

        private void UpdateFlick(Vector2 position)
        {
            float now = Time.unscaledTime;
            if ((position - lastPosition).sqrMagnitude > 0.01f)
            {
                if (now - lastMovedAt > 0.16f || now - flickStartedAt > 0.16f)
                {
                    flickStart = lastPosition;
                    flickStartedAt = Mathf.Max(lastMovedAt, now - 0.016f);
                }
                lastPosition = position;
                lastMovedAt = now;
            }
            Vector2 delta = position - flickStart;
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            float distance = delta.magnitude / scale;
            float duration = Mathf.Max(0.016f, now - flickStartedAt);
            SetFlick(duration <= 0.18f && distance >= 20f && distance / duration >= 250f
                ? delta.normalized * Mathf.Clamp01(distance / 90f) : Vector2.zero);
        }

        private void SetFlick(Vector2 value)
        {
            if (Flick == value) return;
            Flick = value;
            FlickChanged?.Invoke(value);
        }

        public void Cancel()
        {
            if (!IsHeld) return;
            pointerId = null;
            SetFlick(Vector2.zero);
            Cancelled?.Invoke();
        }

        private void OnDisable()
        {
            Cancel();
            commandHeld = false;
            pressBlend = 0f;
            if (image != null) image.color = idleColor;
            if (symbol != null) symbol.localScale = Vector3.one;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    }
}
