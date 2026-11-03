using UnityEngine;
using UnityEngine.EventSystems;

namespace PointPass.UI
{
    public sealed class ControlLayoutHandle : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public string Id { get; private set; }
        public RectTransform Root=> (RectTransform)transform;
        private MobileControlEditor editor;
        private int? pointer;
        private Vector2 offset;
        public void Initialize(MobileControlEditor owner,string id) {editor=owner;Id=id;}
        public void OnPointerDown(PointerEventData value) { editor.Select(Id); }
        public void OnBeginDrag(PointerEventData value)
        {
            if(pointer.HasValue)return;pointer=value.pointerId;
            offset=(Vector2)RectTransformUtility.WorldToScreenPoint(value.pressEventCamera,Root.position)-value.position;
        }
        public void OnDrag(PointerEventData value) {if(pointer==value.pointerId)editor.Drag(Id,value.position+offset,value.pressEventCamera);}
        public void OnEndDrag(PointerEventData value) {if(pointer==value.pointerId)pointer=null;}
        private void OnDisable(){pointer=null;}
    }
}
