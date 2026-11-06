using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Bottom dock of the four hint buttons: glyph, name, owned-count badge, or a coin price chip
    /// when the stock is empty. Pure view - the host decides what a press does.</summary>
    public sealed class HuntHintDock : MonoBehaviour
    {
        sealed class Slot
        {
            public HuntHintKind Kind;
            public RectTransform Root;
            public Button Button;
            public CanvasGroup Group;
            public Image Edge;
            public Image Glyph;
            public Text Name;
            public RectTransform CountBadge;
            public Text Count;
            public RectTransform PriceChip;
            public Text Price;
            public Color Color;
        }

        readonly Slot[] _slots = new Slot[4];
        public event Action<HuntHintKind> Pressed;

        public static Color ColorOf(HuntHintKind kind)
        {
            var t = HuntUiTheme.Current;
            switch (kind)
            {
                case HuntHintKind.Peek: return t.info;
                case HuntHintKind.Radar: return t.violet;
                case HuntHintKind.FreezeTime: return t.success;
                default: return t.accent;
            }
        }

        public static Sprite GlyphOf(HuntHintKind kind)
        {
            switch (kind)
            {
                case HuntHintKind.Peek: return HuntUiSprites.GlyphEye;
                case HuntHintKind.Radar: return HuntUiSprites.GlyphRadar;
                case HuntHintKind.FreezeTime: return HuntUiSprites.GlyphHourglass;
                default: return HuntUiSprites.GlyphCrosshair;
            }
        }

        public static HuntHintDock Build(RectTransform parent)
        {
            var root = HuntUiKit.Rect("HintDock", parent);
            HuntUiKit.Stretch(root);
            root.offsetMin = new Vector2(20f, 18f);
            root.offsetMax = new Vector2(-20f, -18f);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var dock = root.gameObject.AddComponent<HuntHintDock>();
            for (var i = 0; i < HuntHints.All.Length; i++)
                dock._slots[i] = dock.BuildSlot(root, HuntHints.All[i]);
            dock.Refresh();
            return dock;
        }

        Slot BuildSlot(RectTransform parent, HuntHintKind kind)
        {
            var theme = HuntUiTheme.Current;
            var color = ColorOf(kind);
            var slot = new Slot { Kind = kind, Color = color };

            var root = HuntUiKit.Rect(kind + "Hint", parent);
            slot.Root = root;
            slot.Group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = HuntUiKit.Rounded(root, "Bg", HuntUiTheme.WithAlpha(theme.deep, 0.88f), 44f, true);
            HuntUiKit.Stretch(bg.rectTransform);
            slot.Edge = HuntUiKit.NeonEdge(bg.transform, color, 44f, 0.85f);
            var glow = HuntUiKit.Image(root, "Glow", HuntUiSprites.Radial, HuntUiTheme.WithAlpha(color, 0.22f));
            HuntUiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(220f, 220f));

            slot.Glyph = HuntUiKit.Icon(root, GlyphOf(kind), color, new Vector2(92f, 92f));
            slot.Glyph.rectTransform.anchorMin = slot.Glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.64f);
            slot.Glyph.rectTransform.anchoredPosition = Vector2.zero;

            slot.Name = HuntUiKit.Label(root, HuntHints.Name(kind, HuntWallet.Tuning), 28, Color.white,
                TextAnchor.MiddleCenter, false, FontStyle.Bold, true);
            slot.Name.rectTransform.anchorMin = new Vector2(0f, 0f);
            slot.Name.rectTransform.anchorMax = new Vector2(1f, 0.30f);
            slot.Name.rectTransform.offsetMin = new Vector2(4f, 6f);
            slot.Name.rectTransform.offsetMax = new Vector2(-4f, 0f);

            // owned count (top-right)
            var badge = HuntUiKit.Image(root, "Count", HuntUiSprites.Circle, color);
            HuntUiKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-6f, -6f));
            slot.CountBadge = badge.rectTransform;
            slot.Count = HuntUiKit.Label(badge.transform, "0", 32, theme.deep, TextAnchor.MiddleCenter, false,
                FontStyle.Bold, true);

            // empty -> coin price chip (top)
            var chip = HuntUiKit.Rounded(root, "Price", HuntUiTheme.WithAlpha(theme.accent, 1f), 28f);
            HuntUiKit.Place(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, 52f), new Vector2(0f, 14f));
            slot.PriceChip = chip.rectTransform;
            var coin = HuntUiKit.CoinIcon(chip.transform, 40f);
            HuntUiKit.Place(coin, new Vector2(0f, 0.5f), new Vector2(40f, 40f), new Vector2(30f, 0f));
            slot.Price = HuntUiKit.Label(chip.transform, "0", 30, theme.deep, TextAnchor.MiddleCenter, false,
                FontStyle.Bold, true);
            slot.Price.rectTransform.offsetMin = new Vector2(48f, 0f);

            slot.Button = root.gameObject.AddComponent<Button>();
            slot.Button.targetGraphic = bg;
            slot.Button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HuntButtonJuice>();
            var captured = kind;
            slot.Button.onClick.AddListener(() => Pressed?.Invoke(captured));
            return slot;
        }

        /// <summary>Re-reads stock and prices from the wallet.</summary>
        public void Refresh()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null)
                    continue;
                var count = HuntHintStock.Count(s.Kind);
                s.CountBadge.gameObject.SetActive(count > 0);
                s.PriceChip.gameObject.SetActive(count <= 0);
                s.Count.text = count.ToString();
                s.Price.text = HuntHintStock.Price(s.Kind).ToString();
                s.Name.text = HuntHints.Name(s.Kind, HuntWallet.Tuning);
            }
        }

        /// <summary>Lights one slot and dims the rest (a hint effect is running). null = all normal.</summary>
        public void SetActive(HuntHintKind? active)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null)
                    continue;
                var isActive = active.HasValue && active.Value == s.Kind;
                s.Group.alpha = active.HasValue && !isActive ? 0.4f : 1f;
                s.Edge.color = HuntUiTheme.WithAlpha(s.Color, isActive ? 1f : 0.85f);
            }
        }

        public void SetInteractable(bool on)
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i] != null)
                    _slots[i].Button.interactable = on;
        }

        /// <summary>Screen-space rect of a slot (flying-text anchor, tutorials).</summary>
        public RectTransform SlotRect(HuntHintKind kind)
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].Kind == kind)
                    return _slots[i].Root;
            return null;
        }
    }
}
