using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>
    /// MM:SS digital clock drawn with seven rectangles per digit (classic "8" with ghost segments).
    /// Pure uGUI, no assets or fonts.
    /// </summary>
    public sealed class HuntSevenSegmentClock : MonoBehaviour
    {
        // bit order: a(top) b(upper right) c(lower right) d(bottom) e(lower left) f(upper left) g(middle)
        static readonly byte[] DigitMasks =
        {
            0b0111111, // 0  abcdef
            0b0000110, // 1  bc
            0b1011011, // 2  abdeg
            0b1001111, // 3  abcdg
            0b1100110, // 4  bcfg
            0b1101101, // 5  acdfg
            0b1111101, // 6  acdefg
            0b0000111, // 7  abc
            0b1111111, // 8
            0b1101111, // 9  abcdfg
        };

        const float DigitWidth = 38f;
        const float DigitHeight = 66f;
        const float DigitGap = 10f;
        const float ColonWidth = 18f;
        const float Thick = 0.16f;
        const float Gap = 0.025f;

        public const float TotalWidth = DigitWidth * 4f + DigitGap * 2f + ColonWidth + DigitGap * 2f;

        readonly Image[][] _segments = new Image[4][];
        Image _colonTop;
        Image _colonBottom;
        Color _on = new Color(1f, 0.97f, 0.4f);
        bool _blink = true;

        // Unlit segments: dark gray, independent of the lit colour so on/off always contrast.
        static readonly Color Off = new Color(0.13f, 0.13f, 0.15f, 1f);

        public static HuntSevenSegmentClock Create(RectTransform parent)
        {
            var go = new GameObject("SevenSegmentClock", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(TotalWidth, DigitHeight);
            rect.anchoredPosition = Vector2.zero;
            var clock = go.AddComponent<HuntSevenSegmentClock>();
            clock.Build(rect);
            return clock;
        }

        void Build(RectTransform root)
        {
            var x = -TotalWidth * 0.5f;
            for (var d = 0; d < 4; d++)
            {
                var box = MakeBox("Digit" + d, root, x, DigitWidth);
                _segments[d] = BuildDigit(box);
                x += DigitWidth + DigitGap;
                if (d == 1)
                {
                    var colon = MakeBox("Colon", root, x, ColonWidth);
                    _colonTop = MakeDot(colon, 0.68f);
                    _colonBottom = MakeDot(colon, 0.32f);
                    x += ColonWidth + DigitGap;
                }
            }

            SetTime(0, 0, _on);
        }

        static RectTransform MakeBox(string name, RectTransform parent, float left, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(width, DigitHeight);
            r.anchoredPosition = new Vector2(left, 0f);
            return r;
        }

        static Image[] BuildDigit(RectTransform box)
        {
            var h = Thick;
            var g = Gap;
            return new[]
            {
                Seg(box, "a", h + g, 1f - h, 1f - h - g - g * 0f, 1f),             // top
                Seg(box, "b", 1f - h, 1f, 0.5f + g, 1f - g),                       // upper right
                Seg(box, "c", 1f - h, 1f, g, 0.5f - g),                            // lower right
                Seg(box, "d", h + g, 1f - h - g, 0f, h * (DigitWidth / DigitHeight)), // bottom
                Seg(box, "e", 0f, h, g, 0.5f - g),                                 // lower left
                Seg(box, "f", 0f, h, 0.5f + g, 1f - g),                            // upper left
                Seg(box, "g", h + g, 1f - h - g, 0.5f - h * 0.5f * (DigitWidth / DigitHeight), 0.5f + h * 0.5f * (DigitWidth / DigitHeight)), // middle
            };
        }

        static Image Seg(RectTransform box, string name, float x0, float x1, float y0, float y1)
        {
            // top segment: derive y from thickness in height units so horizontals match verticals visually
            if (name == "a")
            {
                var t = Thick * (DigitWidth / DigitHeight);
                x0 = Thick + Gap; x1 = 1f - Thick - Gap; y0 = 1f - t; y1 = 1f;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(box, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        static Image MakeDot(RectTransform box, float yCenter)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(box, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.2f, yCenter - 0.06f);
            r.anchorMax = new Vector2(0.8f, yCenter + 0.06f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        public void SetTime(int minutes, int seconds, Color on)
        {
            _on = on;
            minutes = Mathf.Clamp(minutes, 0, 99);
            seconds = Mathf.Clamp(seconds, 0, 59);
            SetDigit(0, minutes / 10);
            SetDigit(1, minutes % 10);
            SetDigit(2, seconds / 10);
            SetDigit(3, seconds % 10);
            ApplyColon();
        }

        void SetDigit(int index, int value)
        {
            var segs = _segments[index];
            if (segs == null)
                return;
            var mask = DigitMasks[value];
            for (var s = 0; s < 7; s++)
                segs[s].color = (mask & (1 << s)) != 0 ? _on : Off;
        }

        void Update()
        {
            var blink = Time.unscaledTime % 1f < 0.6f;
            if (blink == _blink)
                return;
            _blink = blink;
            ApplyColon();
        }

        void ApplyColon()
        {
            if (_colonTop == null)
                return;
            var c = _blink ? _on : Off;
            _colonTop.color = c;
            _colonBottom.color = c;
        }
    }
}
