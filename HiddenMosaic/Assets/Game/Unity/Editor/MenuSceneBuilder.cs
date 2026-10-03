using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Unity.Editor
{
    public static class MenuSceneBuilder
    {
        [MenuItem("Nixin Studio/Hidden Mosaic/Rebuild Home Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var eventGo = new GameObject("EventSystem");
            HuntUiInputEditor.WireEventSystem(eventGo);

            var homeGo = new GameObject("HuntHome", typeof(RectTransform));
            homeGo.transform.localScale = Vector3.one;
            var home = homeGo.AddComponent<HuntHomeScreen>();
            var catalog = HuntCatalogMenu.EnsureAsset();

            var so = new SerializedObject(home);
            so.FindProperty("catalogAsset").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, HuntBuildScenes.HomeScenePath);
            HuntBuildScenes.RegisterMenuAndPlayScenes();
            EditorSceneManager.OpenScene(HuntBuildScenes.HomeScenePath);
            Debug.Log("Hidden Mosaic home scene built at " + HuntBuildScenes.HomeScenePath);
        }
    }
}
