using System;
using SeaPower;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Roster;

/// <summary>Owns the current mission's <see cref="Roster"/>: builds it on mission start, drops it on unload.</summary>
internal static class RosterService
{
    public static Roster Current { get; private set; }

    public static void Init()
    {
        Rig.Mission.Started += OnMissionStarted;
        Rig.Mission.Ended += () => Current = null;
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
            Rig.Log.Warn($"[aliases] not loaded, ships use their own names: {ex.Message}");
        }
        try
        {
            Voices.SquadronVoices.Reload(); // an edited squadrons.txt applies from the next mission
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"[squadrons] not loaded, squadrons use the general voice settings: {ex.Message}");
        }
        CampaignMemory campaign = CampaignMemory.ForMission(seed);
        string salt = RandomVoiceSalt(seed, campaign);
        var roster = new Roster(seed, campaign, salt);
        roster.Build();
        Current = roster;
        if (Settings.LogRoster.Value)
            Rig.Log.Info(roster.Describe());
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
        Rig.Log.Info(salt != null
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
            Rig.Log.Warn($"[voices] dynamic campaign check failed: {ex.Message}");
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
            Rig.Log.Info($"[roster] launch {launch.Aircraft.getUIDAndName()} -> {element.Callsign} ({element.Voice?.FullId ?? "-"})");
    }

    /// <summary>
    /// Stable per-mission seed: the mission file relative to its mod folder. For a save it is the original
    /// mission the save chain started from, so loading a save keeps the voices of the mission it came from.
    /// </summary>
    private static string MissionSeed()
    {
        // Each step may be empty on some missions or saves (a null reference here was logged in 43 player reports): every
        // step is checked on its own, and the log says which one gave nothing.
        string raw = Globals.currentMissionFilePath;
        string step = "save";
        try
        {
            if (SaveLoadManager.IsASaveFile && Singleton<SceneCreator>.InstanceExists())
            {
                step = "save chain";
                var ini = Singleton<SceneCreator>.Instance?.MissionIni;
                string baseFile = ini?.readValue("File", "BaseFile", "", false);
                if (!string.IsNullOrEmpty(baseFile))
                    return Normal(baseFile);
            }
            if (string.IsNullOrEmpty(raw))
            {
                Rig.Log.Info("[roster] mission seed: no mission file path, voices use a fixed seed");
                return "unknown";
            }
            step = "relative path";
            string relative = Singleton<FileManager>.InstanceExists() ? Singleton<FileManager>.Instance?.GetRelativePath(raw) : null;
            if (string.IsNullOrEmpty(relative))
            {
                Rig.Log.Info("[roster] mission seed: the mission lies outside a mod folder, the full path is used");
                return Normal(raw);
            }
            return Normal(relative);
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"Mission seed fallback at the {step} step: {ex.GetType().Name}: {ex.Message}");
            return Normal(raw ?? "unknown");
        }
    }

    private static string Normal(string path) => path.Replace('\\', '/').ToLowerInvariant();
}
