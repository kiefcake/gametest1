using UnityEngine;

namespace DungeonCrawler.Audio
{
    // No audio assets exist in this project (same constraint as art -- see the sprite
    // placeholders), so short SFX are synthesized directly into AudioClips instead of
    // being missing entirely. Each clip is built exactly once (static, lazy) and replayed
    // via AudioSource.PlayClipAtPoint, which spawns and auto-destroys its own temporary
    // AudioSource -- no manual cleanup needed at any call site.
    public static class SfxLibrary
    {
        private const int SampleRate = 22050;

        private static AudioClip _hit;
        private static AudioClip _dash;
        private static AudioClip _pickup;
        private static AudioClip _gold;
        private static AudioClip _warning;
        private static AudioClip _win;
        private static AudioClip _lose;

        public static AudioClip Hit => _hit != null ? _hit : (_hit = BuildThump(0.12f, 180f));
        public static AudioClip Dash => _dash != null ? _dash : (_dash = BuildSweep(220f, 60f, 0.18f, noisy: true));
        public static AudioClip Pickup => _pickup != null ? _pickup : (_pickup = BuildBlip(660f, 990f, 0.12f));
        public static AudioClip Gold => _gold != null ? _gold : (_gold = BuildBlip(880f, 1320f, 0.15f));
        public static AudioClip Warning => _warning != null ? _warning : (_warning = BuildSweep(160f, 520f, 0.5f, noisy: false));
        // Gambling/claw-machine payoff stings -- a bigger, brighter version of Gold's blip
        // for a win, and a falling non-noisy sweep (the "sad trombone" shape) for a loss.
        public static AudioClip Win => _win != null ? _win : (_win = BuildBlip(520f, 1040f, 0.3f));
        public static AudioClip Lose => _lose != null ? _lose : (_lose = BuildSweep(320f, 110f, 0.4f, noisy: false));

        public static void PlayAt(AudioClip clip, Vector3 pos, float volume = 0.4f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, pos, volume);
        }

        private static AudioClip Create(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Short decaying noise-and-tone burst -- reads as a percussive "hit" thump.
        // Used for every damage instance in the game (see HealthVFX), so it's deliberately
        // plain rather than tuned to any one weapon.
        private static AudioClip BuildThump(float duration, float freq)
        {
            int n = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[n];
            var rng = new System.Random(12345);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 22f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                samples[i] = (tone * 0.6f + noise * 0.4f) * env;
            }
            return Create("sfx_hit", samples);
        }

        // Frequency sweep, fading in and out -- a rising sweep reads as a warning cue
        // (boss telegraph), a falling one blended with noise reads as a "whoosh" (dash).
        private static AudioClip BuildSweep(float startFreq, float endFreq, float duration, bool noisy)
        {
            int n = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[n];
            var rng = new System.Random(54321);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float frac = (float)i / n;
                float freq = Mathf.Lerp(startFreq, endFreq, frac);
                phase += freq / SampleRate;
                float env = Mathf.Sin(Mathf.PI * frac); // fades in then out across the clip
                float tone = Mathf.Sin(2f * Mathf.PI * phase);
                float noise = noisy ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
                samples[i] = (tone * (noisy ? 0.5f : 1f) + noise * 0.5f) * env;
            }
            return Create("sfx_sweep", samples);
        }

        // Two quick ascending notes -- a "ding," used for item/gold pickups.
        private static AudioClip BuildBlip(float freq1, float freq2, float duration)
        {
            int n = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[n];
            int half = n / 2;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                bool firstHalf = i < half;
                float freq = firstHalf ? freq1 : freq2;
                float localT = firstHalf ? t : (float)(i - half) / SampleRate;
                float env = Mathf.Exp(-localT * 14f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            }
            return Create("sfx_blip", samples);
        }
    }
}
