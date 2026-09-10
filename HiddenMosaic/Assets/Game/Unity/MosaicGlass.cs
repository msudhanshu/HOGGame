using Nixin.Mosaic.Core;
using Nixin.Pinhole;
using Nixin.Pinhole.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Game.Unity
{
    [DefaultExecutionOrder(-50)]
    public sealed class MosaicGlass : MonoBehaviour
    {
        public const int MosaicLayer = 31;

        [SerializeField] Camera targetCamera;
        [SerializeField] PinholeSurface surface;
        [SerializeField] float fadeSeconds = 6f;
        [SerializeField] float holeRadius = 0.14f;
        [SerializeField] float zoom = 2.2f;

        static Material _spriteUnlit;

        RenderTexture _rt;
        Camera _capture;
        MosaicScanMemory _memory;

        public MosaicScanMemory Memory => _memory;
        public Vector3 GlassWorld { get; private set; }
        public float GlassWorldRadius { get; private set; }

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

        public void Bind(Transform levelRoot, Camera cam)
        {
            targetCamera = cam;
            EnsureGlobalLight();
            SetLayerRecursively(levelRoot.gameObject, MosaicLayer);
            ApplyUnlit(levelRoot);
            EnsureCapture(cam);
            EnsureSurface(cam);
            _memory = new MosaicScanMemory(fadeSeconds, 0.2f, 12);
            TrackPointer(cam);
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
                _capture.backgroundColor = cam.backgroundColor;
            }

            TrackPointer(cam);
            if (_memory != null)
                _memory.Tick(Time.time);
        }

        void TrackPointer(Camera cam)
        {
            GlassWorldRadius = cam.orthographicSize * 2f * holeRadius;
            var pointer = Pointer.current;
            if (pointer == null)
                return;
            Vector3 screen = pointer.position.ReadValue();
            screen.z = -cam.transform.position.z;
            GlassWorld = cam.ScreenToWorldPoint(screen);
        }

        void OnDestroy()
        {
            if (_capture != null)
            {
                Destroy(_capture.gameObject);
                _capture = null;
            }

            ReleaseRt();
        }

        void EnsureCapture(Camera cam)
        {
            var height = Screen.height >= 900 ? 1024 : 512;
            var width = Mathf.Max(64, Mathf.RoundToInt(height * Mathf.Max(cam.aspect, 0.1f)));
            if (_rt != null && (_rt.width != width || _rt.height != height))
                ReleaseRt();

            if (_rt == null)
            {
                _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "MosaicGlassRT",
                    filterMode = FilterMode.Point
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

        static void ApplyUnlit(Transform root)
        {
            var mat = SpriteUnlit();
            if (mat == null || root == null)
                return;
            var all = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < all.Length; i++)
                all[i].sharedMaterial = mat;
        }

        static Material SpriteUnlit()
        {
            if (_spriteUnlit != null)
                return _spriteUnlit;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
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
