using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaPowerRadio.Radio;

/// <summary>
/// The transmission half of the radio sound (the voice half is baked into every clip by tools/voicegen/radio.py):
/// open-carrier hiss that starts a beat before the mic click, sparse velvet-noise crackle, the click, the voice,
/// a short hang and the squelch tail. Built into one AudioClip per transmission so the stitched clips play gapless.
/// Every number comes from ClipRules (tools/voicegen/rules.py), which radio.py's transmission() renders the listening
/// samples with, so the game sounds like the samples.
/// </summary>
internal static class RadioFx
{
    /// <param name="maxPause">per part: most silence (seconds) where it joins the part before, see
    /// TransmissionBuilder.MaxPause</param>
    public static AudioClip Build(IList<AudioClip> parts, IList<float> maxPause, string profileName, int seed, float noiseScale,
                                  float level = 1f)
    {
        var rng = new System.Random(seed);
        ClipRules.Profile p = ClipRules.Profiles.TryGetValue(profileName ?? "", out ClipRules.Profile found)
            ? found : ClipRules.Profiles[ClipRules.DefaultProfile];

        int rate = 22050;
        foreach (AudioClip c in parts)
            if (c != null) { rate = c.frequency; break; }

        float[] speech = JoinVoice(parts, maxPause, rate);
        BandLimit(speech, rate, p.Hp, p.Lp);
        float[] mix = Key(rng, rate, speech, Math.Min(1f, Math.Max(0f, noiseScale)));
        AddNoiseBed(mix, rng, rate, p, noiseScale);

        // Hiss and crackle get the same gain as the voice, so the noise/voice balance stays the one from the approved
        // samples; the soft limiter also adds a little more transmitter grit.
        float gain = Mathf.Clamp(ClipRules.FxTargetSpeechRms * level / SpeechRms(speech), ClipRules.FxGainMin, ClipRules.FxGainMax);
        for (int i = 0; i < mix.Length; i++)
            mix[i] = SoftLimit(mix[i] * gain);

        AudioClip clip = AudioClip.Create("SeaPowerRadio.tx", mix.Length, 1, rate, false);
        clip.SetData(mix, 0);
        return clip;
    }

