using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Unity.Editor
{
    /// <summary>
    /// Keeps the HiddenMosaic shaders in player builds. They are loaded with Shader.Find,
    /// which Unity strips on mobile unless the shader is in Always Included Shaders
    /// (symptom: black level with a pink square in the middle on Android).
    /// </summary>
    [InitializeOnLoad]
    public sealed class HuntShaderBuildInclude : IPreprocessBuildWithReport
    {
        const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        const string AlwaysIncludedProperty = "m_AlwaysIncludedShaders";

        static readonly string[] ShaderPaths =
        {
            "Assets/Game/Unity/Shaders/MosaicBlurBackdrop.shader",
            "Assets/Game/Unity/Shaders/MosaicSpriteReveal.shader",
            "Packages/com.nixin.pinhole/Runtime/Unity/Shaders/PinholeScanner.shader",
        };

        public int callbackOrder => 0;

        static HuntShaderBuildInclude()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EnsureIncluded();
            };
        }

        public void OnPreprocessBuild(BuildReport report) => EnsureIncluded();

        [MenuItem("Hidden Mosaic/Ensure Shaders In Build")]
        static void EnsureMenu()
        {
            Debug.Log(EnsureIncluded()
                ? "HiddenMosaic shaders added to Always Included Shaders."
                : "HiddenMosaic shaders already in Always Included Shaders.");
        }

        static bool EnsureIncluded()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            if (assets == null || assets.Length == 0)
                return false;

            var settings = new SerializedObject(assets[0]);
            var array = settings.FindProperty(AlwaysIncludedProperty);
            if (array == null || !array.isArray)
                return false;

            var changed = false;
            foreach (var path in ShaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    Debug.LogWarning("HuntShaderBuildInclude: shader not found at " + path);
                    continue;
                }

                var present = false;
                for (var i = 0; i < array.arraySize && !present; i++)
                    present = array.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (present)
                    continue;

                array.InsertArrayElementAtIndex(array.arraySize);
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
                changed = true;
            }

            if (changed)
            {
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }

            return changed;
        }
    }
}
