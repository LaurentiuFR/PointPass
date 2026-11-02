using UnityEngine;

namespace PointPass.Core
{
    /// <summary>The same club colours and warm stadium neutrals across world and interface.</summary>
    public static class MatchPalette
    {
        public static readonly Color Ink = new Color(0.045f, 0.085f, 0.085f);
        public static readonly Color Surface = new Color(0.085f, 0.15f, 0.145f);
        public static readonly Color Paper = new Color(0.95f, 0.945f, 0.88f);
        public static readonly Color Muted = new Color(0.61f, 0.71f, 0.66f);
        public static readonly Color Accent = new Color(0.81f, 0.9f, 0.59f);
        public static readonly Color Gold = new Color(0.94f, 0.73f, 0.39f);
        public static readonly Color Blue = new Color(0.25f, 0.52f, 0.86f);
        public static readonly Color Red = new Color(0.88f, 0.35f, 0.29f);
        public static Color TeamColor(Team team) => team == Team.Blue ? Blue : Red;
        public static string TeamHex(Team team) => ColorUtility.ToHtmlStringRGB(TeamColor(team));
    }
}
