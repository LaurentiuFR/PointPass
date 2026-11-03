using System;
using UnityEngine;

namespace PointPass.UI
{
    /// <summary>Local prototype wallet and owned cosmetics. No real transactions or online identity.</summary>
    public sealed class LobbyProfile
    {
        public const string SaveKey = "PointPass.LobbyProfile.v1";
        [Serializable] private class Data
        {
            public int coins = 10000, points = 250, owned, equipped = -1;
        }
        public static readonly string[] ItemNames = { "AQUA STRIKE", "CAPTAIN'S HALO", "MIDNIGHT CLUB" };
        public static readonly int[] Prices = { 1200, 75, 2500 };
        private Data data;
        public int Coins => data.coins;
        public int Points => data.points;
        public int Equipped => data.equipped;
        public string DisplayName => PlayerPrefs.GetString("PointPass.DisplayName", "LAURENTIU");
        public event Action Changed;
        public LobbyProfile()
        {
            try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { data = null; }
            if (data == null || data.coins < 0 || data.points < 0 || data.owned < 0 || data.owned > 7
                || data.equipped < -1 || data.equipped > 2) data = new Data();
        }
        public bool Owns(int item) => item >= 0 && item < 3 && (data.owned & (1 << item)) != 0;
        public bool Purchase(int item)
        {
            if (item < 0 || item >= Prices.Length || Owns(item)) return false;
            if (item == 1)
            {
                if (data.points < Prices[item]) return false;
                data.points -= Prices[item];
            }
            else
            {
                if (data.coins < Prices[item]) return false;
                data.coins -= Prices[item];
            }
            data.owned |= 1 << item; Save(); return true;
        }
        public void Equip(int item)
        {
            if (!Owns(item)) return;
            data.equipped = item; Save();
        }
        public void ResetDemoWallet() { data = new Data(); Save(); }
        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data)); PlayerPrefs.Save(); Changed?.Invoke();
        }
    }
}
