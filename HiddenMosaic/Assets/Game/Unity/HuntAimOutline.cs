using UnityEngine;

namespace Game.Unity
{
    /// <summary>Yellow hull behind a hunt stamp while it is aimed at.</summary>
    [DisallowMultipleComponent]
    public sealed class HuntAimOutline : MonoBehaviour
    {
        const float OutlineScale = 1.14f;
        const float AimScale = 1.22f;

        SpriteRenderer _source;
        SpriteRenderer _outline;
        Vector3 _baseScale;
        bool _active;

        void Ensure()
        {
            if (_source != null)
                return;

            _source = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            var child = new GameObject("AimOutline");
            child.transform.SetParent(transform, false);
            _outline = child.AddComponent<SpriteRenderer>();
            _outline.sprite = _source != null ? _source.sprite : null;
            _outline.sharedMaterial = _source != null ? _source.sharedMaterial : null;
            _outline.sortingLayerID = _source.sortingLayerID;
            _outline.sortingOrder = _source.sortingOrder - 2;
            _outline.color = new Color(1f, 0.88f, 0.12f, 0.98f);
            _outline.enabled = false;
        }

        public void SetAimed(bool aimed)
        {
            Ensure();
            if (_source == null || _outline == null)
                return;

            _outline.sprite = _source.sprite;
            if (_active == aimed)
                return;

            _active = aimed;
            _outline.enabled = aimed;
            if (aimed)
            {
                transform.localScale = _baseScale * AimScale;
                _outline.transform.localScale = Vector3.one * OutlineScale;
            }
            else
            {
                transform.localScale = _baseScale;
                _outline.transform.localScale = Vector3.one;
            }
        }

        public void CaptureBaseScale()
        {
            _baseScale = transform.localScale;
        }
    }
}
