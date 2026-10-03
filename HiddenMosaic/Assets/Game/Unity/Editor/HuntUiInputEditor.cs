using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.Unity.Editor
{
    public static class HuntUiInputEditor
    {
        public const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        public static void WireEventSystem(GameObject eventGo)
        {
            if (eventGo == null)
                return;

            if (eventGo.GetComponent<EventSystem>() == null)
                eventGo.AddComponent<EventSystem>();

            var legacy = eventGo.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);

            var uiModule = eventGo.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
                uiModule = eventGo.AddComponent<InputSystemUIInputModule>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions != null)
                uiModule.actionsAsset = actions;
            else
            {
                uiModule.actionsAsset = null;
                uiModule.AssignDefaultActions();
            }
        }
    }
}
