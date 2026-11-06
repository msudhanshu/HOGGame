using System.Collections;
using System.Collections.Generic;
using Nixin.Mosaic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Unity.PlayTests
{
    /// <summary>Loads the experimental drift scene; hunt icons must wander and fillers must get shoved. Fails on any logged error.</summary>
    public sealed class DriftSmokeTests
    {
        static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator TargetsDriftAndShoveFillers()
        {
            SceneManager.LoadScene(HuntScenes.Drift);
            yield return Frames(30);
            Assert.IsNotNull(Object.FindAnyObjectByType<DriftHost>());

            var views = Object.FindObjectsByType<MosaicItemView>(FindObjectsSortMode.None);
            var hunts = new List<MosaicItemView>();
            var start = new Dictionary<MosaicItemView, Vector3>();
            var decoys = 0;
            foreach (var v in views)
            {
                start[v] = v.transform.position;
                if (v.Hunt)
                    hunts.Add(v);
                else if (MosaicDecoyFillUnity.IsDecoy(v))
                    decoys++;
            }

            Assert.Greater(hunts.Count, 0, "hunt icons");
            Assert.Greater(decoys, 0, "decoy icons");

            yield return new WaitForSeconds(3f);

            var moved = 0;
            foreach (var h in hunts)
            {
                // ~0.45 world units/s cruise: 3 s must carry an icon well across the body, not wobble in place.
                if (Vector3.Distance(start[h], h.transform.position) > 0.6f)
                    moved++;
            }

            var shoved = 0;
            foreach (var v in views)
            {
                if (!v.Hunt && Vector3.Distance(start[v], v.transform.position) > 0.02f)
                    shoved++;
            }

            Assert.GreaterOrEqual(moved * 2, hunts.Count, "most targets should travel, not rotate in place");
            Assert.Greater(shoved, 0, "fillers should be pushed aside");
        }
    }
}
