using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using SeaPower;

namespace SeaPowerRadio.Roster;

/// <summary>
/// In a linear campaign every unit keeps its voice and callsign from mission to mission. The roster of one mission
/// is shuffled per mission file and the pool order shifts whenever the unit set changes, so a deterministic seed
/// cannot hold that; instead each campaign has a small memory file
/// <c>BepInEx/config/SeaPowerRadio/campaigns/&lt;campaign id&gt;.txt</c> with one line per unit:
/// <c>key|packId/personaId|callsign</c>. The key is the unit's <c>_campaignTag</c> (the game's stable per-unit id across
/// the missions of a linear campaign), else the unit's name for ships and shore units. The campaign id is the
/// folder of the campaign's missions (taken from the mission file, never from a save), so a new playthrough of the same
/// campaign gets the same voices. Outside a linear campaign there is no memory and nothing changes.
/// </summary>
internal sealed class CampaignMemory
{
    private static readonly Regex NotId = new Regex("[^a-z0-9._-]+", RegexOptions.Compiled);

    private readonly string file;
    private readonly Dictionary<string, (string voice, string callsign)> entries =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal);
    private bool dirty;

    public string Id { get; }

    private CampaignMemory(string id, string file)
    {
        Id = id;
        this.file = file;
        Load();
    }

    /// <summary>The memory of the campaign this mission belongs to, or null (single mission, other kind of campaign).</summary>
    public static CampaignMemory ForMission(string missionSeed)
    {
        try
        {
            if (!Singleton<SceneCreator>.InstanceExists() || !Singleton<SceneCreator>.Instance._isLinearCampaign)
                return null;
            string seed = (missionSeed ?? "").Replace('\\', '/');
            int cut = seed.LastIndexOf('/');
            string id = NotId.Replace((cut > 0 ? seed.Substring(0, cut) : seed).ToLowerInvariant(), "_").Trim('_');
            if (id.Length == 0)
                id = "campaign";
            string path = Path.Combine(Paths.ConfigPath, "SeaPowerRadio", "campaigns", id + ".txt");
            return new CampaignMemory(id, path);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[campaign] no voice memory: {ex.Message}");
            return null;
        }
    }

    public int Count => entries.Count;

    public bool TryGet(string key, out string voice, out string callsign)
    {
        if (entries.TryGetValue(key, out var e))
        {
            voice = e.voice;
            callsign = e.callsign;
            return true;
        }
        voice = callsign = null;
        return false;
    }

    public void Set(string key, string voice, string callsign)
    {
        callsign ??= "";
        if (entries.TryGetValue(key, out var old) && old.voice == voice && old.callsign == callsign)
            return;
        entries[key] = (voice, callsign);
        dirty = true;
    }

    public bool IsRemembered(string voice) => entries.Values.Any(e => e.voice == voice);

    public IEnumerable<string> Callsigns() => entries.Values.Select(e => e.callsign).Where(c => c.Length > 0);

    public void SaveIfDirty()
    {
        if (!dirty)
            return;
        dirty = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllLines(file, new[] { "# Sea Power Rats Radio: voices of one campaign, key|voice|callsign. Delete a line to give that unit a new voice." }
                .Concat(entries.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}|{p.Value.voice}|{p.Value.callsign}")));
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[campaign] could not save {file}: {ex.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(file))
                return;
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string[] f = line.Split('|');
                if (f.Length >= 2 && f[0].Length > 0 && f[1].Contains("/"))
                    entries[f[0]] = (f[1], f.Length > 2 ? f[2] : "");
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[campaign] could not read {file}: {ex.Message}");
        }
    }
}
