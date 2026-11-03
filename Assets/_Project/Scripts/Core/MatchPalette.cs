using System;
using UnityEngine;

namespace PointPass.Core
{
    /// <summary>Point Pass's fixed navy and ice interface; club kits stay independent.</summary>
    public static class MatchPalette
    {
        public enum InterfaceTheme { IceBlue, Coral, Lime }
        public const string ThemePreferenceKey = "PointPass.InterfaceTheme";
        public static InterfaceTheme Theme => InterfaceTheme.IceBlue;
        public static event Action<Color, Color> AccentChanged;
        public static readonly Color Ink = new Color(0.027f, 0.067f, 0.149f);
        public static readonly Color Surface = new Color(0.063f, 0.122f, 0.224f);
        public static readonly Color Paper = new Color(0.953f, 0.969f, 1f);
        public static readonly Color Muted = new Color(0.639f, 0.69f, 0.788f);
        public static Color Accent => new Color(0.49f, 0.827f, 0.988f);
        public static string AccentHex => ColorUtility.ToHtmlStringRGB(Accent);
        public static string ThemeName => "ICE BLUE";
        public static readonly Color Gold = new Color(0.94f, 0.73f, 0.39f);
        public static readonly Color Blue = new Color(0.25f, 0.52f, 0.86f);
        public static readonly Color Red = new Color(0.88f, 0.35f, 0.29f);
        public static Color TeamColor(Team team) => team == Team.Blue ? Blue : Red;
        /// <summary>Unlit mesh vertex colours bypass the Canvas colour-space conversion.</summary>
        public static Color WorldColor(Color displayColor) => QualitySettings.activeColorSpace == ColorSpace.Linear
            ? displayColor.linear : displayColor;
        public static string TeamHex(Team team) => ColorUtility.ToHtmlStringRGB(TeamColor(team));

        public static void SetTheme(InterfaceTheme next)
        {
            if (!Enum.IsDefined(typeof(InterfaceTheme), next)) return;
            Color previous = Accent;
            // Migrate callers and old saved accents to the game's permanent visual identity.
            PlayerPrefs.SetInt(ThemePreferenceKey, (int)InterfaceTheme.IceBlue);
            PlayerPrefs.Save();
            AccentChanged?.Invoke(previous, Accent);
        }

    }
}
