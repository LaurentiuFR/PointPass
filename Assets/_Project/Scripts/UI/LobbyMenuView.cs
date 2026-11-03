using System;
using System.Collections.Generic;
using PointPass.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using static PointPass.UI.LobbyUi;

namespace PointPass.UI
{
    /// <summary>The front end of the offline game, with explicit previews for online services.</summary>
    public partial class LobbyMenuView : MonoBehaviour
    {
        public enum Page { MainLobby, GameModes, PlayerHub, ItemShop, CommunityHub, Settings }
        private readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
        private readonly Dictionary<Page, UnityEngine.UI.Button> tabs = new Dictionary<Page, UnityEngine.UI.Button>();
        private readonly LobbyPodGraphic[] pods = new LobbyPodGraphic[5];
        private readonly CanvasGroup[] podGroups = new CanvasGroup[5];
        private readonly UnityEngine.UI.Button[] friendButtons = new UnityEngine.UI.Button[5];
        private readonly UnityEngine.UI.Text[] slotDescriptions = new UnityEngine.UI.Text[5];
        private readonly LobbyIcon[] silhouettes = new LobbyIcon[5];
        private readonly string[] guests = new string[5];
        private readonly UnityEngine.UI.Button[] formats = new UnityEngine.UI.Button[5];
        private readonly UnityEngine.UI.Button[] shopButtons = new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.Button[] inventoryButtons = new UnityEngine.UI.Button[3];
        private readonly UnityEngine.UI.Text[] inventoryStates = new UnityEngine.UI.Text[3];
        private MatchMenu host;
        private UnityEngine.UI.Text coins, points, wordmark, formation, equipped, captainBadge;
        private RectTransform modalContent;
        private NativeLobbyArt nativeArt;
        private GameObject modal;
        private UnityEngine.UI.InputField guestName;

        private int difficulty = 1;
        private int guideStep;
        private UnityEngine.UI.Button homePlay;
        private UnityEngine.UI.Button backToLobby;
        private UnityEngine.UI.Text partyCount;
        public Vector2 DesignSize => CurrentPage == Page.MainLobby || CurrentPage == Page.GameModes ? new Vector2(1600, 900) : new Vector2(1824, 1000);
        private readonly int[] activationOrder = { 2, 1, 3, 0, 4 };
        public LobbyProfile Profile { get; private set; }
        public Page CurrentPage { get; private set; }
        public int SquadSize { get; private set; } = 5;
        public bool ModalOpen => modal != null && modal.activeSelf;
        public UnityEngine.UI.Button FirstButton => homePlay;
        public string GuestAt(int slot) => slot >= 0 && slot < 5 ? guests[slot] : null;

        public void Build(MatchMenu owner)
        {
            host = owner; Profile = new LobbyProfile();
            nativeArt = gameObject.AddComponent<NativeLobbyArt>();
            nativeArt.BuildBackground();
            Stretch(Image(transform, "Atmosphere", Vector2.zero, new Vector2(1824, 1000), ClubhouseNavy).rectTransform);
            foreach (Page page in Enum.GetValues(typeof(Page)))
            {
                RectTransform root = Rect(transform, page + "Page", Vector2.zero, new Vector2(1824, 1000));
                pages.Add(page, root.gameObject);
            }
            BuildLobby(); BuildModes(); BuildPlayer(); BuildShop(); BuildCommunity(); BuildSettings();
            BuildHeader(); BuildModal();
            Profile.Changed += RefreshProfile;
            RefreshProfile(); SetSquadSize(5); ShowPage(Page.MainLobby);
        }

