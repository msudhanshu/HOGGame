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

        static readonly string[] ChapterNames =
        {
            "Sunny Meadow", "Night Market", "Ocean Breeze", "Candy Town", "Starlight Bay", "Snowy Peaks", "Jungle Drums",
            "Desert Glow"
        };

        RectTransform _content;
        RectTransform _viewport;
        Text _starTotal;
        Action<HuntNode> _onPick;
        readonly Dictionary<int, RectTransform> _nodeViews = new Dictionary<int, RectTransform>();

        public static string ChapterName(int chapter) => ChapterNames[Mathf.Abs(chapter) % ChapterNames.Length];

        public void Build(RectTransform panel, Action<HuntNode> onPick, Action onBack)
        {
            _onPick = onPick;
            var theme = HuntUiTheme.Current;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel, false);
            var scrollRt = HuntUiKit.Stretch((RectTransform)scrollGo.transform);
            scrollRt.offsetMax = new Vector2(0f, -200f);
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
            var title = HuntUiKit.Label(top, "Levels", 76, Color.white, TextAnchor.MiddleCenter, true);
            title.rectTransform.offsetMin = new Vector2(220f, 0f);
            title.rectTransform.offsetMax = new Vector2(-220f, 0f);

            var starPill = HuntUiKit.Rounded(top, "StarPill", new Color(0.12f, 0.03f, 0.25f, 0.45f), 46f);
            HuntUiKit.Place(starPill.rectTransform, new Vector2(1f, 0.5f), new Vector2(200f, 92f), new Vector2(-120f, 0f));
            var star = HuntUiKit.Image(starPill.transform, "Star", HuntUiSprites.Star, theme.accent);
            HuntUiKit.Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 80f), new Vector2(40f, 4f));
            _starTotal = HuntUiKit.Label(starPill.transform, "0", 46, Color.white, TextAnchor.MiddleCenter, true);
            _starTotal.rectTransform.offsetMin = new Vector2(70f, 0f);
        }

        public void Rebuild()
        {
            if (_content == null)
                return;
            for (var i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _nodeViews.Clear();

            var nodes = HuntRoadmapService.Nodes;
            var current = HuntSave.CurrentNode;
            _starTotal.text = HuntSave.State.TotalStars.ToString();

            // Level 1 sits at the bottom and the path climbs. Lay out bottom-up (heights measured from the
            // content's bottom edge), then flip into the top-anchored content once the total height is known.
            var chapterRise = new List<KeyValuePair<int, float>>();
            var nodeRise = new float[nodes.Count];
            var y = BottomPad;
            var chapter = -1;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.Chapter != chapter)
                {
                    chapter = node.Chapter;
                    chapterRise.Add(new KeyValuePair<int, float>(chapter, y + HeaderStep * 0.5f));
                    y += HeaderStep;
                }

                if (node.Kind == HuntNodeKind.ImageHog)
                    y += HogGap;
                nodeRise[i] = y + NodeStep * 0.5f;
                y += NodeStep + (node.Kind == HuntNodeKind.ImageHog ? HogGap : 0f);
            }

            var height = y + TopPad;
            _content.sizeDelta = new Vector2(0f, height);

            var dotLayer = HuntUiKit.Rect("Path", _content);
            dotLayer.anchorMin = dotLayer.anchorMax = new Vector2(0.5f, 1f);
            for (var c = 0; c < chapterRise.Count; c++)
                Header(chapterRise[c].Key, chapterRise[c].Value - height);

            chapter = -1;
            Vector2? prev = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.Chapter != chapter)
                {
                    chapter = node.Chapter;
                    prev = null;
                }

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

        /// <summary>Chapter pill centred at <paramref name="y"/> (top-anchored, negative down).</summary>
        void Header(int chapter, float y)
        {
            var theme = HuntUiTheme.Current;
            var pill = HuntUiKit.Rounded(_content, "Chapter" + chapter, HuntUiTheme.WithAlpha(theme.ink, 0.55f), 50f);
            HuntUiKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 130f), new Vector2(0f, y));
            var label = HuntUiKit.Label(pill.transform, "Chapter " + (chapter + 1) + "  ·  <color=#FFC93C>" + ChapterName(chapter) + "</color>",
                48, Color.white, TextAnchor.MiddleCenter, true);
            label.supportRichText = true;
        }

        void Dots(RectTransform layer, Vector2 a, Vector2 b)
        {
            var dist = Vector2.Distance(a, b);
            var n = Mathf.FloorToInt(dist / 44f);
            for (var k = 2; k < n - 1; k++)
            {
                var p = Vector2.Lerp(a, b, k / (float)n);
                var dot = HuntUiKit.Image(layer, "Dot", HuntUiSprites.Circle, new Color(1f, 1f, 1f, 0.55f));
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

            var shadow = HuntUiKit.Image(root, "Shadow", HuntUiSprites.Circle, new Color(0.1f, 0f, 0.2f, 0.3f));
            HuntUiKit.Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            var rim = HuntUiKit.Image(root, "Rim", HuntUiSprites.Circle, HuntUiTheme.Darken(ring, 0.25f));
            HuntUiKit.Stretch(rim.rectTransform);
            rim.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            var face = HuntUiKit.Image(root, "Ring", HuntUiSprites.Circle, ring, true);
            HuntUiKit.Stretch(face.rectTransform);
            var inner = HuntUiKit.Image(root, "Inner", HuntUiSprites.Circle, unlocked ? Color.white : HuntUiTheme.Lighten(theme.locked, 0.4f));
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
                    var art = HuntUiKit.Image(root, "Art", preview, HuntUiTheme.WithAlpha(theme.ink, 0.7f));
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
                    HuntUiKit.Label(root, node.Number.ToString(), 70, theme.ink);
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
                var arrow = HuntUiKit.Image(root, "Arrow", NixinIcons.Load(NixinIcons.ArrowDown), Color.white);
                arrow.preserveAspect = true;
                HuntUiKit.Place(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(90f, 90f), new Vector2(0f, 70f));
                arrow.gameObject.AddComponent<Outline>().effectColor = HuntUiTheme.WithAlpha(theme.ink, 0.6f);
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

        static Sprite SilhouettePreview(HuntNode node)
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
