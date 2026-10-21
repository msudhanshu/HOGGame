using System.Collections.Generic;
using Nixin.Game.Core;

namespace Game.Unity
{
    /// <summary>HOGGame analytics events, built on the shared AnalyticsEvents/AnalyticsParams names.</summary>
    public static class HuntAnalytics
    {
        public const string ItemFound = "item_found";
        public const string LevelExitEvent = "level_exit";

        public const string Mode = "mode";
        public const string Attempt = "attempt";
        public const string WrongPicks = "wrong_picks";
        public const string ItemsFound = "items_found";
        public const string ItemsTotal = "items_total";
        public const string TimeLimit = "time_limit_seconds";

        public static void LevelStart(string levelId, int index, string mode, int attempt, float timeLimit, int itemsTotal)
            => GameServices.Analytics.Track(AnalyticsEvents.LevelStart, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.LevelIndex, index },
                { Mode, mode }, { Attempt, attempt }, { TimeLimit, timeLimit }, { ItemsTotal, itemsTotal },
            });

        public static void LevelComplete(string levelId, int index, int attempt, float duration, int wrongPicks)
            => GameServices.Analytics.Track(AnalyticsEvents.LevelComplete, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.LevelIndex, index },
                { Attempt, attempt }, { AnalyticsParams.Duration, duration }, { WrongPicks, wrongPicks },
            });

        /// <summary>reason: "time_out".</summary>
        public static void LevelFail(string levelId, int index, int attempt, string reason, float duration, int found, int total, int wrongPicks)
            => GameServices.Analytics.Track(AnalyticsEvents.LevelFail, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.LevelIndex, index },
                { Attempt, attempt }, { AnalyticsParams.Reason, reason }, { AnalyticsParams.Duration, duration },
                { ItemsFound, found }, { ItemsTotal, total }, { WrongPicks, wrongPicks },
            });

        public static void LevelRestart(string levelId, int index, int attempt)
            => GameServices.Analytics.Track(AnalyticsEvents.LevelRestart, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.LevelIndex, index }, { Attempt, attempt },
            });

        /// <summary>Player left mid-level (home/back/menu) before finishing or timing out.</summary>
        public static void LevelExit(string levelId, int index, float duration, int found, int total)
            => GameServices.Analytics.Track(LevelExitEvent, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.LevelIndex, index },
                { AnalyticsParams.Duration, duration }, { ItemsFound, found }, { ItemsTotal, total },
            });

        public static void Found(string levelId, string itemName, float elapsed)
            => GameServices.Analytics.Track(ItemFound, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { AnalyticsParams.ItemName, itemName }, { AnalyticsParams.Duration, elapsed },
            });

        public const string CurrencyEarnEvent = "currency_earn";
        public const string CurrencySpendEvent = "currency_spend";
        public const string PopupViewEvent = "popup_view";
        public const string ShopViewEvent = "shop_view";
        public const string ContinueUsedEvent = "continue_used";
        public const string StarsAwardedEvent = "stars_awarded";
        public const string ImageHogStartEvent = "image_hog_start";
        public const string ImageHogCompleteEvent = "image_hog_complete";

        public static void CurrencyEarn(string currency, int amount, string source)
            => GameServices.Analytics.Track(CurrencyEarnEvent, new Dictionary<string, object>
            {
                { "currency", currency }, { "amount", amount }, { "source", source ?? "" },
            });

        public static void CurrencySpend(string currency, int amount, string sink)
            => GameServices.Analytics.Track(CurrencySpendEvent, new Dictionary<string, object>
            {
                { "currency", currency }, { "amount", amount }, { "sink", sink ?? "" },
            });

        public static void PopupView(string popup)
            => GameServices.Analytics.Track(PopupViewEvent, new Dictionary<string, object> { { "popup", popup } });

        public static void ShopView(string from)
            => GameServices.Analytics.Track(ShopViewEvent, new Dictionary<string, object> { { "from", from ?? "" } });

        /// <summary>method: "coins" | "ad".</summary>
        public static void ContinueUsed(string levelId, string method, int continueIndex)
            => GameServices.Analytics.Track(ContinueUsedEvent, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { "method", method }, { "continue_index", continueIndex },
            });

        public static void StarsAwarded(string levelId, int stars, bool firstClear)
            => GameServices.Analytics.Track(StarsAwardedEvent, new Dictionary<string, object>
            {
                { AnalyticsParams.LevelId, levelId }, { "stars", stars }, { "first_clear", firstClear },
            });

        public static void ImageHogStart(string id, bool placeholder)
            => GameServices.Analytics.Track(ImageHogStartEvent, new Dictionary<string, object>
            {
                { "id", id }, { "placeholder", placeholder },
            });

        public static void ImageHogComplete(string id, bool success)
            => GameServices.Analytics.Track(ImageHogCompleteEvent, new Dictionary<string, object>
            {
                { "id", id }, { "success", success },
            });
    }
}
