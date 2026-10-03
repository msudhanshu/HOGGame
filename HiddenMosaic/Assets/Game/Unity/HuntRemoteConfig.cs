using System.Collections.Generic;
using Game.Core;
using Nixin.Game.Core;

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

        public static IDictionary<string, object> Defaults() => new Dictionary<string, object>
        {
            { WaveSize, 0 },
            { WaveCount, 0 },
            { TimeScale, 1.0 },
            { UrgentSeconds, 15 },
        };

        public static float UrgentThreshold => GameServices.Config.GetFloat(UrgentSeconds, 15f);

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
            return new HuntCatalog(defs);
        }
    }
}