        private void BuildFooter()
        {
            RectTransform footer = Rect(transform, "GlobalFooter", new Vector2(0, 50), new Vector2(1824, 100));
            footer.anchorMin = footer.anchorMax = new Vector2(0.5f, 0);
            Image(footer, "FooterRule", new Vector2(0, 21), new Vector2(1720, 1), new Color(Cyan.r, Cyan.g, Cyan.b, 0.2f));
            var leaderboard = Button(footer, "GlobalLeaderboards", "GLOBAL LEADERBOARDS", new Vector2(-657, -20),
                new Vector2(370, 48), () => OpenOnline("GLOBAL LEADERBOARDS", "Rankings will appear here when PointPass is connected to its online service."));
            Icon(leaderboard.transform, "Icon", LobbyIcon.Symbol.Trophy, new Vector2(-166, 0), 22);
            var finder = Button(footer, "PlayerFinder", "PLAYER FINDER", new Vector2(-258, -20), new Vector2(300, 48),
                () => OpenOnline("PLAYER FINDER", "Connect to find teammates and invite friends to your squad."));
            Icon(finder.transform, "Icon", LobbyIcon.Symbol.Search, new Vector2(-123, 0), 23);
            Text(footer, "NoSignal", "NO SIGNAL", 17, new Vector2(796, -20), new Vector2(150, 45), Dim, true);
            Icon(footer, "ConnectionIcon", LobbyIcon.Symbol.Signal, new Vector2(699, -20), 22, Dim);
        }

        private void OpenParty()
        {
            foreach (int slot in activationOrder)
                if (slot != 2 && IsSlotAvailable(slot) && string.IsNullOrEmpty(guests[slot])) { OpenFriend(slot); return; }
            OpenDialog("YOUR PARTY", "Your preview party is full. Online invitations are not connected. Choose a larger format or continue solo in the local game.", "CHOOSE FORMAT", () => ShowPage(Page.GameModes));
        }

        public void ShowGuide() { guideStep = 0; BuildGuide(); }
        private void BuildGuide()
        {
            ClearDialog();
            modalContent.sizeDelta = new Vector2(1320, 720);
            ((RectTransform)modalContent.Find("DialogPlate")).sizeDelta = modalContent.sizeDelta;
            string[] headings = { "MOVE & DRIBBLE", "PASS", "SHOOT & CURVE", "SPRINT", "CANCEL A KICK" };
            string[] instructions = { "Use the left joystick to move and guide the ball.", "Hold PASS to charge. Aim with your movement direction, then release.", "Hold SHOOT for power. Flick sideways as you release to bend the shot.", "Hold SPRINT while moving.", "Tap CANCEL while charging to stop the kick." };
            string[] captions = { "LEFT JOYSTICK", "HOLD → RELEASE", "FLICK SIDEWAYS", "HOLD SPRINT", "TAP CANCEL" };
            Text(modalContent, "GuideBrand", "POINTPASS", 29, new Vector2(-480, 290), new Vector2(270, 50), Aqua, true);
            Text(modalContent, "GuideStep", (guideStep + 1) + " / 05", 25, new Vector2(535, 290), new Vector2(150, 50), Dim, true);
            Icon(modalContent, "ControlVisual", guideStep == 0 ? LobbyIcon.Symbol.Target : guideStep == 4 ? LobbyIcon.Symbol.Plus : LobbyIcon.Symbol.Arrow, new Vector2(-360, 30), 180, Aqua);
            Text(modalContent, "ControlCaption", captions[guideStep], 27, new Vector2(-360, -130), new Vector2(470, 55), Aqua, true);
            Text(modalContent, "GuideHeading", headings[guideStep], 46, new Vector2(260, 130), new Vector2(680, 85), null, true, TextAnchor.MiddleLeft);
            Text(modalContent, "GuideInstruction", instructions[guideStep], 35, new Vector2(260, -4), new Vector2(680, 150), null, false, TextAnchor.UpperLeft);
            var back = Button(modalContent, "GuidePrevious", guideStep == 0 ? "BACK TO LOBBY" : "PREVIOUS", new Vector2(-422, -262), new Vector2(360, 96), () => { if (guideStep == 0) CloseModal(); else { guideStep--; BuildGuide(); } });
            var next = Button(modalContent, "GuideNext", guideStep == 4 ? "TRY PASSING PRACTICE" : "NEXT", new Vector2(355, -262), new Vector2(500, 96), () => { if (guideStep == 4) host.RequestPassingPractice(); else { guideStep++; BuildGuide(); } }, true);
            Text(modalContent, "GuidePrinciple", "One player. Every pass matters.", 23, new Vector2(0, -334), new Vector2(700, 34), Dim);
            Select(next);
        }

