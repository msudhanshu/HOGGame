using UnityEditor;

namespace Game.Unity.Editor
{
    /// <summary>
    /// Day/night reveal images must keep their authored pixel size: no NPOT rescale, no mipmaps,
    /// clamped, high-quality compression. (RevealLevelBuilder tolerates a resize, but a 1:1 import
    /// keeps the painted objects crisp under the scanner.)
    /// Cutout icons (&lt;set&gt;/icons/*.png from hog-object-extract) keep their alpha.
    /// </summary>
    sealed class RevealTexturePostprocessor : AssetPostprocessor
    {
        const string RevealFolder = "/Resources/HiddenMosaic/Reveal/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains(RevealFolder))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var isIcon = assetPath.Contains("/icons/");
            importer.alphaSource = isIcon ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = isIcon;
        }
    }
}
