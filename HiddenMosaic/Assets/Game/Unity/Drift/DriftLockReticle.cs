using Game.Core;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>
    /// Fighter-jet lock-on box drawn on the mosaic layer (so the lens magnifies it with the target):
    /// four corner brackets close in and spin square while a ring of ticks fills, snap red on lock,
    /// spring out grey when the target escapes, burst white when it is shot.
    /// </summary>
    public sealed class DriftLockReticle : MonoBehaviour
    {
        enum Mode
        {
            Hidden,
            Acquiring,
            Locked,
            Lost,
            Fired
        }

        const int TickCount = 24;
        const int SortingOrder = 4000;
        static readonly Color AcquireStart = new Color(1f, 0.92f, 0.3f);
        static readonly Color AcquireEnd = new Color(1f, 0.55f, 0.1f);
        static readonly Color LockColor = new Color(1f, 0.18f, 0.16f);
        static readonly Color LostColor = new Color(0.72f, 0.72f, 0.75f);

        static Sprite _bar;
        static Sprite _dot;

        readonly Transform[] _corners = new Transform[4];
        readonly SpriteRenderer[] _bars = new SpriteRenderer[8];
        readonly SpriteRenderer[] _ticks = new SpriteRenderer[TickCount];
        TextMesh _tag;
        MeshRenderer _tagRenderer;
        Mode _mode;
        float _modeTime;
        float _progress;
        float _radius = 0.5f;
        Vector3 _position;

        public static DriftLockReticle Create(Transform parent, int layer)
        {
            var go = new GameObject("DriftLockReticle");
            go.transform.SetParent(parent, false);
            var reticle = go.AddComponent<DriftLockReticle>();
            reticle.Build(layer);
            return reticle;
        }

        public void BeginAcquire(Vector3 position, float radius)
        {
            SetMode(Mode.Acquiring);
            Follow(position, radius, 0f);
        }

        public void Lock()
        {
            SetMode(Mode.Locked);
        }

        public void Lose()
        {
            if (_mode != Mode.Hidden)
                SetMode(Mode.Lost);
        }

        public void Fire()
        {
            SetMode(Mode.Fired);
        }

        public void Hide()
        {
            SetMode(Mode.Hidden);
        }

        /// <summary>Called every frame while a target is tracked; the box rides the moving tile.</summary>
        public void Follow(Vector3 position, float radius, float progress)
        {
            _position = position;
            _radius = Mathf.Max(0.05f, radius);
            _progress = progress;
        }

        void SetMode(Mode mode)
        {
            _mode = mode;
            _modeTime = 0f;
            gameObject.SetActive(mode != Mode.Hidden);
        }

        void LateUpdate()
        {
            _modeTime += Time.deltaTime;
            float half, spin, alpha = 1f, width = 0.07f;
            Color color;
            var litTicks = 0f;
            var tagAlpha = 0f;
            switch (_mode)
            {
                case Mode.Acquiring:
                {
                    var p = Ease.OutCubic(_progress);
                    half = Mathf.Lerp(1.9f, 1.18f, p);
                    spin = Mathf.Lerp(90f, 0f, p);
                    color = Color.Lerp(AcquireStart, AcquireEnd, _progress);
                    // Faster flicker as the lock nears, like a seeker tone.
                    alpha = 0.75f + 0.25f * Mathf.Sin(_modeTime * Mathf.Lerp(10f, 40f, _progress));
                    litTicks = _progress * TickCount;
                    break;
                }
                case Mode.Locked:
                {
                    var t = Mathf.Clamp01(_modeTime / 0.25f);
                    half = Mathf.LerpUnclamped(1.18f, 1.0f, Ease.OutBack(t));
                    spin = 0f;
                    color = LockColor;
                    width = 0.1f + 0.025f * Mathf.Sin(_modeTime * 18f);
                    litTicks = TickCount;
                    tagAlpha = 1f;
                    break;
                }
                case Mode.Lost:
                {
                    var t = Mathf.Clamp01(_modeTime / 0.35f);
                    half = Mathf.Lerp(1.18f, 2.3f, Ease.OutCubic(t));
                    spin = Mathf.Lerp(0f, -35f, t);
                    color = LostColor;
                    alpha = 1f - t;
                    if (t >= 1f)
                        Hide();
                    break;
                }
                case Mode.Fired:
                {
                    var t = Mathf.Clamp01(_modeTime / 0.25f);
                    half = Mathf.Lerp(1.0f, 2.8f, Ease.OutQuad(t));
                    spin = 0f;
                    color = Color.white;
                    alpha = 1f - t;
                    litTicks = TickCount;
                    if (t >= 1f)
                        Hide();
                    break;
                }
                default:
                    return;
            }

            transform.position = new Vector3(_position.x, _position.y, -0.5f);
            var r = _radius;
            var arm = r * 0.55f;
            var thick = r * width;
            for (var i = 0; i < 4; i++)
            {
                var sx = i == 0 || i == 3 ? -1f : 1f;
                var sy = i < 2 ? 1f : -1f;
                var corner = _corners[i];
                corner.localPosition = Quaternion.Euler(0f, 0f, spin) * new Vector3(sx * half * r, sy * half * r, 0f);
                corner.localRotation = Quaternion.Euler(0f, 0f, spin);
                var horizontal = _bars[i * 2];
                var vertical = _bars[i * 2 + 1];
                horizontal.transform.localPosition = new Vector3(-sx * arm * 0.5f, 0f, 0f);
                horizontal.transform.localScale = new Vector3(arm + thick, thick, 1f);
                vertical.transform.localPosition = new Vector3(0f, -sy * arm * 0.5f, 0f);
                vertical.transform.localScale = new Vector3(thick, arm + thick, 1f);
                var c = new Color(color.r, color.g, color.b, alpha);
                horizontal.color = c;
                vertical.color = c;
            }

            var ringR = r * (half + 0.35f);
            var tickSize = r * 0.09f;
            for (var i = 0; i < TickCount; i++)
            {
                var a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / TickCount;
                var tick = _ticks[i];
                tick.transform.localPosition = new Vector3(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR, 0f);
                var lit = Mathf.Clamp01(litTicks - i);
                tick.transform.localScale = Vector3.one * tickSize * Mathf.Lerp(0.6f, 1.15f, lit);
                tick.color = new Color(color.r, color.g, color.b, alpha * Mathf.Lerp(0.18f, 1f, lit));
            }

            if (_tag != null)
            {
                _tag.transform.localPosition = new Vector3(0f, -r * (half + 0.75f), 0f);
                _tag.characterSize = r * 0.07f;
                _tag.color = new Color(LockColor.r, LockColor.g, LockColor.b, tagAlpha);
                _tagRenderer.enabled = tagAlpha > 0f;
            }
        }

        void Build(int layer)
        {
            gameObject.layer = layer;
            var material = MosaicGlass.PlainSpriteMaterial();
            for (var i = 0; i < 4; i++)
            {
                var corner = new GameObject("Corner" + i).transform;
                corner.SetParent(transform, false);
                corner.gameObject.layer = layer;
                _corners[i] = corner;
                _bars[i * 2] = MakeSprite(corner, "H", Bar(), material, layer);
                _bars[i * 2 + 1] = MakeSprite(corner, "V", Bar(), material, layer);
            }

            for (var i = 0; i < TickCount; i++)
                _ticks[i] = MakeSprite(transform, "Tick" + i, Dot(), material, layer);

            var tagGo = new GameObject("LockedTag");
            tagGo.transform.SetParent(transform, false);
            tagGo.layer = layer;
            _tag = tagGo.AddComponent<TextMesh>();
            _tag.text = "LOCKED";
            _tag.anchor = TextAnchor.MiddleCenter;
            _tag.alignment = TextAlignment.Center;
            _tag.fontSize = 64;
            _tag.fontStyle = FontStyle.Bold;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                _tag.font = font;
            _tagRenderer = tagGo.GetComponent<MeshRenderer>();
            if (font != null)
                _tagRenderer.sharedMaterial = font.material;
            _tagRenderer.sortingOrder = SortingOrder + 2;
            SetMode(Mode.Hidden);
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Material material, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.sortingOrder = SortingOrder;
            return renderer;
        }

        /// <summary>1x1 world-unit white square; scaled into bars.</summary>
        static Sprite Bar()
        {
            if (_bar != null)
                return _bar;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            _bar = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _bar;
        }

        /// <summary>1 world-unit soft-edged disc.</summary>
        static Sprite Dot()
        {
            if (_dot != null)
                return _dot;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
                    var a = (byte)(Mathf.Clamp01((1f - d) * 6f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _dot;
        }
    }
}
