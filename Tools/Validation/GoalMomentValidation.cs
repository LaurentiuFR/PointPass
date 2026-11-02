using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    public static class GoalMomentValidation
    {
        public static void Run(Action<bool,string> check)
        {
            GameObject root=new GameObject("Goal moment validation");
            try
            {
                StadiumBuilder.Build(root.transform);
                StadiumPhotographers photographers=root.GetComponentInChildren<StadiumPhotographers>();
                photographers.Celebrate(); photographers.Advance(0.16f);
                check(photographers.PeakIntensity>0.95f && Shader.GetGlobalVector("_PointPassFlash").z>0.95f,
                    "A photographer's flash briefly illuminates nearby turf");
                float peak=photographers.PeakIntensity;
                photographers.Advance(0);
                check(photographers.PeakIntensity==peak,"User pause preserves the coordinated flash timeline");
                photographers.ResetFlashes();
                check(photographers.PeakIntensity==0 && Shader.GetGlobalVector("_PointPassFlash")==Vector4.zero,
                    "Reset clears flash reflections without changing sustained stadium lighting");
                StadiumFixtures fixtures=root.GetComponentInChildren<StadiumFixtures>();
                check(!fixtures.transform.Find("AtmosphericBeams").GetComponent<MeshRenderer>().enabled,
                    "Clear conditions show illuminated turf without visible light beams");
                fixtures.SetHaze(0.5f);
                check(fixtures.transform.Find("AtmosphericBeams").GetComponent<MeshRenderer>().enabled,
                    "Atmospheric beams become visible only when haze is enabled");
                StadiumCrowd crowd=root.GetComponentInChildren<StadiumCrowd>();
                Mesh crowdMesh=crowd.transform.Find("Spectators").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] seated=crowdMesh.vertices;
                crowd.Celebrate(Team.Blue); crowd.Advance(0.7f);
                Vector3[] reacted=crowdMesh.vertices;
                float blueY=0,redY=0; int blues=0,reds=0;
                for (int i=0;i<seated.Length;i++)
                    if (seated[i].x<0) { blueY+=reacted[i].y-seated[i].y; blues++; }
                    else { redY+=reacted[i].y-seated[i].y; reds++; }
                check(blues>0 && reds>0 && blueY/blues>redY/reds,
                    "Scoring supporters rise and cheer while opposing sections slump or hold their heads");

                GameObject ballObject=new GameObject("Replay ball",typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(Ball));
                ballObject.transform.SetParent(root.transform);
                Ball ball=ballObject.GetComponent<Ball>(); Call(ball,"Awake"); ball.Body.gravityScale=0;
                GameObject playerObject=new GameObject("Replay scorer",typeof(Rigidbody2D),typeof(PlayerMovement),typeof(PlayerController),typeof(PlayerAnimator));
                playerObject.transform.SetParent(root.transform);
                PlayerController player=playerObject.GetComponent<PlayerController>(); player.Init(Team.Blue,Vector2.zero,0);
                GoalReplayRecorder recorder=root.AddComponent<GoalReplayRecorder>();
                recorder.Init(ball,new List<PlayerController>{player},false);
                recorder.SaveDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../PR2Verification/Replay"));
                recorder.SetRecording(true);
                for (int i=0;i<80;i++) { ball.Body.position=new Vector2(i*0.1f,0); player.Body.position=new Vector2(i*0.08f,0); recorder.Advance(0.05f); }
                recorder.BeginGoal(Team.Blue); recorder.SetRecording(false);
                recorder.Advance(0);
                check(recorder.Latest==null,"Replay capture does not claim a saved highlight before the post-goal tail completes");
                for (int i=0;i<22;i++) recorder.Advance(0.05f);
                GoalReplayClip clip=recorder.Latest;
                check(clip!=null && clip.frames.Length<=120 && clip.Duration>4,
                    "Replay contains bounded pre-goal history and the ball's post-goal finish");
                check(recorder.SavedToDisk && File.Exists(Path.Combine(recorder.SaveDirectory,"last-goal.json")),
                    "Replay saved cue is backed by an actual local clip file");
                GoalReplayRecorder restored=new GameObject("Restored replay",typeof(GoalReplayRecorder)).GetComponent<GoalReplayRecorder>();
                restored.transform.SetParent(root.transform); restored.Init(ball,new List<PlayerController>{player},false);
                restored.SaveDirectory=recorder.SaveDirectory; Call(restored,"RestoreLatest");
                check(restored.Latest!=null && restored.SavedToDisk && restored.Latest.frames.Length==clip.frames.Length,
                    "Saved goal replay can be restored for a later game session");
                Vector2 saved=clip.frames[0].players[0];
                recorder.ResetHistory(); recorder.SetRecording(true); player.Body.position=Vector2.one*99; recorder.Advance(0.1f);
                check(clip.frames[0].players[0]==saved,"New match samples cannot mutate an already clipped highlight");
                GameObject replayPanel=new GameObject("Replay viewer",typeof(RectTransform),typeof(Canvas),typeof(GoalReplayView));
                replayPanel.transform.SetParent(root.transform);
                GoalReplayView view=replayPanel.GetComponent<GoalReplayView>(); view.Build(); view.Play(clip);
                Vector2 livePosition=player.Body.position; view.Advance(0.3f);
                check(view.PlaybackTime>0.29f && player.Body.position==livePosition,
                    "Replay playback advances its own view without modifying the live player");

                PlayerAnimator animator=player.GetComponent<PlayerAnimator>(); Call(animator,"Awake");
                Transform torso=new GameObject("Celebrating torso").transform; torso.SetParent(player.transform,false);
                animator.Init(null,null,torso); player.Body.simulated=false;
                animator.CelebrateGoal(); animator.AdvanceCelebration(0.3f);
                check(animator.IsCelebrating && Quaternion.Angle(torso.localRotation,Quaternion.identity)>1
                    && player.Body.position==livePosition,"Scorer emote animates the frozen player's visuals without moving physics");
                animator.ClearCelebration();
                check(!animator.IsCelebrating && torso.localRotation==Quaternion.identity,"Kickoff clears the personal scorer celebration");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Call(object target,string method) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    }
}
