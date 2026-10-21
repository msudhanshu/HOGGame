using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Nixin.Audio;
using Nixin.Game.Core;
using Nixin.Monetization;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
using Nixin.Pinhole;
using Nixin.Pinhole.Core;
using Nixin.StoreEngagement;
using Nixin.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Unity
{
    [DefaultExecutionOrder(-45)]
    public sealed class PlayHost : MonoBehaviour
    {
        [SerializeField] HuntHud hud;
        [SerializeField] MosaicGlass glass;
        [SerializeField] HuntCatalogAsset catalogAsset;

        HuntDirector _director;
        IMosaicStampCatalog _catalog;
        readonly HuntLevelClock _clock = new HuntLevelClock();
        HuntLaunchMode _launchMode;
        bool _timedOut;
        int _currentLevelIndex;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);
        readonly Dictionary<int, int> _attemptByLevel = new Dictionary<int, int>();
        int _layoutGeneration;
        int _wrongPicks;
        float _levelStartTime;
        bool _levelEnded;
        int _lastTickSecond = -1;
        bool _adPlaying;
        int _levelsSinceInterstitial;
        bool _boardIsReveal;
        HuntLevelDef _currentDef;
        HuntNode _node;
        HuntDirector _roadmapDirector;
        /// <summary>Image-HOG reveal scene with no authored objects: explore only, Done = cleared.</summary>
        bool _preview;
        bool _introOpen;
        int _hintsUsed;
        int _continues;
        bool _adContinueUsed;
        float _lastFindTime = -10f;
        int _combo;
        static readonly string[] ComboWords = { "Nice!", "Great!", "Amazing!", "Incredible!", "Unstoppable!" };
        /// <summary>Dev-only: scanner forced by keys 1-6 / the HUD Lens button; empty = level's own.</summary>
        static string _scannerOverride = string.Empty;

        [SerializeField] [Range(0.25f, 0.85f)] float centerPickRadiusFactor = 0.52f;

        MosaicItemView _aimView;

        public HuntDirector Director => _director;

        /// <summary>Clock and picking freeze while any popup, ad or the intro card is up.</summary>
        bool Paused => _adPlaying || _introOpen || HuntPopups.AnyOpen;

        void Start()
        {
            HuntUiInput.EnsureEventSystem();

            HuntRoadmapService.Use(catalogAsset);
            HuntWallet.EnsureStarterGrant();
            HuntAudio.EnsureMusic();
            _director = new HuntDirector(HuntRoadmapService.Catalog);
            _roadmapDirector = _director;
            _launchMode = HuntLaunchRequest.Mode;
            var startNode = HuntLaunchRequest.NodeIndex >= 0
                ? HuntRoadmapService.Node(HuntLaunchRequest.NodeIndex)
                : HuntSave.CurrentNode;
            if (startNode == null || (startNode.Kind != HuntNodeKind.Mosaic && !HuntImageHogBridge.HasRevealSet(startNode)))
            {
                // Scene-based / placeholder image HOGs never load here; HuntFlow routes them through the bridge.
                HuntFlow.GoHome();
                return;
            }

            HuntPopups.CloseAll();
            HuntPopups.Navigator.UnhandledBack = OpenPause;
            HuntWallet.Changed += OnWalletChanged;

            if (hud != null)
            {
                hud.EnsureReady();
                hud.Bind(_director);
                hud.NextRequested.AddListener(LoadNextLevel);
                hud.RetryRequested.AddListener(RetryCurrentLevel);
                hud.MenuRequested.AddListener(ExitToHome);
                hud.HomeRequested.AddListener(OnHomePressed);
                hud.BackRequested.AddListener(OpenPause);
                hud.HintRequested.AddListener(OnHintRequested);
                hud.LensRequested.AddListener(CycleScanner);
                hud.ShopRequested.AddListener(OpenShop);
                hud.SetHintCount(HuntWallet.Hints);
            }

            GameServices.Analytics.TrackScreenView("HuntPlayScreen");
            LoadNode(startNode);
        }

        void OnDestroy()
        {
            HuntWallet.Changed -= OnWalletChanged;
        }

        void OnWalletChanged(string currency, int balance)
        {
            if (currency == HuntCurrencies.Hints && hud != null)
                hud.SetHintCount(balance);
        }

        void Update()
        {
            TickScannerKeys();
            TickClock();
        }

        void LateUpdate()
        {
            if (_director == null || _preview || _director.IsLevelComplete || _timedOut || Paused)
            {
                if (Paused)
                    ClearAimHighlight();
                return;
            }
            var pointer = Pointer.current;
            if (pointer == null)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var overHud = PointerOverHud();
            var glassCenter = glass != null ? glass.GlassWorld : AimSampleWorld(camera);
            var glassRadius = glass != null ? glass.GlassWorldRadius : 0.12f;

            if (pointer.press.isPressed && !overHud)
                SetAimHighlight(FindPickTargetNearGlassCenter(glassCenter, glassRadius));
            else if (!pointer.press.isPressed)
                ClearAimHighlight();

            if (!pointer.press.wasReleasedThisFrame || overHud)
                return;

            TryPickAtAim(glassCenter, glassRadius, camera);
        }

        Vector3 AimSampleWorld(Camera camera)
        {
            if (glass != null)
                return glass.GlassWorld;
            Vector3 screen = Pointer.current.position.ReadValue();
            screen.z = -camera.transform.position.z;
            return camera.ScreenToWorldPoint(screen);
        }

        void TickClock()
        {
            if (Paused || _preview || _director == null || _director.IsLevelComplete || _timedOut || !_clock.IsRunning)
                return;
            _clock.Tick(Time.deltaTime);
            if (hud != null)
            {
                var urgent = _clock.Remaining <= HuntRemoteConfig.UrgentThreshold;
                hud.RefreshTimer(_clock.Remaining, urgent);
                PlayCountdownTick(urgent);
            }

            if (_clock.IsExpired)
                OnTimeExpired();
        }

        void PlayCountdownTick(bool urgent)
        {
            var second = Mathf.CeilToInt(_clock.Remaining);
            if (second == _lastTickSecond)
                return;
            _lastTickSecond = second;
            if (!urgent || second <= 0)
                return;
            HuntAudio.Clip(second <= 3 ? HuntTickSound.FinalTick : HuntTickSound.Tick);
            if (second <= 3)
                HuntHaptics.Light();
        }

        void OnTimeExpired()
        {
            if (_timedOut)
                return;
            _timedOut = true;
            _clock.Stop();
            ClearAimHighlight();
            PlayClip(NixinAudio.Fail);
            _levelEnded = true;
            var session = _director.Session;
            var total = _director.Current.TargetCount;
            var found = _director.WaveIndex * _director.Current.WaveSize + (session != null ? session.Found.Count : 0);
            HuntAnalytics.LevelFail(_director.Current.FileName, _currentLevelIndex, _attemptByLevel[_currentLevelIndex],
                "time_out", Time.time - _levelStartTime, found, total, _wrongPicks);
            if (hud != null)
                hud.ShowTimedOut();
            HuntHaptics.Heavy();
            HuntTweener.Delay(this, 0.6f, ShowFail);
        }

        void ShowFail()
        {
            if (!_timedOut || _director == null)
                return;
            var t = HuntWallet.Tuning;
            var cost = HuntEconomyRules.ContinueCost(_continues, t);
            var session = _director.Session;
            HuntPopups.Fail(new HuntFailPayload
            {
                Found = _director.WaveIndex * _director.Current.WaveSize + (session != null ? session.Found.Count : 0),
                Total = _director.Current.TargetCount,
                ContinueCost = cost,
                ContinueSeconds = t.ContinueSeconds,
                AdContinueAvailable = !_adContinueUsed && Monetization.IsAvailable(HuntPlacements.ContinueAd),
                OnContinueCoins = () =>
                {
                    if (HuntWallet.TrySpend(HuntCurrencies.Coins, cost, "continue"))
                        Continue("coins");
                    else
                        HuntJams.NotEnoughCoins(cost, ShowFail, ShowFail);
                },
                OnContinueAd = () =>
                {
                    _adPlaying = true;
                    Monetization.Request(HuntPlacements.ContinueAd, granted =>
                    {
                        _adPlaying = false;
                        if (granted)
                        {
                            _adContinueUsed = true;
                            Continue("ad");
                        }
                        else ShowFail();
                    });
                },
                OnRetry = RetryCurrentLevel,
                OnHome = ExitToHome
            });
        }

        void Continue(string method)
        {
            var seconds = HuntWallet.Tuning.ContinueSeconds;
            _timedOut = false;
            _levelEnded = false;
            _lastTickSecond = -1;
            _clock.AddTime(seconds);
            _continues++;
            HuntAnalytics.ContinueUsed(_director.Current.FileName, method, _continues);
            if (hud != null)
            {
                hud.Refresh("+" + Mathf.RoundToInt(seconds) + "s!");
                hud.SetPlayControls(true);
                hud.RefreshTimer(_clock.Remaining, false);
                HuntFx.PopText((RectTransform)hud.transform, new Vector2(0f, 200f), "+" + Mathf.RoundToInt(seconds) + "s",
                    HuntUiTheme.Current.accent, 110);
            }

            HuntAudio.Sfx(NixinAudio.Confirm);
            HuntHaptics.Medium();
        }

        void OpenPause()
        {
            if (NixinStoreEngagement.TryHandleBack())
                return;
            if (_director == null || _levelEnded || _timedOut || HuntPopups.AnyOpen || _introOpen)
                return;
            HuntPopups.Pause(new HuntPausePayload
            {
                Title = "Level " + (_node != null ? _node.Number : _currentLevelIndex + 1),
                OnResume = null,
                OnRestart = RetryCurrentLevel,
                OnHome = ExitToHome
            });
        }

        void OpenShop()
        {
            if (_director == null || _levelEnded || _timedOut || HuntPopups.AnyOpen || _introOpen)
                return;
            HuntPopups.Shop("play_hud");
        }

        void RetryCurrentLevel()
        {
            if (_director == null || _director.Current == null)
                return;
            if (IsImageHogNode)
            {
                HuntFlow.PayPhotoFee(_node, RestartLevel);
                return;
            }

            RestartLevel();
        }

        void RestartLevel()
        {
            HuntAnalytics.LevelRestart(_director.Current.FileName, _currentLevelIndex, _attemptByLevel[_currentLevelIndex]);
            MaybeShowInterstitial();
            LoadLevel(_director.Current, _currentLevelIndex);
        }

        void OnHintRequested()
        {
            if (Paused || _director == null || _director.Session == null || _director.IsLevelComplete || _timedOut)
                return;
            if (_director.Session.Remaining == null || _director.Session.Remaining.Count == 0)
                return;

            if (HuntWallet.TrySpend(HuntCurrencies.Hints, 1, "hint"))
            {
                UseHint();
                return;
            }

            HuntJams.OutOfHints(UseHint);
        }

        void UseHint()
        {
            _hintsUsed++;
            HuntAudio.Sfx(NixinAudio.Shuffle, 1.3f);
            HuntHaptics.Light();
            RevealHint();
        }

        void RevealHint()
        {
            if (_director == null || _director.Session == null)
                return;
            var remaining = _director.Session.Remaining;
            if (remaining == null || remaining.Count == 0)
                return;

            var view = FindViewByName(remaining[0]);
            if (view == null)
                return;

            var outline = view.GetComponent<HuntAimOutline>();
            if (outline == null)
                outline = view.gameObject.AddComponent<HuntAimOutline>();
            outline.CaptureBaseScale();
            StartCoroutine(PulseHint(outline));
        }

        static IEnumerator PulseHint(HuntAimOutline outline)
        {
            outline.SetAimed(true);
            yield return new WaitForSeconds(2.5f);
            if (outline != null)
                outline.SetAimed(false);
        }

        MosaicItemView FindViewByName(string name)
        {
            var views = GetComponentsInChildren<MosaicItemView>(true);
            for (var i = 0; i < views.Length; i++)
            {
                if (views[i].Hunt && views[i].ItemName == name)
                    return views[i];
            }
            return null;
        }

        /// <summary>Frequency-capped via MonetizationConfigKeys.InterstitialLevelInterval; the
        /// director itself also enforces a minimum gap and suppresses entirely for remove_ads
        /// owners (see MonetizationCatalog.asset / MonetizationDirector).</summary>
        void MaybeShowInterstitial()
        {
            _levelsSinceInterstitial++;
            var interval = GameServices.Config.GetInt(MonetizationConfigKeys.InterstitialLevelInterval, 3);
            if (interval <= 0 || _levelsSinceInterstitial < interval)
                return;
            _levelsSinceInterstitial = 0;
            Monetization.Request(HuntPlacements.InterstitialLevelComplete);
        }

        void ExitToHome()
        {
            HuntPopups.CloseAll();
            if (_director != null && _director.Current != null && !_levelEnded && !_director.IsLevelComplete)
            {
                var session = _director.Session;
                var found = _director.WaveIndex * _director.Current.WaveSize + (session != null ? session.Found.Count : 0);
                HuntAnalytics.LevelExit(_director.Current.FileName, _currentLevelIndex,
                    Time.time - _levelStartTime, found, _director.Current.TargetCount);
            }
            LoadHome();
        }

        static void LoadHome()
        {
            HuntFlow.GoHome();
        }

        void TryPickAtAim(Vector3 glassCenter, float glassRadius, Camera camera)
        {
            if (_timedOut)
                return;
            var view = _aimView != null ? _aimView : FindPickTargetNearGlassCenter(glassCenter, glassRadius);
            ClearAimHighlight();
            if (view == null)
                return;

            var itemWorld = view.transform.position;
            if (glass != null && !glass.IsRevealed(itemWorld))
                return;

            var waveBefore = _director.WaveIndex;
            if (!_director.TryFind(view.ItemName))
            {
                _wrongPicks++;
                _combo = 0;
                HuntWrongFx.Play(view, camera, hud != null ? hud.GetComponent<Canvas>() : null, this);
                HuntCameraShake.Shake(camera, 0.05f, 0.22f);
                HuntAudio.Sfx(NixinAudio.Error);
                HuntHaptics.Medium();
                if (hud != null)
                    hud.Refresh("Not on the list.");
                return;
            }

            var hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            HuntFindFx.Play(view, camera, hudCanvas, this);
            PlayFindJuice(view, camera);

            var elapsed = Time.time - _levelStartTime;
            HuntAnalytics.Found(_director.Current.FileName, view.ItemName, elapsed);
            if (_director.IsLevelComplete)
            {
                PlayClip(NixinAudio.Win);
                _clock.Stop();
                _levelEnded = true;
                HuntAnalytics.LevelComplete(_director.Current.FileName, _currentLevelIndex,
                    _attemptByLevel[_currentLevelIndex], elapsed, _wrongPicks);
                if (hud != null)
                {
                    hud.Refresh();
                    hud.ShowLevelComplete();
                }

                HuntTweener.Delay(this, 0.9f, OnLevelWon);
                return;
            }

            if (hud != null)
            {
                hud.Refresh();
                if (_director.WaveIndex != waveBefore)
                {
                    hud.PopIcons();
                    HuntFx.PopText((RectTransform)hud.transform, new Vector2(0f, 150f),
                        "Wave " + (_director.WaveIndex + 1) + "/" + _director.WaveCount, Color.white, 90);
                }
            }
        }

        /// <summary>Combo text, rising pitch, haptic tick on every correct find.</summary>
        void PlayFindJuice(MosaicItemView view, Camera camera)
        {
            _combo = Time.time - _lastFindTime < 2.5f ? _combo + 1 : 1;
            _lastFindTime = Time.time;
            HuntAudio.Sfx(NixinAudio.Click, 1f + Mathf.Min(_combo, 6) * 0.08f);
            HuntHaptics.Light();
            if (hud == null || camera == null)
                return;
            var hudRect = (RectTransform)hud.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, camera.WorldToScreenPoint(view.transform.position),
                    null, out var local))
                HuntFx.Sparkle(hudRect, local, HuntUiTheme.Current.accent, 10, 160f);
            if (_combo >= 2 && !_director.IsLevelComplete)
            {
                var word = ComboWords[Mathf.Min(_combo - 2, ComboWords.Length - 1)];
                HuntFx.PopText(hudRect, new Vector2(0f, 320f), word, HuntUiTheme.Current.accent, 96);
            }
        }

        bool IsImageHogNode => _node != null && _node.Kind == HuntNodeKind.ImageHog;

        void OnHomePressed()
        {
            if (_preview)
                FinishImageHog(true, 0f);
            else
                ExitToHome();
        }

        /// <summary>Image HOGs report through the bridge so Home awards stars + the chapter chest
        /// and shows Chapter Complete, exactly like a scene-based image HOG.</summary>
        void FinishImageHog(bool success, float timeLeftFraction)
        {
            HuntImageHogBridge.ReportResult(success, timeLeftFraction, _hintsUsed);
            HuntImageHogBridge.ReturnToHome();
        }

        void OnLevelWon()
        {
            if (IsImageHogNode)
            {
                FinishImageHog(true, _continues > 0 ? 0.01f : _clock.RemainingFraction);
                return;
            }

            var tuning = HuntWallet.Tuning;
            var def = _director.Current;
            var stars = _continues > 0 ? 1 : HuntEconomyRules.StarsFor(_clock.RemainingFraction, _hintsUsed, tuning);
            var prevBest = HuntSave.StarsFor(_node);
            var first = HuntSave.RecordWin(_node, stars);
            if (first)
                NixinRateUs.ReportLevelCompleted();
            var coins = HuntEconomyRules.CoinsForWin(def.Toughness, stars, first, prevBest, tuning);
            HuntCurrencyPill.HoldOn(HuntCurrencies.Coins, coins);
            HuntWallet.Earn(HuntCurrencies.Coins, coins, first ? "level_win" : "level_replay");
            HuntAnalytics.StarsAwarded(def.FileName, stars, first);

            var next = HuntRoadmapService.Next(_node);
            HuntPopups.Win(new HuntWinPayload
            {
                Title = next == null ? "All Levels Done!" : stars == 3 ? "Perfect!" : "Level Complete!",
                Stars = stars,
                Coins = coins,
                FirstClear = first,
                CanDouble = Monetization.IsAvailable(HuntPlacements.DoubleCoins),
                RequestDouble = done =>
                {
                    _adPlaying = true;
                    Monetization.Request(HuntPlacements.DoubleCoins, granted =>
                    {
                        _adPlaying = false;
                        if (!granted)
                        {
                            done(0);
                            return;
                        }

                        HuntCurrencyPill.HoldOn(HuntCurrencies.Coins, coins);
                        HuntWallet.Earn(HuntCurrencies.Coins, coins, "double_coins_ad");
                        done(coins);
                    });
                },
                NextLabel = next == null ? "HOME" : next.Kind == HuntNodeKind.ImageHog ? PhotoLabel(next) : "NEXT",
                OnNext = () =>
                {
                    // Rate-us first so it never stacks with the interstitial inside GoToNode.
                    if (!NixinRateUs.TryShow(() => GoToNode(next)))
                        GoToNode(next);
                },
                OnHome = ExitToHome
            });
        }

        static string PhotoLabel(HuntNode node)
        {
            var fee = HuntFlow.PhotoFee(node);
            return fee > 0 ? "PHOTO HUNT  " + fee : "PHOTO HUNT";
        }

        void GoToNode(HuntNode next)
        {
            MaybeShowInterstitial();
            if (next == null)
            {
                LoadHome();
                return;
            }

            if (next.Kind == HuntNodeKind.ImageHog)
            {
                HuntFlow.Launch(next);
                return;
            }

            LoadNode(next);
        }

        void SetAimHighlight(MosaicItemView view)
        {
            if (view == _aimView)
                return;
            ClearAimHighlight();
            if (view == null)
                return;

            _aimView = view;
            var outline = view.GetComponent<HuntAimOutline>();
            if (outline == null)
                outline = view.gameObject.AddComponent<HuntAimOutline>();
            outline.CaptureBaseScale();
            outline.SetAimed(true);
            if (glass != null && glass.Saturation != null)
                glass.Saturation.AimTarget = view;
        }

        void ClearAimHighlight()
        {
            if (glass != null && glass.Saturation != null)
                glass.Saturation.AimTarget = null;
            if (_aimView == null)
                return;
            var outline = _aimView.GetComponent<HuntAimOutline>();
            if (outline != null)
                outline.SetAimed(false);
            _aimView = null;
        }

        /// <summary>Hunt stamp whose center is within the glass center threshold (pickable zone).</summary>
        MosaicItemView FindPickTargetNearGlassCenter(Vector3 glassCenter, float glassRadius)
        {
            var shape = glass != null ? glass.LensShape : ScannerShape.Circle;
            var aspect = glass != null ? glass.LensRectAspect : 1f;
            var halfH = Mathf.Max(0.02f, glassRadius * centerPickRadiusFactor);
            ScannerHole.HalfExtents(glassRadius, aspect, shape, out var lensHalfW, out var lensHalfH);
            var searchRadius = Mathf.Max(lensHalfW, lensHalfH) * 1.05f;
            var center2 = new Vector2(glassCenter.x, glassCenter.y);
            var hits = Physics2D.OverlapCircleAll(center2, searchRadius);
            MosaicItemView best = null;
            var bestCenterDist = float.MaxValue;
            // Reveal objects with an exact outline: the shape under the lens centre wins (smallest when
            // shapes overlap), so a neighbour's bounding box can never steal the pick.
            MosaicItemView under = null;
            var underArea = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;
                var view = hit.GetComponent<MosaicItemView>();
                if (view == null || !view.Hunt)
                    continue;

                var outline = hit.GetComponent<RevealHitShape>();
                if (outline != null && outline.Def != null && outline.Def.HasOutline)
                {
                    if (outline.ContainsWorld(center2) && outline.AreaPx < underArea)
                    {
                        underArea = outline.AreaPx;
                        under = view;
                    }

                    // Not under the centre: only its own centre in the lens's inner zone may still pick it
                    // (keeps tiny objects friendly), never its collider overlapping the centre.
                    var c = (Vector2)hit.bounds.center;
                    var cdx = c.x - glassCenter.x;
                    var cdy = c.y - glassCenter.y;
                    if (!ScannerHole.Contains(cdx, cdy, halfH, aspect, shape))
                        continue;
                    var cDist = Mathf.Sqrt(cdx * cdx + cdy * cdy);
                    if (cDist < bestCenterDist)
                    {
                        bestCenterDist = cDist;
                        best = view;
                    }

                    continue;
                }

                var pos = view.transform.position;
                var dx = pos.x - glassCenter.x;
                var dy = pos.y - glassCenter.y;
                // Pickable when the item centre is in the lens's inner zone, or the lens centre sits
                // on the item (large painted objects in reveal levels).
                var nearCenter = ScannerHole.Contains(dx, dy, halfH, aspect, shape);
                if (!nearCenter && !hit.OverlapPoint(center2))
                    continue;
                var centerDist = Mathf.Sqrt(dx * dx + dy * dy);
                if (centerDist >= bestCenterDist)
                    continue;
                bestCenterDist = centerDist;
                best = view;
            }

            return under != null ? under : best;
        }

        bool PointerOverHud()
        {
            var es = EventSystem.current;
            if (es == null || Pointer.current == null)
                return false;
            var data = new PointerEventData(es)
            {
                position = Pointer.current.position.ReadValue()
            };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        void LoadNextLevel()
        {
            GoToNode(HuntRoadmapService.Next(_node));
        }

        void LoadNode(HuntNode node)
        {
            _node = node;
            if (node.Kind == HuntNodeKind.ImageHog)
            {
                var set = RevealSetAsset.Load(node.ImageHog.RevealSet);
                if (set == null)
                {
                    HuntFlow.GoHome();
                    return;
                }

                // One-level director for the chapter's reveal scene; mosaics keep the roadmap director.
                var def = set.ToLevelDef(node.ImageHog);
                _director = new HuntDirector(new HuntCatalog(new[] { def }));
                LoadLevel(def, 0);
                return;
            }

            _director = _roadmapDirector;
            LoadLevel(_director.Catalog[node.CatalogIndex], node.CatalogIndex);
        }

        void LoadLevel(HuntLevelDef def, int index)
        {
            HuntPopups.CloseAll();
            _currentLevelIndex = index;
            _timedOut = false;
            _levelEnded = false;
            _wrongPicks = 0;
            _hintsUsed = 0;
            _continues = 0;
            _adContinueUsed = false;
            _combo = 0;
            _lastTickSecond = -1;
            _levelStartTime = Time.time;
            _clock.Reset(def.TimeLimitSeconds);
            if (hud != null)
                hud.HideActionButtons();

            _currentDef = def;
            if (!_attemptByLevel.TryGetValue(index, out var attempt))
                attempt = 0;
            _attemptByLevel[index] = attempt + 1;
            _layoutGeneration++;

            var reveal = def.Mode == HuntLevelMode.Reveal;
            List<string> names;
            if (reveal ? !BuildRevealBoard(def, out names) : !BuildMosaicBoard(def, index, out names))
                return;

            var cam = Camera.main;
            var root = transform.Find(MosaicAssembler.RootName);
            if (glass == null)
                glass = GetComponent<MosaicGlass>();
            if (glass == null)
                glass = gameObject.AddComponent<MosaicGlass>();
            var fog = !reveal && def.GlassFog;
            glass.SetRevealMode(reveal);
            glass.SetScanner(ResolveScanner(def));
            glass.SetGlassFogMode(fog);
            glass.ApplyRules(def.MemoryFade, def.GlassRadius, def.Zoom);
            if (root != null && cam != null)
                glass.Bind(root, cam);
            glass.SetGlassFogMode(fog);

            _preview = names == null || names.Count == 0;
            if (_preview)
            {
                ClearAimHighlight();
                _clock.Stop();
                if (hud != null)
                {
                    hud.Bind(_director);
                    hud.ShowPreview(def.Title, "Preview - scan around! Objects coming soon.");
                }

                HuntAnalytics.LevelStart(def.FileName, index, "preview", _attemptByLevel[index], 0f, 0);
                return;
            }

            _director.BeginLevel(index, names);
            ClearAimHighlight();
            if (hud != null)
            {
                hud.SetStampCatalog(_catalog);
                hud.Bind(_director);
                hud.RefreshTimer(_clock.Remaining, false);
                hud.SetPlayControls(true);
                hud.SetHintCount(HuntWallet.Hints);
            }
            PlayClip(NixinAudio.Shuffle);
            ShowIntro(def);
            HuntAnalytics.LevelStart(def.FileName, index, _launchMode.ToString(), _attemptByLevel[index],
                def.TimeLimitSeconds, def.TargetCount);
        }

        bool BuildMosaicBoard(HuntLevelDef def, int index, out List<string> names)
        {
            names = null;
            if (!HuntContentResources.TryLoadText(HuntCatalogLoader.SilhouetteFolder, def.FileName, out var json))
            {
                Debug.LogError(
                    "Missing mosaic level resource "
                    + HuntContentResources.TextKey(HuntCatalogLoader.SilhouetteFolder, def.FileName));
                return false;
            }

            if (_boardIsReveal)
                RevealLevelBuilder.Clear(transform);
            _boardIsReveal = false;

            var level = MosaicLevelCodec.Parse(json);
            _catalog = LoadCatalog(level, def);

            var huntCount = def.WaveSize * def.WaveCount;
            var seed = MosaicHuntSprinkler.SeedFor(
                level.LevelId + ":" + def.FileName,
                _attemptByLevel[index] + def.SprinkleSeed + (_layoutGeneration * 4861));
            level = MosaicHuntSprinklerUnity.Apply(level, _catalog, huntCount, seed, huntSizeScale: def.HuntSizeScale);

            MosaicAssembler.Assemble(level, _catalog, transform);

            var cam = Camera.main;
            if (hud != null && cam != null)
                hud.ApplyPlayViewport(cam);
            MosaicAssembler.FrameCamera(cam, level);
            var background = HuntBackgrounds.Selected;
            if (cam != null && background != null)
                cam.backgroundColor = background.Field;
            names = UniqueNames(level);
            return true;
        }

        bool BuildRevealBoard(HuntLevelDef def, out List<string> names)
        {
            names = null;
            var built = RevealLevelBuilder.Build(def.RevealSet, transform, MosaicGlass.PlainSpriteMaterial());
            if (built == null)
                return false;
            _boardIsReveal = true;
            _catalog = built.Stamps;

            var cam = Camera.main;
            if (hud != null && cam != null)
                hud.ApplyPlayViewport(cam);
            RevealLevelBuilder.FrameCamera(cam, built.Set);
            names = built.Set.ObjectNames();
            return true;
        }

        static ScannerStyle ResolveScanner(HuntLevelDef def)
        {
            if (!string.IsNullOrEmpty(_scannerOverride))
                return NixinScanners.Get(_scannerOverride);
            if (!string.IsNullOrEmpty(def.Scanner))
                return NixinScanners.Get(def.Scanner);
            return NixinScanners.Get(def.Mode == HuntLevelMode.Reveal ? NixinScanners.SciFiScanner : NixinScanners.Magnifier);
        }

        void TickScannerKeys()
        {
            if (!Debug.isDebugBuild)
                return;
            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (kb.digit0Key.wasPressedThisFrame)
                SetScannerOverride(string.Empty);
            for (var i = 0; i < NixinScanners.All.Length && i < 9; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                    SetScannerOverride(NixinScanners.All[i]);
            }
        }

        /// <summary>Dev aid: next built-in scanner on every press, then back to the level's own.</summary>
        void CycleScanner()
        {
            var all = NixinScanners.All;
            var i = System.Array.IndexOf(all, _scannerOverride);
            SetScannerOverride(i + 1 < all.Length ? all[i + 1] : string.Empty);
        }

        void SetScannerOverride(string id)
        {
            _scannerOverride = id ?? string.Empty;
            if (glass == null || _currentDef == null)
                return;
            var style = ResolveScanner(_currentDef);
            glass.SetScanner(style);
            if (hud != null)
                hud.Refresh("Lens: " + (style != null ? style.Id : "classic"));
        }

        /// <summary>Level card; the clock (already reset) starts ticking once it closes.</summary>
        void ShowIntro(HuntLevelDef def)
        {
            _introOpen = true;
            var payload = HuntFlow.IntroFor(_node, def);
            payload.OnPlay = () =>
            {
                _introOpen = false;
                _levelStartTime = Time.time;
                if (hud != null)
                    hud.PopIcons();
            };
            payload.OnHome = () =>
            {
                _introOpen = false;
                ExitToHome();
            };
            HuntPopups.LevelIntro(payload);
        }

        static void PlayClip(string id)
        {
            HuntAudio.Sfx(id);
        }

        /// <summary>Uses the level's own atlas, unless it declares an alternate Theme and the
        /// player owns the theme-pack entitlement - a remote-config-tunable catalog edit away
        /// from being the default, never a code change.</summary>
        static IMosaicStampCatalog LoadCatalog(MosaicLevel level, HuntLevelDef def)
        {
            var atlas = level.Atlas;
            if (def != null && !string.IsNullOrEmpty(def.Theme) && Monetization.IsOwned(HuntEntitlements.ThemePack))
                atlas = def.Theme;

            if (string.IsNullOrEmpty(atlas))
                return new PrimitiveMosaicStampCatalog();

            var parts = atlas.Split(',');
            var keys = new List<string>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length == 0)
                    continue;
                keys.Add(HuntContentResources.AtlasJsonKey(name));
            }

            var stampCatalog = AtlasMosaicStampCatalog.TryLoadManyFromResources(
                keys,
                HuntContentResources.AtlasTextureKey);
            if (stampCatalog != null)
                return stampCatalog;
            return new PrimitiveMosaicStampCatalog();
        }

        static List<string> UniqueNames(MosaicLevel level)
        {
            var names = new List<string>();
            for (var i = 0; i < level.Items.Count; i++)
            {
                var item = level.Items[i];
                if (!item.Hunt)
                    continue;
                var name = item.Name;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name);
            }

            if (names.Count == 0)
                names.Add(MosaicStampNames.Circle);
            return names;
        }
    }
}
