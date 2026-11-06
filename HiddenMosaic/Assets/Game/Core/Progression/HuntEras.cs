using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>One era (= roadmap chapter) as the map header and the era drawer show it.</summary>
    public sealed class HuntEraInfo
    {
        public int Chapter;
        /// <summary>Nodes in the era (mosaics + its photo hunt).</summary>
        public int Count;
        public int Cleared;
        /// <summary>First node of the era is reachable.</summary>
        public bool Unlocked;
        public bool Current;
    }

    public static class HuntEras
    {
        /// <summary>Summarises each chapter in order. Chapters are the distinct <see cref="HuntNode.Chapter"/> values.</summary>
        public static List<HuntEraInfo> Build(IReadOnlyList<HuntNode> nodes, Func<HuntNode, bool> isCleared,
            Func<HuntNode, bool> isUnlocked, int currentChapter)
        {
            var result = new List<HuntEraInfo>();
            if (nodes == null)
                return result;

            HuntEraInfo era = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (era == null || era.Chapter != node.Chapter)
                {
                    era = new HuntEraInfo
                    {
                        Chapter = node.Chapter,
                        Unlocked = isUnlocked == null || isUnlocked(node),
                        Current = node.Chapter == currentChapter
                    };
                    result.Add(era);
                }

                era.Count++;
                if (isCleared != null && isCleared(node))
                    era.Cleared++;
            }

            return result;
        }

        /// <summary>Index of the era to open on: the one holding the current node, else the first.</summary>
        public static int IndexOfChapter(IReadOnlyList<HuntEraInfo> eras, int chapter)
        {
            for (var i = 0; i < eras.Count; i++)
                if (eras[i].Chapter == chapter)
                    return i;
            return 0;
        }
    }
}
