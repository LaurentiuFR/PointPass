using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>Routes UI Cancel from the selected button to its owning modal menu.</summary>
    public class MenuButton : Button, ICancelHandler
    {
        private MatchMenu menu;

        protected override void Awake()
        {
            base.Awake();
            menu = GetComponentInParent<MatchMenu>();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!isActiveAndEnabled || !IsInteractable()) return;
            if (menu == null) menu = GetComponentInParent<MatchMenu>();
            if (menu != null && menu.IsOpen) menu.OnCancel(eventData);
        }
    }
}
