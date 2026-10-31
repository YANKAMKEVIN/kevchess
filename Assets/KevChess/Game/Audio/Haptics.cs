using UnityEngine;

namespace KevChess.Game.Audio
{
    /// <summary>
    /// Vibrations courtes (Android) : sélection, pose, prise, échec. Désactivables dans le menu.
    /// Handheld.Vibrate (trop long) n'est utilisé qu'en secours : sa présence fait ajouter la permission VIBRATE.
    /// </summary>
    public static class Haptics
    {
        const string Key = "kevchess.haptics";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) == 1;
            set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Select() => Pulse(8, 60);
        public static void Land() => Pulse(16, 120);
        public static void Capture() => Pulse(32, 220);
        public static void Check() => Pulse(45, 255);
        public static void Illegal() => Pulse(22, 90);

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effectClass;
        static bool initialized, available;

        static void Pulse(long ms, int amplitude)
        {
            if (!Enabled) return;
            try
            {
                if (!initialized)
                {
                    initialized = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    available = vibrator != null && vibrator.Call<bool>("hasVibrator");
                }
                if (!available) return;
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, Mathf.Clamp(amplitude, 1, 255)))
                    vibrator.Call("vibrate", effect);
            }
            catch (System.Exception)
            {
                available = false;
                Handheld.Vibrate();
            }
        }
#else
        static void Pulse(long ms, int amplitude) { }
#endif
    }
}
