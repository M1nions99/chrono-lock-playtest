using UnityEngine;

namespace ChronoLock
{
    public enum ChronoCue { Freeze, Accelerate, Rewind, Confirm, Error, Complete }

    // Original synthesized feedback: no downloaded recordings or asset dependencies.
    [DisallowMultipleComponent]
    public sealed class ChronoFeedback : MonoBehaviour
    {
        AudioSource source;
        readonly AudioClip[] clips = new AudioClip[6];
        float nextError;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.volume = .12f * ChronoPreferences.EffectsVolume;
            for (int i = 0; i < clips.Length; i++) clips[i] = Synthesize((ChronoCue)i);
        }
        public void Play(ChronoCue cue)
        {
            if (!source || (int)cue < 0 || (int)cue >= clips.Length) return;
            if (cue == ChronoCue.Error)
            {
                if (Time.unscaledTime < nextError) return;
                nextError = Time.unscaledTime + .25f;
            }
            // A single voice prevents repeated inputs from stacking loud sounds.
            source.Stop(); source.volume = .12f * ChronoPreferences.EffectsVolume; source.clip = clips[(int)cue]; source.Play();
        }
        void Update() { if (source) source.volume = .12f * ChronoPreferences.EffectsVolume; }
        static AudioClip Synthesize(ChronoCue cue)
        {
            const int rate = 22050;
            float duration = cue == ChronoCue.Complete ? .64f : cue == ChronoCue.Confirm ? .34f : .2f;
            float[] samples = new float[Mathf.CeilToInt(rate * duration)];
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, u = t / duration;
                float frequency;
                switch (cue)
                {
                    case ChronoCue.Freeze: frequency = Mathf.Lerp(880, 440, u); break;
                    case ChronoCue.Accelerate: frequency = Mathf.Lerp(330, 990, u); break;
                    case ChronoCue.Rewind: frequency = Mathf.Lerp(650, 210, u); break;
                    case ChronoCue.Confirm: frequency = u < .45f ? 523.25f : 783.99f; break;
                    case ChronoCue.Complete: frequency = u < .33f ? 523.25f : u < .66f ? 659.25f : 783.99f; break;
                    default: frequency = 180; break;
                }
                phase += 2 * System.Math.PI * frequency / rate;
                float attack = Mathf.Clamp01(t / .012f), release = Mathf.Clamp01((duration - t) / .07f);
                float envelope = attack * release * (1 - .35f * u);
                samples[i] = (float)(System.Math.Sin(phase) * .65 + System.Math.Sin(phase * 2) * .12) * envelope;
            }
            var clip = AudioClip.Create("Chrono / " + cue, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        void OnDestroy()
        {
            if (source) { source.Stop(); Destroy(source); }
            foreach (var clip in clips) if (clip) Destroy(clip);
        }
    }
}
