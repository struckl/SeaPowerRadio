using System;
using System.Collections.Generic;
using System.IO;

namespace SeaPowerRadio.Radio;

/// <summary>How a call sits in the mix: replies to the player in front, alerts as loud, reports in the background.</summary>
internal enum CallRole
{
    Reply,
    Alert,
    Report,
}

/// <summary>One spoken variant of a phrase, as rendered (tools/voicegen/phrases.py → catalog.txt).</summary>
internal sealed class PhraseVariant
{
    public string Phrase;
    public int N;
    public string Slot;      // null, "trk", "brg", "num", "alt", "count"
    public bool HasA;        // clip <Phrase>_<N>_a before the number
    public bool HasB;        // clip <Phrase>_<N>_b after the number
    public bool PauseA;      // the text pauses between _a and the number ("climbing, | angels twenty")
    public bool PauseB;      // the text pauses between the number and _b ("track 7034, | faded")
    public int Priority;
    public CallRole Role;
}

/// <summary>
/// What can be said, and how stock voice events map onto it. Both files are generated next to the voice library
/// by tools/voicegen/phrases.py: catalog.txt (phrase variants and their number slots) and stockmap.txt (stock
/// voice.ini key → phrase, plus what the number passed with the stock call means).
/// </summary>
internal static class Catalog
{
    private static readonly Dictionary<string, List<PhraseVariant>> Phrases =
        new Dictionary<string, List<PhraseVariant>>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string phrase, string number)> Stock =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> LastVariant = new Dictionary<string, int>();
    private static readonly Random Rng = new Random();

    public static bool Loaded { get; private set; }

    public static void Load(string voicesDirectory)
    {
        Phrases.Clear();
        Stock.Clear();
        string catalog = Path.Combine(voicesDirectory, "catalog.txt");
        string stockmap = Path.Combine(voicesDirectory, "stockmap.txt");
        if (!File.Exists(catalog) || !File.Exists(stockmap))
        {
            ModLog.Warn($"Voice catalog missing in {voicesDirectory}; stock voices stay on.");
            return;
        }
        foreach (string line in ReadRows(catalog))
        {
            string[] f = line.Split('|');
            if (f.Length < 6) continue;
            var v = new PhraseVariant
            {
                Phrase = f[0],
                N = int.Parse(f[1]),
                Slot = f[2] == "-" ? null : f[2],
                HasA = f[3] == "1",
                HasB = f[4] == "1",
                Priority = int.Parse(f[5]),
                Role = ParseRole(f.Length > 6 ? f[6] : null, int.Parse(f[5])),
                PauseA = f.Length > 7 && f[7] == "1",
                PauseB = f.Length > 8 && f[8] == "1",
            };
            if (!Phrases.TryGetValue(v.Phrase, out List<PhraseVariant> list))
                Phrases[v.Phrase] = list = new List<PhraseVariant>();
            list.Add(v);
        }
        foreach (string line in ReadRows(stockmap))
        {
            string[] f = line.Split('|');
            if (f.Length < 3) continue;
            Stock[f[0]] = (f[1] == "-" ? null : f[1], f[2] == "-" ? null : f[2]);
        }
        Loaded = true;
        ModLog.Info($"Voice catalog: {Phrases.Count} phrases, {Stock.Count} stock keys mapped.");
    }

    private static CallRole ParseRole(string role, int priority)
    {
        switch (role)
        {
            case "reply": return CallRole.Reply;
            case "alert": return CallRole.Alert;
            case "report": return CallRole.Report;
            default: return priority <= 2 ? CallRole.Alert : priority >= 7 ? CallRole.Reply : CallRole.Report;
        }
    }

    private static IEnumerable<string> ReadRows(string file)
    {
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith("#"))
                yield return line;
        }
    }

    /// <summary>
    /// Look up a stock voice key. Returns false for keys we don't know (other mods' lines) – those keep playing.
    /// <paramref name="phrase"/> is null for keys the mod swallows on purpose.
    /// </summary>
    public static bool TryStock(string key, out string phrase, out string numberKind)
    {
        if (Stock.TryGetValue(key, out var entry))
        {
            phrase = entry.phrase;
            numberKind = entry.number;
            return true;
        }
        phrase = null;
        numberKind = null;
        return false;
    }

    public static bool Has(string phrase) => Phrases.ContainsKey(phrase);

    public static int PriorityOf(string phrase) =>
        Phrases.TryGetValue(phrase, out List<PhraseVariant> list) && list.Count > 0 ? list[0].Priority : 6;

    /// <summary>
    /// A variant to speak. Variants that need a number are only used when there is one, and only variants whose
    /// clips exist in this voice (a library can be partly rendered); the variant this voice used last time is
    /// avoided, so nobody says the identical line twice in a row. <paramref name="only"/> &gt; 0 = that variant or none
    /// (a phrase whose variants don't all fit the situation: "ahead full." / "increasing speed.").
    /// </summary>
    public static PhraseVariant Pick(string phrase, string voice, bool haveNumber, int only = 0)
    {
        if (!Phrases.TryGetValue(phrase, out List<PhraseVariant> all))
            return null;
        var usable = all.FindAll(v => (only == 0 || v.N == only) && (haveNumber || v.Slot == null) && Rendered(v, voice));
        if (usable.Count == 0)
            return null;
        string memo = voice + "|" + phrase;
        LastVariant.TryGetValue(memo, out int last);
        var fresh = usable.FindAll(v => v.N != last);
        List<PhraseVariant> pool = fresh.Count > 0 ? fresh : usable;
        PhraseVariant pick = pool[Rng.Next(pool.Count)];
        LastVariant[memo] = pick.N;
        return pick;
    }

    /// <summary>Does this voice have the clips of this variant?</summary>
    public static bool Rendered(PhraseVariant v, string voice)
    {
        if (voice == null) return false;
        if (v.Slot == null)
            return ClipLibrary.Exists(voice, $"{v.Phrase}_{v.N}");
        return (!v.HasA || ClipLibrary.Exists(voice, $"{v.Phrase}_{v.N}_a"))
               && (!v.HasB || ClipLibrary.Exists(voice, $"{v.Phrase}_{v.N}_b"));
    }

    /// <summary>
    /// Does the text pause where this clip meets the number next to it? Only a phrase half (&lt;Phrase&gt;_&lt;N&gt;_a /
    /// _b) can say no; anything else next to a number (a callsign) is followed by a pause anyway.
    /// </summary>
    public static bool PausesAtNumber(string clipKey)
    {
        bool a = clipKey.EndsWith("_a", StringComparison.Ordinal), b = clipKey.EndsWith("_b", StringComparison.Ordinal);
        if (!a && !b)
            return true;
        string variant = clipKey.Substring(0, clipKey.Length - 2);
        int cut = variant.LastIndexOf('_');
        if (cut < 0 || !int.TryParse(variant.Substring(cut + 1), out int n)
            || !Phrases.TryGetValue(variant.Substring(0, cut), out List<PhraseVariant> all))
            return true;
        PhraseVariant v = all.Find(x => x.N == n);
        return v == null || (a ? v.PauseA : v.PauseB);
    }

    /// <summary>Can this voice say the phrase at all (any variant rendered, or variant <paramref name="only"/>)?</summary>
    public static bool CanSay(string phrase, string voice, bool haveNumber = false, int only = 0) =>
        Phrases.TryGetValue(phrase, out List<PhraseVariant> all)
        && all.Exists(v => (only == 0 || v.N == only) && (haveNumber || v.Slot == null) && Rendered(v, voice));
}
