using System;
using SeaPower;
using SeaPowerRadio.Core;
using SeaPowerRadio.Voices;

namespace SeaPowerRadio.Roster;

/// <summary>Owns the current mission's <see cref="Roster"/>: builds it on mission start, drops it on unload.</summary>
internal static class RosterService
{
    public static Roster Current { get; private set; }

    public static void Init()
    {
        EventBus.MissionStarted += OnMissionStarted;
        EventBus.MissionEnded += () => Current = null;
        EventBus.AircraftLaunched += OnAircraftLaunched;
    }

    private static void OnMissionStarted()
    {
        string seed = MissionSeed();
        try
        {
            Radio.NameAliases.Reload();     // an edited aliases.txt applies from the next mission
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[aliases] not loaded, ships use their own names: {ex.Message}");
        }
        CampaignMemory campaign = CampaignMemory.ForMission(seed);
        string salt = RandomVoiceSalt(seed, campaign);
        var roster = new Roster(seed, campaign, salt);
        roster.Build();
        Current = roster;
        if (Settings.LogRoster.Value)
            ModLog.Info(roster.Describe());
    }

    /// <summary>
    /// The salt that makes the voices of this mission start random (Settings.RandomVoices), or null for the plain per-mission
    /// seed: Random voices off, a linear campaign (the voice memory decides there), a dynamic campaign (not randomised).
    /// </summary>
    private static string RandomVoiceSalt(string seed, CampaignMemory campaign)
    {
        if (Settings.RandomVoices == null || !Settings.RandomVoices.Value || campaign != null || IsDynamicCampaign(seed))
            return null;
        bool fresh = !SaveLoadManager.IsASaveFile;
        string salt = VoiceSalts.ForStart(seed, fresh);
        ModLog.Info(salt != null
            ? $"[voices] random voices: salt {salt} for '{seed}' ({(fresh ? "new start" : "save, salt of its latest start")})"
            : $"[voices] random voices: no salt on record for the save of '{seed}', plain seed");
        return salt;
    }

    /// <summary>
    /// A tactical mission fought from the dynamic campaign (<c>CampaignMain</c>). The campaign scene stays loaded under the
    /// mission, and the game writes the mission to <c>user/missions/Campaign Mission.ini</c> (<c>MissionEditorModel.SetupMission</c>,
    /// same in 0.8.7 and the globe build); either one marks it.
    /// </summary>
    private static bool IsDynamicCampaign(string seed)
    {
        try
        {
            if (Singleton<CampaignMain>.InstanceExists())
                return true;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[voices] dynamic campaign check failed: {ex.Message}");
        }
        return (seed ?? "").EndsWith("campaign mission.ini", StringComparison.OrdinalIgnoreCase);
    }

    private static void OnAircraftLaunched(AircraftLaunch launch)
    {
        Roster roster = Current;
        if (roster == null || launch.Deck?._baseObject == null) return;
        if (!launch.Deck._baseObject.IsPlayerObject) return;

        VehicleTypeOnBoard type = launch.Deck._vehiclesOnBoard[launch.VehicleIndex];
        string squadron = type.Squadrons[launch.SquadronIndex]._name;
        RosterElement element = roster.BindLaunch(launch.Deck._baseObject, type._fileName, squadron, launch.Aircraft);
        if (Settings.LogRoster.Value && element != null)
            ModLog.Info($"[roster] launch {launch.Aircraft.getUIDAndName()} -> {element.Callsign} ({element.Voice?.FullId ?? "-"})");
    }

    /// <summary>
    /// Stable per-mission seed: the mission file relative to its mod folder. For a save it is the original
    /// mission the save chain started from, so loading a save keeps the voices of the mission it came from.
    /// </summary>
    private static string MissionSeed()
    {
        try
        {
            if (SaveLoadManager.IsASaveFile && Singleton<SceneCreator>.InstanceExists())
            {
                string baseFile = Singleton<SceneCreator>.Instance.MissionIni?.readValue("File", "BaseFile", "", false);
                if (!string.IsNullOrEmpty(baseFile))
                    return baseFile.Replace('\\', '/').ToLowerInvariant();
            }
            string path = Globals.currentMissionFilePath ?? "unknown";
            if (Singleton<FileManager>.InstanceExists())
                path = Singleton<FileManager>.Instance.GetRelativePath(path);
            return path.Replace('\\', '/').ToLowerInvariant();
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Mission seed fallback: {ex.Message}");
            return Globals.currentMissionFilePath ?? "unknown";
        }
    }
}
