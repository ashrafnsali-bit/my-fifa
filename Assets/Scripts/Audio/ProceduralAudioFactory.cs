using UnityEngine;

namespace Football.Audio
{
    /// <summary>
    /// Generates high-fidelity procedural audio waveforms directly in memory:
    /// authentic leather ball impacts, referee whistles, metallic goalpost strikes,
    /// and immersive World Cup stadium crowd atmospheres.
    /// </summary>
    public static class ProceduralAudioFactory
    {
        public static AudioClip CreateKickBallClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // 1. Initial leather strike snap (burst in first 18ms)
                float snap = (Random.value * 2f - 1f) * Mathf.Exp(-progress * 42f) * 0.55f;

                // 2. Punchy resonant bass thump (pitch drops from 155Hz to 48Hz)
                float freq = Mathf.Lerp(155f, 48f, Mathf.Sqrt(progress));
                float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-progress * 16f);

                // 3. Sub-bass body
                float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * Mathf.Exp(-progress * 20f) * 0.4f;

                samples[i] = Mathf.Clamp(snap + thump * 0.85f + sub, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_KickBall", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateRefereeWhistleClip()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float f1 = 3080f;
            float f2 = 3390f;
            float flutterFreq = 22f; // Beat frequency flutter

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Attack and release envelope
                float env;
                if (progress < 0.05f) env = progress / 0.05f;
                else if (progress > 0.75f) env = (1.0f - progress) / 0.25f;
                else env = 1.0f;

                // Flutter modulation
                float flutter = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * flutterFreq * t);

                // Dual-tone pea whistle resonance
                float tone1 = Mathf.Sin(2f * Mathf.PI * f1 * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * f2 * t);
                float hiss = (Random.value * 2f - 1f) * 0.08f;

                samples[i] = Mathf.Clamp((tone1 * 0.5f + tone2 * 0.45f + hiss) * env * flutter * 0.85f, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_Whistle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateWoodworkHitClip()
        {
            int sampleRate = 44100;
            float duration = 0.85f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Solid impact snap
                float impact = (Random.value * 2f - 1f) * Mathf.Exp(-progress * 50f) * 0.6f;

                // High-Q metallic ringing harmonics of steel/aluminum post
                float f0 = 840f;
                float ring1 = Mathf.Sin(2f * Mathf.PI * f0 * t) * Mathf.Exp(-progress * 7.5f) * 0.65f;
                float ring2 = Mathf.Sin(2f * Mathf.PI * (f0 * 2.14f) * t) * Mathf.Exp(-progress * 11.0f) * 0.35f;
                float ring3 = Mathf.Sin(2f * Mathf.PI * (f0 * 3.42f) * t) * Mathf.Exp(-progress * 15.0f) * 0.20f;
                float ring4 = Mathf.Sin(2f * Mathf.PI * (f0 * 0.45f) * t) * Mathf.Exp(-progress * 9.0f) * 0.30f;

                samples[i] = Mathf.Clamp(impact + ring1 + ring2 + ring3 + ring4, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_Woodwork", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateGoalRoarClip()
        {
            int sampleRate = 44100;
            float duration = 3.8f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float lowpass = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Eruption envelope: explosive rise in 0.2s, sustained roar, gradual taper
                float env;
                if (progress < 0.08f) env = Mathf.SmoothStep(0f, 1f, progress / 0.08f);
                else if (progress < 0.65f) env = 1f - (progress - 0.08f) * 0.18f;
                else env = Mathf.SmoothStep(1f - (0.65f - 0.08f) * 0.18f, 0f, (progress - 0.65f) / 0.35f);

                // Deep stadium sub-bass boom on goal
                float bassBoom = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-progress * 3.5f) * 0.5f;

                // Band-filtered pink crowd noise
                float white = Random.value * 2f - 1f;
                lowpass += (white - lowpass) * 0.15f;

                float midWave = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.12f;
                float highCheer = Mathf.Sin(2f * Mathf.PI * 540f * t + Mathf.Sin(t * 8f)) * 0.08f;

                samples[i] = Mathf.Clamp((lowpass * 0.7f + bassBoom + midWave + highCheer) * env, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_GoalRoar", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateNearMissGaspClip()
        {
            int sampleRate = 44100;
            float duration = 1.3f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float filterState = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Gasp envelope (sharp inhale and release)
                float env;
                if (progress < 0.25f) env = Mathf.SmoothStep(0f, 1f, progress / 0.25f);
                else env = Mathf.SmoothStep(1f, 0f, (progress - 0.25f) / 0.75f);

                float white = Random.value * 2f - 1f;
                // Modulate cutoff frequency upwards to simulate collective vocal gasp
                float cutoff = Mathf.Lerp(0.04f, 0.18f, progress);
                filterState += (white - filterState) * cutoff;

                samples[i] = Mathf.Clamp(filterState * env * 0.9f, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_NearMissGasp", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateCrowdMurmurLoop()
        {
            int sampleRate = 44100;
            float duration = 4.0f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float filter1 = 0f;
            float filter2 = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // Slow breathing crowd swell
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.4f * t);

                float white = Random.value * 2f - 1f;
                filter1 += (white - filter1) * 0.065f;
                filter2 += (filter1 - filter2) * 0.085f;

                float chantHarmonic = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.06f;

                // Seamless loop fade at boundaries
                float loopFade = 1f;
                float fadeLen = 0.2f;
                if (t < fadeLen) loopFade = t / fadeLen;
                else if (t > duration - fadeLen) loopFade = (duration - t) / fadeLen;

                samples[i] = Mathf.Clamp((filter2 * 0.9f + chantHarmonic) * swell * loopFade * 0.55f, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_CrowdMurmur", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateExcitementSwellLoop()
        {
            int sampleRate = 44100;
            float duration = 3.0f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float f = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                float white = Random.value * 2f - 1f;
                f += (white - f) * 0.12f;

                float chant1 = Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.08f;
                float chant2 = Mathf.Sin(2f * Mathf.PI * 390f * t) * 0.05f;

                float loopFade = 1f;
                float fadeLen = 0.2f;
                if (t < fadeLen) loopFade = t / fadeLen;
                else if (t > duration - fadeLen) loopFade = (duration - t) / fadeLen;

                samples[i] = Mathf.Clamp((f * 0.85f + chant1 + chant2) * loopFade * 0.7f, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_ExcitementSwell", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateSlideTackleClip()
        {
            int sampleRate = 44100;
            float duration = 0.38f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float filter = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                float env = Mathf.Sin(progress * Mathf.PI);
                float white = Random.value * 2f - 1f;
                filter += (white - filter) * 0.22f;

                samples[i] = Mathf.Clamp(filter * env * 0.75f, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_SlideTackle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateNetRustleClip()
        {
            int sampleRate = 44100;
            float duration = 0.42f;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float filter = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Authentic cord friction & net bulge whoosh
                float env = Mathf.Sin(progress * Mathf.PI);
                float white = (Random.value * 2f - 1f);
                filter += (white - filter) * 0.16f; // Low-pass filtered mesh rustle
                float whoosh = Mathf.Sin(2f * Mathf.PI * 72f * t) * Mathf.Exp(-progress * 6f);

                samples[i] = Mathf.Clamp((filter * 0.65f + whoosh * 0.35f) * env, -1f, 1f);
            }

            var clip = AudioClip.Create("Procedural_NetRustle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
