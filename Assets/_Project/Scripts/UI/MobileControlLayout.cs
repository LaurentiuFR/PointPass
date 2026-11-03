using System;
using System.Collections.Generic;
using UnityEngine;

namespace PointPass.UI
{
    [Serializable] public sealed class MobileControlEntry
    {
        public string id;
        public Vector2 position;
        public float scale=1, opacity=1;
        public MobileControlEntry Copy() => new MobileControlEntry{id=id,position=position,scale=scale,opacity=opacity};
    }
    [Serializable] public sealed class MobileControlSettings
    {
        public int version=1;
        public float sensitivity=1;
        public List<MobileControlEntry> controls=new List<MobileControlEntry>();
        public MobileControlSettings Copy()
        {
            var value=new MobileControlSettings{sensitivity=sensitivity};
            foreach(var control in controls) value.controls.Add(control.Copy()); return value;
        }
    }
    /// <summary>Normalized safe-area positions, independent opacity, and local preferences.</summary>
    public sealed class MobileControlLayout : MonoBehaviour
    {
        public const string PreferenceKey="PointPass.MobileControls.v1";
        public sealed class Slot
        {
            public string Id;
            public RectTransform Root;
            public Vector2 BaseSize;
            public CanvasGroup Opacity;
            public MobileControlEntry Default;
        }
        public readonly List<Slot> Slots=new List<Slot>();
        public RectTransform SafeArea { get; private set; }
        public MobileControlSettings Settings { get; private set; }
        private VirtualJoystick joystick;
        private Vector2 previousSize;
        private float previousPixelScale;
        private static event Action Saved;
        public void Initialize(RectTransform safeArea,VirtualJoystick value)
        { SafeArea=safeArea; joystick=value; Saved+=LoadSaved; }
        public void Register(string id,RectTransform control)
        {
            Vector2 size=SafeArea.rect.size;
            var baseline=new MobileControlEntry{id=id,position=new Vector2(
                (control.anchorMin.x*size.x+control.anchoredPosition.x)/Mathf.Max(1,size.x),
                (control.anchorMin.y*size.y+control.anchoredPosition.y)/Mathf.Max(1,size.y))};
            var root=LobbyUi.Rect(SafeArea,id+"Control",Vector2.zero,control.sizeDelta);
            Vector2 baseSize=control.sizeDelta;
            control.SetParent(root,false); control.anchorMin=control.anchorMax=control.pivot=Vector2.one*.5f;
            control.anchoredPosition=Vector2.zero;
            Slots.Add(new Slot{Id=id,Root=root,BaseSize=baseSize,Default=baseline,Opacity=root.gameObject.AddComponent<CanvasGroup>()});
        }
        public MobileControlSettings Defaults(bool leftHanded=false)
        {
            var value=new MobileControlSettings();
            foreach(var slot in Slots) { var entry=slot.Default.Copy(); if(leftHanded) entry.position.x=1-entry.position.x; value.controls.Add(entry); }
            return value;
        }
        public void LoadSaved()
        {
            MobileControlSettings value=null;
            try { value=JsonUtility.FromJson<MobileControlSettings>(PlayerPrefs.GetString(PreferenceKey,"")); } catch(ArgumentException) { }
            if(value==null||value.version!=1||value.controls==null) value=Defaults();
            Apply(value);
        }
        public void Apply(MobileControlSettings value)
        {
            value.controls.RemoveAll(entry=>entry==null||string.IsNullOrEmpty(entry.id));
            Settings=value;
            value.sensitivity=Valid(value.sensitivity)?Mathf.Clamp(value.sensitivity,.5f,2):1;
            joystick.Sensitivity=value.sensitivity;
            Vector2 size=SafeArea.rect.size; previousSize=size;
            float pixelsPerUnit=Mathf.Max(.01f,GetComponent<Canvas>().scaleFactor); previousPixelScale=pixelsPerUnit;
            foreach(var slot in Slots)
            {
                var entry=value.controls.Find(e=>e!=null&&e.id==slot.Id);
                if(entry==null) { entry=slot.Default.Copy(); value.controls.Add(entry); }
                if(!Valid(entry.position.x)||!Valid(entry.position.y)) entry.position=slot.Default.position;
                entry.scale=Valid(entry.scale)?Mathf.Clamp(entry.scale,.8f,1.55f):1;
                entry.opacity=Valid(entry.opacity)?Mathf.Clamp(entry.opacity,.25f,1):1;
                // Keep a useful target even when a saved layout is opened on a smaller display.
                float effectiveScale=Mathf.Max(entry.scale,44/(Mathf.Min(slot.BaseSize.x,slot.BaseSize.y)*pixelsPerUnit));
                Vector2 margin=(slot.BaseSize+Vector2.one*16)*effectiveScale*.5f+Vector2.one*8;
                Vector2 point=new Vector2(entry.position.x*size.x,entry.position.y*size.y);
                point.x=Mathf.Clamp(point.x,Mathf.Min(margin.x,size.x*.5f),Mathf.Max(size.x-margin.x,size.x*.5f));
                point.y=Mathf.Clamp(point.y,Mathf.Min(margin.y,size.y*.5f),Mathf.Max(size.y-margin.y,size.y*.5f));
                entry.position=new Vector2(point.x/Mathf.Max(1,size.x),point.y/Mathf.Max(1,size.y));
                slot.Root.anchorMin=slot.Root.anchorMax=Vector2.zero; slot.Root.pivot=Vector2.one*.5f;
                slot.Root.anchoredPosition=point; slot.Root.localScale=Vector3.one*effectiveScale;
                slot.Opacity.alpha=entry.opacity;
            }
        }
        public void Save()
        { PlayerPrefs.SetString(PreferenceKey,JsonUtility.ToJson(Settings)); PlayerPrefs.Save(); Saved?.Invoke(); }
        public bool HasOverlaps()
        {
            for(int a=0;a<Slots.Count;a++) for(int b=a+1;b<Slots.Count;b++)
            {
                var x=Slots[a]; var y=Slots[b];
                Vector2 xs=x.BaseSize*x.Root.localScale.x*.46f,ys=y.BaseSize*y.Root.localScale.x*.46f;
                Vector2 distance=x.Root.anchoredPosition-y.Root.anchoredPosition;
                if(Mathf.Abs(distance.x)<xs.x+ys.x&&Mathf.Abs(distance.y)<xs.y+ys.y) return true;
            }
            return false;
        }
        private static bool Valid(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void LateUpdate()
        { if(Settings!=null&&(SafeArea.rect.size!=previousSize||GetComponent<Canvas>().scaleFactor!=previousPixelScale)) Apply(Settings); }
        private void OnDestroy() { Saved-=LoadSaved; }
    }
}
