using System.Collections.Generic;
using Nixin.Game.Core.Firebase;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// HOGGame-specific Firebase bootstrap. Only supplies Remote Config defaults;
    /// all wiring lives in FirebaseBootstrapBase (shared package).
    /// Created automatically before the first scene loads, so no scene wiring is needed.
    /// </summary>
    public class FirebaseBootstrap : FirebaseBootstrapBase
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (FindFirstObjectByType<FirebaseBootstrapBase>() != null)
                return;
            new GameObject("FirebaseBootstrap").AddComponent<FirebaseBootstrap>();
        }

        protected override IDictionary<string, object> GetRemoteConfigDefaults()
            => HuntRemoteConfig.Defaults();
    }
}
