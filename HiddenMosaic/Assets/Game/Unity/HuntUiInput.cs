using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Unity
{
    public static class HuntUiInput
    {
        public static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
            }

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                legacy.enabled = false;

            var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
                uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            uiModule.enabled = true;
            uiModule.actionsAsset = null;
            uiModule.AssignDefaultActions();
        }
    }
}
