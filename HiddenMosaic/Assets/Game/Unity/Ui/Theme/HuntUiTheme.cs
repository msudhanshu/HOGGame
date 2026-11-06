using UnityEngine;

namespace Game.Unity
{
    /// <summary>Every colour / font / optional sprite the meta UI uses. Optional asset at
    /// Resources/HuntUiTheme; without it the built-in vibrant defaults below are used. Swap in
    /// real art by assigning the sprite slots - no code change needed.</summary>
    [CreateAssetMenu(fileName = "HuntUiTheme", menuName = "Nixin Studio/Hidden Mosaic/UI Theme")]
    public sealed class HuntUiTheme : ScriptableObject
    {
        public const string ResourcePath = "HuntUiTheme";

        [Header("Background")]
        public Color bgTop = Hex("#0B1B4D");
        public Color bgBottom = Hex("#04071A");
        public Color bgGlow = Hex("#1E6BFF");

        [Header("Palette")]
        public Color primary = Hex("#2F7BFF");
        public Color accent = Hex("#FFC857");
        public Color success = Hex("#19E3A6");
        public Color danger = Hex("#FF4F7B");
        public Color info = Hex("#31D6FF");
        public Color panel = Hex("#101B3D");
        public Color panelAlt = Hex("#19295A");
        public Color ink = Hex("#E6F4FF");
        [Tooltip("Dark fill (shadows, deep panels). Use this, not ink, when you need a dark colour.")]
        public Color deep = Hex("#050B24");
        [Tooltip("Neon edge / energy accent.")]
        public Color glow = Hex("#4FE3FF");
        public Color violet = Hex("#B65CFF");
        public Color inkSoft = Hex("#8FA8D6");
        public Color coin = Hex("#FFC857");
        public Color hint = Hex("#31D6FF");
        public Color locked = Hex("#3A4770");

        [Tooltip("Toughness 1..5 ring colours on the map / intro card.")]
        public Color[] toughness =
        {
            Hex("#19E3A6"), Hex("#FFC857"), Hex("#FF8A4C"), Hex("#FF4F7B"), Hex("#B65CFF")
        };

        [Tooltip("Confetti colours.")]
        public Color[] confetti =
        {
            Hex("#FFC857"), Hex("#4FE3FF"), Hex("#19E3A6"), Hex("#B65CFF"), Hex("#2F7BFF"), Hex("#FF8A4C")
        };

        [Header("Typography (optional)")]
        [Tooltip("Body font (Rajdhani). Empty = Resources/Fonts/Rajdhani-Bold, then built-in.")]
        public Font font;
        [Tooltip("Title / number font (Orbitron). Empty = Resources/Fonts/Orbitron-Bold, then body font.")]
        public Font titleFont;

        [Header("Art overrides (optional)")]
        public Sprite logo;
        public Sprite coinIcon;
        public Sprite hintIcon;
        public Sprite chestIcon;
        public Sprite imageHogIcon;

        [Header("Audio (optional)")]
        public AudioClip music;

        static HuntUiTheme _current;

        public static HuntUiTheme Current
        {
            get
            {
                if (_current != null)
                    return _current;
                _current = Resources.Load<HuntUiTheme>(ResourcePath);
                if (_current == null)
                {
                    _current = CreateInstance<HuntUiTheme>();
                    _current.hideFlags = HideFlags.DontSave;
                }

                return _current;
            }
        }

        Font _bodyCache, _titleCache;

        public Font Font
        {
            get
            {
                if (font != null)
                    return font;
                if (_bodyCache == null)
                    _bodyCache = Resources.Load<Font>("Fonts/Rajdhani-Bold") ?? BuiltIn();
                return _bodyCache;
            }
        }

        public Font TitleFont
        {
            get
            {
                if (titleFont != null)
                    return titleFont;
                if (_titleCache == null)
                    _titleCache = Resources.Load<Font>("Fonts/Orbitron-Bold") ?? Font;
                return _titleCache;
            }
        }

        static Font BuiltIn()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public Color ToughnessColor(int toughness)
        {
            if (this.toughness == null || this.toughness.Length == 0)
                return accent;
            var i = Mathf.Clamp(toughness - 1, 0, this.toughness.Length - 1);
            return this.toughness[i];
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static Color Darken(Color c, float amount = 0.25f)
        {
            return new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
        }

        public static Color Lighten(Color c, float amount = 0.25f)
        {
            return new Color(c.r + (1f - c.r) * amount, c.g + (1f - c.g) * amount, c.b + (1f - c.b) * amount, c.a);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
