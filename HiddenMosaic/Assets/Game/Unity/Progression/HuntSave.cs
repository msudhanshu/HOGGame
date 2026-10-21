using System.Collections.Generic;
using Game.Core;
using Nixin.Monetization;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Cached roadmap (catalog → chapters of mosaics + image HOGs).</summary>
    public static class HuntRoadmapService
    {
        static HuntCatalog _catalog;
        static IReadOnlyList<HuntNode> _nodes;

        public static void Use(HuntCatalogAsset asset)
        {
            if (_catalog != null && asset == null)
                return;
            _catalog = HuntRemoteConfig.Apply(HuntCatalogLoader.Load(asset));
            _nodes = HuntRoadmap.Build(_catalog, HuntRemoteConfig.ChapterSize);
        }

        public static HuntCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                    Use(null);
                return _catalog;
            }
        }

        public static IReadOnlyList<HuntNode> Nodes
        {
            get
            {
                if (_nodes == null)
                    Use(null);
                return _nodes;
            }
        }

        public static HuntNode Node(int index)
        {
            var nodes = Nodes;
            if (nodes.Count == 0)
                return null;
            return nodes[Mathf.Clamp(index, 0, nodes.Count - 1)];
        }

        public static HuntNode Next(HuntNode node)
        {
            if (node == null || node.Index + 1 >= Nodes.Count)
                return null;
            return Nodes[node.Index + 1];
        }

        public static HuntNode Previous(HuntNode node)
        {
            if (node == null || node.Index <= 0)
                return null;
            return Nodes[node.Index - 1];
        }

        public static HuntLevelDef LevelFor(HuntNode node)
        {
            if (node == null || node.Kind != HuntNodeKind.Mosaic)
                return null;
            return Catalog[node.CatalogIndex];
        }

        public static int ToughnessOf(HuntNode node)
        {
            if (node == null)
                return 1;
            return node.Kind == HuntNodeKind.Mosaic ? LevelFor(node).Toughness : node.ImageHog.Toughness;
        }

        public static string TitleOf(HuntNode node)
        {
            if (node == null)
                return "";
            return node.Kind == HuntNodeKind.Mosaic ? LevelFor(node).Title : node.ImageHog.Title;
        }
    }

    /// <summary>Persistent progress (stars + unlock) in PlayerPrefs. Replaces the old
    /// hunt.progress.highest_unlocked key (migrated once).</summary>
    public static class HuntSave
    {
        const string Key = "hunt.save.v1";
        const string LegacyKey = "hunt.progress.highest_unlocked";

        static HuntProgressState _state;

        public static HuntProgressState State
        {
            get
            {
                if (_state == null)
                    Load();
                return _state;
            }
        }

        static void Load()
        {
            var data = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(data))
            {
                _state = HuntProgressState.Deserialize(data);
                return;
            }

            _state = new HuntProgressState();
            if (PlayerPrefs.HasKey(LegacyKey))
            {
                var legacy = PlayerPrefs.GetInt(LegacyKey, 0);
                _state.UnlockUpTo(HuntRoadmap.NodeIndexForCatalogIndex(HuntRoadmapService.Nodes, legacy));
            }

            Save();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, State.Serialize());
            PlayerPrefs.Save();
        }

        public static bool AllUnlocked => Monetization.IsOwned(HuntEntitlements.AllLevels);

        public static bool IsUnlocked(HuntNode node) =>
            node != null && (node.Index <= State.HighestUnlocked || AllUnlocked);

        public static int StarsFor(HuntNode node) => node == null ? 0 : State.StarsFor(node.Id);

        public static bool IsCleared(HuntNode node) => node != null && State.IsCleared(node.Id);

        /// <summary>Returns true on first clear.</summary>
        public static bool RecordWin(HuntNode node, int stars)
        {
            if (node == null)
                return false;
            var first = State.RecordWin(node.Index, node.Id, stars);
            Save();
            return first;
        }

        /// <summary>First unlocked node that is not yet cleared (the PLAY button target).</summary>
        public static HuntNode CurrentNode
        {
            get
            {
                var nodes = HuntRoadmapService.Nodes;
                if (nodes.Count == 0)
                    return null;
                var highest = Mathf.Min(State.HighestUnlocked, nodes.Count - 1);
                for (var i = 0; i <= highest; i++)
                {
                    if (!State.IsCleared(nodes[i].Id))
                        return nodes[i];
                }

                return nodes[highest];
            }
        }

        public static void ResetAll()
        {
            State.Clear();
            Save();
        }
    }
}
