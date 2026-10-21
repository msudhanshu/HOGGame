using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Mosaic = grey stamp board under a (usually magnifying) lens.
    /// Reveal = darker "night" image over the real "day" image; the scanner reveals the day art.</summary>
    public enum HuntLevelMode
    {
        Mosaic,
        Reveal
    }

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
            int sprinkleSeed = 0,
            string theme = null,
            HuntLevelMode mode = HuntLevelMode.Mosaic,
            string scanner = null,
            string revealSet = null)
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
            Theme = theme ?? string.Empty;
            Mode = mode;
            Scanner = scanner ?? string.Empty;
            RevealSet = string.IsNullOrWhiteSpace(revealSet) ? (mode == HuntLevelMode.Reveal ? fileName : string.Empty) : revealSet;
        }

        public static HuntLevelMode ParseMode(string value)
        {
            return string.Equals(value, "reveal", StringComparison.OrdinalIgnoreCase)
                ? HuntLevelMode.Reveal
                : HuntLevelMode.Mosaic;
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
                Toughness, SprinkleSeed, Theme, Mode, Scanner, RevealSet);
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
        /// <summary>Optional alternate atlas name(s), comma-separated same as MosaicLevel.Atlas.
        /// Empty = always use the silhouette's own atlas. Non-empty is only honoured when the
        /// player owns the theme-pack entitlement - see PlayHost.LoadCatalog.</summary>
        public string Theme { get; }
        public HuntLevelMode Mode { get; }
        /// <summary>Scanner style id (see NixinScanners). Empty = mode default.</summary>
        public string Scanner { get; }
        /// <summary>Reveal levels: folder under Resources/HiddenMosaic/Reveal. Defaults to FileName.</summary>
        public string RevealSet { get; }
        public int TargetCount => WaveSize * WaveCount;
    }

    public sealed class HuntCatalog
    {
        readonly List<HuntLevelDef> _levels;
        readonly List<ImageHogDef> _imageHogs = new List<ImageHogDef>();

        public HuntCatalog(IReadOnlyList<HuntLevelDef> levels, IReadOnlyList<ImageHogDef> imageHogs = null)
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

            if (imageHogs == null)
                return;
            for (var i = 0; i < imageHogs.Count; i++)
            {
                if (imageHogs[i] != null)
                    _imageHogs.Add(imageHogs[i]);
            }
        }

        public IReadOnlyList<HuntLevelDef> Levels => _levels;
        /// <summary>Real-image HOG levels, one per chapter in order (see HuntRoadmap).</summary>
        public IReadOnlyList<ImageHogDef> ImageHogs => _imageHogs;
        public int Count => _levels.Count;
        public HuntLevelDef this[int index] => _levels[index];

        public static HuntCatalog Fallback(string fileName)
        {
            return new HuntCatalog(new[] { new HuntLevelDef(fileName, "Mosaic") });
        }
    }
}
