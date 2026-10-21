using UnityEngine;

namespace Game.Unity
{
    /// <summary>Procedural countdown tick clips (no audio assets needed).</summary>
    public static class HuntTickSound
    {
        static AudioClip _tick;
        static AudioClip _finalTick;

        public static AudioClip Tick => _tick != null ? _tick : _tick = Make("HuntTick", 1500f, 0.06f, 0.5f);
        public static AudioClip FinalTick => _finalTick != null ? _finalTick : _finalTick = Make("HuntTickFinal", 2300f, 0.09f, 0.8f);

        static AudioClip Make(string name, float freq, float seconds, float volume)
        {
            const int rate = 44100;
            var count = Mathf.CeilToInt(rate * seconds);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)rate;
                var envelope = Mathf.Exp(-t * 55f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
