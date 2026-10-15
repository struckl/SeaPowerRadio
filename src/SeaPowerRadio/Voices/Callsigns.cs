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
/// exist as a pre-rendered audio fragment in every voice — adding a word here means adding it to the phrase catalog.
/// </summary>
internal sealed class Callsigns
{
    // Word lists double as the phrase catalog's callsign fragments (tools/voicegen). Keep them speakable on a noisy
    // net: two syllables or a strong single one, no two words that sound alike.
    public static readonly Dictionary<CallsignKind, string[]> Words = new Dictionary<CallsignKind, string[]>
    {
        [CallsignKind.Fighter] = new[] { "Viper", "Cobra", "Falcon", "Razor", "Lancer", "Sabre", "Jester", "Rapier", "Talon", "Venom", "Ghost", "Raptor" },
        [CallsignKind.Attack] = new[] { "Hammer", "Anvil", "Mace", "Dagger", "Tusk", "Spear", "Brick", "Boxer" },
        [CallsignKind.Patrol] = new[] { "Trident", "Pelican", "Albatross", "Corsair", "Neptune", "Marlin" },
        [CallsignKind.Helicopter] = new[] { "Seahawk", "Dipper", "Lynx", "Kingfish", "Dusty", "Rotor" },
        [CallsignKind.Support] = new[] { "Magic", "Drover", "Tankard", "Wizard", "Mercury", "Darkstar" },
        [CallsignKind.Ship] = new[] { "Bulwark", "Sentinel", "Victory", "Warden", "Guardian", "Valiant", "Resolute", "Steadfast", "Vigilant", "Defiant", "Intrepid", "Courage" },
        [CallsignKind.Submarine] = new[] { "Shadow", "Silent", "Deepwater", "Barracuda" },
        [CallsignKind.Shore] = new[] { "Castle", "Bastion", "Harbor", "Lighthouse" },
    };

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

    /// <summary>Lowest flight number 1–9 not yet taken for this word, or -1 when all nine are in use.</summary>
    public int NextFlightNumber(string flightWord)
    {
        for (int n = 1; n < 10; n++)
            if (!used.Contains($"{flightWord} {n}-1"))
                return n;
        return -1;
    }

    /// <summary>
    /// Callsigns for <paramref name="count"/> aircraft flying together: flights of at most four, numbers 1–9, a
    /// fresh word when a word's nine flights are used up. Only those combinations exist as audio fragments.
    /// </summary>
    public List<string> Flight(CallsignKind kind, int count)
    {
        var result = new List<string>(count);
        string word = NextWord(kind);
        for (int start = 0; start < count; start += 4)
        {
            int flight = NextFlightNumber(word);
            for (int tries = 0; flight < 0 && tries < Words[kind].Length; tries++)
            {
                word = NextWord(kind);
                flight = NextFlightNumber(word);
            }
            if (flight < 0) flight = 9; // every word of this kind full: share rather than fail
            for (int pos = 1; pos <= 4 && start + pos - 1 < count; pos++)
                result.Add(Aircraft(word, flight, pos));
        }
        return result;
    }

    public string Single(CallsignKind kind)
    {
        string word = NextWord(kind);
        if (used.Add(word))
            return word;
        // Word list exhausted for this kind: append a number ("Sentinel 2").
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
