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
    }
}
