using Nixin.Mosaic.Core;
using Nixin.Pinhole;
using Nixin.Pinhole.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Game.Unity
{
    [DefaultExecutionOrder(-46)]
    public sealed class MosaicGlass : MonoBehaviour
    {
        public const int MosaicLayer = 31;

        const float BackdropDistance = 1.55f;

        [SerializeField] Camera targetCamera;
        [SerializeField] PinholeSurface surface;
        [SerializeField] float fadeSeconds = 6f;
        [SerializeField] float holeRadius = 0.09f;
        [SerializeField] float zoom = 3.8f;
        [SerializeField] float mobileHandleOffsetV = 0.14f;
        [SerializeField] [Range(0.08f, 0.45f)] float handleOffsetEaseBand = 0.32f;
        [Tooltip("Optional pre-blurred cover plate. If unset, the live capture is blurred in the magnifier shader.")]
        [SerializeField] Texture2D coverPlate;
        [SerializeField] [Range(0.35f, 1f)] float coverBlurStrength = 0.85f;

        static Material _spriteUnlit;
        static Material _spriteReveal;

        MosaicSaturationDriver _saturation;

        RenderTexture _rt;
        Camera _capture;
        MosaicScanMemory _memory;
        int _savedMainCullingMask = -1;
        bool _glassFogMode;
        MeshRenderer _backdropRenderer;
        Material _backdropMaterial;

        public MosaicScanMemory Memory => _memory;
        /// <summary>World point under the center of the magnifier (aim sample).</summary>
        public Vector3 GlassWorld { get; private set; }

        /// <summary>World point under the finger; the lens sits above this on touch devices.</summary>
        public Vector3 HandleWorld { get; private set; }

        public float GlassWorldRadius { get; private set; }
        public bool PointerHeld { get; private set; }
        public MosaicSaturationDriver Saturation => _saturation;

        public void ApplyRules(float fade, float hole, float zoomAmount)
        {
            fadeSeconds = fade;
            holeRadius = hole;
            zoom = zoomAmount;
            if (surface != null)
            {
                surface.HoleRadius = holeRadius;
                surface.Zoom = zoom;
            }
        }

        /// <summary>When enabled, a blurred cover layer hides the board; only the magnifier shows sharp detail.</summary>
        public void SetGlassFogMode(bool enabled)
        {
            _glassFogMode = enabled;
            ApplyFogPresentation();
        }

        void ApplyFogPresentation()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
            {
                if (_glassFogMode)
                {
                    if (_savedMainCullingMask < 0)
                        _savedMainCullingMask = cam.cullingMask;
                    cam.cullingMask &= ~(1 << MosaicLayer);
                }
                else if (_savedMainCullingMask >= 0)
                {
                    cam.cullingMask = _savedMainCullingMask;
                    _savedMainCullingMask = -1;
                }
            }

            if (surface == null)
                return;

            if (_glassFogMode)
            {
                EnsureBlurBackdrop();
                surface.DistinctZoomCover = true;
                surface.MagnifierHoleOnly = true;
                surface.FogBlur = 0f;
                surface.OverlayAlpha = 1f;
                if (_rt != null)
                    surface.SetTextures(_rt, _rt, takeOwnership: false);
                if (_backdropRenderer != null)
                    _backdropRenderer.enabled = true;
            }
            else
            {
                surface.FogBlur = 0f;
                surface.DistinctZoomCover = false;
                surface.MagnifierHoleOnly = false;
                surface.OverlayAlpha = 0f;
                if (_rt != null)
                    surface.SetTextures(_rt, _rt, takeOwnership: false);
                if (_backdropRenderer != null)
                    _backdropRenderer.enabled = false;
            }
        }

        void EnsureBlurBackdrop()
        {
            if (_backdropRenderer != null)
                return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BlurBackdrop";
            quad.transform.SetParent(transform, false);
            var col = quad.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            _backdropRenderer = quad.GetComponent<MeshRenderer>();
            _backdropRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _backdropRenderer.receiveShadows = false;

            var shader = Shader.Find("HiddenMosaic/BlurBackdrop");
            if (shader == null)
            {
                Debug.LogWarning("HiddenMosaic/BlurBackdrop shader missing; memory cover may not render.");
                return;
            }

            _backdropMaterial = new Material(shader) { name = "MosaicBlurBackdropMat" };
            _backdropRenderer.sharedMaterial = _backdropMaterial;
        }

        void SyncBlurBackdrop(Camera cam)
        {
            if (!_glassFogMode || _backdropRenderer == null || _backdropMaterial == null || _rt == null)
                return;

            ScreenFitQuad.Apply(_backdropRenderer.transform, cam, BackdropDistance);
            if (coverPlate != null)
            {
                _backdropMaterial.SetTexture("_MainTex", coverPlate);
                _backdropMaterial.SetFloat("_BlurStrength", 0f);
            }
            else
            {
                _backdropMaterial.SetTexture("_MainTex", _rt);
                _backdropMaterial.SetFloat("_BlurStrength", coverBlurStrength);
            }
        }

        public void Bind(Transform levelRoot, Camera cam)
        {
            targetCamera = cam;
            EnsureGlobalLight();
            SetLayerRecursively(levelRoot.gameObject, MosaicLayer);
            ApplyRevealMaterial(levelRoot);
            EnsureSaturationDriver(levelRoot);
            EnsureCapture(cam);
            EnsureSurface(cam);
            _memory = new MosaicScanMemory(fadeSeconds, 0.2f, 12);
            TrackPointer(cam);
            ApplyFogPresentation();
        }

        public bool IsRevealed(Vector3 world)
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return true;
            TrackPointer(cam);
            var dx = world.x - GlassWorld.x;
            var dy = world.y - GlassWorld.y;
            if (dx * dx + dy * dy <= GlassWorldRadius * GlassWorldRadius)
                return true;
            return _memory != null && _memory.Contains(world.x, world.y, Time.time);
        }

        void Update()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
                TrackPointer(cam);
        }

        void LateUpdate()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return;

            if (_capture != null)
            {
                _capture.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
                _capture.orthographicSize = cam.orthographicSize;
                _capture.aspect = cam.aspect;
                _capture.rect = new Rect(0f, 0f, 1f, 1f);
                _capture.backgroundColor = cam.backgroundColor;
            }

            if (_glassFogMode && _capture != null)
                _capture.Render();
            SyncBlurBackdrop(cam);
            if (_memory != null)
                _memory.Tick(Time.time);
        }

        void TrackPointer(Camera cam)
        {
            GlassWorldRadius = cam.orthographicSize * 2f * holeRadius;
            var pointer = Pointer.current;
            if (pointer == null)
            {
                PointerHeld = false;
                return;
            }

            PointerHeld = pointer.press.isPressed;
            Vector2 screen = pointer.position.ReadValue();
            var depth = -cam.transform.position.z;
            var fingerScreen = new Vector3(screen.x, screen.y, depth);
            HandleWorld = cam.ScreenToWorldPoint(fingerScreen);

            var handleOffsetV = ComputeHandleOffsetV(cam, screen);
            if (surface != null)
                surface.HoleOffset = handleOffsetV;

            var playHeightPx = cam.rect.height * Screen.height;
            var offsetPx = handleOffsetV * playHeightPx;
            var sampleScreen = new Vector3(screen.x, screen.y + offsetPx, depth);
            GlassWorld = cam.ScreenToWorldPoint(sampleScreen);

            if (PointerHeld && _memory != null)
                _memory.Record(GlassWorld.x, GlassWorld.y, GlassWorldRadius, Time.time);
        }

        float ComputeHandleOffsetV(Camera cam, Vector2 screenPos)
        {
            if (!UseHandleOffset())
                return 0f;

            var rect = cam.rect;
            var bottomPx = rect.y * Screen.height;
            var topPx = (rect.y + rect.height) * Screen.height;
            var playHeight = topPx - bottomPx;
            if (playHeight < 1f)
                return mobileHandleOffsetV;

            var y01 = Mathf.Clamp01((screenPos.y - bottomPx) / playHeight);
            var band = Mathf.Clamp(handleOffsetEaseBand, 0.08f, 0.45f);
            if (y01 >= band)
                return mobileHandleOffsetV;

            return mobileHandleOffsetV * (y01 / band);
        }

        static bool UseHandleOffset()
        {
            if (Application.isMobilePlatform)
                return true;
            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
        }

        void OnDestroy()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null && _savedMainCullingMask >= 0)
                cam.cullingMask = _savedMainCullingMask;

            if (_capture != null)
            {
                Destroy(_capture.gameObject);
                _capture = null;
            }

            if (_backdropMaterial != null)
                Destroy(_backdropMaterial);

            ReleaseRt();
        }

        void EnsureCapture(Camera cam)
        {
            var height = Mathf.Clamp(Screen.height, 1024, 2048);
            var width = Mathf.Max(64, Mathf.RoundToInt(height * Mathf.Max(cam.aspect, 0.1f)));
            if (_rt != null && (_rt.width != width || _rt.height != height))
                ReleaseRt();

            if (_rt == null)
            {
                _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "MosaicGlassRT",
                    filterMode = FilterMode.Bilinear
                };
                _rt.Create();
            }

            if (_capture == null)
            {
                var go = new GameObject("MosaicCapture");
                _capture = go.AddComponent<Camera>();
            }

            _capture.CopyFrom(cam);
            _capture.transform.SetParent(null);
            _capture.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            _capture.tag = "Untagged";
            // Capture renders to a texture — keep a full rect; match play-camera aspect only.
            _capture.rect = new Rect(0f, 0f, 1f, 1f);
            var listener = _capture.GetComponent<AudioListener>();
            if (listener != null)
                Destroy(listener);
            _capture.cullingMask = 1 << MosaicLayer;
            _capture.targetTexture = _rt;
            _capture.clearFlags = CameraClearFlags.SolidColor;
            _capture.backgroundColor = cam.backgroundColor;
            _capture.allowHDR = false;
            _capture.allowMSAA = false;
            _capture.depth = -10;
            _capture.aspect = cam.aspect;
            _capture.enabled = true;
            ConfigureUrp(_capture);
        }

        void EnsureSurface(Camera cam)
        {
            if (surface == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "MagnifyingGlass";
                quad.transform.SetParent(transform, false);
                var col = quad.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
                surface = quad.AddComponent<PinholeSurface>();
            }

            surface.Mode = PinholeMode.Zoom;
            surface.MaintainAspect = false;
            surface.HoleRadius = holeRadius;
            surface.HoleOffset = 0f;
            surface.Zoom = zoom;
            surface.OverlayAlpha = 0f;
            surface.SetTextures(_rt, _rt, takeOwnership: false);
            var meshRenderer = surface.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.sortingOrder = 5000;
            }
        }

        void ReleaseRt()
        {
            if (_rt == null)
                return;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        static void ConfigureUrp(Camera capture)
        {
            var dst = capture.GetUniversalAdditionalCameraData();
            dst.renderType = CameraRenderType.Base;
            dst.renderPostProcessing = false;
            dst.antialiasing = AntialiasingMode.None;
            dst.SetRenderer(0);
        }

        static void EnsureGlobalLight()
        {
            if (FindFirstObjectByType<Light2D>() != null)
                return;
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        void EnsureSaturationDriver(Transform levelRoot)
        {
            if (_saturation == null)
                _saturation = GetComponent<MosaicSaturationDriver>();
            if (_saturation == null)
                _saturation = gameObject.AddComponent<MosaicSaturationDriver>();
            _saturation.Bind(levelRoot, this);
        }

        static void ApplyRevealMaterial(Transform root)
        {
            var mat = SpriteRevealMaterial();
            if (mat == null || root == null)
                return;
            var all = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < all.Length; i++)
                all[i].sharedMaterial = mat;
        }

        static Material SpriteRevealMaterial()
        {
            if (_spriteReveal != null)
                return _spriteReveal;

            var shader = Shader.Find("HiddenMosaic/MosaicSpriteReveal");
            if (shader == null)
                return SpriteUnlit();

            _spriteReveal = new Material(shader) { name = "MosaicSpriteReveal" };
            return _spriteReveal;
        }

        static Material SpriteUnlit()
        {
            if (_spriteUnlit != null)
                return _spriteUnlit;

            var fromResources = Resources.Load<Material>("HiddenMosaic/MosaicSpriteUnlit");
            if (fromResources != null)
            {
                _spriteUnlit = fromResources;
                return _spriteUnlit;
            }

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            _spriteUnlit = new Material(shader) { name = "MosaicSpriteUnlit" };
            return _spriteUnlit;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
