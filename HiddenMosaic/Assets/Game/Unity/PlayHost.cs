using System.Collections.Generic;
using Game.Core;
using Nixin.Audio;
using Nixin.Game.Core;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
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
        AudioSource _audio;
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

        [SerializeField] [Range(0.25f, 0.85f)] float centerPickRadiusFactor = 0.52f;

        MosaicItemView _aimView;

        public HuntDirector Director => _director;

        void Start()
        {
            HuntUiInput.EnsureEventSystem();
            _audio = gameObject.GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

            _director = new HuntDirector(HuntRemoteConfig.Apply(HuntCatalogLoader.Load(catalogAsset)));
            _launchMode = HuntLaunchRequest.Mode;
            var startIndex = HuntLaunchRequest.StartLevelIndex;
            if (startIndex < 0 || startIndex >= _director.Catalog.Count)
                startIndex = 0;

            if (hud != null)
            {
                hud.EnsureReady();
                hud.Bind(_director);
                hud.NextRequested.AddListener(LoadNextLevel);
                hud.RetryRequested.AddListener(RetryCurrentLevel);
                hud.MenuRequested.AddListener(ExitToHome);
                hud.HomeRequested.AddListener(ExitToHome);
                hud.BackRequested.AddListener(ExitToHome);
            }

            GameServices.Analytics.TrackScreenView("HuntPlayScreen");
            LoadLevel(_director.Catalog[startIndex], startIndex);
        }

        void Update()
        {
            TickClock();
        }

        void LateUpdate()
        {
            if (_director == null || _director.IsLevelComplete || _timedOut)
                return;
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
            if (_director == null || _director.IsLevelComplete || _timedOut || !_clock.IsRunning)
                return;
            _clock.Tick(Time.deltaTime);
            if (hud != null)
            {
                var urgent = _clock.Remaining <= HuntRemoteConfig.UrgentThreshold;
                hud.RefreshTimer(_clock.Remaining, urgent);
            }

            if (_clock.IsExpired)
                OnTimeExpired();
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
        }

        void RetryCurrentLevel()
        {
            if (_director == null || _director.Current == null)
                return;
            HuntAnalytics.LevelRestart(_director.Current.FileName, _currentLevelIndex, _attemptByLevel[_currentLevelIndex]);
            LoadLevel(_director.Current, _currentLevelIndex);
        }

        void ExitToHome()
        {
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
            HuntLaunchRequest.ResetToDefaults();
            SceneManager.LoadScene(HuntScenes.Home);
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

            if (!_director.TryFind(view.ItemName))
            {
                _wrongPicks++;
                HuntWrongFx.Play(view, camera, hud != null ? hud.GetComponent<Canvas>() : null, this);
                if (hud != null)
                    hud.Refresh("Not on the list.");
                return;
            }

            var hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
            HuntFindFx.Play(view, camera, hudCanvas, this);

            PlayClip(_director.IsLevelComplete ? NixinAudio.Win : NixinAudio.Click);
            var elapsed = Time.time - _levelStartTime;
            HuntAnalytics.Found(_director.Current.FileName, view.ItemName, elapsed);
            if (_director.IsLevelComplete)
            {
                _clock.Stop();
                _levelEnded = true;
                HuntAnalytics.LevelComplete(_director.Current.FileName, _currentLevelIndex,
                    _attemptByLevel[_currentLevelIndex], elapsed, _wrongPicks);
            }
            if (hud != null)
                RefreshHudAfterFind();
        }

        void RefreshHudAfterFind()
        {
            if (_director.IsCampaignComplete)
            {
                hud.Refresh();
                hud.ShowCampaignComplete();
                return;
            }

            if (_director.IsLevelComplete && _launchMode == HuntLaunchMode.Single)
            {
                hud.Refresh();
                hud.ShowLevelCompleteSingle();
                return;
            }

            hud.Refresh();
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
            var centerThreshold = Mathf.Max(0.02f, glassRadius * centerPickRadiusFactor);
            var searchRadius = Mathf.Max(centerThreshold, glassRadius * 1.05f);
            var hits = Physics2D.OverlapCircleAll(new Vector2(glassCenter.x, glassCenter.y), searchRadius);
            MosaicItemView best = null;
            var bestCenterDist = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;
                var view = hit.GetComponent<MosaicItemView>();
                if (view == null || !view.Hunt)
                    continue;

                var pos = view.transform.position;
                var centerDist = Vector2.Distance(
                    new Vector2(glassCenter.x, glassCenter.y),
                    new Vector2(pos.x, pos.y));
                if (centerDist > centerThreshold)
                    continue;
                if (centerDist >= bestCenterDist)
                    continue;
                bestCenterDist = centerDist;
                best = view;
            }

            return best;
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
            if (_director == null || !_director.HasNextLevel)
                return;
            var next = _director.Catalog[_director.LevelIndex + 1];
            LoadLevel(next, _director.LevelIndex + 1);
        }

        void LoadLevel(HuntLevelDef def, int index)
        {
            _currentLevelIndex = index;
            _timedOut = false;
            _levelEnded = false;
            _wrongPicks = 0;
            _levelStartTime = Time.time;
            _clock.Reset(def.TimeLimitSeconds);
            if (hud != null)
                hud.HideActionButtons();

            if (!HuntContentResources.TryLoadText(HuntCatalogLoader.SilhouetteFolder, def.FileName, out var json))
            {
                Debug.LogError(
                    "Missing mosaic level resource "
                    + HuntContentResources.TextKey(HuntCatalogLoader.SilhouetteFolder, def.FileName));
                return;
            }

            var level = MosaicLevelCodec.Parse(json);
            _catalog = LoadCatalog(level);

            if (!_attemptByLevel.TryGetValue(index, out var attempt))
                attempt = 0;
            _attemptByLevel[index] = attempt + 1;
            _layoutGeneration++;
            var huntCount = def.WaveSize * def.WaveCount;
            var sprinkleAttempt = _attemptByLevel[index];
            var seed = MosaicHuntSprinkler.SeedFor(
                level.LevelId + ":" + def.FileName,
                sprinkleAttempt + def.SprinkleSeed + (_layoutGeneration * 4861));
            level = MosaicHuntSprinklerUnity.Apply(level, _catalog, huntCount, seed, huntSizeScale: def.HuntSizeScale);

            MosaicAssembler.Assemble(level, _catalog, transform);

            var cam = Camera.main;
            if (hud != null && cam != null)
                hud.ApplyPlayViewport(cam);
            MosaicAssembler.FrameCamera(cam, level);

            var root = transform.Find(MosaicAssembler.RootName);
            if (glass == null)
                glass = GetComponent<MosaicGlass>();
            if (glass == null)
                glass = gameObject.AddComponent<MosaicGlass>();
            glass.SetGlassFogMode(def.GlassFog);
            glass.ApplyRules(def.MemoryFade, def.GlassRadius, def.Zoom);
            if (root != null && cam != null)
                glass.Bind(root, cam);
            glass.SetGlassFogMode(def.GlassFog);

            _director.BeginLevel(index, UniqueNames(level));
            ClearAimHighlight();
            if (hud != null)
            {
                hud.SetStampCatalog(_catalog);
                hud.Bind(_director);
                hud.RefreshTimer(_clock.Remaining, false);
            }
            PlayClip(NixinAudio.Shuffle);
            HuntAnalytics.LevelStart(def.FileName, index, _launchMode.ToString(), _attemptByLevel[index],
                def.TimeLimitSeconds, def.TargetCount);
        }

        void PlayClip(string id)
        {
            var clip = NixinAudio.Load(id);
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
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
                if (name.Length == 0)
                    continue;
                keys.Add(HuntContentResources.AtlasJsonKey(name));
            }

            var atlas = AtlasMosaicStampCatalog.TryLoadManyFromResources(
                keys,
                HuntContentResources.AtlasTextureKey);
            if (atlas != null)
                return atlas;
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
