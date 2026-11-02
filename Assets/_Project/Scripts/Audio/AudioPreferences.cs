using System;
using UnityEngine;

namespace PointPass.Audio
{
    /// <summary>Local sound preferences. Does not change the OS or other applications' volume.</summary>
    public static class AudioPreferences
    {
        public const string VolumeKey = "PointPass.SoundVolume";
        public const string MutedKey = "PointPass.SoundMuted";
        private static float volume = ReadVolume();
        private static bool muted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
        public static float Volume => volume;
        public static bool Muted => muted;
        public static event Action Changed;

        public static void SetVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetMuted(bool value)
        {
            muted = value;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static float ReadVolume()
        {
            float saved = PlayerPrefs.GetFloat(VolumeKey, 0.65f);
            return float.IsNaN(saved) || float.IsInfinity(saved) ? 0.65f : Mathf.Clamp01(saved);
        }
    }
}
