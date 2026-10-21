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
        public Color bgTop = Hex("#6C3BFF");
        public Color bgBottom = Hex("#FF5FA2");

        [Header("Palette")]
        public Color primary = Hex("#7C4DFF");
        public Color accent = Hex("#FFC93C");
        public Color success = Hex("#2ED47A");
        public Color danger = Hex("#FF4D5E");
        public Color info = Hex("#29C5F6");
        public Color panel = Hex("#FFF7EC");
        public Color panelAlt = Hex("#FFE9D2");
        public Color ink = Hex("#2B1B4A");
        public Color inkSoft = Hex("#7A6A99");
        public Color coin = Hex("#FFC93C");
        public Color hint = Hex("#29C5F6");
        public Color locked = Hex("#B9B1CC");

        [Tooltip("Toughness 1..5 ring colours on the map / intro card.")]
        public Color[] toughness =
        {
            Hex("#2ED47A"), Hex("#FFC93C"), Hex("#FF9A3C"), Hex("#FF4D5E"), Hex("#B04DFF")
        };

        [Tooltip("Confetti colours.")]
        public Color[] confetti =
        {
            Hex("#FFC93C"), Hex("#FF5FA2"), Hex("#29C5F6"), Hex("#2ED47A"), Hex("#7C4DFF"), Hex("#FF9A3C")
        };

        [Header("Typography (optional)")]
        public Font font;

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

        public Font Font
        {
            get
            {
                if (font != null)
                    return font;
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
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
