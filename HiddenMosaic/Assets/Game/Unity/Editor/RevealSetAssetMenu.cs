using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Unity.Editor
{
    /// <summary>
    /// Keeps RevealSet.asset files in sync with the folders under Resources/HiddenMosaic/Reveal:
    /// creates one for any folder that has day.png + night.png but no asset yet, and links an
    /// objects.json that appeared later (Reveal Set Editor / hog-object-extract tool).
    /// </summary>
    public static class RevealSetAssetMenu
    {
        const string RevealRoot = "Assets/Game/Resources/HiddenMosaic/Reveal";

        [MenuItem("Nixin Studio/Hidden Mosaic/Sync Reveal Set Assets")]
        public static void SyncAll()
        {
            if (!Directory.Exists(RevealRoot))
                return;
            int created = 0, linked = 0;
            foreach (var dir in Directory.GetDirectories(RevealRoot))
            {
                var id = Path.GetFileName(dir);
                var result = Sync(id);
                if (result == 1) created++;
                if (result == 2) linked++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RevealSets] created {created}, linked objects.json on {linked}.");
        }

        /// <summary>0 = unchanged, 1 = created, 2 = objects.json linked.</summary>
        public static int Sync(string id)
        {
            var folder = RevealRoot + "/" + id;
            var day = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/day.png");
            var night = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/night.png");
            var objects = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/objects.json");
            var path = folder + "/" + RevealSetAsset.AssetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<RevealSetAsset>(path);

            if (asset == null)
            {
                if (day == null || night == null)
                    return 0;
                asset = ScriptableObject.CreateInstance<RevealSetAsset>();
                asset.Configure(id, ObjectNames.NicifyVariableName(id), "", day, night, objects, "Flashlight", 2, 60f,
                    new Color(0.05f, 0.06f, 0.08f, 1f));
                AssetDatabase.CreateAsset(asset, path);
                return 1;
            }

            if (objects == null || asset.Objects != null)
                return 0;
            var so = new SerializedObject(asset);
            so.FindProperty("objects").objectReferenceValue = objects;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return 2;
        }
    }
}
