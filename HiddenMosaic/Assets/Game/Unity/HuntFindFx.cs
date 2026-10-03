using System.Collections;
using Nixin.Mosaic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Score popup and lift-off when a hunt target is found.</summary>
    public static class HuntFindFx
    {
        public const int PointsPerFind = 4;

        public static void Play(MosaicItemView view, Camera worldCamera, Canvas overlayCanvas, MonoBehaviour runner)
        {
            if (view == null || runner == null)
                return;

            var transform = view.transform;
            var renderer = view.GetComponent<SpriteRenderer>();
            var collider = view.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            runner.StartCoroutine(FloatItemAway(transform, renderer));
            if (overlayCanvas != null && worldCamera != null)
                runner.StartCoroutine(FloatScoreText(overlayCanvas, worldCamera, transform.position, PointsPerFind));
        }

        static IEnumerator FloatItemAway(Transform item, SpriteRenderer renderer)
        {
            if (item == null)
                yield break;

            var startPos = item.position;
            var lift = 0.55f;
            if (renderer != null)
                lift = Mathf.Max(0.35f, renderer.bounds.size.y * 1.15f);
            var endPos = startPos + Vector3.up * lift;

            var startScale = item.localScale;
            var endScale = startScale * 1.38f;
            Color baseColor = renderer != null ? renderer.color : Color.white;

            const float duration = 0.72f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (item == null)
                    yield break;
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                item.position = Vector3.Lerp(startPos, endPos, eased);
                item.localScale = Vector3.Lerp(startScale, endScale, eased);
                if (renderer != null)
                {
                    var alpha = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0.35f, 1f, t));
                    renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }

                yield return null;
            }

            if (item != null)
                item.gameObject.SetActive(false);
        }

        static IEnumerator FloatScoreText(Canvas canvas, Camera worldCamera, Vector3 worldPos, int points)
        {
            var root = canvas.transform as RectTransform;
            if (root == null)
                yield break;

            var go = new GameObject("ScorePop", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "+" + points;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var startColor = new Color(1f, 0.93f, 0.38f, 1f);
            text.color = startColor;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 80f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root,
                    worldCamera.WorldToScreenPoint(worldPos),
                    null,
                    out var startLocal))
            {
                Object.Destroy(go);
                yield break;
            }

            rt.anchoredPosition = startLocal;

            const float duration = 0.9f;
            const float risePx = 140f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = startLocal + new Vector2(0f, risePx * eased);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, Mathf.Sin(t * Mathf.PI));
                text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                yield return null;
            }

            Object.Destroy(go);
        }
    }
}
