using System;
using Nixin.Game.Core;
using Nixin.Monetization;

namespace Game.Unity
{
    /// <summary>Out-of-currency flows. The game owns these (rather than the generic offer
    /// presenter) so it can offer a coins option and a Shop link.</summary>
    public static class HuntJams
    {
        /// <summary>No hints left. onGranted = a hint was obtained and should be used now.
        /// onClosed = player backed out (resume play).</summary>
        public static void OutOfHints(Action onGranted, Action onClosed = null)
        {
            HuntPopups.Jam(new HuntJamPayload
            {
                Currency = HuntCurrencies.Hints,
                Title = "Out of Hints!",
                Message = "Grab a hint and keep hunting.",
                AdAvailable = Monetization.IsAvailable(HuntPlacements.HintAd),
                AdLabel = "FREE HINT",
                OnAd = () => Monetization.Request(HuntPlacements.HintAd, granted =>
                {
                    if (granted) onGranted?.Invoke();
                    else onClosed?.Invoke();
                }),
                OnShop = () => HuntPopups.Shop("jam_hints", onClosed),
                OnClose = onClosed
            });
        }

        /// <summary>Not enough coins for `needed`. onResolved = player now has more coins.</summary>
        public static void NotEnoughCoins(int needed, Action onResolved, Action onClosed = null)
        {
            var reward = FreeCoinsReward;
            var missing = Math.Max(0, needed - HuntWallet.Coins);
            HuntPopups.Jam(new HuntJamPayload
            {
                Currency = HuntCurrencies.Coins,
                Title = "Need More Coins",
                Message = missing > 0 ? "You need " + missing + " more coins." : "Top up your coins!",
                AdAvailable = Monetization.IsAvailable(HuntPlacements.FreeCoins),
                AdLabel = "+" + reward + " FREE",
                OnAd = () => Monetization.Request(HuntPlacements.FreeCoins, granted =>
                {
                    if (granted)
                    {
                        HuntAnalytics.CurrencyEarn(HuntCurrencies.Coins, reward, "free_coins_ad");
                        onResolved?.Invoke();
                    }
                    else onClosed?.Invoke();
                }),
                OnShop = () => HuntPopups.Shop("jam_coins", onClosed),
                OnClose = onClosed
            });
        }

        public static int FreeCoinsReward =>
            GameServices.Monetization != null
            && GameServices.Monetization.Catalog.TryGetPlacement(HuntPlacements.FreeCoins, out var def)
                ? def.RewardAmount
                : 25;
    }
}
