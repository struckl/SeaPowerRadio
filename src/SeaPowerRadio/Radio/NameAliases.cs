using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using SeaPowerRadio.Voices;

namespace SeaPowerRadio.Radio;

/// <summary>One line of an alias file: what a ship called <c>Match</c> says instead, and the voice it is pinned to.</summary>
internal sealed class NameAlias
{
    public string Match;      // slug of the game name ("uss_pargo")
    public bool StartsWith;   // the line ended in '*': every name that starts with it
    public string Say;        // a name with clips (names.txt), null = keep the unit's own
    public string Voice;      // "gb_m_04" or "nato-en/gb_m_04", null = chosen as usual
    public string Source;     // file:line, for the log
}

/// <summary>
/// Player-editable names for ships and shore units (docs/name-aliases.md). Mod ships often carry a prefix ("USS Pargo")
/// or a name of their own that has no rendered clip, so they fall back to a generated callsign. An alias file maps such a
/// name onto one that has clips ("USS Pargo = Pargo"), can pin the unit to one voice ("= Bristol | gb_m_06"), and a
/// <c>*prefix</c> line strips hull prefixes for every name. Read at every mission start, so an edit applies from the next
/// mission: the base mod's <c>rats-radio-aliases.txt</c>, the same file in any other mod folder (a ship mod can ship its own),
/// then the player's <c>BepInEx/config/SeaPowerRadio/aliases.txt</c>. A later file wins over an earlier one.
/// </summary>
internal static class NameAliases
{
    public const string ModFileName = "rats-radio-aliases.txt";

    private static readonly Dictionary<string, NameAlias> exact = new Dictionary<string, NameAlias>();
    private static readonly List<NameAlias> starts = new List<NameAlias>();     // longest first
    private static readonly List<string> prefixes = new List<string>();         // lower case, longest first
    private static readonly HashSet<string> warned = new HashSet<string>();

    public static string UserFile => Path.Combine(Paths.ConfigPath, "SeaPowerRadio", "aliases.txt");