        private void Header(Transform page, string eyebrow, string title, string description)
        {
            Text(page, "Eyebrow", eyebrow, 15, new Vector2(0, 367), new Vector2(1560, 30), Cyan, true);
            Text(page, "PageTitle", title, 41, new Vector2(0, 310), new Vector2(1650, 66), null, true);
            Text(page, "Description", description, 19, new Vector2(0, 252), new Vector2(1650, 42), Dim);
        }

        private UnityEngine.UI.Button ModeCard(Transform parent, string name, string title, string body, LobbyIcon.Symbol symbol,
            Vector2 position, Vector2 size, Action action)
        {
            var button = Button(parent, name, "", position, size, action);
            Icon(button.transform, "Icon", symbol, new Vector2(-size.x * 0.5f + 39, size.y * 0.5f - 34), 30);
            Text(button.transform, "Title", title, 36, new Vector2(17, size.y * 0.5f - 35), new Vector2(size.x - 108, 50), null, true, TextAnchor.MiddleLeft);
            Text(button.transform, "Details", body, 29, new Vector2(0, -20), new Vector2(size.x - 42, size.y - 62), Dim, false, TextAnchor.MiddleLeft);
            return button;
        }

        private void BuildPlayer()
        {
            Transform page = pages[Page.PlayerHub].transform;
            Header(page, "YOUR GAME. YOUR IDENTITY.", "PLAYER HUB", "Your captain and your local cosmetic collection.");
            var identity = Panel(page, "CaptainProfile", new Vector2(-558, -80), new Vector2(570, 622));
            nativeArt.PlayerPicture(identity.transform, "Captain", new Vector2(0, 20), new Vector2(380, 380));
            Text(identity.transform, "Name", "PLAYER 3 (YOU)", 26, new Vector2(0, -231), new Vector2(500, 50), null, true);
            equipped = Text(identity.transform, "Equipped", "", 16, new Vector2(0, -275), new Vector2(520, 35), Cyan);
            for (int item = 0; item < 3; item++)
            {
                int index = item;
                var card = Panel(page, "Inventory" + item, new Vector2(310, 115 - item * 206), new Vector2(1070, 178));
                Icon(card.transform, "Icon", item == 1 ? LobbyIcon.Symbol.Coin : LobbyIcon.Symbol.Bag, new Vector2(-457, 7), 55);
                Text(card.transform, "Name", LobbyProfile.ItemNames[item], 24, new Vector2(-128, 34), new Vector2(525, 48), null, true, TextAnchor.MiddleLeft);
                inventoryStates[item] = Text(card.transform, "State", "", 17, new Vector2(-128, -20), new Vector2(525, 42), Dim, false, TextAnchor.MiddleLeft);
                inventoryButtons[item] = Button(card.transform, "Equip" + item, "VISIT SHOP", new Vector2(350, 0), new Vector2(280, 58),
                    () => { if (Profile.Owns(index)) Profile.Equip(index); else ShowPage(Page.ItemShop); });
            }
        }

        private void BuildShop()
        {
            Transform page = pages[Page.ItemShop].transform;
            Header(page, "MAKE YOUR MARK.", "ITEM SHOP", "Preview cosmetics with demo currency. No real-money purchases.");
            string[] details = { "A crisp aqua identity accent.\nUnlock for your lobby collection.", "A captain's podium signature.\nUnlock a little extra presence.", "Deep navy. Pure football.\nA badge for your lobby collection." };
            for (int item = 0; item < 3; item++)
            {
                int index = item;
                var card = Panel(page, "ShopItem" + item, new Vector2((item - 1) * 567, -83), new Vector2(530, 590));
                Image(card.transform, "ItemAura", new Vector2(0, 100), new Vector2(286, 286), new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f), SpriteLibrary.SoftDisc());
                Icon(card.transform, "ItemIcon", item == 1 ? LobbyIcon.Symbol.Coin : item == 2 ? LobbyIcon.Symbol.User : LobbyIcon.Symbol.Bag,
                    new Vector2(0, 115), 120, item == 1 ? Aqua : Cyan);
                Text(card.transform, "Name", LobbyProfile.ItemNames[item], 30, new Vector2(0, -30), new Vector2(480, 70), null, true);
                Text(card.transform, "Description", details[item], 20, new Vector2(0, -113), new Vector2(460, 84), Dim);
                shopButtons[item] = Button(card.transform, "Purchase" + item, "", new Vector2(0, -237), new Vector2(450, 69), () => Purchase(index));
                Text(card.transform, "LocalOnly", "LOCAL PREVIEW ITEM", 13, new Vector2(0, 268), new Vector2(450, 32), Cyan, true);
            }
        }

