using Nixin.Audio;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Persistent music loop + one-shot SFX, gated by HuntSettings. Music clip comes from
    /// HuntUiTheme.music (none assigned = silence).</summary>
    public sealed class HuntAudio : MonoBehaviour
    {
        static HuntAudio _instance;
        AudioSource _music;
        AudioSource _sfx;

        static HuntAudio Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                var go = new GameObject("HuntAudio");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HuntAudio>();
                return _instance;
            }
        }

        void Awake()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = 0.45f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            HuntSettings.Changed += ApplySettings;
        }

        void OnDestroy()
        {
            HuntSettings.Changed -= ApplySettings;
        }

        /// <summary>Starts the theme music if enabled (safe to call every scene).</summary>
        public static void EnsureMusic()
        {
            Instance.ApplySettings();
        }

        void ApplySettings()
        {
            var clip = HuntUiTheme.Current.music;
            if (clip == null || !HuntSettings.Music)
            {
                if (_music.isPlaying)
                    _music.Stop();
                return;
            }

            if (_music.clip != clip)
                _music.clip = clip;
            if (!_music.isPlaying)
                _music.Play();
        }

        /// <summary>path = a NixinAudio constant (e.g. NixinAudio.Win).</summary>
        public static void Sfx(string path, float pitch = 1f, float volume = 1f)
        {
            if (string.IsNullOrEmpty(path))
                return;
            Clip(NixinAudio.Load(path), pitch, volume);
        }

        public static void Clip(AudioClip clip, float pitch = 1f, float volume = 1f)
        {
            if (clip == null || !HuntSettings.Sfx)
                return;
            var src = Instance._sfx;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }
    }
}
