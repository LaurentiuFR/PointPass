using System;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Players
{
    public enum TackleResult : byte { None, WonBall, Missed }

    /// <summary>A short timed challenge against the exposed ball, resolved only by authoritative physics.</summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerMovement))]
    public sealed class PlayerTackler : MonoBehaviour
    {
        [SerializeField] private float reach = 1.05f;
        [SerializeField] private float windup = .09f;
        [SerializeField] private float contactWindow = .13f;
        private PlayerController player;
        private PlayerMovement movement;
        private Ball ball;
        private PossessionSystem possession;
        private float age, recovery;
        private bool pending, replicated;
        private Vector2 direction;
        public uint ResultSerial { get; private set; }
        public TackleResult LastResult { get; private set; }
        public float RecoveryRemaining => recovery;
        public bool IsChallenging => pending;
        public bool CanAct => !pending && recovery <= 0;
        public event Action<TackleResult> Resolved;

        public void Init(Ball value, PossessionSystem ownership)
        {
            player = GetComponent<PlayerController>(); movement = GetComponent<PlayerMovement>();
            ball = value; possession = ownership;
        }
        public bool Request()
        {
            if (replicated || !CanAct || player == null || !player.IsActive || ball == null
                || !ball.Body.simulated || possession == null || possession.IsOwner(player)) return false;
            player.CancelKick();
            direction = player.AimDirection.normalized;
            pending = true; age = 0; recovery = .76f;
            movement.ActionSpeedMultiplier = .6f;
            return true;
        }
        private void FixedUpdate()
        {
            if (replicated || player == null || !player.Body.simulated) return;
            recovery = Mathf.Max(0, recovery - Time.fixedDeltaTime);
            if (!pending) { if (recovery <= 0) movement.ActionSpeedMultiplier = 1; return; }
            if (!player.IsActive) { ResetChallenge(); return; }
            age += Time.fixedDeltaTime;
            if (age >= windup && BallIsExposed())
            {
                PlayerController formerOwner = possession.Owner;
                if (formerOwner != null) formerOwner.Dribbler?.Dispossess(.42f);
                possession.Clear(); player.Dribbler.Dispossess(.1f);
                ball.RecordTouch(player); ball.SetCurve(0);
                // A real impulse leaves an interceptable loose ball; no position/ownership teleport.
                ball.Body.linearVelocity = direction * 4.8f + player.Body.linearVelocity * .3f;
                Finish(TackleResult.WonBall, .38f);
            }
            else if (age >= windup + contactWindow) Finish(TackleResult.Missed, .54f);
        }
        private bool BallIsExposed()
        {
            Vector2 toBall = ball.Body.position - player.Body.position;
            if (toBall.sqrMagnitude > reach * reach || toBall.sqrMagnitude < .01f
                || Vector2.Dot(direction, toBall.normalized) < .45f) return false;
            var owner = possession.Owner;
            if (owner == null) return true;
            if (owner.Team == player.Team) return false;
            Vector2 exposed = ball.Body.position - owner.Body.position;
            Vector2 challenger = player.Body.position - owner.Body.position;
            // The opponent's body shields the ball from a challenge through their back.
            return challenger.sqrMagnitude <= 1.65f * 1.65f && exposed.sqrMagnitude > .01f
                && Vector2.Dot(challenger.normalized, exposed.normalized) >= -.12f;
        }
        private void Finish(TackleResult result, float cooldown)
        {
            pending = false; recovery = cooldown; LastResult = result; ResultSerial++;
            Resolved?.Invoke(result);
        }
        public void ResetChallenge()
        {
            pending = false; recovery = 0; age = 0;
            if (movement != null) movement.ActionSpeedMultiplier = 1;
        }
        public void SetReplicated(uint serial, TackleResult result, float cooldown, bool active)
        {
            replicated = true; pending = active; recovery = Mathf.Max(0, cooldown);
            if (serial != ResultSerial)
            {
                ResultSerial = serial; LastResult = result;
                if (result != TackleResult.None) Resolved?.Invoke(result);
            }
        }
        private void OnDisable() { if (!replicated) ResetChallenge(); }
    }
}
