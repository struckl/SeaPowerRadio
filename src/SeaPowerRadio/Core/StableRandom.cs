using System;
using System.Collections.Generic;

namespace SeaPowerRadio.Core;

/// <summary>
/// Deterministic randomness for per-mission choices (callsigns, voices). Seeded from a stable FNV-1a hash rather
/// than <c>string.GetHashCode</c>, which is not guaranteed stable across runtimes — a reload of the same mission
/// must give every element the same callsign and voice.
/// </summary>
internal sealed class StableRandom
{
    private readonly Random rng;

    public StableRandom(string seed) => rng = new Random(Hash(seed));

    public static int Hash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    public int Next(int maxExclusive) => rng.Next(maxExclusive);

    /// <summary>Fisher–Yates shuffle into a new list.</summary>
    public List<T> Shuffled<T>(IEnumerable<T> items)
    {
        var list = new List<T>(items);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
