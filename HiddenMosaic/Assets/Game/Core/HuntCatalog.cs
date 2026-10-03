using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class HuntLevelDef
    {
        public HuntLevelDef(
            string fileName,
            string title,
            int waveSize = 3,
            int waveCount = 2,
            float memoryFade = 6f,
            float glassRadius = 0.09f,
            float zoom = 3.8f,
            float huntSizeScale = 1f,
            bool glassFog = true,
            float timeLimitSeconds = 180f,
            int toughness = 1,
            int sprinkleSeed = 0)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Level file is required.", nameof(fileName));
            FileName = fileName;
            Title = string.IsNullOrWhiteSpace(title) ? fileName : title;
            WaveSize = waveSize < 1 ? 1 : waveSize;
            WaveCount = waveCount < 1 ? 1 : waveCount;
            MemoryFade = memoryFade < 0.5f ? 0.5f : memoryFade;
            GlassRadius = glassRadius < 0.04f ? 0.04f : glassRadius;
            Zoom = zoom < 1f ? 1f : zoom;
            HuntSizeScale = huntSizeScale < 0.25f ? 0.25f : (huntSizeScale > 1.5f ? 1.5f : huntSizeScale);
            GlassFog = glassFog;
            TimeLimitSeconds = timeLimitSeconds < 30f ? 30f : (timeLimitSeconds > 600f ? 600f : timeLimitSeconds);
            Toughness = toughness < 1 ? 1 : (toughness > 5 ? 5 : toughness);
            SprinkleSeed = sprinkleSeed;
        }

        /// <summary>Copy with remote-tuned values. waveSize/waveCount &lt;= 0 and timeScale &lt;= 0 keep the level's own value.</summary>
        public HuntLevelDef WithOverrides(int waveSize, int waveCount, float timeScale)
        {
            return new HuntLevelDef(
                FileName, Title,
                waveSize > 0 ? waveSize : WaveSize,
                waveCount > 0 ? waveCount : WaveCount,
                MemoryFade, GlassRadius, Zoom, HuntSizeScale, GlassFog,
                timeScale > 0f ? TimeLimitSeconds * timeScale : TimeLimitSeconds,
                Toughness, SprinkleSeed);
        }

        public string FileName { get; }
        public string Title { get; }
        public int WaveSize { get; }
        public int WaveCount { get; }
        public float MemoryFade { get; }
        public float GlassRadius { get; }
        public float Zoom { get; }
        /// <summary>Multiplier for hunt icon footprint (e.g. 0.85 on dense silhouettes).</summary>
        public float HuntSizeScale { get; }
        /// <summary>Blur/fog the board except under the magnifier (easy / memory-style levels).</summary>
        public bool GlassFog { get; }
        public float TimeLimitSeconds { get; }
        /// <summary>1–5 difficulty band for level-select UI.</summary>
        public int Toughness { get; }
        /// <summary>Offset mixed into hunt sprinkle seed for layout variation.</summary>
        public int SprinkleSeed { get; }
        public int TargetCount => WaveSize * WaveCount;
    }

    public sealed class HuntCatalog
    {
        readonly List<HuntLevelDef> _levels;

        public HuntCatalog(IReadOnlyList<HuntLevelDef> levels)
        {
            if (levels == null || levels.Count == 0)
                throw new ArgumentException("Catalog needs at least one level.", nameof(levels));
            _levels = new List<HuntLevelDef>(levels.Count);
            for (var i = 0; i < levels.Count; i++)
            {
                if (levels[i] == null)
                    throw new ArgumentException("Catalog levels cannot be null.", nameof(levels));
                _levels.Add(levels[i]);
            }
        }

        public IReadOnlyList<HuntLevelDef> Levels => _levels;
        public int Count => _levels.Count;
        public HuntLevelDef this[int index] => _levels[index];

        public static HuntCatalog Fallback(string fileName)
        {
            return new HuntCatalog(new[] { new HuntLevelDef(fileName, "Mosaic") });
        }
    }
}
