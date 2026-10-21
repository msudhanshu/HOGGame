using UnityEngine;

namespace Game.Unity
{
    /// <summary>Light / medium / heavy vibration, gated by HuntSettings.Haptics.</summary>
    public static class HuntHaptics
    {
        public static void Light() => Vibrate(12, 60);
        public static void Medium() => Vibrate(25, 140);
        public static void Heavy() => Vibrate(60, 255);

        static void Vibrate(long ms, int amplitude)
        {
            if (!HuntSettings.Haptics)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Android(ms, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            if (amplitude >= 200)
                Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk = -1;

        static void Android(long ms, int amplitude)
        {
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                }

                if (_vibrator == null)
                    return;
                if (_sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", oneShot);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception)
            {
                // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission.
                _vibrator = null;
                if (amplitude >= 200)
                    Handheld.Vibrate();
            }
        }
#endif
    }
}
