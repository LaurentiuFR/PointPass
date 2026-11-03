using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointPass.Audio;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEditor;
using UnityEngine;

namespace PointPass.Validation
{
    public static class LobbyPlayMode
    {
        private static string Folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/LobbyReview");
        private static string Report => Path.Combine(Folder, "live-tests.txt");
        private static MatchMenu menu;
        private static void Check(bool condition, string description)
        {
            File.AppendAllText(Report, (condition ? "PASS " : "FAIL ") + description + "\n");
            if (!condition) throw new InvalidOperationException(description);
        }
        private static async Task Wait(Func<bool> predicate, float seconds, string name)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!predicate() && timer.Elapsed.TotalSeconds < seconds) await Task.Delay(20);
            Check(predicate(), name);
        }
        private static void Click(string name)
        {
            var button = menu.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == name && b.IsInteractable());
            if (button == null) throw new InvalidOperationException("No active button " + name);
            button.onClick.Invoke();
        }
        private static async Task Capture(string name)
        {
            await Task.Delay(160);
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        }

        private sealed class NativeView : IDisposable
        {
            private readonly Type viewType, sizesType, sizeType, kindType;
            private readonly EditorWindow view;
            private readonly object group;
            private readonly PropertyInfo selected;
            private readonly int original;
            private readonly List<object> added = new List<object>();
            public NativeView()
            {
                Assembly editor = typeof(Editor).Assembly;
                viewType = editor.GetType("UnityEditor.GameView"); sizesType = editor.GetType("UnityEditor.GameViewSizes");
                sizeType = editor.GetType("UnityEditor.GameViewSize"); kindType = editor.GetType("UnityEditor.GameViewSizeType");
                view = EditorWindow.GetWindow(viewType);
                selected = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                original = (int)selected.GetValue(view);
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            }
            public async Task Resize(int width, int height)
            {
                object size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "PointPass automated review" });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); added.Add(size);
                int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null)
                    + (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { size });
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { index, null });
                view.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                await Task.Delay(180);
                var current = viewType.GetProperty("currentGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(view);
                File.AppendAllText(Report, "VIEW requested index=" + index + " selected=" + selected.GetValue(view)
                    + " size=" + sizeType.GetProperty("width").GetValue(current) + "x" + sizeType.GetProperty("height").GetValue(current)
                    + " screen=" + Screen.width + "x" + Screen.height + "\n");
                await Wait(() => Screen.width == width && Screen.height == height, 3, "Native Game view renders at " + width + "x" + height);
                await Task.Delay(180);
            }
            public void Dispose()
            {
                viewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { original, null });
                for (int i = added.Count - 1; i >= 0; i--)
                {
                    int index = (int)group.GetType().GetMethod("IndexOf").Invoke(group, new[] { added[i] });
                    int builtins = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtins + index });
                }
                view.Repaint();
            }
        }

        public static async Task<string> Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Folder); File.WriteAllText(Report, "PointPass native Unity lobby and game integration\n");
            var match = UnityEngine.Object.FindFirstObjectByType<MatchManager>();
            menu = UnityEngine.Object.FindFirstObjectByType<MatchMenu>(FindObjectsInactive.Include);
            var human = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).First(p => p.GetComponent<HumanPlayerInput>() != null);
            var input = human.GetComponent<HumanPlayerInput>(); bool inputEnabled = input.enabled;
            bool hadSave = PlayerPrefs.HasKey(LobbyProfile.SaveKey); string saved = PlayerPrefs.GetString(LobbyProfile.SaveKey, "");
            float volume = AudioPreferences.Volume; bool muted = AudioPreferences.Muted;
            EditorApplication.CallbackFunction keepLive = () => { if (match != null && match.IsUserPaused) match.ResumeMatch(); };
            EditorApplication.update += keepLive;
            NativeView viewport = null;
            try
            {
                input.enabled = false; match.OpenMainMenu(); menu.Lobby.Profile.ResetDemoWallet();
                viewport = new NativeView(); await viewport.Resize(1920, 1080);
                int frame = Time.frameCount; await Task.Delay(150);
                Check(Time.frameCount > frame, "Unity player-loop frames advance during the actual lobby review");
                Check(menu.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>().Length == 0
                    && UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1,
                    "One EventSystem handles game and menu input");
                var art = menu.transform.Find("SafeArea/Content/Main/MainLobbyPage/Slot3/Captain3").GetComponent<UnityEngine.UI.RawImage>();
                await Wait(() => art.texture != null, 2, "Captain uses the actual in-match actor rendered into the lobby");
                Check(menu.GetComponentInChildren<NativeLobbyArt>().UsesMatchAssets,
                    "Backdrop and player presentation reuse PointPass's actual match renderer and assets");
                await Capture("lobby-1920x1080");
                for (int size = 1; size <= 5; size++)
                {
                    Click("Format" + size); await Task.Delay(60);
                    Check(menu.Lobby.SquadSize == size && Enumerable.Range(0, 5).Count(menu.Lobby.IsSlotAvailable) == size && menu.Lobby.IsSlotAvailable(2),
                        size + "v" + size + " correctly dims and locks the flanking pods while YOU stays in slot 3");
                    if (size == 1 || size == 3) await Capture("lobby-" + size + "v" + size);
                }
                Click("AddFriend2");
                Check(menu.Lobby.ModalOpen, "Add Friend opens an interactive local-preview dialog");
                menu.GetComponentInChildren<UnityEngine.UI.InputField>().text = "SQUADMATE"; Click("AddPreviewGuest");
                Check(menu.Lobby.GuestAt(1) == "SQUADMATE", "Named preview player occupies the selected flanking slot");
                Click("Format1"); Click("Format5"); Check(menu.Lobby.GuestAt(1) == null, "Reducing the squad removes guests from locked slots");

                foreach (LobbyMenuView.Page page in Enum.GetValues(typeof(LobbyMenuView.Page)))
                {
                    Click(page + "Nav"); await Task.Delay(80);
                    Check(menu.Lobby.CurrentPage == page, page + " tab routes to its dedicated screen");
                    var coin = menu.GetComponentsInChildren<UnityEngine.UI.Text>().First(t => t.name == "GoldCoins");
                    Check(coin.text.Contains("10,000") && coin.gameObject.activeInHierarchy, "Global wallet remains visible on " + page);
                    await Capture(page.ToString().ToLowerInvariant() + "-1920x1080");
                }
                Click("ItemShopNav"); Click("Purchase0"); Click("DialogPrimary");
                Check(menu.Lobby.Profile.Coins == 8800 && menu.Lobby.Profile.Owns(0), "Gold test purchase debits and unlocks exactly once");
                Click("Purchase1"); Click("DialogPrimary");
                Check(menu.Lobby.Profile.Points == 175 && menu.Lobby.Profile.Owns(1), "Point test purchase uses the separate points balance");
                Click("PlayerHubNav"); Click("Equip0");
                Check(menu.Lobby.Profile.Equipped == 0, "Player Hub equips an owned local badge");
                Check(menu.transform.Find("SafeArea/Content/Main/MainLobbyPage/Slot3/Ready").GetComponent<UnityEngine.UI.Text>().text.Contains("AQUA STRIKE"),
                    "Equipping a cosmetic updates the captain's actual lobby badge");
                var restored = new LobbyProfile(); Check(restored.Coins == 8800 && restored.Points == 175 && restored.Equipped == 0, "Purchased inventory and wallet survive a profile reload");
                await Capture("player-hub-purchased");
                Click("SettingsNav");
                var slider = menu.GetComponentInChildren<UnityEngine.UI.Slider>(); slider.value = 0.3f;
                var toggle = menu.GetComponentInChildren<UnityEngine.UI.Toggle>(); toggle.isOn = true;
                Check(AudioPreferences.Muted && Mathf.Approximately(AudioPreferences.Volume, 0.3f), "New Settings screen controls the actual game's master sound");
                toggle.isOn = false;
                Click("HowToPlay"); Check(menu.CurrentScreen == MatchMenu.Screen.Controls, "Settings opens the real controls page");
                menu.TryBack(); Check(menu.Lobby.CurrentPage == LobbyMenuView.Page.Settings, "Back retains the originating Settings tab");

                await viewport.Resize(1280, 960); Click("MainLobbyNav"); await Capture("lobby-1280x960");
                Click("GameModesNav"); await Capture("game-modes-1280x960");
                Vector3[] corners = new Vector3[4];
                var bar = menu.transform.Find("SafeArea/Content/Main/GlobalHeader/NavigationBar").GetComponent<RectTransform>();
                bar.GetWorldCorners(corners);
                Check(corners.All(c => c.x >= 0 && c.x <= Screen.width && c.y >= 0 && c.y <= Screen.height), "Top navigation remains inside a 4:3 safe area");
                var arena = menu.transform.Find("SafeArea/Content/Main/GameModesPage/OnlineArena").GetComponent<RectTransform>();
                arena.GetWorldCorners(corners);
                Check(corners.All(c => c.x >= 0 && c.x <= Screen.width && c.y >= 0 && c.y <= Screen.height), "Unified mode grid remains within the 4:3 viewport");
                await viewport.Resize(1920, 1080);

                Click("MechanicsLab"); Click("PassingPractice"); await Task.Delay(100);
                Check(match.IsPracticing && !match.GetComponent<PassingPractice>().IsFreePlay && !menu.IsOpen,
                    "Mechanics Lab launches the real scored passing drill");
                await Capture("passing-drill"); match.OpenMainMenu(); Click("GameModesNav"); Click("FreePlay"); await Task.Delay(100);
                var practice = match.GetComponent<PassingPractice>();
                Check(practice.IsRunning && practice.IsFreePlay && !menu.IsOpen,
                    "Free Play launches an untimed session without passing targets");
                Check(!practice.transform.Find("PracticeGates").gameObject.activeSelf, "Free Play hides drill gates and their feedback");
                await Capture("free-play"); match.OpenMainMenu(); Click("GameModesNav"); Click("ScenarioTrainer"); Click("PenaltyPreset");
                await Task.Delay(100); Check(practice.IsFreePlay && Mathf.Abs(human.Body.position.x - 5.5f) < 0.05f, "Penalty preset places the real player at a shooting position");
                match.OpenMainMenu(); Click("GameModesNav"); Click("AiBotMatches"); Click("Difficulty2"); Click("PlayOffline");
                await Wait(() => match.IsMatchLive, 4, "Keeper Challenge launches an actual match from Game Modes");
                Check(UnityEngine.Object.FindObjectsByType<GoalkeeperBrain>(FindObjectsSortMode.None).All(k => Mathf.Approximately(k.ReactionDelay, 0.14f)),
                    "Chosen AI difficulty changes actual goalkeeper reaction timing");
                await Capture("keeper-challenge"); match.OpenMainMenu(); Click("FindMatch");
                Check(menu.Lobby.ModalOpen, "Find Match shows connection status rather than claiming an online match");
                await Capture("find-match"); Click("DialogPrimary"); await Wait(() => match.IsMatchLive, 4, "Find Match offline action launches the current game");
                Check(UnityEngine.Object.FindObjectsByType<GoalkeeperBrain>(FindObjectsSortMode.None).All(k => Mathf.Approximately(k.ReactionDelay, 0.2f)),
                    "Ordinary offline match restores standard keeper difficulty");
                match.OpenMainMenu(); menu.Lobby.Profile.ResetDemoWallet(); await Capture("lobby-final");
                File.AppendAllText(Report, "COMPLETE Native lobby, all tabs, wallet, training and match launch checks passed.\n");
                return File.ReadAllText(Report);
            }
            catch (Exception error) { File.AppendAllText(Report, error + "\n"); throw; }
            finally
            {
                EditorApplication.update -= keepLive;
                viewport?.Dispose(); input.enabled = inputEnabled;
                AudioPreferences.SetVolume(volume); AudioPreferences.SetMuted(muted);
                if (hadSave) PlayerPrefs.SetString(LobbyProfile.SaveKey, saved); else PlayerPrefs.DeleteKey(LobbyProfile.SaveKey);
                PlayerPrefs.Save(); match.OpenMainMenu();
            }
        }
    }
}
