using Game.Core;
using Nixin.Mosaic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Unity
{
    [DefaultExecutionOrder(-200)]
    public sealed class HuntHud : MonoBehaviour
    {
        public const float BarHeight = 300f;
        public const float IconSide = 128f;
        const float BarScreenFraction = 0.165f;
        const int TitleFontSize = 40;
        const int StatusFontSize = 30;
        const float ReferenceHeight = 1920f;
        const float IconRowTop = 0.62f;

        Text _title;
        Text _status;
        Text _timer;
        GameObject _nextGo;
        GameObject _retryGo;
        GameObject _menuGo;
        GameObject _homeGo;
        GameObject _backGo;
        Transform _iconRow;
        RectTransform _barRect;
        HuntDirector _director;
        IMosaicStampCatalog _stamps;

        public readonly UnityEvent NextRequested = new UnityEvent();
        public readonly UnityEvent RetryRequested = new UnityEvent();
        public readonly UnityEvent MenuRequested = new UnityEvent();
        public readonly UnityEvent HomeRequested = new UnityEvent();
        public readonly UnityEvent BackRequested = new UnityEvent();

        public float BottomInsetNormalized => BarHeight / ReferenceHeight;

        void Awake()
        {
            EnsureReady();
        }

        void LateUpdate()
        {
            ApplyScreenSizing();
            var cam = Camera.main;
            if (cam != null && _director != null)
                ApplyPlayViewport(cam);
        }

        /// <summary>Fix broken scene canvases and apply readable HUD sizing (call before play).</summary>
        public void EnsureReady()
        {
            EnsureCanvasReady();
            EnsureRefs();
            ApplyReadableLayout();
            ApplyScreenSizing();
        }

        public void Build()
        {
            EnsureCanvasReady();
            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, ReferenceHeight);
                scaler.matchWidthOrHeight = 1f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var bar = MakePanel("TopBar", transform, new Color(0.07f, 0.08f, 0.1f, 0.94f));
            _barRect = bar.GetComponent<RectTransform>();
            _barRect.anchorMin = new Vector2(0f, 0f);
            _barRect.anchorMax = new Vector2(1f, 0f);
            _barRect.pivot = new Vector2(0.5f, 0f);
            _barRect.anchoredPosition = Vector2.zero;
            _barRect.sizeDelta = new Vector2(0f, BarHeight);

            _title = MakeText("Title", bar.transform, TitleFontSize, FontStyle.Bold, TextAnchor.MiddleLeft);
            _status = MakeText("Status", bar.transform, StatusFontSize, FontStyle.Normal, TextAnchor.MiddleRight);
            _status.text = "Find these objects";

            var iconGo = new GameObject("IconRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            iconGo.transform.SetParent(bar.transform, false);
            _iconRow = iconGo.transform;
            ConfigureIconRow(iconGo.GetComponent<RectTransform>(), iconGo.GetComponent<HorizontalLayoutGroup>());

            _nextGo = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
            _nextGo.transform.SetParent(transform, false);
            var nextRect = _nextGo.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.5f, 0f);
            nextRect.anchorMax = new Vector2(0.5f, 0f);
            nextRect.pivot = new Vector2(0.5f, 0f);
            nextRect.anchoredPosition = new Vector2(0f, 28f);
            nextRect.sizeDelta = new Vector2(360f, 56f);
            _nextGo.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.28f, 0.95f);
            var nextText = MakeText("Label", _nextGo.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            nextText.text = "Next silhouette";
            nextText.rectTransform.offsetMin = Vector2.zero;
            nextText.rectTransform.offsetMax = Vector2.zero;
            WireNextButton();
            _nextGo.SetActive(false);

            var timerGo = MakePanel("TimerPanel", transform, new Color(0.07f, 0.08f, 0.1f, 0.88f));
            var timerRect = timerGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(1f, 1f);
            timerRect.anchorMax = new Vector2(1f, 1f);
            timerRect.pivot = new Vector2(1f, 1f);
            timerRect.anchoredPosition = new Vector2(-16f, -16f);
            timerRect.sizeDelta = new Vector2(200f, 56f);
            _timer = MakeText("Timer", timerGo.transform, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            _timer.text = "3:30";

            _backGo = MakeTopBarButton("Back", true);
            WireButton(_backGo, OnBackClicked);

            _retryGo = MakeActionButton("Retry", new Vector2(-120f, 28f), new Color(0.35f, 0.32f, 0.18f, 0.95f));
            _menuGo = MakeActionButton("Menu", new Vector2(120f, 28f), new Color(0.22f, 0.24f, 0.32f, 0.95f));
            _homeGo = MakeActionButton("Home", new Vector2(0f, 28f), new Color(0.22f, 0.24f, 0.32f, 0.95f));
            WireActionButtons();
            HideActionButtons();

            ApplyReadableLayout();
        }

        public void SetStampCatalog(IMosaicStampCatalog stamps)
        {
            _stamps = stamps;
        }

        public void Bind(HuntDirector director)
        {
            EnsureReady();
            _director = director;
            Refresh();
        }

        public void ApplyPlayViewport(Camera camera)
        {
            if (camera == null)
                return;

            EnsureReady();
            Canvas.ForceUpdateCanvases();

            var inset = MeasureBottomInsetNormalized();
            inset = Mathf.Clamp(inset + 0.008f, 0.08f, 0.42f);
            camera.rect = new Rect(0f, inset, 1f, 1f - inset);
        }

        public void FramePlayCamera(Camera camera)
        {
            ApplyPlayViewport(camera);
        }

        float MeasureBottomInsetNormalized()
        {
            if (_barRect == null || Screen.height < 1)
                return BottomInsetNormalized;

            var corners = new Vector3[4];
            _barRect.GetWorldCorners(corners);
            var barPx = Mathf.Abs(corners[1].y - corners[0].y);
            if (barPx < 1f)
                return BottomInsetNormalized;
            return Mathf.Clamp01(barPx / Screen.height);
        }

        public void Refresh(string footnote = null)
        {
            EnsureReady();
            if (_status == null || _director == null || _director.Current == null)
                return;

            var current = _director.Current;
            _title.text = current.Title + "  ·  " + (_director.WaveIndex + 1) + "/" + _director.WaveCount;

            if (_director.IsCampaignComplete)
            {
                _status.text = "Complete";
                ClearIcons();
                _nextGo.SetActive(false);
                return;
            }

            if (_director.IsLevelComplete)
            {
                _status.text = "Done";
                ClearIcons();
                _nextGo.SetActive(_director.HasNextLevel);
                _retryGo.SetActive(false);
                _menuGo.SetActive(false);
                return;
            }

            _nextGo.SetActive(false);
            _status.text = string.IsNullOrEmpty(footnote) ? "Find these objects" : footnote;
            RebuildIcons(_director.Session.Remaining);
        }

        void RebuildIcons(System.Collections.Generic.IReadOnlyList<string> names)
        {
            ClearIcons();
            if (_iconRow == null || names == null)
                return;

            for (var i = 0; i < names.Count; i++)
            {
                var go = new GameObject("Target_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_iconRow, false);
                var image = go.GetComponent<Image>();
                image.preserveAspect = true;
                image.color = Color.white;
                image.raycastTarget = false;
                if (_stamps != null)
                {
                    var sprite = _stamps.GetStamp(names[i]);
                    if (sprite != null)
                        image.sprite = sprite;
                }

                var rect = go.GetComponent<RectTransform>();
                var iconPx = IconSide;
                if (_barRect != null && Screen.height > 100)
                    iconPx = Mathf.Max(IconSide, Mathf.Max(BarHeight, Screen.height * BarScreenFraction) * 0.4f);
                rect.sizeDelta = new Vector2(iconPx, iconPx);
            }
        }

        public void RefreshTimer(float remainingSeconds, bool urgent)
        {
            EnsureReady();
            if (_timer == null)
                return;
            var total = Mathf.Max(0f, remainingSeconds);
            var minutes = Mathf.FloorToInt(total / 60f);
            var seconds = Mathf.FloorToInt(total % 60f);
            _timer.text = minutes + ":" + seconds.ToString("00");
            _timer.color = urgent ? new Color(1f, 0.45f, 0.35f) : Color.white;
        }

        public void ShowTimedOut()
        {
            EnsureReady();
            if (_status != null)
                _status.text = "Time's up";
            ClearIcons();
            _nextGo.SetActive(false);
            _retryGo.SetActive(true);
            _menuGo.SetActive(true);
            _homeGo.SetActive(false);
        }

        public void ShowCampaignComplete()
        {
            EnsureReady();
            _nextGo.SetActive(false);
            _retryGo.SetActive(false);
            _menuGo.SetActive(false);
            _homeGo.SetActive(true);
        }

        public void ShowLevelCompleteSingle()
        {
            EnsureReady();
            _nextGo.SetActive(false);
            _retryGo.SetActive(false);
            _menuGo.SetActive(false);
            _homeGo.SetActive(true);
        }

        public void HideActionButtons()
        {
            if (_nextGo != null)
                _nextGo.SetActive(false);
            if (_retryGo != null)
                _retryGo.SetActive(false);
            if (_menuGo != null)
                _menuGo.SetActive(false);
            if (_homeGo != null)
                _homeGo.SetActive(false);
        }

        void ClearIcons()
        {
            if (_iconRow == null)
                return;
            for (var i = _iconRow.childCount - 1; i >= 0; i--)
                Destroy(_iconRow.GetChild(i).gameObject);
        }

        void EnsureCanvasReady()
        {
            var root = transform as RectTransform;
            if (root != null)
            {
                if (root.localScale.sqrMagnitude < 0.01f)
                    root.localScale = Vector3.one;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.pivot = new Vector2(0.5f, 0.5f);
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
                root.anchoredPosition = Vector2.zero;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, ReferenceHeight);
                scaler.matchWidthOrHeight = 1f;
            }
        }

        void ApplyScreenSizing()
        {
            if (_barRect == null || Screen.height < 100)
                return;

            var barPx = Mathf.Max(BarHeight, Screen.height * BarScreenFraction);
            _barRect.sizeDelta = new Vector2(0f, barPx);

            if (_nextGo != null)
            {
                var nextRect = _nextGo.GetComponent<RectTransform>();
                if (nextRect != null)
                    nextRect.anchoredPosition = new Vector2(0f, barPx + 36f);
            }

            var iconPx = Mathf.Max(IconSide, barPx * 0.4f);
            if (_iconRow != null)
            {
                for (var i = 0; i < _iconRow.childCount; i++)
                {
                    var child = _iconRow.GetChild(i) as RectTransform;
                    if (child != null)
                        child.sizeDelta = new Vector2(iconPx, iconPx);
                }
            }
        }

        void ApplyReadableLayout()
        {
            if (_barRect == null)
            {
                var bar = transform.Find("TopBar");
                if (bar != null)
                    _barRect = bar.GetComponent<RectTransform>();
            }

            if (_barRect != null)
            {
                _barRect.sizeDelta = new Vector2(0f, BarHeight);
                _barRect.anchorMin = new Vector2(0f, 0f);
                _barRect.anchorMax = new Vector2(1f, 0f);
                _barRect.pivot = new Vector2(0.5f, 0f);
                _barRect.anchoredPosition = Vector2.zero;
            }

            if (_title != null)
            {
                _title.fontSize = TitleFontSize;
                _title.fontStyle = FontStyle.Bold;
                _title.rectTransform.anchorMin = new Vector2(0f, 0f);
                _title.rectTransform.anchorMax = new Vector2(0.55f, 1f - IconRowTop);
                _title.rectTransform.offsetMin = new Vector2(24f, 8f);
                _title.rectTransform.offsetMax = new Vector2(-8f, 0f);
                _title.raycastTarget = false;
            }

            if (_status != null)
            {
                _status.fontSize = StatusFontSize;
                _status.rectTransform.anchorMin = new Vector2(0.55f, 0f);
                _status.rectTransform.anchorMax = new Vector2(1f, 1f - IconRowTop);
                _status.rectTransform.offsetMin = new Vector2(8f, 8f);
                _status.rectTransform.offsetMax = new Vector2(-24f, 0f);
                _status.alignment = TextAnchor.MiddleRight;
                _status.raycastTarget = false;
            }

            if (_iconRow != null)
            {
                var iconRect = _iconRow as RectTransform;
                if (iconRect == null)
                    iconRect = _iconRow.GetComponent<RectTransform>();
                var layout = _iconRow.GetComponent<HorizontalLayoutGroup>();
                if (iconRect != null && layout != null)
                    ConfigureIconRow(iconRect, layout);
            }
        }

        static void ConfigureIconRow(RectTransform iconRect, HorizontalLayoutGroup layout)
        {
            iconRect.anchorMin = new Vector2(0f, 1f - IconRowTop);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.offsetMin = new Vector2(16f, 4f);
            iconRect.offsetMax = new Vector2(-16f, -10f);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 20f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
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

            if (_barRect == null)
            {
                var bar = transform.Find("TopBar");
                if (bar != null)
                    _barRect = bar.GetComponent<RectTransform>();
            }

            if (_iconRow == null)
            {
                var t = transform.Find("TopBar/IconRow");
                if (t != null)
                    _iconRow = t;
                else
                    _iconRow = EnsureIconRow();
            }

            if (_nextGo == null)
            {
                var t = transform.Find("Next");
                if (t != null)
                    _nextGo = t.gameObject;
            }

            if (_timer == null)
            {
                var t = transform.Find("TimerPanel/Timer");
                if (t != null)
                    _timer = t.GetComponent<Text>();
            }

            if (_retryGo == null)
            {
                var t = transform.Find("Retry");
                if (t != null)
                    _retryGo = t.gameObject;
            }

            if (_menuGo == null)
            {
                var t = transform.Find("Menu");
                if (t != null)
                    _menuGo = t.gameObject;
            }

            if (_homeGo == null)
            {
                var t = transform.Find("Home");
                if (t != null)
                    _homeGo = t.gameObject;
            }

            if (_backGo == null)
            {
                var t = transform.Find("BackPanel/Back");
                if (t == null)
                    t = transform.Find("Back");
                if (t != null)
                    _backGo = t.gameObject;
                else
                    _backGo = MakeTopBarButton("Back", true);
            }

            WireNextButton();
            WireActionButtons();
            WireButton(_backGo, OnBackClicked);
            if (_backGo != null)
                _backGo.SetActive(true);
        }

        Transform EnsureIconRow()
        {
            var bar = transform.Find("TopBar");
            if (bar == null)
                return null;

            var iconGo = new GameObject("IconRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            iconGo.transform.SetParent(bar, false);
            ConfigureIconRow(iconGo.GetComponent<RectTransform>(), iconGo.GetComponent<HorizontalLayoutGroup>());
            return iconGo.transform;
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

        void WireActionButtons()
        {
            WireButton(_retryGo, OnRetryClicked);
            WireButton(_menuGo, OnMenuClicked);
            WireButton(_homeGo, OnHomeClicked);
        }

        static void WireButton(GameObject go, UnityEngine.Events.UnityAction handler)
        {
            if (go == null)
                return;
            var button = go.GetComponent<Button>();
            if (button == null)
                return;
            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }

        void OnRetryClicked() => RetryRequested.Invoke();
        void OnMenuClicked() => MenuRequested.Invoke();
        void OnHomeClicked() => HomeRequested.Invoke();
        void OnBackClicked() => BackRequested.Invoke();

        GameObject MakeTopBarButton(string label, bool topLeft)
        {
            var panel = MakePanel(label + "Panel", transform, new Color(0.07f, 0.08f, 0.1f, 0.88f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(topLeft ? 0f : 1f, 1f);
            panelRect.anchorMax = new Vector2(topLeft ? 0f : 1f, 1f);
            panelRect.pivot = new Vector2(topLeft ? 0f : 1f, 1f);
            panelRect.anchoredPosition = new Vector2(topLeft ? 16f : -16f, -16f);
            panelRect.sizeDelta = new Vector2(160f, 56f);

            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.24f, 0.32f, 0.95f);
            var text = MakeText("Label", go.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            go.GetComponent<Button>().targetGraphic = image;
            return go;
        }

        GameObject MakeActionButton(string label, Vector2 anchoredPosition, Color color)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 56f);
            go.GetComponent<Image>().color = color;
            var text = MakeText("Label", go.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            go.SetActive(false);
            return go;
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
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
