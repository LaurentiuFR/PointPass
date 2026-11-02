using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Real graphics-enabled Play Mode match, celebration, replay and editor performance review.</summary>
    [InitializeOnLoad]
    public static class MatchVisualReview
    {
        private const string Key="PointPass.MatchVisualReview.";
        private static readonly List<double> frameTimes=new List<double>();
        private static readonly List<long> allocations=new List<long>();
        private static ProfilerRecorder gc;
        private static int sampleFrames,skipFrames;
        private static float goalStarted;
        private static bool flashCaptured;
        private static bool reactionVerified;
        private static Vector2 pausedPosition;
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"../../MatchVisualReview"));
        private static string Report => Path.Combine(Folder,"review.txt");

        static MatchVisualReview() { EditorApplication.update+=Tick; }

        public static void Run()
        {
            DirectoryInfo project=Directory.GetParent(Application.dataPath);
            if (project.Name!="AppearancePreview" || project.Parent.Name!="Logs")
                throw new InvalidOperationException("Review is restricted to the disposable appearance project.");
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Report,"Graphics-enabled Unity Play Mode match review\n");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Key+"active",true); SessionState.SetBool(Key+"finishing",false);
            SessionState.SetBool(Key+"success",false); SessionState.SetInt(Key+"stage",0); SessionState.SetInt(Key+"frame",-1);
            SessionState.SetFloat(Key+"started",(float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }

        private static T Find<T>() where T:UnityEngine.Object
        {
            foreach (T item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (item is Component component && component.GetComponentInParent<GoalReplayWorld>(true) != null) continue;
                return item;
            }
            return null;
        }
        private static T Read<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void Check(bool result,string message)
        {
            File.AppendAllText(Report,(result ? "PASS ":"FAIL ")+message+"\n");
            if (!result) throw new InvalidOperationException(message);
        }
        private static void Stage(int stage) { SessionState.SetInt(Key+"stage",stage); }
        private static void Click(MatchMenu menu,string name)
        {
            foreach (Button button in menu.GetComponentsInChildren<Button>())
                if (button.name==name && button.IsActive() && button.IsInteractable()) { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("No active button: "+name);
        }

        private static void Finish(bool success,string message)
        {
            if (gc.Valid) gc.Dispose();
            File.AppendAllText(Report,(success ? "COMPLETE ":"FAILED ")+message+"\n");
            SessionState.SetBool(Key+"success",success); SessionState.SetBool(Key+"finishing",true);
            EditorApplication.ExitPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key+"active",false)) return;
            if (SessionState.GetBool(Key+"finishing",false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key+"active",false);
                EditorApplication.Exit(SessionState.GetBool(Key+"success",false) ? 0:1); return;
            }
            try
            {
                if (EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"started",0)>100)
                    throw new TimeoutException("Match review timed out at stage "+SessionState.GetInt(Key+"stage",0));
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || SessionState.GetInt(Key+"frame",-1)==Time.frameCount) return;
                SessionState.SetInt(Key+"frame",Time.frameCount);
                MatchManager manager=Find<MatchManager>(); MatchMenu menu=Find<MatchMenu>();
                PlayerController player=Find<PlayerController>(); Ball ball=Find<Ball>();
                if (manager==null || menu==null || player==null || ball==null) return;
                string phase=Read<object>(manager,"phase").ToString();
                GoalReplayRecorder recorder=Find<GoalReplayRecorder>();
                int stage=SessionState.GetInt(Key+"stage",0);
                switch (stage)
                {
                    case 0:
                        if (!manager.IsInMainMenu) return;
                        Application.runInBackground=true; Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
                        menu.AdvanceMotion(0.7f);
                        Capture("menu-1080p.png",1920,1080,menu.GetComponent<Canvas>());
                        Capture("menu-720p.png",1280,720,menu.GetComponent<Canvas>());
                        Capture("menu-1440p.png",2560,1440,menu.GetComponent<Canvas>());
                        Check(menu.GetComponentsInChildren<SharpText>().Length>8,"Main menu renders dynamic sharp glyphs at 720p, 1080p and 1440p");
                        manager.StartMatch(); Stage(1); break;
                    case 1:
                        if (phase!="Playing") return;
                        player.GetComponent<HumanPlayerInput>().enabled=false;
                        gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
                        sampleFrames=0; frameTimes.Clear(); allocations.Clear(); Stage(2); break;
                    case 2:
                        sampleFrames++;
                        player.SetMove(sampleFrames<100 ? Vector2.up : sampleFrames<200 ? Vector2.right:Vector2.down);
                        player.SetSprint(sampleFrames>=120);
                        if (sampleFrames>35 && skipFrames==0)
                        {
                            frameTimes.Add(Time.unscaledDeltaTime*1000.0);
                            if (gc.Valid) allocations.Add(gc.LastValue);
                        }
                        if (skipFrames>0) skipFrames--;
                        if (sampleFrames==80 || sampleFrames==180)
                        {
                            Capture(sampleFrames==80 ? "jogging.png":"sprinting.png",1920,1080,Find<MatchHud>().GetComponent<Canvas>());
                            skipFrames=3;
                        }
                        if (sampleFrames<260) return;
                        Check(player.Body.position.sqrMagnitude>1,"Real match advances player physics and running/sprint animation");
                        frameTimes.Sort(); allocations.Sort();
                        File.AppendAllText(Report,$"EDITOR FRAME TIME: median {frameTimes[frameTimes.Count/2]:F2} ms; p95 {frameTimes[(int)(frameTimes.Count*0.95)]:F2} ms; {frameTimes.Count} samples.\n");
                        if (allocations.Count>0) File.AppendAllText(Report,$"EDITOR GC ALLOCATED/FRAME: median {allocations[allocations.Count/2]} bytes; p95 {allocations[(int)(allocations.Count*0.95)]} bytes. Includes Unity editor overhead.\n");
                        File.AppendAllText(Report,"GRAPHICS: "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n");
                        File.AppendAllText(Report,$"LIVE VIEW: {Screen.width} x {Screen.height}; captures separately rendered at their stated resolution.\n");
                        if (gc.Valid) gc.Dispose();
                        player.SetMove(Vector2.zero); player.SetSprint(false);
                        player.GetComponent<PlayerMovement>().ResetTo(new Vector2(8,0.4f),-90);
                        ball.ResetTo(player.Body.position+Vector2.right*0.64f);
                        Read<PossessionSystem>(manager,"possession").TryAcquire(player);
                        player.BeginKick(PlayerKicker.KickType.Shot);
                        Set(player.GetComponent<PlayerKicker>(),"chargeStarted",Time.time-1f);
                        player.ReleaseKick(PlayerKicker.KickType.Shot);
                        Stage(3); break;
                    case 3:
                        if (Read<int>(manager,"blueScore")!=1) return;
                        goalStarted=Time.time; flashCaptured=false; reactionVerified=false; Stage(4); break;
                    case 4:
                        if (!flashCaptured && Find<StadiumPhotographers>().PeakIntensity>0.35f)
                        {
                            Capture("blue-goal-flash.png",1920,1080,Find<MatchHud>().GetComponent<Canvas>());
                            Capture("scorer-emote.png",1200,900,Find<MatchHud>().GetComponent<Canvas>(),player.Body.position);
                            flashCaptured=true;
                        }
                        if (Time.time-goalStarted<1.2f) return;
                        if (!reactionVerified) Check(flashCaptured && Find<StadiumCrowd>().IsCelebrating && player.GetComponent<PlayerAnimator>().IsCelebrating,
                            "Actual charged goal coordinates photographers, supporters and scorer emote");
                        reactionVerified=true;
                        if (recorder.Latest==null || !recorder.SavedToDisk) return;
                        Check(recorder.Latest!=null && recorder.SavedToDisk && recorder.Latest.Duration>1,
                            "Goal capture produces a playable, locally saved replay with the build-up and finish");
                        Capture("replay-saved.png",1920,1080,Find<MatchHud>().GetComponent<Canvas>());
                        manager.PauseMatch(); pausedPosition=player.Body.position;
                        Click(menu,"ReplayLastGoal"); Stage(5); break;
                    case 5:
                        if (Find<GoalReplayView>().PlaybackTime<0.3f) return;
                        Capture("goal-replay.png",1920,1080,menu.GetComponent<Canvas>());
                        Check(menu.CurrentScreen==MatchMenu.Screen.Replay && Time.timeScale==0 && player.Body.position==pausedPosition,
                            "Replay animates independently while live match physics remains paused");
                        GoalReplayView replayView=Find<GoalReplayView>();
                        float replayStart=recorder.Latest.frames[0].time;
                        replayView.SetPlaybackSpeed(2f); replayView.SetShowShotArrow(true);
                        float strike=recorder.Latest.shots[recorder.Latest.shots.Length-1].time-replayStart+0.05f;
                        replayView.Advance(Mathf.Max(0,strike-replayView.PlaybackTime)/2f);
                        replayView.World.Camera.Render();
                        Capture("replay-strike.png",1920,1080,menu.GetComponent<Canvas>());
                        Check(replayView.World.ShotArrowVisible && replayView.PlaybackSpeed==2,
                            "Actual recorded strike displays the optional arrow at 2x playback");
                        float celebration=recorder.Latest.goalTime-replayStart+0.65f;
                        replayView.Advance(Mathf.Max(0,celebration-replayView.PlaybackTime)/2f);
                        replayView.World.Camera.Render();
                        Capture("replay-celebration.png",1920,1080,menu.GetComponent<Canvas>());
                        Check(player.Body.position==pausedPosition && Time.timeScale==0,
                            "Replay goal and celebration preserve the paused live player and clock");
                        Check(menu.TryBack() && menu.CurrentScreen==MatchMenu.Screen.Pause,"Replay Back returns to the pause menu");
                        manager.ResumeMatch(); Stage(6); break;
                    case 6:
                        if (phase!="Playing") return;
                        Check(!player.GetComponent<PlayerAnimator>().IsCelebrating && !Find<GoalConfetti>().IsActive,
                            "Kickoff clears the coordinated celebration and restores gameplay");
                        ball.ResetTo(new Vector2(-8.8f,-0.4f)); ball.RecordTouch(player);
                        ball.Body.linearVelocity=Vector2.left*12f;
                        Stage(7); break;
                    case 7:
                        if (Read<int>(manager,"redScore")!=1) return;
                        goalStarted=Time.time; Stage(8); break;
                    case 8:
                        if (Time.time-goalStarted<0.7f) return;
                        Check(!player.GetComponent<PlayerAnimator>().IsCelebrating,"An own-goal scorer does not celebrate for the opponent");
                        Capture("red-goal-reactions.png",1920,1080,Find<MatchHud>().GetComponent<Canvas>());
                        Find<StadiumFixtures>().SetHaze(0.4f);
                        Capture("hazy-floodlights.png",1920,1080,Find<MatchHud>().GetComponent<Canvas>());
                        Check(Find<StadiumFixtures>().Haze>0.2f,"Atmospheric beams are confined to the hazy lighting variant");
                        Finish(true,"Live match, both teams' reactions, scorer attribution, local replay, UI resolutions and editor performance reviewed.");
                        break;
                }
            }
            catch (Exception exception) { Finish(false,exception.ToString()); }
        }

        private static void Capture(string name,int width,int height,Canvas canvas,Vector2? focus=null)
        {
            Camera camera=Camera.main;
            RenderTexture target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { antiAliasing=4 };
            float aspect=camera.aspect, oldScale=canvas.scaleFactor, oldSize=camera.orthographicSize;
            int sorting=canvas.sortingOrder;
            Vector3 oldPosition=camera.transform.position;
            bool canvasEnabled=canvas.enabled;
            CanvasScaler scaler=canvas.GetComponent<CanvasScaler>(); bool scaling=scaler.enabled;
            RenderMode mode=canvas.renderMode; Camera oldCamera=canvas.worldCamera; float distance=canvas.planeDistance;
            scaler.enabled=false; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            canvas.scaleFactor=height/1080f;
            canvas.sortingOrder=1000;
            camera.targetTexture=target; camera.aspect=(float)width/height;
            camera.GetComponent<CameraFit>().SendMessage("LateUpdate");
            if (focus.HasValue)
            {
                camera.transform.position=new Vector3(focus.Value.x,focus.Value.y,-10);
                camera.orthographicSize=0.95f; canvas.enabled=false;
            }
            Canvas.ForceUpdateCanvases();
            foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>()) graphic.Rebuild(CanvasUpdate.PreRender);
            camera.Render();
            RenderTexture previous=RenderTexture.active; RenderTexture.active=target;
            Texture2D pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Folder,name),pixels.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null; camera.aspect=aspect;
            camera.orthographicSize=oldSize; canvas.sortingOrder=sorting;
            camera.transform.position=oldPosition; canvas.enabled=canvasEnabled;
            canvas.renderMode=mode; canvas.worldCamera=oldCamera; canvas.planeDistance=distance;
            canvas.scaleFactor=oldScale; scaler.enabled=scaling; Canvas.ForceUpdateCanvases();
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
