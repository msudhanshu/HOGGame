using Game.Core;
using Nixin.CrossPromo;
using Nixin.Game.Core;
using Nixin.Icons;
using Nixin.Monetization;
using Nixin.StoreEngagement;
using Nixin.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>Home + level map, code-built and themed (HuntUiTheme). Popups (shop, settings,
    /// chapter, coming soon) open through HuntPopups over this canvas.</summary>
    public sealed class HuntHomeScreen : MonoBehaviour
    {
        const string HomePanelName = "HomePanel";
        const string MapPanelName = "MapPanel";

        /// <summary>Canvas pixels held for the LevelPlay bottom banner (about 50dp on a phone).</summary>
        const float BannerReserve = 168f;

        [SerializeField] HuntCatalogAsset catalogAsset;

        static HuntHomeScreen _instance;

        RectTransform _home;
        RectTransform _map;
        CanvasGroup _homeGroup;
        CanvasGroup _mapGroup;
        HuntMapScreen _mapScreen;
        RectTransform _aboveBanner;
        Text _playSub;
        RectTransform _gamesTile;
        GameObject _gamesDot;
        Image _preview;
        bool _mapOpen;

        void OnEnable()
        {
            GameServices.Entitlements.OnEntitlementChanged += OnEntitlementChanged;
            HuntBackgrounds.Changed += ApplyBackground;
            NixinCrossPromo.Changed += RefreshGamesTile;
        }

        void OnDisable()
        {
            GameServices.Entitlements.OnEntitlementChanged -= OnEntitlementChanged;
            HuntBackgrounds.Changed -= ApplyBackground;
            NixinCrossPromo.Changed -= RefreshGamesTile;
        }

        void Awake()
        {
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            Monetization.HideBanner();
        }

        void Start()
        {
            HuntUiInput.EnsureEventSystem();
            HuntMenuUi.EnsureCanvas(gameObject);
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.matchWidthOrHeight = 0f;

            HuntRoadmapService.Use(catalogAsset);
            HuntWallet.EnsureStarterGrant();
            HuntCrossPromoHost.EnsureCreated();
            NixinAppUpdate.Init();
            NixinRateUs.Init();
            HuntAudio.EnsureMusic();

            HuntPopups.CloseAll();
            HuntPopups.Navigator.UnhandledBack = OnBack;

            Build();
            ApplyHomeBanner();
            RefreshHome();
            PlayIntro();
            GameServices.Analytics.TrackScreenView("HuntHomeScreen");
            ConsumePendingResult();
        }

        Image _bg;

        void ApplyBackground()
        {
            if (_bg == null)
                return;
            var theme = HuntUiTheme.Current;
            var def = HuntBackgrounds.Selected;
            _bg.sprite = HuntUiSprites.Gradient(def != null ? def.Top : theme.bgTop, def != null ? def.Bottom : theme.bgBottom);
        }

        public void Build()
        {
            HuntMenuUi.EnsureCanvas(gameObject);
            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var theme = HuntUiTheme.Current;
            _bg = HuntUiKit.GradientBg(transform, theme.bgTop, theme.bgBottom);
            ApplyBackground();
            HuntUiKit.FloatingTiles(transform);

            var safe = HuntUiKit.Stretch(HuntUiKit.Rect("SafeArea", transform));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            _aboveBanner = HuntUiKit.Stretch(HuntUiKit.Rect("AboveBanner", safe));

            _home = HuntUiKit.Stretch(HuntUiKit.Rect(HomePanelName, _aboveBanner));
            _homeGroup = HuntUiKit.Group(_home.gameObject);
            BuildHome(_home);

            _map = HuntUiKit.Stretch(HuntUiKit.Rect(MapPanelName, _aboveBanner));
            _mapGroup = HuntUiKit.Group(_map.gameObject);
            _mapScreen = _map.gameObject.AddComponent<HuntMapScreen>();
            _mapScreen.Build(_map, OnNodePicked, CloseMap);
            _map.gameObject.SetActive(false);
        }

        void BuildHome(RectTransform panel)
        {
            var theme = HuntUiTheme.Current;

            var top = HuntUiKit.Rect("TopBar", panel);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 170f);
            var coins = HuntCurrencyPill.Build(top, HuntCurrencies.Coins, () => HuntPopups.Shop("home_coins"));
            HuntUiKit.Place((RectTransform)coins.transform, new Vector2(0f, 0.5f), new Vector2(300f, 92f), new Vector2(190f, 0f));
            var hints = HuntCurrencyPill.Build(top, HuntCurrencies.Hints, () => HuntPopups.Shop("home_hints"), 260f);
            HuntUiKit.Place((RectTransform)hints.transform, new Vector2(0f, 0.5f), new Vector2(260f, 92f), new Vector2(500f, 0f));
            var settings = HuntUiKit.IconButton(top, NixinIcons.Load(NixinIcons.Settings), theme.primary, 110f,
                () => HuntPopups.Settings("home"));
            HuntUiKit.Place(settings.Root, new Vector2(1f, 0.5f), new Vector2(110f, 110f), new Vector2(-90f, 0f));

            // Logo
            var logo = HuntUiKit.Rect("Logo", panel);
            HuntUiKit.Place(logo, new Vector2(0.5f, 0.78f), new Vector2(900f, 380f));
            if (theme.logo != null)
            {
                var art = HuntUiKit.Image(logo, "Art", theme.logo, Color.white);
                art.preserveAspect = true;
                HuntUiKit.Stretch(art.rectTransform);
            }
            else
            {
                var hidden = HuntUiKit.Label(logo, "HIDDEN", 150, Color.white, TextAnchor.MiddleCenter, true);
                hidden.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                var mosaic = HuntUiKit.Label(logo, "MOSAIC", 170, theme.accent, TextAnchor.MiddleCenter, true);
                mosaic.rectTransform.anchorMax = new Vector2(1f, 0.55f);
            }

            logo.gameObject.AddComponent<HuntFloatBob>().amplitude = 10f;

            // Current-level medallion = the play button, framed by a swaying magnifying glass.
            var medal = HuntUiKit.Rect("Medal", panel);
            HuntUiKit.Place(medal, new Vector2(0.5f, 0.5f), new Vector2(420f, 420f), new Vector2(0f, 40f));
            var hit = HuntUiKit.Image(medal, "Hit", null, Color.clear, true);
            HuntUiKit.Stretch(hit.rectTransform, -40f);

            var lens = HuntUiKit.Rect("Lens", medal);
            HuntUiKit.Stretch(lens);
            var arm = HuntUiKit.Rect("HandleArm", lens);
            HuntUiKit.Place(arm, new Vector2(0.5f, 0.5f), Vector2.zero);
            arm.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var handle = HuntUiKit.Rounded(arm, "Handle", HuntUiTheme.Darken(theme.hint, 0.3f), 36f);
            HuntUiKit.Place(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(72f, 180f), new Vector2(0f, -240f), new Vector2(0.5f, 1f));
            var collar = HuntUiKit.Rounded(arm, "Collar", HuntUiTheme.Darken(theme.hint, 0.15f), 20f);
            HuntUiKit.Place(collar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(96f, 48f), new Vector2(0f, -236f), new Vector2(0.5f, 1f));
            var rim = HuntUiKit.Image(lens, "Rim", HuntUiSprites.Ring, theme.hint);
            HuntUiKit.Stretch(rim.rectTransform, -50f);
            var sway = lens.gameObject.AddComponent<HuntFloatBob>();
            sway.amplitude = 0f;
            sway.rotate = 14f;

            var ring = HuntUiKit.Image(medal, "Ring", HuntUiSprites.Circle, theme.accent);
            HuntUiKit.Stretch(ring.rectTransform);
            var inner = HuntUiKit.Image(medal, "Inner", HuntUiSprites.Circle, Color.white);
            HuntUiKit.Stretch(inner.rectTransform, 26f);
            _preview = HuntUiKit.Image(medal, "Preview", null, theme.ink);
            _preview.preserveAspect = true;
            HuntUiKit.Stretch(_preview.rectTransform, 80f);

            var pill = HuntUiKit.Rounded(medal, "PlayPill", theme.success, 50f, true);
            HuntUiKit.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(300f, 130f), new Vector2(0f, -10f));
            pill.gameObject.AddComponent<HuntIdlePulse>().amount = 0.06f;
            var playLabel = HuntUiKit.Label(pill.transform, "PLAY", 68, Color.white, TextAnchor.MiddleCenter, true);
            playLabel.rectTransform.offsetMin = new Vector2(0f, 34f);
            _playSub = HuntUiKit.Label(pill.transform, "Level 1", 34, HuntUiTheme.WithAlpha(Color.white, 0.92f), TextAnchor.MiddleCenter, true);
            _playSub.rectTransform.offsetMax = new Vector2(0f, -78f);

            var playButton = medal.gameObject.AddComponent<Button>();
            playButton.targetGraphic = hit;
            playButton.transition = Selectable.Transition.None;
            playButton.onClick.AddListener(OnPlay);
            medal.gameObject.AddComponent<HuntButtonJuice>().pressedScale = 0.95f;

            // Levels: a tilted mosaic tile drifting in the background.
            var levels = HuntUiKit.Button(panel, null, theme.info, new Vector2(190f, 190f), OpenMap, NixinIcons.Load(NixinIcons.Grid));
            levels.Root.name = "LevelsTile";
            HuntUiKit.Place(levels.Root, new Vector2(0.2f, 0.2f), new Vector2(190f, 190f));
            levels.Root.localRotation = Quaternion.Euler(0f, 0f, 12f);
            levels.Icon.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            var levelsLabel = HuntUiKit.Label(levels.Root, "Levels", 38, Color.white, TextAnchor.MiddleCenter, true);
            levelsLabel.rectTransform.offsetMax = new Vector2(0f, -110f);
            var drift = levels.Root.gameObject.AddComponent<HuntFloatBob>();
            drift.amplitude = 8f;
            drift.speed = 0.7f;

            // Nixin Games: other studio games with Try It. Hidden until the list has a game; red dot while one is new.
            var games = HuntUiKit.Button(panel, null, theme.primary, new Vector2(190f, 190f), OnGames, CrossPromoArt.BadgeStarSprite());
            games.Root.name = "GamesTile";
            HuntUiKit.Place(games.Root, new Vector2(0.8f, 0.2f), new Vector2(190f, 190f));
            games.Root.localRotation = Quaternion.Euler(0f, 0f, -10f);
            games.Icon.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            var gamesLabel = HuntUiKit.Label(games.Root, CrossPromoArt.BadgeLabel, 30, Color.white, TextAnchor.MiddleCenter, true);
            gamesLabel.rectTransform.offsetMax = new Vector2(0f, -120f);
            var dot = HuntUiKit.Image(games.Root, "NewDot", HuntUiSprites.Circle, theme.danger);
            HuntUiKit.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(46f, 46f), new Vector2(-10f, -10f));
            var gamesDrift = games.Root.gameObject.AddComponent<HuntFloatBob>();
            gamesDrift.amplitude = 8f;
            gamesDrift.speed = 0.6f;
            _gamesTile = games.Root;
            _gamesDot = dot.gameObject;
            RefreshGamesTile();
        }

        void RefreshGamesTile()
        {
            if (_gamesTile == null)
                return;
            _gamesTile.gameObject.SetActive(NixinCrossPromo.IsAvailable);
            _gamesDot.SetActive(NixinCrossPromo.HasNew);
        }

        void OnGames()
        {
            GameServices.Analytics.TrackButtonClick("nixin_games", "HuntHomeScreen");
            HuntCrossPromoHost.Open();
        }

        void ApplyHomeBanner()
        {
            var adsOn = !Monetization.IsOwned(EntitlementKeys.RemoveAds);
            if (_aboveBanner != null)
                _aboveBanner.offsetMin = new Vector2(0f, adsOn ? BannerReserve : 0f);
            if (adsOn)
                Monetization.Request(HuntPlacements.HomeBanner);
            else
                Monetization.HideBanner();
        }

        void OnEntitlementChanged(string key)
        {
            if (key == EntitlementKeys.RemoveAds)
                ApplyHomeBanner();
        }

        void PlayIntro()
        {
            var items = new[] { "Logo", "Medal", "LevelsTile", "TopBar" };
            for (var i = 0; i < items.Length; i++)
            {
                var t = _home.Find(items[i]);
                if (t == null)
                    continue;
                t.localScale = Vector3.zero;
                HuntTweener.Scale(t, Vector3.zero, Vector3.one, 0.5f, Ease.OutBack, 0.05f + i * 0.08f);
            }
        }

        void RefreshHome()
        {
            var node = HuntSave.CurrentNode;
            if (node == null)
                return;
            var allDone = HuntSave.IsCleared(node);
            _playSub.text = allDone ? "All cleared! Replay" : node.Kind == HuntNodeKind.ImageHog ? "Photo Hunt" : "Level " + node.Number;
            var def = HuntRoadmapService.LevelFor(node);
            _preview.sprite = null;
            _preview.enabled = false;
            if (def != null)
            {
                var stem = System.IO.Path.GetFileNameWithoutExtension(def.FileName);
                var tex = Resources.Load<Texture2D>(HuntContentResources.TextKey(HuntCatalogLoader.SilhouetteFolder, stem + ".png"));
                if (tex != null)
                {
                    _preview.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    _preview.enabled = true;
                }
            }
            else
            {
                _preview.sprite = HuntUiSprites.Star;
                _preview.color = HuntUiTheme.Current.accent;
                _preview.enabled = true;
            }
        }

        void OnPlay()
        {
            GameServices.Analytics.TrackButtonClick("play", "HuntHomeScreen");
            HuntFlow.Launch(HuntSave.CurrentNode);
        }

        void OnNodePicked(HuntNode node)
        {
            GameServices.Analytics.TrackButtonClick("map_node", "HuntHomeScreen");
            HuntFlow.Launch(node);
        }

        void OpenMap()
        {
            if (_mapOpen)
                return;
            _mapOpen = true;
            GameServices.Analytics.TrackButtonClick("select_levels", "HuntHomeScreen");
            _map.gameObject.SetActive(true);
            _mapScreen.Rebuild();
            _mapScreen.ScrollTo(HuntSave.CurrentNode);
            var width = ((RectTransform)transform).rect.width;
            HuntTweener.Move(_map, new Vector2(width, 0f), Vector2.zero, 0.35f, Ease.OutCubic);
            HuntTweener.Fade(_mapGroup, 0f, 1f, 0.25f);
            HuntTweener.Fade(_homeGroup, 1f, 0f, 0.2f, 0f, () => _home.gameObject.SetActive(false));
        }

        void CloseMap()
        {
            if (!_mapOpen)
                return;
            _mapOpen = false;
            _home.gameObject.SetActive(true);
            RefreshHome();
            var width = ((RectTransform)transform).rect.width;
            HuntTweener.Fade(_homeGroup, 0f, 1f, 0.25f);
            HuntTweener.Move(_map, Vector2.zero, new Vector2(width, 0f), 0.3f, Ease.InQuad, 0f,
                () => _map.gameObject.SetActive(false));
        }

        void OnBack()
        {
            if (NixinStoreEngagement.TryHandleBack())
                return;
            if (HuntCrossPromoHost.TryHandleBack())
                return;
            if (_mapOpen)
                CloseMap();
            else
                AppShell.Minimize();
        }

        /// <summary>Applies an image-HOG result (scene or placeholder): stars, chest coins,
        /// unlock, then the Chapter Complete popup.</summary>
        public static void ConsumePendingResult()
        {
            var result = HuntImageHogBridge.TakePending();
            if (result == null)
                return;
            var node = HuntRoadmapService.Node(result.NodeIndex);
            if (node == null || node.Kind != HuntNodeKind.ImageHog)
                return;
            HuntAnalytics.ImageHogComplete(node.ImageHog.Id, result.Success);
            if (!result.Success)
            {
                HuntToast.Show("So close! Try the Photo Hunt again.");
                return;
            }

            var tuning = HuntWallet.Tuning;
            var stars = result.TimeLeftFraction > 0f ? HuntEconomyRules.StarsFor(result.TimeLeftFraction, result.HintsUsed, tuning) : 0;
            var prevBest = HuntSave.StarsFor(node);
            var first = HuntSave.RecordWin(node, stars);
            if (first)
                NixinRateUs.ReportLevelCompleted();
            var coins = HuntEconomyRules.CoinsForPhoto(stars, first, prevBest, tuning);
            HuntCurrencyPill.HoldOn(HuntCurrencies.Coins, coins);
            HuntWallet.Earn(HuntCurrencies.Coins, coins, "chapter_chest");
            HuntPopups.Chapter(new HuntChapterPayload
            {
                Chapter = node.Chapter,
                Coins = coins,
                OnContinue = () =>
                {
                    if (_instance == null)
                        return;
                    _instance.RefreshHome();
                    if (_instance._mapOpen)
                    {
                        _instance._mapScreen.Rebuild();
                        _instance._mapScreen.ScrollTo(HuntSave.CurrentNode);
                    }
                }
            });
        }
    }
}
