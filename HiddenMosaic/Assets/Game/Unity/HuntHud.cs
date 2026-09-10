using Game.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Unity
{
    public sealed class HuntHud : MonoBehaviour
    {
        Text _title;
        Text _status;
        GameObject _nextGo;
        HuntDirector _director;

        public readonly UnityEvent NextRequested = new UnityEvent();

        void Awake()
        {
            EnsureRefs();
        }

        public void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            gameObject.AddComponent<GraphicRaycaster>();

            var bar = MakePanel("TopBar", transform, new Color(0.07f, 0.08f, 0.1f, 0.92f));
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 140f);

            _title = MakeText("Title", bar.transform, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            _title.rectTransform.offsetMin = new Vector2(24f, 78f);
            _title.rectTransform.offsetMax = new Vector2(-24f, -12f);
            _title.color = new Color(1f, 0.92f, 0.7f);

            _status = MakeText("Status", bar.transform, 26, FontStyle.Normal, TextAnchor.UpperLeft);
            _status.rectTransform.offsetMin = new Vector2(24f, 12f);
            _status.rectTransform.offsetMax = new Vector2(-24f, -64f);
            _status.text = "Find: …";

            _nextGo = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
            _nextGo.transform.SetParent(transform, false);
            var nextRect = _nextGo.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.5f, 0f);
            nextRect.anchorMax = new Vector2(0.5f, 0f);
            nextRect.pivot = new Vector2(0.5f, 0f);
            nextRect.anchoredPosition = new Vector2(0f, 36f);
            nextRect.sizeDelta = new Vector2(420f, 72f);
            _nextGo.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.28f, 0.95f);
            var nextText = MakeText("Label", _nextGo.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            nextText.text = "Next silhouette";
            nextText.rectTransform.offsetMin = Vector2.zero;
            nextText.rectTransform.offsetMax = Vector2.zero;
            WireNextButton();
            _nextGo.SetActive(false);
        }

        public void Bind(HuntDirector director)
        {
            EnsureRefs();
            _director = director;
            Refresh();
        }

        public void Refresh(string footnote = null)
        {
            EnsureRefs();
            if (_status == null || _director == null || _director.Current == null)
                return;

            var current = _director.Current;
            _title.text = current.Title + "   ·   wave " + (_director.WaveIndex + 1) + "/" + _director.WaveCount;

            if (_director.IsCampaignComplete)
            {
                _status.text = "Campaign complete. Every silhouette is yours.";
                _nextGo.SetActive(false);
                return;
            }

            if (_director.IsLevelComplete)
            {
                _status.text = "Silhouette complete.";
                _nextGo.SetActive(_director.HasNextLevel);
                return;
            }

            _nextGo.SetActive(false);
            var session = _director.Session;
            _status.text = "Move to magnify · click to collect:  " + string.Join("   ", session.Remaining);
            if (!string.IsNullOrEmpty(footnote))
                _status.text += "\n" + footnote;
        }

        void EnsureRefs()
        {
            if (_title == null)
            {
                var t = transform.Find("TopBar/Title");
                if (t != null)
                    _title = t.GetComponent<Text>();
            }

            if (_status == null)
            {
                var t = transform.Find("TopBar/Status");
                if (t != null)
                    _status = t.GetComponent<Text>();
            }

            if (_nextGo == null)
            {
                var t = transform.Find("Next");
                if (t != null)
                    _nextGo = t.gameObject;
            }

            WireNextButton();
        }

        void WireNextButton()
        {
            if (_nextGo == null)
                return;
            var button = _nextGo.GetComponent<Button>();
            if (button == null)
                return;
            button.onClick.RemoveListener(OnNextClicked);
            button.onClick.AddListener(OnNextClicked);
        }

        void OnNextClicked()
        {
            NextRequested.Invoke();
        }

        static GameObject MakePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static Text MakeText(string name, Transform parent, int size, FontStyle style, TextAnchor align)
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
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
