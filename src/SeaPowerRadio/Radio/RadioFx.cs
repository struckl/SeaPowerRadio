using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaPowerRadio.Radio;

/// <summary>
/// The transmission half of the radio sound (the voice half is baked into every clip by tools/voicegen/radio.py):
/// open-carrier hiss that starts a beat before the mic click, sparse velvet-noise crackle, the click, the voice,
/// a short hang and the squelch tail. Built into one AudioClip per transmission so the stitched clips play gapless.
/// Same parameters as radio.py's transmission(), so the game sounds like the listening samples.
/// </summary>
internal static class RadioFx
{
    private sealed class Profile
    {
        public float Hiss, Crackle, Lp, Hp;
        public Profile(float hiss, float crackle, float lp, float hp) { Hiss = hiss; Crackle = crackle; Lp = lp; Hp = hp; }
    }

    private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
    {
        ["station"] = new Profile(0.040f, 0.05f, 3400, 280),
        ["near"] = new Profile(0.060f, 0.08f, 3300, 300),
        ["far"] = new Profile(0.120f, 0.18f, 2600, 420),
        ["cockpit_hot"] = new Profile(0.070f, 0.10f, 3100, 320),
    };

    private const float PhraseGapSeconds = 0.12f;

    /// <param name="maxPause">per part: most silence (seconds) where it joins the part before, see
    /// TransmissionBuilder.MaxPause</param>
    public static AudioClip Build(IList<AudioClip> parts, IList<float> maxPause, string profileName, int seed, float noiseScale,
                                  float level = 1f)
    {
        var rng = new System.Random(seed);
        Profile p = Profiles.TryGetValue(profileName ?? "", out Profile found) ? found : Profiles["near"];

        int rate = 22050;
        foreach (AudioClip c in parts)
            if (c != null) { rate = c.frequency; break; }

        // Voice: clips back to back, a short beat where a null marks a new phrase. The silence at a join (the end of
        // one clip plus the start of the next) is cut back to what the join allows, from the middle of the pause.
        // Where a cut fell into running speech (number pieces cut out of "seven zero three four"), the two clips
        // are cross-faded instead of butted together, so the join doesn't chop.
        var voice = new List<float>(rate * 4);
        bool previousWasClip = false;
        int trailingQuiet = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            AudioClip c = parts[i];
            if (c == null)
            {
                voice.AddRange(new float[(int)(PhraseGapSeconds * rate)]);
                previousWasClip = false;
                continue;
            }
            float[] data = Mono(c);
            int leadingQuiet = LeadingSilence(data, rate);
            int endQuiet = TrailingSilence(data, rate);
            if (previousWasClip)
            {
                int keep = (int)(Math.Min(maxPause[i], 10f) * rate);
                if (trailingQuiet + leadingQuiet > keep)
                {
                    // Each side keeps up to half of what's allowed (the fade of one word, the onset of the next).
                    int keepEnd = Math.Min(trailingQuiet, keep / 2);
                    int keepStart = Math.Min(leadingQuiet, keep - keepEnd);
                    keepEnd = Math.Min(trailingQuiet, keep - keepStart);
                    voice.RemoveRange(voice.Count - (trailingQuiet - keepEnd), trailingQuiet - keepEnd);
                    var rest = new float[data.Length - (leadingQuiet - keepStart)];
                    Array.Copy(data, leadingQuiet - keepStart, rest, 0, rest.Length);
                    data = rest;
                    endQuiet = Math.Min(endQuiet, data.Length);
                }
                AppendJoined(voice, data, rate);
            }
            else
            {
                voice.AddRange(data);
            }
            trailingQuiet = endQuiet;
            previousWasClip = true;
        }
        float[] speech = voice.ToArray();
        BandLimit(speech, rate, p.Hp, p.Lp);

        int preroll = (int)(rate * Range(rng, 0.08f, 0.20f));
        float[] click = Burst(rng, rate, Range(rng, 0.022f, 0.045f), Range(rng, 0.14f, 0.26f), 700, 3200, exponential: true);
        int hang = (int)(rate * Range(rng, 0.05f, 0.14f));
        float[] tail = Burst(rng, rate, Range(rng, 0.09f, 0.18f), Range(rng, 0.18f, 0.32f), 500, 3400, exponential: false);

