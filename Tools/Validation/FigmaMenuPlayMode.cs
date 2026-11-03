using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointPass.UI;
using PointPass.Gameplay;
using PointPass.Core;
using PointPass.Players;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PointPass.Validation
{
    public static class FigmaMenuPlayMode
    {
        private static readonly string Folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/PdfMenuReview");
        private static string Report => Path.Combine(Folder, "verification.txt");
        private static MatchMenu menu;
        private static void Check(bool okay, string label)
        {
            File.AppendAllText(Report, (okay ? "PASS " : "FAIL ") + label + "\n");
            if (!okay) throw new InvalidOperationException(label);
        }
        private static async Task Capture(string name)
        {
            await Task.Delay(180);
            Canvas.ForceUpdateCanvases();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
        private static async Task Click(string name)
        {
            var button = menu.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == name && b.IsInteractable());
            Check(button != null, name + " is an active selectable");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            await Task.Delay(80);
        }
        private sealed class Viewport : IDisposable
        {
            private readonly Type viewType, sizeType, kindType;
            private readonly EditorWindow view;
            private readonly object group;
            private readonly int original;
            private readonly List<object> added = new List<object>();
            public Viewport()
            {
                var assembly = typeof(Editor).Assembly;
                viewType = assembly.GetType("UnityEditor.GameView");
                sizeType = assembly.GetType("UnityEditor.GameViewSize");
                kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                view = EditorWindow.GetWindow(viewType); view.Focus();
                original = (int)viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(view);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            }
            public async Task Resize(int width, int height)
            {
                var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "PointPass Figma review" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); added.Add(size);
                int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) + (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { size });
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(view, new object[] { index, null });
                view.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(350);
                Check(Screen.width == width && Screen.height == height, "Native viewport " + width + "x" + height);
            }
            public void Dispose()
            {
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(view, new object[] { original, null });
                foreach (var size in added.AsEnumerable().Reverse())
                {
                    int index = (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { size });
                    int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtins + index });
                }
            }
        }
        public static async Task<string> Run()
        {
            Directory.CreateDirectory(Folder); File.WriteAllText(Report, "PointPass PDF clubhouse / native Unity integration\n");
            Check(Application.isPlaying, "Actual Unity Play mode");
            menu = UnityEngine.Object.FindFirstObjectByType<MatchMenu>(FindObjectsInactive.Include);
            var match = UnityEngine.Object.FindFirstObjectByType<MatchManager>();
            var human = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.GetComponent<HumanPlayerInput>() != null);
            var input = human.GetComponent<HumanPlayerInput>();
            bool inputEnabled = input.enabled;
            MatchPalette.InterfaceTheme originalTheme = MatchPalette.Theme;
            bool hadTheme = PlayerPrefs.HasKey(MatchPalette.ThemePreferenceKey);
            var runtimeErrors = new List<string>();
            Application.LogCallback recordError = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors.Add(message);
            };
            Application.logMessageReceived += recordError;
            EditorApplication.CallbackFunction keepLive = () => { if (match != null && match.IsUserPaused) match.ResumeMatch(); };
            EditorApplication.update += keepLive;
            input.enabled = false;
            try
            {
            int start = Time.frameCount; await Task.Delay(180);
            Check(Time.frameCount > start, "Player-loop frames advance");
            using (var viewport = new Viewport())
            {
                match.OpenMainMenu(); await viewport.Resize(844, 390);
                await Capture("home-844x390");
                Check(menu.Lobby.GetComponentsInChildren<LobbyPodGraphic>().Length == 0, "Main menu has no old squad podiums");
                Check(menu.GetComponentInChildren<ClubhousePlayerGraphic>().HasArtwork, "Reference footballer uses scalable vector geometry");
                var hero=menu.GetComponentInChildren<ClubhousePlayerGraphic>();
                Check(hero.GetComponent<CanvasRenderer>() != null && hero.canvasRenderer.GetMesh() != null && hero.canvasRenderer.GetMesh().vertexCount > 0,
                    "Footballer geometry is submitted to the actual Canvas renderer");
                Check(menu.transform.Find("SafeArea/Content/Main/MainLobbyPage/TouchlinePitch") == null, "Overhead pitch panel has been removed from the main menu");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One input EventSystem");
                await Click("Play");
                Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.GameModes, "Play opens a separate mode selector");
                Check(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("MatchFormat")) == 6, "1v1 through 5v5 and Custom Lobby are available");
                await Capture("mode-selector-844x390");
                await Click("MatchFormat3"); Check(menu.Lobby.ModalOpen && menu.Lobby.SquadSize == 3, "Multiplayer preview preserves selected format");
                menu.Lobby.CloseModal(); menu.ShowMain();
                await Click("HowToPlay");
                for (int i = 0; i < 4; i++) await Click("GuideNext");
                await Capture("guide-practice-link");
                await Click("GuideNext"); await Task.Delay(180);
                Check(!menu.IsOpen, "Guide practice action launches actual practice");
                Check(UnityEngine.Object.FindFirstObjectByType<PassingPractice>() != null, "Passing Practice gameplay exists");
                match.OpenMainMenu();
                await Click("Settings"); Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.Settings, "Settings opens actual audio controls");
                for (int i = 0; i < 3; i++)
                {
                    MatchPalette.InterfaceTheme expected = (MatchPalette.InterfaceTheme)(((int)MatchPalette.Theme + 1) % 3);
                    await Click("ColourScheme");
                    Check(MatchPalette.Theme == expected && PlayerPrefs.GetInt(MatchPalette.ThemePreferenceKey) == (int)expected,
                        "Settings button applies and saves " + expected);
                }
                await Capture("settings");
                menu.ShowMain(); await Click("Shop"); Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.ItemShop, "Shop preview remains accessible");
                menu.ShowMain(); await Click("MyClub"); Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.PlayerHub, "My Club remains accessible");
                await Task.Delay(180);
                var nativeArt = menu.Lobby.GetComponent<NativeLobbyArt>();
                var matchTarget = (RenderTexture)typeof(NativeLobbyArt).GetField("matchTexture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(nativeArt);
                Check(nativeArt.PlayerTexture.IsCreated() && matchTarget.IsCreated() && matchTarget.depth > 0,
                    "My Club camera targets are created with the depth attachment required by URP");
                await Capture("my-club");
                menu.ShowMain(); await Click("Leaderboards"); Check(menu.Lobby.ModalOpen, "Leaderboard connection state is explicit");
                menu.Lobby.CloseModal();
                await viewport.Resize(1920, 1080); menu.ShowMain(); await Capture("home-1920x1080");
                await viewport.Resize(1280, 720); menu.ShowMain(); await Capture("home-1280x720");
                await viewport.Resize(1280, 960); menu.ShowMain(); await Capture("home-1280x960");
                await Click("Play"); await Click("Training"); await Click("MechanicsLab"); await Click("PassingPractice");
                await Task.Delay(180);
                var practice = UnityEngine.Object.FindFirstObjectByType<PassingPractice>();
                Check(practice.IsRunning && !practice.IsFreePlay, "Training menu launches the scored passing drill");
                match.OpenMainMenu(); await Click("Play"); await Click("Training"); await Click("FreePlay"); await Task.Delay(180);
                Check(practice.IsFreePlay && !practice.transform.Find("PracticeGates").gameObject.activeSelf, "Free Play keeps targets and clocks out of the drill");
                Check(UnityEngine.Object.FindObjectsByType<GoalkeeperBrain>(FindObjectsSortMode.None).Length == 0, "Practice hides offline goalkeeper actors");
                match.OpenMainMenu(); await Click("Play"); await Click("Training"); await Click("ScenarioTrainer"); await Click("PenaltyPreset"); await Task.Delay(180);
                Check(practice.IsFreePlay && Mathf.Abs(human.Body.position.x - 5.5f) < 0.05f, "Penalty menu launches the existing scenario preset");
                match.OpenMainMenu(); await Click("Play"); await Click("Training"); await Click("AiBotMatches"); await Click("Difficulty2"); await Click("PlayOffline");
                await Task.Delay(180);
                Check(UnityEngine.Object.FindObjectsByType<GoalkeeperBrain>(FindObjectsSortMode.None).All(k => Mathf.Approximately(k.ReactionDelay, 0.14f)), "Selected keeper difficulty reaches match simulation");
                match.OpenMainMenu(); await Click("Play"); await Click("PlayOffline"); await Task.Delay(180);
                Check(!menu.IsOpen && UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length == 3,
                    "Local Play enters one human plus two offline keepers");
                await Capture("local-gameplay");
                match.OpenMainMenu();
            }
            Check(runtimeErrors.Count == 0, "Menu rendering, navigation and viewport cleanup emit no runtime errors: " + string.Join("; ", runtimeErrors));
            File.AppendAllText(Report, "COMPLETE\n"); return Report;
            }
            finally
            {
                EditorApplication.update -= keepLive;
                Application.logMessageReceived -= recordError;
                input.enabled = inputEnabled;
                MatchPalette.SetTheme(originalTheme);
                if (!hadTheme) { PlayerPrefs.DeleteKey(MatchPalette.ThemePreferenceKey); PlayerPrefs.Save(); }
                match.OpenMainMenu();
            }
        }
    }
}
