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

    public VoiceAssignment(VoicePack pack, string missionSeed)
    {
        // The NATO pack keeps the 1.1.0 seed, so its voices per mission are the ones players already know.
        var rng = new StableRandom(pack.Id == VoicePacks.NatoId ? "voices|" + missionSeed : "voices|" + missionSeed + "|" + pack.Id);
        // Sort first so the shuffle does not depend on the order the library happened to be read from disk.
        order = rng.Shuffled(pack.Personas.OrderBy(p => p.Id, StringComparer.Ordinal));
        byId = order.ToDictionary(p => p.Id);
    }

    public int PoolSize => order.Count;

    /// <summary>"Female voices" off: women's voices are skipped while a male voice is still available.</summary>
    private static bool Allowed(VoicePersona p) => p.Gender != "f" || Settings.FemaleVoices == null || Settings.FemaleVoices.Value;

    /// <summary>Whether this persona may speak with the current settings.</summary>
    public static bool IsAllowed(VoicePersona p) => p == null || Allowed(p);

    /// <summary>Next unused voice for an element (stable key: unit id, or carrier + air-wing slot).</summary>
    public VoicePersona For(string elementKey)
    {
        if (byElement.TryGetValue(elementKey, out VoicePersona persona))
            return persona;
        if (order.Count == 0)
            return null;
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

    /// <summary>
    /// A voice from <paramref name="candidates"/> (the voices a name was rendered in): the first one nobody uses
    /// yet, else the first one even if shared – saying the right name matters more than a unique voice.
    /// Falls back to <see cref="For"/> when no candidate is in the library.
    /// </summary>
    public VoicePersona Prefer(string elementKey, IEnumerable<string> candidates)
    {
        var known = candidates?.Where(byId.ContainsKey).ToList();
        if (known == null || known.Count == 0)
            return For(elementKey);
        var men = known.Where(c => Allowed(byId[c])).ToList();
        if (men.Count > 0)
            known = men;                 // none of the voices the name exists in is a man: the name matters more
        string id = known.FirstOrDefault(c => !used.Contains(c)) ?? known[0];
        return Bind(elementKey, byId[id]);
    }

    /// <summary>A new voice for an element that already has one (the settings changed): forgets the old binding.</summary>
    public VoicePersona Rebind(string elementKey, IEnumerable<string> candidates)
    {
        byElement.Remove(elementKey);
        return candidates != null ? Prefer(elementKey, candidates) : For(elementKey);
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
