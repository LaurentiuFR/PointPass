using System;
using System.Reflection;
using PointPass.Core;
using PointPass.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PointPass.Validation
{
    /// <summary>Checks the actual menu artwork/material contract and generated glyph geometry.</summary>
    public static class MenuSharpnessValidation
    {
        public static void Run(Action<bool, string> check)
        {
            MatchMenu menu = MatchMenu.Create();
            MatchHud hud = MatchHud.Create();
            GameObject canvasObject = new GameObject("Glyph canvas", typeof(Canvas));
            MatchPalette.InterfaceTheme original = MatchPalette.Theme;
            bool hadPreference = PlayerPrefs.HasKey(MatchPalette.ThemePreferenceKey);
            try
            {
                menu.ShowMain();
                Transform main = menu.transform.Find("SafeArea/Content/Main");
                check(main.Find("HeroCard") == null && main.GetComponent<LobbyMenuView>() != null,
                    "Main menu replaces the overhead promotional card with the clubhouse lobby");
                check(main.Find("SettingsPage/Options/ColourScheme") != null,
                    "Lobby Settings preserves the selectable interface colour schemes");
                check(MatchPalette.Ink.b > MatchPalette.Ink.g * 1.8f,
                    "Shared identity retains the deep navy base with selectable accents");
                GoalReplayView replay = menu.GetComponentInChildren<GoalReplayView>(true);
                replay.SetPlaybackSpeed(1.5f);
                replay.SetShowShotArrow(true);
                Image rim = hud.ShootButton.transform.Find("Rim").GetComponent<Image>();
                float rimAlpha = rim.color.a;
                foreach (MatchPalette.InterfaceTheme scheme in Enum.GetValues(typeof(MatchPalette.InterfaceTheme)))
                {
                    MatchPalette.SetTheme(scheme);
                    Color expected = MatchPalette.Accent;
                    check(Vector3.Distance(new Vector3(rim.color.r, rim.color.g, rim.color.b),
                            new Vector3(expected.r, expected.g, expected.b)) < 0.001f
                        && Mathf.Approximately(rim.color.a, rimAlpha),
                        scheme + " updates the existing HUD accent while retaining its opacity");
                    check(main.Find("GlobalHeader/Wordmark").GetComponent<Text>().text.Contains("#" + MatchPalette.AccentHex)
                        && PlayerPrefs.GetInt(MatchPalette.ThemePreferenceKey) == (int)scheme,
                        scheme + " updates lobby branding and saves the selected preference");
                }
                check(replay.PlaybackSpeed == 1.5f && replay.ShowShotArrow,
                    "Colour changes preserve replay speed and shot-arrow settings");
                MatchPalette.SetTheme(MatchPalette.InterfaceTheme.IceBlue);
                check(MatchPalette.Accent.b > 0.98f && MatchPalette.Accent.g > 0.8f,
                    "Default ice blue provides a cool accent rather than lime");

                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = true;
                canvas.scaleFactor = 0.75f;
                GameObject labelObject = new GameObject("Glyph label", typeof(RectTransform), typeof(SharpText));
                labelObject.transform.SetParent(canvasObject.transform, false);
                SharpText label = labelObject.GetComponent<SharpText>();
                label.rectTransform.sizeDelta = new Vector2(1000, 160);
                label.rectTransform.anchoredPosition = new Vector2(0.3f, 0.7f);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 56;
                label.text = "POINT <color=#B7F433>PASS</color>";
                label.alignment = TextAnchor.MiddleCenter;
                using (VertexHelper mesh = new VertexHelper())
                {
                    Populate(label, mesh);
                    check(mesh.currentVertCount > 0 && mesh.currentVertCount == label.cachedTextGenerator.verts.Count,
                        "Supersampled text preserves every generated glyph quad");
                    bool finite = true, bounded = true;
                    UIVertex vertex = default;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        mesh.PopulateUIVertex(ref vertex, i);
                        Vector3 p = vertex.position;
                        finite &= !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y);
                        bounded &= Mathf.Abs(p.x) <= 501 && Mathf.Abs(p.y) <= 81;
                    }
                    check(finite && bounded, "Fractional Canvas scaling leaves crisp glyph geometry finite and within its label");
                    label.text = "";
                    Populate(label, mesh);
                    check(mesh.currentVertCount == 0, "Clearing a label clears old glyph geometry");
                    label.text = "PLAY";
                    Populate(label, mesh);
                    check(mesh.currentVertCount > 0, "A cleared label can rebuild its font mesh normally");
                }
            }
            finally
            {
                MatchPalette.SetTheme(original);
                if (!hadPreference) { PlayerPrefs.DeleteKey(MatchPalette.ThemePreferenceKey); PlayerPrefs.Save(); }
                UnityEngine.Object.DestroyImmediate(menu.gameObject);
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static void Populate(SharpText text, VertexHelper mesh) =>
            typeof(SharpText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(VertexHelper) }, null)
                .Invoke(text, new object[] { mesh });
    }
}