        private void BuildCommunity()
        {
            Transform page = pages[Page.CommunityHub].transform;
            Header(page, "BETTER WITH YOUR SQUAD.", "COMMUNITY HUB", "The future home of your teammates, rivalries and match-day stories.");
            string[] names = { "SQUAD FINDER", "GLOBAL LEADERBOARDS", "MATCH HIGHLIGHTS" };
            string[] body = { "Find a player who sees the same pass.\nOnline player discovery needs a connection.",
                "Follow the climb to the top.\nRankings will appear when online play launches.",
                "Your best moments stay with you.\nScore in an offline match, then watch the goal\nfrom Pause or the full-time screen." };
            for (int i = 0; i < 3; i++)
            {
                int cardIndex = i;
                var card = ModeCard(page, "CommunityCard" + i, names[i], body[i], i == 1 ? LobbyIcon.Symbol.Trophy : LobbyIcon.Symbol.People,
                    new Vector2((i - 1) * 567, -33), new Vector2(530, 448),
                    () => { if (cardIndex == 2) host.RequestOfflineMatch(); else OpenOnline(names[cardIndex], body[cardIndex]); });
                card.transform.Find("Title").GetComponent<UnityEngine.UI.Text>().fontSize = 25;
            }
        }

        private void BuildSettings()
        {
            Transform page = pages[Page.Settings].transform;
            Header(page, "SET UP YOUR MATCHDAY.", "SETTINGS", "Keep the crowd loud and your controls comfortable.");
            var sound = Panel(page, "SoundSettings", new Vector2(-300, -90), new Vector2(1080, 610));
            Text(sound.transform, "Title", "SOUND SETTINGS", 28, new Vector2(0, 247), new Vector2(900, 60), null, true);
            sound.gameObject.AddComponent<AudioSettingsView>().Build();
            var options = Panel(page, "Options", new Vector2(575, -90), new Vector2(540, 610));
            Icon(options.transform, "SettingsIcon", LobbyIcon.Symbol.Gear, new Vector2(0, 186), 64);
            Text(options.transform, "Title", "MAKE IT YOUR GAME", 25, new Vector2(0, 100), new Vector2(490, 60), null, true);
            Button(options.transform, "HowToPlay", "HOW TO PLAY", new Vector2(0, 6), new Vector2(440, 64), () => host.RequestControls());
            Button(options.transform, "ResetDemoWallet", "RESET PREVIEW WALLET", new Vector2(0, -94), new Vector2(440, 64),
                () => OpenDialog("RESET PREVIEW WALLET", "Restore 10,000 gold coins and 250 pass points? This also clears locally owned preview items.",
                    "RESET WALLET", () => { Profile.ResetDemoWallet(); CloseModal(); }));
            Text(options.transform, "SavedHint", "Sound and preview purchases are saved\non this device. No account required.", 17,
                new Vector2(0, -218), new Vector2(460, 86), Dim);
        }

