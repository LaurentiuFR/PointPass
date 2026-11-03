using PointPass.Core;
using UnityEngine;

namespace PointPass.Players
{
    /// <summary>
    /// One footballer on the pitch. This is the single place that anything controlling him
    /// (human input now, AI and network later) sends commands to. It does not care who is
    /// controlling it.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerController : MonoBehaviour
    {
        private PlayerMovement movement;
        private Dribbler dribbler;
        private PlayerKicker kicker;
        private bool inputEnabled = true;
        private PlayerKicker Kicker => kicker != null ? kicker : (kicker = GetComponent<PlayerKicker>());
        public float KickCharge => Kicker != null ? Kicker.Charge : 0f;
        public PlayerKicker.KickType ChargingKick => Kicker != null ? Kicker.Charging : PlayerKicker.KickType.None;
        public float ShotCurve => Kicker != null ? Kicker.ShotCurve : 0f;

        public Team Team { get; private set; }
        public Vector2 KickoffPosition { get; private set; }
        public float KickoffAngle { get; private set; }

        private PlayerMovement Movement => movement != null ? movement : (movement = GetComponent<PlayerMovement>());
        public Dribbler Dribbler => dribbler != null ? dribbler : (dribbler = GetComponent<Dribbler>());
        public Rigidbody2D Body => Movement.Body;
        public Vector2 AimDirection => Movement.AimDirection;

        /// <summary>False while physics is frozen or a menu has gated gameplay commands.</summary>
        public bool IsActive => Body.simulated && inputEnabled;

        /// <summary>Menus gate commands without releasing possession or disturbing paused physics.</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                CancelActions();
                Movement.SetMoveInput(Vector2.zero);
            }
        }

        public void Init(Team team, Vector2 kickoffPosition, float kickoffAngle)
        {
            Team = team;
            KickoffPosition = kickoffPosition;
            KickoffAngle = kickoffAngle;
        }

        // ---- Commands (used by HumanPlayerInput now, AI later) ----

        /// <summary>Move in a direction. Length 0 to 1 sets how hard.</summary>
        public void SetMove(Vector2 direction)
        {
            Movement.SetMoveInput(IsActive ? direction : Vector2.zero);
        }

        public void BeginKick(PlayerKicker.KickType type) { if (IsActive) Kicker?.Begin(type); }
        public void ReleaseKick(PlayerKicker.KickType type) => Kicker?.Release(type);
        public void CancelKick() => Kicker?.Cancel();
        public void SetShotCurve(float curve) { if (IsActive) Kicker?.SetShotCurve(curve); }
        public void SetSprint(bool held) => Movement.SetSprint(IsActive && held);
        public void SetAim(Vector2 direction) { if (IsActive) Movement.SetAimDirection(direction); }

        // ---- Match control (used by MatchManager) ----

        public void CancelActions()
        {
            CancelKick();
            Movement.SetSprint(false);
        }

        public void SetActive(bool active)
        {
            Body.simulated = active;
            if (!active)
            {
                Movement.SetMoveInput(Vector2.zero);
                CancelActions();
                if (Dribbler != null) Dribbler.Release();
            }
        }

        public void ResetToKickoff()
        {
            CancelActions();
            if (Dribbler != null) Dribbler.Release();
            Movement.ResetTo(KickoffPosition, KickoffAngle);
        }
    }
}
