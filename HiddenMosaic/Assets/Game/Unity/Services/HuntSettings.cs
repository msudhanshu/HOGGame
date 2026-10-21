using System;
using UnityEngine;

namespace Game.Unity
{
    /// <summary>Player settings (PlayerPrefs). Music / SFX / haptics toggles.</summary>
    public static class HuntSettings
    {
        const string MusicKey = "hunt.settings.music";
        const string SfxKey = "hunt.settings.sfx";
        const string HapticsKey = "hunt.settings.haptics";

        public static event Action Changed;

        public static bool Music
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set => Set(MusicKey, value);
        }

        public static bool Sfx
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set => Set(SfxKey, value);
        }

        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set => Set(HapticsKey, value);
        }

        static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
