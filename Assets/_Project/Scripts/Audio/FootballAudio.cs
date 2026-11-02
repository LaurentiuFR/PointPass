using System;
using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Audio
{
    /// <summary>One event-driven 2D sound source for the live session, separate from replay actors.</summary>
    public class FootballAudio : MonoBehaviour
    {
        public enum Cue { Pass, Shot, Whistle, Goal }
        private readonly AudioClip[] clips = new AudioClip[4];
        private readonly List<PlayerKicker> kickers = new List<PlayerKicker>();
        private AudioSource source;
        private MatchManager manager;
        private bool focused = true;
        private bool applicationPaused;
        private bool suspended;
        public event Action<Cue> Played;

        public void Init(Ball ball, List<PlayerController> roster, MatchManager match, PossessionSystem possession)
        {
            Unsubscribe();
            manager = match;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
            source.priority = 96; source.dopplerLevel = 0f;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] == null) clips[i] = FootballSounds.Create((Cue)i);
            if (roster != null) foreach (PlayerController player in roster)
            {
                PlayerKicker kicker = player != null ? player.GetComponent<PlayerKicker>() : null;
                if (kicker == null || kickers.Contains(kicker)) continue;
                kicker.Kicked += OnKick;
                kickers.Add(kicker);
            }
            if (manager != null)
            {
                manager.GoalScored += OnGoal;
                manager.MatchWhistle += OnWhistle;
            }
            AudioPreferences.Changed += ApplyPreferences;
            ApplyPreferences();
        }

        private void OnKick(PlayerKicker.KickType type)
        {
            if (type == PlayerKicker.KickType.Pass) Play(Cue.Pass);
            else if (type == PlayerKicker.KickType.Shot) Play(Cue.Shot);
        }
        private void OnGoal(Team team) => Play(Cue.Goal);
        private void OnWhistle() => Play(Cue.Whistle);

        public void Play(Cue cue)
        {
            if (source == null || !isActiveAndEnabled || !CanPlay() || AudioPreferences.Muted || AudioPreferences.Volume <= 0) return;
            int index = (int)cue;
            if (index < 0 || index >= clips.Length || clips[index] == null) return;
            // One voice prevents overlapping repeated whistles or stacked goal jingles.
            source.clip = clips[index];
            source.Play();
            Played?.Invoke(cue);
        }

        private bool CanPlay() => focused && !applicationPaused && Time.timeScale > 0f
            && (manager == null || (!manager.IsUserPaused && !manager.IsInMainMenu));

        private void Update()
        {
            if (source == null) return;
            if (manager != null && manager.IsInMainMenu)
            {
                if (source.isPlaying || suspended) source.Stop();
                suspended = false;
                return;
            }
            bool pause = !CanPlay();
            if (pause == suspended) return;
            suspended = pause;
            if (suspended) source.Pause();
            else source.UnPause();
        }

        private void ApplyPreferences()
        {
            if (this == null) { AudioPreferences.Changed -= ApplyPreferences; return; }
            if (source == null) return;
            source.volume = AudioPreferences.Volume;
            source.mute = AudioPreferences.Muted;
            if (AudioPreferences.Muted || AudioPreferences.Volume <= 0f) source.Stop();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && source != null) { source.Pause(); suspended = true; }
        }
        private void OnApplicationPause(bool value)
        {
            applicationPaused = value;
            if (value && source != null) { source.Pause(); suspended = true; }
        }
        private void OnDisable() { if (source != null) source.Stop(); }
        private void Unsubscribe()
        {
            foreach (PlayerKicker kicker in kickers) if (kicker != null) kicker.Kicked -= OnKick;
            kickers.Clear();
            if (manager != null) { manager.GoalScored -= OnGoal; manager.MatchWhistle -= OnWhistle; }
            AudioPreferences.Changed -= ApplyPreferences;
        }
        private void OnDestroy()
        {
            Unsubscribe();
            foreach (AudioClip clip in clips)
                if (clip != null) { if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip); }
        }
    }
}
