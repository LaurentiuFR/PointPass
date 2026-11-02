using UnityEngine;

namespace PointPass.Audio
{
    /// <summary>Deterministic, gently enveloped placeholder sounds; no external sound assets.</summary>
    public static class FootballSounds
    {
        public const int SampleRate = 24000;

        public static AudioClip Create(FootballAudio.Cue cue)
        {
            float duration = cue == FootballAudio.Cue.Pass ? 0.14f : cue == FootballAudio.Cue.Shot ? 0.2f
                : cue == FootballAudio.Cue.Whistle ? 0.62f : 0.95f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[count];
            uint noiseState = 473;
            float filteredNoise = 0;
            for (int i = 0; i < count; i++)
            {
                float time = (float)i / SampleRate;
                float fade = Mathf.Clamp01(time / 0.008f) * Mathf.Clamp01((duration - time) / 0.025f);
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = (noiseState & 65535) / 32767.5f - 1f;
                filteredNoise += (noise - filteredNoise) * 0.16f;
                float sample;
                if (cue == FootballAudio.Cue.Pass || cue == FootballAudio.Cue.Shot)
                {
                    bool shot = cue == FootballAudio.Cue.Shot;
                    float frequency = shot ? 95f : 145f;
                    float decay = Mathf.Exp(-time * (shot ? 28f : 38f));
                    sample = (Mathf.Sin(2f * Mathf.PI * frequency * time) * (shot ? 0.31f : 0.22f)
                        + filteredNoise * (shot ? 0.22f : 0.16f)) * decay;
                }
                else if (cue == FootballAudio.Cue.Whistle)
                {
                    float vibrato = Mathf.Sin(time * 2f * Mathf.PI * 22f) * 0.4f;
                    float envelope = 0.7f + 0.3f * Mathf.Sin(time * Mathf.PI / duration);
                    sample = Mathf.Sin(time * 2f * Mathf.PI * 2350f + vibrato) * 0.12f * envelope;
                }
                else
                {
                    float note = time < 0.28f ? 392f : time < 0.56f ? 493.88f : 587.33f;
                    float phraseTime = time < 0.28f ? time : time < 0.56f ? time - 0.28f : time - 0.56f;
                    float phraseDuration = time < 0.56f ? 0.28f : duration - 0.56f;
                    float phrase = Mathf.Clamp01(phraseTime / 0.018f) * Mathf.Clamp01((phraseDuration - phraseTime) / 0.025f)
                        * Mathf.Exp(-phraseTime * 3f);
                    sample = (Mathf.Sin(time * 2f * Mathf.PI * note) * 0.12f
                        + Mathf.Sin(time * 2f * Mathf.PI * note * 0.5f) * 0.06f) * phrase;
                }
                samples[i] = sample * fade;
            }
            AudioClip clip = AudioClip.Create("PointPass " + cue + " placeholder", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
