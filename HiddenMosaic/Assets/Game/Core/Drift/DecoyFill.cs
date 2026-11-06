using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Picks which filler tiles become decoy icons (look-alikes that are never on the hunt list).
    /// Engine-free so static mosaic levels can reuse it; MosaicDecoyFillUnity applies it to a level.
    /// </summary>
    public static class DecoyFill
    {
        /// <param name="fillerCount">Number of filler slots.</param>
        /// <param name="iconPool">Candidate decoy icon names.</param>
        /// <param name="exclude">Names that must never appear as decoys (every hunt target).</param>
        /// <param name="percent">0-100 share of fillers to convert.</param>
        /// <returns>Per filler slot: decoy icon name, or null to keep the filler.</returns>
        public static string[] Assign(int fillerCount, IReadOnlyList<string> iconPool, ICollection<string> exclude, float percent, int seed)
        {
            var result = new string[Math.Max(0, fillerCount)];
            if (fillerCount <= 0 || iconPool == null || percent <= 0f)
                return result;

            var pool = new List<string>(iconPool.Count);
            for (var i = 0; i < iconPool.Count; i++)
            {
                var name = iconPool[i];
                if (string.IsNullOrEmpty(name) || pool.Contains(name) || (exclude != null && exclude.Contains(name)))
                    continue;
                pool.Add(name);
            }

            if (pool.Count == 0)
                return result;

            var rng = new Random(seed);
            var count = (int)Math.Round(fillerCount * Math.Min(100f, percent) / 100f);
            var slots = new int[fillerCount];
            for (var i = 0; i < fillerCount; i++)
                slots[i] = i;
            for (var i = 0; i < count; i++)
            {
                var j = i + rng.Next(fillerCount - i);
                var tmp = slots[i];
                slots[i] = slots[j];
                slots[j] = tmp;
            }

            // Walk a shuffled pool so neighbouring decoys rarely repeat.
            var order = new List<string>(pool);
            var cursor = order.Count;
            for (var i = 0; i < count; i++)
            {
                if (cursor >= order.Count)
                {
                    for (var k = order.Count - 1; k > 0; k--)
                    {
                        var j = rng.Next(k + 1);
                        var tmp = order[k];
                        order[k] = order[j];
                        order[j] = tmp;
                    }

                    cursor = 0;
                }

                result[slots[i]] = order[cursor++];
            }

            return result;
        }
    }
}
