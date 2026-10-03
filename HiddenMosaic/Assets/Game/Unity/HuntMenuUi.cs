using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Unity
{
    public static class HuntMenuUi
    {
        public const float ReferenceHeight = 1920f;

        public static void EnsureCanvas(GameObject root)
        {
            var rect = root.transform as RectTransform;
            if (rect == null)
                rect = root.GetComponent<RectTransform>();
            if (rect == null)
                rect = root.AddComponent<RectTransform>();

            if (rect.localScale.sqrMagnitude < 0.01f)
                rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            if (root.GetComponent<Canvas>() == null)
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (root.GetComponent<CanvasScaler>() == null)
            {
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, ReferenceHeight);
                scaler.matchWidthOrHeight = 1f;
            }

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();
        }

        public static GameObject MakePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        public static Text MakeText(string name, Transform parent, int size, FontStyle style, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button MakeButton(Transform parent, string label, Vector2 size, Color color)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            var text = MakeText("Label", go.transform, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        public static void WireClick(Transform parent, string childName, UnityAction handler)
        {
            if (parent == null || handler == null)
                return;
            var t = parent.Find(childName);
            if (t == null)
                return;
            var button = t.GetComponent<Button>();
            if (button == null)
                return;
            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }
    }
}
