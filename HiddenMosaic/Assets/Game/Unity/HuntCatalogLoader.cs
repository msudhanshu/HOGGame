using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    public static class HuntCatalogLoader
    {
        public const string SilhouetteFolder = "SilhouetteJson";

        [Serializable]
        public sealed class CatalogJson
        {
            public LevelJson[] levels;
        }

        [Serializable]
        public sealed class LevelJson
        {
            public string file;
            public string title;
            public int wave_size;
            public int waves;
            public float memory_fade;
            public float glass_radius;
            public float zoom;
        }

        public static string SilhouetteDirectory =>
            Path.Combine(Application.streamingAssetsPath, SilhouetteFolder);

        public static string SilhouettePath(string fileName) =>
            Path.Combine(SilhouetteDirectory, fileName);

        public static HuntCatalog Load(HuntCatalogAsset asset)
        {
            if (asset != null && asset.Count > 0)
                return asset.ToCatalog();
            return LoadJson(SilhouetteDirectory);
        }

        public static HuntCatalog LoadJson(string silhouetteDirectory)
        {
            var path = Path.Combine(silhouetteDirectory, "catalog.json");
            if (!File.Exists(path))
                return HuntCatalog.Fallback("horse.json");

            var data = JsonUtility.FromJson<CatalogJson>(File.ReadAllText(path));
            if (data == null || data.levels == null || data.levels.Length == 0)
                return HuntCatalog.Fallback("horse.json");

            var defs = new List<HuntLevelDef>(data.levels.Length);
            for (var i = 0; i < data.levels.Length; i++)
            {
                var row = data.levels[i];
                if (row == null || string.IsNullOrWhiteSpace(row.file))
                    continue;
                defs.Add(HuntSilhouetteEntry.FromJson(row).ToDef());
            }

            if (defs.Count == 0)
                return HuntCatalog.Fallback("horse.json");
            return new HuntCatalog(defs);
        }

        public static HuntSilhouetteEntry[] EntriesFromJson(string json)
        {
            var data = JsonUtility.FromJson<CatalogJson>(json);
            if (data == null || data.levels == null || data.levels.Length == 0)
                return Array.Empty<HuntSilhouetteEntry>();
            var list = new List<HuntSilhouetteEntry>(data.levels.Length);
            for (var i = 0; i < data.levels.Length; i++)
            {
                if (data.levels[i] == null || string.IsNullOrWhiteSpace(data.levels[i].file))
                    continue;
                list.Add(HuntSilhouetteEntry.FromJson(data.levels[i]));
            }

            return list.ToArray();
        }
    }
}
