using System.Collections.Generic;
using Game.Core;
using Nixin.Icons;
using Nixin.Mosaic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Top-of-arena glass panel: the wave's targets as their real colour icons. Found ones are
    /// ticked off, the next one is gold. A banner can temporarily cover it (radar sweep, +time, ...).</summary>
    public sealed class HuntTargetStrip : MonoBehaviour
    {
        public const float Height = 170f;

        RectTransform _row;
        Text _label;
        RectTransform _banner;
        Image _bannerBg;
        Image _bannerEdge;
        Text _bannerText;

        public static HuntTargetStrip Build(RectTransform parent)
        {
            var theme = HuntUiTheme.Current;
            var root = HuntUiKit.Rect("TargetStrip", parent);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(-40f, Height);
            root.anchoredPosition = new Vector2(0f, -136f);

            var bg = HuntUiKit.Rounded(root, "Bg", HuntUiTheme.WithAlpha(theme.deep, 0.84f), 40f);
            HuntUiKit.Stretch(bg.rectTransform);
            HuntUiKit.NeonEdge(bg.transform, theme.glow, 40f, 0.55f);

            var strip = root.gameObject.AddComponent<HuntTargetStrip>();
            strip._label = HuntUiKit.Label(root, "TARGETS", 24, theme.inkSoft, TextAnchor.MiddleLeft, false, FontStyle.Bold, true);
            strip._label.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip._label.rectTransform.anchorMax = new Vector2(1f, 1f);
            strip._label.rectTransform.pivot = new Vector2(0.5f, 1f);
            strip._label.rectTransform.sizeDelta = new Vector2(-48f, 40f);
            strip._label.rectTransform.anchoredPosition = new Vector2(0f, -10f);

            strip._row = HuntUiKit.Rect("Row", root);
            strip._row.anchorMin = new Vector2(0f, 0f);
            strip._row.anchorMax = new Vector2(1f, 1f);
            strip._row.offsetMin = new Vector2(20f, 10f);
            strip._row.offsetMax = new Vector2(-20f, -52f);
            var layout = strip._row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // banner (covers the whole panel)
            strip._banner = HuntUiKit.Rect("Banner", root);
            HuntUiKit.Stretch(strip._banner);
            strip._bannerBg = HuntUiKit.Rounded(strip._banner, "Bg", HuntUiTheme.WithAlpha(theme.violet, 0.3f), 40f);
            HuntUiKit.Stretch(strip._bannerBg.rectTransform);
            strip._bannerEdge = HuntUiKit.NeonEdge(strip._bannerBg.transform, theme.violet, 40f, 1f);
            strip._bannerText = HuntUiKit.Label(strip._banner, "", 36, Color.white, TextAnchor.MiddleCenter, false,
                FontStyle.Bold, true);
            strip._banner.gameObject.SetActive(false);
            return strip;
        }

        /// <summary>Rebuilds the chips. <paramref name="found"/> are ticked, the first of <paramref name="remaining"/> is the current target.</summary>
        public void Set(IReadOnlyList<string> found, IReadOnlyList<string> remaining, IMosaicStampCatalog stamps, string label)
        {
            for (var i = _row.childCount - 1; i >= 0; i--)
                Destroy(_row.GetChild(i).gameObject);

            var nFound = found != null ? found.Count : 0;
            var nRemain = remaining != null ? remaining.Count : 0;
            _label.text = (string.IsNullOrEmpty(label) ? "TARGETS" : label.ToUpperInvariant()) + "   "
                + nFound + "/" + (nFound + nRemain);
            var count = nFound + nRemain;
            var chip = count <= 5 ? 100f : Mathf.Max(72f, 100f - (count - 5) * 10f);
            for (var i = 0; i < nFound; i++)
                Chip(found[i], stamps, chip, true, false);
            for (var i = 0; i < nRemain; i++)
                Chip(remaining[i], stamps, chip, false, i == 0);
        }

        void Chip(string name, IMosaicStampCatalog stamps, float size, bool done, bool current)
        {
            var theme = HuntUiTheme.Current;
            var edgeColor = done ? theme.success : current ? theme.accent : theme.glow;
            var root = HuntUiKit.Rect("Chip_" + name, _row);
            root.sizeDelta = new Vector2(size, size * 0.95f);
            var bg = HuntUiKit.Rounded(root, "Bg", HuntUiTheme.WithAlpha(edgeColor, done ? 0.16f : 0.1f), 30f);
            HuntUiKit.Stretch(bg.rectTransform);
            HuntUiKit.NeonEdge(bg.transform, edgeColor, 30f, done ? 0.7f : 1f);

            var sprite = stamps != null ? stamps.GetStamp(name) : null;
            if (sprite != null)
            {
                var icon = HuntUiKit.Image(root, "Icon", sprite, done ? new Color(1f, 1f, 1f, 0.45f) : Color.white);
                icon.preserveAspect = true;
                HuntUiKit.Stretch(icon.rectTransform, size * 0.12f);
            }
            else
            {
                HuntUiKit.Label(root, name, 22, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
            }

            if (done)
            {
                var tick = HuntUiKit.Image(root, "Tick", NixinIcons.Load(NixinIcons.Check), theme.success);
                tick.preserveAspect = true;
                HuntUiKit.Place(tick.rectTransform, new Vector2(1f, 1f), new Vector2(size * 0.42f, size * 0.42f), new Vector2(-6f, -6f));
            }
            else if (current)
            {
                root.gameObject.AddComponent<HuntIdlePulse>().amount = 0.035f;
            }
        }

        public void ShowBanner(string text, Color color)
        {
            _banner.gameObject.SetActive(true);
            _bannerBg.color = HuntUiTheme.WithAlpha(color, 0.3f);
            _bannerEdge.color = color;
            _bannerText.text = text;
            _bannerText.color = Color.white;
            _row.gameObject.SetActive(false);
            _label.gameObject.SetActive(false);
        }

        public void HideBanner()
        {
            _banner.gameObject.SetActive(false);
            _row.gameObject.SetActive(true);
            _label.gameObject.SetActive(true);
        }

        /// <summary>Chips bounce in (new wave).</summary>
        public void Pop()
        {
            for (var i = 0; i < _row.childCount; i++)
            {
                var child = _row.GetChild(i);
                child.localScale = Vector3.zero;
                HuntTweener.Scale(child, Vector3.zero, Vector3.one, 0.4f, Ease.OutBack, i * 0.08f);
            }
        }
    }
}
