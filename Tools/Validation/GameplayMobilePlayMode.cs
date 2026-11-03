using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PointPass.Validation
{
    public static class GameplayMobilePlayMode
    {
        private static readonly string Folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/PlayerFeatures/Editor");
        private static string Report=>Path.Combine(Folder,"tests.txt");
        private static void Check(bool value,string label)
        {File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new InvalidOperationException(label);}
        private static async Task Capture(string name)
        {await Task.Delay(160);var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Folder,name+".png"),texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);}
        private static object Call(object target,string name,params object[] arguments)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,arguments);
        private static async Task Wait(Func<bool> predicate,string label,float seconds=5)
        {var watch=System.Diagnostics.Stopwatch.StartNew();while(!predicate()&&watch.Elapsed.TotalSeconds<seconds)await Task.Delay(20);Check(predicate(),label);}
        private sealed class InputProbe : IPlayerCommandSink
        {
            public bool IsActive=>true;
            public float KickCharge=>.3f;
            public PlayerKicker.KickType ChargingKick{get;private set;}
            public float ShotCurve{get;private set;}
            public Vector2 Move;
            public bool Sprint;
            public int Tackles,Releases;
            public void SetMove(Vector2 value){Move=value;}
            public void SetSprint(bool value){Sprint=value;}
            public bool BeginKick(PlayerKicker.KickType value){ChargingKick=value;return true;}
            public void ReleaseKick(PlayerKicker.KickType value){ChargingKick=PlayerKicker.KickType.None;Releases++;}
            public void CancelKick(){ChargingKick=PlayerKicker.KickType.None;}
            public void SetShotCurve(float value){ShotCurve=value;}
            public bool RequestTackle(){Tackles++;return true;}
        }
        private static void CheckInputRoutes(HumanPlayerInput human,PlayerController player,MatchHud hud)
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var pad=InputSystem.AddDevice<Gamepad>();
            var background=InputSystem.settings.backgroundBehavior;var prior=player.CommandSink;
            var probe=new InputProbe();
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.EnableDevice(keyboard);InputSystem.EnableDevice(pad);keyboard.MakeCurrent();pad.MakeCurrent();
                player.CommandSink=probe;human.enabled=true;human.SendMessage("OnApplicationFocus",true);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputState.Change(pad,new GamepadState());
                Call(human,"Update");Call(human,"Update");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.L));InputSystem.Update();Call(human,"Update");
                Check(probe.Move.y>.9f&&probe.Sprint&&probe.Tackles==1,"Keyboard movement, sprint and L tackle use the common command route ("+probe.Move+", "+probe.Sprint+", "+probe.Tackles+", key="+keyboard.wKey.isPressed+", current="+(Keyboard.current==keyboard)+")");
                Call(human,"Update");Check(probe.Tackles==1,"A held tackle key does not repeat");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();Call(human,"Update");
                InputState.Change(pad,new GamepadState{leftStick=Vector2.left}.WithButton(GamepadButton.West).WithButton(GamepadButton.RightShoulder));Call(human,"Update");
                Check(probe.Move.x<-.9f&&probe.Sprint&&probe.Tackles==2,"Gamepad movement, RB sprint and West tackle use the same command route ("+probe.Move+", "+probe.Sprint+", "+probe.Tackles+")");
                InputState.Change(pad,new GamepadState());Call(human,"Update");
                Vector2 centre=RectTransformUtility.WorldToScreenPoint(null,hud.Joystick.transform.position);
                var move=new PointerEventData(EventSystem.current){pointerId=70,position=centre+Vector2.right*100};
                var sprint=new PointerEventData(EventSystem.current){pointerId=71,position=new Vector2(850,100)};
                var kick=new PointerEventData(EventSystem.current){pointerId=72,position=new Vector2(900,220)};
                hud.Joystick.OnPointerDown(move);hud.SprintButton.OnPointerDown(sprint);hud.PassButton.OnPointerDown(kick);Call(human,"Update");
                Check(probe.Move.x>0&&probe.Sprint&&probe.ChargingKick==PlayerKicker.KickType.Pass,"Touch movement, sprint and charged pass work at the same time");
                hud.PassButton.OnPointerUp(kick);Check(probe.Releases==1,"The owning touch releases the charged pass");
                hud.ShootButton.OnPointerDown(kick);kick.position+=Vector2.up*130;hud.ShootButton.OnDrag(kick);Call(human,"Update");
                Check(probe.ChargingKick==PlayerKicker.KickType.Shot&&probe.ShotCurve>.5f,"Touch aiming and a sideways shoot flick preserve shot bend");
                hud.ShootButton.OnPointerUp(kick);hud.SprintButton.OnPointerUp(sprint);hud.Joystick.OnPointerUp(move);
                Check(probe.Releases==2,"Shoot releases independently while movement and sprint retain their own fingers");
            }
            finally
            {
                human.enabled=false;player.CommandSink=prior;hud.PassButton.Cancel();hud.ShootButton.Cancel();hud.SprintButton.Cancel();hud.Joystick.ResetInput();
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(pad);InputSystem.settings.backgroundBehavior=background;
            }
        }

        private sealed class Viewport : IDisposable
        {
            private readonly Type viewType, sizeType, kindType;
            private readonly EditorWindow view;
            private readonly object group;
            private readonly int original;
            private readonly List<object> added = new List<object>();
            public Viewport()
            {
                var assembly = typeof(Editor).Assembly;
                viewType = assembly.GetType("UnityEditor.GameView");
                sizeType = assembly.GetType("UnityEditor.GameViewSize");
                kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                view = EditorWindow.GetWindow(viewType); view.Focus();
                original = (int)viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(view);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            }
            public async Task Resize(int width, int height)
            {
                var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "PointPass touch review" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); added.Add(size);
                int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) + (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { size });
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(view, new object[] { index, null });
                view.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(350);
                Check(Screen.width == width && Screen.height == height, "Native viewport " + width + "x" + height);
            }
            public void Dispose()
            {
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(view, new object[] { original, null });
                foreach (var size in added.AsEnumerable().Reverse())
                {
                    int index = (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { size });
                    int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtins + index });
                }
            }
        }
        private static async Task CheckScreenSizes(MatchHud originalHud)
        {
            originalHud.gameObject.SetActive(false);
            try
            {
            using(var viewport=new Viewport())
            {
                foreach(var resolution in new[]{new Vector2Int(844,390),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2340,1080)})
                {
                    await viewport.Resize(resolution.x,resolution.y);
                    var preview=MatchHud.Create();preview.PrepareLayoutPreview();Canvas.ForceUpdateCanvases();
                    var layout=preview.ControlLayout;layout.Apply(layout.Defaults());
                    Check(!layout.HasOverlaps(),"Default controls are distinct at "+resolution);
                    foreach(var slot in layout.Slots)
                    {
                        float pixels=Mathf.Min(slot.BaseSize.x,slot.BaseSize.y)*slot.Root.localScale.x*preview.GetComponent<Canvas>().scaleFactor;
                        Check(pixels>=43.9f,slot.Id+" target is at least 44 screen pixels at "+resolution);
                    }
                    // Synthetic side cutouts and gesture inset; this is not a hardware notch test.
                    layout.SafeArea.anchorMin=new Vector2(.04f,.06f);layout.SafeArea.anchorMax=new Vector2(.96f,.98f);
                    Canvas.ForceUpdateCanvases();layout.Apply(layout.Settings);
                    foreach(var slot in layout.Slots)
                    {
                        Vector2 half=slot.BaseSize*slot.Root.localScale.x*.5f;
                        Vector2 position=slot.Root.anchoredPosition;
                        Check(position.x-half.x>=0&&position.y-half.y>=0&&position.x+half.x<=layout.SafeArea.rect.width&&position.y+half.y<=layout.SafeArea.rect.height,
                            slot.Id+" stays inside simulated safe area at "+resolution);
                    }
                    await Capture("safe-area-"+resolution.x+"x"+resolution.y);
                    UnityEngine.Object.Destroy(preview.gameObject);await Task.Delay(30);
                }
                await viewport.Resize(844,390);
                MobileControlEditor.Open();await Task.Delay(180);await Capture("control-layout-editor-844x390");
                var editor=UnityEngine.Object.FindFirstObjectByType<MobileControlEditor>();
                var handle=editor.GetComponentsInChildren<ControlLayoutHandle>().First(h=>h.Id=="Tackle");
                var point=RectTransformUtility.WorldToScreenPoint(null,handle.Root.position);
                var touch=new PointerEventData(EventSystem.current){pointerId=91,position=point};
                handle.OnPointerDown(touch);handle.OnBeginDrag(touch);touch.position+=Vector2.left*65;handle.OnDrag(touch);handle.OnEndDrag(touch);
                Check(Vector2.Distance(point,RectTransformUtility.WorldToScreenPoint(null,handle.Root.position))>40,"A real editor drag moves the selected control");
                var reset=editor.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="Reset");
                reset.onClick.Invoke();
                Check(!editor.GetComponentsInChildren<UnityEngine.UI.Text>().First(t=>t.name=="OverlapWarning").text.StartsWith("CONTROLS OVERLAP"),"Reset to Default clears the edited layout without overlaps");
                editor.Close();await Task.Delay(50);
            }
            }
            finally { originalHud.gameObject.SetActive(true); }
        }
        public static async Task<string> Run()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Report,"Gameplay and mobile control validation in the Windows Unity Editor\n");
            Check(Application.isPlaying,"Actual Play mode");
            int frame=Time.frameCount;await Task.Delay(100);Check(Time.frameCount>frame,"Game frames advance");
            var match=UnityEngine.Object.FindFirstObjectByType<MatchManager>();
            var hud=UnityEngine.Object.FindFirstObjectByType<MatchHud>(FindObjectsInactive.Include);
            var human=UnityEngine.Object.FindFirstObjectByType<HumanPlayerInput>(FindObjectsInactive.Include);
            Check(match!=null&&hud!=null&&human!=null,"Offline scene exposes the gameplay and real HUD");
            var player=human.GetComponent<PlayerController>();
            var ball=UnityEngine.Object.FindFirstObjectByType<Ball>();
            bool humanEnabled=human.enabled;
            bool hadSave=PlayerPrefs.HasKey(MobileControlLayout.PreferenceKey);
            string saved=PlayerPrefs.GetString(MobileControlLayout.PreferenceKey,"");
            EditorApplication.CallbackFunction keepLive=()=>{if(match!=null&&match.IsUserPaused)match.ResumeMatch();};
            EditorApplication.update+=keepLive;
            GameObject staminaObject=null;
            try
            {
                human.enabled=false;match.StartFreePlay();await Task.Delay(200);
                var layout=hud.ControlLayout;
                Check(layout!=null&&layout.Slots.Count==6,"Joystick, passing, shooting, sprint, tackle and cancel are customizable");
                var defaults=layout.Defaults();layout.Apply(defaults);
                Check(!layout.HasOverlaps(),"Default touch targets do not overlap");
                var mirrored=layout.Defaults(true);
                foreach(var original in defaults.controls)
                    Check(Mathf.Abs(mirrored.controls.First(x=>x.id==original.id).position.x-(1-original.position.x))<.001f,"Left-handed preset mirrors "+original.id);
                var custom=layout.Defaults();var edit=custom.controls.First(x=>x.id=="Tackle");edit.position=new Vector2(.5f,.4f);edit.scale=1.3f;edit.opacity=.45f;custom.sensitivity=1.6f;
                layout.Apply(custom);layout.Save();
                var preview=MatchHud.Create();preview.PrepareLayoutPreview();
                var restored=preview.ControlLayout.Settings.controls.First(x=>x.id=="Tackle");
                Check(Mathf.Abs(restored.position.x-.5f)<.01f&&Mathf.Abs(restored.scale-1.3f)<.001f&&Mathf.Abs(restored.opacity-.45f)<.001f,"Layout size, position and opacity survive creation of another HUD");
                Check(Mathf.Abs(preview.Joystick.Sensitivity-1.6f)<.001f,"Joystick sensitivity persists locally");
                UnityEngine.Object.Destroy(preview.gameObject);await Task.Delay(40);
                var pass=custom.controls.First(x=>x.id=="Pass");var shoot=custom.controls.First(x=>x.id=="Shoot");pass.position=shoot.position;
                layout.Apply(custom);Check(layout.HasOverlaps(),"Overlap detection flags ambiguous touch targets");
                foreach(var control in custom.controls)control.position=new Vector2(-5,9);
                layout.Apply(custom);
                foreach(var slot in layout.Slots)
                {
                    Vector2 extent=slot.BaseSize*slot.Root.localScale.x*.5f;
                    Check(slot.Root.anchoredPosition.x>=extent.x&&slot.Root.anchoredPosition.y+extent.y<=layout.SafeArea.rect.height+.1f,"Safe-area clamps "+slot.Id);
                }
                layout.Apply(layout.Defaults());

                // Real UI handlers retain individual touch ownership while the other fingers act.
                var centre=RectTransformUtility.WorldToScreenPoint(null,hud.Joystick.transform.position);
                var stick=new PointerEventData(EventSystem.current){pointerId=11,position=centre+Vector2.right*45};
                var sprint=new PointerEventData(EventSystem.current){pointerId=12,position=new Vector2(800,180)};
                var tackle=new PointerEventData(EventSystem.current){pointerId=13,position=new Vector2(700,180)};
                hud.Joystick.OnPointerDown(stick);hud.SprintButton.OnPointerDown(sprint);hud.TackleButton.OnPointerDown(tackle);
                Check(hud.Joystick.Value.x>0&&hud.SprintButton.IsHeld&&hud.TackleButton.IsHeld,"Three simultaneous pointers retain move, sprint and tackle");
                hud.Joystick.OnPointerUp(tackle);hud.SprintButton.OnPointerUp(tackle);
                Check(hud.Joystick.Value.x>0&&hud.SprintButton.IsHeld,"Another finger cannot release joystick or sprint");
                hud.TackleButton.OnPointerUp(tackle);hud.SprintButton.OnPointerUp(sprint);hud.Joystick.OnPointerUp(stick);
                hud.PassButton.OnPointerDown(sprint);hud.ShootButton.OnPointerDown(tackle);
                Check(hud.PassButton.IsHeld&&hud.ShootButton.IsHeld,"Separate fingers retain both kick controls without stealing ownership");
                hud.PassButton.Cancel();Check(hud.ShootButton.IsHeld,"Cancelling one finger does not release the other kick");hud.ShootButton.Cancel();
                CheckInputRoutes(human,player,hud);

                staminaObject=new GameObject("StaminaBudgetValidation",typeof(PlayerStamina));
                var stamina=staminaObject.GetComponent<PlayerStamina>();
                for(int i=0;i<260;i++)stamina.Step(true,true,.02f);
                Check(stamina.Exhausted&&!stamina.IsSprinting&&stamina.Remaining<.05f,"Sprinting depletes stamina and disables the speed boost");
                for(int i=0;i<80;i++)stamina.Step(true,true,.02f);
                Check(stamina.Exhausted&&!stamina.IsSprinting,"Holding sprint cannot bypass the exhaustion latch");
                for(int i=0;i<170;i++)stamina.Step(false,false,.02f);
                Check(!stamina.Exhausted&&stamina.Remaining>.98f,"Release and a short recovery refill the budget");
                stamina.ResetBudget();for(int i=0;i<150;i++)stamina.Step(true,false,.02f);
                Check(stamina.Remaining==1,"Stationary sprint holds do not drain stamina");

                player.GetComponent<PlayerMovement>().ResetTo(Vector2.zero,-90);player.Dribbler.Dispossess(.05f);
                ball.ResetTo(new Vector2(1.05f,0));await Task.Delay(350);
                Check(!player.Dribbler.HasBall,"A distant ball is not magnetically acquired");
                ball.ResetTo(new Vector2(.6f,0));await Wait(()=>player.Dribbler.HasBall,"Ball is collected through close foot contact");
                Check(Vector2.Distance(player.Body.position,ball.Body.position)<.8f,"Controlled ball remains close to the feet");
                player.SetMove(Vector2.right);await Task.Delay(400);player.SetMove(Vector2.left);await Task.Delay(180);
                Check(!player.Dribbler.HasBall||Vector2.Distance(player.Body.position,ball.Body.position)<1.1f,"Sharp turns permit separation without pulling the ball across the body");
                player.SetMove(Vector2.zero);
                MobileControlEditor.Open();await Task.Delay(180);
                Check(MobileControlEditor.IsOpen,"The live control-layout editor opens");
                Check(UnityEngine.Object.FindObjectsByType<ControlLayoutHandle>(FindObjectsSortMode.None).Length==6,"Every touch control has an independent drag handle");
                await Capture("control-layout-editor");
                UnityEngine.Object.FindFirstObjectByType<MobileControlEditor>().Close();await Task.Delay(120);
                Check(!MobileControlEditor.IsOpen,"Editor closes without leaking an input-blocking overlay");
                await Capture("stamina-tackle-hud");
                await CheckScreenSizes(hud);
                File.AppendAllText(Report,"COMPLETE · Editor synthetic touches are not physical device tests.\n");
                return Report;
            }
            finally
            {
                var editor=UnityEngine.Object.FindFirstObjectByType<MobileControlEditor>();if(editor!=null)editor.Close();
                if(staminaObject!=null)UnityEngine.Object.Destroy(staminaObject);
                if(hadSave)PlayerPrefs.SetString(MobileControlLayout.PreferenceKey,saved);else PlayerPrefs.DeleteKey(MobileControlLayout.PreferenceKey);
                PlayerPrefs.Save();
                foreach(var layout in UnityEngine.Object.FindObjectsByType<MobileControlLayout>(FindObjectsInactive.Include,FindObjectsSortMode.None))layout.LoadSaved();
                human.enabled=humanEnabled;human.SuppressUntilRelease();EditorApplication.update-=keepLive;
                match.OpenMainMenu();
            }
        }
    }
}
