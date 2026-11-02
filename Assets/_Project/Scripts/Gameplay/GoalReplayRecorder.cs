using System;
using System.Collections.Generic;
using System.IO;
using PointPass.Core;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Bounded 20 Hz pose history. Playback never re-simulates football physics.</summary>
    [DefaultExecutionOrder(1000)]
    public class GoalReplayRecorder : MonoBehaviour
    {
        public const int Capacity = 240;
        private const float SampleInterval = 0.05f;
        private readonly GoalReplayClip.Frame[] history = new GoalReplayClip.Frame[Capacity];
        private readonly List<GoalReplayClip.Shot> shots = new List<GoalReplayClip.Shot>();
        private readonly List<GoalReplayClip.NetImpact> impacts = new List<GoalReplayClip.NetImpact>();
        private readonly List<Action> unsubscribe = new List<Action>();
        private Ball ball;
        private List<PlayerController> roster;
        private Transform[][] bones;
        private SpriteRenderer[][] renderers;
        private GoalReplayClip.ActorRig[] rigs;
        private Transform ballVisual;
        private int head, count, scorer = -1;
        private float sampleClock, recordedTime, goalAt;
        private bool pending, recording;
        private Team scoringTeam;
        public GoalReplayClip Latest { get; private set; }
        public bool SavedToDisk { get; private set; }
        public string SaveDirectory { get; set; }
        public event Action<GoalReplayClip> ClipReady;

        public void Init(Ball matchBall, List<PlayerController> players, bool restoreSaved = true)
        {
            Unsubscribe();
            ball = matchBall; roster = players; ballVisual = ball.transform.Find("Sprite");
            bones = new Transform[players.Count][]; renderers = new SpriteRenderer[players.Count][];
            rigs = new GoalReplayClip.ActorRig[players.Count];
            for (int p = 0; p < players.Count; p++)
            {
                var children = new List<Transform>();
                foreach (Transform child in players[p].GetComponentsInChildren<Transform>(true))
                    if (child != players[p].transform && !child.name.StartsWith("FloodlightShadow")) children.Add(child);
                bones[p] = children.ToArray(); renderers[p] = new SpriteRenderer[children.Count];
                rigs[p].paths = new string[children.Count];
                for (int b = 0; b < children.Count; b++)
                {
                    rigs[p].paths[b] = RelativePath(players[p].transform, children[b]);
                    renderers[p][b] = children[b].GetComponent<SpriteRenderer>();
                }
                int index = p;
                PlayerKicker kicker = players[p].GetComponent<PlayerKicker>();
                if (kicker != null)
                {
                    Action<PlayerKicker.KickType> handler = type => { if (type == PlayerKicker.KickType.Shot) RecordShot(index); };
                    kicker.Kicked += handler; unsubscribe.Add(() => { if (kicker != null) kicker.Kicked -= handler; });
                }
            }
            foreach (GoalNet net in transform.GetComponentsInChildren<GoalNet>())
            {
                Action<Vector2, float> handler = (point, speed) =>
                {
                    if (recording || pending) impacts.Add(new GoalReplayClip.NetImpact
                        { time = recordedTime, point = point, speed = speed, side = net.Side });
                };
                net.Impacted += handler; unsubscribe.Add(() => { if (net != null) net.Impacted -= handler; });
            }
            for (int i = 0; i < Capacity; i++)
            {
                history[i].players = new Vector2[players.Count]; history[i].angles = new float[players.Count];
                history[i].poses = new GoalReplayClip.ActorPose[players.Count];
                for (int p = 0; p < players.Count; p++) history[i].poses[p].bones = new GoalReplayClip.BonePose[bones[p].Length];
            }
            SaveDirectory = Application.isEditor ? Path.Combine(Application.dataPath, "../Logs/Highlights")
                : Path.Combine(Application.persistentDataPath, "Highlights");
            if (restoreSaved) RestoreLatest();
        }

        private static string RelativePath(Transform root, Transform child)
        {
            string path = child.name;
            for (Transform parent = child.parent; parent != root; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private void RestoreLatest()
        {
            try
            {
                string path = Path.Combine(SaveDirectory, "last-goal.json");
                if (!File.Exists(path) || new FileInfo(path).Length > 16000000) return;
                GoalReplayClip clip = JsonUtility.FromJson<GoalReplayClip>(File.ReadAllText(path));
                if (!IsValid(clip)) return;
                Latest = clip; SavedToDisk = true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { Debug.LogWarning("Could not restore the previous goal replay: " + exception.Message); }
        }

        public static bool IsValid(GoalReplayClip clip)
        {
            if (clip?.frames == null || clip.frames.Length < 2 || clip.frames.Length > Capacity
                || clip.teams == null || clip.teams.Length < 1 || clip.teams.Length > 16 || !Finite(clip.goalTime)) return false;
            float previous = -float.MaxValue;
            foreach (GoalReplayClip.Frame frame in clip.frames)
            {
                if (!Finite(frame.time) || frame.time <= previous || !Finite(frame.ball) || !Finite(frame.ballAngle)
                    || frame.players == null || frame.players.Length != clip.teams.Length
                    || frame.angles == null || frame.angles.Length != clip.teams.Length) return false;
                previous = frame.time;
                for (int i = 0; i < frame.players.Length; i++)
                    if (!Finite(frame.players[i]) || !Finite(frame.angles[i])) return false;
                if (frame.poses == null) continue; // Legacy clips contain position/heading only.
                if (clip.rigs == null || clip.rigs.Length != clip.teams.Length || frame.poses.Length != clip.teams.Length) return false;
                for (int p = 0; p < frame.poses.Length; p++)
                {
                    if (clip.rigs[p].paths == null || clip.rigs[p].paths.Length > 64 || frame.poses[p].bones == null
                        || frame.poses[p].bones.Length != clip.rigs[p].paths.Length) return false;
                    foreach (GoalReplayClip.BonePose bone in frame.poses[p].bones)
                        if (!Finite(bone.position) || !Finite(bone.scale) || !Finite(bone.angle)) return false;
                }
            }
            if (clip.shots != null) foreach (GoalReplayClip.Shot shot in clip.shots)
                if (!Finite(shot.time) || !Finite(shot.origin) || !Finite(shot.direction) || shot.player < 0 || shot.player >= clip.teams.Length) return false;
            if (clip.impacts != null) foreach (GoalReplayClip.NetImpact impact in clip.impacts)
                if (!Finite(impact.time) || !Finite(impact.point) || !Finite(impact.speed) || !Finite(impact.side)) return false;
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        public void SetRecording(bool active) => recording = active;
        public void BeginGoal(Team team)
        {
            scoringTeam = team; goalAt = recordedTime; pending = true; SavedToDisk = false;
            scorer = roster != null ? roster.IndexOf(ball.LastTouch) : -1;
        }
        private void LateUpdate() => Advance(Time.deltaTime);

        private void RecordShot(int index)
        {
            if (!recording || ball == null) return;
            shots.Add(new GoalReplayClip.Shot { time = recordedTime, player = index,
                origin = ball.Body.position, direction = ball.Body.linearVelocity.normalized });
            shots.RemoveAll(shot => shot.time < recordedTime - 12f);
        }
        public void Advance(float seconds)
        {
            if (ball == null || roster == null || seconds <= 0 || (!recording && !pending)) return;
            // Script hot reload can preserve Unity object references while discarding the
            // nonserialized pose cache. Resume a clean buffer instead of dereferencing it.
            if (bones == null || bones.Length != roster.Count || history[0].poses == null)
            {
                bool resume = recording;
                Init(ball, roster, false); ResetHistory(); recording = resume;
                if (!recording) return;
            }
            recordedTime += seconds; sampleClock += seconds;
            if (sampleClock < SampleInterval) return;
            sampleClock %= SampleInterval;
            CaptureFrame();
            impacts.RemoveAll(impact => impact.time < recordedTime - 12f);
            if (pending && recordedTime - goalAt >= MatchConfig.GoalPauseSeconds) FinishClip();
        }
        private void CaptureFrame()
        {
            ref GoalReplayClip.Frame frame = ref history[head];
            frame.time = recordedTime; frame.ball = ball.Body.position;
            frame.ballAngle = ballVisual != null ? ballVisual.localEulerAngles.z : 0;
            for (int p = 0; p < roster.Count; p++)
            {
                frame.players[p] = roster[p].Body.position; frame.angles[p] = roster[p].Body.rotation;
                for (int b = 0; b < bones[p].Length; b++)
                    frame.poses[p].bones[b] = new GoalReplayClip.BonePose { position = bones[p][b].localPosition,
                        scale = bones[p][b].localScale, angle = bones[p][b].localEulerAngles.z,
                        order = renderers[p][b] != null ? renderers[p][b].sortingOrder : 0 };
            }
            head = (head + 1) % Capacity; count = Mathf.Min(Capacity, count + 1);
        }
        public void CompleteGoalCapture()
        {
            if (!pending) return;
            if (count == 0 || recordedTime > history[(head - 1 + Capacity) % Capacity].time) CaptureFrame();
            FinishClip();
        }
        private void FinishClip()
        {
            pending = false;
            int first = (head - count + Capacity) % Capacity;
            float start = Mathf.Max(history[first].time, goalAt - 5f);
            // Include three seconds before the actual strike when a long shot took over two seconds.
            for (int i = shots.Count - 1; i >= 0; i--)
                if (shots[i].time <= goalAt) { start = Mathf.Max(history[first].time, Mathf.Min(start, shots[i].time - 3f)); break; }
            int skip = 0;
            while (skip < count - 2 && history[(first + skip + 1) % Capacity].time < start) skip++;
            GoalReplayClip clip = new GoalReplayClip { scoringTeam = scoringTeam, goalTime = goalAt, scorer = scorer,
                teams = new Team[roster.Count], rigs = rigs, frames = new GoalReplayClip.Frame[count - skip] };
            for (int p = 0; p < roster.Count; p++) clip.teams[p] = roster[p].Team;
            for (int i = skip; i < count; i++)
            {
                GoalReplayClip.Frame frame = history[(first + i) % Capacity];
                frame.players = (Vector2[])frame.players.Clone(); frame.angles = (float[])frame.angles.Clone();
                frame.poses = (GoalReplayClip.ActorPose[])frame.poses.Clone();
                for (int p = 0; p < frame.poses.Length; p++) frame.poses[p].bones = (GoalReplayClip.BonePose[])frame.poses[p].bones.Clone();
                clip.frames[i - skip] = frame;
            }
            float begin = clip.frames[0].time;
            clip.shots = shots.FindAll(shot => shot.time >= begin && shot.time <= recordedTime).ToArray();
            clip.impacts = impacts.FindAll(impact => impact.time >= begin && impact.time <= recordedTime).ToArray();
            Latest = clip;
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(Path.Combine(SaveDirectory, "last-goal.json"), JsonUtility.ToJson(clip)); SavedToDisk = true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            { Debug.LogWarning("Goal replay is available for this session; local save failed: " + exception.Message); }
            ClipReady?.Invoke(clip);
        }
        public void ResetHistory()
        { count = head = 0; sampleClock = recordedTime = 0; pending = recording = false; shots.Clear(); impacts.Clear(); }
        private void Unsubscribe() { foreach (Action action in unsubscribe) action(); unsubscribe.Clear(); }
        private void OnDestroy() => Unsubscribe();
    }
}
