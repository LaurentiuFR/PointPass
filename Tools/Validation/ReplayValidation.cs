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
    public static class ReplayValidation
    {
        public static void Run(Action<bool, string> check)
        {
            GoalReplayClip clip = Clip();
            check(GoalReplayRecorder.IsValid(clip), "Replay accepts finite ordered pose samples and strike metadata");
            GameObject panel = new GameObject("Replay validation", typeof(RectTransform), typeof(Canvas), typeof(GoalReplayView));
            try
            {
                GoalReplayView view = panel.GetComponent<GoalReplayView>(); view.Build(); view.Play(clip);
                check(view.PlaybackSpeed == 1 && !view.ShowShotArrow && view.PlaybackTime == 0,
                    "A new replay defaults to 1x with shot arrow off");
                check(view.World.GetComponentsInChildren<GoalNet>().Length == 2
                    && view.World.GetComponentInChildren<StadiumFixtures>() != null
                    && view.World.GetComponentInChildren<StadiumCrowd>() != null,
                    "Replay uses the full pitch, both goals and nets, stadium and crowd");
                bool isolated = true;
                foreach (Collider2D collider in view.World.GetComponentsInChildren<Collider2D>()) isolated &= !collider.enabled;
                foreach (Rigidbody2D body in view.World.GetComponentsInChildren<Rigidbody2D>()) isolated &= !body.simulated;
                check(isolated, "Replay has no active physics or goal triggers");
                float gameTime = Time.timeScale;
                foreach (float speed in new[] { 0.5f, 1f, 1.5f, 2f })
                {
                    float before = view.PlaybackTime;
                    view.SetPlaybackSpeed(speed); view.Advance(0.1f);
                    check(Mathf.Abs(view.PlaybackTime - before - speed * 0.1f) < 0.0001f,
                        "Replay speed " + speed + "x advances one continuous recorded timeline");
                }
                check(Time.timeScale == gameTime, "Replay speeds never alter the live match time scale");
                view.SetPlaybackSpeed(float.NaN);
                check(view.PlaybackSpeed == 2, "Unsupported or invalid replay speed is ignored");
                view.World.Present(1.5f, false);
                check(Vector2.Distance(view.World.BallPosition, new Vector2(7, 0.5f)) < 0.001f,
                    "Replay ball interpolates the recorded curved path without physics resimulation");
                Transform arm = view.World.transform.Find("Player/LeftArm");
                check(arm != null && Mathf.Abs(arm.localPosition.y - 0.15f) < 0.0001f,
                    "Replay interpolates recorded articulated arm poses");
                view.World.Present(2.01f, true);
                check(view.World.ShotArrowVisible, "Optional shot arrow appears at the recorded strike");
                view.World.Present(3.1f, true);
                check(!view.World.ShotArrowVisible, "Shot arrow does not remain attached to the moving ball");
                view.SetShowShotArrow(true); view.SetPlaybackSpeed(0.5f);
                check(view.ShowShotArrow, "Changing playback speed preserves the independent arrow setting");
                view.SetShowShotArrow(false);
                check(view.PlaybackSpeed == 0.5f, "Changing the arrow toggle preserves playback speed");
                view.Play(clip);
                check(view.PlaybackTime == 0 && view.PlaybackSpeed == 1 && !view.ShowShotArrow,
                    "Opening another replay resets its controls to the requested defaults");
                panel.SetActive(false);
                if (!Application.isPlaying) Call(view, "OnDisable");
                check(!view.World.Camera.enabled, "Closing the replay disables its render camera");
            }
            finally { UnityEngine.Object.DestroyImmediate(panel); }
            clip.frames[1].poses[0].bones[0].angle = float.NaN;
            check(!GoalReplayRecorder.IsValid(clip), "Saved replay validation rejects nonfinite animation poses");
            RecordedStrike(check);
        }

        private static void RecordedStrike(Action<bool, string> check)
        {
            GameObject root = new GameObject("Recorded strike validation");
            try
            {
                GameObject ballObject = new GameObject("Ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                ballObject.transform.SetParent(root.transform);
                Ball ball = ballObject.GetComponent<Ball>(); Call(ball, "Awake"); ball.Body.gravityScale = 0;
                Transform ballSprite = new GameObject("Sprite").transform; ballSprite.SetParent(ball.transform, false);
                ballSprite.localRotation = Quaternion.Euler(0, 0, 37);
                GameObject playerObject = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
                playerObject.transform.SetParent(root.transform);
                PlayerController player = playerObject.GetComponent<PlayerController>();
                Dribbler dribbler = playerObject.GetComponent<Dribbler>(); PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>();
                Call(dribbler, "Awake"); Call(kicker, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null); kicker.Init(ball, possession);
                player.Init(Team.Blue, Vector2.zero, -90); player.Body.rotation = -90;
                Transform arm = new GameObject("Arm").transform; arm.SetParent(player.transform, false);
                arm.localPosition = new Vector2(0.2f, 0.4f);
                GoalReplayRecorder recorder = root.AddComponent<GoalReplayRecorder>();
                recorder.Init(ball, new List<PlayerController> { player }, false);
                recorder.SaveDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../PR2Verification/StrikeReplay"));
                recorder.SetRecording(true);
                for (int i = 0; i < 30; i++) recorder.Advance(0.05f);
                possession.TryAcquire(player); player.BeginKick(PlayerKicker.KickType.Shot);
                player.ReleaseKick(PlayerKicker.KickType.Shot); Call(kicker, "FixedUpdate");
                recorder.Advance(0.05f); recorder.BeginGoal(Team.Blue); recorder.SetRecording(false);
                for (int i = 0; i < 65; i++) recorder.Advance(0.05f);
                GoalReplayClip captured = recorder.Latest;
                check(captured != null && captured.shots.Length == 1 && captured.shots[0].player == 0
                    && Vector2.Distance(captured.shots[0].direction, Vector2.right) < 0.001f,
                    "Physical shot execution records the actual striker and velocity direction");
                check(captured.frames[captured.frames.Length - 1].time - captured.goalTime >= MatchConfig.GoalPauseSeconds - 0.01f,
                    "Highlight capture includes the full goal celebration tail");
                check(Mathf.Abs(captured.frames[0].ballAngle - 37) < 0.001f
                    && captured.frames[0].poses[0].bones[0].position == new Vector2(0.2f, 0.4f),
                    "Recorder stores articulated local poses and actual ball roll");
                recorder.ResetHistory(); arm.localPosition = Vector2.one * 99; recorder.SetRecording(true); recorder.Advance(0.05f);
                check(captured.frames[0].poses[0].bones[0].position == new Vector2(0.2f, 0.4f),
                    "Saved articulated poses are independent of reused history buffers");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        public static GoalReplayClip Clip()
        {
            var clip = new GoalReplayClip { teams = new[] { Team.Blue }, scorer = 0, scoringTeam = Team.Blue,
                goalTime = 3, rigs = new[] { new GoalReplayClip.ActorRig { paths = new[] { "LeftArm" } } },
                shots = new[] { new GoalReplayClip.Shot { time = 2, player = 0, origin = new Vector2(8, 0), direction = Vector2.right } },
                frames = new GoalReplayClip.Frame[5] };
            for (int i = 0; i < clip.frames.Length; i++)
                clip.frames[i] = new GoalReplayClip.Frame { time = i, ball = new Vector2(4 + i * 2, i == 1 ? 1 : 0), ballAngle = i * 45,
                    players = new[] { new Vector2(3 + i, 0) }, angles = new[] { -90f },
                    poses = new[] { new GoalReplayClip.ActorPose { bones = new[] { new GoalReplayClip.BonePose
                        { position = new Vector2(-0.3f, i * 0.1f), scale = Vector2.one, angle = i * 10, order = 4 } } } } };
            return clip;
        }
    }
}
