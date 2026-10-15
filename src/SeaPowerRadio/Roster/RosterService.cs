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
        var roster = new Roster(seed, VoiceLibrary.Personas);
        roster.Build();
        Current = roster;
        if (Settings.LogRoster.Value)
            ModLog.Info(roster.Describe());
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
            ModLog.Info($"[roster] launch {launch.Aircraft.getUIDAndName()} -> {element.Callsign} ({element.Voice?.Id ?? "-"})");
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
