using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Coin-priced cosmetic backgrounds: the main coin sink. Recolours the Home gradient
    /// and the mosaic play-field. "default" is free and means "use the theme's own colours".</summary>
    public static class HuntBackgrounds
    {
        public sealed class Def
        {
            public string Id;
            public string Name;
            public int Price;
            public Color Top;
            public Color Bottom;
            public Color Field => Color.Lerp(Top, Bottom, 0.5f);
        }

        public const string DefaultId = "default";
        const string OwnedKey = "hunt.bg.owned";
        const string SelectedKey = "hunt.bg.selected";

        public static readonly IReadOnlyList<Def> All = new[]
        {
            Make("ocean", "Ocean Breeze", 250, "#2C8FD6", "#0B3C75"),
            Make("forest", "Mossy Forest", 300, "#4DB56A", "#12502D"),
            Make("sunset", "Sunset Glow", 350, "#FF8A5B", "#8E2A6B"),
            Make("midnight", "Midnight Sky", 450, "#3A2D7A", "#0A0720"),
            Make("candy", "Cotton Candy", 500, "#FFB3DE", "#8F7BFF")
        };

        public static event Action Changed;

        static Def Make(string id, string name, int price, string top, string bottom) => new Def
        {
            Id = id, Name = name, Price = price, Top = HuntUiTheme.Hex(top), Bottom = HuntUiTheme.Hex(bottom)
        };

        /// <summary>Equipped background, or null for the theme default.</summary>
        public static Def Selected => Find(PlayerPrefs.GetString(SelectedKey, DefaultId));

        public static Def Find(string id)
        {
            for (var i = 0; i < All.Count; i++)
                if (All[i].Id == id)
                    return All[i];
            return null;
        }

        public static bool IsOwned(Def def)
        {
            if (def == null)
                return true;
            foreach (var id in PlayerPrefs.GetString(OwnedKey, "").Split(','))
                if (id == def.Id)
                    return true;
            return false;
        }

        public static bool IsSelected(Def def) =>
            (def == null ? DefaultId : def.Id) == PlayerPrefs.GetString(SelectedKey, DefaultId);

        public static void Select(Def def)
        {
            PlayerPrefs.SetString(SelectedKey, def == null ? DefaultId : def.Id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Spends the coins, owns and equips it. False = not enough coins.</summary>
        public static bool TryBuy(Def def)
        {
            if (def == null || IsOwned(def))
                return true;
            if (!HuntWallet.TrySpend(HuntCurrencies.Coins, def.Price, "background_" + def.Id))
                return false;
            var owned = PlayerPrefs.GetString(OwnedKey, "");
            PlayerPrefs.SetString(OwnedKey, owned.Length == 0 ? def.Id : owned + "," + def.Id);
            Select(def);
            return true;
        }

        /// <summary>Wipes cosmetics (used by progress reset).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(OwnedKey);
            PlayerPrefs.DeleteKey(SelectedKey);
        }
    }
}
