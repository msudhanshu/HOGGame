using System.IO;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Runtime paths under Assets/Game/Resources/HiddenMosaic (mobile-safe via Resources.Load).</summary>
    public static class HuntContentResources
    {
        public const string Root = "HiddenMosaic";

        public static bool TryLoadText(string folder, string fileName, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            var key = TextKey(folder, fileName);
            var asset = Resources.Load<TextAsset>(key);
            if (asset == null)
                return false;
            text = asset.text;
            return true;
        }

        public static string TextKey(string folder, string fileName)
        {
            var baseName = Path.GetFileName(fileName.Trim());
            var stem = Path.GetFileNameWithoutExtension(baseName);
            return $"{Root}/{folder}/{stem}";
        }

        /// <summary>Maps level atlas filename (e.g. pets.atlas.json) to a Resources key.</summary>
        public static string AtlasJsonKey(string atlasFileName)
        {
            var name = Path.GetFileName(atlasFileName.Trim());
            if (name.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 5);
            return $"{Root}/Atlases/{name}";
        }

        public static string AtlasTextureKey(string imageFileName)
        {
            var stem = Path.GetFileNameWithoutExtension(Path.GetFileName(imageFileName.Trim()));
            return $"{Root}/Atlases/{stem}";
        }
    }
}
