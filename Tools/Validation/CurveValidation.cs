using System;
using System.Reflection;
using PointPass.Gameplay;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Deterministic checks for physical shot bending and command lifecycle.</summary>
    public static class CurveValidation
    {
        private static void Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private sealed class Flight : IDisposable
        {
            public readonly GameObject Object;
            public readonly Ball Ball;

            public Flight(float curve)
            {
                Object = new GameObject("Curve flight", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                Ball = Object.GetComponent<Ball>();
                Call(Ball, "Awake");
                Ball.Body.gravityScale = 0;
                Ball.Body.linearDamping = 0;
                Ball.Body.linearVelocity = Vector2.right * 12;
                Ball.SetCurve(curve);
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Object);
        }

        private static Vector2 SimulateFlight(float curve, float timestep, out Vector2 velocity)
        {
            using (Flight flight = new Flight(curve))
            {
                Time.fixedDeltaTime = timestep;
                int ticks = Mathf.RoundToInt(0.6f / timestep);
                for (int i = 0; i < ticks; i++)
                {
                    Call(flight.Ball, "FixedUpdate");
                    Physics2D.Simulate(timestep);
                }
                velocity = flight.Ball.Body.linearVelocity;
                return flight.Ball.Body.position;
            }
        }

        public static void Run(Action<bool, string> check)
        {
            float previousStep = Time.fixedDeltaTime;
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Time.fixedDeltaTime = 0.02f;
                using (Flight flight = new Flight(1))
                {
                    Vector2 position = flight.Ball.Body.position;
                    Call(flight.Ball, "FixedUpdate");
                    check(flight.Ball.Body.linearVelocity.y > 0, "Positive shot curve bends left relative to travel");
                    check(flight.Ball.Body.position == position, "Curve changes velocity without teleporting the ball");
                    check(Mathf.Abs(flight.Ball.Body.linearVelocity.magnitude - 12) < 0.001f,
                        "Curve does not add physical speed or energy");
                    flight.Ball.SetCurve(-1);
                    flight.Ball.Body.linearVelocity = Vector2.right * 12;
                    Call(flight.Ball, "FixedUpdate");
                    check(flight.Ball.Body.linearVelocity.y < 0, "Negative shot curve bends right relative to travel");
                    flight.Ball.SetCurve(3);
                    check(Mathf.Abs(flight.Ball.Curve - 1) < 0.001f, "Ball curve input clamps to the supported range");
                    flight.Ball.SetCurve(float.NaN);
                    check(flight.Ball.Curve == 0, "Invalid curve input is ignored safely");

                    flight.Ball.SetCurve(1);
                    flight.Ball.Body.simulated = false;
                    Vector2 frozenVelocity = flight.Ball.Body.linearVelocity;
                    Call(flight.Ball, "FixedUpdate");
                    check(flight.Ball.Curve == 1 && flight.Ball.Body.linearVelocity == frozenVelocity,
                        "Frozen physics preserves curve duration and velocity");
                    flight.Ball.Body.simulated = true;
                    flight.Ball.Body.linearVelocity = Vector2.right * 40;
                    Call(flight.Ball, "FixedUpdate");
                    check(Mathf.Abs(flight.Ball.Body.linearVelocity.magnitude - 22) < 0.001f,
                        "Curved shots respect the existing ball speed cap");
                    for (int i = 0; i < 70; i++) Call(flight.Ball, "FixedUpdate");
                    Vector2 expiredVelocity = flight.Ball.Body.linearVelocity;
                    Call(flight.Ball, "FixedUpdate");
                    check(flight.Ball.Curve == 0 && flight.Ball.Body.linearVelocity == expiredVelocity,
                        "Curve fades out and stops steering the ball");
                    flight.Ball.SetCurve(1);
                    Call(flight.Ball, "OnCollisionEnter2D", new object[] { null });
                    check(flight.Ball.Curve == 0, "A physical collision ends the shot bend");
                    flight.Ball.SetCurve(-1);
                    flight.Ball.ResetTo(new Vector2(2, 3));
                    check(flight.Ball.Curve == 0 && flight.Ball.Body.linearVelocity == Vector2.zero,
                        "Kickoff ball reset clears bend and momentum");
                }

                Vector2 left = SimulateFlight(1, 0.02f, out Vector2 leftVelocity);
                Vector2 right = SimulateFlight(-1, 0.02f, out Vector2 rightVelocity);
                check(left.x > 5 && left.y > 1 && right.x > 5 && right.y < -1,
                    "Curved shots travel through physics along visibly different paths");
                check(Mathf.Abs(left.x - right.x) < 0.002f && Mathf.Abs(left.y + right.y) < 0.002f,
                    "Left and right curves have symmetric physical trajectories");
                SimulateFlight(1, 0.01f, out Vector2 finerVelocity);
                check(Vector2.Distance(finerVelocity, leftVelocity) < 0.002f,
                    "Curve response is consistent across physics timesteps");
                Vector2 straight = SimulateFlight(0, 0.02f, out Vector2 straightVelocity);
                check(Mathf.Abs(straight.y) < 0.001f && Mathf.Abs(straightVelocity.y) < 0.001f,
                    "A release without a flick remains a straight shot");
                Commands(check);
            }
            finally
            {
                Time.fixedDeltaTime = previousStep;
                Physics2D.simulationMode = previousMode;
            }
        }

        private static void Commands(Action<bool, string> check)
        {
            using (Flight flight = new Flight(0))
            {
                GameObject player = new GameObject("Curve commands", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
                try
                {
                    PlayerController controller = player.GetComponent<PlayerController>();
                    Dribbler dribbler = player.GetComponent<Dribbler>();
                    PlayerKicker kicker = player.GetComponent<PlayerKicker>();
                    Call(dribbler, "Awake");
                    Call(kicker, "Awake");
                    PossessionSystem possession = new PossessionSystem();
                    dribbler.Init(flight.Ball, possession, null);
                    kicker.Init(flight.Ball, possession);
                    possession.TryAcquire(controller);
                    controller.BeginKick(PlayerKicker.KickType.Pass);
                    controller.SetShotCurve(1);
                    check(controller.ShotCurve == 0, "Passing ignores shot-curve commands");
                    controller.CancelKick();
                    controller.BeginKick(PlayerKicker.KickType.Shot);
                    controller.SetShotCurve(-3);
                    check(controller.ShotCurve == -1, "Charged shot receives clamped curve through PlayerController");
                    controller.ReleaseKick(PlayerKicker.KickType.Shot);
                    check(flight.Ball.Curve == 0, "Shot bend waits for the physical release tick");
                    Call(kicker, "FixedUpdate");
                    check(flight.Ball.Curve == -1 && controller.ShotCurve == 0 && !possession.HasOwner,
                        "Physical shot transfers queued bend and clears its charge state");

                    possession.TryAcquire(controller);
                    controller.BeginKick(PlayerKicker.KickType.Pass);
                    controller.ReleaseKick(PlayerKicker.KickType.Pass);
                    Call(kicker, "FixedUpdate");
                    check(flight.Ball.Curve == 0, "A new pass clears any previous ball curve");
                    possession.TryAcquire(controller);
                    controller.BeginKick(PlayerKicker.KickType.Shot);
                    controller.SetShotCurve(1);
                    controller.ReleaseKick(PlayerKicker.KickType.Shot);
                    controller.CancelKick();
                    Vector2 velocity = flight.Ball.Body.linearVelocity;
                    Call(kicker, "FixedUpdate");
                    check(flight.Ball.Curve == 0 && controller.ShotCurve == 0
                        && flight.Ball.Body.linearVelocity == velocity && possession.IsOwner(controller),
                        "Cancelling a queued curved shot cannot bend or release the ball");
                    controller.BeginKick(PlayerKicker.KickType.Shot);
                    controller.SetShotCurve(1);
                    possession.Clear();
                    check(controller.ShotCurve == 0, "Losing possession clears a charged curve");
                }
                finally { UnityEngine.Object.DestroyImmediate(player); }
            }
        }
    }
}
