using System;
using System.Collections.Generic;
using Game.Core;
using Nixin.Audio;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
using Nixin.Pinhole;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Unity
{
    /// <summary>
    /// Experimental drift hunt (own scene, campaign untouched): hunt icons wander through the silhouette
    /// shoving filler tiles aside; hold the lens on one until it locks, then lift to shoot it down.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class DriftHost : MonoBehaviour
    {
        const float Substep = 1f / 60f;
        const int MaxSubsteps = 2;
        const float PrewarmSeconds = 1.5f;
        /// <summary>Lock progress at which the seeker beeps start, so sweeping past a target stays quiet.</summary>
        const float BeepAfterProgress = 0.25f;

        [SerializeField] HuntHud hud;
        [SerializeField] MosaicGlass glass;
        [SerializeField] HuntCatalogAsset catalogAsset;
        [SerializeField] DriftLevelSet levelSet;
        [SerializeField] int levelIndex;

        struct Piece
        {
            public MosaicItemView View;
            public Transform Transform;
            public float BaseAngle;
            public Vector3 BaseScale;
            public float Radius;
        }

        /// <summary>Something the lens can lock onto: a moving target, or a static decoy icon.</summary>
        struct Candidate
        {
            public bool Target;
            public int Index;
        }

        readonly List<Piece> _fillers = new List<Piece>();
        readonly List<Piece> _targets = new List<Piece>();
        readonly List<Candidate> _candidates = new List<Candidate>();
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);
        readonly HuntLevelClock _clock = new HuntLevelClock();
        static readonly string[] ComboWords = { "Nice!", "Great!", "Amazing!", "Incredible!", "Unstoppable!" };

        HuntDirector _director;
        DriftSwarm _swarm;
        DriftLockTracker _tracker;
        DriftLockReticle _reticle;
        DriftLevel _level;
        IMosaicStampCatalog _catalog;
        Func<int, float> _errorOf;
        float _accumulator;
        bool _running;
        bool _gestureOnHud;
        int _attempt;
        int _wrongPicks;
        int _combo;
        float _lastFindTime = -10f;
        float _levelStartTime;
        float _nextBeep;
        int _lastTickSecond = -1;
        Vector3 _lensCenter;
        float _lensRadius = 1f;

        bool Paused => HuntPopups.AnyOpen;

        void Start()
        {
            HuntUiInput.EnsureEventSystem();
            HuntRoadmapService.Use(catalogAsset);
            HuntAudio.EnsureMusic();
            HuntPopups.CloseAll();
            HuntPopups.Navigator.UnhandledBack = ExitToHome;
            _errorOf = ErrorOf;

            if (glass == null)
                glass = GetComponent<MosaicGlass>();
            if (glass == null)
                glass = gameObject.AddComponent<MosaicGlass>();

            if (hud != null)
            {
                hud.EnsureReady();
                hud.NextRequested.AddListener(NextLevel);
                hud.RetryRequested.AddListener(Restart);
                hud.MenuRequested.AddListener(ExitToHome);
                hud.HomeRequested.AddListener(ExitToHome);
                hud.BackRequested.AddListener(ExitToHome);
            }

            LoadLevel(levelIndex);
        }

        void Update()
        {
            if (_swarm == null || Paused)
                return;

            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, Substep * MaxSubsteps);
            while (_accumulator >= Substep)
            {
                _swarm.Step(Substep);
                _accumulator -= Substep;
            }

            ApplyPoses();
            TickClock();
        }

        void LateUpdate()
        {
            if (!_running || Paused || _tracker == null)
            {
                if (_reticle != null && Paused)
                    _reticle.Hide();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null)
                return;

            _lensCenter = glass.GlassWorld;
            _lensRadius = Mathf.Max(0.01f, glass.VisibleWorldRadius);
            ReadInput(pointer, out var active, out var fire);
            var ev = _tracker.Tick(Time.deltaTime, active, fire, active ? NearestCandidate() : DriftLockTracker.None, _errorOf);
            HandleLockEvent(ev);
            TrackReticle();
        }

        /// <summary>
        /// Mouse: hovering over the board tracks (no press needed), a click fires. Touch / pen: dragging the
        /// finger moves the lens and tracks, lifting the finger fires.
        /// </summary>
        void ReadInput(Pointer pointer, out bool active, out bool fire)
        {
            var hover = pointer is Mouse;
            if (hover)
            {
                var screen = pointer.position.ReadValue();
                var onBoard = screen.x >= 0f && screen.y >= 0f && screen.x <= Screen.width && screen.y <= Screen.height
                              && !PointerOverHud();
                active = onBoard;
                fire = onBoard && pointer.press.wasPressedThisFrame;
                return;
            }

            if (pointer.press.wasPressedThisFrame)
                _gestureOnHud = PointerOverHud();
            active = pointer.press.isPressed && !_gestureOnHud;
            fire = pointer.press.wasReleasedThisFrame && !_gestureOnHud;
        }

        void HandleLockEvent(DriftLockEvent ev)
        {
            switch (ev)
            {
                case DriftLockEvent.Acquiring:
                    CandidatePose(_tracker.FocusId, out var pos, out var radius);
                    _reticle.BeginAcquire(pos, ReticleRadius(radius));
                    _nextBeep = 0f;
                    SetAim(_tracker.FocusId);
                    break;
                case DriftLockEvent.Locked:
                    _reticle.Lock();
                    HuntAudio.Sfx(NixinAudio.Confirm, 1.25f);
                    HuntHaptics.Medium();
                    break;
                case DriftLockEvent.Broken:
                    // Slipped off before locking: silent, the reticle just springs away.
                    _reticle.Lose();
                    SetAim(DriftLockTracker.None);
                    break;
                case DriftLockEvent.Lost:
                    _reticle.Lose();
                    HuntAudio.Sfx(NixinAudio.Error, 0.7f, 0.6f);
                    HuntHaptics.Light();
                    SetAim(DriftLockTracker.None);
                    break;
                case DriftLockEvent.Released:
                    _reticle.Hide();
                    SetAim(DriftLockTracker.None);
                    break;
                case DriftLockEvent.Fired:
                    _reticle.Fire();
                    SetAim(DriftLockTracker.None);
                    Shoot(_tracker.FiredId);
                    break;
            }
        }

        void TrackReticle()
        {
            var focus = _tracker.FocusId;
            if (focus == DriftLockTracker.None)
                return;
            CandidatePose(focus, out var pos, out var radius);
            _reticle.Follow(pos, ReticleRadius(radius), _tracker.Progress01);
            if (_tracker.State != DriftLockState.Acquiring || Time.time < _nextBeep)
                return;
            // Seeker tone: beeps speed up and rise as the lock builds. Silent for a quick fly-over.
            var p = _tracker.Progress01;
            if (p < BeepAfterProgress)
                return;
            HuntAudio.Sfx(NixinAudio.Click, 1.1f + p * 0.9f, 0.45f);
            _nextBeep = Time.time + Mathf.Lerp(0.22f, 0.06f, p);
        }

        /// <summary>Box hugs the target but never outgrows the magnified view.</summary>
        float ReticleRadius(float radius) => Mathf.Min(radius, _lensRadius * 0.4f);

        void Shoot(int id)
        {
            if (id < 0 || id >= _candidates.Count)
                return;
            var c = _candidates[id];
            var piece = c.Target ? _targets[c.Index] : _fillers[c.Index];
            var camera = Camera.main;
            var hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            var waveBefore = _director.WaveIndex;

            if (!c.Target || !_director.TryFind(piece.View.ItemName))
            {
                _wrongPicks++;
                _combo = 0;
                HuntWrongFx.Play(piece.View, camera, hudCanvas, this);
                HuntCameraShake.Shake(camera, 0.05f, 0.22f);
                HuntAudio.Sfx(NixinAudio.Error);
                HuntHaptics.Medium();
                if (hud != null)
                    hud.Refresh("Not on the list.");
                return;
            }

            _swarm.RemoveTarget(c.Index);
            HuntFindFx.Play(piece.View, camera, hudCanvas, this);
            PlayFindJuice(piece.View, camera);
            var elapsed = Time.time - _levelStartTime;
            HuntAnalytics.Found(_level.file, piece.View.ItemName, elapsed);

            if (_director.IsLevelComplete)
            {
                Won(elapsed);
                return;
            }

            if (hud == null)
                return;
            hud.Refresh();
            if (_director.WaveIndex == waveBefore)
                return;
            hud.PopIcons();
            HuntFx.PopText((RectTransform)hud.transform, new Vector2(0f, 150f),
                "Wave " + (_director.WaveIndex + 1) + "/" + _director.WaveCount, Color.white, 90);
        }

        void PlayFindJuice(MosaicItemView view, Camera camera)
        {
            _combo = Time.time - _lastFindTime < 4f ? _combo + 1 : 1;
            _lastFindTime = Time.time;
            HuntAudio.Sfx(NixinAudio.Click, 1f + Mathf.Min(_combo, 6) * 0.08f);
            HuntHaptics.Medium();
            if (hud == null || camera == null)
                return;
            var hudRect = (RectTransform)hud.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRect, camera.WorldToScreenPoint(view.transform.position),
                    null, out var local))
                HuntFx.Sparkle(hudRect, local, HuntUiTheme.Current.accent, 12, 180f);
            if (_combo >= 2 && !_director.IsLevelComplete)
                HuntFx.PopText(hudRect, new Vector2(0f, 320f), ComboWords[Mathf.Min(_combo - 2, ComboWords.Length - 1)],
                    HuntUiTheme.Current.accent, 96);
        }

        void Won(float elapsed)
        {
            _running = false;
            _clock.Stop();
            HuntAudio.Sfx(NixinAudio.Win);
            HuntAnalytics.LevelComplete(_level.file, levelIndex, _attempt, elapsed, _wrongPicks);
            if (hud != null)
            {
                hud.Refresh();
                hud.ShowLevelComplete();
            }

            HuntTweener.Delay(this, 0.9f, () => HuntPopups.Win(new HuntWinPayload
            {
                Title = "All Locked Down!",
                Stars = HuntEconomyRules.StarsFor(_clock.RemainingFraction, 0, HuntWallet.Tuning),
                Coins = 0,
                CanDouble = false,
                RequestDouble = done => done(0),
                NextLabel = "NEXT",
                OnNext = NextLevel,
                OnHome = ExitToHome
            }));
        }

        void TickClock()
        {
            if (!_running || !_clock.IsRunning)
                return;
            _clock.Tick(Time.deltaTime);
            var urgent = _clock.Remaining <= HuntRemoteConfig.UrgentThreshold;
            if (hud != null)
                hud.RefreshTimer(_clock.Remaining, urgent);
            var second = Mathf.CeilToInt(_clock.Remaining);
            if (urgent && second != _lastTickSecond && second > 0)
                HuntAudio.Clip(second <= 3 ? HuntTickSound.FinalTick : HuntTickSound.Tick);
            _lastTickSecond = second;
            if (_clock.IsExpired)
                TimedOut();
        }

        void TimedOut()
        {
            _running = false;
            _clock.Stop();
            _tracker.Reset();
            _reticle.Hide();
            SetAim(DriftLockTracker.None);
            HuntAudio.Sfx(NixinAudio.Fail);
            HuntHaptics.Heavy();
            var found = FoundCount();
            HuntAnalytics.LevelFail(_level.file, levelIndex, _attempt, "time_out", Time.time - _levelStartTime, found,
                _director.Current.TargetCount, _wrongPicks);
            if (hud != null)
                hud.ShowTimedOut();
            HuntTweener.Delay(this, 0.6f, () => HuntPopups.Fail(new HuntFailPayload
            {
                Found = found,
                Total = _director.Current.TargetCount,
                ContinueCost = 0,
                ContinueSeconds = 30f,
                OnContinueCoins = Continue,
                OnRetry = Restart,
                OnHome = ExitToHome
            }));
        }

        /// <summary>Lab build: continues are free.</summary>
        void Continue()
        {
            _clock.AddTime(30f);
            _running = true;
            _lastTickSecond = -1;
            if (hud != null)
            {
                hud.Refresh("+30s!");
                hud.RefreshTimer(_clock.Remaining, false);
            }
        }

        int FoundCount()
        {
            var session = _director.Session;
            return _director.WaveIndex * _director.Current.WaveSize + (session != null ? session.Found.Count : 0);
        }

        void Restart()
        {
            HuntAnalytics.LevelRestart(_level.file, levelIndex, _attempt);
            LoadLevel(levelIndex);
        }

        void NextLevel()
        {
            LoadLevel(levelIndex + 1);
        }

        void ExitToHome()
        {
            if (_running && _director != null)
                HuntAnalytics.LevelExit(_level.file, levelIndex, Time.time - _levelStartTime, FoundCount(), _director.Current.TargetCount);
            HuntFlow.GoHome();
        }

        void LoadLevel(int index)
        {
            HuntPopups.CloseAll();
            levelIndex = levelSet != null && levelSet.levels.Count > 0 ? ((index % levelSet.levels.Count) + levelSet.levels.Count) % levelSet.levels.Count : 0;
            _level = levelSet != null ? levelSet.Get(levelIndex) : new DriftLevel();
            _attempt++;
            _wrongPicks = 0;
            _combo = 0;
            _lastTickSecond = -1;
            _accumulator = 0f;

            var def = BuildDef(_level);
            _director = new HuntDirector(new HuntCatalog(new[] { def }));
            if (!BuildBoard(def, out var names))
            {
                HuntFlow.GoHome();
                return;
            }

            var cam = Camera.main;
            var root = transform.Find(MosaicAssembler.RootName);
            glass.SetRevealMode(false);
            glass.SetScanner(NixinScanners.Get(string.IsNullOrEmpty(def.Scanner) ? NixinScanners.Magnifier : def.Scanner));
            MosaicSaturationDriver.SetBaseSaturation(def.BaseSaturation);
            glass.SetGlassFogMode(def.GlassFog);
            glass.ApplyRules(def.MemoryFade, def.GlassRadius, def.Zoom);
            if (root != null && cam != null)
                glass.Bind(root, cam);
            glass.SetGlassFogMode(def.GlassFog);

            if (_reticle == null)
                _reticle = DriftLockReticle.Create(transform, MosaicGlass.MosaicLayer);
            _reticle.Hide();
            _tracker = new DriftLockTracker(_level.lockOn);

            _director.BeginLevel(0, names);
            _clock.Reset(def.TimeLimitSeconds);
            _levelStartTime = Time.time;
            _running = true;
            if (hud != null)
            {
                hud.SetStampCatalog(_catalog);
                hud.Bind(_director);
                hud.HideActionButtons();
                hud.SetPlayControls(false);
                hud.RefreshTimer(_clock.Remaining, false);
                hud.Refresh(Pointer.current is Mouse
                    ? "Hover over a target to lock, click to shoot"
                    : "Drag the lens over a target to lock, lift to shoot");
                hud.PopIcons();
            }

            HuntAudio.Sfx(NixinAudio.Shuffle);
            HuntAnalytics.LevelStart(def.FileName, levelIndex, "drift", _attempt, def.TimeLimitSeconds, def.TargetCount);
        }

        HuntLevelDef BuildDef(DriftLevel level)
        {
            HuntLevelDef campaign = null;
            var catalog = HuntRoadmapService.Catalog;
            for (var i = 0; catalog != null && i < catalog.Count; i++)
            {
                if (catalog[i].FileName == level.file)
                {
                    campaign = catalog[i];
                    break;
                }
            }

            var useOwn = level.overrideGlass || campaign == null;
            return new HuntLevelDef(
                level.file,
                level.title,
                level.waveSize,
                level.waveCount,
                useOwn ? level.memoryFade : campaign.MemoryFade,
                useOwn ? level.glassRadius : campaign.GlassRadius,
                useOwn ? level.zoom : campaign.Zoom,
                level.huntSizeScale,
                useOwn ? level.glassFog : campaign.GlassFog,
                level.timeLimitSeconds,
                campaign != null ? campaign.Toughness : 2,
                scanner: campaign != null ? campaign.Scanner : null,
                baseSaturation: level.baseSaturation,
                iconScale: level.iconScale);
        }

        bool BuildBoard(HuntLevelDef def, out List<string> names)
        {
            names = null;
            if (!HuntContentResources.TryLoadText(HuntCatalogLoader.SilhouetteFolder, def.FileName, out var json))
            {
                Debug.LogError("Drift: missing mosaic level " + def.FileName);
                return false;
            }

            var original = MosaicLevelCodec.Parse(json);
            _catalog = LoadCatalog(original);
            var seed = MosaicHuntSprinkler.SeedFor(original.LevelId + ":drift", _attempt);
            var sprinkled = MosaicHuntSprinklerUnity.Apply(original, _catalog, def.WaveSize * def.WaveCount, seed,
                huntSizeScale: def.HuntSizeScale);

            // The sprinkler culls fillers under each hunt icon; here every original filler stays so the
            // silhouette is fully packed and the icons have to shove their way through.
            var items = new List<MosaicItem>(original.Items.Count + def.TargetCount);
            for (var i = 0; i < original.Items.Count; i++)
            {
                var item = original.Items[i];
                items.Add(item.Hunt ? item.WithHunt(false) : item);
            }

            names = new List<string>();
            for (var i = 0; i < sprinkled.Items.Count; i++)
            {
                var item = sprinkled.Items[i];
                if (!item.Hunt)
                    continue;
                items.Add(item);
                if (!names.Contains(item.Name))
                    names.Add(item.Name);
            }

            if (names.Count == 0)
            {
                Debug.LogError("Drift: no hunt icons in the atlas for " + def.FileName);
                return false;
            }

            var level = MosaicDecoyFillUnity.Apply(original.WithItems(items), _catalog, _level.decoyPercent,
                _level.decoyTintKeep, _level.decoySize, seed * 31 + 7);
            var root = MosaicAssembler.Assemble(level, _catalog, transform);
            if (Mathf.Abs(def.IconScale - 1f) > 0.001f)
            {
                foreach (var v in root.GetComponentsInChildren<MosaicItemView>(false))
                    v.transform.localScale *= def.IconScale;
            }

            var cam = Camera.main;
            if (hud != null && cam != null)
                hud.ApplyPlayViewport(cam);
            MosaicAssembler.FrameCamera(cam, level);
            var background = HuntBackgrounds.Selected;
            if (cam != null && background != null)
                cam.backgroundColor = background.Field;

            BuildSwarm(root.transform, seed);
            return true;
        }

        void BuildSwarm(Transform root, int seed)
        {
            _fillers.Clear();
            _targets.Clear();
            _candidates.Clear();
            var fillerBodies = new List<DriftBody>();
            var targetBodies = new List<DriftBody>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var view = child.GetComponent<MosaicItemView>();
                var renderer = child.GetComponent<SpriteRenderer>();
                if (view == null || renderer == null || !child.gameObject.activeSelf)
                    continue;
                // Collisions use the physical tile; the sim drives transforms, not Physics2D.
                var collider = child.GetComponent<Collider2D>();
                if (collider != null)
                    collider.enabled = false;

                var extents = renderer.bounds.extents;
                var piece = new Piece
                {
                    View = view,
                    Transform = child,
                    BaseAngle = child.localEulerAngles.z,
                    BaseScale = child.localScale,
                    Radius = Mathf.Max(0.01f, Mathf.Max(extents.x, extents.y) * 0.9f)
                };
                var body = new DriftBody(child.localPosition.x, child.localPosition.y, piece.Radius);
                if (view.Hunt)
                {
                    _candidates.Add(new Candidate { Target = true, Index = _targets.Count });
                    _targets.Add(piece);
                    targetBodies.Add(body);
                }
                else
                {
                    _fillers.Add(piece);
                    fillerBodies.Add(body);
                }
            }

            _swarm = new DriftSwarm(fillerBodies, targetBodies, _level.motion, seed);
            _swarm.Prewarm(PrewarmSeconds, Substep);
            ApplyPoses();
        }

        void ApplyPoses()
        {
            for (var i = 0; i < _fillers.Count; i++)
            {
                _swarm.GetFiller(i, out var x, out var y, out var angle, out var scale);
                Pose(_fillers[i], x, y, angle, scale);
            }

            for (var i = 0; i < _targets.Count; i++)
            {
                // Shot targets belong to HuntFindFx's lift-off now.
                if (!_swarm.IsTargetAlive(i))
                    continue;
                _swarm.GetTarget(i, out var x, out var y, out var angle, out var scale);
                Pose(_targets[i], x, y, angle, scale);
            }
        }

        static void Pose(Piece piece, float x, float y, float angle, float scale)
        {
            var t = piece.Transform;
            t.localPosition = new Vector3(x, y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, piece.BaseAngle + angle);
            t.localScale = piece.BaseScale * scale;
        }

        int NearestCandidate()
        {
            var best = DriftLockTracker.None;
            var bestErr = 1f;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var err = ErrorOf(i);
                if (err >= bestErr)
                    continue;
                bestErr = err;
                best = i;
            }

            return best;
        }

        /// <summary>Candidate distance from the lens centre over the magnified view radius; infinity once shot.</summary>
        float ErrorOf(int id)
        {
            if (id < 0 || id >= _candidates.Count)
                return float.PositiveInfinity;
            var c = _candidates[id];
            if (!IsTrackable(c))
                return float.PositiveInfinity;
            CandidatePose(id, out var pos, out _);
            var dx = pos.x - _lensCenter.x;
            var dy = pos.y - _lensCenter.y;
            return Mathf.Sqrt(dx * dx + dy * dy) / _lensRadius;
        }

        /// <summary>
        /// Only a real target on the current wave's list can be tracked. Decoy icons, filler shapes and targets
        /// of later waves are ignored entirely: no tracking, no lock, no sound.
        /// </summary>
        bool IsTrackable(Candidate c)
        {
            if (!c.Target || !_swarm.IsTargetAlive(c.Index))
                return false;
            var session = _director.Session;
            if (session == null)
                return false;
            var remaining = session.Remaining;
            var name = _targets[c.Index].View.ItemName;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (remaining[i] == name)
                    return true;
            }

            return false;
        }

        void CandidatePose(int id, out Vector3 world, out float radius)
        {
            var c = _candidates[id];
            var piece = c.Target ? _targets[c.Index] : _fillers[c.Index];
            world = piece.Transform.position;
            radius = piece.Radius;
        }

        void SetAim(int id)
        {
            if (glass == null || glass.Saturation == null)
                return;
            if (id < 0 || id >= _candidates.Count)
            {
                glass.Saturation.AimTarget = null;
                return;
            }

            var c = _candidates[id];
            glass.Saturation.AimTarget = (c.Target ? _targets[c.Index] : _fillers[c.Index]).View;
        }

        bool PointerOverHud()
        {
            var es = EventSystem.current;
            if (es == null || Pointer.current == null)
                return false;
            var data = new PointerEventData(es) { position = Pointer.current.position.ReadValue() };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        static IMosaicStampCatalog LoadCatalog(MosaicLevel level)
        {
            if (string.IsNullOrEmpty(level.Atlas))
                return new PrimitiveMosaicStampCatalog();
            var parts = level.Atlas.Split(',');
            var keys = new List<string>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length > 0)
                    keys.Add(HuntContentResources.AtlasJsonKey(name));
            }

            return AtlasMosaicStampCatalog.TryLoadManyFromResources(keys, HuntContentResources.AtlasTextureKey)
                ?? (IMosaicStampCatalog)new PrimitiveMosaicStampCatalog();
        }
    }
}
