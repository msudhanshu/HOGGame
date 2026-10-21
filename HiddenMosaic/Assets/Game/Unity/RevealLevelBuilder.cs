using System;
using System.Collections.Generic;
using Game.Core;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// Builds a day/night reveal board from Resources/HiddenMosaic/Reveal/&lt;set&gt;/{day,night,objects}:
    /// - LevelRoot/Day: the real image (goes to the glass capture layer, seen only through the scanner);
    /// - LevelRoot/&lt;object&gt;: one hunt target per authored object, laid exactly over itself in the day image, with
    ///   MosaicItemView so picking/hints/outlines work as on mosaic levels. Objects with an outline (from the
    ///   hog-object-extract tool) get a PolygonCollider2D + RevealHitShape and their cutout icon sprite; rect-only
    ///   objects keep a rect crop + BoxCollider2D;
    /// - RevealNight (sibling of LevelRoot, Default layer): the dark cover drawn by the main camera.
    /// </summary>
    public static class RevealLevelBuilder
    {
        public const string Folder = "Reveal";
        public const string NightName = "RevealNight";
        const float PixelsPerUnit = MosaicCoords.DefaultPixelsPerUnit;

        [Serializable]
        sealed class SetJson
        {
            public int width;
            public int height;
            public string background;
            public ObjectJson[] objects;
        }

        [Serializable]
        sealed class ObjectJson
        {
            public string name;
            public int x;
            public int y;
            public int w;
            public int h;
            public PathJson[] paths;
            public string icon;
            public int icon_x = -1;
            public int icon_y = -1;
            public int pad;
            public SeedJson[] seeds;
        }

        [Serializable]
        sealed class PathJson
        {
            public bool hole;
            public float[] points;
        }

        [Serializable]
        sealed class SeedJson
        {
            public int x;
            public int y;
            public bool positive = true;
        }

        public sealed class Result
        {
            public RevealSetDef Set;
            public RevealStampCatalog Stamps;
            public Transform Root;
        }

        public static string ResourceKey(string setId, string file) => $"{HuntContentResources.Root}/{Folder}/{setId}/{file}";

        public static bool TryLoadSet(string setId, out RevealSetDef set)
        {
            set = null;
            if (string.IsNullOrWhiteSpace(setId))
                return false;
            var asset = Resources.Load<TextAsset>(ResourceKey(setId, "objects"));
            if (asset == null)
                return false;
            set = Parse(setId, asset.text);
            return set != null;
        }

        public static RevealSetDef Parse(string setId, string json)
        {
            var data = JsonUtility.FromJson<SetJson>(json);
            if (data == null)
                return null;
            var objects = new List<RevealObjectDef>();
            if (data.objects != null)
            {
                foreach (var o in data.objects)
                {
                    if (o == null)
                        continue;
                    var paths = new List<RevealPathDef>();
                    if (o.paths != null)
                    {
                        foreach (var p in o.paths)
                        {
                            if (p?.points != null && p.points.Length >= 6)
                                paths.Add(new RevealPathDef(p.hole, p.points));
                        }
                    }

                    var seeds = new List<RevealSeedDef>();
                    if (o.seeds != null)
                    {
                        foreach (var sd in o.seeds)
                        {
                            if (sd != null)
                                seeds.Add(new RevealSeedDef(sd.x, sd.y, sd.positive));
                        }
                    }

                    objects.Add(new RevealObjectDef(o.name, o.x, o.y, o.w, o.h, paths, o.icon, o.pad, seeds, o.icon_x, o.icon_y));
                }
            }

            return new RevealSetDef(setId, data.width, data.height, objects, data.background);
        }

        public static string ToJson(RevealSetDef set)
        {
            var data = new SetJson
            {
                width = set.Width,
                height = set.Height,
                background = set.Background,
                objects = new ObjectJson[set.Objects.Count]
            };
            for (var i = 0; i < set.Objects.Count; i++)
            {
                var o = set.Objects[i];
                var paths = new PathJson[o.Paths.Count];
                for (var p = 0; p < paths.Length; p++)
                {
                    var src = o.Paths[p].Points;
                    var pts = new float[src.Count];
                    for (var k = 0; k < pts.Length; k++)
                        pts[k] = Mathf.Round(src[k] * 10f) / 10f;
                    paths[p] = new PathJson { hole = o.Paths[p].Hole, points = pts };
                }

                var seeds = new SeedJson[o.Seeds.Count];
                for (var k = 0; k < seeds.Length; k++)
                    seeds[k] = new SeedJson { x = o.Seeds[k].X, y = o.Seeds[k].Y, positive = o.Seeds[k].Positive };
                data.objects[i] = new ObjectJson
                {
                    name = o.Name, x = o.X, y = o.Y, w = o.Width, h = o.Height,
                    paths = paths, icon = o.Icon, icon_x = o.IconX, icon_y = o.IconY, pad = o.Pad, seeds = seeds
                };
            }

            return JsonUtility.ToJson(data, true);
        }

        /// <summary>Removes a previous board (mosaic or reveal) so the next build starts clean.</summary>
        public static void Clear(Transform parent)
        {
            var root = parent.Find(MosaicAssembler.RootName);
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            var night = parent.Find(NightName);
            if (night != null)
                UnityEngine.Object.DestroyImmediate(night.gameObject);
        }

        /// <summary>Builds from the set's RevealSetAsset when one exists, else from the raw folder files.</summary>
        public static Result Build(string setId, Transform parent, Material spriteMaterial)
        {
            var asset = RevealSetAsset.Load(setId);
            if (asset != null)
                return Build(asset, parent, spriteMaterial);

            if (!TryLoadSet(setId, out var set))
            {
                Debug.LogError($"Reveal set '{setId}' missing: expected Resources/{ResourceKey(setId, RevealSetAsset.AssetName)}.asset or {ResourceKey(setId, "objects")}.json");
                return null;
            }

            var day = Resources.Load<Texture2D>(ResourceKey(setId, "day"));
            var night = Resources.Load<Texture2D>(ResourceKey(setId, "night"));
            return BuildFrom(setId, set, day, night, parent, spriteMaterial);
        }

        public static Result Build(RevealSetAsset asset, Transform parent, Material spriteMaterial)
        {
            if (asset == null)
                return null;
            var result = BuildFrom(asset.Id, asset.ToSetDef(), asset.Day, asset.Night, parent, spriteMaterial);
            if (result != null && Camera.main != null)
                Camera.main.backgroundColor = asset.Background;
            return result;
        }

        static Result BuildFrom(string setId, RevealSetDef set, Texture2D day, Texture2D night, Transform parent, Material spriteMaterial)
        {
            if (day == null || night == null)
            {
                Debug.LogError($"Reveal set '{setId}' needs both a day and a night image.");
                return null;
            }

            var errors = RevealSetValidator.Validate(set);
            foreach (var e in errors)
                Debug.LogWarning($"[Reveal:{setId}] {e}");

            Clear(parent);

            var root = new GameObject(MosaicAssembler.RootName).transform;
            root.SetParent(parent, false);

            var daySprite = FullSprite(day, set, out var dayScale);
            MakeRenderer("Day", root, daySprite, dayScale, Vector3.zero, 0, spriteMaterial);

            var stamps = new RevealStampCatalog();
            for (var i = 0; i < set.Objects.Count; i++)
            {
                var o = set.Objects[i];
                if (o.Width <= 0 || o.Height <= 0)
                    continue;
                // Cutout icon (exact shape, transparent around it) when the tool exported one; else a rect crop.
                Sprite sprite;
                Vector2 spriteScale;
                Vector2 anchorPx;
                var icon = o.HasIconPlacement ? Resources.Load<Texture2D>(ResourceKey(setId, StripExtension(o.Icon))) : null;
                if (icon != null)
                {
                    sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                    sprite.name = o.Name;
                    spriteScale = Vector2.one;
                    anchorPx = new Vector2(o.IconX + icon.width * 0.5f, o.IconY + icon.height * 0.5f);
                }
                else
                {
                    sprite = Crop(day, set, o, out spriteScale);
                    anchorPx = new Vector2(o.X + o.Width * 0.5f, o.Y + o.Height * 0.5f);
                }

                MosaicCoords.ToWorld(anchorPx.x, anchorPx.y, set.Width, set.Height, PixelsPerUnit, out var wx, out var wy);
                var go = MakeRenderer(o.Name, root, sprite, spriteScale, new Vector3(wx, wy, 0f), 100 + i, spriteMaterial);
                var view = go.AddComponent<MosaicItemView>();
                view.ItemName = o.Name;
                view.Hunt = true;
                view.SourceIndex = i;
                // localScale may correct a non-uniform import resize; keep colliders in image units.
                var inv = new Vector2(1f / Mathf.Max(spriteScale.x, 0.0001f), 1f / Mathf.Max(spriteScale.y, 0.0001f));
                if (o.HasOutline)
                {
                    var poly = go.AddComponent<PolygonCollider2D>();
                    poly.pathCount = o.Paths.Count;
                    for (var p = 0; p < o.Paths.Count; p++)
                        poly.SetPath(p, LocalPath(o.Paths[p], anchorPx, inv));
                }
                else
                {
                    var collider = go.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(o.Width / PixelsPerUnit * inv.x, o.Height / PixelsPerUnit * inv.y);
                    collider.offset = new Vector2(
                        (o.X + o.Width * 0.5f - anchorPx.x) / PixelsPerUnit * inv.x,
                        -(o.Y + o.Height * 0.5f - anchorPx.y) / PixelsPerUnit * inv.y);
                }

                go.AddComponent<RevealHitShape>().Init(o, set.Width, set.Height, PixelsPerUnit);
                stamps.Add(o.Name, sprite);
            }

            var nightSprite = FullSprite(night, set, out var nightScale);
            var nightGo = MakeRenderer(NightName, parent, nightSprite, nightScale, Vector3.zero, -100, spriteMaterial);
            nightGo.layer = 0;

            if (HexColor.TryParse(set.Background, out var r, out var g, out var b, out var a) && Camera.main != null)
                Camera.main.backgroundColor = new Color(r, g, b, a);

            return new Result { Set = set, Stamps = stamps, Root = root };
        }

        public static void FrameCamera(Camera camera, RevealSetDef set)
        {
            if (camera == null || set == null)
                return;
            camera.orthographic = true;
            var aspect = Mathf.Max(camera.aspect, 0.01f);
            const float margin = 1.02f;
            var fitHeight = set.Height / PixelsPerUnit * 0.5f * margin;
            var fitWidth = set.Width / PixelsPerUnit / (2f * aspect) * margin;
            camera.orthographicSize = Mathf.Max(fitHeight, fitWidth);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        /// <summary>Image-pixel outline → collider-local points around the object's anchor (y flipped).</summary>
        public static Vector2[] LocalPath(RevealPathDef path, Vector2 anchorPx, Vector2 invScale)
        {
            var n = path.VertexCount;
            var pts = new Vector2[n];
            for (var k = 0; k < n; k++)
            {
                pts[k] = new Vector2(
                    (path.Points[2 * k] - anchorPx.x) / PixelsPerUnit * invScale.x,
                    -(path.Points[2 * k + 1] - anchorPx.y) / PixelsPerUnit * invScale.y);
            }

            return pts;
        }

        static string StripExtension(string relative)
        {
            var dot = relative.LastIndexOf('.');
            var slash = relative.LastIndexOf('/');
            return dot > slash ? relative.Substring(0, dot) : relative;
        }

        // The imported texture may be resized (max size / NPOT scaling); map authored pixels through
        // the actual texture size so world size stays authored-pixels / 100 either way.
        static Sprite FullSprite(Texture2D tex, RevealSetDef set, out Vector2 scale)
        {
            scale = ScaleFor(tex, set);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit * (tex.width / (float)set.Width));
            sprite.name = tex.name;
            return sprite;
        }

        static Sprite Crop(Texture2D tex, RevealSetDef set, RevealObjectDef o, out Vector2 scale)
        {
            scale = ScaleFor(tex, set);
            var sx = tex.width / (float)set.Width;
            var sy = tex.height / (float)set.Height;
            var rect = new Rect(o.X * sx, (set.Height - o.Y - o.Height) * sy, o.Width * sx, o.Height * sy);
            rect.xMin = Mathf.Clamp(rect.xMin, 0, tex.width);
            rect.yMin = Mathf.Clamp(rect.yMin, 0, tex.height);
            rect.xMax = Mathf.Clamp(rect.xMax, 0, tex.width);
            rect.yMax = Mathf.Clamp(rect.yMax, 0, tex.height);
            var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit * sx);
            sprite.name = o.Name;
            return sprite;
        }

        /// <summary>localScale correcting a non-uniform resize (ppu already uses the X ratio).</summary>
        static Vector2 ScaleFor(Texture2D tex, RevealSetDef set)
        {
            var sx = tex.width / (float)Mathf.Max(set.Width, 1);
            var sy = tex.height / (float)Mathf.Max(set.Height, 1);
            return new Vector2(1f, sy > 0f ? sx / sy : 1f);
        }

        static GameObject MakeRenderer(string name, Transform parent, Sprite sprite, Vector2 scale, Vector3 position, int order, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (material != null)
                sr.sharedMaterial = material;
            return go;
        }
    }

    /// <summary>Find-list icons for a reveal level: each object's own crop of the day image.</summary>
    public sealed class RevealStampCatalog : IMosaicStampCatalog
    {
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _names = new List<string>();

        public IReadOnlyList<string> Names => _names;

        public void Add(string name, Sprite sprite)
        {
            if (string.IsNullOrEmpty(name) || _sprites.ContainsKey(name))
                return;
            _sprites[name] = sprite;
            _names.Add(name);
        }

        public Sprite GetStamp(string name) => name != null && _sprites.TryGetValue(name, out var s) ? s : null;
    }
}
