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
            float glassRadius = 0.14f,
            float zoom = 2.2f)
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
        }

        public string FileName { get; }
        public string Title { get; }
        public int WaveSize { get; }
        public int WaveCount { get; }
        public float MemoryFade { get; }
        public float GlassRadius { get; }
        public float Zoom { get; }
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
