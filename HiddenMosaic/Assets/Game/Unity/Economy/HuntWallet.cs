using System;
using Game.Core;
using Nixin.Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Coins + Hints over GameServices.Wallet, with analytics on every earn/spend.</summary>
    public static class HuntWallet
    {
        const string StarterKey = "hunt.eco.starter";
        const string HintKitKey = "hunt.eco.hintkit";

        public static int Coins => GameServices.Wallet.Get(HuntCurrencies.Coins);
        public static int Hints => GameServices.Wallet.Get(HuntCurrencies.Hints);

        public static int Balance(string currency) => GameServices.Wallet.Get(currency);

        public static HuntEconomyTuning Tuning => HuntRemoteConfig.Economy();

        /// <summary>Raised with (currency, newBalance) - forwards IWallet.OnBalanceChanged.</summary>
        public static event Action<string, int> Changed
        {
            add => GameServices.Wallet.OnBalanceChanged += value;
            remove => GameServices.Wallet.OnBalanceChanged -= value;
        }

        /// <summary>First launch: grants the starting coins and hints once.</summary>
        public static void EnsureStarterGrant()
        {
            if (PlayerPrefs.GetInt(StarterKey, 0) != 1)
            {
                var t = Tuning;
                Earn(HuntCurrencies.Coins, t.StartCoins, "starter");
                Earn(HuntCurrencies.Hints, t.StartHints, "starter");
                PlayerPrefs.SetInt(StarterKey, 1);
                PlayerPrefs.Save();
            }

            EnsureHintKitGrant();
        }

        /// <summary>Once per install (new players and upgraders alike): a taste of Peek / Radar / +time.
        /// Lock-on already lives in the original "hints" balance.</summary>
        static void EnsureHintKitGrant()
        {
            if (PlayerPrefs.GetInt(HintKitKey, 0) == 1)
                return;
            var n = Tuning.StarterOtherHints;
            Earn(HuntCurrencies.HintPeek, n, "starter");
            Earn(HuntCurrencies.HintRadar, n, "starter");
            Earn(HuntCurrencies.HintFreeze, n, "starter");
            PlayerPrefs.SetInt(HintKitKey, 1);
            PlayerPrefs.Save();
        }

        public static void Earn(string currency, int amount, string source)
        {
            if (amount <= 0)
                return;
            GameServices.Wallet.Add(currency, amount);
            HuntAnalytics.CurrencyEarn(currency, amount, source);
        }

        public static bool TrySpend(string currency, int amount, string sink)
        {
            if (amount <= 0)
                return true;
            if (!GameServices.Wallet.TrySpend(currency, amount))
                return false;
            HuntAnalytics.CurrencySpend(currency, amount, sink);
            return true;
        }

        public static bool CanAfford(string currency, int amount) => GameServices.Wallet.Get(currency) >= amount;
    }
}
