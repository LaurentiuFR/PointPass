using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>Reaction-limited keeper: tracks, dives, gathers slow balls and distributes through the shared kicker.</summary>
    [DefaultExecutionOrder(50)]
    public class GoalkeeperBrain : MonoBehaviour
    {
        public enum KeeperState { Tracking, Diving, Recovering, Holding, Distributing }
        public const float ReactionSeconds = 0.2f;
        private float reactionDelay = ReactionSeconds, diveSpeed = 6.4f, keeperSpeed = 0.82f;
        public float ReactionDelay => reactionDelay;
        public void SetDifficulty(int level)
        {
            reactionDelay = level <= 0 ? 0.32f : level >= 2 ? 0.14f : ReactionSeconds;
            diveSpeed = level <= 0 ? 5.2f : level >= 2 ? 7.2f : 6.4f;
            keeperSpeed = level <= 0 ? 0.7f : level >= 2 ? 0.9f : 0.82f;
            GetComponent<PlayerMovement>().SpeedMultiplier = keeperSpeed;
        }
        private PlayerController controller;
        private Ball ball;
        private PossessionSystem possession;
        private List<PlayerController> roster;
        private MatchManager match;
        private float stateAge, threatAge, savedThreatY;
        private Vector2 diveVelocity;
        private bool released;
        public KeeperState State { get; private set; }
        public float StateAge => stateAge;
        public float DiveSide => Mathf.Sign(diveVelocity.y);
        public int Catches { get; private set; }
        public int Parries { get; private set; }
        public int Distributions { get; private set; }
        public float Side { get; private set; }
        private float HomeX => Side * (MatchConfig.FieldHalfWidth - 0.65f);

        public void Init(Ball matchBall, PossessionSystem ownership, List<PlayerController> players, MatchManager manager)
        {
            controller = GetComponent<PlayerController>(); ball = matchBall; possession = ownership; roster = players; match = manager;
            Side = controller.Team == Team.Blue ? -1 : 1;
            controller.Dribbler.AutomaticPickup = false;
            GetComponent<PlayerMovement>().SpeedMultiplier = keeperSpeed;
            ResetKeeper();
        }

        private void FixedUpdate() => Advance(Time.fixedDeltaTime);
        public void Advance(float seconds)
        {
            if (controller == null || ball == null || !controller.IsActive || !ball.Body.simulated
                || Time.timeScale <= 0 || seconds <= 0 || (match != null && !match.IsMatchLive)) return;
            stateAge += seconds;
            Vector2 position = controller.Body.position;
            if (State == KeeperState.Holding || State == KeeperState.Distributing)
            {
                controller.SetMove(Vector2.zero);
                if (!possession.IsOwner(controller))
                {
                    // ReleaseForKick owns the short collision grace period after a distribution.
                    if (!released) Physics2D.IgnoreCollision(GetComponent<Collider2D>(), ball.Collider, false);
                    ChangeState(KeeperState.Recovering); return;
                }
                Vector2 hand = position + Vector2.left * Side * 0.56f;
                ball.Body.linearVelocity = Vector2.ClampMagnitude((hand - ball.Body.position) * 18f, 7f);
                if (State == KeeperState.Holding && stateAge >= 1f)
                {
                    Vector2 direction = DistributionDirection(); controller.SetAim(direction);
                    controller.BeginKick(PlayerKicker.KickType.Pass); ChangeState(KeeperState.Distributing);
                }
                else if (State == KeeperState.Distributing && controller.ChargingKick != PlayerKicker.KickType.Pass)
                {
                    // Pausing cancels queued player actions while preserving possession.
                    // Re-arm a cancelled throw/pass after Resume rather than holding forever.
                    controller.SetAim(DistributionDirection());
                    controller.BeginKick(PlayerKicker.KickType.Pass); stateAge = 0; released = false;
                }
                else if (State == KeeperState.Distributing && stateAge > 0.22f && !released)
                {
                    released = true; controller.ReleaseKick(PlayerKicker.KickType.Pass); Distributions++;
                }
                return;
            }
            if (State == KeeperState.Recovering)
            {
                controller.SetMove(Vector2.zero);
                if (stateAge > 0.55f) ChangeState(KeeperState.Tracking);
                return;
            }
            if (State == KeeperState.Diving)
            {
                controller.SetMove(Vector2.zero);
                float limit = MatchConfig.GoalWidth * 0.5f - 0.15f;
                Vector2 velocity = diveVelocity;
                if (Mathf.Abs(position.y) >= limit && Mathf.Sign(velocity.y) == Mathf.Sign(position.y)) velocity.y = 0;
                controller.Body.linearVelocity = velocity;
                TrySave(0.88f, ball.Body.linearVelocity.magnitude);
                if (State == KeeperState.Diving && stateAge >= 0.32f) ChangeState(KeeperState.Recovering);
                return;
            }

            Vector2 ballPosition = ball.Body.position, ballVelocity = ball.Body.linearVelocity;
            bool incoming = ballVelocity.x * Side > 1.5f && ballPosition.x * Side > MatchConfig.FieldHalfWidth - 6;
            if (incoming)
            {
                threatAge += seconds;
                if (threatAge >= reactionDelay)
                {
                    float travel = Mathf.Clamp((HomeX - ballPosition.x) / ballVelocity.x, 0, 0.8f);
                    savedThreatY = Mathf.Clamp(ballPosition.y + ballVelocity.y * travel, -1.32f, 1.32f);
                    if (travel < 0.5f && Mathf.Abs(savedThreatY - position.y) > 0.28f)
                    {
                        diveVelocity = new Vector2(-Side * 0.7f, Mathf.Sign(savedThreatY - position.y) * diveSpeed);
                        controller.Body.linearVelocity = diveVelocity; ChangeState(KeeperState.Diving); return;
                    }
                }
            }
            else threatAge = 0;

            Vector2 target = new Vector2(HomeX, Mathf.Clamp(incoming && threatAge >= reactionDelay
                ? savedThreatY : ballPosition.y * 0.48f, -1.05f, 1.05f));
            bool loose = !possession.HasOwner && ballVelocity.magnitude < 4f
                && ballPosition.x * Side > MatchConfig.FieldHalfWidth - 2.7f && Mathf.Abs(ballPosition.y) < 2.2f;
            if (loose && Vector2.Distance(ballPosition, position) < 2.25f)
                target = new Vector2(Mathf.Clamp(ballPosition.x * Side - 0.22f,
                    MatchConfig.FieldHalfWidth - 2.4f, MatchConfig.FieldHalfWidth - 0.42f) * Side,
                    Mathf.Clamp(ballPosition.y, -2.1f, 2.1f));
            Vector2 error = target - position;
            controller.SetMove(error.magnitude > 0.06f ? Vector2.ClampMagnitude(error * 2f, 1) : Vector2.zero);
            controller.SetAim(Vector2.left * Side);
            TrySave(0.62f, ballVelocity.magnitude);
        }

        private void TrySave(float reach, float speed)
        {
            if (possession.HasOwner || ball.Body.position.x * Side > MatchConfig.FieldHalfWidth + 0.1f) return;
            // Sweep one physics step so a fast shot cannot bounce off the body before the
            // keeper sees it. This grants no extra reaction time or reach beyond this tick.
            Vector2 offset = ball.Body.position - controller.Body.position;
            Vector2 relativeVelocity = ball.Body.linearVelocity - controller.Body.linearVelocity;
            float closestTime = relativeVelocity.sqrMagnitude > 0.001f
                ? Mathf.Clamp(-Vector2.Dot(offset, relativeVelocity) / relativeVelocity.sqrMagnitude, 0, Time.fixedDeltaTime) : 0;
            if ((offset + relativeVelocity * closestTime).magnitude > reach) return;
            if (speed <= 12f)
            {
                if (!possession.TryAcquire(controller)) return;
                ball.RecordTouch(controller); ball.SetCurve(0); ball.Body.linearVelocity = Vector2.zero;
                Physics2D.IgnoreCollision(GetComponent<Collider2D>(), ball.Collider, true);
                Catches++; ChangeState(KeeperState.Holding);
            }
            else
            {
                float wide = Mathf.Abs(ball.Body.position.y) > 0.15f ? Mathf.Sign(ball.Body.position.y) : (Side > 0 ? 1 : -1);
                ball.Body.linearVelocity = new Vector2(-Side, wide * 0.65f).normalized * Mathf.Clamp(speed * 0.5f, 6, 10);
                ball.SetCurve(0); ball.RecordTouch(controller); Parries++; ChangeState(KeeperState.Recovering);
            }
        }

        private Vector2 DistributionDirection()
        {
            PlayerController receiver = null; float best = float.MaxValue;
            if (roster != null) foreach (PlayerController player in roster)
            {
                if (player == null || player == controller || player.Team != controller.Team || player.GetComponent<GoalkeeperBrain>() != null) continue;
                float distance = Vector2.Distance(player.Body.position, controller.Body.position);
                if (distance < best) { receiver = player; best = distance; }
            }
            Vector2 direction = receiver != null ? receiver.Body.position - controller.Body.position : new Vector2(-Side, -controller.Body.position.y * 0.2f);
            if (direction.x * Side > -0.1f) direction.x = -Side * Mathf.Max(0.5f, Mathf.Abs(direction.x));
            return direction.normalized;
        }

        private void ChangeState(KeeperState next) { State = next; stateAge = 0; released = false; }
        public void ResetKeeper()
        {
            ChangeState(KeeperState.Tracking); threatAge = 0;
            if (controller != null)
            {
                controller.CancelActions(); controller.SetMove(Vector2.zero);
                controller.GetComponent<PlayerMovement>().SpeedMultiplier = keeperSpeed;
            }
            if (ball != null && controller != null)
                Physics2D.IgnoreCollision(GetComponent<Collider2D>(), ball.Collider, false);
        }
        private void OnDisable()
        {
            if (controller != null) possession?.Release(controller);
            ResetKeeper();
        }
    }
}
