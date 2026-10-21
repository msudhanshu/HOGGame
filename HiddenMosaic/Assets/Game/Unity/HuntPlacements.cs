namespace Game.Unity
{
    /// <summary>Placement/product/entitlement ids for HiddenMosaic's monetization catalog
    /// (MonetizationCatalog.asset). Game code calls Monetization.Request(HuntPlacements.X, ...)
    /// and never hardcodes a string literal at the call site.</summary>
    public static class HuntPlacements
    {
        /// <summary>SpendOrOffer: spends 1 hint if the wallet can afford it, otherwise offers a
        /// rewarded ad (refills hints) or a hint pack purchase. Kept for back-compat; the game's
        /// own jam flow uses HintAd.</summary>
        public const string Hint = "hint";

        /// <summary>RewardedAd: +1 hint that is used immediately (reward 1, cost 1).</summary>
        public const string HintAd = "hint_ad";

        /// <summary>RewardedAd: +1 hint banked in the wallet (shop "Free Hint").</summary>
        public const string FreeHint = "free_hint";

        /// <summary>RewardedAd: +30s after time-out. Once per level (enforced by PlayHost).</summary>
        public const string ContinueAd = "continue_ad";

        /// <summary>RewardedAd: doubles the coins of the win just earned.</summary>
        public const string DoubleCoins = "double_coins";

        /// <summary>RewardedAd: +coins from the shop / coin jam (catalog reward, cooldown + cap).</summary>
        public const string FreeCoins = "free_coins";

        /// <summary>Interstitial, frequency-capped via MonetizationConfigKeys.InterstitialLevelInterval.</summary>
        public const string InterstitialLevelComplete = "interstitial_level_complete";

        /// <summary>Banner anchored to the bottom of the home screen. Suppressed when remove_ads is owned.</summary>
        public const string HomeBanner = "home_banner";

        /// <summary>RewardedAd with a bundle-purchase fallback (legacy level-grid unlock).</summary>
        public const string UnlockLevel = "unlock_level";

        /// <summary>Direct Purchase placements, shown as buttons in the shop.</summary>
        public const string RemoveAdsPurchase = "remove_ads_purchase";
        public const string ThemePackPurchase = "theme_pack_purchase";
        public const string AllLevelsPurchase = "buy_all_levels";
        public const string CoinsSmallPurchase = "buy_coins_small";
        public const string CoinsLargePurchase = "buy_coins_large";
        public const string HintsSmallPurchase = "buy_hints_small";
        public const string HintsLargePurchase = "buy_hints_large";
    }

    /// <summary>Product ids as authored in MonetizationCatalog.asset / the store listing.</summary>
    public static class HuntProducts
    {
        public const string HintsSmall = "hints_small";
        public const string HintsLarge = "hints_large";
        public const string CoinsSmall = "coins_small";
        public const string CoinsLarge = "coins_large";
        public const string RemoveAds = "remove_ads";
        public const string ThemePack = "theme_pack";
        public const string BundleAllLevels = "bundle_all_levels";
    }

    /// <summary>Entitlement keys. "remove_ads" itself is Nixin.Game.Core.EntitlementKeys.RemoveAds
    /// - the director already special-cases that one to suppress Interstitial/Banner placements.</summary>
    public static class HuntEntitlements
    {
        public const string AllLevels = "all_levels";
        public const string ThemePack = "theme_pack";
    }

    /// <summary>Wallet currency ids.</summary>
    public static class HuntCurrencies
    {
        public const string Hints = "hints";
        public const string Coins = "coins";
    }
}
