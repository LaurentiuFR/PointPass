using UnityEngine;
using UnityEngine.EventSystems;

namespace PointPass.UI
{
    /// <summary>
    /// On-screen movement stick. Put this on the joystick background Image.
    /// Value is a Vector2 from (-1,-1) to (1,1), zero when released.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField, Range(0f, 0.5f)] private float deadzone = 0.1f;

        public Vector2 Value { get; private set; }

        private void Reset()
        {
            background = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        private void UpdateStick(PointerEventData eventData)
        {
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
