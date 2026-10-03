using System.Collections.Generic;
using UnityEditor;

namespace Game.Unity.Editor
{
    public static class HuntBuildScenes
    {
        public const string HomeScenePath = "Assets/Scenes/Home.unity";
        public const string PlayScenePath = "Assets/Scenes/Play.unity";

        public static void RegisterMenuAndPlayScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(HomeScenePath, true),
                new EditorBuildSettingsScene(PlayScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void EnsureSceneInBuild(string scenePath)
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
