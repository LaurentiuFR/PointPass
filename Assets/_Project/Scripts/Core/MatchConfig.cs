using UnityEngine;

namespace PointPass.Core
{
    public enum Team { Blue, Red }

    /// <summary>All the tunable numbers in one place. Change a value here, press Play.</summary>
    public static class MatchConfig
    {
        // Pitch (world units)
        public const float PitchWidth = 20f;
        public const float PitchHeight = 12f;
        public const float GoalWidth = 3f;
        public const float GoalDepth = 1f;

        // The white boundary line is drawn this far inside the pitch image edge.
        // Walls and goals sit ON the line, so the ball never visibly leaves the pitch.
        public const float LineInset = 0.3f;
        public const float FieldHalfWidth = PitchWidth * 0.5f - LineInset;
        public const float FieldHalfHeight = PitchHeight * 0.5f - LineInset;

        // Bodies
        public const float BallDiameter = 0.5f;
        public const float PlayerDiameter = 1f;
        public const float PlayerColliderRadius = 0.4f;

        // Match rules
        public const float HalfSeconds = 90f;
        public const float OvertimeSeconds = 60f;
        public const float GoalPauseSeconds = 2f;
        public const float KickoffPauseSeconds = 1.2f;
        public const float HalfTimePauseSeconds = 2.5f;

        // Look
        public static readonly Color Background = new Color(0.09f, 0.27f, 0.13f);
        public static readonly Color BlueTeam = new Color(0.18f, 0.44f, 0.93f);
        public static readonly Color RedTeam = new Color(0.88f, 0.19f, 0.19f);
    }
}
