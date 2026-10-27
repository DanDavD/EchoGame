using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sound effects made at runtime from maths, with no audio files: the cave's room tone, water drips, distant rumbles,
/// the monster's breath, roar and steps, and the players' footsteps. Every clip is generated once and cached, and every
/// generator is deterministic (fixed seeds), so the game sounds the same on every machine and the tests can check it.
/// </summary>
public static class ProceduralAudio
{
    public const int SampleRate = 44100;

    private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => Cache.Clear();

    // ------------------------------------------------------------------ clips

    /// <summary>The cave's background: a low, slowly breathing hum with filtered air noise. Loops without a seam.</summary>
    public static AudioClip RoomTone() => Cached("CaveRoomTone", () => RoomToneSamples(8f));

    /// <summary>A water drop falling into a puddle. <paramref name="variant"/> 0..3 changes the pitch.</summary>
    public static AudioClip Drip(int variant) => Cached($"CaveDrip{variant}", () => DripSamples(variant));

    /// <summary>A deep, distant rumble of rock, about 3 s long.</summary>
    public static AudioClip Rumble() => Cached("CaveRumble", RumbleSamples);

    /// <summary>The monster's breathing: one wet inhale and a long exhale, as a 2.4 s loop.</summary>
    public static AudioClip MonsterBreath() => Cached("MonsterBreath", MonsterBreathSamples);

    /// <summary>The roar the monster lets out when it starts to hunt.</summary>
    public static AudioClip MonsterRoar() => Cached("MonsterRoar", MonsterRoarSamples);

    /// <summary>A short, low growl the monster lets out now and then, so players can tell where it is.</summary>
    public static AudioClip MonsterGrowl() => Cached("MonsterGrowl", MonsterGrowlSamples);

    /// <summary>One heavy monster step.</summary>
    public static AudioClip MonsterStep() => Cached("MonsterStep", () => StepSamples(seed: 31, pitch: 55f, length: 0.28f, grit: 0.35f));

    /// <summary>One player step on stone. <paramref name="variant"/> 0..3 gives small differences between steps.</summary>
    public static AudioClip PlayerStep(int variant) =>
        Cached($"PlayerStep{variant}", () => StepSamples(seed: 11 + variant, pitch: 120f + variant * 9f, length: 0.12f, grit: 0.6f));

    // ------------------------------------------------------------------ generators (public for the tests)

    public static float[] RoomToneSamples(float seconds)
    {
        int count = Mathf.RoundToInt(SampleRate * seconds);
        int overlap = Mathf.RoundToInt(SampleRate * 0.5f);
        var samples = new float[count + overlap];
        var random = new System.Random(3);
        float low = 0f, air = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / SampleRate;
            // Whole cycles in the loop length, so the end meets the start.
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / seconds);
            float hum = Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.2f;

            float white = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.998f + white * 0.002f;   // deep rumble
            air = air * 0.96f + white * 0.04f;     // soft hiss of moving air

