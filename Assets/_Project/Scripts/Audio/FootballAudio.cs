using System;
using System.Collections.Generic;
using PointPass.Core;
using PointPass.Gameplay;
using PointPass.Players;
using UnityEngine;

namespace PointPass.Audio
{
    /// <summary>Bounded match voices: ball effects, goal sting and opposing supporter sections.</summary>
    public class FootballAudio : MonoBehaviour
    {
        public enum Cue { Pass, Shot, Whistle, Goal }
        private readonly AudioClip[] clips = new AudioClip[4];
        private readonly List<PlayerKicker> kickers = new List<PlayerKicker>();
        private AudioSource source;
        private AudioSource goalSource, supporters, opposition, applause;
        private AudioSource[] voices = Array.Empty<AudioSource>();
        private AudioClip cheerClip, disappointedClip, applauseClip;
        private Coroutine celebration;
        private float crowdAge = 10f;
        private int celebrationNumber;
        public Team CelebratingTeam { get; private set; }
        public int CrowdVariation { get; private set; }
        public int CrowdCelebrations { get; private set; }
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
            if (goalSource == null) goalSource = Voice(64);
            if (supporters == null) supporters = Voice(100);
            if (opposition == null) opposition = Voice(110);
            if (applause == null) applause = Voice(115);
            voices = new[] { source, goalSource, supporters, opposition, applause };
            cheerClip = Resources.Load<AudioClip>("Audio/goal_cheer");
            disappointedClip = Resources.Load<AudioClip>("Audio/crowd_disappointed");
            applauseClip = Resources.Load<AudioClip>("Audio/crowd_applause");
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
        private void OnGoal(Team team) => PlayGoal(team);

        /// <summary>Shared goal presentation for authoritative events and received goal snapshots.</summary>
        public void PlayGoal(Team team)
        {
            Play(Cue.Goal);
            if (!CanPlay() || !isActiveAndEnabled || AudioPreferences.Muted || AudioPreferences.Volume <= 0) return;
            if (celebration != null) StopCoroutine(celebration);
            StopCrowd();
            CelebratingTeam = team; CrowdVariation = celebrationNumber++ % 4;
            CrowdCelebrations++; crowdAge = 0;
            celebration = StartCoroutine(CrowdReaction(team, CrowdVariation));
        }
        private void OnWhistle() => Play(Cue.Whistle);

        public void Play(Cue cue)
        {
            if (source == null || !isActiveAndEnabled || !CanPlay() || AudioPreferences.Muted || AudioPreferences.Volume <= 0) return;
            int index = (int)cue;
            if (index < 0 || index >= clips.Length || clips[index] == null) return;
            AudioSource voice = cue == Cue.Goal ? goalSource : source;
            voice.clip = clips[index];
            voice.Play();
            Played?.Invoke(cue);
        }

        private bool CanPlay() => focused && !applicationPaused && Time.timeScale > 0f
            && (manager == null || (!manager.IsUserPaused && !manager.IsInMainMenu));

        private void Update()
        {
            if (source == null) return;
            if (manager != null && manager.IsInMainMenu)
            {
                StopVoices();
                suspended = false;
                return;
            }
            if (CanPlay() && crowdAge < 6f)
            {
                crowdAge += Time.deltaTime;
                // Leave space for the next kick-off instead of carrying a full-volume cheer into play.
                float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.2f, 4.5f, crowdAge));
                SetCrowdLevels(fade);
            }
            bool pause = !CanPlay();
            if (pause == suspended) return;
            suspended = pause;
            foreach (AudioSource voice in Voices())
                if (voice != null) { if (suspended) voice.Pause(); else voice.UnPause(); }
        }

        private void ApplyPreferences()
        {
            if (this == null) { AudioPreferences.Changed -= ApplyPreferences; return; }
            if (source == null) return;
            source.volume = AudioPreferences.Volume * 0.9f;
            goalSource.volume = AudioPreferences.Volume * 0.85f;
            SetCrowdLevels(1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.2f, 4.5f, crowdAge)));
            foreach (AudioSource voice in Voices()) if (voice != null) voice.mute = AudioPreferences.Muted;
            if (AudioPreferences.Muted || AudioPreferences.Volume <= 0f) StopVoices();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { foreach (AudioSource voice in Voices()) if (voice != null) voice.Pause(); suspended = true; }
        }
        private void OnApplicationPause(bool value)
        {
            applicationPaused = value;
            if (value) { foreach (AudioSource voice in Voices()) if (voice != null) voice.Pause(); suspended = true; }
        }
        private AudioSource Voice(int priority)
        {
            AudioSource voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0; voice.dopplerLevel = 0; voice.priority = priority;
            return voice;
        }
        private AudioSource[] Voices() => voices;
        private System.Collections.IEnumerator CrowdReaction(Team team, int variation)
        {
            yield return new WaitForSeconds(0.10f + variation * 0.015f);
            if (!CanPlay() || AudioPreferences.Muted || AudioPreferences.Volume <= 0) yield break;
            float section = team == Team.Blue ? -0.24f : 0.24f;
            supporters.panStereo = section; opposition.panStereo = -section; applause.panStereo = section * 0.7f;
            supporters.pitch = new[] { 0.98f, 1.025f, 1f, 0.955f }[variation];
            opposition.pitch = 1.035f - variation * 0.02f;
            applause.pitch = 0.97f + variation * 0.025f;
            StartSample(supporters, cheerClip, variation * 0.055f);
            yield return new WaitForSeconds(0.06f);
            if (!CanPlay() || AudioPreferences.Muted || AudioPreferences.Volume <= 0) yield break;
            StartSample(opposition, disappointedClip, 0);
            yield return new WaitForSeconds(0.12f + variation * 0.025f);
            if (CanPlay() && !AudioPreferences.Muted && AudioPreferences.Volume > 0)
                StartSample(applause, applauseClip, variation * 0.04f);
            celebration = null;
        }
        private static void StartSample(AudioSource voice, AudioClip clip, float offset)
        {
            if (clip == null) return;
            voice.clip = clip; voice.time = Mathf.Min(offset, clip.length * 0.1f); voice.Play();
        }
        private void SetCrowdLevels(float fade)
        {
            float level = AudioPreferences.Volume * fade;
            if (supporters != null) supporters.volume = level * 0.8f;
            if (opposition != null) opposition.volume = level * 0.2f;
            if (applause != null) applause.volume = level * 0.14f;
        }
        private void StopCrowd()
        {
            if (supporters != null) supporters.Stop(); if (opposition != null) opposition.Stop(); if (applause != null) applause.Stop();
        }
        private void StopVoices()
        {
            if (celebration != null) { StopCoroutine(celebration); celebration = null; }
            foreach (AudioSource voice in Voices()) if (voice != null) voice.Stop();
            crowdAge = 10;
        }
        private void OnDisable() => StopVoices();
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
