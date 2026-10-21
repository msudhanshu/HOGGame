using System.Collections.Generic;
using Game.Core;
using Nixin.CrossPromo;
using Nixin.Game.Core;
using Nixin.Game.Core.StoreEngagement;

namespace Game.Unity
{
    /// <summary>Remote Config keys for HOGGame. Create the same keys in the Firebase console.</summary>
    public static class HuntRemoteConfig
    {
        /// <summary>Items to find per round ("in one go"). 0 = use each level's own wave_size.</summary>
        public const string WaveSize = "hunt_wave_size";
        /// <summary>Rounds per level. 0 = use each level's own waves.</summary>
        public const string WaveCount = "hunt_wave_count";
        /// <summary>Multiplier on every level's time limit (1.0 = unchanged).</summary>
        public const string TimeScale = "hunt_time_scale";
        /// <summary>Timer turns urgent (red) at or below this many seconds left.</summary>
        public const string UrgentSeconds = "hunt_urgent_seconds";
        /// <summary>Mosaic levels per chapter before the image-HOG level.</summary>
        public const string MosaicsPerChapter = "hunt_mosaics_per_chapter";

        public const string EcoStartCoins = "eco_start_coins";
        public const string EcoStartHints = "eco_start_hints";
        public const string EcoWinBase = "eco_win_base";
        public const string EcoWinPerToughness = "eco_win_per_toughness";
        public const string EcoReplayCoins = "eco_replay_coins";
        public const string EcoPhotoFeeBase = "eco_photo_fee_base";
        public const string EcoPhotoFeePerToughness = "eco_photo_fee_per_toughness";
        public const string EcoChapterChest = "eco_chapter_chest";
        public const string EcoHintCoinCost = "eco_hint_coin_cost";
        public const string EcoContinueBase = "eco_continue_base";
        public const string EcoContinueStep = "eco_continue_step";
        public const string EcoContinueSeconds = "eco_continue_seconds";

        public static IDictionary<string, object> Defaults()
        {
            var defaults = new Dictionary<string, object>
            {
                { WaveSize, 0 },
                { WaveCount, 0 },
                { TimeScale, 1.0 },
                { UrgentSeconds, 15 },
                { MosaicsPerChapter, HuntRoadmap.DefaultMosaicsPerChapter },
            };
            var eco = new HuntEconomyTuning();
            defaults[EcoStartCoins] = eco.StartCoins;
            defaults[EcoStartHints] = eco.StartHints;
            defaults[EcoWinBase] = eco.WinBase;
            defaults[EcoWinPerToughness] = eco.WinPerToughness;
            defaults[EcoReplayCoins] = eco.ReplayCoins;
            defaults[EcoPhotoFeeBase] = eco.PhotoFeeBase;
            defaults[EcoPhotoFeePerToughness] = eco.PhotoFeePerToughness;
            defaults[EcoChapterChest] = eco.ChapterChest;
            defaults[EcoHintCoinCost] = eco.HintCoinCost;
            defaults[EcoContinueBase] = eco.ContinueBase;
            defaults[EcoContinueStep] = eco.ContinueStep;
            defaults[EcoContinueSeconds] = (double)eco.ContinueSeconds;
            foreach (var kvp in MonetizationConfigKeys.Defaults())
                defaults[kvp.Key] = kvp.Value;
            foreach (var kvp in StoreEngagementConfigKeys.Defaults())
                defaults[kvp.Key] = kvp.Value;
            foreach (var kvp in CrossPromoConfigKeys.Defaults())
                defaults[kvp.Key] = kvp.Value;
            return defaults;
        }

        public static float UrgentThreshold => GameServices.Config.GetFloat(UrgentSeconds, 15f);

        public static int ChapterSize => GameServices.Config.GetInt(MosaicsPerChapter, HuntRoadmap.DefaultMosaicsPerChapter);

        /// <summary>Economy tuning = PRD defaults overlaid with remote config.</summary>
        public static HuntEconomyTuning Economy()
        {
            var cfg = GameServices.Config;
            var t = new HuntEconomyTuning();
            t.StartCoins = cfg.GetInt(EcoStartCoins, t.StartCoins);
            t.StartHints = cfg.GetInt(EcoStartHints, t.StartHints);
            t.WinBase = cfg.GetInt(EcoWinBase, t.WinBase);
            t.WinPerToughness = cfg.GetInt(EcoWinPerToughness, t.WinPerToughness);
            t.ReplayCoins = cfg.GetInt(EcoReplayCoins, t.ReplayCoins);
            t.PhotoFeeBase = cfg.GetInt(EcoPhotoFeeBase, t.PhotoFeeBase);
            t.PhotoFeePerToughness = cfg.GetInt(EcoPhotoFeePerToughness, t.PhotoFeePerToughness);
            t.ChapterChest = cfg.GetInt(EcoChapterChest, t.ChapterChest);
            t.HintCoinCost = cfg.GetInt(EcoHintCoinCost, t.HintCoinCost);
            t.ContinueBase = cfg.GetInt(EcoContinueBase, t.ContinueBase);
            t.ContinueStep = cfg.GetInt(EcoContinueStep, t.ContinueStep);
            t.ContinueSeconds = cfg.GetFloat(EcoContinueSeconds, t.ContinueSeconds);
            return t;
        }

        /// <summary>Applies remote overrides to every level of the catalog.</summary>
        public static HuntCatalog Apply(HuntCatalog catalog)
        {
            var cfg = GameServices.Config;
            var size = cfg.GetInt(WaveSize, 0);
            var count = cfg.GetInt(WaveCount, 0);
            var scale = cfg.GetFloat(TimeScale, 1f);
            UnityEngine.Debug.Log($"[HuntRemoteConfig] fetched={cfg.IsFetched} wave_size={size} wave_count={count} time_scale={scale}");
            if (size <= 0 && count <= 0 && (scale <= 0f || scale == 1f))
                return catalog;

            var defs = new List<HuntLevelDef>(catalog.Count);
            for (var i = 0; i < catalog.Count; i++)
                defs.Add(catalog[i].WithOverrides(size, count, scale));
            return new HuntCatalog(defs, catalog.ImageHogs);
        }
    }
}
