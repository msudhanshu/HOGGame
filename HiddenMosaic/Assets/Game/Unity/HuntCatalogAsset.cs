using System;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    [CreateAssetMenu(fileName = "HuntCatalog", menuName = "Nixin Studio/Hidden Mosaic/Hunt Catalog")]
    public sealed class HuntCatalogAsset : ScriptableObject
    {
        [SerializeField] HuntSilhouetteEntry[] entries = Array.Empty<HuntSilhouetteEntry>();

        public HuntSilhouetteEntry[] Entries => entries;
        public int Count => entries == null ? 0 : entries.Length;

        public HuntCatalog ToCatalog()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("Hunt catalog has no silhouettes.");
            var defs = new HuntLevelDef[entries.Length];
            for (var i = 0; i < entries.Length; i++)
                defs[i] = entries[i].ToDef();
            return new HuntCatalog(defs);
        }

        public void ReplaceEntries(HuntSilhouetteEntry[] next)
        {
            entries = next ?? Array.Empty<HuntSilhouetteEntry>();
        }
    }

    [Serializable]
    public sealed class HuntSilhouetteEntry
    {
        [Tooltip("Filename only, under StreamingAssets/SilhouetteJson")]
        [SerializeField] string silhouetteFile = "horse.json";
        [SerializeField] string title = "Horse";
        [SerializeField] int waveSize = 3;
        [SerializeField] int waveCount = 2;
        [SerializeField] float memoryFade = 6f;
        [SerializeField] float glassRadius = 0.14f;
        [SerializeField] float zoom = 2.2f;

        public string SilhouetteFile => silhouetteFile;
        public string Title => title;

        public HuntLevelDef ToDef()
        {
            return new HuntLevelDef(silhouetteFile, title, waveSize, waveCount, memoryFade, glassRadius, zoom);
        }

        public static HuntSilhouetteEntry FromJson(HuntCatalogLoader.LevelJson row)
        {
            var entry = new HuntSilhouetteEntry();
            if (row == null)
                return entry;
            entry.silhouetteFile = row.file;
            entry.title = string.IsNullOrWhiteSpace(row.title) ? row.file : row.title;
            entry.waveSize = row.wave_size > 0 ? row.wave_size : 3;
            entry.waveCount = row.waves > 0 ? row.waves : 2;
            entry.memoryFade = row.memory_fade > 0f ? row.memory_fade : 6f;
            entry.glassRadius = row.glass_radius > 0f ? row.glass_radius : 0.14f;
            entry.zoom = row.zoom > 0f ? row.zoom : 2.2f;
            return entry;
        }
    }
}
