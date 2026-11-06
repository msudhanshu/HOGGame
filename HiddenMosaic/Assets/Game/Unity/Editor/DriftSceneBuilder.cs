using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Unity.Editor
{
    /// <summary>Generates the experimental drift-hunt scene (moving targets, lock-on picks) and its level set.</summary>
    public static class DriftSceneBuilder
    {
        const string LevelSetPath = "Assets/Game/Unity/Data/DriftLevels.asset";

        [MenuItem("Nixin Studio/Hidden Mosaic/Rebuild Drift Scene (lab)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.97f, 0.65f, 0.76f);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<AudioListener>();

            var eventGo = new GameObject("EventSystem");
            HuntUiInputEditor.WireEventSystem(eventGo);

            var hostGo = new GameObject("DriftHost");
            var host = hostGo.AddComponent<DriftHost>();
            var glass = hostGo.AddComponent<MosaicGlass>();

            var hudGo = new GameObject("HuntHUD");
            var hud = hudGo.AddComponent<HuntHud>();
            hud.Build();

            var so = new SerializedObject(host);
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("glass").objectReferenceValue = glass;
            so.FindProperty("catalogAsset").objectReferenceValue = HuntCatalogMenu.EnsureAsset();
            so.FindProperty("levelSet").objectReferenceValue = EnsureLevelSet();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, HuntBuildScenes.DriftScenePath);
            HuntBuildScenes.EnsureSceneInBuild(HuntBuildScenes.DriftScenePath);
            EditorSceneManager.OpenScene(HuntBuildScenes.DriftScenePath);
            Debug.Log("Hidden Mosaic drift scene built at " + HuntBuildScenes.DriftScenePath);
        }

        public static void BuildAndQuit()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static DriftLevelSet EnsureLevelSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<DriftLevelSet>(LevelSetPath);
            if (set != null)
                return set;
            set = ScriptableObject.CreateInstance<DriftLevelSet>();
            AssetDatabase.CreateAsset(set, LevelSetPath);
            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
