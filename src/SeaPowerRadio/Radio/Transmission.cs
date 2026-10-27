using System;
using System.Collections.Generic;
using SeaPower;
using SeapowerUI.ViewModels;
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
    public int CallsignClips;                                  // how many of the first clips are the callsign
    public string Phrase;                                      // main phrase key (for queue rules / logs)
    public int Priority;                                       // 1 = most urgent … 7 = acknowledgement
    public string Profile;                                     // radio flavour, see RadioFx
    public float SubmittedAt;
    public string Text;                                        // human-readable, for the log
    public readonly List<Alert> Alerts = new List<Alert>();     // the bottom-right messages this call speaks

    public CallRole Role = CallRole.Report;                     // from the main phrase, see Catalog

    public bool Urgent => Role == CallRole.Alert;

    /// <summary>
    /// Queue order: alerts first, then units answering the player – an order should be acknowledged right away,
    /// like the stock system's command channel – then reports by priority.
    /// </summary>
    public int Rank => Role == CallRole.Alert ? -1000 + Priority : Role == CallRole.Reply ? -100 : Priority;
}

/// <summary>
/// Turns "Viper 2-1 says EngageTrack 7034" into the clip sequence the voice library holds. Mirrors the assembly
/// rules of tools/voicegen/reel.py: callsign word + flight-position, phrase part a, number pieces, phrase part b.
/// Every clip carries its half of the pause it was cut in, so they are joined back to back – with the pauses at
/// a number closed up (<see cref="MaxPause"/>).
/// </summary>
internal static class TransmissionBuilder
{
    // Most silence left where two clips join. The takes speak a pause at every cut, so the cut falls into silence –
    // also where nobody pauses ("bearing, | two seven three"): there the two halves added up to a quarter to half a
    // second, which is what made the numbers sound read off a card.
    private const float NumberPause = 0.05f;   // word ↔ number, no pause in the text: "bearing two seven three"
    private const float GroupPause = 0.08f;    // between the digit groups: "seven zero, three four"
    private const float SpokenPause = 0.25f;   // a pause the speaker makes anyway (after the callsign, at a comma)

    /// <summary>Most silence allowed where <paramref name="next"/> follows <paramref name="previous"/>.</summary>
    public static float MaxPause(string previous, string next)
    {
        // "Viper | six-two", "Lancer | three": the takes pause after the callsign word so the cut falls into silence,
        // the callsign itself is said in one go.
        if (IsCallsignWord(previous) && IsCallsignNumber(next))
            return NumberPause;
        bool a = IsNumberPiece(previous), b = IsNumberPiece(next);
        if (a && b)
            return GroupPause;
        if (a || b)
            return Catalog.PausesAtNumber(a ? next : previous) ? SpokenPause : NumberPause;
        return SpokenPause;
    }

    private static bool IsNumberPiece(string key) =>
        key.StartsWith("brg_", StringComparison.Ordinal) || key.StartsWith("pm_", StringComparison.Ordinal)
        || key.StartsWith("pe_", StringComparison.Ordinal) || key.StartsWith("dm_", StringComparison.Ordinal)
        || key.StartsWith("alt_", StringComparison.Ordinal) || key.StartsWith("cnt_", StringComparison.Ordinal);

    private static bool IsCallsignWord(string key) =>
        key.StartsWith("cs_", StringComparison.Ordinal) || key.StartsWith("sq_", StringComparison.Ordinal);

    private static bool IsCallsignNumber(string key) =>
        key.StartsWith("fp_", StringComparison.Ordinal) || key.StartsWith("cn_", StringComparison.Ordinal);

    public static Transmission Build(RosterElement speaker, string phrase, int? number, int variant = 0)
    {
        var t = Start(speaker);
        return Append(t, phrase, number, variant) ? t : null;
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
        t.Clips.AddRange(CallsignFor(speaker, out string said));
        t.CallsignClips = t.Clips.Count;
        t.Text = said + ",";
        return t;
    }

    /// <summary>
    /// The callsign as the game shows it right now: a ship's short name ("Pargo"), an aircraft's squadron callsign
    /// ("Lancer 3", a wingman "Lancer 3-2" – read live, because formations rename aircraft). Falls back to the
    /// generated NATO callsign when the name wasn't rendered in this voice (custom mission names, mod ships,
    /// squadrons without callsigns).
    /// </summary>
    public static List<string> CallsignFor(RosterElement e, out string said)
    {
        ObjectBase unit = e.Unit;
        string voice = e.Voice?.Id;
        if (unit != null && voice != null)
        {
            GameName name = Roster.Roster.NameOf(unit);
            if (name != null && ClipLibrary.Exists(voice, name.Key))
            {
                if (!name.Squadron)
                {
                    said = name.Text;
                    return new List<string> { name.Key };
                }
                if (Names.TryParseAircraft(unit.Name.Value, out _, out int number, out int position))
                {
                    var clips = new List<string> { name.Key };
                    if (position > 0 && number <= 9 && position <= 4)
                        clips.Add($"fp_{number}{position}");
                    else if (number >= 1 && number <= 20)
                        clips.Add($"cn_{number}");
                    said = unit.Name.Value;
                    return clips;
                }
            }
        }
        said = e.Callsign;
        return new List<string>(Callsign(e.Callsign));
    }

    /// <summary>
    /// Add a phrase (with an optional number) to the transmission, <paramref name="variant"/> &gt; 0 = only that
    /// variant. False if it can't be spoken.
    /// </summary>
    public static bool Append(Transmission t, string phrase, int? number, int variant = 0)
    {
        if (t.Voice == null)
            return false;
        PhraseVariant v = Catalog.Pick(phrase, t.Voice, number.HasValue, variant);
        if (v == null)
            return false;

        if (t.Phrase != null)
        {
            t.Clips.Add(null); // a beat between two phrases in one call
        }
        else
        {
            t.Phrase = phrase;
            t.Role = v.Role;
        }
        t.Priority = Math.Min(t.Priority, v.Priority);

        if (v.Slot == null)
        {
            t.Clips.Add($"{phrase}_{v.N}");
            t.Text += $" {phrase}";
        }
        else
        {
            if (v.HasA) t.Clips.Add($"{phrase}_{v.N}_a");
            t.Clips.AddRange(Number(v.Slot, number.Value, t.Voice));
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

    /// <summary>
    /// Number pieces: brg_DDD (a whole bearing), dm_D (leading digit), pm_PP (pair, more to come), pe_PP (closing
    /// pair). Libraries rendered before whole bearings existed fall back to dm + pe.
    /// </summary>
    public static IEnumerable<string> Number(string kind, int value, string voice = null)
    {
        switch (kind)
        {
            case "brg":
                value = ((value % 360) + 360) % 360;
                string b = value.ToString("000");
                if (voice == null || ClipLibrary.Exists(voice, "brg_" + b))
                {
                    yield return "brg_" + b;
                    break;
                }
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
