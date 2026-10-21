using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Game.Unity
{
    /// <summary>One-shot UI effects: confetti, coin fly, toast/combo text, star burst.</summary>
    public static class HuntFx
    {
        /// <summary>Confetti burst from a local point in parent.</summary>
        public static void Confetti(RectTransform parent, Vector2 localOrigin, int count = 60)
        {
            if (parent == null)
                return;
            var theme = HuntUiTheme.Current;
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = Random.value < 0.3f ? HuntUiSprites.Circle : HuntUiSprites.White;
                img.raycastTarget = false;
                var c = theme.confetti != null && theme.confetti.Length > 0
                    ? theme.confetti[Random.Range(0, theme.confetti.Length)]
                    : Color.yellow;
                img.color = c;
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(Random.Range(14f, 28f), Random.Range(8f, 16f));
                rt.anchoredPosition = localOrigin;

                var angle = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                var speed = Random.Range(900f, 1900f);
                var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                var spin = Random.Range(-720f, 720f);
                var life = Random.Range(1.3f, 2f);
                const float gravity = -2600f;
                var start = localOrigin;
                HuntTweener.To(go, life, k =>
                {
                    var t = k * life;
                    rt.anchoredPosition = start + vel * t + new Vector2(0f, 0.5f * gravity * t * t);
                    rt.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                    var flip = Mathf.Abs(Mathf.Cos(t * 9f));
                    rt.localScale = new Vector3(1f, 0.3f + 0.7f * flip, 1f);
                    img.color = new Color(c.r, c.g, c.b, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                }, Ease.Linear, Random.Range(0f, 0.12f), () => UnityEngine.Object.Destroy(go));
            }
        }

        /// <summary>Coins (or any sprite) arc from a screen point into target, calling onEach per arrival.</summary>
        public static void FlyIcons(RectTransform layer, Vector2 fromScreen, RectTransform target, int count, Sprite sprite,
            Color color, Action onEach, Action onDone)
        {
            if (layer == null || target == null || count <= 0)
            {
                onDone?.Invoke();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, fromScreen, null, out var from);
            var targetScreen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, targetScreen, null, out var to);
            count = Mathf.Clamp(count, 1, 12);
            var arrived = 0;
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(layer, false);
                var img = go.GetComponent<Image>();
                img.sprite = sprite != null ? sprite : HuntUiSprites.Circle;
                img.color = color;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(72f, 72f);
                var spread = Random.insideUnitCircle * 90f;
                var start = from + spread;
                rt.anchoredPosition = start;
                rt.localScale = Vector3.zero;
                var ctrl = Vector2.Lerp(start, to, 0.5f) + new Vector2(Random.Range(-260f, 260f), Random.Range(160f, 380f));
                var delay = i * 0.06f;
                HuntTweener.Scale(rt, Vector3.zero, Vector3.one, 0.18f, Ease.OutBack, delay);
                HuntTweener.To(go, 0.6f, k =>
                {
                    var a = Vector2.Lerp(start, ctrl, k);
                    var b = Vector2.Lerp(ctrl, to, k);
                    rt.anchoredPosition = Vector2.Lerp(a, b, k);
                    rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
                }, Ease.InQuad, delay + 0.2f, () =>
                {
                    UnityEngine.Object.Destroy(go);
                    arrived++;
                    onEach?.Invoke();
                    HuntTweener.Punch(target, 0.18f, 0.3f);
                    if (arrived == count)
                        onDone?.Invoke();
                });
            }
        }

        /// <summary>Big pop-in text that rises and fades ("Great!", "+40").</summary>
        public static void PopText(RectTransform parent, Vector2 local, string message, Color color, int size = 84, float rise = 140f)
        {
            if (parent == null)
                return;
            var text = HuntUiKit.Label(parent, message, size, color, TextAnchor.MiddleCenter, outline: true);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(800f, 140f);
            rt.anchoredPosition = local;
            HuntTweener.Scale(rt, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack);
            HuntTweener.To(text.gameObject, 0.8f, k =>
            {
                rt.anchoredPosition = local + new Vector2(0f, rise * k);
                text.color = new Color(color.r, color.g, color.b, 1f - k);
            }, Ease.InQuad, 0.45f, () => UnityEngine.Object.Destroy(text.gameObject));
        }

        /// <summary>Radiating star sparks around a point (find / star earned).</summary>
        public static void Sparkle(RectTransform parent, Vector2 local, Color color, int count = 8, float radius = 140f)
        {
            if (parent == null)
                return;
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = HuntUiSprites.Star;
                img.color = color;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(34f, 34f);
                var ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var dist = radius * Random.Range(0.7f, 1.2f);
                HuntTweener.To(go, 0.55f, k =>
                {
                    rt.anchoredPosition = local + dir * dist * k;
                    rt.localScale = Vector3.one * (1f - k) * 1.3f;
                    rt.localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                }, Ease.OutCubic, 0f, () => UnityEngine.Object.Destroy(go));
            }
        }
    }
}