        private void BuildModal()
        {
            var overlay = Image(transform, "LobbyModal", Vector2.zero, new Vector2(1824, 1000), new Color(0.005f, 0.02f, 0.045f, 0.94f));
            overlay.raycastTarget = true; modal = overlay.gameObject;
            modalContent = Rect(overlay.transform, "DialogContent", Vector2.zero, new Vector2(1020, 650));
            modal.SetActive(false);
        }
        private void ClearDialog()
        {
            for (int i = modalContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = modalContent.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            modalContent.sizeDelta = new Vector2(1020, 650);
            guestName = null; modal.SetActive(true); modal.transform.SetAsLastSibling();
            Panel(modalContent, "DialogPlate", Vector2.zero, new Vector2(1020, 650));
        }
        private void DialogHeading(string title, string body)
        {
            Text(modalContent, "DialogEyebrow", "POINTPASS  /  GLOBAL ARENA", 14, new Vector2(0, 255), new Vector2(940, 36), Cyan, true);
            Text(modalContent, "DialogTitle", title, 34, new Vector2(0, 180), new Vector2(940, 100), null, true);
            Text(modalContent, "DialogBody", body, 23, new Vector2(0, 56), new Vector2(880, 140), Dim);
        }
        private void OpenDialog(string title, string body, string action, Action callback)
        {
            ClearDialog(); DialogHeading(title, body);
            var primary = Button(modalContent, "DialogPrimary", action, new Vector2(0, -107), new Vector2(660, 68), callback, true);
            Button(modalContent, "DialogBack", "BACK", new Vector2(0, -211), new Vector2(660, 57), CloseModal);
            Select(primary);
        }
        private void OpenOnline(string title, string body) => OpenDialog(title, body, "PLAY OFFLINE", () => { CloseModal(); host.RequestOfflineMatch(); });
        private void OpenFriend(int slot)
        {
            if (!IsSlotAvailable(slot) || slot == 2) return;
            if (!string.IsNullOrEmpty(guests[slot])) { SetGuest(slot, null); return; }
            ClearDialog(); DialogHeading("ADD FRIEND  /  SLOT " + (slot + 1),
                "Friends will join here once online services are connected. Add a named preview guest to try the squad layout locally.");
            var fieldImage = Panel(modalContent, "GuestNameField", new Vector2(0, -67), new Vector2(660, 58), new Color(0.015f, 0.06f, 0.1f));
            fieldImage.raycastTarget = true; guestName = fieldImage.gameObject.AddComponent<UnityEngine.UI.InputField>();
            var label = Text(fieldImage.transform, "Text", "", 22, Vector2.zero, new Vector2(610, 50), null, false, TextAnchor.MiddleLeft);
            guestName.textComponent = label; guestName.characterLimit = 20;
            guestName.contentType = UnityEngine.UI.InputField.ContentType.Standard;
            var placeholder = Text(fieldImage.transform, "Placeholder", "Guest name", 22, Vector2.zero, new Vector2(610, 50), Dim, false, TextAnchor.MiddleLeft);
            guestName.placeholder = placeholder; guestName.text = "GUEST " + (slot + 1);
            var add = Button(modalContent, "AddPreviewGuest", "ADD PREVIEW PLAYER", new Vector2(0, -160), new Vector2(660, 60),
                () => { if (guestName != null && SetGuest(slot, guestName.text)) CloseModal(); }, true);
            Button(modalContent, "DialogBack", "BACK", new Vector2(0, -250), new Vector2(660, 50), CloseModal); Select(add);
        }
        private void OpenMechanics()
        {
            ClearDialog(); DialogHeading("MECHANICS LAB", "Choose a skill to work on. Passing uses scored gates; dribbling and shooting open unrestricted ball practice.");
            Button(modalContent, "DribblingPractice", "DRIBBLING", new Vector2(-302, -110), new Vector2(280, 65), () => host.RequestFreePlay());
            var passing = Button(modalContent, "PassingPractice", "PASSING", new Vector2(0, -110), new Vector2(280, 65), () => host.RequestPassingPractice(), true);
            Button(modalContent, "ShootingPractice", "SHOOTING", new Vector2(302, -110), new Vector2(280, 65), () => host.RequestFreePlay());
            Button(modalContent, "DialogBack", "BACK", new Vector2(0, -225), new Vector2(600, 54), CloseModal); Select(passing);
        }
        private void OpenScenarios()
        {
            ClearDialog(); DialogHeading("SCENARIO TRAINER", "Rehearse your placement and shot bend from a fixed starting position. These are local technique presets without a defending wall.");
            var kick = Button(modalContent, "FreeKickPreset", "FREE KICKS", new Vector2(-225, -110), new Vector2(410, 65), () => host.RequestScenario(false));
            Button(modalContent, "PenaltyPreset", "PENALTIES", new Vector2(225, -110), new Vector2(410, 65), () => host.RequestScenario(true));
            Button(modalContent, "DialogBack", "BACK", new Vector2(0, -225), new Vector2(600, 54), CloseModal); Select(kick);
        }
        private void OpenAi()
        {
            ClearDialog(); DialogHeading("AI BOT MATCHES", "Play the current goalkeeper challenge with your chosen difficulty. Full outfield bot teams are still in development.");
            string[] names = { "RELAXED", "STANDARD", "CHALLENGING" };
            for (int level = 0; level < 3; level++)
            {
                int chosen = level;
                var button = Button(modalContent, "Difficulty" + level, names[level], new Vector2((level - 1) * 294, -69), new Vector2(270, 54),
                    () => { difficulty = chosen; OpenAi(); });
                if (difficulty == level) button.GetComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.4f, 0.43f);
            }
            var play = Button(modalContent, "PlayOffline", "PLAY KEEPER CHALLENGE", new Vector2(0, -160), new Vector2(710, 63),
                () => host.RequestOfflineMatch(difficulty), true);
            Button(modalContent, "DialogBack", "BACK", new Vector2(0, -251), new Vector2(600, 50), CloseModal); Select(play);
        }
        private void Purchase(int item)
        {
            if (Profile.Owns(item)) { Profile.Equip(item); return; }
            string currency = item == 1 ? "PASS POINTS" : "GOLD COINS";
            OpenDialog(LobbyProfile.ItemNames[item], "Unlock this local preview cosmetic for " + LobbyProfile.Prices[item].ToString("N0") + " " + currency
                + ". The demo wallet updates across every menu screen.", "CONFIRM PREVIEW PURCHASE", () =>
                {
                    if (Profile.Purchase(item)) { Profile.Equip(item); CloseModal(); }
                    else OpenDialog("NOT ENOUGH " + currency, "Your preview wallet does not have enough currency for this item.", "RETURN TO SHOP", CloseModal);
                });
        }

