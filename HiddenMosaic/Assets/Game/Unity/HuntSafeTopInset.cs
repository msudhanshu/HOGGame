using UnityEngine;

namespace Game.Unity
{
    /// <summary>Top safe-area (status bar / notch) helpers for this game's runtime-built UI.</summary>
    public static class HuntSafeArea
    {
        public static float TopInsetPixels
        {
            get
            {
                var safe = Screen.safeArea;
                var inset = Screen.height - safe.yMax;
                return inset > 0f ? inset : 0f;
            }
        }

        public static float TopInsetNormalized => Screen.height < 1 ? 0f : Mathf.Clamp01(TopInsetPixels / Screen.height);
    }

    /// <summary>
    /// Pushes a top-anchored element down by the status-bar inset so it stays tappable and visible.
    /// Position mode shifts anchoredPosition; stretch mode shifts offsetMax (for rects stretched to the top).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HuntSafeTopInset : MonoBehaviour
    {
        public bool stretch;

        RectTransform _rect;
        Vector2 _base;
        bool _captured;
        float _applied = -1f;

        void OnEnable() => Apply();

        void LateUpdate() => Apply();

        public static HuntSafeTopInset Ensure(Transform target, bool stretch = false)
        {
            if (target == null)
                return null;
            var c = target.GetComponent<HuntSafeTopInset>();
            if (c == null)
            {
                c = target.gameObject.AddComponent<HuntSafeTopInset>();
                c.stretch = stretch;
            }

            return c;
        }

        void Apply()
        {
            if (_rect == null)
                _rect = transform as RectTransform;
            if (_rect == null)
                return;

            if (!_captured)
            {
                _base = stretch ? _rect.offsetMax : _rect.anchoredPosition;
                _captured = true;
            }

            var canvas = GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
            var units = HuntSafeArea.TopInsetPixels / scale;
            if (Mathf.Approximately(units, _applied))
                return;
            _applied = units;

            var shifted = new Vector2(_base.x, _base.y - units);
            if (stretch)
                _rect.offsetMax = shifted;
            else
                _rect.anchoredPosition = shifted;
        }
    }
}
