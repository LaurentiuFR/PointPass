using System;
using System.Reflection;
using PointPass.Gameplay;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Deterministic presentation contracts, run only in the isolated Unity project.</summary>
    public static class PresentationValidation
    {
        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static T Field<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        public static void Run(Action<bool, string> check)
        {
            Animation(check);
            BallPresentation(check);
            KickEvent(check);
        }

        private static void Animation(Action<bool, string> check)
        {
            GameObject player = new GameObject("Animation validation", typeof(Rigidbody2D), typeof(PlayerAnimator));
            try
            {
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.position = new Vector2(3f, -2f);
                Transform left = new GameObject("Left boot").transform;
                Transform right = new GameObject("Right boot").transform;
                Transform torso = new GameObject("Torso").transform;
                left.SetParent(player.transform, false);
                right.SetParent(player.transform, false);
                torso.SetParent(player.transform, false);
                left.localPosition = new Vector3(-0.15f, 0.32f, 0f);
                right.localPosition = new Vector3(0.15f, 0.32f, 0f);
                left.localScale = new Vector3(0.2f, 0.3f, 1f);
                right.localScale = left.localScale;
                torso.localPosition = new Vector3(0f, 0.02f, 0f);
                torso.localScale = Vector3.one * 0.9f;
                Vector3 leftRest = left.localPosition;
                Vector3 rightRest = right.localPosition;
                Vector3 torsoRest = torso.localPosition;
                Vector3 leftScale = left.localScale;
                Vector3 torsoScale = torso.localScale;
                PlayerAnimator animator = player.GetComponent<PlayerAnimator>();
                Call(animator, "Awake");
                animator.Init(left, right, torso);

                animator.PlayKick(PlayerKicker.KickType.None);
                Call(animator, "Update");
                check(left.localPosition == leftRest && right.localPosition == rightRest,
                    "No-action kick leaves boots in their resting pose");
                Vector2 physicalPosition = body.position;
                Vector2 physicalVelocity = body.linearVelocity;
                animator.PlayKick(PlayerKicker.KickType.Shot);
                Call(animator, "Update");
                check(left.localPosition.y > leftRest.y + 0.1f || right.localPosition.y > rightRest.y + 0.1f,
                    "Physical-shot feedback visibly extends a striking boot");
                check(body.position == physicalPosition && body.linearVelocity == physicalVelocity,
                    "Kick animation never changes physical player position or velocity");
                body.simulated = false;
                Call(animator, "Update");
                check(left.localPosition == leftRest && right.localPosition == rightRest && torso.localPosition == torsoRest,
                    "Freezing resets kick, stride and torso position");
                check(left.localScale == leftScale && torso.localScale == torsoScale && animator.Intensity == 0f,
                    "Freezing restores base scales and animation intensity");
                animator.PlayKick(PlayerKicker.KickType.Shot);
                check(Field<float>(animator, "kickRemaining") == 0f, "Frozen players cannot start a kick animation");
                body.simulated = true;
                animator.PlayKick(PlayerKicker.KickType.Pass);
                Call(animator, "Update");
                Call(animator, "OnDisable");
                check(left.localPosition == leftRest && right.localPosition == rightRest && torso.localPosition == torsoRest,
                    "Disabling clears presentation offsets");
                animator.Init(null, null, null);
                animator.PlayKick(PlayerKicker.KickType.Shot);
                Call(animator, "Update");
                check(true, "Missing visual parts do not break player animation");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static void BallPresentation(Action<bool, string> check)
        {
            GameObject go = new GameObject("Ball presentation validation", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
            try
            {
                Ball ball = go.GetComponent<Ball>();
                Call(ball, "Awake");
                ball.Body.gravityScale = 0f;
                Transform visual = new GameObject("Ball visual").transform;
                visual.SetParent(go.transform, false);
                ball.SetVisual(visual);
                ball.SetVisual(visual);
                TrailRenderer[] trails = go.GetComponentsInChildren<TrailRenderer>();
                check(trails.Length == 1, "Repeated ball visual setup reuses one speed trail");
                TrailRenderer trail = trails[0];
                ball.Body.linearVelocity = Vector2.right * 11f;
                Call(ball, "Update");
                check(trail.emitting, "Fast ball movement emits its subtle speed trail");
                check(Field<float>(ball, "rollDirection") > 0f, "Rightward ball motion selects forward rolling");
                ball.Body.linearVelocity = Vector2.left * 11f;
                Call(ball, "Update");
                check(Field<float>(ball, "rollDirection") < 0f, "Reversed ball motion reverses visual rolling direction");
                ball.Body.linearVelocity = Vector2.up * 2f;
                Call(ball, "Update");
                check(!trail.emitting, "Slow dribbling does not emit a speed trail");
                ball.Body.linearVelocity = Vector2.up * 11f;
                ball.Body.simulated = false;
                Call(ball, "Update");
                check(!trail.emitting, "Frozen ball stops emitting a trail");
                ball.Body.simulated = true;
                ball.Body.linearVelocity = Vector2.right * 40f;
                Call(ball, "FixedUpdate");
                check(Mathf.Abs(ball.Body.linearVelocity.magnitude - 22f) < 0.001f,
                    "Ball presentation preserves the existing physical speed cap");
                visual.localRotation = Quaternion.Euler(0f, 0f, 47f);
                trail.AddPosition(Vector3.zero);
                trail.AddPosition(Vector3.right);
                ball.ResetTo(new Vector2(2f, 3f));
                check(ball.Body.linearVelocity == Vector2.zero && ball.Body.angularVelocity == 0f,
                    "Ball reset clears physical velocity");
                check(!trail.emitting && trail.positionCount == 0 && Quaternion.Angle(visual.localRotation, Quaternion.identity) < 0.001f,
                    "Ball reset clears speed trail and visual rotation");
                trail.AddPosition(Vector3.zero);
                trail.AddPosition(Vector3.right);
                Call(ball, "OnDisable");
                check(trail.positionCount == 0 && !trail.emitting, "Disabling ball clears stale trail geometry");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void KickEvent(Action<bool, string> check)
        {
            GameObject player = new GameObject("Kick event validation", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
            GameObject ballObject = new GameObject("Kick event ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
            try
            {
                Ball ball = ballObject.GetComponent<Ball>();
                Call(ball, "Awake");
                PlayerController controller = player.GetComponent<PlayerController>();
                Dribbler dribbler = player.GetComponent<Dribbler>();
                PlayerKicker kicker = player.GetComponent<PlayerKicker>();
                Call(dribbler, "Awake");
                Call(kicker, "Awake");
                PossessionSystem possession = new PossessionSystem();
                dribbler.Init(ball, possession, null);
                kicker.Init(ball, possession);
                possession.TryAcquire(controller);
                int events = 0;
                PlayerKicker.KickType eventType = PlayerKicker.KickType.None;
                bool physicalStateReady = false;
                kicker.Kicked += type =>
                {
                    events++;
                    eventType = type;
                    physicalStateReady = !possession.HasOwner && ball.Body.linearVelocity.magnitude > 0f;
                };
                controller.BeginKick(PlayerKicker.KickType.Pass);
                controller.ReleaseKick(PlayerKicker.KickType.Pass);
                check(events == 0, "Presentation kick event waits for the physics tick");
                Call(kicker, "FixedUpdate");
                check(events == 1 && eventType == PlayerKicker.KickType.Pass && physicalStateReady,
                    "Kick event fires after physical release with the correct action type");
                Call(kicker, "FixedUpdate");
                check(events == 1, "Kick event fires once for one queued kick");
                possession.TryAcquire(controller);
                controller.BeginKick(PlayerKicker.KickType.Shot);
                controller.ReleaseKick(PlayerKicker.KickType.Shot);
                controller.CancelKick();
                Call(kicker, "FixedUpdate");
                check(events == 1 && possession.IsOwner(controller), "Cancelled kick never emits presentation feedback");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(ballObject);
            }
        }
    }
}
