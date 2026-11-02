using System;
using System.Reflection;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Steering/arm contracts for the isolated Unity engine validation project.</summary>
    public static class FlowAnimationValidation
    {
        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Step(PlayerMovement movement, int ticks)
        {
            for (int i = 0; i < ticks; i++) Call(movement, "FixedUpdate");
        }

        public static void Run(Action<bool, string> check)
        {
            Steering(check);
            Arms(check);
        }

        private static void Steering(Action<bool, string> check)
        {
            GameObject go = new GameObject("Flow validation", typeof(Rigidbody2D), typeof(PlayerMovement));
            PlayerMovement movement = go.GetComponent<PlayerMovement>();
            float previousStep = Time.fixedDeltaTime;
            try
            {
                Time.fixedDeltaTime = 0.02f;
                movement.ResetTo(new Vector2(2, 3), -90);
                movement.SetMoveInput(Vector2.right);
                Step(movement, 40);
                movement.SetMoveInput(Vector2.up);
                check(movement.AimDirection == Vector2.up, "Flowing movement does not delay commanded kick aim");
                Step(movement, 1);
                check(movement.Body.linearVelocity.x > 4 && movement.Body.linearVelocity.y > 0,
                    "Sudden right-angle input initially retains momentum while starting the new direction");
                bool bounded = true;
                for (int i = 0; i < 30; i++)
                {
                    Step(movement, 1);
                    if (movement.Body.linearVelocity.magnitude > 4.801f) bounded = false;
                }
                check(bounded, "Steering through an arc never creates extra running speed");
                check(Vector2.Angle(movement.Body.linearVelocity, Vector2.up) < 1,
                    "Steering settles to the new direction without lasting sideways drift");
                check(movement.Body.position == new Vector2(2, 3), "Steering commands never teleport player position");

                movement.SetSprint(true);
                Step(movement, 40);
                check(Mathf.Abs(movement.Body.linearVelocity.magnitude - 6.48f) < 0.01f,
                    "Flowing sprint retains the approved top speed");
                movement.SetSprint(false);
                Step(movement, 1);
                check(movement.Body.linearVelocity.magnitude > 4.8f && movement.Body.linearVelocity.magnitude < 6.48f,
                    "Releasing sprint eases speed down rather than clamping it instantly");
                Step(movement, 40);
                check(Mathf.Abs(movement.Body.linearVelocity.magnitude - 4.8f) < 0.01f,
                    "Sprint release settles at normal run speed");
                movement.SetMoveInput(Vector2.zero);
                Step(movement, 40);
                check(movement.Body.linearVelocity == Vector2.zero, "Releasing movement stops completely without permanent skating");

                movement.ResetTo(Vector2.zero, 0);
                movement.SetMoveInput(Vector2.up);
                Step(movement, 1);
                Vector2 firstAcceleration = movement.Body.linearVelocity;
                movement.SetMoveInput(Vector2.left);
                Step(movement, 20);
                movement.ResetTo(Vector2.zero, 0);
                movement.SetMoveInput(Vector2.up);
                Step(movement, 1);
                check(Vector2.Distance(firstAcceleration, movement.Body.linearVelocity) < 0.0001f,
                    "Kickoff reset clears old steering acceleration");
                movement.Body.simulated = false;
                Vector2 velocity = movement.Body.linearVelocity;
                float angle = movement.Body.rotation;
                movement.SetMoveInput(Vector2.right);
                Step(movement, 10);
                check(movement.Body.linearVelocity == velocity && movement.Body.rotation == angle,
                    "Frozen movement cannot advance its flow or facing");
            }
            finally
            {
                Time.fixedDeltaTime = previousStep;
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Arms(Action<bool, string> check)
        {
            GameObject go = new GameObject("Arm validation", typeof(Rigidbody2D), typeof(PlayerAnimator));
            try
            {
                Rigidbody2D body = go.GetComponent<Rigidbody2D>();
                body.gravityScale = 0;
                PlayerAnimator animator = go.GetComponent<PlayerAnimator>();
                Call(animator, "Awake");
                Transform leftBoot = new GameObject("Left boot").transform;
                Transform rightBoot = new GameObject("Right boot").transform;
                Transform torso = new GameObject("Torso").transform;
                Transform leftArm = new GameObject("Left arm pivot").transform;
                Transform rightArm = new GameObject("Right arm pivot").transform;
                foreach (Transform part in new[] { leftBoot, rightBoot, torso, leftArm, rightArm }) part.SetParent(go.transform, false);
                leftArm.localPosition = new Vector3(-0.33f, -0.04f, 0);
                rightArm.localPosition = new Vector3(0.33f, -0.04f, 0);
                Vector3 leftRest = leftArm.localPosition;
                Vector3 rightRest = rightArm.localPosition;
                animator.Init(leftBoot, rightBoot, torso, leftArm, rightArm);
                typeof(PlayerAnimator).GetProperty("Phase").SetValue(animator, Mathf.PI * 0.5f);
                typeof(PlayerAnimator).GetProperty("Intensity").SetValue(animator, 1f);
                body.linearVelocity = Vector2.up * 4.8f;
                Vector2 position = body.position;
                Call(animator, "Update");
                float leftTravel = leftArm.localPosition.y - leftRest.y;
                float rightTravel = rightArm.localPosition.y - rightRest.y;
                check(Mathf.Abs(leftTravel) > 0.01f && Mathf.Abs(rightTravel) > 0.01f && leftTravel * rightTravel < 0,
                    "Running swings separate left and right arms in opposite phases");
                check(leftTravel * leftBoot.localPosition.y < 0,
                    "Arm swing counterbalances the same-side running leg");
                check(Quaternion.Angle(leftArm.localRotation, Quaternion.identity) > 1
                    && Quaternion.Angle(rightArm.localRotation, Quaternion.identity) > 1,
                    "Running arms rotate around their shoulder pivots");
                check(body.position == position && body.linearVelocity == Vector2.up * 4.8f,
                    "Arm animation never changes physical movement");
                body.simulated = false;
                Call(animator, "Update");
                check(leftArm.localPosition == leftRest && rightArm.localPosition == rightRest
                    && leftArm.localRotation == Quaternion.identity && rightArm.localRotation == Quaternion.identity,
                    "Freezing restores arm positions and rotations");
                body.simulated = true;
                animator.PlayKick(PlayerKicker.KickType.Shot);
                Call(animator, "Update");
                Call(animator, "OnDisable");
                check(leftArm.localPosition == leftRest && rightArm.localPosition == rightRest,
                    "Disabling a kicking player clears arm balance offsets");
                animator.Init(leftBoot, rightBoot, torso);
                Call(animator, "Update");
                check(true, "The original three-part animation initialization stays compatible");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
