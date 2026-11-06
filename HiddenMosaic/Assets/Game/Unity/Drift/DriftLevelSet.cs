using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Experimental drift-hunt levels: moving targets, lock-on picks. Kept apart from the campaign catalog.</summary>
    [CreateAssetMenu(menuName = "Nixin Studio/Hidden Mosaic/Drift Level Set", fileName = "DriftLevels")]
    public sealed class DriftLevelSet : ScriptableObject
    {
        public List<DriftLevel> levels = new List<DriftLevel> { new DriftLevel() };

        public DriftLevel Get(int index)
        {
            if (levels == null || levels.Count == 0)
                return new DriftLevel();
            return levels[((index % levels.Count) + levels.Count) % levels.Count];
        }
    }

    [Serializable]
    public sealed class DriftLevel
    {
        [Tooltip("Silhouette JSON in Resources/HiddenMosaic/SilhouetteJson (no extension).")]
        public string file = "horse";
        public string title = "Drifting Horse";
        [Min(1)] public int waveSize = 3;
        [Min(1)] public int waveCount = 2;
        public float timeLimitSeconds = 150f;
        [Range(0.25f, 1.5f)] public float huntSizeScale = 1f;

        [Header("Look (same meaning as the campaign level fields)")]
        [Tooltip("Multiplier on every board icon size.")]
        [Range(0.5f, 4f)] public float iconScale = 1f;
        [Tooltip("0 = grey board outside the lens, 1 = full colour while blurred.")]
        [Range(0f, 1f)] public float baseSaturation;

        [Header("Decoys")]
        [Tooltip("Share of filler tiles turned into look-alike icons that are never on the list.")]
        [Range(0f, 100f)] public float decoyPercent = 35f;
        [Tooltip("0 = decoys keep their own icon colours, 1 = fully tinted with the silhouette colour.")]
        [Range(0f, 1f)] public float decoyTintKeep = 0.3f;
        [Range(0.6f, 1.6f)] public float decoySize = 1.1f;

        [Header("Lens (otherwise taken from the campaign level with the same file)")]
        public bool overrideGlass;
        public float glassRadius = 0.09f;
        public float zoom = 3.8f;
        public bool glassFog = true;
        public float memoryFade = 6f;

        [Header("Lock-on")]
        public DriftLockTuning lockOn = new DriftLockTuning();

        [Header("Motion")]
        public DriftSwarmTuning motion = new DriftSwarmTuning();
    }
}
