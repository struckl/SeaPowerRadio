using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Voices;

/// <summary>
/// Player-editable voice gender per squadron (<c>BepInEx/config/SeaPowerRadio/squadrons.txt</c>, docs/squadron-voices.md):
/// <c>Bear Aces = female</c>, <c>Ghostriders = male</c>. Text file only, no setting in the game's menu. A name without '*'
/// matches every squadron whose name contains it (case and punctuation ignored); a '*' in it is a wildcard. Read at every
/// mission start, so an edit applies from the next mission. The rule beats the "Female voices" setting for that squadron.
/// </summary>
internal static class SquadronVoices
{
    private sealed class Rule
    {
        public string Pattern;   // slug, '*' = wildcard
        public string Gender;    // "m" / "f"
        public string Source;    // file:line, for the log
    }

    private static readonly List<Rule> rules = new List<Rule>();

    public static string UserFile => Path.Combine(Paths.ConfigPath, "SeaPowerRadio", "squadrons.txt");

    public static void Reload()
    {
        rules.Clear();
        EnsureUserFile();
        if (!File.Exists(UserFile)) return;
        string[] lines = File.ReadAllLines(UserFile);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith(";")) continue;
            string where = $"SeaPowerRadio/squadrons.txt:{i + 1}";
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                ModLog.Warn($"[squadrons] {where}: no '=' in \"{line}\", skipped");
                continue;
            }
            string left = line.Substring(0, eq).Trim();
            string gender = ParseGender(line.Substring(eq + 1).Trim());
            if (gender == null)
            {
                ModLog.Warn($"[squadrons] {where}: \"{line}\" needs 'male' or 'female' after the '=', skipped");
                continue;
            }
            // Slug each piece between wildcards, so "VF-11 *" and "vf_11*" are the same rule.
            string[] parts = left.Split('*');
            for (int p = 0; p < parts.Length; p++)
                parts[p] = parts[p].Trim().Length == 0 ? "" : Names.Slug(parts[p]);
            string pattern = string.Join("*", parts);
            if (pattern.Replace("*", "").Length == 0)
            {
                ModLog.Warn($"[squadrons] {where}: \"{line}\" needs a squadron name, skipped");
                continue;
            }
            rules.RemoveAll(r => r.Pattern == pattern);   // the later line wins
            rules.Add(new Rule { Pattern = pattern, Gender = gender, Source = where });
        }
        ModLog.Info($"[squadrons] {rules.Count} squadron voice rule(s)");
    }

    private static string ParseGender(string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "male": case "m": case "man": case "men": return "m";
            case "female": case "f": case "woman": case "women": return "f";
            default: return null;
        }
    }

    /// <summary>"m" / "f" for a squadron the player made a rule for, else null. When several rules match, the last one in the file wins.</summary>
    public static string GenderFor(string squadronName)
    {
        if (rules.Count == 0 || string.IsNullOrWhiteSpace(squadronName)) return null;
        string slug = Names.Slug(squadronName);
        string gender = null;
        foreach (Rule r in rules)
            if (Matches(slug, r.Pattern))
                gender = r.Gender;
        return gender;
    }

    /// <summary>No '*' = contains; with '*' = wildcard over the whole name (a leading or trailing '*' makes it open at that end).</summary>
    private static bool Matches(string slug, string pattern)
    {
        if (!pattern.Contains("*")) return slug.Contains(pattern);
        string[] parts = pattern.Split('*');
        int at = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0) continue;
            int found = slug.IndexOf(part, at, StringComparison.Ordinal);
            if (found < 0) return false;
            if (i == 0 && found != 0) return false;                       // no leading '*': must start with it
            at = found + part.Length;
            if (i == parts.Length - 1 && at != slug.Length) return false; // no trailing '*': must end with it
        }
        return true;
    }

    private static void EnsureUserFile()
    {
        try
        {
            if (File.Exists(UserFile)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(UserFile));
            File.WriteAllText(UserFile,
@"# Rats Radio: the voice gender of a whole squadron. Read at every mission start. Workshop updates never touch this file.
#
#   squadron name = male | female
#
# Every pilot of a matching squadron then speaks with a voice of that gender, whatever the 'Female voices' setting says.
# Without a '*' the name matches every squadron whose name contains it (case and punctuation ignored);
# a '*' is a wildcard (VF-11* = every squadron name that starts with VF-11).
# Squadron names are listed in BepInEx/LogOutput.log at mission start ('[hangar <carrier> / <squadron> #1]').
# If several lines match a squadron, the last one wins. Applies from the next mission.
#
# Examples:
# Bear Aces = female
# Ghostriders = male
");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[squadrons] could not create {UserFile}: {ex.Message}");
        }
    }
}
