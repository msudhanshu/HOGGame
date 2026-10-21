using System.Collections.Generic;

namespace Game.Core
{
    public enum HuntNodeKind
    {
        Mosaic,
        ImageHog
    }

    /// <summary>One stop on the level map.</summary>
    public sealed class HuntNode
    {
        public HuntNode(HuntNodeKind kind, int index, int chapter, int number, string id, int catalogIndex, ImageHogDef imageHog)
        {
            Kind = kind;
            Index = index;
            Chapter = chapter;
            Number = number;
            Id = id;
            CatalogIndex = catalogIndex;
            ImageHog = imageHog;
        }

        public HuntNodeKind Kind { get; }
        /// <summary>Position in the roadmap (0-based); unlock progress is stored against this.</summary>
        public int Index { get; }
        /// <summary>0-based chapter.</summary>
        public int Chapter { get; }
        /// <summary>1-based label shown to players ("Level 7"). Image HOGs share the running count.</summary>
        public int Number { get; }
        /// <summary>Stable save id; survives catalog re-sorting.</summary>
        public string Id { get; }
        /// <summary>Index into HuntCatalog.Levels for mosaic nodes, -1 otherwise.</summary>
        public int CatalogIndex { get; }
        /// <summary>Image-HOG definition; a placeholder def when none is authored yet.</summary>
        public ImageHogDef ImageHog { get; }
        public bool IsPlaceholder => Kind == HuntNodeKind.ImageHog && string.IsNullOrEmpty(ImageHog.Scene) && !ImageHog.HasRevealSet;
    }

    public static class HuntRoadmap
    {
        public const int DefaultMosaicsPerChapter = 5;

        /// <summary>Every full chapter = perChapter mosaics then one image HOG. A trailing partial
        /// chapter has no image HOG. Missing image-HOG rows become placeholders.</summary>
        public static IReadOnlyList<HuntNode> Build(HuntCatalog catalog, int mosaicsPerChapter = DefaultMosaicsPerChapter)
        {
            var nodes = new List<HuntNode>();
            if (catalog == null)
                return nodes;
            if (mosaicsPerChapter < 1)
                mosaicsPerChapter = DefaultMosaicsPerChapter;

            var hogs = catalog.ImageHogs;
            var seen = new Dictionary<string, int>();
            var chapter = 0;
            var inChapter = 0;
            for (var i = 0; i < catalog.Count; i++)
            {
                var file = catalog[i].FileName;
                seen.TryGetValue(file, out var occurrence);
                seen[file] = occurrence + 1;
                nodes.Add(new HuntNode(HuntNodeKind.Mosaic, nodes.Count, chapter, nodes.Count + 1,
                    "m:" + file + "#" + occurrence, i, null));

                inChapter++;
                if (inChapter < mosaicsPerChapter)
                    continue;

                var def = hogs != null && chapter < hogs.Count && hogs[chapter] != null
                    ? hogs[chapter]
                    : new ImageHogDef("chapter" + (chapter + 1), "Photo Hunt");
                nodes.Add(new HuntNode(HuntNodeKind.ImageHog, nodes.Count, chapter, nodes.Count + 1,
                    "h:" + def.Id, -1, def));
                chapter++;
                inChapter = 0;
            }

            return nodes;
        }

        /// <summary>Roadmap index of the n-th mosaic level (legacy save migration).</summary>
        public static int NodeIndexForCatalogIndex(IReadOnlyList<HuntNode> nodes, int catalogIndex)
        {
            if (nodes == null)
                return 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Kind == HuntNodeKind.Mosaic && nodes[i].CatalogIndex == catalogIndex)
                    return i;
            }

            return nodes.Count > 0 ? nodes.Count - 1 : 0;
        }

        public static int ChapterCount(IReadOnlyList<HuntNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return 0;
            return nodes[nodes.Count - 1].Chapter + 1;
        }
    }
}
