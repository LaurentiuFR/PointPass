using System;
using PointPass.Gameplay;
using PointPass.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Transport controls use the same recorded timeline as every replay animation and event.</summary>
    public static class ReplayControlsValidation
    {
        public static void Run(Action<bool, string> check)
        {
            GameObject panel = new GameObject("Replay transport validation", typeof(RectTransform), typeof(Canvas), typeof(GoalReplayView));
            try
            {
                GoalReplayView view = panel.GetComponent<GoalReplayView>();
                view.Build(); view.Play(ReplayValidation.Clip());
                check(!view.ReplayIsPaused && Mathf.Approximately(view.PlaybackDuration, 4),
                    "A newly opened highlight starts playing and exposes its recorded duration");
                view.SetPlaybackSpeed(1.5f); view.SetShowShotArrow(true); view.Advance(1.4f);
                view.SetPaused(true);
                float pausedTime = view.PlaybackTime;
                Vector2 ball = view.World.BallPosition;
                Quaternion arm = view.World.transform.Find("Player/LeftArm").localRotation;
                bool arrow = view.World.ShotArrowVisible;
                view.Advance(1);
                check(view.PlaybackTime == pausedTime && view.World.BallPosition == ball
                    && view.World.transform.Find("Player/LeftArm").localRotation == arm && view.World.ShotArrowVisible == arrow,
                    "Pause freezes the recorded playhead, ball, articulated pose and strike-arrow window together");
                view.SetPlaybackSpeed(0.5f);
                check(view.PlaybackTime == pausedTime && view.ReplayIsPaused && view.ShowShotArrow,
                    "Changing replay speed while paused preserves its position and independent arrow preference");
                view.SetShowShotArrow(false);
                check(view.ReplayIsPaused && view.PlaybackSpeed == 0.5f,
                    "Changing the shot-arrow display does not resume paused playback");
                view.Restart();
                check(view.PlaybackTime == 0 && view.ReplayIsPaused && view.PlaybackSpeed == 0.5f && !view.ShowShotArrow,
                    "Restart returns to the opening frame while preserving speed, arrow and pause preferences");
                Text elapsed = panel.transform.Find("ReplayElapsed").GetComponent<Text>();
                check(elapsed.text == "00:00.0 / 00:04.0", "Elapsed replay display resets to zero against the recorded duration");
                panel.transform.Find("PauseReplay").GetComponent<Button>().onClick.Invoke();
                view.Advance(0.4f);
                check(!view.ReplayIsPaused && Mathf.Abs(view.PlaybackTime - 0.2f) < 0.001f,
                    "Resume button continues from the current frame at the selected half speed");
                view.SetPlaybackSpeed(2); view.Advance(1.6f);
                StadiumCrowd crowd = view.World.GetComponentInChildren<StadiumCrowd>();
                Mesh crowdMesh = crowd.transform.Find("Spectators").GetComponent<MeshFilter>().sharedMesh;
                view.SetPaused(true); Vector3[] celebration = crowdMesh.vertices;
                view.Advance(3);
                check(crowd.IsCelebrating && Same(celebration, crowdMesh.vertices),
                    "Pausing during the goal celebration freezes the crowd event timeline");
                panel.transform.Find("RestartReplay").GetComponent<Button>().onClick.Invoke();
                check(view.PlaybackTime == 0 && view.ReplayIsPaused && !crowd.IsCelebrating,
                    "Restart clears post-goal presentation events without resuming a paused replay");
                view.Play(ReplayValidation.Clip());
                check(!view.ReplayIsPaused && view.PlaybackTime == 0 && view.PlaybackSpeed == 1 && !view.ShowShotArrow,
                    "Opening a new highlight restores playing, 1x and arrow-off defaults");
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
        }

        private static bool Same(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
