using System;
using UnityEngine;

namespace PointPass.Core
{
    /// <summary>Shared navy interface with a selectable accent; club kits stay independent.</summary>
    public static class MatchPalette
    {
        public enum InterfaceTheme { IceBlue, Coral, Lime }
        public const string ThemePreferenceKey = "PointPass.InterfaceTheme";
        private static InterfaceTheme theme = ReadTheme();
        public static InterfaceTheme Theme => theme;
        public static event Action<Color, Color> AccentChanged;
        public static readonly Color Ink = new Color(0.027f, 0.067f, 0.149f);
        public static readonly Color Surface = new Color(0.063f, 0.122f, 0.224f);
        public static readonly Color Paper = new Color(0.953f, 0.969f, 1f);
        public static readonly Color Muted = new Color(0.639f, 0.69f, 0.788f);
        public static Color Accent => theme == InterfaceTheme.Coral ? new Color(1f, 0.569f, 0.51f)
            : theme == InterfaceTheme.Lime ? new Color(0.718f, 0.957f, 0.2f)
            : new Color(0.49f, 0.827f, 0.988f);
        public static string AccentHex => ColorUtility.ToHtmlStringRGB(Accent);
        public static string ThemeName => theme == InterfaceTheme.IceBlue ? "ICE BLUE" : theme.ToString().ToUpperInvariant();
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
            theme = next;
            PlayerPrefs.SetInt(ThemePreferenceKey, (int)theme);
            PlayerPrefs.Save();
            AccentChanged?.Invoke(previous, Accent);
        }

        private static InterfaceTheme ReadTheme()
        {
            int saved = PlayerPrefs.GetInt(ThemePreferenceKey, 0);
            return saved >= 0 && saved <= 2 ? (InterfaceTheme)saved : InterfaceTheme.IceBlue;
        }
    }
}
