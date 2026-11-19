using System.Collections.Generic;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Voices;

/// <summary>What kind of radio callsign an element gets.</summary>
internal enum CallsignKind
{
    Fighter,
    Attack,
    Patrol,      // maritime patrol / ASW fixed wing
    Helicopter,
    Support,     // AEW, tanker, transport, recon
    Ship,
    Submarine,
    Shore,       // airbase, land unit
}

/// <summary>
/// Generated NATO-style voice callsigns, unique within a mission. Aircraft get "word + flight-position"
/// ("Viper two-one"); ships and subs get a single word. Every word comes from a fixed list because each one has to
/// exist as a pre-rendered audio fragment in every voice - adding a word here means adding it to the phrase catalog.
/// </summary>
internal sealed class Callsigns
{
    // The words of tools/voicegen/rules.py, rendered in every voice.
    private static Dictionary<CallsignKind, string[]> Words => Radio.ClipRules.CallsignWords;

    private readonly Dictionary<CallsignKind, Queue<string>> freeWords = new Dictionary<CallsignKind, Queue<string>>();
    private readonly Dictionary<CallsignKind, int> wordReuse = new Dictionary<CallsignKind, int>();
    private readonly HashSet<string> used = new HashSet<string>();

    public Callsigns(string missionSeed)
    {
        var rng = new StableRandom("callsigns|" + missionSeed);
        foreach (var pair in Words)
            freeWords[pair.Key] = new Queue<string>(rng.Shuffled(pair.Value));
    }

    /// <summary>Next unused word of that kind. When a list runs out, words come round again with a new number.</summary>
    public string NextWord(CallsignKind kind)
    {
        Queue<string> queue = freeWords[kind];
        string word = queue.Dequeue();
        queue.Enqueue(word);
        return word;
    }

    /// <summary>
    /// Callsign for one member of a flight. <paramref name="flightWord"/> and <paramref name="flightNumber"/> come
    /// from the first call for the flight; position is 1-based ("Viper 2-1" is the lead of Viper flight 2).
    /// </summary>
    public string Aircraft(string flightWord, int flightNumber, int position)
    {
        string callsign = $"{flightWord} {flightNumber}-{position}";
        used.Add(callsign);
        return callsign;
    }

    /// <summary>Lowest rendered flight number not yet taken for this word, or -1 when all are in use.</summary>
    public int NextFlightNumber(string flightWord)
    {
        for (int n = Radio.ClipRules.FlightMin; n <= Radio.ClipRules.FlightMax; n++)
            if (!used.Contains($"{flightWord} {n}-1"))
                return n;
        return -1;
    }

    /// <summary>
    /// Callsigns for <paramref name="count"/> aircraft flying together: flights of at most four, numbers 1-9, a
    /// fresh word when a word's nine flights are used up. Only those combinations exist as audio fragments.
    /// </summary>
    public List<string> Flight(CallsignKind kind, int count)
    {
        const int size = Radio.ClipRules.PositionMax;
        var result = new List<string>(count);
        string word = NextWord(kind);
        for (int start = 0; start < count; start += size)
        {
            int flight = NextFlightNumber(word);
            for (int tries = 0; flight < 0 && tries < Words[kind].Length; tries++)
            {
                word = NextWord(kind);
                flight = NextFlightNumber(word);
            }
            if (flight < 0) flight = Radio.ClipRules.FlightMax; // every word of this kind full: share rather than fail
            for (int pos = 1; pos <= size && start + pos - 1 < count; pos++)
                result.Add(Aircraft(word, flight, pos));
        }
        return result;
    }

    /// <summary>A callsign that is taken (a unit of a campaign keeps it): generation will not hand it out again.</summary>
    public void Reserve(string callsign)
    {
        if (!string.IsNullOrEmpty(callsign))
            used.Add(callsign);
    }

    /// <summary>
    /// The single-word callsign (ship, submarine or shore list) that <paramref name="say"/> names, in its listed spelling;
    /// null when it is none. Those words are rendered standalone in every voice, so an alias line may hand one out.
    /// </summary>
    public static string SingleWord(string say)
    {
        if (string.IsNullOrWhiteSpace(say)) return null;
        string s = say.Trim();
        foreach (CallsignKind kind in new[] { CallsignKind.Ship, CallsignKind.Submarine, CallsignKind.Shore })
            foreach (string w in Words[kind])
                if (string.Equals(w, s, System.StringComparison.OrdinalIgnoreCase))
                    return w;
        return null;
    }

    public string Single(CallsignKind kind)
    {
        // A free word first (an alias line may hold one), only then a numbered one.
        for (int i = 0; i < Words[kind].Length; i++)
        {
            string free = NextWord(kind);
            if (used.Add(free))
                return free;
        }
        string word = NextWord(kind);
        wordReuse.TryGetValue(kind, out int n);
        for (n = n == 0 ? 2 : n; n < 10; n++)
        {
            string numbered = $"{word} {n}";
            if (used.Add(numbered))
            {
                wordReuse[kind] = n;
                return numbered;
            }
        }
        return word;
    }
}
