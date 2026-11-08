using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Voices;

/// <summary>
/// One voice pack: a folder with a <c>voicepack.ini</c>, its personas, phrase catalog, in-game names and one folder
/// of clips per persona. The NATO (English) pack is the base mod's own <c>voices/</c> folder and goes through the
/// same code as every pack found in another mod folder (docs/voice-packs.md).
/// </summary>
internal sealed class VoicePack
{
    public string Id;
    public string Name;
    public string Language;                 // picks the number / callsign rules, see LanguageRules
    public string[] Nations;                // countries that use this pack under "Auto"
    public Version Version;
    public Version MinBaseVersion;
    public string Directory;                // the folder that holds voicepack.ini

    public readonly List<VoicePersona> Personas = new List<VoicePersona>();
    public Catalog Catalog;
    public Names Names;

    /// <summary>
    /// <c>accents.txt</c> (optional): per nation the accents its units may use, best first. A nation that is not listed
    /// uses the whole pack. Data only, so a later pack can declare its own accents and nations.
    /// </summary>
    private readonly Dictionary<string, string[]> nationAccents = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The accents a nation's units use in this pack, best first; null = no restriction (any voice of the pack).</summary>
    public string[] AccentsFor(string nation)
    {
        if (string.IsNullOrEmpty(nation))
            return null;
        return nationAccents.TryGetValue(nation, out string[] accents) ? accents : null;
    }

    public bool ListsNation(string nation)
    {
        if (string.IsNullOrEmpty(nation))
            return false;
        foreach (string n in Nations)
            if (string.Equals(n, nation, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public string PathOf(string persona, string key) => System.IO.Path.Combine(Directory, persona, key + ".mp3");

    /// <summary>Reads <c>voicepack.ini</c> in <paramref name="directory"/>; null (with a log line) if it is unusable.</summary>
    public static VoicePack ReadIni(string directory, Version baseVersion)
    {
        string file = System.IO.Path.Combine(directory, "voicepack.ini");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inPack = false;
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw;
            int comment = line.IndexOf(';');
            if (comment >= 0) line = line.Substring(0, comment);
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inPack = string.Equals(line.Substring(1, line.Length - 2).Trim(), "Pack", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            int eq = line.IndexOf('=');
            if (inPack && eq > 0)
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }

        values.TryGetValue("Id", out string id);
        if (string.IsNullOrWhiteSpace(id))
        {
            ModLog.Warn($"Voice pack {directory}: voicepack.ini has no Id, skipped.");
            return null;
        }
        if (id.Equals(VoicePacks.Auto, StringComparison.OrdinalIgnoreCase) || id.Equals(VoicePacks.GameVoice, StringComparison.OrdinalIgnoreCase))
        {
            ModLog.Warn($"Voice pack {directory}: the Id '{id}' is reserved, skipped.");
            return null;
        }
        Version min = ParseVersion(Get(values, "MinBaseVersion"), new Version(0, 0));
        if (min > baseVersion)
        {
            ModLog.Warn($"Voice pack '{id}' ({directory}) needs Rats Radio {min} or newer (this is {baseVersion}), skipped.");
            return null;
        }
        return new VoicePack
        {
            Id = id.Trim(),
            Name = Get(values, "Name") is string n && n.Length > 0 ? n : id,
            Language = (Get(values, "Language") ?? "en").ToLowerInvariant(),
            Nations = (Get(values, "Nations") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray(),
            Version = ParseVersion(Get(values, "Version"), new Version(1, 0, 0)),
            MinBaseVersion = min,
            Directory = directory,
        };
    }

    private static string Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string v) ? v : null;

    public static Version ParseVersion(string text, Version fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return Version.TryParse(text.Trim(), out Version v) ? v : fallback;
    }

    /// <summary>Loads personas, catalog and names. Returns false when the pack has nothing to speak with.</summary>
    public bool LoadContent(Catalog baseCatalog)
    {
        string personas = System.IO.Path.Combine(Directory, "personas.txt");
        if (!File.Exists(personas))
        {
            ModLog.Warn($"Voice pack '{Id}': no personas.txt in {Directory}, skipped.");
            return false;
        }
        foreach (string raw in File.ReadAllLines(personas))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] f = line.Split('|');
            if (f.Length < 4)
            {
                ModLog.Warn($"{Id}/personas.txt: bad line '{line}'");
                continue;
            }
            Personas.Add(new VoicePersona { Pack = this, Id = f[0].Trim(), Language = f[1].Trim(), Gender = f[2].Trim(), Accent = f[3].Trim() });
        }
        LoadAccents();
        Catalog = new Catalog(Directory, baseCatalog);
        Names = new Names(Directory);
        ModLog.Info($"Voice pack '{Id}' ({Name}) {Version}: {Personas.Count} personas, {Catalog.PhraseCount} phrases, {Names.Count} names, nations: {string.Join(",", Nations)}");
        return Catalog.Loaded && Personas.Count > 0;
    }

    private void LoadAccents()
    {
        string file = System.IO.Path.Combine(Directory, "accents.txt");
        if (!File.Exists(file))
            return;
        var known = new HashSet<string>(Personas.Select(p => p.Accent), StringComparer.OrdinalIgnoreCase);
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] f = line.Split('|');
            string[] accents = f.Length >= 2
                ? f[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).Where(a => known.Contains(a)).ToArray()
                : new string[0];
            if (f[0].Trim().Length == 0 || accents.Length == 0)
            {
                ModLog.Warn($"{Id}/accents.txt: no usable accent in '{line}', the nation uses the whole pack.");
                continue;
            }
            nationAccents[f[0].Trim()] = accents;
        }
    }
}
