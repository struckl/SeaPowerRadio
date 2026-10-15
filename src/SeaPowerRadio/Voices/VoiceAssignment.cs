using System.Collections.Generic;
using System.Linq;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Voices;

/// <summary>One rendered voice in the library: a base TTS voice plus fixed rate/pitch/radio quirks.</summary>
internal sealed class VoicePersona
{
    public string Id;          // folder name in the voice library, e.g. "us_m_03"
    public string Language;    // "en" in v1; Warsaw Pact voices plug in as another language
    public string Gender;      // "m" / "f"
    public string Accent;      // "us", "gb", "ca", "au", "nz", "ie"
}

/// <summary>
/// Gives every element of a mission its own voice. Deterministic per mission (same seed → same voices after a
/// reload) and unique while the pool lasts; once every persona is in use the pool is reshuffled and reused, so two
/// elements share a voice only in missions with more elements than voices.
/// </summary>
internal sealed class VoiceAssignment
{
    private readonly List<VoicePersona> order;
    private readonly Dictionary<string, VoicePersona> byElement = new Dictionary<string, VoicePersona>();
    private int next;

    public VoiceAssignment(IEnumerable<VoicePersona> pool, string missionSeed, string language = "en")
    {
        var rng = new StableRandom("voices|" + missionSeed);
        // Sort first so the shuffle does not depend on the order the library happened to be read from disk.
        order = rng.Shuffled(pool.Where(p => p.Language == language).OrderBy(p => p.Id));
    }

    public int PoolSize => order.Count;

    /// <summary>
    /// Voice for an element. <paramref name="elementKey"/> must be stable across reloads (unit id from the mission
    /// file, or carrier + air-wing slot for aircraft not launched yet). Elements must be requested in a stable order.
    /// </summary>
    public VoicePersona For(string elementKey)
    {
        if (byElement.TryGetValue(elementKey, out VoicePersona persona))
            return persona;
        if (order.Count == 0)
            return null;
        persona = order[next % order.Count];
        next++;
        byElement[elementKey] = persona;
        return persona;
    }
}
