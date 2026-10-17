using System;
using System.Collections.Generic;
using SeaPowerRadio.Roster;
using SeaPowerRadio.Voices;
using UnityEngine;

namespace SeaPowerRadio.Radio;

/// <summary>One radio call: who speaks, which clips in which order, how urgent.</summary>
internal sealed class Transmission
{
    public RosterElement Speaker;
    public string Voice;
    public readonly List<string> Clips = new List<string>();   // clip keys; null = short pause between phrases
    public string Phrase;                                      // main phrase key (for queue rules / logs)
    public int Priority;                                       // 1 = most urgent … 7 = acknowledgement
    public string Profile;                                     // radio flavour, see RadioFx
    public float SubmittedAt;
    public string Text;                                        // human-readable, for the log

    public bool Response;                                      // a unit answering the player (ack, order, refusal)

    public bool Urgent => Priority <= 2;

    /// <summary>
    /// Queue order: urgent calls first, then units answering the player – an order should be acknowledged right
    /// away, like the stock system's command channel – then everything else by priority.
    /// </summary>
    public int Rank => Urgent ? -1000 + Priority : Response ? -100 : Priority;
}

/// <summary>
/// Turns "Viper 2-1 says EngageTrack 7034" into the clip sequence the voice library holds. Mirrors the assembly
/// rules of tools/voicegen/reel.py: callsign word + flight-position, phrase part a, number pieces, phrase part b.
/// Every clip carries its half of the natural pauses, so they are joined back to back.
/// </summary>
internal static class TransmissionBuilder
{
    public static Transmission Build(RosterElement speaker, string phrase, int? number)
    {
        var t = Start(speaker);
        return Append(t, phrase, number) ? t : null;
    }

    public static Transmission Start(RosterElement speaker)
    {
        var t = new Transmission
        {
            Speaker = speaker,
            Voice = speaker.Voice?.Id,
            SubmittedAt = Time.unscaledTime,
            Priority = 7,
        };
        t.Clips.AddRange(Callsign(speaker.Callsign));
        t.Text = speaker.Callsign + ",";
        return t;
    }

    /// <summary>Add a phrase (with an optional number) to the transmission. False if it can't be spoken.</summary>
    public static bool Append(Transmission t, string phrase, int? number)
    {
        if (t.Voice == null)
            return false;
        PhraseVariant v = Catalog.Pick(phrase, t.Voice, number.HasValue);
        if (v == null)
            return false;

        if (t.Phrase != null)
            t.Clips.Add(null); // a beat between two phrases in one call
        else
            t.Phrase = phrase;
        t.Priority = Math.Min(t.Priority, v.Priority);

        if (v.Slot == null)
        {
            t.Clips.Add($"{phrase}_{v.N}");
            t.Text += $" {phrase}";
        }
        else
        {
            if (v.HasA) t.Clips.Add($"{phrase}_{v.N}_a");
            t.Clips.AddRange(Number(v.Slot, number.Value));
            if (v.HasB) t.Clips.Add($"{phrase}_{v.N}_b");
            t.Text += $" {phrase}({number.Value})";
        }
        t.Profile = ProfileFor(t.Speaker, t.Priority);
        return true;
    }

    /// <summary>"Viper 2-1" → cs_viper + fp_21; "Sentinel" or "Sentinel 2" → cs_sentinel.</summary>
    public static IEnumerable<string> Callsign(string callsign)
    {
        if (string.IsNullOrEmpty(callsign))
            yield break;
        string[] parts = callsign.Split(' ');
        yield return "cs_" + parts[0].ToLowerInvariant();
        if (parts.Length > 1 && parts[1].Length == 3 && parts[1][1] == '-')
            yield return $"fp_{parts[1][0]}{parts[1][2]}";
    }

    /// <summary>Number pieces: dm_D (leading digit), pm_PP (pair, more to come), pe_PP (closing pair).</summary>
    public static IEnumerable<string> Number(string kind, int value)
    {
        switch (kind)
        {
            case "brg":
                value = ((value % 360) + 360) % 360;
                string b = value.ToString("000");
                yield return "dm_" + b[0];
                yield return "pe_" + b.Substring(1);
                break;
            case "alt":
                if (value >= 1000)
                    yield return "alt_" + Mathf.Clamp(Mathf.RoundToInt(value / 1000f), 1, 60);
                else
                    yield return "alt_lo_" + NearestLowAltitude(value);
                break;
            case "count":
                yield return "cnt_" + Mathf.Clamp(value, 2, 9);
                break;
            default: // trk, num
                value = Math.Abs(value);
                string s = value.ToString();
                if (s.Length >= 4)
                {
                    s = s.Substring(s.Length - 4);
                    yield return "pm_" + s.Substring(0, 2);
                    yield return "pe_" + s.Substring(2);
                }
                else if (s.Length == 3)
                {
                    yield return "dm_" + s[0];
                    yield return "pe_" + s.Substring(1);
                }
                else
                {
                    yield return "pe_" + value.ToString("00");
                }
                break;
        }
    }

    private static int NearestLowAltitude(int feet)
    {
        int[] steps = { 1, 2, 3, 5, 8 };
        int best = 1;
        foreach (int h in steps)
            if (Math.Abs(h * 100 - feet) < Math.Abs(best * 100 - feet))
                best = h;
        return best;
    }

    private static string ProfileFor(RosterElement speaker, int priority)
    {
        switch (speaker.Kind)
        {
            case CallsignKind.Ship:
            case CallsignKind.Submarine:
            case CallsignKind.Shore:
            case CallsignKind.Support:
                return "station";
            default:
                return priority <= 2 ? "cockpit_hot" : "near";
        }
    }
}
