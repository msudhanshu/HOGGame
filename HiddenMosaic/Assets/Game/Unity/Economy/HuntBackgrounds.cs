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
            Make("ocean", "Deep Ocean", 250, "#1E6BFF", "#061A4A"),
            Make("forest", "Bio Dome", 300, "#19B98A", "#05301F"),
            Make("sunset", "Solar Flare", 350, "#FF8A4C", "#4A1456"),
            Make("midnight", "Void", 450, "#2A2470", "#05041A"),
            Make("candy", "Aurora", 500, "#4FE3FF", "#7A4DFF")
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
