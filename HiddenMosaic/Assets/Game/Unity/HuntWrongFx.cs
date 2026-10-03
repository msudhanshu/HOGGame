using System.Collections;
using Nixin.Mosaic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Red cross when the player picks an item that is not on the hunt list.</summary>
    public static class HuntWrongFx
    {
        public static void Play(MosaicItemView view, Camera worldCamera, Canvas overlayCanvas, MonoBehaviour runner)
        {
            if (view == null || runner == null || overlayCanvas == null || worldCamera == null)
                return;

            runner.StartCoroutine(ShowCross(overlayCanvas, worldCamera, view.transform.position));
        }

        static IEnumerator ShowCross(Canvas canvas, Camera worldCamera, Vector3 worldPos)
        {
            var root = canvas.transform as RectTransform;
            if (root == null)
                yield break;

            var go = new GameObject("WrongCross", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(root, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "✕";
            text.fontSize = 96;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.95f, 0.18f, 0.14f, 1f);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 120f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root,
                    worldCamera.WorldToScreenPoint(worldPos),
                    null,
                    out var local))
            {
                Object.Destroy(go);
                yield break;
            }

            rt.anchoredPosition = local;

            const float duration = 0.55f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = 1f + 0.22f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * pulse;
                text.color = new Color(text.color.r, text.color.g, text.color.b, 1f - t);
                yield return null;
            }

            Object.Destroy(go);
        }
    }
}