        public bool IsSlotAvailable(int slot)
        {
            for (int i = 0; i < SquadSize; i++) if (activationOrder[i] == slot) return true;
            return false;
        }
        public bool SetGuest(int slot, string name)
        {
            if (slot < 0 || slot > 4 || slot == 2 || !IsSlotAvailable(slot)) return false;
            if (name != null)
            {
                name = name.Trim().Replace("<", "").Replace(">", "");
                if (name.Length == 0) return false;
                if (name.Length > 20) name = name.Substring(0, 20);
            }
            guests[slot] = name; RefreshSquad(); return true;
        }
        public void SetSquadSize(int size)
        {
            if (size < 1 || size > 5) return;
            SquadSize = size;
            for (int i = 0; i < 5; i++) if (!IsSlotAvailable(i)) guests[i] = null;
            RefreshSquad();
        }
        private void RefreshSquad()
        {
            int occupied = 1;
            for (int i = 0; i < 5; i++)
            {
                if (i != 2 && IsSlotAvailable(i) && !string.IsNullOrEmpty(guests[i])) occupied++;
                if (pods[i] == null) continue;
                bool active = IsSlotAvailable(i); pods[i].Available = active; podGroups[i].alpha = active ? 1 : 0.28f;
                if (i == 2) continue;
                bool joined = !string.IsNullOrEmpty(guests[i]);
                friendButtons[i].interactable = active;
                friendButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text = !active ? "LOCKED" : joined ? "REMOVE GUEST" : "+ ADD FRIEND";
                silhouettes[i].Glyph = active ? LobbyIcon.Symbol.User : LobbyIcon.Symbol.Lock;
                silhouettes[i].color = joined ? Cyan : new Color(Cyan.r, Cyan.g, Cyan.b, 0.43f); silhouettes[i].SetVerticesDirty();
                slotDescriptions[i].text = !active ? "INACTIVE IN " + SquadSize + "v" + SquadSize : joined ? guests[i] + "\nPREVIEW PLAYER" : "WAITING FOR PLAYER...";
            }
            if (partyCount != null) partyCount.text = occupied + " / " + SquadSize;
            if (formation != null) formation.text = occupied + " / " + SquadSize + " PLAYERS  /  " + SquadSize + "v" + SquadSize;
            for (int i = 0; i < formats.Length; i++) if (formats[i] != null)
                formats[i].GetComponent<UnityEngine.UI.Image>().color = i == SquadSize - 1 ? new Color(0.08f, 0.4f, 0.43f) : new Color(0.04f, 0.14f, 0.2f, 0.9f);
        }
        private void RefreshProfile()
        {
            if (coins != null) coins.text =  Profile.Coins.ToString("N0");
            if (points != null) points.text =  Profile.Points.ToString("N0");
            if (equipped != null) equipped.text = Profile.Equipped < 0 ? "POINTPASS CAPTAIN  /  DEFAULT" : "LOBBY BADGE  /  " + LobbyProfile.ItemNames[Profile.Equipped];
            for (int i = 0; i < 3; i++)
            {
                if (shopButtons[i] != null) shopButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text = Profile.Owns(i)
                    ? Profile.Equipped == i ? "EQUIPPED" : "EQUIP LOBBY BADGE" : (i == 1 ? "P " : "G ") + LobbyProfile.Prices[i].ToString("N0") + "   /   UNLOCK";
                if (inventoryButtons[i] != null) inventoryButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text = Profile.Owns(i)
                    ? Profile.Equipped == i ? "EQUIPPED" : "EQUIP BADGE" : "VISIT SHOP";
                if (inventoryStates[i] != null) inventoryStates[i].text = Profile.Owns(i) ? "OWNED  /  LOCAL COLLECTION" : "AVAILABLE IN THE ITEM SHOP";
            }
            Color badgeColor = Profile.Equipped == 1 ? new Color(0.73f, 0.94f, 1f)
                : Profile.Equipped == 2 ? new Color(0.43f, 0.64f, 0.96f) : Aqua;
            if (wordmark != null) wordmark.color = MatchPalette.Paper;
            if (captainBadge != null)
            {
                captainBadge.text = Profile.Equipped < 0 ? "BLUE TEAM · CAPTAIN" : LobbyProfile.ItemNames[Profile.Equipped];
                captainBadge.color = badgeColor;
            }
            if (pods[2] != null) pods[2].Accent = badgeColor;
        }
        public void ShowPage(Page page)
        {
            if (!pages.ContainsKey(page)) return;
            CloseModal(); CurrentPage = page;
            foreach (var pair in pages) pair.Value.SetActive(pair.Key == page);
            foreach (var pair in tabs)
            {
                var image = pair.Value.GetComponent<UnityEngine.UI.Image>();
                image.color = pair.Key == page ? new Color(0.08f, 0.29f, 0.36f, 0.95f) : new Color(0.025f, 0.075f, 0.13f, 0.3f);
                pair.Value.GetComponentInChildren<UnityEngine.UI.Text>().color = pair.Key == page ? Cyan : MatchPalette.Paper;
            }
            if (backToLobby != null) backToLobby.gameObject.SetActive(page != Page.MainLobby);
            nativeArt.SetHomeVisible(page == Page.MainLobby);
            nativeArt.enabled = page == Page.PlayerHub;
            RefreshClubhouseHeader(page == Page.MainLobby);
            host.RefreshLayout();
            Select(page == Page.MainLobby ? homePlay : backToLobby);
        }
        public bool TryBack()
        {
            if (ModalOpen) { CloseModal(); return true; }
            if (CurrentPage != Page.MainLobby) { ShowPage(Page.MainLobby); return true; }
            return false;
        }
        public void CloseModal()
        {
            if (modal != null) modal.SetActive(false);
            guestName = null;
        }
        private static void Select(UnityEngine.UI.Button button)
        {
            if (EventSystem.current != null && button != null && button.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
        private void OnDestroy() { if (Profile != null) Profile.Changed -= RefreshProfile; }
    }
}
