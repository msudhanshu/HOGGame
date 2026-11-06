using Game.Core;

namespace Game.Unity
{
    /// <summary>Per-hint stock over the wallet (one currency per hint kind).</summary>
    public static class HuntHintStock
    {
        public static string Currency(HuntHintKind kind)
        {
            switch (kind)
            {
                case HuntHintKind.Peek: return HuntCurrencies.HintPeek;
                case HuntHintKind.Radar: return HuntCurrencies.HintRadar;
                case HuntHintKind.FreezeTime: return HuntCurrencies.HintFreeze;
                default: return HuntCurrencies.Hints;
            }
        }

        public static bool IsHintCurrency(string currency)
        {
            return currency == HuntCurrencies.Hints || currency == HuntCurrencies.HintPeek
                || currency == HuntCurrencies.HintRadar || currency == HuntCurrencies.HintFreeze;
        }

        public static int Count(HuntHintKind kind) => HuntWallet.Balance(Currency(kind));

        public static int Price(HuntHintKind kind) => HuntHints.CoinPrice(kind, HuntWallet.Tuning);

        /// <summary>Uses one from stock; false when empty (caller then offers the coin price).</summary>
        public static bool TryUseStock(HuntHintKind kind) =>
            HuntWallet.TrySpend(Currency(kind), 1, "hint_" + kind);

        /// <summary>Pays the coin price for one use.</summary>
        public static bool TryBuyUse(HuntHintKind kind) =>
            HuntWallet.TrySpend(HuntCurrencies.Coins, Price(kind), "hint_coins_" + kind);
    }
}
