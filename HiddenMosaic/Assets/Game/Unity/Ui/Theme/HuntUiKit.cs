using System;
using Game.Core;
using Nixin.Icons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Chunky, vibrant code-built widgets. Every colour comes from HuntUiTheme.</summary>
    public static class HuntUiKit
    {
        public const float SpriteRadius = 40f;

        static HuntUiTheme T => HuntUiTheme.Current;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Point-anchors rt at a normalized anchor with a pixel size and offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default, Vector2? pivot = null)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Rounded(Transform parent, string name, Color color, float radius = 40f, bool raycast = false)
        {
            var img = Image(parent, name, HuntUiSprites.Rounded, color, raycast);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(4f, radius);
            return img;
        }

        public static Image GradientBg(Transform parent, Color top, Color bottom)
        {
            var img = Image(parent, "Bg", HuntUiSprites.Gradient(top, bottom), Color.white, true);
            Stretch(img.rectTransform);
            img.transform.SetAsFirstSibling();
            return img;
        }

        /// <summary>Panel with a soft drop shadow. Returns the face; root is face.parent.</summary>
        public static Image Card(Transform parent, Vector2 size, Color color, float radius = 48f)
        {
            var root = Rect("Card", parent);
            Place(root, new Vector2(0.5f, 0.5f), size);
            var shadow = Image(root, "Shadow", HuntUiSprites.Shadow, HuntUiTheme.WithAlpha(T.glow, radius > 2f ? 0.28f : 0f));
            shadow.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(shadow.rectTransform, -22f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            var face = Rounded(root, "Face", color, radius, true);
            Stretch(face.rectTransform);
            if (radius > 2f)
                NeonEdge(face.transform, T.glow, radius);
            return face;
        }

        /// <summary>Thin glowing outline stretched over a rounded panel (child "Edge").</summary>
        public static Image NeonEdge(Transform parent, Color color, float radius = 48f, float alpha = 0.9f)
        {
            var edge = Image(parent, "Edge", HuntUiSprites.Frame, HuntUiTheme.WithAlpha(color, alpha));
            edge.type = UnityEngine.UI.Image.Type.Sliced;
            edge.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(4f, radius);
            Stretch(edge.rectTransform);
            return edge;
        }

        /// <summary>Perceived brightness 0..1 - decides dark vs light text on a coloured face.</summary>
        public static bool IsLight(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f > 0.52f;

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, bool outline = false, FontStyle style = FontStyle.Bold,
            bool title = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = title ? T.TitleFont : T.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            Stretch(t.rectTransform);
            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = HuntUiTheme.WithAlpha(T.deep, 0.85f);
                o.effectDistance = new Vector2(3f, -3f);
                var s = go.AddComponent<Shadow>();
                s.effectColor = HuntUiTheme.WithAlpha(T.deep, 0.6f);
                s.effectDistance = new Vector2(0f, -6f);
            }

            return t;
        }

        public static Image Icon(Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            var img = Image(parent, "Icon", sprite, color);
            img.preserveAspect = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), size);
            return img;
        }

        public static Sprite CoinSprite => T.coinIcon != null ? T.coinIcon : HuntUiSprites.Coin;

        /// <summary>Hint glyph = magnifier, fits the game.</summary>
        public static Sprite HintGlyph => T.hintIcon != null ? T.hintIcon : NixinIcons.Load(NixinIcons.Search);

        /// <summary>Round coloured badge with the hint glyph inside.</summary>
        public static RectTransform HintBadgeIcon(Transform parent, float size)
        {
            var root = Rect("HintIcon", parent);
            Place(root, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            if (T.hintIcon != null)
            {
                Stretch(Image(root, "Art", T.hintIcon, Color.white).rectTransform);
                return root;
            }

            Stretch(Image(root, "Rim", HuntUiSprites.Circle, HuntUiTheme.Darken(T.hint, 0.25f)).rectTransform);
            Stretch(Image(root, "Face", HuntUiSprites.Circle, T.hint).rectTransform, size * 0.08f);
            var glyph = Image(root, "Glyph", HintGlyph, Color.white);
            glyph.preserveAspect = true;
            Stretch(glyph.rectTransform, size * 0.22f);
            return root;
        }

        public static RectTransform CoinIcon(Transform parent, float size)
        {
            var img = Image(parent, "CoinIcon", CoinSprite, Color.white);
            img.preserveAspect = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            return img.rectTransform;
        }

        /// <summary>Chunky 3D button: shadow, darker lip, face, gloss, optional icon + label.</summary>
        public static HuntKitButton Button(Transform parent, string label, Color color, Vector2 size, Action onClick,
            Sprite icon = null, int fontSize = 46, float radius = 0f)
        {
            if (radius <= 0f)
                radius = Mathf.Min(size.y * 0.42f, 44f);
            var root = Rect(label + "Button", parent);
            Place(root, new Vector2(0.5f, 0.5f), size);

            // Neon glow in the button's own colour (replaces the old dark drop shadow).
            var shadow = Image(root, "Shadow", HuntUiSprites.Shadow, HuntUiTheme.WithAlpha(color, 0.5f));
            shadow.type = UnityEngine.UI.Image.Type.Sliced;
            Stretch(shadow.rectTransform, -18f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            var lip = Rounded(root, "Lip", HuntUiTheme.Darken(color, 0.28f), radius);
            Stretch(lip.rectTransform);
            lip.rectTransform.offsetMin = new Vector2(0f, -10f);
            lip.rectTransform.offsetMax = new Vector2(0f, -10f);

            var face = Rounded(root, "Face", color, radius, true);
            Stretch(face.rectTransform);

            var gloss = Rounded(face.transform, "Gloss", new Color(1f, 1f, 1f, 0.28f), radius * 0.8f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
            gloss.rectTransform.offsetMin = new Vector2(10f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-10f, -6f);

            var lightFace = IsLight(color);
            var faceText = lightFace ? new Color(0.016f, 0.07f, 0.18f, 1f) : Color.white;
            Image iconImg = null;
            Text text = null;
            var hasLabel = !string.IsNullOrEmpty(label);
            if (icon != null)
            {
                var iconSize = Mathf.Min(size.y * 0.58f, 84f);
                iconImg = Icon(root, icon, icon == CoinSprite ? Color.white : faceText, new Vector2(iconSize, iconSize));
                if (hasLabel)
                {
                    iconImg.rectTransform.anchorMin = iconImg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    iconImg.rectTransform.anchoredPosition = new Vector2(size.y * 0.55f, 2f);
                }
            }

            if (hasLabel)
            {
                text = Label(root, label, fontSize, faceText, TextAnchor.MiddleCenter, outline: !lightFace, title: true);
                if (icon != null)
                    text.rectTransform.offsetMin = new Vector2(size.y * 0.75f, 0f);
                text.rectTransform.offsetMax = new Vector2(text.rectTransform.offsetMax.x, -2f);
            }

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HuntButtonJuice>();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            return new HuntKitButton(root, button, face, lip, text, iconImg, color);
        }

        /// <summary>Round icon button (pause, settings, close).</summary>
        public static HuntKitButton IconButton(Transform parent, Sprite icon, Color color, float size, Action onClick)
        {
            var b = Button(parent, null, color, new Vector2(size, size), onClick, icon, 0, size * 0.5f);
            b.Face.sprite = HuntUiSprites.Circle;
            b.Face.type = UnityEngine.UI.Image.Type.Simple;
            b.Lip.sprite = HuntUiSprites.Circle;
            b.Lip.type = UnityEngine.UI.Image.Type.Simple;
            var shadow = b.Root.Find("Shadow");
            if (shadow != null)
            {
                var img = shadow.GetComponent<Image>();
                img.sprite = HuntUiSprites.SoftCircle;
                img.type = UnityEngine.UI.Image.Type.Simple;
            }
            var gloss = b.Face.transform.Find("Gloss");
            if (gloss != null)
                gloss.gameObject.SetActive(false);
            return b;
        }

        public static HuntKitToggle Toggle(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var row = Rect(label + "Toggle", parent);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(720f, 110f));
            var text = Label(row, label, 44, T.ink, TextAnchor.MiddleLeft);
            text.rectTransform.offsetMax = new Vector2(-220f, 0f);

            var track = Rounded(row, "Track", T.success, 40f, true);
            Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(170f, 84f), new Vector2(-85f, 0f));
            var knob = Image(track.transform, "Knob", HuntUiSprites.Circle, Color.white);
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(68f, 68f));
            var toggle = new HuntKitToggle(row, track, knob, value, onChanged);
            var button = track.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            track.gameObject.AddComponent<HuntButtonJuice>();
            button.onClick.AddListener(toggle.Flip);
            return toggle;
        }

        /// <summary>Small round icon toggle with a caption under it (music / sound quick toggles).
        /// Off = grey face plus <paramref name="offIcon"/>, or a slash over the on icon when null.</summary>
        public static HuntKitIconToggle IconToggle(Transform parent, Sprite onIcon, Sprite offIcon, string label,
            Color color, float size, bool value, Action<bool> onChanged)
        {
            var root = Rect(label + "IconToggle", parent);
            Place(root, new Vector2(0.5f, 0.5f), new Vector2(size, size));
            var button = IconButton(root, onIcon, color, size, null);
            Stretch(button.Root);
            button.Icon.rectTransform.sizeDelta = new Vector2(size * 0.52f, size * 0.52f);

            Image slash = null;
            if (offIcon == null)
            {
                slash = Rounded(button.Root, "Slash", Color.white, 6f);
                Place(slash.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.09f));
                slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }

            var caption = Label(root, label, Mathf.RoundToInt(size * 0.28f), T.inkSoft);
            Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(size * 1.8f, size * 0.4f),
                new Vector2(0f, -size * 0.12f), new Vector2(0.5f, 1f));

            var toggle = new HuntKitIconToggle(root, button, onIcon, offIcon, slash, color, value, onChanged);
            button.Button.onClick.AddListener(toggle.Flip);
            return toggle;
        }

        /// <summary>Row of 3 stars; earned ones gold, others faded.</summary>
        public static Image[] Stars(Transform parent, int earned, float size, float spacing, bool arc = false)
        {
            var row = Rect("Stars", parent);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(size * 3f + spacing * 2f, size * 1.2f));
            var stars = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var s = Image(row, "Star" + i, HuntUiSprites.Star, i < earned ? T.accent : new Color(0f, 0f, 0f, 0.22f));
                var x = (i - 1) * (size + spacing);
                var y = arc && i == 1 ? size * 0.22f : 0f;
                var scale = arc && i == 1 ? 1.2f : 1f;
                Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size, size) * scale, new Vector2(x, y));
                if (arc)
                    s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (1 - i) * 12f);
                stars[i] = s;
            }

            return stars;
        }

        /// <summary>Coloured title ribbon (popups).</summary>
        public static Text Ribbon(Transform parent, string title, Color color, float width = 760f)
        {
            var ribbon = Rounded(parent, "Ribbon", color, 36f);
            Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(width, 130f), new Vector2(0f, 10f));
            var lip = Rounded(ribbon.transform, "RibbonLip", HuntUiTheme.Darken(color, 0.3f), 36f);
            Stretch(lip.rectTransform);
            lip.rectTransform.offsetMin = new Vector2(0f, -10f);
            lip.rectTransform.offsetMax = new Vector2(0f, -10f);
            lip.transform.SetAsFirstSibling();
            var face = Rounded(ribbon.transform, "RibbonFace", color, 36f);
            Stretch(face.rectTransform);
            ribbon.color = Color.clear;
            var text = Label(ribbon.transform, title, 64, Color.white, TextAnchor.MiddleCenter, outline: true);
            return text;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>Time-machine backdrop: two slowly counter-rotating dials plus drifting energy motes.</summary>
        public static void FloatingTiles(Transform parent, int count = 14)
        {
            var layer = Stretch(Rect("Tiles", parent));
            layer.SetSiblingIndex(1);

            var glow = Image(layer, "Glow", HuntUiSprites.Radial, HuntUiTheme.WithAlpha(T.bgGlow, 0.55f));
            Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(2400f, 2400f), new Vector2(0f, -120f));

            var big = Image(layer, "DialBig", HuntUiSprites.Dial, HuntUiTheme.WithAlpha(T.glow, 0.2f));
            Place(big.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1500f, 1500f), new Vector2(0f, -120f));
            big.gameObject.AddComponent<HuntSpin>().degreesPerSecond = 3f;

            var small = Image(layer, "DialSmall", HuntUiSprites.Dial, HuntUiTheme.WithAlpha(T.accent, 0.16f));
            Place(small.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(960f, 960f), new Vector2(0f, -120f));
            small.gameObject.AddComponent<HuntSpin>().degreesPerSecond = -5f;

            for (var i = 0; i < count; i++)
            {
                var gold = i % 4 == 0;
                var size = UnityEngine.Random.Range(8f, 22f);
                var mote = Image(layer, "Mote", HuntUiSprites.SoftCircle,
                    HuntUiTheme.WithAlpha(gold ? T.accent : T.glow, UnityEngine.Random.Range(0.25f, 0.6f)));
                Place(mote.rectTransform, new Vector2(UnityEngine.Random.value, UnityEngine.Random.value), new Vector2(size, size));
                var bob = mote.gameObject.AddComponent<HuntFloatBob>();
                bob.amplitude = UnityEngine.Random.Range(20f, 60f);
                bob.speed = UnityEngine.Random.Range(0.25f, 0.7f);
            }
        }
    }

    public sealed class HuntKitButton
    {
        public HuntKitButton(RectTransform root, Button button, Image face, Image lip, Text label, Image icon, Color color)
        {
            Root = root;
            Button = button;
            Face = face;
            Lip = lip;
            Label = label;
            Icon = icon;
            _color = color;
        }

        public RectTransform Root { get; }
        public Button Button { get; }
        public Image Face { get; }
        public Image Lip { get; }
        public Text Label { get; }
        public Image Icon { get; }
        Color _color;

        public GameObject gameObject => Root.gameObject;

        public void SetLabel(string text)
        {
            if (Label != null)
                Label.text = text;
        }

        public void SetColor(Color color)
        {
            _color = color;
            Apply(Button.interactable);
        }

        public void SetInteractable(bool on)
        {
            Button.interactable = on;
            Apply(on);
        }

        void Apply(bool on)
        {
            var c = on ? _color : HuntUiTheme.Current.locked;
            Face.color = c;
            Lip.color = HuntUiTheme.Darken(c, 0.28f);
            var glow = Root.Find("Shadow");
            if (glow != null)
                glow.GetComponent<Image>().color = HuntUiTheme.WithAlpha(c, on ? 0.5f : 0f);
            var fg = HuntUiKit.IsLight(c) ? new Color(0.016f, 0.07f, 0.18f, 1f) : Color.white;
            if (Label != null)
                Label.color = fg;
            if (Icon != null && Icon.sprite != HuntUiKit.CoinSprite)
                Icon.color = fg;
        }
    }

    public sealed class HuntKitIconToggle
    {
        readonly HuntKitButton _button;
        readonly Sprite _onIcon;
        readonly Sprite _offIcon;
        readonly Image _slash;
        readonly Color _color;
        readonly Action<bool> _changed;

        public HuntKitIconToggle(RectTransform root, HuntKitButton button, Sprite onIcon, Sprite offIcon, Image slash,
            Color color, bool value, Action<bool> changed)
        {
            Root = root;
            _button = button;
            _onIcon = onIcon;
            _offIcon = offIcon;
            _slash = slash;
            _color = color;
            _changed = changed;
            Set(value);
        }

        public RectTransform Root { get; }
        public bool Value { get; private set; }

        public void Flip()
        {
            Set(!Value);
            _changed?.Invoke(Value);
        }

        public void Set(bool value)
        {
            Value = value;
            _button.SetColor(value ? _color : HuntUiTheme.Current.locked);
            if (_offIcon != null)
                _button.Icon.sprite = value ? _onIcon : _offIcon;
            if (_slash != null)
                _slash.gameObject.SetActive(!value);
        }
    }

    public sealed class HuntKitToggle
    {
        readonly Image _track;
        readonly Image _knob;
        readonly Action<bool> _changed;

        public HuntKitToggle(RectTransform root, Image track, Image knob, bool value, Action<bool> changed)
        {
            Root = root;
            _track = track;
            _knob = knob;
            _changed = changed;
            Value = value;
            Apply(false);
        }

        public RectTransform Root { get; }
        public bool Value { get; private set; }

        public void Flip()
        {
            Value = !Value;
            Apply(true);
            _changed?.Invoke(Value);
        }

        public void Set(bool value)
        {
            Value = value;
            Apply(false);
        }

        void Apply(bool animate)
        {
            var theme = HuntUiTheme.Current;
            _track.color = Value ? theme.success : theme.locked;
            var to = new Vector2(Value ? 43f : -43f, 0f);
            if (animate)
                HuntTweener.Move(_knob.rectTransform, _knob.rectTransform.anchoredPosition, to, 0.25f, Ease.OutBack);
            else
                _knob.rectTransform.anchoredPosition = to;
        }
    }
}
