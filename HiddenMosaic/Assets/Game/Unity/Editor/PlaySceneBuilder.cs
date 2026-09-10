using System.Collections.Generic;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Unity.Editor
{
    public static class PlaySceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Play.unity";

        [MenuItem("Nixin Studio/Hidden Mosaic/Rebuild Play Scene")]
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
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<InputSystemUIInputModule>();

            var hostGo = new GameObject("PlayHost");
            var host = hostGo.AddComponent<PlayHost>();
            var glass = hostGo.AddComponent<MosaicGlass>();

            var hudGo = new GameObject("HuntHUD");
            var hud = hudGo.AddComponent<HuntHud>();
            hud.Build();

            var catalog = HuntCatalogMenu.EnsureAsset();

            var so = new SerializedObject(host);
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("glass").objectReferenceValue = glass;
            so.FindProperty("catalogAsset").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AppendSceneToBuildSettings(ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Hidden Mosaic play scene built at " + ScenePath);
        }

        public static void BuildAndQuit()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static void AppendSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
