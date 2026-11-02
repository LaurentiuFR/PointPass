using System;
using System.Reflection;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Validation
{
    /// <summary>Deterministic engine checks for responsive, bounded player facing.</summary>
    public static class TurningValidation
    {
        private static readonly MethodInfo Tick = typeof(PlayerMovement).GetMethod(
            "FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Step(PlayerMovement movement, int ticks)
        {
            for (int i = 0; i < ticks; i++) Tick.Invoke(movement, null);
        }

        private static bool Facing(PlayerMovement movement, float angle) =>
            Mathf.Abs(Mathf.DeltaAngle(movement.Body.rotation, angle)) < 0.01f;

        public static void Run(Action<bool, string> check)
        {
            GameObject player = new GameObject("Turning validation", typeof(Rigidbody2D), typeof(PlayerMovement));
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            float previousStep = Time.fixedDeltaTime;
            try
            {
                Time.fixedDeltaTime = 0.02f;
                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.right);
                Step(movement, 1);
                check(Mathf.Abs(Mathf.DeltaAngle(movement.Body.rotation, 0f)) <= 16.81f,
                    "Facing turns at a bounded angular speed rather than snapping");
                Step(movement, 5);
                check(Facing(movement, -90f), "Ninety-degree facing change settles within 120ms");

                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.down);
                Step(movement, 11);
                check(Facing(movement, 180f), "Full reversal settles within 220ms");

                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.right);
                Step(movement, 1);
                movement.SetMoveInput(Vector2.zero);
                Step(movement, 5);
                check(Facing(movement, -90f), "Releasing the stick still completes the intended turn");

                movement.ResetTo(Vector2.zero, 37f);
                Step(movement, 12);
                check(Facing(movement, 37f), "Reset facing remains stable without new movement input");
                movement.SetMoveInput(Vector2.left);
                movement.Body.simulated = false;
                Step(movement, 12);
                check(Facing(movement, 37f), "Frozen player cannot rotate");
                movement.Body.simulated = true;

                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.right);
                Step(movement, 3);
                float coarseAngle = movement.Body.rotation;
                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.right);
                Time.fixedDeltaTime = 0.01f;
                Step(movement, 6);
                check(Mathf.Abs(Mathf.DeltaAngle(coarseAngle, movement.Body.rotation)) < 0.01f,
                    "Turn rate is consistent across physics timesteps");

                Time.fixedDeltaTime = 0.02f;
                movement.ResetTo(Vector2.zero, 0f);
                movement.SetMoveInput(Vector2.one);
                Step(movement, 30);
                check(Mathf.Abs(movement.Body.linearVelocity.magnitude - 4.8f) < 0.01f,
                    "Diagonal movement keeps the same top speed");
            }
            finally
            {
                Time.fixedDeltaTime = previousStep;
                UnityEngine.Object.DestroyImmediate(player);
            }
        }
    }
}
