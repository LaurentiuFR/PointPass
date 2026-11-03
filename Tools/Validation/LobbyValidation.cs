using System;
using System.Linq;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Validation
{
    public static class LobbyValidation
    {
        private static void Click(MatchMenu menu, string name)
        {
            var button = menu.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name && b.IsInteractable());
            button.onClick.Invoke();
        }
        public static void Run(Action<bool, string> check)
        {
            bool hadSave = PlayerPrefs.HasKey(LobbyProfile.SaveKey);
            string saved = PlayerPrefs.GetString(LobbyProfile.SaveKey, "");
            MatchMenu menu = null;
            try
            {
                PlayerPrefs.DeleteKey(LobbyProfile.SaveKey);
                menu = MatchMenu.Create(); menu.ShowMain();
                LobbyMenuView lobby = menu.Lobby;
                check(lobby != null && lobby.CurrentPage == LobbyMenuView.Page.MainLobby,
                    "PointPass opens its actual clubhouse Unity lobby");
                for (int size = 1; size <= 5; size++)
                {
                    lobby.SetSquadSize(size);
                    check(Enumerable.Range(0, 5).Count(lobby.IsSlotAvailable) == size && lobby.IsSlotAvailable(2),
                        size + "v" + size + " activates exactly the requested slots with YOU locked in slot 3");
                    foreach (int slot in new[] { 0, 1, 3, 4 })
                    {
                        if (!lobby.IsSlotAvailable(slot))
                            check(!lobby.SetGuest(slot, "LOCKED"), "Inactive squad slots reject invitations");
                    }
                }
                check(!lobby.SetGuest(2, "OTHER") && !lobby.SetGuest(7, "BAD"), "Preview guests cannot replace YOU or use an invalid slot");
                lobby.SetGuest(0, "FRIEND"); lobby.SetSquadSize(1); lobby.SetSquadSize(5);
                check(lobby.GuestAt(0) == null, "Reducing squad size clears guests from locked slots");
                Click(menu, "InviteFriends");
                check(lobby.ModalOpen, "Add Friend opens a clear local-preview invitation dialog");
                var field = menu.GetComponentInChildren<UnityEngine.UI.InputField>(); field.text = "  <Guest>  ";
                Click(menu, "AddPreviewGuest");
                check(!lobby.ModalOpen && lobby.GuestAt(1) == "Guest", "Preview guest entry is trimmed and strips rich-text markup");
                lobby.SetGuest(1, null); check(lobby.GuestAt(1) == null, "A joined preview guest can leave the squad");

                var coins = menu.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t => t.name == "MatchCoins");
                var points = menu.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t => t.name == "PassPoints");
                foreach (LobbyMenuView.Page page in Enum.GetValues(typeof(LobbyMenuView.Page)))
                {
                    lobby.ShowPage(page);
                    check(lobby.CurrentPage == page && coins.text.Contains("10,000") && points.text.Contains("250"),
                        page + " routes instantly while retaining the wallet values");
                }
                lobby.ShowPage(LobbyMenuView.Page.ItemShop);
                Click(menu, "Purchase0"); Click(menu, "DialogPrimary");
                check(lobby.Profile.Coins == 8800 && lobby.Profile.Owns(0) && coins.text.Contains("8,800"),
                    "Confirmed gold purchase debits the wallet and updates the global bar immediately");
                Click(menu, "Purchase1"); Click(menu, "DialogPrimary");
                check(lobby.Profile.Points == 175 && lobby.Profile.Owns(1) && points.text.Contains("175"),
                    "Pass-point purchase debits its own currency without changing gold");
                check(!lobby.Profile.Purchase(0) && lobby.Profile.Coins == 8800, "Duplicate purchases cannot double-charge the wallet");
                var restored = new LobbyProfile();
                check(restored.Coins == 8800 && restored.Points == 175 && restored.Owns(0) && restored.Owns(1),
                    "Preview currency and owned inventory restore together from one local save");
                lobby.ShowPage(LobbyMenuView.Page.PlayerHub); Click(menu, "Equip0");
                check(lobby.Profile.Equipped == 0, "Owned cosmetics can be equipped from Player Hub");
                check(menu.TryBack() && lobby.CurrentPage == LobbyMenuView.Page.MainLobby, "Cancel returns from a lobby subpage to Main Lobby");
                Click(menu, "Play"); Click(menu, "MatchFormat1");
                check(lobby.ModalOpen, "Match format reports its preview connection state through a functional dialog");
                check(menu.TryBack() && !lobby.ModalOpen, "Cancel dismisses the connection dialog without starting a match");
                int starts = 0; menu.PlayRequested += () => starts++;
                menu.ShowMain(); Click(menu, "Play"); Click(menu, "PlayOffline");
                check(starts == 1 && !lobby.ModalOpen, "Offline fallback requests one real game start");
                lobby.ShowPage(LobbyMenuView.Page.Settings);
                check(menu.GetComponentInChildren<UnityEngine.UI.Slider>() != null && menu.GetComponentInChildren<UnityEngine.UI.Toggle>() != null,
                    "Dedicated Settings screen retains functional sound controls");
                lobby.Profile.ResetDemoWallet();
                check(lobby.Profile.Coins == 10000 && lobby.Profile.Points == 250 && !lobby.Profile.Owns(0),
                    "Reset preview wallet consistently clears ownership and restores both balances");
            }
            finally
            {
                if (menu != null) UnityEngine.Object.DestroyImmediate(menu.gameObject);
                if (hadSave) PlayerPrefs.SetString(LobbyProfile.SaveKey, saved); else PlayerPrefs.DeleteKey(LobbyProfile.SaveKey);
                PlayerPrefs.Save();
            }
        }
    }
}
