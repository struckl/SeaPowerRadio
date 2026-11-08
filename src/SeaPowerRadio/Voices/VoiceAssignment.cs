using System;
using System.Collections.Generic;
using System.Linq;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Voices;

/// <summary>One rendered voice in the library: a base TTS voice plus fixed rate/pitch/radio quirks.</summary>
internal sealed class VoicePersona
{
    public VoicePack Pack;     // the pack this voice belongs to
    public string Id;          // folder name in the pack, e.g. "us_m_03" (unique inside the pack)
    public string Language;    // language column of personas.txt
    /// <summary>Unique across packs: <c>packId/personaId</c>, what the transmissions and logs carry.</summary>
    public string FullId => Pack.Id + "/" + Id;
    public string Gender;      // "m" / "f"
    public string Accent;      // "us", "gb", "ca", "au", "nz", "ie", "sc", "ni"
}

/// <summary>
/// Gives every element of a mission its own voice. Named units (a ship's in-game name, a squadron callsign) can
/// only use the few voices their name was rendered in, so they ask for those first; everyone else gets the next
/// unused voice of a per-mission shuffle. Deterministic per mission (same seed and order → same voices after a
/// reload) and unique while the pool lasts; after that voices are shared.
/// </summary>
internal sealed class VoiceAssignment
{
    private readonly List<VoicePersona> order;
    private readonly Dictionary<string, VoicePersona> byId;
    private readonly Dictionary<string, VoicePersona> byElement = new Dictionary<string, VoicePersona>();
    private readonly HashSet<string> used = new HashSet<string>();
    private int next;

    private readonly string seed;       // mission seed, plus the salt when voices are randomised
    private readonly bool randomize;
    private readonly Dictionary<string, int> familyNext = new Dictionary<string, int>();

    /// <param name="salt">
    /// Null = today's deterministic shuffle (same voices at every start of the mission). A salt (Settings.RandomVoices) is mixed
    /// into the shuffle and into the choice among a named unit's candidate voices, so a fresh start sounds different.
    /// </param>
    public VoiceAssignment(VoicePack pack, string missionSeed, string salt = null)
    {
        randomize = !string.IsNullOrEmpty(salt);
        seed = randomize ? missionSeed + "|salt:" + salt : missionSeed;
        // The NATO pack keeps the 1.1.0 seed, so its voices per mission are the ones players already know.
        var rng = new StableRandom(pack.Id == VoicePacks.NatoId ? "voices|" + seed : "voices|" + seed + "|" + pack.Id);
        // Sort first so the shuffle does not depend on the order the library happened to be read from disk.
        order = rng.Shuffled(pack.Personas.OrderBy(p => p.Id, StringComparer.Ordinal));
        byId = order.ToDictionary(p => p.Id);
    }

    public int PoolSize => order.Count;

    /// <summary>"Female voices" off: women's voices are skipped while a male voice is still available.</summary>
    private static bool Allowed(VoicePersona p) => p.Gender != "f" || Settings.FemaleVoices == null || Settings.FemaleVoices.Value;

    /// <summary>Whether this persona may speak with the current settings.</summary>
    public static bool IsAllowed(VoicePersona p) => p == null || Allowed(p);

    /// <summary>
    /// Next unused voice for an element (stable key: unit id, or carrier + air-wing slot). <paramref name="accents"/> = the
    /// accents of the unit's nation, best first (accents.txt): the voices of the first accent come first, then the next; when
    /// every voice of that family is taken they are shared inside the family, never a foreign accent. Null (a nation without
    /// accents) = the whole pack, as before.
    /// </summary>
    public VoicePersona For(string elementKey, string[] accents = null)
    {
        if (byElement.TryGetValue(elementKey, out VoicePersona persona))
            return persona;
        if (order.Count == 0)
            return null;
        if (accents != null)
        {
            persona = FromFamily(accents);
            if (persona != null)
                return Bind(elementKey, persona);
        }
        persona = null;
        for (int i = 0; i < order.Count && persona == null; i++)
        {
            VoicePersona candidate = order[(next + i) % order.Count];
            if (!used.Contains(candidate.Id) && Allowed(candidate))
                persona = candidate;
        }
        // Everyone is taken: share (a man, if women are switched off).
        persona ??= order.Where(Allowed).Skip(next % Math.Max(1, order.Count(Allowed))).FirstOrDefault() ?? order[next % order.Count];
        next++;
        return Bind(elementKey, persona);
    }