    /// <summary>
    /// Voice: clips back to back, a short beat where a null marks a new phrase. The silence at a join (the end of one
    /// clip plus the start of the next) is cut back to what the join allows, from the middle of the pause. Where a cut
    /// fell into running speech (number pieces cut out of "seven zero three four"), the two clips are cross-faded
    /// instead of butted together, so the join doesn't chop.
    /// </summary>
    private static float[] JoinVoice(IList<AudioClip> parts, IList<float> maxPause, int rate)
    {
        var voice = new List<float>(rate * 4);
        bool previousWasClip = false;
        int trailingQuiet = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            AudioClip c = parts[i];
            if (c == null)
            {
                voice.AddRange(new float[(int)(ClipRules.FxPhraseGap * rate)]);
                previousWasClip = false;
                continue;
            }
            float[] data = Mono(c);
            int endQuiet = TrailingSilence(data, rate);
            if (previousWasClip)
            {
                data = CloseUp(voice, data, trailingQuiet, (int)(Math.Min(maxPause[i], 10f) * rate), rate);
                endQuiet = Math.Min(endQuiet, data.Length);
                AppendJoined(voice, data, rate);
            }
            else
            {
                voice.AddRange(data);
            }
            trailingQuiet = endQuiet;
            previousWasClip = true;
        }
        return voice.ToArray();
    }

    /// <summary>
    /// Cuts the silence where <paramref name="data"/> joins <paramref name="voice"/> back to <paramref name="keep"/>
    /// samples: each side keeps up to half (the fade of one word, the onset of the next). Returns what is left of data.
    /// </summary>
    private static float[] CloseUp(List<float> voice, float[] data, int trailingQuiet, int keep, int rate)
    {
        int leadingQuiet = LeadingSilence(data, rate);
        if (trailingQuiet + leadingQuiet <= keep)
            return data;
        int keepEnd = Math.Min(trailingQuiet, keep / 2);
        int keepStart = Math.Min(leadingQuiet, keep - keepEnd);
        keepEnd = Math.Min(trailingQuiet, keep - keepStart);
        voice.RemoveRange(voice.Count - (trailingQuiet - keepEnd), trailingQuiet - keepEnd);
        var rest = new float[data.Length - (leadingQuiet - keepStart)];
        Array.Copy(data, leadingQuiet - keepStart, rest, 0, rest.Length);
        return rest;
    }

    /// <summary>
    /// The keyed transmission: hiss-only preroll, mic click, the voice, a short hang, the squelch tail. Click and tail follow the
    /// static setting up to the default amount (0 = a clean line, players asked for it); above it only hiss and crackle grow.
    /// </summary>
    private static float[] Key(System.Random rng, int rate, float[] speech, float keyLevel)
    {
        int preroll = (int)(rate * Range(rng, ClipRules.FxPrerollMin, ClipRules.FxPrerollMax));
        float[] click = Burst(rng, rate, Range(rng, ClipRules.FxClickSecondsMin, ClipRules.FxClickSecondsMax),
            Range(rng, ClipRules.FxClickLevelMin, ClipRules.FxClickLevelMax) * keyLevel, ClipRules.FxClickHp, ClipRules.FxClickLp, exponential: true);
        int hang = (int)(rate * Range(rng, ClipRules.FxHangMin, ClipRules.FxHangMax));
        float[] tail = Burst(rng, rate, Range(rng, ClipRules.FxTailSecondsMin, ClipRules.FxTailSecondsMax),
            Range(rng, ClipRules.FxTailLevelMin, ClipRules.FxTailLevelMax) * keyLevel, ClipRules.FxTailHp, ClipRules.FxTailLp, exponential: false);

        var mix = new float[preroll + click.Length + speech.Length + hang + tail.Length];
        int at = preroll;
        Array.Copy(click, 0, mix, at, click.Length); at += click.Length;
        Array.Copy(speech, 0, mix, at, speech.Length); at += speech.Length + hang;
        Array.Copy(tail, 0, mix, at, tail.Length);
        return mix;
    }

    /// <summary>Hiss bed for the whole transmission, crackle riding on top (denser on a weaker signal).</summary>
    private static void AddNoiseBed(float[] mix, System.Random rng, int rate, ClipRules.Profile p, float noiseScale)
    {
        float[] hiss = Noise(rng, mix.Length, p.Hiss * noiseScale);
        BandLimit(hiss, rate, ClipRules.FxHissHp, ClipRules.FxHissLp);
        float density = Range(rng, ClipRules.FxCrackleDensityMin, ClipRules.FxCrackleDensityMax)
                        * (p.Hiss > ClipRules.FxWeakSignalHiss ? ClipRules.FxWeakSignalDensity : 1f);
        float[] crackle = Velvet(rng, mix.Length, density, p.Crackle * noiseScale);
        BandLimit(crackle, rate, ClipRules.FxCrackleHp, ClipRules.FxCrackleLp);
        for (int i = 0; i < mix.Length; i++)
            mix[i] += hiss[i] + crackle[i];
    }

    /// <summary>Silence at the end of a clip in samples: whole 10 ms frames below the speech level.</summary>
    private static int TrailingSilence(float[] x, int rate)
    {
        int frame = Math.Max(1, (int)(ClipRules.FxFrame * rate));
        for (int f = 0; f < x.Length / frame; f++)
            if (Rms(x, x.Length - (f + 1) * frame, frame) >= ClipRules.FxSoundRms)
                return f * frame;
        return 0;
    }

    /// <summary>
    /// Silence at the start of a clip in samples: everything before the onset of the first word - the frames above
    /// OnsetRms that run into the first frame above SoundRms. The weak "s" of "seven" stays, a breath before it goes.
    /// Measured against word errors of a speech recognizer over every voice: cutting at SoundRms here too ate those
    /// onsets ("seven" -> "even"), this doesn't.
    /// </summary>
    private static int LeadingSilence(float[] x, int rate)
    {
        int frame = Math.Max(1, (int)(ClipRules.FxFrame * rate));
        int frames = x.Length / frame, f = 0;
        while (f < frames && Rms(x, f * frame, frame) < ClipRules.FxSoundRms)
            f++;
        if (f == frames)
            return 0;
        while (f > 0 && Rms(x, (f - 1) * frame, frame) >= ClipRules.FxOnsetRms)
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
        int edge = (int)(ClipRules.FxEdge * rate);
        int fade = Math.Min((int)(ClipRules.FxCrossfade * rate), Math.Min(voice.Count, next.Length) / 3);
        if (fade < 8 || voice.Count < edge || next.Length < edge)
        {
            voice.AddRange(next);
            return;
        }

        bool hotEnd = Rms(voice, voice.Count - edge, edge) > ClipRules.FxHotEdgeRatio * Rms(voice, Math.Max(0, voice.Count - rate / 2), Math.Min(voice.Count, rate / 2));
        bool hotStart = Rms(next, 0, edge) > ClipRules.FxHotEdgeRatio * Rms(next, 0, Math.Min(next.Length, rate / 2));
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

    /// <summary>RMS over the voiced part only (pauses between words would drag the average down).</summary>
    private static float SpeechRms(float[] speech)
    {
        double sum = 0;
        int count = 0;
        foreach (float s in speech)
        {
            if (Math.Abs(s) < ClipRules.FxSpeechGate) continue;
            sum += s * s;
            count++;
        }
        return count == 0 ? ClipRules.FxTargetSpeechRms : Math.Max(1e-4f, (float)Math.Sqrt(sum / count));
    }

    /// <summary>Linear below the knee, tanh above it, never past the ceiling.</summary>
    private static float SoftLimit(float x)
    {
        const float knee = ClipRules.FxLimitKnee;
        float a = Math.Abs(x);
        if (a <= knee) return x;
        float room = ClipRules.FxLimitCeiling - knee;
        float y = knee + room * (float)Math.Tanh((a - knee) / room);
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
