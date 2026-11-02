using System;
using System.Collections.Generic;
using System.IO;
using PointPass.Core;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Preallocated 20 Hz history: five seconds before the goal and one second afterwards.</summary>
    public class GoalReplayRecorder : MonoBehaviour
    {
        private const int Capacity=120;
        private readonly GoalReplayClip.Frame[] history=new GoalReplayClip.Frame[Capacity];
        private Ball ball;
        private List<PlayerController> roster;
        private int head,count;
        private float sampleClock,recordedTime,goalAt;
        private bool pending,recording;
        private Team scoringTeam;
        public GoalReplayClip Latest { get; private set; }
        public bool SavedToDisk { get; private set; }
        public string SaveDirectory { get; set; }
        public event Action<GoalReplayClip> ClipReady;

        public void Init(Ball matchBall,List<PlayerController> players,bool restoreSaved=true)
        {
            ball=matchBall; roster=players;
            for (int i=0;i<Capacity;i++)
            {
                history[i].players=new Vector2[players.Count];
                history[i].angles=new float[players.Count];
            }
            SaveDirectory=Application.isEditor ? Path.Combine(Application.dataPath,"../Logs/Highlights")
                : Path.Combine(Application.persistentDataPath,"Highlights");
            if (restoreSaved) RestoreLatest();
        }

        private void RestoreLatest()
        {
            try
            {
                string path=Path.Combine(SaveDirectory,"last-goal.json");
                if (!File.Exists(path) || new FileInfo(path).Length>512000) return;
                GoalReplayClip clip=JsonUtility.FromJson<GoalReplayClip>(File.ReadAllText(path));
                if (clip?.frames==null || clip.frames.Length<2 || clip.frames.Length>Capacity
                    || clip.teams==null || clip.teams.Length<1 || clip.teams.Length>16) return;
                float previous=-float.MaxValue;
                foreach (GoalReplayClip.Frame frame in clip.frames)
                {
                    if (float.IsNaN(frame.time) || float.IsInfinity(frame.time) || frame.time<=previous
                        || frame.players==null || frame.players.Length!=clip.teams.Length
                        || frame.angles==null || frame.angles.Length!=clip.teams.Length) return;
                    previous=frame.time;
                    if (!Finite(frame.ball)) return;
                    for (int i=0;i<frame.players.Length;i++)
                        if (!Finite(frame.players[i]) || float.IsNaN(frame.angles[i]) || float.IsInfinity(frame.angles[i])) return;
                }
                Latest=clip; SavedToDisk=true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { Debug.LogWarning("Could not restore the previous goal replay: "+exception.Message); }
        }

        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);

        public void SetRecording(bool active) => recording=active;
        public void BeginGoal(Team team) { scoringTeam=team; goalAt=recordedTime; pending=true; SavedToDisk=false; }
        private void LateUpdate() => Advance(Time.deltaTime);

        public void Advance(float seconds)
        {
            if (ball==null || roster==null || seconds<=0 || (!recording && !pending)) return;
            recordedTime+=seconds; sampleClock+=seconds;
            if (sampleClock<0.05f) return;
            sampleClock%=0.05f;
            ref GoalReplayClip.Frame frame=ref history[head];
            frame.time=recordedTime; frame.ball=ball.Body.position;
            for (int i=0;i<roster.Count;i++)
            {
                frame.players[i]=roster[i].Body.position;
                frame.angles[i]=roster[i].Body.rotation;
            }
            head=(head+1)%Capacity; count=Mathf.Min(Capacity,count+1);
            if (pending && recordedTime-goalAt>=1f) FinishClip();
        }

        private void FinishClip()
        {
            pending=false;
            GoalReplayClip clip=new GoalReplayClip { scoringTeam=scoringTeam, goalTime=goalAt,
                teams=new Team[roster.Count], frames=new GoalReplayClip.Frame[count] };
            for (int i=0;i<roster.Count;i++) clip.teams[i]=roster[i].Team;
            for (int i=0;i<count;i++)
            {
                GoalReplayClip.Frame frame=history[(head-count+i+Capacity)%Capacity];
                frame.players=(Vector2[])frame.players.Clone(); frame.angles=(float[])frame.angles.Clone();
                clip.frames[i]=frame;
            }
            Latest=clip;
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(Path.Combine(SaveDirectory,"last-goal.json"),JsonUtility.ToJson(clip));
                SavedToDisk=true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Goal replay is available for this session; local save failed: "+exception.Message);
            }
            ClipReady?.Invoke(clip);
        }

        public void ResetHistory()
        {
            count=head=0; sampleClock=recordedTime=0; pending=recording=false;
        }
    }
}