        int length = preroll + click.Length + speech.Length + hang + tail.Length;
        var mix = new float[length];
        int at = preroll;
        Array.Copy(click, 0, mix, at, click.Length); at += click.Length;
        Array.Copy(speech, 0, mix, at, speech.Length); at += speech.Length + hang;
        Array.Copy(tail, 0, mix, at, tail.Length);

        // Hiss bed for the whole transmission, crackle riding on top (denser on a weaker signal).
        float[] hiss = Noise(rng, length, p.Hiss * noiseScale);
        BandLimit(hiss, rate, 400, 2600);
        float density = Range(rng, 0.02f, 0.05f) * (p.Hiss > 0.1f ? 1.6f : 1f);
        float[] crackle = Velvet(rng, length, density, p.Crackle * noiseScale);
        BandLimit(crackle, rate, 600, 3200);

        for (int i = 0; i < length; i++)
            mix[i] += hiss[i] + crackle[i];

        // Loudness: the game's own voice lines are mastered hot (about -11 LUFS, peaks at -6 dBFS) while the
        // rendered clips sit around -26 LUFS. Bring the voice up to the stock level and catch the peaks with a soft
        // limiter – which also adds a little more transmitter grit. Hiss and crackle get the same gain, so the
        // noise/voice balance stays the one from the approved samples.
        float gain = Mathf.Clamp(TargetSpeechRms * level / SpeechRms(speech), 0.25f, 12f);
        for (int i = 0; i < length; i++)
            mix[i] = SoftLimit(mix[i] * gain);

