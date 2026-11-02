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

        public Team Team { get; private set; }
        public Vector2 KickoffPosition { get; private set; }
        public float KickoffAngle { get; private set; }

        private PlayerMovement Movement => movement != null ? movement : (movement = GetComponent<PlayerMovement>());
        public Dribbler Dribbler => dribbler != null ? dribbler : (dribbler = GetComponent<Dribbler>());
        public Rigidbody2D Body => Movement.Body;

        /// <summary>False while the match is paused or frozen.</summary>
        public bool IsActive => Body.simulated;

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
            Movement.SetMoveInput(direction);
        }

        // ---- Match control (used by MatchManager) ----

        public void SetActive(bool active)
        {
            Body.simulated = active;
            if (!active) Movement.SetMoveInput(Vector2.zero);
        }

        public void ResetToKickoff()
        {
            if (Dribbler != null) Dribbler.Release();
            Movement.ResetTo(KickoffPosition, KickoffAngle);
        }
    }
}
