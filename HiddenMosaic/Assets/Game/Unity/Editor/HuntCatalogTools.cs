using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Unity.Editor
{
    /// <summary>Content pipeline helpers for catalog.json: stable sort by toughness and validation.
    /// Both re-import the JSON into HuntCatalog.asset so the runtime sees the same order.</summary>
    public static class HuntCatalogTools
    {
        static string JsonPath => Path.Combine(HuntCatalogLoader.SilhouetteDirectoryEditor, "catalog.json");

        [MenuItem("Nixin Studio/Hidden Mosaic/Sort Catalog by Toughness")]
        public static void SortByToughness()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError("[HuntCatalogTools] catalog.json not found at " + JsonPath);
                return;
            }

            var json = File.ReadAllText(JsonPath);
            var data = JsonUtility.FromJson<HuntCatalogLoader.CatalogJson>(json);
            if (data?.levels == null)
            {
                Debug.LogError("[HuntCatalogTools] catalog.json has no levels[].");
                return;
            }

            // OrderBy is stable: hand order inside a toughness band is preserved.
            data.levels = data.levels.Where(l => l != null).OrderBy(l => Mathf.Clamp(l.toughness, 1, 5)).ToArray();
            File.WriteAllText(JsonPath, JsonUtility.ToJson(data, true));
            AssetDatabase.Refresh();
            HuntCatalogMenu.ImportCatalogJsonIntoAsset();
            Debug.Log("[HuntCatalogTools] Sorted " + data.levels.Length + " levels by toughness.");
            Validate();
        }

        [MenuItem("Nixin Studio/Hidden Mosaic/Validate Catalog")]
        public static void Validate()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError("[HuntCatalogTools] catalog.json not found at " + JsonPath);
                return;
            }

            var json = File.ReadAllText(JsonPath);
            var data = JsonUtility.FromJson<HuntCatalogLoader.CatalogJson>(json);
            var errors = 0;
            var warnings = 0;
            var dir = HuntCatalogLoader.SilhouetteDirectoryEditor;
            var defs = new List<HuntLevelDef>();
            var lastToughness = 0;
            for (var i = 0; data?.levels != null && i < data.levels.Length; i++)
            {
                var row = data.levels[i];
                if (row == null || string.IsNullOrWhiteSpace(row.file))
                {
                    Debug.LogError("[HuntCatalogTools] levels[" + i + "] has no file.");
                    errors++;
                    continue;
                }

                if (!File.Exists(Path.Combine(dir, row.file)))
                {
                    Debug.LogError("[HuntCatalogTools] levels[" + i + "] missing " + row.file);
                    errors++;
                }

                var png = Path.GetFileNameWithoutExtension(row.file) + ".png";
                if (!File.Exists(Path.Combine(dir, png)))
                {
                    Debug.LogWarning("[HuntCatalogTools] levels[" + i + "] has no preview " + png);
                    warnings++;
                }

                if (row.toughness < 1 || row.toughness > 5)
                {
                    Debug.LogWarning("[HuntCatalogTools] levels[" + i + "] toughness " + row.toughness + " outside 1..5");
                    warnings++;
                }

                if (row.toughness < lastToughness)
                {
                    Debug.LogWarning("[HuntCatalogTools] levels[" + i + "] (" + row.file + ") is easier than the level before it.");
                    warnings++;
                }

                lastToughness = row.toughness;
                defs.Add(HuntSilhouetteEntry.FromJson(row).ToDef());
            }

            var hogs = HuntCatalogLoader.ImageHogsFromJson(json);
            var ids = new HashSet<string>();
            foreach (var hog in hogs)
            {
                if (!ids.Add(hog.Id))
                {
                    Debug.LogError("[HuntCatalogTools] duplicate image_hogs id " + hog.Id);
                    errors++;
                }
            }

            if (defs.Count == 0)
            {
                Debug.LogError("[HuntCatalogTools] no valid levels.");
                return;
            }

            var nodes = HuntRoadmap.Build(new HuntCatalog(defs, hogs));
            var sb = new StringBuilder("[HuntCatalogTools] Roadmap (" + nodes.Count + " nodes):\n");
            foreach (var n in nodes)
            {
                sb.Append("  ch").Append(n.Chapter + 1).Append("  #").Append(n.Number).Append("  ");
                sb.Append(n.Kind == HuntNodeKind.Mosaic
                    ? defs[n.CatalogIndex].Title + "  (toughness " + defs[n.CatalogIndex].Toughness + ")"
                    : "PHOTO HUNT " + n.ImageHog.Id + (n.IsPlaceholder ? "  [placeholder]" : "  scene=" + n.ImageHog.Scene));
                sb.Append('\n');
            }

            Debug.Log(sb.ToString());
            Debug.Log("[HuntCatalogTools] Validate: " + errors + " error(s), " + warnings + " warning(s).");
        }
    }
}
