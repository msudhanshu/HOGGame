using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Nixin.Audio;
using Nixin.Mosaic;
using Nixin.Mosaic.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Unity
{
    public sealed class PlayHost : MonoBehaviour
    {
        [SerializeField] HuntHud hud;
        [SerializeField] MosaicGlass glass;
        [SerializeField] HuntCatalogAsset catalogAsset;

        HuntDirector _director;
        IMosaicStampCatalog _catalog;
        AudioSource _audio;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);

        public HuntDirector Director => _director;

        void Start()
        {
            _audio = gameObject.GetComponent<AudioSource>();
            if (_audio == null)
                _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;

            _director = new HuntDirector(HuntCatalogLoader.Load(catalogAsset));
            if (hud != null)
            {
                hud.Bind(_director);
                hud.NextRequested.AddListener(LoadNextLevel);
            }

            LoadLevel(_director.Catalog[0], 0);
        }

        void Update()
        {
            if (_director == null || _director.IsLevelComplete)
                return;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;
            if (PointerOverHud())
                return;
            var camera = Camera.main;
            if (camera == null)
                return;
            Vector3 screen = pointer.position.ReadValue();
            screen.z = -camera.transform.position.z;
            var world = camera.ScreenToWorldPoint(screen);
            var view = HitStamp(world);
            if (view == null)
                return;
            if (glass != null && !glass.IsRevealed(world))
                return;
            if (!_director.TryFind(view.ItemName))
            {
                if (hud != null)
                    hud.Refresh("That's a " + view.ItemName + " - click a name on the list.");
                return;
            }

            var collider = view.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
            var renderer = view.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                var c = renderer.color;
                renderer.color = new Color(c.r, c.g, c.b, 0.28f);
                StartCoroutine(Punch(view.transform));
            }

            PlayClip(_director.IsLevelComplete ? NixinAudio.Win : NixinAudio.Click);
            if (hud != null)
                hud.Refresh();
        }

        MosaicItemView HitStamp(Vector3 world)
        {
            var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            MosaicItemView best = null;
            var bestArea = float.MaxValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;
                var view = hit.GetComponent<MosaicItemView>();
                if (view == null)
                    continue;
                var size = hit.bounds.size;
                var area = size.x * size.y;
                if (area >= bestArea)
                    continue;
                bestArea = area;
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
            var path = HuntCatalogLoader.SilhouettePath(def.FileName);
            if (!File.Exists(path))
            {
                Debug.LogError("Missing mosaic level at " + path);
                return;
            }

            var level = MosaicLevelCodec.Parse(File.ReadAllText(path));
            _catalog = LoadCatalog(level, path);
            MosaicAssembler.Assemble(level, _catalog, transform);
            MosaicAssembler.FrameCamera(Camera.main, level);

            var root = transform.Find(MosaicAssembler.RootName);
            if (glass == null)
                glass = GetComponent<MosaicGlass>();
            if (glass == null)
                glass = gameObject.AddComponent<MosaicGlass>();
            glass.ApplyRules(def.MemoryFade, def.GlassRadius, def.Zoom);
            if (root != null && Camera.main != null)
                glass.Bind(root, Camera.main);

            _director.BeginLevel(index, UniqueNames(level));
            if (hud != null)
                hud.Bind(_director);
            PlayClip(NixinAudio.Shuffle);
        }

        void PlayClip(string id)
        {
            var clip = NixinAudio.Load(id);
            if (clip != null && _audio != null)
                _audio.PlayOneShot(clip);
        }

        static IEnumerator Punch(Transform target)
        {
            var from = target.localScale;
            var to = from * 1.35f;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 8f;
                target.localScale = Vector3.Lerp(to, from, t);
                yield return null;
            }

            target.localScale = from;
        }

        static IMosaicStampCatalog LoadCatalog(MosaicLevel level, string jsonPath)
        {
            if (!string.IsNullOrEmpty(level.Atlas))
            {
                var dir = Path.GetDirectoryName(jsonPath) ?? "";
                var atlasPath = Path.Combine(dir, level.Atlas);
                if (!File.Exists(atlasPath))
                    atlasPath = Path.Combine(Application.streamingAssetsPath, "Atlases", level.Atlas);
                var atlas = AtlasMosaicStampCatalog.TryLoad(atlasPath);
                if (atlas != null)
                    return atlas;
            }

            return new PrimitiveMosaicStampCatalog();
        }

        static List<string> UniqueNames(MosaicLevel level)
        {
            var names = new List<string>();
            for (var i = 0; i < level.Items.Count; i++)
            {
                var name = level.Items[i].Name;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name);
            }

            if (names.Count == 0)
                names.Add(MosaicStampNames.Circle);
            return names;
        }
    }
}
