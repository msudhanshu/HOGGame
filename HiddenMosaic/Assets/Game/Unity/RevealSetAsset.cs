using System;
using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// One day/night reveal scene: the art, its hidden-object data and how it plays. Lives next to its
    /// images at Resources/HiddenMosaic/Reveal/&lt;id&gt;/RevealSet.asset so it can be loaded by id, and is
    /// referenced from the catalog's image-HOG rows (one per chapter).
    ///
    /// <see cref="objects"/> may stay empty while hit-boxes are being authored: the level then opens as a
    /// scanner-only preview (no find list) and counts as cleared when the player taps Done.
    /// </summary>
    [CreateAssetMenu(fileName = "RevealSet", menuName = "Nixin Studio/Hidden Mosaic/Reveal Set")]
    public sealed class RevealSetAsset : ScriptableObject
    {
        public const string AssetName = "RevealSet";

        [Tooltip("Stable id = folder name under Resources/HiddenMosaic/Reveal. Saves and analytics key off it.")]
        [SerializeField] string id = "";
        [SerializeField] string title = "Photo Hunt";
        [TextArea(2, 5)]
        [SerializeField] string description = "";

        [Header("Art")]
        [Tooltip("The real image; hidden objects are painted into it. Revealed through the scanner.")]
        [SerializeField] Texture2D day;
        [Tooltip("The cover shown everywhere else (night / disguised world). Same size as day.")]
        [SerializeField] Texture2D night;
        [Tooltip("objects.json from the Reveal Set Editor / hog-object-extract tool. Empty = preview only.")]
        [SerializeField] TextAsset objects;
        [SerializeField] Color background = new Color(0.05f, 0.06f, 0.08f, 1f);
        [Tooltip("Shown in the level map's Photo Hunt circle (cropped to a circle). Empty = the night image.")]
        [SerializeField] Sprite thumbnail;

        [Header("Play")]
        [Tooltip("Scanner style id: Magnifier, Microscope, Target, SciFiScanner, RectLens, Flashlight.")]
        [SerializeField] string scanner = "Flashlight";
        [SerializeField] [Range(0.05f, 0.25f)] float lensRadius = 0.11f;
        [SerializeField] float timeLimitSeconds = 60f;
        [SerializeField] [Range(1, 5)] int toughness = 2;
        [SerializeField] [Range(1, 8)] int targetsPerWave = 3;
        [SerializeField] [Range(1, 6)] int waves = 2;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
        public string Title => title;
        public string Description => description;
        public Texture2D Day => day;
        public Texture2D Night => night;
        public TextAsset Objects => objects;
        public Color Background => background;
        public Sprite Thumbnail => thumbnail;

        [NonSerialized] Sprite _nightThumb;

        /// <summary><see cref="thumbnail"/>, or a sprite over the night cover when none is authored.</summary>
        public Sprite MapThumbnail
        {
            get
            {
                if (thumbnail != null)
                    return thumbnail;
                if (_nightThumb == null && night != null)
                    _nightThumb = Sprite.Create(night, new Rect(0f, 0f, night.width, night.height), new Vector2(0.5f, 0.5f));
                return _nightThumb;
            }
        }
        public string Scanner => scanner;
        public float TimeLimitSeconds => timeLimitSeconds;
        public int Toughness => toughness;

        /// <summary>True once the set has at least one authored object to find.</summary>
        public bool HasObjects => ToSetDef().Objects.Count > 0;

        public static string ResourceKey(string setId) => RevealLevelBuilder.ResourceKey(setId, AssetName);

        public static RevealSetAsset Load(string setId)
        {
            return string.IsNullOrWhiteSpace(setId) ? null : Resources.Load<RevealSetAsset>(ResourceKey(setId));
        }

        /// <summary>Hidden objects in image pixels. No objects.json → an empty set sized to the day image.</summary>
        public RevealSetDef ToSetDef()
        {
            if (objects != null && !string.IsNullOrWhiteSpace(objects.text))
            {
                var parsed = RevealLevelBuilder.Parse(Id, objects.text);
                if (parsed != null && parsed.Width > 0 && parsed.Height > 0)
                    return parsed;
            }

            var w = day != null ? day.width : 1024;
            var h = day != null ? day.height : 1024;
            return new RevealSetDef(Id, w, h, Array.Empty<RevealObjectDef>(), "#" + ColorUtility.ToHtmlStringRGB(background));
        }

        /// <summary>The playable level: always reveal mode, file/reveal-set = this id. A chapter's
        /// image-HOG row may override title / time / toughness.</summary>
        public HuntLevelDef ToLevelDef(ImageHogDef hog = null)
        {
            return new HuntLevelDef(
                Id,
                hog != null && !string.IsNullOrWhiteSpace(hog.Title) ? hog.Title : title,
                waveSize: targetsPerWave,
                waveCount: waves,
                glassRadius: lensRadius,
                zoom: 1f,
                glassFog: false,
                timeLimitSeconds: hog != null ? hog.TimeLimitSeconds : timeLimitSeconds,
                toughness: hog != null ? hog.Toughness : toughness,
                mode: HuntLevelMode.Reveal,
                scanner: scanner,
                revealSet: Id);
        }

        /// <summary>Editor/tool helper: fill in a freshly created asset.</summary>
        public void Configure(string setId, string setTitle, string setDescription, Texture2D dayTex, Texture2D nightTex,
            TextAsset objectsJson, string scannerId, int setToughness, float timeLimit, Color bg)
        {
            id = setId;
            title = setTitle;
            description = setDescription;
            day = dayTex;
            night = nightTex;
            objects = objectsJson;
            scanner = scannerId;
            toughness = Mathf.Clamp(setToughness, 1, 5);
            timeLimitSeconds = timeLimit;
            background = bg;
        }
    }
}
