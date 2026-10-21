using System.Collections.Generic;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Runtime-generated, cached UI sprites (rounded 9-slice, circle, soft shadow,
    /// gradients, star). Keeps the code-built UI free of imported art.</summary>
    public static class HuntUiSprites
    {
        const int RoundSize = 96;
        const int RoundRadius = 40;

        static Sprite _rounded;
        static Sprite _shadow;
        static Sprite _circle;
        static Sprite _softCircle;
        static Sprite _ring;
        static Sprite _star;
        static Sprite _white;
        static Sprite _coin;
        static readonly Dictionary<long, Sprite> Gradients = new Dictionary<long, Sprite>();

        /// <summary>9-sliced rounded rectangle (use Image.Type.Sliced; pixelsPerUnitMultiplier sets the radius).</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = BuildRounded(RoundSize, RoundRadius, 1.2f, "HuntRounded"));

        /// <summary>Soft, blurred rounded rect for drop shadows (sliced).</summary>
        public static Sprite Shadow => _shadow != null ? _shadow : (_shadow = BuildRounded(RoundSize, RoundRadius, 14f, "HuntShadow"));

        public static Sprite Circle => _circle != null ? _circle : (_circle = BuildCircle(128, 0f));

        /// <summary>Blurred circle for round-button drop shadows.</summary>
        public static Sprite SoftCircle => _softCircle != null ? _softCircle : (_softCircle = BuildSoftCircle(128, 14f));

        public static Sprite Ring => _ring != null ? _ring : (_ring = BuildCircle(128, 0.8f));

        public static Sprite Star => _star != null ? _star : (_star = BuildStar(128));

        /// <summary>Gold coin with rim and embossed star (colours baked in - tint white).</summary>
        public static Sprite Coin => _coin != null ? _coin : (_coin = BuildCoin(128));

        public static Sprite White
        {
            get
            {
                if (_white != null)
                    return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "HuntWhite" };
                var px = new Color32[16];
                for (var i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return _white;
            }
        }

        /// <summary>Vertical gradient (top → bottom), cached per colour pair.</summary>
        public static Sprite Gradient(Color top, Color bottom)
        {
            var key = (PackColor(top) << 32) ^ PackColor(bottom);
            if (Gradients.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int h = 256;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                name = "HuntGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[2 * h];
            for (var y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            Gradients[key] = sprite;
            return sprite;
        }

        static long PackColor(Color c)
        {
            Color32 c32 = c;
            return ((long)c32.r << 24) | ((long)c32.g << 16) | ((long)c32.b << 8) | c32.a;
        }

        static Sprite BuildRounded(int size, int radius, float soft, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            var half = size * 0.5f;
            var inner = half - radius;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var qx = Mathf.Abs(x + 0.5f - half) - inner;
                    var qy = Mathf.Abs(y + 0.5f - half) - inner;
                    var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    var d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    // Soft sprites shrink the solid core so the blur stays inside the texture.
                    // Mathf.SmoothStep interpolates (from, to, t) - it is not GLSL smoothstep(edge0, edge1, x).
                    var a = soft > 2f
                        ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + soft) / soft))
                        : Mathf.Clamp01(0.5f - d / soft);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

        static Sprite BuildCircle(int size, float holeFrac)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = holeFrac > 0f ? "HuntRing" : "HuntCircle",
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[size * size];
            var r = size * 0.5f - 1f;
            var hole = r * holeFrac;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = y + 0.5f - size * 0.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(r - d + 0.5f);
                    if (holeFrac > 0f)
                        a *= Mathf.Clamp01(d - hole + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildSoftCircle(int size, float soft)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HuntSoftCircle", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var r = size * 0.5f - 1f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - size * 0.5f;
                    var dy = y + 0.5f - size * 0.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    var a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + soft) / soft));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildStar(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HuntStar", wrapMode = TextureWrapMode.Clamp };
            var poly = new Vector2[10];
            var c = size * 0.5f;
            var outer = size * 0.48f;
            var innerR = outer * 0.48f;
            for (var i = 0; i < 10; i++)
            {
                var ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var rad = i % 2 == 0 ? outer : innerR;
                poly[i] = new Vector2(c + Mathf.Cos(ang) * rad, c + Mathf.Sin(ang) * rad - size * 0.03f);
            }

            var px = new Color32[size * size];
            const int ss = 3;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < ss; sy++)
                    for (var sx = 0; sx < ss; sx++)
                    {
                        if (Inside(poly, new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss)))
                            hits++;
                    }

                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildCoin(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HuntCoin", wrapMode = TextureWrapMode.Clamp };
            var rim = HuntUiTheme.Hex("#E08A00");
            var face = HuntUiTheme.Hex("#FFC93C");
            var shine = HuntUiTheme.Hex("#FFF0A8");
            var emboss = HuntUiTheme.Hex("#FFE07A");
            var c = size * 0.5f;
            var r = c - 1f;
            var poly = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var rad = i % 2 == 0 ? r * 0.46f : r * 0.2f;
                poly[i] = new Vector2(c + Mathf.Cos(ang) * rad, c + Mathf.Sin(ang) * rad - 2f);
            }

            var px = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - c;
                    var dy = y + 0.5f - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Clamp01(r - d + 0.5f);
                    Color col;
                    if (d > r * 0.8f)
                        col = rim;
                    else
                    {
                        col = Color.Lerp(face, shine, Mathf.Clamp01((dy / r) * 0.8f + 0.1f) * 0.6f);
                        if (Inside(poly, new Vector2(x + 0.5f, y + 0.5f)))
                            col = emboss;
                    }

                    col.a = a;
                    px[y * size + x] = col;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y)
                    && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }

            return inside;
        }
    }
}
