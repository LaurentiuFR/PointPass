using System;
using System.Collections.Generic;
using System.Reflection;
using PointPass.Audio;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using PointPass.UI;
using UnityEngine;

namespace PointPass.Validation
{
    public static class AudioFeedbackValidation
    {
        public static void Run(Action<bool, string> check)
        {
            bool hadVolume = PlayerPrefs.HasKey(AudioPreferences.VolumeKey);
            bool hadMute = PlayerPrefs.HasKey(AudioPreferences.MutedKey);
            float originalVolume = AudioPreferences.Volume;
            bool originalMute = AudioPreferences.Muted;
            float timeScale = Time.timeScale;
            GameObject root = new GameObject("Audio feedback validation");
            FootballAudio audio = null;
            var generated = new List<AudioClip>();
            try
            {
                var lengths = new HashSet<int>();
                foreach (FootballAudio.Cue cue in Enum.GetValues(typeof(FootballAudio.Cue)))
                {
                    AudioClip clip = FootballSounds.Create(cue); generated.Add(clip);
                    float[] data = new float[clip.samples];
                    bool readable = clip.GetData(data, 0);
                    float energy = 0, peak = 0;
                    bool finite = true;
                    foreach (float sample in data)
                    {
                        finite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                        peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample;
                    }
                    check(readable && finite && peak > 0.04f && peak < 0.5f && energy > 0.1f
                        && Mathf.Abs(data[0]) < 0.001f && Mathf.Abs(data[data.Length - 1]) < 0.002f,
                        cue + " placeholder has finite audible samples, bounded peaks and soft endpoints");
                    lengths.Add(clip.samples);
                }
                check(lengths.Count == 4 && generated[0].channels == 1 && generated[0].frequency == FootballSounds.SampleRate,
                    "Four compact mono football cues have distinct durations");

                AudioPreferences.SetVolume(2);
                check(AudioPreferences.Volume == 1 && PlayerPrefs.GetFloat(AudioPreferences.VolumeKey) == 1,
                    "Sound volume clamps and persists locally");
                AudioPreferences.SetVolume(float.NaN);
                check(AudioPreferences.Volume == 1, "Invalid sound volume cannot poison the source");
                AudioPreferences.SetVolume(0.45f); AudioPreferences.SetMuted(true);
                check(AudioPreferences.Muted && AudioPreferences.Volume == 0.45f && PlayerPrefs.GetInt(AudioPreferences.MutedKey) == 1,
                    "Mute persists independently without destroying the selected volume");
                AudioPreferences.SetMuted(false); Time.timeScale = 1;

                GameObject ballObject = new GameObject("Ball", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Ball));
                ballObject.transform.SetParent(root.transform); Ball ball = ballObject.GetComponent<Ball>(); Call(ball, "Awake");
                GameObject playerObject = new GameObject("Player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(PlayerMovement), typeof(PlayerController), typeof(Dribbler), typeof(PlayerKicker));
                playerObject.transform.SetParent(root.transform);
                PlayerController player = playerObject.GetComponent<PlayerController>(); player.Init(Team.Blue, Vector2.zero, -90);
                Dribbler dribbler = playerObject.GetComponent<Dribbler>(); PlayerKicker kicker = playerObject.GetComponent<PlayerKicker>();
                Call(dribbler, "Awake"); Call(kicker, "Awake");
                var possession = new PossessionSystem(); dribbler.Init(ball, possession, null); kicker.Init(ball, possession);
                MatchManager manager = root.AddComponent<MatchManager>();
                audio = root.AddComponent<FootballAudio>();
                var roster = new List<PlayerController> { player, player };
                audio.Init(ball, roster, manager, possession);
                audio.Init(ball, roster, manager, possession);
                int emitted = 0; FootballAudio.Cue last = FootballAudio.Cue.Whistle;
                audio.Played += cue => { emitted++; last = cue; };
                AudioSource source = audio.GetComponent<AudioSource>();
                check(source != null && !source.playOnAwake && source.spatialBlend == 0 && Mathf.Approximately(source.volume, 0.45f * 0.9f)
                    && root.GetComponents<AudioSource>().Length == 5,
                    "Live football audio reuses five bounded 2D voices with master-volume headroom");
                possession.TryAcquire(player); player.BeginKick(PlayerKicker.KickType.Pass);
                player.ReleaseKick(PlayerKicker.KickType.Pass); Call(kicker, "FixedUpdate"); Call(kicker, "FixedUpdate");
                check(emitted == 1 && last == FootballAudio.Cue.Pass,
                    "A physical pass emits one cue even after repeated Init and duplicate roster references");
                possession.TryAcquire(player); player.BeginKick(PlayerKicker.KickType.Shot); player.CancelKick();
                player.ReleaseKick(PlayerKicker.KickType.Shot); Call(kicker, "FixedUpdate");
                check(emitted == 1, "Cancelled charges never emit a kick sound");
                AudioPreferences.SetMuted(true); audio.Play(FootballAudio.Cue.Goal);
                check(emitted == 1 && source.mute, "Mute stops the voice and suppresses new football cues");
                AudioPreferences.SetMuted(false); Time.timeScale = 0; audio.Play(FootballAudio.Cue.Whistle);
                check(emitted == 1, "Paused game time cannot trigger a new whistle or goal cue");
                Time.timeScale = 1; Set(manager, "userPaused", true);
                Call(audio, "OnApplicationFocus", false); Call(audio, "OnApplicationFocus", true);
                audio.Play(FootballAudio.Cue.Shot);
                check(emitted == 1, "Regaining focus does not re-enable sound until the paused match resumes");
                Set(manager, "userPaused", false); audio.Play(FootballAudio.Cue.Shot);
                check(emitted == 2 && last == FootballAudio.Cue.Shot,
                    "A resumed match can play the distinct shot cue normally");

                GameObject page = new GameObject("Audio settings page", typeof(RectTransform));
                page.transform.SetParent(root.transform);
                AudioSettingsView view = page.AddComponent<AudioSettingsView>(); view.Build();
                var slider = page.GetComponentInChildren<UnityEngine.UI.Slider>();
                var toggle = page.GetComponentInChildren<UnityEngine.UI.Toggle>();
                slider.value = 0.25f; toggle.isOn = true;
                check(AudioPreferences.Volume == 0.25f && AudioPreferences.Muted,
                    "Audio settings slider and mute toggle update saved preferences");
            }
            finally
            {
                if (audio != null && !Application.isPlaying) Call(audio, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
                foreach (AudioClip clip in generated) if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                Time.timeScale = timeScale;
                AudioPreferences.SetVolume(originalVolume); AudioPreferences.SetMuted(originalMute);
                if (!hadVolume) PlayerPrefs.DeleteKey(AudioPreferences.VolumeKey);
                if (!hadMute) PlayerPrefs.DeleteKey(AudioPreferences.MutedKey);
                PlayerPrefs.Save();
            }
        }
        private static void Call(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