    private static bool InFamily(VoicePersona p, string[] accents) =>
        accents.Any(a => string.Equals(a, p.Accent, StringComparison.OrdinalIgnoreCase));

    /// <summary>The next voice of an accent family: unused ones first (first accent first, shuffled inside it), then shared round-robin. Null when the family has no allowed voice.</summary>
    private VoicePersona FromFamily(string[] accents)
    {
        var family = accents.SelectMany(a => order.Where(p => string.Equals(p.Accent, a, StringComparison.OrdinalIgnoreCase) && Allowed(p))).ToList();
        if (family.Count == 0)
            return null;
        VoicePersona free = family.FirstOrDefault(p => !used.Contains(p.Id));
        if (free != null)
            return free;
        string key = string.Join(",", accents);
        familyNext.TryGetValue(key, out int n);
        familyNext[key] = n + 1;
        return family[n % family.Count];
    }

    /// <summary>
    /// A voice from <paramref name="candidates"/> (the voices a name was rendered in): the first one nobody uses
    /// yet, else the first one even if shared – saying the right name matters more than a unique voice.
    /// Falls back to <see cref="For"/> when no candidate is in the library.
    /// </summary>
    public VoicePersona Prefer(string elementKey, IEnumerable<string> candidates, string[] accents = null)
    {
        var known = candidates?.Where(byId.ContainsKey).ToList();
        if (known == null || known.Count == 0)
            return For(elementKey, accents);
        var men = known.Where(c => Allowed(byId[c])).ToList();
        if (men.Count > 0)
            known = men;                 // none of the voices the name exists in is a man: the name matters more
        if (accents != null)
        {
            // The accent of the unit's nation; when the name was rendered in none of them the name still matters more.
            var inFamily = known.Where(c => InFamily(byId[c], accents)).ToList();
            if (inFamily.Count > 0)
                known = inFamily;
        }
        if (randomize)                   // a salted start picks among the name's voices in a new order
            known = known.OrderBy(c => StableRandom.Hash(seed + "|name|" + elementKey + "|" + c)).ThenBy(c => c, StringComparer.Ordinal).ToList();
        string id = known.FirstOrDefault(c => !used.Contains(c)) ?? known[0];
        return Bind(elementKey, byId[id]);
    }

    /// <summary>A new voice for an element that already has one (the settings changed): forgets the old binding.</summary>
    public VoicePersona Rebind(string elementKey, IEnumerable<string> candidates, string[] accents = null)
    {
        byElement.Remove(elementKey);
        return candidates != null ? Prefer(elementKey, candidates, accents) : For(elementKey, accents);
    }

    public bool IsAmong(VoicePersona persona, IEnumerable<string> candidates) =>
        persona != null && candidates != null && candidates.Contains(persona.Id);

    /// <summary>Keeps a voice that is remembered for a unit (campaign) for that unit: new units avoid it while the pool lasts.</summary>
    public void Reserve(string personaId)
    {
        if (byId.ContainsKey(personaId))
            used.Add(personaId);
    }

    /// <summary>Gives an element the voice it had before (campaign memory).</summary>
    public VoicePersona Claim(string elementKey, VoicePersona persona)
    {
        byElement.Remove(elementKey);
        return Bind(elementKey, persona);
    }

    private VoicePersona Bind(string elementKey, VoicePersona persona)
    {
        byElement[elementKey] = persona;
        used.Add(persona.Id);
        return persona;
    }
}