            samples[i] = (hum * 0.35f + low * 6f + air * 0.35f) * swell * 0.5f;
        }

        // The noise does not loop on its own: blend the extra half second over the start, so the end flows into it.
        var loop = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (i < overlap)
            {
                float k = (float)i / overlap;
                loop[i] = samples[i] * k + samples[count + i] * (1f - k);
            }
            else
            {
                loop[i] = samples[i];
            }
        }
        return Normalize(loop, 0.5f);
    }

    public static float[] DripSamples(int variant)
    {
        float length = 0.35f;
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        float baseFreq = 900f + 180f * (variant % 4);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            // A falling "plip": the pitch slides up quickly (the bubble), then rings out.
            float freq = baseFreq * (1f + 0.9f * (1f - Mathf.Exp(-t * 60f)));
            float env = Mathf.Exp(-t * 18f) * Mathf.Min(1f, t * 600f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        }
        return Normalize(samples, 0.8f);
    }

    public static float[] RumbleSamples()
    {
        float length = 3f;
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        var random = new System.Random(19);
        float low = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Sin(Mathf.PI * t / length); // swell in and out
            env *= env;
            low = low * 0.999f + ((float)random.NextDouble() * 2f - 1f) * 0.001f;
            float tone = Mathf.Sin(2f * Mathf.PI * 32f * t + Mathf.Sin(2f * Mathf.PI * 0.7f * t) * 2f);
            samples[i] = (low * 25f + tone * 0.3f) * env;
        }
        return Normalize(samples, 0.9f);
    }

    public static float[] MonsterBreathSamples()
    {
        float length = 2.4f;
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        var random = new System.Random(23);
        float band = 0f, prev = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            // Inhale for the first 0.8 s, exhale for the next 1.2 s, then a pause.
            float inhale = t < 0.8f ? Mathf.Sin(Mathf.PI * t / 0.8f) * 0.6f : 0f;
            float exhale = t >= 0.8f && t < 2f ? Mathf.Sin(Mathf.PI * (t - 0.8f) / 1.2f) : 0f;
            float env = inhale + exhale;

            // Breath is noise through a soft band-pass, with a low rattle in the throat on the exhale.
            float white = (float)random.NextDouble() * 2f - 1f;
            band = band * 0.85f + white * 0.15f;
            float hissy = band - prev * 0.6f;
            prev = band;
            float rattle = Mathf.Sin(2f * Mathf.PI * 70f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 23f * t)) * exhale;

            samples[i] = (hissy * 1.6f + rattle * 0.35f) * env;
        }
        return Normalize(samples, 0.8f);
    }

    public static float[] MonsterRoarSamples()
    {
        float length = 1.8f;
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        var random = new System.Random(29);
        float phase = 0f, noise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            // Fast attack, a long body and a tail; the pitch rises, then sags as the roar runs out of breath.
            float env = Mathf.Min(1f, t * 12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.9f) * 3f);
            float freq = 85f + 45f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 1.2f)) + Mathf.Sin(2f * Mathf.PI * 9f * t) * 6f;
            phase += 2f * Mathf.PI * freq / SampleRate;

            // A growl: a rough, clipped tone with its own harmonics, plus a throaty noise.
            float tone = Mathf.Sin(phase) + 0.6f * Mathf.Sin(phase * 2.01f) + 0.4f * Mathf.Sin(phase * 3.03f);
            tone = Mathf.Clamp(tone * 1.8f, -1f, 1f);
            noise = noise * 0.7f + ((float)random.NextDouble() * 2f - 1f) * 0.3f;

            samples[i] = (tone * 0.7f + noise * 0.5f) * env;
        }
        return Normalize(samples, 0.95f);
    }

    public static float[] MonsterGrowlSamples()
    {
        float length = 1.1f;
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        var random = new System.Random(37);
        float phase = 0f, noise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            // Swells in and fades out, with a slow wobble: a throat rumbling, not a roar.
            float env = Mathf.Sin(Mathf.PI * t / length);
            env *= env;
            float freq = 62f + 10f * Mathf.Sin(2f * Mathf.PI * 2.3f * t);
            phase += 2f * Mathf.PI * freq / SampleRate;

            float tone = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2.02f) + 0.3f * Mathf.Sin(phase * 3.1f);
            tone = Mathf.Clamp(tone * 1.5f, -1f, 1f);
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 17f * t); // the rattle of the growl
            noise = noise * 0.8f + ((float)random.NextDouble() * 2f - 1f) * 0.2f;

            samples[i] = (tone * flutter * 0.75f + noise * 0.35f) * env;
        }
        return Normalize(samples, 0.9f);
    }

    public static float[] StepSamples(int seed, float pitch, float length, float grit)
    {
        int count = Mathf.RoundToInt(SampleRate * length);
        var samples = new float[count];
        var random = new System.Random(seed);
        float noise = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float env = Mathf.Exp(-t * (6f / length)) * Mathf.Min(1f, t * 2000f);
            // A dull thump (falling pitch) and the grit of the sole on stone.
            float thump = Mathf.Sin(2f * Mathf.PI * pitch * t * (1f - 0.4f * t / length));
            noise = noise * 0.5f + ((float)random.NextDouble() * 2f - 1f) * 0.5f;
            samples[i] = (thump * (1f - grit) + noise * grit) * env;
        }
        return Normalize(samples, 0.8f);
    }

    // ------------------------------------------------------------------ helpers

    private static AudioClip Cached(string name, System.Func<float[]> generate)
    {
        if (Cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;

        float[] samples = generate();
        clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        Cache[name] = clip;
        return clip;
    }

    /// <summary>Scales the samples so the loudest one sits at <paramref name="peak"/> (never clips).</summary>
    public static float[] Normalize(float[] samples, float peak)
    {
        float max = 0f;
        for (int i = 0; i < samples.Length; i++) max = Mathf.Max(max, Mathf.Abs(samples[i]));
        if (max < 1e-6f) return samples;

        float gain = peak / max;
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        return samples;
    }

    /// <summary>
    /// A 3D sound source with the game's distance settings, as a child of <paramref name="parent"/>. A larger
    /// <paramref name="minDistance"/> keeps a sound loud further away (the logarithmic rolloff starts there).
    /// </summary>
    public static AudioSource CreateSource(Transform parent, string name, float maxDistance, float volume, float minDistance = 1.5f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.volume = volume;
        return source;
    }
}
