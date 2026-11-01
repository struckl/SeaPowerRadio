using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPower;

namespace SeaPowerRadio.Voices;

/// <summary>One country of the game, with the group the options page puts it in.</summary>
internal sealed class NationInfo
{
    public string Id;       // the game's nation id: what ObjectBase.getNation() returns ("US", "Soviet", "North_Korea")
    public string Name;     // as the game shows it in the current language
    public string Group;    // NATO, Warsaw Pact or Other
}

/// <summary>
/// Every country the game knows (the <c>[Nations]</c> section of its <c>nations.ini</c>, merged with mods, also
/// outside a mission) and the group each belongs to (<c>voices/nations.txt</c>, <c>nationId|group</c>; unknown ids
/// go to Other, so a game update with new nations needs no mod update).
/// </summary>
internal static class Nations
{
    public const string Other = "Other";
    public static readonly string[] GroupOrder = { "NATO", "Warsaw Pact", Other };

    // Ids of nations.ini that are not countries a player unit can belong to.
    private static readonly HashSet<string> NotCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Undefined", "All", "Civilian", "Pirates", "Terrorists", "Unknown" };

    private static Dictionary<string, string> groups;

    private static Dictionary<string, string> Groups()
    {
        if (groups != null) return groups;
        groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string file = Path.Combine(VoiceLibrary.VoicesDirectory, "nations.txt");
        if (!File.Exists(file))
        {
            ModLog.Warn("nations.txt missing: every country goes in the group 'Other'.");
            return groups;
        }
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] f = line.Split('|');
            if (f.Length >= 2)
                groups[f[0].Trim()] = f[1].Trim();
        }
        return groups;
    }

    public static string GroupOf(string nation) =>
        Groups().TryGetValue(nation ?? "", out string g) && g.Length > 0 ? g : Other;

    /// <summary>Ids from nations.txt: bound in the config at startup, before the game's list can be read.</summary>
    public static IEnumerable<string> Listed() => Groups().Keys;

    /// <summary>
    /// The game's nations in the order of the options page: grouped, by name. Read each time (the page is built
    /// rarely); falls back to nations.txt and the pack lists if the game's table cannot be read.
    /// </summary>
    public static List<NationInfo> All()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string lang = Singleton<LanguageResourceHandler>.InstanceExists() ? Singleton<LanguageResourceHandler>.Instance.Language : "en";
            foreach (string l in new[] { lang, "en" })
            {
                Dictionary<string, string> table = LocalizationLoader.OpenVirtualLocalizationFile("language_" + l, "nations", "nations")?.GetSectionKeyValues("Nations");
                if (table == null) continue;
                foreach (var pair in table)
                    if (!names.ContainsKey(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                        names[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"The game's nation list could not be read: {ex.Message}");
        }
        foreach (string id in Groups().Keys.Concat(VoicePacks.Installed.SelectMany(p => p.Nations)))
            if (!names.ContainsKey(id))
                names[id] = id.Replace('_', ' ');

        return names.Where(p => !NotCountries.Contains(p.Key))
            .Select(p => new NationInfo { Id = p.Key, Name = p.Value, Group = GroupOf(p.Key) })
            .OrderBy(n => { int i = Array.IndexOf(GroupOrder, n.Group); return i >= 0 ? i : GroupOrder.Length; })
            .ThenBy(n => n.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
