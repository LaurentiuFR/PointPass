using UnityEngine;
using UnityEngine.EventSystems;

namespace PointPass.UI
{
    /// <summary>
    /// On-screen movement stick. Value is a Vector2 from (-1,-1) to (1,1), zero when released.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform background;
        private RectTransform handle;
        private float deadzone = 0.1f;
        private int? pointerId;

        public Vector2 Value { get; private set; }

        public void Init(RectTransform background, RectTransform handle, float deadzone = 0.1f)
        {
            this.background = background;
            this.handle = handle;
            this.deadzone = deadzone;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId.HasValue) return;
            pointerId = eventData.pointerId;
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId != eventData.pointerId) return;
            ResetInput();
        }

        public void ResetInput()
        {
            pointerId = null;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        private void OnDisable() => ResetInput();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetInput(); }

        private void UpdateStick(PointerEventData eventData)
        {
            if (background == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            float radius = background.rect.width * 0.5f;
            Vector2 normalized = Vector2.ClampMagnitude(local / radius, 1f);

            Value = normalized.magnitude < deadzone ? Vector2.zero : normalized;

            if (handle != null) handle.anchoredPosition = normalized * radius;
        }
    }
}