        AudioClip clip = AudioClip.Create("SeaPowerRadio.tx", length, 1, rate, false);
        clip.SetData(mix, 0);
        return clip;
    }

    private const float CrossfadeSeconds = 0.025f;
    private const float EdgeSeconds = 0.015f;
    private const float HotEdgeRatio = 0.5f;   // edge RMS vs. clip RMS above this = cut in running speech (~ -6 dB)
    private const float FrameSeconds = 0.010f;
    private const float SoundRms = 0.01f;      // -40 dBFS: speech; breath and room tone sit below it, under the hiss bed
    private const float OnsetRms = 0.0018f;    // -55 dBFS: the start of a digit ("s", "th", "f") after the band-limit

    /// <summary>Silence at the end of a clip in samples: whole 10 ms frames below SoundRms.</summary>
    private static int TrailingSilence(float[] x, int rate)
    {
        int frame = Math.Max(1, (int)(FrameSeconds * rate));
        for (int f = 0; f < x.Length / frame; f++)
            if (Rms(x, x.Length - (f + 1) * frame, frame) >= SoundRms)
                return f * frame;
        return 0;
    }

    /// <summary>
    /// Silence at the start of a clip in samples: everything before the onset of the first word – the frames above
    /// OnsetRms that run into the first frame above SoundRms. The weak "s" of "seven" stays, a breath before it goes.
    /// Measured against word errors of a speech recognizer over every voice: cutting at SoundRms here too ate those
    /// onsets ("seven" → "even"), this doesn't.
    /// </summary>
    private static int LeadingSilence(float[] x, int rate)
    {
        int frame = Math.Max(1, (int)(FrameSeconds * rate));
        int frames = x.Length / frame, f = 0;
        while (f < frames && Rms(x, f * frame, frame) < SoundRms)
            f++;
        if (f == frames)
            return 0;
        while (f > 0 && Rms(x, (f - 1) * frame, frame) >= OnsetRms)
            f--;
        return f * frame;
    }

    private static float[] Mono(AudioClip c)
    {
        var data = new float[c.samples * c.channels];
        c.GetData(data, 0);
        if (c.channels == 1)
            return data;
        var mono = new float[c.samples];
        for (int i = 0; i < mono.Length; i++)
            mono[i] = data[i * c.channels];
        return mono;
    }

    /// <summary>
    /// Append <paramref name="next"/>. If both sides of the seam are loud (the previous clip ends and this one
    /// starts mid-sound), overlap them by a short equal-power crossfade; otherwise just butt them together.
    /// </summary>
    private static void AppendJoined(List<float> voice, float[] next, int rate)
    {
        int edge = (int)(EdgeSeconds * rate);
        int fade = Math.Min((int)(CrossfadeSeconds * rate), Math.Min(voice.Count, next.Length) / 3);
        if (fade < 8 || voice.Count < edge || next.Length < edge)
        {
            voice.AddRange(next);
            return;
        }

        bool hotEnd = Rms(voice, voice.Count - edge, edge) > HotEdgeRatio * Rms(voice, Math.Max(0, voice.Count - rate / 2), Math.Min(voice.Count, rate / 2));
        bool hotStart = Rms(next, 0, edge) > HotEdgeRatio * Rms(next, 0, Math.Min(next.Length, rate / 2));
        if (!hotEnd && !hotStart)
        {
            voice.AddRange(next);
            return;
        }

        int start = voice.Count - fade;
        for (int i = 0; i < fade; i++)
        {
            float t = (i + 0.5f) / fade;
            float fadeOut = Mathf.Cos(t * Mathf.PI * 0.5f);
            float fadeIn = Mathf.Sin(t * Mathf.PI * 0.5f);
            voice[start + i] = voice[start + i] * fadeOut + next[i] * fadeIn;
        }
        for (int i = fade; i < next.Length; i++)
            voice.Add(next[i]);
    }

    private static float Rms(IList<float> x, int from, int count)
    {
        if (count <= 0) return 0f;
        double sum = 0;
        for (int i = from; i < from + count; i++)
            sum += x[i] * x[i];
        return (float)Math.Sqrt(sum / count);
    }

    // RMS of the stock voice clips while speaking (mean volume about -15 dBFS).
    private const float TargetSpeechRms = 0.17f;
    private const float LimitKnee = 0.6f;

    /// <summary>RMS over the voiced part only (pauses between words would drag the average down).</summary>
    private static float SpeechRms(float[] speech)
    {
        double sum = 0;
        int count = 0;
        foreach (float s in speech)
        {
            if (Math.Abs(s) < 0.005f) continue;
            sum += s * s;
            count++;
        }
        return count == 0 ? TargetSpeechRms : Math.Max(1e-4f, (float)Math.Sqrt(sum / count));
    }

    /// <summary>Linear below the knee, tanh above it, never past 0.98.</summary>
    private static float SoftLimit(float x)
    {
        float a = Math.Abs(x);
        if (a <= LimitKnee) return x;
        float room = 0.98f - LimitKnee;
        float y = LimitKnee + room * (float)Math.Tanh((a - LimitKnee) / room);
        return x < 0 ? -y : y;
    }

    private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private static float[] Noise(System.Random rng, int length, float level)
    {
        var n = new float[length];
        for (int i = 0; i < length; i++)
            n[i] = ((float)rng.NextDouble() * 2f - 1f) * level;
        return n;
    }

    /// <summary>Sparse random impulses: reads as static instead of as a fan.</summary>
    private static float[] Velvet(System.Random rng, int length, float density, float level)
    {
        var n = new float[length];
        for (int i = 0; i < length; i++)
            if (rng.NextDouble() < density)
                n[i] = (rng.NextDouble() < 0.5 ? -1f : 1f) * level;
        return n;
    }

    private static float[] Burst(System.Random rng, int rate, float seconds, float level, float hp, float lp, bool exponential)
    {
        int count = Math.Max(1, (int)(seconds * rate));
        float[] b = Noise(rng, count, 1f);
        BandLimit(b, rate, hp, lp);
        for (int i = 0; i < count; i++)
        {
            float x = i / (float)count;
            float env = exponential ? Mathf.Exp(-5f * x) : 1f - Mathf.Log10(1f + 9f * x);
            b[i] *= env * level;
        }
        return b;
    }

    /// <summary>Two-pole high-pass and low-pass (cascaded one-poles), in place.</summary>
    private static void BandLimit(float[] x, int rate, float hp, float lp)
    {
        float aLp = 1f - Mathf.Exp(-2f * Mathf.PI * lp / rate);
        float aHp = 1f - Mathf.Exp(-2f * Mathf.PI * hp / rate);
        float l1 = 0, l2 = 0, h1 = 0, h2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            l1 += aLp * (x[i] - l1);
            l2 += aLp * (l1 - l2);
            h1 += aHp * (l2 - h1);
            float y = l2 - h1;
            h2 += aHp * (y - h2);
            x[i] = y - h2;
        }
    }
}
