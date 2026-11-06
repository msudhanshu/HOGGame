using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Scrolling level path grouped by chapter: mosaic nodes (number, toughness ring,
    /// stars) and a gold Photo Hunt node closing each chapter.</summary>
    public sealed class HuntMapScreen : MonoBehaviour
    {
        const float NodeStep = 250f;
        const float HeaderStep = 190f;
        const float TopPad = 200f;
        const float BottomPad = 140f;
        /// <summary>Extra room above and below a Photo Hunt node (its title tag hangs below it).</summary>
        const float HogGap = 60f;
        const float Amplitude = 250f;

        // Era names + subtitles (placeholders - swap freely). Index = chapter, wraps around.
        static readonly string[] ChapterNames =
        {
            "The Dawn Age", "The Gilded Age", "Age of Sail", "Steam & Iron", "Neon Century", "Orbital Dawn",
            "The Far Future", "The Final Hour"
        };

        static readonly string[] EraSubtitles =
        {
            "Stone, fire & first tools", "Brass, clocks & clockwork", "Maps, storms & open seas", "Engines, rails & smoke",
            "Rain, signs & arcades", "Stations & silent stars", "Where time folds back", "Every clock, one last time"
        };

        RectTransform _content;
        RectTransform _viewport;
        Text _starTotal;
        Action<HuntNode> _onPick;
        readonly Dictionary<int, RectTransform> _nodeViews = new Dictionary<int, RectTransform>();

        public static string ChapterName(int chapter) => ChapterNames[Mathf.Abs(chapter) % ChapterNames.Length];

        public static string EraSubtitle(int chapter) => EraSubtitles[Mathf.Abs(chapter) % EraSubtitles.Length];

        const float TopBarHeight = 200f;
        const float EraHeaderHeight = 200f;

        int _chapter = -1;
        RectTransform _eraDots;
        Text _eraTop;
        Text _eraName;
        Text _eraSub;
        GameObject _prevArrow;
        GameObject _nextArrow;
        CanvasGroup _contentGroup;
        HuntEraDrawer _drawer;
        List<HuntEraInfo> _eras = new List<HuntEraInfo>();

        /// <summary>Back key: closes the era drawer first. True when it handled the key.</summary>
        public bool TryCloseDrawer()
        {
            if (_drawer == null || !_drawer.IsOpen)
                return false;
            _drawer.Close();
            return true;
        }

        public void Build(RectTransform panel, Action<HuntNode> onPick, Action onBack)
        {
            _onPick = onPick;
            var theme = HuntUiTheme.Current;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel, false);
            var scrollRt = HuntUiKit.Stretch((RectTransform)scrollGo.transform);
            scrollRt.offsetMax = new Vector2(0f, -(TopBarHeight + EraHeaderHeight));
            _viewport = HuntUiKit.Stretch(HuntUiKit.Rect("Viewport", scrollRt));
            _viewport.gameObject.AddComponent<RectMask2D>();
            _viewport.gameObject.AddComponent<Image>().color = Color.clear;
            _content = HuntUiKit.Rect("Content", _viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = _viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;

            var top = HuntUiKit.Rect("TopBar", panel);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 200f);
            top.anchoredPosition = Vector2.zero;
            HuntSafeTopInset.Ensure(top);

            var back = HuntUiKit.IconButton(top, NixinIcons.Load(NixinIcons.ChevronLeft), theme.primary, 120f, onBack);
            HuntUiKit.Place(back.Root, new Vector2(0f, 0.5f), new Vector2(120f, 120f), new Vector2(100f, 0f));
            var menu = HuntUiKit.IconButton(top, NixinIcons.Load(NixinIcons.Menu), theme.glow, 120f, OpenDrawer);
            HuntUiKit.Place(menu.Root, new Vector2(0f, 0.5f), new Vector2(120f, 120f), new Vector2(250f, 0f));
            var title = HuntUiKit.Label(top, "TIMELINE", 50, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);
            title.rectTransform.offsetMin = new Vector2(360f, 0f);
            title.rectTransform.offsetMax = new Vector2(-330f, 0f);

            var starPill = HuntUiKit.Rounded(top, "StarPill", HuntUiTheme.WithAlpha(theme.deep, 0.82f), 46f);
            HuntUiKit.NeonEdge(starPill.transform, theme.accent, 46f, 0.7f);
            HuntUiKit.Place(starPill.rectTransform, new Vector2(1f, 0.5f), new Vector2(200f, 92f), new Vector2(-120f, 0f));
            var star = HuntUiKit.Image(starPill.transform, "Star", HuntUiSprites.Star, theme.accent);
            HuntUiKit.Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 80f), new Vector2(40f, 4f));
            _starTotal = HuntUiKit.Label(starPill.transform, "0", 46, Color.white, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);
            _starTotal.rectTransform.offsetMin = new Vector2(70f, 0f);

            BuildEraHeader(panel);
            _prevArrow = EraArrow(panel, true);
            _nextArrow = EraArrow(panel, false);
            _contentGroup = HuntUiKit.Group(_content.gameObject);
            _drawer = HuntEraDrawer.Build(panel, c => ShowChapter(c));
        }

        // ---------------- era navigation ----------------

        void BuildEraHeader(RectTransform panel)
        {
            var theme = HuntUiTheme.Current;
            var box = HuntUiKit.Rect("EraHeader", panel);
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(1f, 1f);
            box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(0f, EraHeaderHeight);
            box.anchoredPosition = new Vector2(0f, -TopBarHeight);
            HuntSafeTopInset.Ensure(box);

            _eraTop = HuntUiKit.Label(box, "", 30, theme.inkSoft, TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
            Anchor(_eraTop.rectTransform, 0f, 1f, 0f, 0.74f);
            _eraName = HuntUiKit.Label(box, "", 58, theme.accent, TextAnchor.MiddleCenter, true, FontStyle.Bold, true);
            Anchor(_eraName.rectTransform, 0f, 0.74f, 0f, 0.44f);
            _eraSub = HuntUiKit.Label(box, "", 34, theme.ink, TextAnchor.MiddleCenter, false, FontStyle.Normal);
            Anchor(_eraSub.rectTransform, 0f, 0.44f, 0f, 0.2f);
            _eraDots = HuntUiKit.Rect("Dots", box);
            Anchor(_eraDots, 0f, 0.2f, 0f, 0f);
            var layout = _eraDots.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        static void Anchor(RectTransform rt, float xMin, float yMax, float xMax, float yMin)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(120f, 0f);
            rt.offsetMax = new Vector2(-120f, 0f);
        }

        GameObject EraArrow(RectTransform panel, bool left)
        {
            var theme = HuntUiTheme.Current;
            var bg = HuntUiKit.Rounded(panel, left ? "PrevEra" : "NextEra", HuntUiTheme.WithAlpha(theme.deep, 0.78f), 56f, true);
            HuntUiKit.Place(bg.rectTransform, new Vector2(left ? 0f : 1f, 0.5f), new Vector2(112f, 168f),
                new Vector2(left ? 14f : -14f, 0f), new Vector2(left ? 0f : 1f, 0.5f));
            HuntUiKit.NeonEdge(bg.transform, theme.glow, 56f, 0.9f);
            var icon = HuntUiKit.Image(bg.transform, "Chevron",
                NixinIcons.Load(left ? NixinIcons.ChevronLeft : NixinIcons.ChevronRight), theme.glow);
            icon.preserveAspect = true;
            HuntUiKit.Stretch(icon.rectTransform, 34f);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<HuntButtonJuice>();
            var step = left ? -1 : 1;
            button.onClick.AddListener(() => ShowChapter(_chapter + step));
            return bg.gameObject;
        }

        void OpenDrawer()
        {
            RefreshEras();
            _drawer.Open(_eras);
        }

        void RefreshEras()
        {
            var current = HuntSave.CurrentNode;
            _eras = HuntEras.Build(HuntRoadmapService.Nodes, HuntSave.IsCleared, HuntSave.IsUnlocked,
                current != null ? current.Chapter : 0);
        }

        /// <summary>Opening the map: land on the era holding the current node, scrolled to it.</summary>
        public void FocusCurrent()
        {
            var current = HuntSave.CurrentNode;
            _chapter = current != null ? current.Chapter : 0;
            Rebuild();
            ScrollTo(current);
        }

        /// <summary>Switches era (clamped), fading the new path in.</summary>
        public void ShowChapter(int chapter)
        {
            RefreshEras();
            if (_eras.Count == 0)
                return;
            var target = Mathf.Clamp(chapter, _eras[0].Chapter, _eras[_eras.Count - 1].Chapter);
            if (target == _chapter)
                return;
            _chapter = target;
            Rebuild();
            var current = HuntSave.CurrentNode;
            ScrollTo(current != null && current.Chapter == _chapter ? current : null);
            if (_contentGroup != null)
                HuntTweener.Fade(_contentGroup, 0f, 1f, 0.22f);
            HuntAudio.Sfx(Nixin.Audio.NixinAudio.Click);
        }

        void RefreshEraHeader()
        {
            var theme = HuntUiTheme.Current;
            RefreshEras();
            var idx = HuntEras.IndexOfChapter(_eras, _chapter);
            _eraTop.text = "ERA " + (idx + 1) + " OF " + Mathf.Max(1, _eras.Count);
            _eraName.text = ChapterName(_chapter).ToUpperInvariant();
            _eraSub.text = EraSubtitle(_chapter);
            _prevArrow.SetActive(idx > 0);
            _nextArrow.SetActive(idx < _eras.Count - 1);

            for (var i = _eraDots.childCount - 1; i >= 0; i--)
                Destroy(_eraDots.GetChild(i).gameObject);
            for (var i = 0; i < _eras.Count; i++)
            {
                var era = _eras[i];
                var cur = i == idx;
                var done = era.Unlocked && era.Count > 0 && era.Cleared >= era.Count;
                var dot = HuntUiKit.Rounded(_eraDots, "Dot" + i, cur ? theme.glow : done ? theme.success : theme.locked, 12f);
                dot.rectTransform.sizeDelta = new Vector2(cur ? 44f : 16f, 16f);
            }
        }


        public void Rebuild()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _nodeViews.Clear();

            var all = HuntRoadmapService.Nodes;
            var current = HuntSave.CurrentNode;
            if (_chapter < 0)
                _chapter = current != null ? current.Chapter : 0;
            _starTotal.text = HuntSave.State.TotalStars.ToString();

            var nodes = new List<HuntNode>();
            for (var i = 0; i < all.Count; i++)
                if (all[i].Chapter == _chapter)
                    nodes.Add(all[i]);
            if (nodes.Count == 0 && all.Count > 0)
            {
                _chapter = all[0].Chapter;
                for (var i = 0; i < all.Count; i++)
                    if (all[i].Chapter == _chapter)
                        nodes.Add(all[i]);
            }

            RefreshEraHeader();

            // Level 1 of the era sits at the bottom and the path climbs. Lay out bottom-up (heights measured from the
            // content's bottom edge), then flip into the top-anchored content once the total height is known.
            var nodeRise = new float[nodes.Count];
            var y = BottomPad;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.Kind == HuntNodeKind.ImageHog)
                    y += HogGap;
                nodeRise[i] = y + NodeStep * 0.5f;
                y += NodeStep + (node.Kind == HuntNodeKind.ImageHog ? HogGap : 0f);
            }

            var height = y + TopPad - 120f;
            _content.sizeDelta = new Vector2(0f, height);

            var dotLayer = HuntUiKit.Rect("Path", _content);
            dotLayer.anchorMin = dotLayer.anchorMax = new Vector2(0.5f, 1f);

            Vector2? prev = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var x = node.Kind == HuntNodeKind.ImageHog ? 0f : Mathf.Sin(i * 0.95f) * Amplitude;
                var pos = new Vector2(x, nodeRise[i] - height);
                if (prev.HasValue)
                    Dots(dotLayer, prev.Value, pos);
                _nodeViews[node.Index] = NodeView(node, pos, current != null && node.Index == current.Index);
                prev = pos;
            }

            dotLayer.SetAsFirstSibling();
        }

        /// <summary>Scroll so the node sits in the middle of the viewport (no node = the bottom, level 1).</summary>
        public void ScrollTo(HuntNode node)
        {
            if (_viewport == null)
                return;
            Canvas.ForceUpdateCanvases();
            var viewH = _viewport.rect.height;
            var max = Mathf.Max(0f, _content.sizeDelta.y - viewH);
            var target = max;
            if (node != null && _nodeViews.TryGetValue(node.Index, out var view))
                target = -view.anchoredPosition.y - viewH * 0.5f;
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(target, 0f, max));
        }

        void Dots(RectTransform layer, Vector2 a, Vector2 b)
        {
            var dist = Vector2.Distance(a, b);
            var n = Mathf.FloorToInt(dist / 44f);
            for (var k = 2; k < n - 1; k++)
            {
                var p = Vector2.Lerp(a, b, k / (float)n);
                var dot = HuntUiKit.Image(layer, "Dot", HuntUiSprites.Circle, HuntUiTheme.WithAlpha(HuntUiTheme.Current.glow, 0.6f));
                HuntUiKit.Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(20f, 20f), p);
            }
        }

        RectTransform NodeView(HuntNode node, Vector2 pos, bool isCurrent)
        {
            var theme = HuntUiTheme.Current;
            var hog = node.Kind == HuntNodeKind.ImageHog;
            var unlocked = HuntSave.IsUnlocked(node);
            var stars = HuntSave.StarsFor(node);
            var cleared = HuntSave.IsCleared(node);
            var size = hog ? 230f : 180f;
            var ring = hog ? theme.accent : theme.ToughnessColor(HuntRoadmapService.ToughnessOf(node));
            if (!unlocked)
                ring = theme.locked;

            var root = HuntUiKit.Rect("Node" + node.Number, _content);
            HuntUiKit.Place(root, new Vector2(0.5f, 1f), new Vector2(size, size), pos);

            var shadow = HuntUiKit.Image(root, "Shadow", HuntUiSprites.SoftCircle, HuntUiTheme.WithAlpha(ring, isCurrent ? 0.6f : 0.3f));
            HuntUiKit.Stretch(shadow.rectTransform, -26f);
            var rim = HuntUiKit.Image(root, "Rim", HuntUiSprites.Circle, HuntUiTheme.Darken(ring, 0.25f));
            HuntUiKit.Stretch(rim.rectTransform);
            rim.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            var face = HuntUiKit.Image(root, "Ring", HuntUiSprites.Circle, ring, true);
            HuntUiKit.Stretch(face.rectTransform);
            var inner = HuntUiKit.Image(root, "Inner", HuntUiSprites.Circle,
                isCurrent ? theme.glow : unlocked ? theme.panelAlt : theme.deep);
            HuntUiKit.Stretch(inner.rectTransform, size * 0.12f);

            var thumb = hog ? PhotoHuntThumbnail(node) : null;
            if (thumb != null)
                CircleThumbnail(inner, thumb, unlocked);

            if (!unlocked)
            {
                var lockIcon = HuntUiKit.Image(root, "Lock", NixinIcons.Load(NixinIcons.Lock), thumb != null ? Color.white : theme.inkSoft);
                lockIcon.preserveAspect = true;
                HuntUiKit.Stretch(lockIcon.rectTransform, size * 0.3f);
            }
            else if (hog)
            {
                if (thumb == null)
                {
                    var glyph = HuntUiKit.Image(root, "Photo", theme.imageHogIcon != null ? theme.imageHogIcon : HuntUiSprites.Star,
                        theme.imageHogIcon != null ? Color.white : theme.accent);
                    glyph.preserveAspect = true;
                    HuntUiKit.Stretch(glyph.rectTransform, size * 0.24f);
                }
            }
            else
            {
                var preview = SilhouettePreview(node);
                var hasArt = preview != null && cleared;
                if (hasArt)
                {
                    var art = HuntUiKit.Image(root, "Art", preview, HuntUiTheme.WithAlpha(theme.glow, 0.95f));
                    art.preserveAspect = true;
                    HuntUiKit.Stretch(art.rectTransform, size * 0.17f);
                }

                if (hasArt)
                {
                    // Number moves to a corner badge so the silhouette reads clearly.
                    var badge = HuntUiKit.Image(root, "Badge", HuntUiSprites.Circle, HuntUiTheme.Darken(ring, 0.25f));
                    HuntUiKit.Place(badge.rectTransform, new Vector2(0.85f, 0.85f), new Vector2(76f, 76f));
                    HuntUiKit.Label(badge.transform, node.Number.ToString(), 40, Color.white, TextAnchor.MiddleCenter, true);
                }
                else
                {
                    HuntUiKit.Label(root, node.Number.ToString(), 70, isCurrent ? theme.deep : theme.ink, TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
                }
            }

            if (hog)
            {
                var tag = HuntUiKit.Rounded(root, "Tag", theme.danger, 28f);
                HuntUiKit.Place(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(340f, 92f), new Vector2(0f, -30f));
                var kind = HuntUiKit.Label(tag.transform, "PHOTO HUNT", 24, HuntUiTheme.WithAlpha(Color.white, 0.85f));
                kind.rectTransform.anchorMin = new Vector2(0f, 0.55f);
                var name = HuntUiKit.Label(tag.transform, PhotoHuntTitle(node), 32, Color.white, TextAnchor.MiddleCenter, true);
                name.rectTransform.anchorMax = new Vector2(1f, 0.62f);
                name.rectTransform.offsetMin = new Vector2(12f, 4f);
                name.rectTransform.offsetMax = new Vector2(-12f, 0f);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 32;
                name.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (cleared && !hog)
            {
                var row = HuntUiKit.Stars(root, stars, 54f, 2f, arc: true);
                var rowRt = (RectTransform)row[0].transform.parent;
                HuntUiKit.Place(rowRt, new Vector2(0.5f, 0f), rowRt.sizeDelta, new Vector2(0f, -18f));
            }

            if (isCurrent)
            {
                root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.06f;
                var arrow = HuntUiKit.Image(root, "Arrow", NixinIcons.Load(NixinIcons.ArrowDown), theme.accent);
                arrow.preserveAspect = true;
                HuntUiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(90f, 90f), new Vector2(0f, 70f));
                arrow.gameObject.AddComponent<Outline>().effectColor = HuntUiTheme.WithAlpha(theme.deep, 0.6f);
                arrow.gameObject.AddComponent<HuntFloatBob>().amplitude = 14f;
            }

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HuntButtonJuice>();
            button.onClick.AddListener(() =>
            {
                if (!HuntSave.IsUnlocked(node))
                {
                    HuntTweener.To(root, 0.4f, k => root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 30f) * 10f * (1f - k)),
                        Ease.Linear, 0f, () => root.localRotation = Quaternion.identity);
                    HuntHaptics.Medium();
                    var prev = HuntRoadmapService.Previous(node);
                    HuntToast.Show(prev != null ? "Finish level " + prev.Number + " first" : "Locked");
                    return;
                }

                _onPick?.Invoke(node);
            });
            return root;
        }

        static RevealSetAsset RevealSetOf(HuntNode node) =>
            node.ImageHog != null && node.ImageHog.HasRevealSet ? RevealSetAsset.Load(node.ImageHog.RevealSet) : null;

        static Sprite PhotoHuntThumbnail(HuntNode node)
        {
            var set = RevealSetOf(node);
            return set != null ? set.MapThumbnail : null;
        }

        /// <summary>Theme title from the reveal set; the catalog row's title when there is none.</summary>
        static string PhotoHuntTitle(HuntNode node)
        {
            var set = RevealSetOf(node);
            if (set != null && !string.IsNullOrWhiteSpace(set.Title))
                return set.Title;
            return node.ImageHog != null ? node.ImageHog.Title : "Photo Hunt";
        }

        /// <summary>Thumbnail cropped to the inner circle (cover fit); dimmed while locked.</summary>
        static void CircleThumbnail(Image circle, Sprite sprite, bool unlocked)
        {
            circle.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var art = HuntUiKit.Image(circle.transform, "Thumb", sprite, unlocked ? Color.white : new Color(0.4f, 0.4f, 0.45f, 1f));
            var rt = art.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        }

        static readonly Dictionary<string, Sprite> Previews = new Dictionary<string, Sprite>();

        public static Sprite SilhouettePreview(HuntNode node)
        {
            var def = HuntRoadmapService.LevelFor(node);
            if (def == null)
                return null;
            var stem = Path.GetFileNameWithoutExtension(def.FileName);
            if (Previews.TryGetValue(stem, out var cached))
                return cached;
            var tex = Resources.Load<Texture2D>(HuntContentResources.TextKey(HuntCatalogLoader.SilhouetteFolder, stem + ".png"));
            var sprite = tex != null ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
            Previews[stem] = sprite;
            return sprite;
        }
    }
}
