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
        public const float PassMinimumSpeed = 12f;
        public const float PassMaximumSpeed = 21f;
        public const float PitchBallDamping = 0.45f;

        // Match rules
        public const float HalfSeconds = 90f;
        public const float OvertimeSeconds = 60f;
        public const float GoalPauseSeconds = 3.2f;
        public const float KickoffPauseSeconds = 1.2f;
        public const float HalfTimePauseSeconds = 2.5f;

        // Look
        public static readonly Color Background = MatchPalette.Ink;
        public static readonly Color BlueTeam = MatchPalette.Blue;
        public static readonly Color RedTeam = MatchPalette.Red;
    }
}
