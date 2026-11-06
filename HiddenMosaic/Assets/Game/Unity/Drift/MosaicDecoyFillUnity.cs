using System.Collections.Generic;
using Game.Core;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Turns a share of a level's filler tiles into decoy atlas icons, sized to the slot they replace.</summary>
    public static class MosaicDecoyFillUnity
    {
        public static MosaicLevel Apply(MosaicLevel level, IMosaicStampCatalog catalog, float percent, float tintKeep,
            float sizeScale, int seed)
        {
            if (level == null || catalog == null || percent <= 0f)
                return level;

            var exclude = new HashSet<string>();
            var fillerSlots = new List<int>();
            for (var i = 0; i < level.Items.Count; i++)
            {
                if (level.Items[i].Hunt)
                    exclude.Add(level.Items[i].Name);
                else
                    fillerSlots.Add(i);
            }

            var pool = new List<string>();
            var names = catalog.Names;
            for (var i = 0; i < names.Count; i++)
            {
                if (!MosaicStampNames.IsKnown(names[i]) && catalog.GetStamp(names[i]) != null)
                    pool.Add(names[i]);
            }

            var picks = DecoyFill.Assign(fillerSlots.Count, pool, exclude, percent, seed);
            var items = new MosaicItem[level.Items.Count];
            for (var i = 0; i < items.Length; i++)
                items[i] = level.Items[i];

            for (var s = 0; s < fillerSlots.Count; s++)
            {
                var name = picks[s];
                if (name == null)
                    continue;
                var slot = fillerSlots[s];
                var old = items[slot];
                var scale = old.Scale * WorldRadius(catalog, old.Name) / WorldRadius(catalog, name) * sizeScale;
                var tint = Lerp(MosaicTint.White, old.Tint, tintKeep);
                items[slot] = new MosaicItem(name, old.X, old.Y, scale, old.Rotation, old.ZLayer, tint, old.Region, false);
            }

            return level.WithItems(items);
        }

        /// <summary>True for a filler that was swapped to an atlas icon.</summary>
        public static bool IsDecoy(MosaicItemView view) => view != null && !view.Hunt && !MosaicStampNames.IsKnown(view.ItemName);

        static float WorldRadius(IMosaicStampCatalog catalog, string name)
        {
            var sprite = catalog.GetStamp(name);
            if (sprite == null)
                return MosaicHuntSprinkler.PrimitiveBaseRadius / MosaicCoords.DefaultPixelsPerUnit;
            var e = sprite.bounds.extents;
            return Mathf.Max(0.0001f, Mathf.Max(e.x, e.y));
        }

        static MosaicTint Lerp(MosaicTint a, MosaicTint b, float t)
        {
            return new MosaicTint(
                Mathf.Lerp(a.R, b.R, t),
                Mathf.Lerp(a.G, b.G, t),
                Mathf.Lerp(a.B, b.B, t),
                Mathf.Lerp(a.A, b.A, t));
        }
    }
}