    public static void Reload()
    {
        exact.Clear();
        starts.Clear();
        prefixes.Clear();
        var files = new List<string> { Path.Combine(VoiceLibrary.ModDirectory, ModFileName) };
        files.AddRange(VoicePacks.ModFolders().Select(f => Path.Combine(f, ModFileName)));
        files.Add(UserFile);
        EnsureUserFile();

        int read = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string full;
            try { full = Path.GetFullPath(file); } catch (Exception) { continue; }
            if (!seen.Add(full) || !File.Exists(full)) continue;
            try
            {
                Read(full);
                read++;
            }
            catch (Exception ex)
            {
                ModLog.Warn($"[aliases] {full} could not be read: {ex.Message}");
            }
        }
        starts.Sort((a, b) => b.Match.Length.CompareTo(a.Match.Length));
        prefixes.Sort((a, b) => b.Length.CompareTo(a.Length));
        ModLog.Info($"[aliases] {exact.Count + starts.Count} aliases, {prefixes.Count} prefixes from {read} file(s)");
    }

    /// <summary>The alias for a game name: an exact line, else the longest "starts with" line; tried again without a prefix.</summary>
    public static NameAlias For(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName) || (exact.Count == 0 && starts.Count == 0)) return null;
        return Lookup(gameName) ?? (StripPrefix(gameName) is string bare && bare != gameName ? Lookup(bare) : null);
    }

    /// <summary>"USS Pargo" → "Pargo" when USS is a listed prefix; the name unchanged otherwise.</summary>
    public static string StripPrefix(string gameName)
    {
        if (string.IsNullOrEmpty(gameName)) return gameName;
        string name = gameName.Trim();
        foreach (string p in prefixes)
        {
            if (name.Length > p.Length + 1 && name.StartsWith(p, StringComparison.OrdinalIgnoreCase)
                && (name[p.Length] == ' ' || name[p.Length] == '.' || name[p.Length] == '-'))
                return name.Substring(p.Length + 1).TrimStart(' ', '.', '-');
        }
        return name;
    }

    private static NameAlias Lookup(string name)
    {
        string slug = Names.Slug(name);
        if (exact.TryGetValue(slug, out NameAlias a)) return a;
        return starts.FirstOrDefault(s => slug.StartsWith(s.Match, StringComparison.Ordinal));
    }

    private static void Read(string file)
    {
        string[] lines = File.ReadAllLines(file);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
            string where = $"{Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)}:{i + 1}";

            if (line.StartsWith("*prefix", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string p in line.Substring("*prefix".Length).Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (!prefixes.Contains(p.ToLowerInvariant()))
                        prefixes.Add(p.ToLowerInvariant());
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                ModLog.Warn($"[aliases] {where}: no '=' in \"{line}\", skipped");
                continue;
            }
            string left = line.Substring(0, eq).Trim();
            string right = line.Substring(eq + 1);
            int bar = right.IndexOf('|');
            string say = (bar >= 0 ? right.Substring(0, bar) : right).Trim();
            string voice = bar >= 0 ? right.Substring(bar + 1).Trim() : "";

            bool startsWith = left.EndsWith("*");
            if (startsWith) left = left.TrimEnd('*').Trim();
            if (Names.Slug(left) == "x" || (say.Length == 0 && voice.Length == 0))   // "x": nothing but punctuation
            {
                ModLog.Warn($"[aliases] {where}: \"{line}\" needs a game name and a name or voice, skipped");
                continue;
            }

            var alias = new NameAlias
            {
                Match = Names.Slug(left),
                StartsWith = startsWith,
                Say = say.Length > 0 ? say : null,
                Voice = voice.Length > 0 ? voice : null,
                Source = where,
            };
            CheckTargets(alias);
            if (startsWith)
            {
                starts.RemoveAll(s => s.Match == alias.Match);
                starts.Add(alias);
            }
            else
            {
                exact[alias.Match] = alias;
            }
        }
    }

    /// <summary>A typo would fail silently in the mission: say so in the log now.</summary>
    private static void CheckTargets(NameAlias a)
    {
        IReadOnlyList<VoicePack> packs = VoicePacks.Installed;
        if (packs.Count == 0) return;
        if (a.Say != null && !packs.Any(p => p.Names?.Unit(a.Say) != null))
            ModLog.Warn($"[aliases] {a.Source}: no voice pack has the name \"{a.Say}\", this ship keeps a generated callsign");
        if (a.Voice != null && FindVoice(a.Voice, null) == null)
            ModLog.Warn($"[aliases] {a.Source}: no voice \"{a.Voice}\" installed, the ship gets a voice as usual");
    }

    /// <summary>
    /// The persona an alias pins, inside <paramref name="pack"/> (null = any installed pack). A voice of another pack than the
    /// unit's own is not used: the pack follows the unit's country.
    /// </summary>
    public static VoicePersona FindVoice(string voice, VoicePack pack)
    {
        if (string.IsNullOrEmpty(voice)) return null;
        int slash = voice.IndexOf('/');
        string packId = slash >= 0 ? voice.Substring(0, slash) : null;
        string persona = slash >= 0 ? voice.Substring(slash + 1) : voice;
        foreach (VoicePack p in pack != null ? new[] { pack } : VoicePacks.Installed)
        {
            if (packId != null && !string.Equals(p.Id, packId, StringComparison.OrdinalIgnoreCase)) continue;
            VoicePersona found = p.Personas.FirstOrDefault(v => string.Equals(v.Id, persona, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Logs once per alias that its pinned voice is not in the pack this unit speaks with.</summary>
    public static void WarnVoiceNotInPack(NameAlias a, VoicePack pack, string unitName)
    {
        if (warned.Add(a.Source + "|" + pack.Id))
            ModLog.Warn($"[aliases] {a.Source}: {unitName} speaks with the '{pack.Id}' pack, which has no voice \"{a.Voice}\"; voice chosen as usual");
    }

    private static void EnsureUserFile()
    {
        try
        {
            if (File.Exists(UserFile)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(UserFile));
            File.WriteAllText(UserFile,
@"# Rats Radio: your own ship names. Read at every mission start. Workshop updates never touch this file.
#
#   game name = name to say | voice
#
# 'name to say' must be a name the mod has clips for (any ship or shore unit of the base game,
# see voices/names.txt). 'voice' is optional and pins the ship to one voice, e.g. gb_m_04
# (folder names in voices/). Leave the name out to only pin the voice: 'HMS Bristol = | gb_m_06'.
# A '*' at the end of the game name matches every name that starts with it.
# '*prefix' lines strip hull prefixes before the lookup (the mod ships a list; add your own).
#
# Examples:
# USS Pargo = Pargo
# HMS Bristol = Bristol | gb_m_06
# CVN-68* = Nimitz
# *prefix HNoMS, HSwMS
");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[aliases] could not create {UserFile}: {ex.Message}");
        }
    }
}
