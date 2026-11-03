using UnityEngine;
using UnityEngine.UI;

namespace PointPass.UI
{
    /// <summary>A live instance of the real touch HUD, with an editor overlay and explicit save/cancel.</summary>
    public sealed class MobileControlEditor : MonoBehaviour
    {
        private static MobileControlEditor current;
        public static bool IsOpen => current != null;
        private GameObject backdrop;
        private MatchHud preview;
        private MobileControlLayout layout;
        private MobileControlSettings working;
        private MobileControlEntry selected;
        private RectTransform safeArea;
        private RectTransform toolbarRoot;
        private UnityEngine.UI.Slider sizeSlider,opacitySlider,sensitivitySlider;
        private UnityEngine.UI.Text selectedLabel,warning;
        private readonly System.Collections.Generic.List<ControlLayoutHandle> handles=new System.Collections.Generic.List<ControlLayoutHandle>();
        private Rect lastSafeArea;
        private Vector2Int lastResolution;

        public static void Open()
        {
            if(IsOpen) return;
            var root=MakeCanvas("MobileControlEditor",212);
            current=root.AddComponent<MobileControlEditor>(); current.Build();
        }
        private static GameObject MakeCanvas(string name,int order)
        {
            var root=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=1;
            return root;
        }
        private void Build()
        {
            backdrop=MakeCanvas("ControlEditorBackdrop",210);
            var background=LobbyUi.Image(backdrop.transform,"Background",Vector2.zero,Vector2.one,LobbyMenuView.ClubhouseNavy);
            LobbyUi.Stretch(background.rectTransform); background.raycastTarget=true;
            preview=MatchHud.Create(); preview.GetComponent<Canvas>().sortingOrder=211; preview.PrepareLayoutPreview();
            layout=preview.ControlLayout; working=layout.Settings.Copy();
            safeArea=LobbyUi.Rect(transform,"SafeArea",Vector2.zero,Vector2.zero); ApplySafeArea();
            Canvas.ForceUpdateCanvases();
            foreach(var slot in layout.Slots)
            {
                var root=LobbyUi.Rect(safeArea,slot.Id+"DragHandle",Vector2.zero,slot.BaseSize);
                var image=root.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=MatchMenu.Rounded();image.type=UnityEngine.UI.Image.Type.Sliced;
                image.color=new Color(.6f,.85f,1,.045f);image.raycastTarget=true;
                var outline=root.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.6f,.85f,1,.5f);outline.effectDistance=Vector2.one*2;
                var handle=root.gameObject.AddComponent<ControlLayoutHandle>();handle.Initialize(this,slot.Id);handles.Add(handle);
            }
            var toolbar=LobbyUi.Panel(safeArea,"Toolbar",new Vector2(0,-218),new Vector2(1800,400),new Color(.04f,.1f,.17f,.98f));
            toolbarRoot=toolbar.rectTransform;
            toolbar.rectTransform.anchorMin=toolbar.rectTransform.anchorMax=new Vector2(.5f,1);
            LobbyUi.Button(toolbar.transform,"Back","CANCEL",new Vector2(-716,120),new Vector2(270,124),Close);
            LobbyUi.Button(toolbar.transform,"LeftHanded","LEFT-HANDED PRESET",new Vector2(-333,120),new Vector2(430,124),()=>Reset(true));
            LobbyUi.Button(toolbar.transform,"Reset","RESET TO DEFAULT",new Vector2(157,120),new Vector2(430,124),()=>Reset(false));
            LobbyUi.Button(toolbar.transform,"Save","SAVE CONTROLS",new Vector2(671,120),new Vector2(360,124),()=>{layout.Save();Close();},true);
            selectedLabel=LobbyUi.Text(toolbar.transform,"Selected","",30,new Vector2(-696,-45),new Vector2(320,100),Color.white,true);
            sizeSlider=Slider(toolbar.transform,"SIZE",new Vector2(-307,-70),.8f,1.55f,value=>{if(selected!=null){selected.scale=value;Refresh();}});
            opacitySlider=Slider(toolbar.transform,"OPACITY",new Vector2(111,-70),.25f,1,value=>{if(selected!=null){selected.opacity=value;Refresh();}});
            sensitivitySlider=Slider(toolbar.transform,"JOYSTICK SENSITIVITY",new Vector2(550,-70),.5f,2,value=>{working.sensitivity=value;Refresh();});
            warning=LobbyUi.Text(toolbar.transform,"OverlapWarning","",26,new Vector2(0,-168),new Vector2(1680,45),Color.white);
            Select("Joystick"); Refresh();
        }
        private UnityEngine.UI.Slider Slider(Transform parent,string title,Vector2 position,float min,float max,UnityEngine.Events.UnityAction<float> changed)
        {
            var root=LobbyUi.Rect(parent,title,position,new Vector2(345,124));
            LobbyUi.Text(root,"Caption",title,24,new Vector2(0,48),new Vector2(370,40),LobbyUi.Aqua,true);
            var track=LobbyUi.Image(root,"Track",Vector2.zero,new Vector2(345,8),new Color(.25f,.35f,.45f));
            var handleArea=LobbyUi.Rect(root,"HandleArea",Vector2.zero,new Vector2(313,56));
            var thumb=LobbyUi.Image(handleArea,"Handle",Vector2.zero,new Vector2(32,0),LobbyUi.Aqua);thumb.raycastTarget=true;
            var slider=root.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.minValue=min;slider.maxValue=max;
            slider.handleRect=thumb.rectTransform;slider.targetGraphic=thumb;
            var hit=root.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
            slider.onValueChanged.AddListener(changed);return slider;
        }
        public void Select(string id)
        {
            selected=working.controls.Find(x=>x.id==id);
            if(selectedLabel==null||selected==null) return;
            selectedLabel.text=id.ToUpperInvariant()+"\nDrag to move";
            sizeSlider.SetValueWithoutNotify(selected.scale);opacitySlider.SetValueWithoutNotify(selected.opacity);
            sensitivitySlider.SetValueWithoutNotify(working.sensitivity);
        }
        public void Drag(string id,Vector2 screenPoint,Camera camera)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea,screenPoint,camera,out Vector2 point))return;
            var value=working.controls.Find(x=>x.id==id); if(value==null)return;
            value.position=new Vector2((point.x-safeArea.rect.xMin)/safeArea.rect.width,(point.y-safeArea.rect.yMin)/safeArea.rect.height);
            Refresh();
        }
        private void Reset(bool left)
        { working=layout.Defaults(left);layout.Apply(working);Select(selected!=null?selected.id:"Joystick");Refresh(); }
        private void Refresh()
        {
            if(toolbarRoot!=null)toolbarRoot.localScale=Vector3.one*Mathf.Min(1,(safeArea.rect.width-24)/1800f);
            layout.Apply(working);
            foreach(var handle in handles)
            {
                var slot=layout.Slots.Find(x=>x.Id==handle.Id);
                handle.Root.anchorMin=handle.Root.anchorMax=Vector2.zero;
                handle.Root.anchoredPosition=slot.Root.anchoredPosition;
                handle.Root.sizeDelta=slot.BaseSize*slot.Root.localScale.x;
            }
            if(warning!=null)
            {
                bool overlap=layout.HasOverlaps();warning.color=overlap?new Color(1,.72f,.25f):LobbyUi.Dim;
                warning.text=overlap?"CONTROLS OVERLAP · Drag them apart so each touch is reliable.":"Drag a control to position it. Changes stay inside the safe area. Save to keep your layout.";
            }
        }
        private void ApplySafeArea()
        {
            lastSafeArea=Screen.safeArea;lastResolution=new Vector2Int(Screen.width,Screen.height);
            safeArea.anchorMin=new Vector2(lastSafeArea.xMin/Screen.width,lastSafeArea.yMin/Screen.height);
            safeArea.anchorMax=new Vector2(lastSafeArea.xMax/Screen.width,lastSafeArea.yMax/Screen.height);
            safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        private void LateUpdate()
        {
            if(Screen.safeArea!=lastSafeArea||lastResolution.x!=Screen.width||lastResolution.y!=Screen.height)
            { ApplySafeArea();Canvas.ForceUpdateCanvases();Refresh(); }
        }
        public void Close() { Destroy(gameObject); }
        private void OnDestroy()
        {
            if(preview!=null)Destroy(preview.gameObject);if(backdrop!=null)Destroy(backdrop);
            if(current==this)current=null;
            foreach(var input in UnityEngine.Object.FindObjectsByType<PointPass.Players.HumanPlayerInput>(FindObjectsSortMode.None)) input.SuppressUntilRelease();
        }
    }
}
