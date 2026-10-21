using System;
using Game.Core;
using Nixin.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Squash on press, OutBack recover, click sound + light haptic on click.</summary>
    [DisallowMultipleComponent]
    public sealed class HuntButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string sfx = NixinAudio.Click;
        public float pressedScale = 0.9f;
        Button _button;
        bool _down;

        void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            if (!string.IsNullOrEmpty(sfx))
                HuntAudio.Sfx(sfx);
            HuntHaptics.Light();
        }

        bool Interactable => _button == null || _button.IsInteractable();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable)
                return;
            _down = true;
            HuntTweener.Kill(transform);
            var from = transform.localScale;
            HuntTweener.Scale(transform, from, Vector3.one * pressedScale, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        void Release()
        {
            if (!_down)
                return;
            _down = false;
            HuntTweener.Kill(transform);
            HuntTweener.Scale(transform, transform.localScale, Vector3.one, 0.3f, Ease.OutBack);
        }

        void OnDisable()
        {
            _down = false;
            HuntTweener.Kill(transform);
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>Gentle breathing scale (Play button, current map node).</summary>
    public sealed class HuntIdlePulse : MonoBehaviour
    {
        public float amount = 0.05f;
        public float speed = 3.2f;
        float _phase;

        void OnEnable() => _phase = UnityEngine.Random.value * 6f;

        void Update()
        {
            var s = 1f + Mathf.Sin(Time.unscaledTime * speed + _phase) * amount;
            transform.localScale = new Vector3(s, s, 1f);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }

    /// <summary>Vertical bob for logos / arrows.</summary>
    public sealed class HuntFloatBob : MonoBehaviour
    {
        public float amplitude = 12f;
        public float speed = 2f;
        public float rotate;
        RectTransform _rect;
        Vector2 _base;
        bool _captured;

        void OnEnable()
        {
            _rect = transform as RectTransform;
            if (_rect != null && !_captured)
            {
                _base = _rect.anchoredPosition;
                _captured = true;
            }
        }

        void Update()
        {
            if (_rect == null)
                return;
            var t = Time.unscaledTime * speed;
            _rect.anchoredPosition = _base + new Vector2(0f, Mathf.Sin(t) * amplitude);
            if (rotate != 0f)
                _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * rotate);
        }
    }

    public sealed class HuntSpin : MonoBehaviour
    {
        public float degreesPerSecond = 30f;

        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }

    /// <summary>Rolling number label.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class HuntCounterText : MonoBehaviour
    {
        public string prefix = "";
        Text _text;
        int _shown;
        bool _init;

        public int Value { get; private set; }

        Text Label => _text != null ? _text : (_text = GetComponent<Text>());

        public void SetValue(int value, bool animate = true, float duration = 0.6f)
        {
            Value = value;
            if (!animate || !_init || !isActiveAndEnabled)
            {
                _init = true;
                _shown = value;
                Label.text = prefix + Format(value);
                return;
            }

            HuntTweener.Kill(this);
            var from = _shown;
            HuntTweener.To(this, duration, k =>
            {
                _shown = Mathf.RoundToInt(Mathf.Lerp(from, value, k));
                Label.text = prefix + Format(_shown);
            }, Ease.OutCubic);
        }

        public static string Format(int value)
        {
            if (value >= 100000)
                return (value / 1000) + "k";
            return value.ToString("#,0");
        }
    }

    /// <summary>Damped camera shake that restores the exact camera position.</summary>
    public sealed class HuntCameraShake : MonoBehaviour
    {
        Vector3 _base;
        float _amp;
        float _left;
        float _duration;

        public static void Shake(Camera camera, float amplitude = 0.08f, float duration = 0.25f)
        {
            if (camera == null)
                return;
            var shake = camera.GetComponent<HuntCameraShake>();
            if (shake == null)
                shake = camera.gameObject.AddComponent<HuntCameraShake>();
            if (shake._left <= 0f)
                shake._base = camera.transform.localPosition;
            shake._amp = Mathf.Max(shake._amp * (shake._left / Mathf.Max(0.01f, shake._duration)), amplitude);
            shake._duration = duration;
            shake._left = duration;
        }

        void LateUpdate()
        {
            if (_left <= 0f)
                return;
            _left -= Time.unscaledDeltaTime;
            if (_left <= 0f)
            {
                transform.localPosition = _base;
                _amp = 0f;
                return;
            }

            var k = _left / _duration;
            var off = new Vector3(
                (Mathf.PerlinNoise(Time.unscaledTime * 40f, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, Time.unscaledTime * 40f) - 0.5f) * 2f,
                0f) * (_amp * k);
            transform.localPosition = _base + off;
        }
    }
}
