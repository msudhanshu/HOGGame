using System;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    [CreateAssetMenu(fileName = "HuntCatalog", menuName = "Nixin Studio/Hidden Mosaic/Hunt Catalog")]
    public sealed class HuntCatalogAsset : ScriptableObject
    {
        [SerializeField] HuntSilhouetteEntry[] entries = Array.Empty<HuntSilhouetteEntry>();
        [Tooltip("One real-image HOG per chapter (after every N mosaics). Empty = taken from catalog.json image_hogs.")]
        [SerializeField] HuntImageHogEntry[] imageHogs = Array.Empty<HuntImageHogEntry>();

        public HuntSilhouetteEntry[] Entries => entries;
        public int Count => entries == null ? 0 : entries.Length;

        public HuntCatalog ToCatalog()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("Hunt catalog has no silhouettes.");
            var defs = new HuntLevelDef[entries.Length];
            for (var i = 0; i < entries.Length; i++)
                defs[i] = entries[i].ToDef();
            var hogs = new ImageHogDef[imageHogs == null ? 0 : imageHogs.Length];
            for (var i = 0; i < hogs.Length; i++)
                hogs[i] = imageHogs[i] != null ? imageHogs[i].ToDef() : null;
            return new HuntCatalog(defs, hogs);
        }

        public void ReplaceEntries(HuntSilhouetteEntry[] next)
        {
            entries = next ?? Array.Empty<HuntSilhouetteEntry>();
        }

        public void ReplaceImageHogs(HuntImageHogEntry[] next)
        {
            imageHogs = next ?? Array.Empty<HuntImageHogEntry>();
        }
    }

    [Serializable]
    public sealed class HuntImageHogEntry
    {
        [SerializeField] string id = "chapter1";
        [SerializeField] string title = "Photo Hunt";
        [Tooltip("Scene name in Build Settings. Empty = 'Coming soon' placeholder.")]
        [SerializeField] string scene = "";
        [SerializeField] float timeLimitSeconds = 60f;
        [SerializeField] int toughness = 1;
        [Tooltip("Day/night reveal scene played in the Play scene. Blank title/time fall back to the set's own.")]
        [SerializeField] RevealSetAsset revealSet;

        public RevealSetAsset RevealSet => revealSet;

        public ImageHogDef ToDef()
        {
            if (revealSet == null)
                return new ImageHogDef(id, title, scene, timeLimitSeconds, toughness);
            return new ImageHogDef(
                string.IsNullOrWhiteSpace(id) ? revealSet.Id : id,
                string.IsNullOrWhiteSpace(title) ? revealSet.Title : title,
                scene,
                timeLimitSeconds > 0f ? timeLimitSeconds : revealSet.TimeLimitSeconds,
                toughness > 0 ? toughness : revealSet.Toughness,
                revealSet.Id);
        }

        public static HuntImageHogEntry FromDef(ImageHogDef def)
        {
            return new HuntImageHogEntry
            {
                id = def.Id,
                title = def.Title,
                scene = def.Scene,
                timeLimitSeconds = def.TimeLimitSeconds,
                toughness = def.Toughness,
                revealSet = RevealSetAsset.Load(def.RevealSet)
            };
        }
    }

    [Serializable]
    public sealed class HuntSilhouetteEntry
    {
        [Tooltip("Filename only, under Resources/HiddenMosaic/SilhouetteJson")]
        [SerializeField] string silhouetteFile = "horse.json";
        [SerializeField] string title = "Horse";
        [SerializeField] int waveSize = 3;
        [SerializeField] int waveCount = 2;
        [SerializeField] float memoryFade = 6f;
        [SerializeField] float glassRadius = 0.09f;
        [SerializeField] float zoom = 3.8f;
        [SerializeField] float huntSizeScale = 1f;
        [SerializeField] bool glassFog = true;
        [SerializeField] float timeLimitSeconds = 60f;
        [SerializeField] int toughness = 1;
        [SerializeField] int sprinkleSeed;
        [Tooltip("Optional alternate atlas name(s) for theme-pack owners, same syntax as the silhouette's own atlas field. Empty = no alternate theme for this level.")]
        [SerializeField] string theme = "";
        [Tooltip("Mosaic = stamp board; Reveal = night image over day image (Resources/HiddenMosaic/Reveal/<revealSet>).")]
        [SerializeField] HuntLevelMode mode = HuntLevelMode.Mosaic;
        [Tooltip("Scanner style id (Magnifier, Microscope, Target, SciFiScanner, RectLens, Flashlight). Empty = mode default.")]
        [SerializeField] string scanner = "";
        [Tooltip("Reveal levels only: folder under Resources/HiddenMosaic/Reveal. Empty = silhouetteFile.")]
        [SerializeField] string revealSet = "";

        public string SilhouetteFile => silhouetteFile;
        public string Title => title;
        public int WaveSize => waveSize;
        public int WaveCount => waveCount;
        public int Toughness => toughness;

        public HuntLevelDef ToDef()
        {
            return new HuntLevelDef(
                silhouetteFile,
                title,
                waveSize,
                waveCount,
                memoryFade,
                glassRadius,
                zoom,
                huntSizeScale,
                glassFog,
                timeLimitSeconds,
                toughness,
                sprinkleSeed,
                theme,
                mode,
                scanner,
                revealSet);
        }

        public static HuntSilhouetteEntry FromJson(HuntCatalogLoader.LevelJson row)
        {
            var entry = new HuntSilhouetteEntry();
            if (row == null)
                return entry;
            entry.silhouetteFile = row.file;
            entry.title = string.IsNullOrWhiteSpace(row.title) ? row.file : row.title;
            entry.waveSize = row.wave_size > 0 ? row.wave_size : 3;
            entry.waveCount = row.waves > 0 ? row.waves : 2;
            entry.memoryFade = row.memory_fade > 0f ? row.memory_fade : 6f;
            entry.glassRadius = row.glass_radius > 0f ? row.glass_radius : 0.09f;
            entry.zoom = row.zoom > 0f ? row.zoom : 3.8f;
            entry.huntSizeScale = row.hunt_size_scale > 0f ? row.hunt_size_scale : 1f;
            entry.glassFog = row.glass_fog;
            entry.timeLimitSeconds = row.time_limit_seconds > 0f ? row.time_limit_seconds : 60f;
            entry.toughness = row.toughness > 0 ? row.toughness : 1;
            entry.sprinkleSeed = row.sprinkle_seed;
            entry.theme = row.theme ?? "";
            entry.mode = HuntLevelDef.ParseMode(row.mode);
            entry.scanner = row.scanner ?? "";
            entry.revealSet = row.reveal_set ?? "";
            return entry;
        }
    }
}
