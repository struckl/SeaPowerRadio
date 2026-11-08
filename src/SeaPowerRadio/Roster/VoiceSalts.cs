using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace SeaPowerRadio.Roster;

/// <summary>
/// "Random voices each mission": a fresh start of a mission gets a new random salt for the voice shuffle; loading a save of that
/// mission reuses the salt of its latest start. The latest salt per mission is kept in
/// <c>BepInEx/config/SeaPowerRadio/voice-salts.txt</c>, one line <c>mission seed|salt</c> (the oldest lines are dropped after
/// <see cref="MaxEntries"/> missions). A save with no salt on record uses the plain seed.
/// </summary>
internal static class VoiceSalts
{
    private const int MaxEntries = 400;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "SeaPowerRadio", "voice-salts.txt");

    /// <summary>The salt for this mission start, or null for the plain per-mission seed.</summary>
    public static string ForStart(string missionSeed, bool freshStart)
    {
        try
        {
            List<KeyValuePair<string, string>> entries = Read();
            if (!freshStart)
            {
                string found = entries.Where(e => e.Key == missionSeed).Select(e => e.Value).LastOrDefault();
                return string.IsNullOrEmpty(found) ? null : found;
            }
            string salt = Guid.NewGuid().ToString("N").Substring(0, 8);
            entries.RemoveAll(e => e.Key == missionSeed);
            entries.Add(new KeyValuePair<string, string>(missionSeed, salt));
            if (entries.Count > MaxEntries)
                entries.RemoveRange(0, entries.Count - MaxEntries);
            Write(entries);
            return salt;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[voices] could not use the voice salt file, using the plain seed: {ex.Message}");
            return null;
        }
    }

    private static List<KeyValuePair<string, string>> Read()
    {
        var entries = new List<KeyValuePair<string, string>>();
        if (!File.Exists(FilePath))
            return entries;
        foreach (string raw in File.ReadAllLines(FilePath))
        {
            int cut = raw.LastIndexOf('|');
            if (cut > 0 && cut < raw.Length - 1)
                entries.Add(new KeyValuePair<string, string>(raw.Substring(0, cut), raw.Substring(cut + 1).Trim()));
        }
        return entries;
    }

    private static void Write(List<KeyValuePair<string, string>> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllLines(FilePath, entries.Select(e => e.Key + "|" + e.Value));
    }
}
