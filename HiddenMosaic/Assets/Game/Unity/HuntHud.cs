using System.Collections;
using Game.Core;
using Nixin.Icons;
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
        HuntSevenSegmentClock _clock;
        Transform _iconRow;
        RectTransform _barRect;
        HuntDirector _director;
        IMosaicStampCatalog _stamps;

        public readonly UnityEvent NextRequested = new UnityEvent();
        public readonly UnityEvent RetryRequested = new UnityEvent();
        public readonly UnityEvent MenuRequested = new UnityEvent();
        public readonly UnityEvent HomeRequested = new UnityEvent();
        public readonly UnityEvent BackRequested = new UnityEvent();
        public readonly UnityEvent<HuntHintKind> HintRequested = new UnityEvent<HuntHintKind>();
        /// <summary>Dev builds only: cycles scanner styles for look comparison.</summary>
        public readonly UnityEvent LensRequested = new UnityEvent();
        /// <summary>"+" on the coins pill.</summary>
        public readonly UnityEvent ShopRequested = new UnityEvent();

        bool _skinned;
        bool _topLaidOut;
        HuntCurrencyPill _coinsPill;
        HuntHintDock _dock;
        HuntTargetStrip _targets;
        RectTransform _brackets;
        RectTransform _radarLayer;
        Coroutine _radar;

        /// <summary>Canvas units (of ReferenceHeight) the top row + target strip take from the play area.</summary>
        const float ArenaTopReserve = (136f + HuntTargetStrip.Height + 12f) / ReferenceHeight;

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
            LayoutTopRow();
            HuntSafeTopInset.Ensure(transform.Find("BackPanel"));
            HuntSafeTopInset.Ensure(transform.Find("TimerPanel"));
            var oldLens = transform.Find("Lens");
            if (oldLens != null)
                oldLens.gameObject.SetActive(false);
            EnsureClock();
            ApplySkin();
            EnsureArenaUi();
        }

        /// <summary>Timer chip moves to the top centre (pause left, coins right). Must run before
        /// HuntSafeTopInset captures the panel's base position.</summary>
        void LayoutTopRow()
        {
            if (_topLaidOut)
                return;
            var timer = transform.Find("TimerPanel") as RectTransform;
            if (timer == null)
                return;
            _topLaidOut = true;
            timer.anchorMin = timer.anchorMax = new Vector2(0.5f, 1f);
            timer.pivot = new Vector2(0.5f, 1f);
            timer.anchoredPosition = new Vector2(0f, -16f);
        }

        /// <summary>Target strip (top), hint dock (inside the bottom tray), corner brackets. Built once.</summary>
        void EnsureArenaUi()
        {
            if (_targets != null || _barRect == null)
                return;
            var rootRect = (RectTransform)transform;

            // The old tray texts / icon row and the old hint button are replaced by the new widgets.
            if (_title != null) _title.gameObject.SetActive(false);
            if (_status != null) _status.gameObject.SetActive(false);
            if (_iconRow != null) _iconRow.gameObject.SetActive(false);
            var legacyHint = transform.Find("Hint");
            if (legacyHint != null) legacyHint.gameObject.SetActive(false);

            var theme = HuntUiTheme.Current;
            var bar = _barRect.GetComponent<Image>();
            if (bar != null)
            {
                bar.sprite = HuntUiSprites.Gradient(HuntUiTheme.WithAlpha(theme.panelAlt, 1f), theme.deep);
                bar.color = Color.white;
            }

            _dock = HuntHintDock.Build(_barRect);
            _dock.Pressed += kind => HintRequested.Invoke(kind);

            _targets = HuntTargetStrip.Build(rootRect);
            HuntSafeTopInset.Ensure(_targets.transform);

            var bracketGo = HuntUiKit.Image(rootRect, "Brackets", HuntUiSprites.Brackets, HuntUiTheme.WithAlpha(theme.glow, 0.85f));
            bracketGo.type = Image.Type.Sliced;
            bracketGo.pixelsPerUnitMultiplier = 32f / 40f;
            _brackets = bracketGo.rectTransform;
            _brackets.SetAsFirstSibling();

            _radarLayer = HuntUiKit.Stretch(HuntUiKit.Rect("RadarLayer", rootRect));
            _radarLayer.SetSiblingIndex(1);
        }

        void EnsureClock()
        {
            if (_clock != null)
                return;
            var panel = transform.Find("TimerPanel") as RectTransform;
            if (panel == null)
                return;
            var existing = panel.GetComponentInChildren<HuntSevenSegmentClock>(true);
            _clock = existing != null ? existing : HuntSevenSegmentClock.Create(panel);
            panel.sizeDelta = new Vector2(HuntSevenSegmentClock.TotalWidth + 48f, 96f);
            if (_timer != null)
                _timer.enabled = false;
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

            var bar = MakePanel("TopBar", transform, new Color(0.07f, 0.08f, 0.1f, 1f));
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
            SetPlayControls(true);
            RefreshHints();
        }

        public void ApplyPlayViewport(Camera camera)
        {
            if (camera == null)
                return;

            EnsureReady();
            Canvas.ForceUpdateCanvases();

            var inset = MeasureBottomInsetNormalized();
            inset = Mathf.Clamp(inset + 0.008f, 0.08f, 0.42f);
            var top = TopReserve();
            camera.rect = new Rect(0f, inset, 1f, Mathf.Max(0.3f, 1f - inset - top));
            EnsureBackdropCamera(camera);
            FitBrackets(camera.rect);
            if (isActiveAndEnabled)
            {
                if (_relayout != null)
                    StopCoroutine(_relayout);
                _relayout = StartCoroutine(ReapplyNextFrame(camera));
            }
        }

        Camera _backdrop;
        Coroutine _relayout;

        // Pixels outside camera.rect are never cleared; a full-rect clear-only camera underneath keeps them solid.
        void EnsureBackdropCamera(Camera main)
        {
            if (_backdrop == null)
            {
                var go = new GameObject("BackdropCamera");
                go.transform.SetParent(transform, false);
                _backdrop = go.AddComponent<Camera>();
                _backdrop.clearFlags = CameraClearFlags.SolidColor;
                _backdrop.cullingMask = 0;
                _backdrop.rect = new Rect(0f, 0f, 1f, 1f);
                _backdrop.depth = -100f;
                _backdrop.useOcclusionCulling = false;
                if (go.TryGetComponent<AudioListener>(out var al))
                    Destroy(al);
            }

            var bg = HuntBackgrounds.Selected;
            _backdrop.backgroundColor = bg != null ? bg.Field : main.backgroundColor;
        }

        IEnumerator ReapplyNextFrame(Camera camera)
        {
            yield return null;
            _relayout = null;
            if (camera == null)
                yield break;
            var inset = Mathf.Clamp(MeasureBottomInsetNormalized() + 0.008f, 0.08f, 0.42f);
            if (Mathf.Abs(camera.rect.y - inset) > 0.001f)
            {
                var top = TopReserve();
                camera.rect = new Rect(0f, inset, 1f, Mathf.Max(0.3f, 1f - inset - top));
                FitBrackets(camera.rect);
            }
        }

        float TopReserve() => HuntSafeArea.TopInsetNormalized + (_targets != null && _targets.gameObject.activeSelf ? ArenaTopReserve : 0f);

        void FitBrackets(Rect cameraRect)
        {
            if (_brackets == null)
                return;
            _brackets.anchorMin = new Vector2(0f, cameraRect.y);
            _brackets.anchorMax = new Vector2(1f, cameraRect.yMax);
            _brackets.offsetMin = new Vector2(12f, 8f);
            _brackets.offsetMax = new Vector2(-12f, -8f);
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
                HideActionButtons();
                return;
            }

            _nextGo.SetActive(false);
            _status.text = string.IsNullOrEmpty(footnote) ? "Find these objects" : footnote;
            if (_targets != null)
            {
                var session = _director.Session;
                _targets.Set(session.Found, session.Remaining, _stamps,
                    "WAVE " + (_director.WaveIndex + 1) + "/" + _director.WaveCount);
            }
        }

        public void RefreshTimer(float remainingSeconds, bool urgent)
        {
            EnsureReady();
            var total = Mathf.Max(0f, remainingSeconds);
            var whole = Mathf.CeilToInt(total);
            var minutes = whole / 60;
            var seconds = whole % 60;
            if (_clock != null)
                _clock.SetTime(minutes, seconds, urgent ? new Color(1f, 0.32f, 0.26f) : new Color(1f, 0.97f, 0.4f));
            if (_timer == null)
                return;
            _timer.text = minutes + ":" + seconds.ToString("00");
            _timer.color = urgent ? new Color(1f, 0.45f, 0.35f) : Color.white;
        }

        /// <summary>Win / fail flow is handled by popups; the HUD only hides its play controls.</summary>
        public void ShowTimedOut()
        {
            EnsureReady();
            if (_status != null)
                _status.text = "Time's up";
            HideActionButtons();
            SetPlayControls(false);
        }

        /// <summary>Hint button visibility (hidden after the level ends, back on continue).</summary>
        public void SetPlayControls(bool visible)
        {
            if (_dock != null)
                _dock.gameObject.SetActive(visible);
            if (_targets != null)
                _targets.gameObject.SetActive(visible);
        }

        /// <summary>Reveal scene with no authored objects yet: explore with the scanner, then Done.</summary>
        public void ShowPreview(string title, string message)
        {
            EnsureReady();
            if (_title != null)
                _title.text = title;
            if (_status != null)
                _status.text = message;
            ClearIcons();
            HideActionButtons();
            SetPlayControls(false);
            if (_homeGo == null)
                return;
            var label = _homeGo.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = "Done";
            _homeGo.SetActive(true);
        }

        public void ShowLevelComplete()
        {
            EnsureReady();
            HideActionButtons();
            SetPlayControls(false);
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
            if (_targets != null)
                _targets.Set(null, null, _stamps, null);
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
        void OnLensClicked() => LensRequested.Invoke();

        /// <summary>Re-reads every hint's stock / coin price (wallet changed, level start).</summary>
        public void RefreshHints()
        {
            EnsureReady();
            if (_dock != null)
                _dock.Refresh();
        }

        /// <summary>Lights one dock slot while its effect runs; null = back to normal.</summary>
        public void SetActiveHint(HuntHintKind? kind)
        {
            if (_dock != null)
                _dock.SetActive(kind);
        }

        /// <summary>Temporarily covers the target strip with a coloured message (radar sweep ...).</summary>
        public void ShowBanner(string text, Color color)
        {
            if (_targets != null)
                _targets.ShowBanner(text, color);
        }

        public void HideBanner()
        {
            if (_targets != null)
                _targets.HideBanner();
        }

        /// <summary>Pulsing rings over world positions (radar hint). Follows the camera for <paramref name="seconds"/>.</summary>
        public void ShowRadar(System.Collections.Generic.IList<Vector3> worldPositions, Camera camera, float seconds)
        {
            if (_radarLayer == null || camera == null || worldPositions == null)
                return;
            ClearRadar();
            _radar = StartCoroutine(RadarRoutine(worldPositions, camera, seconds));
        }

        public void ClearRadar()
        {
            if (_radar != null)
                StopCoroutine(_radar);
            _radar = null;
            if (_radarLayer != null)
                for (var i = _radarLayer.childCount - 1; i >= 0; i--)
                    Destroy(_radarLayer.GetChild(i).gameObject);
        }

        IEnumerator RadarRoutine(System.Collections.Generic.IList<Vector3> world, Camera camera, float seconds)
        {
            var violet = HuntUiTheme.Current.violet;
            var markers = new RectTransform[world.Count];
            for (var i = 0; i < world.Count; i++)
            {
                var ring = HuntUiKit.Image(_radarLayer, "Ping", HuntUiSprites.Ring, violet);
                HuntUiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 150f));
                var dot = HuntUiKit.Image(ring.transform, "Dot", HuntUiSprites.Circle, HuntUiTheme.WithAlpha(violet, 0.35f));
                HuntUiKit.Stretch(dot.rectTransform, 40f);
                var pulse = ring.gameObject.AddComponent<HuntIdlePulse>();
                pulse.amount = 0.18f;
                pulse.speed = 7f;
                markers[i] = ring.rectTransform;
            }

            var end = Time.unscaledTime + seconds;
            var canvas = GetComponent<Canvas>();
            while (Time.unscaledTime < end && camera != null)
            {
                for (var i = 0; i < markers.Length; i++)
                {
                    if (markers[i] == null)
                        continue;
                    var screen = camera.WorldToScreenPoint(world[i]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_radarLayer, screen, null, out var local);
                    markers[i].anchoredPosition = local;
                    markers[i].gameObject.SetActive(screen.z > 0f);
                }

                yield return null;
            }

            ClearRadar();
        }

        /// <summary>Find tray pop: target icons bounce in when a wave is shown.</summary>
        public void PopIcons()
        {
            if (_targets != null)
                _targets.Pop();
        }

        public RectTransform CoinsPillRect => _coinsPill != null ? (RectTransform)_coinsPill.transform : null;

        /// <summary>Re-skins the (scene-authored or built) HUD with HuntUiTheme: gradient tray,
        /// round pause button, chunky hint button with count badge, coins pill.</summary>
        void ApplySkin()
        {
            if (_skinned || _barRect == null)
                return;
            _skinned = true;
            var theme = HuntUiTheme.Current;

            var barImage = _barRect.GetComponent<Image>();
            if (barImage != null)
            {
                barImage.sprite = HuntUiSprites.Gradient(HuntUiTheme.Darken(theme.primary, 0.15f), HuntUiTheme.Darken(theme.bgTop, 0.55f));
                barImage.color = Color.white;
            }

            if (_title != null)
            {
                _title.font = theme.Font;
                _title.color = Color.white;
                AddOutline(_title.gameObject);
            }

            if (_status != null)
            {
                _status.font = theme.Font;
                _status.color = theme.accent;
            }

            var timerPanel = transform.Find("TimerPanel");
            if (timerPanel != null)
                RoundPanel(timerPanel.GetComponent<Image>(), HuntUiTheme.WithAlpha(theme.deep, 0.9f));

            if (_backGo != null)
            {
                var backPanel = _backGo.transform.parent != transform ? _backGo.transform.parent : null;
                if (backPanel != null)
                {
                    var panelImage = backPanel.GetComponent<Image>();
                    if (panelImage != null)
                        panelImage.color = Color.clear;
                    var panelRect = (RectTransform)backPanel;
                    panelRect.sizeDelta = new Vector2(120f, 120f);
                }

                var backRect = (RectTransform)_backGo.transform;
                backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
                backRect.sizeDelta = new Vector2(120f, 120f);
                backRect.anchoredPosition = Vector2.zero;
                var img = _backGo.GetComponent<Image>();
                img.sprite = HuntUiSprites.Circle;
                img.type = Image.Type.Simple;
                img.color = theme.primary;
                var label = _backGo.transform.Find("Label");
                if (label != null)
                    label.gameObject.SetActive(false);
                var icon = HuntUiKit.Icon(_backGo.transform, NixinIcons.Load(NixinIcons.Pause), Color.white, new Vector2(64f, 64f));
                icon.raycastTarget = false;
                if (_backGo.GetComponent<HuntButtonJuice>() == null)
                    _backGo.AddComponent<HuntButtonJuice>();
            }

            _coinsPill = HuntCurrencyPill.Build(transform, HuntCurrencies.Coins, () => ShopRequested.Invoke(), 330f);
            var pillRect = (RectTransform)_coinsPill.transform;
            HuntUiKit.Place(pillRect, new Vector2(1f, 1f), new Vector2(330f, 92f), new Vector2(-10f, -22f), new Vector2(1f, 1f));
            HuntSafeTopInset.Ensure(pillRect);
        }

        static void RoundPanel(Image image, Color color)
        {
            if (image == null)
                return;
            image.sprite = HuntUiSprites.Rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = HuntUiKit.SpriteRadius / 28f;
            image.color = color;
        }

        static void AddOutline(GameObject go)
        {
            if (go.GetComponent<Outline>() != null)
                return;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.17f, 0.07f, 0.3f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
        }

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
