using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Unity.Editor
{
    public static class HuntCatalogMenu
    {
        public const string AssetPath = "Assets/Game/Unity/Data/HuntCatalog.asset";

        [MenuItem("Nixin Studio/Hidden Mosaic/Import Catalog JSON into Asset")]
        public static void ImportCatalogJsonIntoAsset()
        {
            var asset = EnsureAsset();
            var jsonPath = Path.Combine(HuntCatalogLoader.SilhouetteDirectoryEditor, "catalog.json");
            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog(
                    "Hunt Catalog",
                    "No catalog.json under Assets/Game/Resources/HiddenMosaic/SilhouetteJson.",
                    "OK");
                return;
            }

            asset.ReplaceEntries(HuntCatalogLoader.EntriesFromJson(File.ReadAllText(jsonPath)));
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log("Imported hunt catalog from " + jsonPath);
        }

        public static HuntCatalogAsset EnsureAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Unity/Data"))
                AssetDatabase.CreateFolder("Assets/Game/Unity", "Data");

            var asset = AssetDatabase.LoadAssetAtPath<HuntCatalogAsset>(AssetPath);
            var created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<HuntCatalogAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            if (asset.Count == 0)
            {
                var jsonPath = Path.Combine(HuntCatalogLoader.SilhouetteDirectoryEditor, "catalog.json");
                if (File.Exists(jsonPath))
                    asset.ReplaceEntries(HuntCatalogLoader.EntriesFromJson(File.ReadAllText(jsonPath)));
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }
    }
}
