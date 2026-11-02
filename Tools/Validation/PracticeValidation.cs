using System;
using System.Collections.Generic;
using System.Reflection;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Physical gate attempts and the boundary between practice and timed matches.</summary>
    public static class PracticeValidation
    {
        private static void Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        public static void Run(Action<bool, string> check)
        {
            SimulationMode2D mode = Physics2D.simulationMode;
            float scale = Time.timeScale;
            GameObject root = new GameObject("Passing practice validation");
            MatchHud hud = null;
            MatchMenu menu = null;
            try
            {
                Time.timeScale = 1; Physics2D.simulationMode = SimulationMode2D.Script;
                GameObject ballObject = new GameObject("Practice ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                ballObject.transform.SetParent(root.transform);
                Ball ball = ballObject.GetComponent<Ball>(); Call(ball, "Awake");
                ball.Body.gravityScale = 0; ball.Body.linearDamping = 0;
                ballObject.GetComponent<CircleCollider2D>().radius = MatchConfig.BallDiameter * 0.5f;
                GameObject playerObject = new GameObject("Practice player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
                playerObject.transform.SetParent(root.transform);
                PlayerController player = playerObject.GetComponent<PlayerController>();
                player.Body.gravityScale = 0;
                playerObject.GetComponent<CircleCollider2D>().radius = MatchConfig.PlayerColliderRadius;
                Dribbler dribbler = player.GetComponent<Dribbler>(); PlayerKicker kicker = player.GetComponent<PlayerKicker>();
                Call(dribbler, "Awake"); Call(kicker, "Awake");
                var possession = new PossessionSystem();
                dribbler.Init(ball, possession, null); kicker.Init(ball, possession);
                player.Init(Team.Blue, new Vector2(-3, 0), -90);
                PassingPractice practice = root.AddComponent<PassingPractice>(); practice.Init(ball, possession, player);
                practice.BeginSession();
                check(practice.IsRunning && practice.Hits == 0 && practice.Attempts == 0 && practice.ActiveGate == 0,
                    "Practice starts a clean untimed passing session targeting the right gate");
                check(root.transform.Find("PracticeGates").GetComponentsInChildren<Collider2D>().Length == 0,
                    "Practice gates add no colliders or obstruction to physical ball travel");
                check(player.Body.position == Vector2.zero && Vector2.Distance(ball.Body.position, new Vector2(0.68f, 0)) < 0.001f,
                    "Practice begins at midfield with a free ball at the player's feet");
                Vector2 origin = ball.Body.position;
                Kick(player, kicker, possession, PlayerKicker.KickType.Pass, Vector2.right);
                check(practice.Attempts == 1 && ball.Body.position == origin,
                    "An executed pass starts one attempt without repositioning the ball");
                Travel(practice, 24);
                check(practice.Hits == 1 && practice.ActiveGate == 1 && ball.Body.position.x > PassingPractice.GateX,
                    "A real physical pass through the active gate scores and alternates the target");
                Travel(practice, 8);
                check(practice.Hits == 1 && practice.Attempts == 1,
                    "A pass cannot count repeatedly after a gate hit");

                practice.ResetBall();
                int attempts = practice.Attempts;
                Kick(player, kicker, possession, PlayerKicker.KickType.Shot, Vector2.left);
                Travel(practice, 35);
                check(practice.Hits == 1 && practice.Attempts == attempts,
                    "A physical shot crossing a gate cannot count as a passing attempt or hit");
                practice.ResetBall();
                ball.Body.position = new Vector2(-5f, 0); practice.Advance(0.02f);
                check(practice.Hits == 1, "Dribbling or repositioning across a gate cannot score without an executed pass");
                check(PassingPractice.CrossesGate(new Vector2(2, 0.2f), new Vector2(6, 0.2f), 0)
                    && PassingPractice.CrossesGate(new Vector2(-2, -0.2f), new Vector2(-6, -0.2f), 1),
                    "Segment-based gate detection handles fast passes in both directions");
                check(!PassingPractice.CrossesGate(new Vector2(6, 0), new Vector2(2, 0), 0)
                    && !PassingPractice.CrossesGate(new Vector2(-2, 0), new Vector2(-6, 0), 0),
                    "Reverse and inactive-side crossings do not satisfy the selected gate");
                check(!PassingPractice.CrossesGate(new Vector2(2, 1.1f), new Vector2(6, 1.1f), 0),
                    "A pass clipping a gate post fails whole-ball clearance");
                practice.ResetBall();
                possession.TryAcquire(player);
                player.BeginKick(PlayerKicker.KickType.Shot);
                Vector2 position = ball.Body.position;
                player.SetInputEnabled(false); Time.timeScale = 0;
                practice.ResetBall(); practice.Advance(1);
                check(ball.Body.position == position && player.ChargingKick == PlayerKicker.KickType.None,
                    "Paused practice rejects reset and gate updates while cancelling the held charge");
                Time.timeScale = 1; player.SetInputEnabled(true);
                practice.ResetBall();
                check(ball.Body.linearVelocity == Vector2.zero && !possession.HasOwner,
                    "Manual Reset Ball clears flight momentum, charge and ownership");
                practice.EndSession();
                check(!practice.IsRunning && !root.transform.Find("PracticeGates").gameObject.activeSelf,
                    "Leaving practice hides gates and stops observing attempts");

                hud = MatchHud.Create(); menu = MatchMenu.Create();
                MatchManager manager = root.AddComponent<MatchManager>();
                manager.Init(ball, possession, new List<PlayerController> { player }, hud); manager.SetMenu(menu);
                Set(manager, "clock", 63f);
                manager.StartPractice();
                Call(manager, "Update"); manager.OnGoal(Team.Blue);
                GoalReplayRecorder recorder = Get<GoalReplayRecorder>(manager, "replay");
                check(manager.IsPracticing && Get<float>(manager, "clock") == 63f
                    && Get<int>(manager, "blueScore") == 0 && !Get<bool>(recorder, "recording") && !Get<bool>(recorder, "pending"),
                    "Practice neither runs the match clock nor scores goals or saves a match highlight");
                Transform safe = hud.transform.Find("SafeArea");
                check(!safe.Find("Scoreboard").gameObject.activeSelf && !safe.Find("ClockPill").gameObject.activeSelf
                    && safe.Find("PracticeHUD").gameObject.activeSelf,
                    "Practice readout replaces normal match score and clock inside the safe area");
                manager.PauseMatch();
                check(manager.IsUserPaused && Time.timeScale == 0 && !hud.gameObject.activeSelf,
                    "Practice pause uses the existing modal and freezes scaled game time");
                manager.ResumeMatch();
                check(manager.IsPracticing && Time.timeScale == 1 && hud.gameObject.activeSelf && player.IsActive,
                    "Resume returns to the same active practice session");
                manager.OpenMainMenu();
                check(!manager.IsPracticing && manager.IsInMainMenu && !ball.Body.simulated
                    && safe.Find("Scoreboard").gameObject.activeSelf && safe.Find("ClockPill").gameObject.activeSelf,
                    "Practice exit restores normal HUD state and leaves the main menu frozen");
            }
            finally
            {
                Time.timeScale = scale; Physics2D.simulationMode = mode;
                UnityEngine.Object.DestroyImmediate(root);
                if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
                if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
            }
        }
        private static void Kick(PlayerController player, PlayerKicker kicker, PossessionSystem possession,
            PlayerKicker.KickType type, Vector2 direction)
        {
            player.SetMove(direction); possession.TryAcquire(player);
            player.BeginKick(type); Set(kicker, "chargeStarted", Time.time - 1f);
            player.ReleaseKick(type); Call(kicker, "FixedUpdate");
        }
        private static void Travel(PassingPractice practice, int ticks)
        {
            for (int i = 0; i < ticks; i++) { Physics2D.Simulate(0.02f); practice.Advance(0.02f); }
        }
    }
}
