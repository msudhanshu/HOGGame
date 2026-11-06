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
        static Sprite _frame;
        static Sprite _brackets;
        static Sprite _dial;
        static Sprite _radial;
        static Sprite _glyphCrosshair, _glyphEye, _glyphRadar, _glyphHourglass;
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

        /// <summary>9-sliced thin rounded outline (neon edge). Same slice radius as <see cref="Rounded"/>.</summary>
        public static Sprite Frame => _frame != null ? _frame : (_frame = BuildFrame(RoundSize, RoundRadius, 3f, "HuntFrame"));

        /// <summary>9-sliced corner brackets only (arena HUD frame). Tint with the glow colour.</summary>
        public static Sprite Brackets => _brackets != null ? _brackets : (_brackets = BuildBrackets(96, 32, 30, 5f));

        /// <summary>Time-dial: outer ring, 60 tick marks (every 5th longer), two thin inner rings. Tint + rotate.</summary>
        public static Sprite Dial => _dial != null ? _dial : (_dial = BuildDial(512));

        /// <summary>Soft radial glow (bright centre, transparent edge).</summary>
        public static Sprite Radial => _radial != null ? _radial : (_radial = BuildRadial(128));

        /// <summary>Hint glyphs, drawn procedurally (white, tint at use): crosshair, eye, radar sweep, hourglass.</summary>
        public static Sprite GlyphCrosshair => _glyphCrosshair != null ? _glyphCrosshair : (_glyphCrosshair = BuildGlyph("HuntGlyphCrosshair", CrosshairCoverage));
        public static Sprite GlyphEye => _glyphEye != null ? _glyphEye : (_glyphEye = BuildGlyph("HuntGlyphEye", EyeCoverage));
        public static Sprite GlyphRadar => _glyphRadar != null ? _glyphRadar : (_glyphRadar = BuildGlyph("HuntGlyphRadar", RadarCoverage));
        public static Sprite GlyphHourglass => _glyphHourglass != null ? _glyphHourglass : (_glyphHourglass = BuildGlyph("HuntGlyphHourglass", HourglassCoverage));

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
            var rim = HuntUiTheme.Hex("#C9780A");
            var face = HuntUiTheme.Hex("#FFC857");
            var shine = HuntUiTheme.Hex("#FFF2B8");
            var emboss = HuntUiTheme.Hex("#FFE08A");
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

        static Sprite BuildFrame(int size, int radius, float stroke, string name)
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
                    var d = RoundedDistance(x, y, half, inner, radius);
                    var a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + stroke + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

        static Sprite BuildBrackets(int size, int border, int arm, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HuntBrackets",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            const int radius = 14;
            var px = new Color32[size * size];
            var half = size * 0.5f;
            var inner = half - radius;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Only the four corner squares carry ink; the stretched middle stays empty.
                    var inCornerX = x < arm || x >= size - arm;
                    var inCornerY = y < arm || y >= size - arm;
                    var a = 0f;
                    if (inCornerX && inCornerY)
                    {
                        var d = RoundedDistance(x, y, half, inner, radius);
                        a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + stroke + 0.5f);
                    }

                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            var b = new Vector4(border, border, border, border);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, b);
        }

        static float RoundedDistance(int x, int y, float half, float inner, float radius)
        {
            var qx = Mathf.Abs(x + 0.5f - half) - inner;
            var qy = Mathf.Abs(y + 0.5f - half) - inner;
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static Sprite BuildDial(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HuntDial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            var c = size * 0.5f;
            var r = c - 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - c;
                    var dy = y + 0.5f - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var n = d / r;
                    var a = 0f;
                    // outer ring
                    a = Mathf.Max(a, Mathf.Clamp01(1.6f - Mathf.Abs(d - r)));
                    // 60 ticks, every 5th longer
                    var ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    var step = ang / 6f;
                    var frac = Mathf.Abs(step - Mathf.Round(step)) * 6f; // degrees from nearest tick
                    var arc = frac * Mathf.Deg2Rad * d;                  // pixels from tick centre
                    var major = Mathf.RoundToInt(step) % 5 == 0;
                    var inner = major ? 0.86f : 0.91f;
                    if (n > inner && n < 0.965f)
                        a = Mathf.Max(a, Mathf.Clamp01((major ? 2.2f : 1.4f) - arc));
                    // inner rings
                    a = Mathf.Max(a, 0.7f * Mathf.Clamp01(1.3f - Mathf.Abs(d - r * 0.74f)));
                    a = Mathf.Max(a, 0.35f * Mathf.Clamp01(1.3f - Mathf.Abs(d - r * 0.55f)));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite BuildRadial(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HuntRadial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - c) / c;
                    var dy = (y + 0.5f - c) / c;
                    var t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    var a = t * t;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- glyphs: coverage in [-0.5, 0.5] glyph space (y up), stroke widths in the same units ----

        const float Stroke = 0.075f;

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            var vx = bx - ax;
            var vy = by - ay;
            var wx = px - ax;
            var wy = py - ay;
            var t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
            var dx = px - (ax + vx * t);
            var dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Line(float dist, float half, float aa) => Mathf.Clamp01((half - dist) / aa + 0.5f);

        static float CrosshairCoverage(float x, float y, float aa)
        {
            var r = Mathf.Sqrt(x * x + y * y);
            var a = Line(Mathf.Abs(r - 0.27f), Stroke * 0.5f, aa);
            a = Mathf.Max(a, Line(r, 0.065f, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0f, 0.17f, 0f, 0.46f), Stroke * 0.5f, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0f, -0.17f, 0f, -0.46f), Stroke * 0.5f, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0.17f, 0f, 0.46f, 0f), Stroke * 0.5f, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, -0.17f, 0f, -0.46f, 0f), Stroke * 0.5f, aa));
            return a;
        }

        static float EyeCoverage(float x, float y, float aa)
        {
            var a = 0f;
            const float w = 0.47f;
            if (Mathf.Abs(x) < w)
            {
                var u = x / w;
                var h = 0.27f * (1f - u * u);
                var d = Mathf.Abs(Mathf.Abs(y) - h) * 0.85f;
                a = Line(d, Stroke * 0.5f, aa);
            }

            var r = Mathf.Sqrt(x * x + y * y);
            a = Mathf.Max(a, Line(Mathf.Abs(r - 0.125f), Stroke * 0.45f, aa));
            a = Mathf.Max(a, Line(r, 0.05f, aa));
            return a;
        }

        static float RadarCoverage(float x, float y, float aa)
        {
            var r = Mathf.Sqrt(x * x + y * y);
            var a = Line(Mathf.Abs(r - 0.42f), Stroke * 0.5f, aa);
            a = Mathf.Max(a, 0.65f * Line(Mathf.Abs(r - 0.21f), Stroke * 0.35f, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0f, 0f, 0.3f, 0.3f), Stroke * 0.5f, aa));
            a = Mathf.Max(a, Line(r, 0.05f, aa));
            var bx = x + 0.17f;
            var by = y - 0.15f;
            a = Mathf.Max(a, Line(Mathf.Sqrt(bx * bx + by * by), 0.055f, aa));
            return a;
        }

        static float HourglassCoverage(float x, float y, float aa)
        {
            var half = Stroke * 0.5f;
            var a = Line(SegDist(x, y, -0.29f, 0.42f, 0.29f, 0.42f), half, aa);
            a = Mathf.Max(a, Line(SegDist(x, y, -0.29f, -0.42f, 0.29f, -0.42f), half, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, -0.22f, 0.42f, 0f, 0f), half, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0.22f, 0.42f, 0f, 0f), half, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, -0.22f, -0.42f, 0f, 0f), half, aa));
            a = Mathf.Max(a, Line(SegDist(x, y, 0.22f, -0.42f, 0f, 0f), half, aa));
            // sand in the lower bulb
            if (y < -0.18f && y > -0.40f && Mathf.Abs(x) < (y + 0.40f) * 0.8f)
                a = Mathf.Max(a, 0.7f);
            return a;
        }

        static Sprite BuildGlyph(string name, System.Func<float, float, float, float> coverage)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            const float aa = 1f / size * 1.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var gx = (x + 0.5f) / size - 0.5f;
                    var gy = (y + 0.5f) / size - 0.5f;
                    var a = Mathf.Clamp01(coverage(gx, gy, aa));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(px);
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
