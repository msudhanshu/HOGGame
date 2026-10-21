using Nixin.Game.Core.Firebase;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// Global game configuration loaded from Resources. Extend with additional toggles as needed.
    /// </summary>
    [CreateAssetMenu(fileName = "HuntGameSettings", menuName = "Nixin Studio/Hidden Mosaic/Game Settings")]
    public sealed class HuntGameSettings : ScriptableObject
    {
        public const string ResourcePath = "HuntGameSettings";

        [Header("Firebase")]
        [Tooltip("When on, analytics events log to Console only — no Firebase / GA upload. "
                 + "Turn off for intentional device or Editor QA.")]
        public bool suppressFirebaseAnalytics = true;

        static HuntGameSettings _loaded;

        public static HuntGameSettings Loaded
        {
            get
            {
                if (_loaded == null)
                    _loaded = Resources.Load<HuntGameSettings>(ResourcePath);
                return _loaded;
            }
        }

        public static bool SuppressFirebaseAnalytics =>
            FirebaseAnalyticsSuppressGate.ShouldSuppress(
                Loaded != null ? Loaded.suppressFirebaseAnalytics : (bool?)null);
    }
}
