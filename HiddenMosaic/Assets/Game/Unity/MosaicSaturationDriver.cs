using System.Collections.Generic;
using Nixin.Mosaic;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Board stays grayscale; each pixel only shows color when under the magnifier circle.</summary>
    [DefaultExecutionOrder(-48)]
    public sealed class MosaicSaturationDriver : MonoBehaviour
    {
        static readonly int RevealAmountId = Shader.PropertyToID("_RevealAmount");
        static readonly int MosaicGlassId = Shader.PropertyToID("_MosaicGlass");

        [SerializeField] float revealEdgeSoftness = 0.08f;

        MosaicGlass _glass;
        Transform _levelRoot;
        readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>(256);
        MaterialPropertyBlock _block;

        public MosaicItemView AimTarget { get; set; }

        public void Bind(Transform levelRoot, MosaicGlass glass)
        {
            _levelRoot = levelRoot;
            _glass = glass;
            RefreshRenderers();
            ApplyGlobals(Vector3.zero, 0.001f);
            ClearAimBoosts();
        }

        public void RefreshRenderers()
        {
            _renderers.Clear();
            if (_levelRoot == null)
                return;
            var all = _levelRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null || r.GetComponent<MosaicItemView>() == null)
                    continue;
                _renderers.Add(r);
            }
        }

        void LateUpdate()
        {
            if (_glass == null)
                return;

            var center = _glass.GlassWorld;
            var radius = Mathf.Max(0.001f, _glass.GlassWorldRadius);
            var soft = Mathf.Max(0.002f, radius * Mathf.Clamp(revealEdgeSoftness, 0.02f, 0.2f));
            ApplyGlobals(center, radius, soft);
            ApplyAimBoosts();
        }

        void ApplyGlobals(Vector3 center, float radius, float edgeSoftness = 0.02f)
        {
            Shader.SetGlobalVector(MosaicGlassId, new Vector4(center.x, center.y, radius, edgeSoftness));
        }

        void ApplyAimBoosts()
        {
            if (_block == null)
                _block = new MaterialPropertyBlock();

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;

                var view = renderer.GetComponent<MosaicItemView>();
                var boost = view != null && view == AimTarget ? 1f : 0f;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(RevealAmountId, boost);
                renderer.SetPropertyBlock(_block);
            }
        }

        void ClearAimBoosts()
        {
            if (_block == null)
                _block = new MaterialPropertyBlock();
            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(RevealAmountId, 0f);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
