using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SeaPower;

namespace SeaPowerRadio.Air;

/// <summary>
/// Why no deck or field takes an aircraft. The game's own check (ObjectBase.CanAcceptAircraft: aircraft list,
/// deck in action, sea state, room) plus what landing needs on top of it (HoldingPattern.TryGetRecoveryPoint: a
/// landing spot and a hangar elevator for its size). Every check is reported, not only the first that fails, so
/// the log shows whether adding the type to a field's aircraft list would be enough.
/// </summary>
internal static class LandingCheck
{
    // SceneCreator keeps the mission's HomeBase= for every aircraft; RebaseAircraft only applies it when the field
    // takes the aircraft.
    private static readonly FieldInfo UnitsToRebase = AccessTools.Field(typeof(SceneCreator), "_unitsToRebase");

    /// <summary>The field the mission named as the aircraft's home base, whether or not the game kept it.</summary>
    public static ObjectBase MissionHome(ObjectBase unit)
    {
        if (!Singleton<SceneCreator>.InstanceExists())
            return null;
        SceneCreator scene = Singleton<SceneCreator>.Instance;
        if (!(UnitsToRebase?.GetValue(scene) is Dictionary<ObjectBase, string> homes) || !homes.TryGetValue(unit, out string key))
            return null;
        return scene.ObjectsByPvKey.TryGetValue(key, out ObjectBase home) ? home : null;
    }

    /// <summary>Log why each friendly deck or field won't be this aircraft's home. Fixed-wing: helicopter decks left out.</summary>
    public static void Log(ObjectBase unit)
    {
        string name = unit.getName();
        ObjectBase missionHome = MissionHome(unit);
        bool helicopter = unit is Helicopter;
        int helicopterDecks = 0;
        var lines = new List<string>();
        foreach (ObjectBase site in unit._taskforce._taskforceUnits.ToList())
        {
            FlightDeck deck = site == null ? null : site._obp?._flightDeck;
            if (deck == null || site == unit || site.IsDestroyed)
                continue;
            if (!helicopter && !deck._recoveryPoints.Any(p => p.IsAllowed(VehicleType.Plane) || p.IsAllowed(VehicleType.VTOL)
                                                              || p.IsAllowed(VehicleType.STOL)))
            {
                helicopterDecks++;
                continue;
            }
            string mark = site == missionHome ? " (the mission's HomeBase)" : "";
            lines.Add($"[fuel]   {site.getName()}{mark}: {Why(site, deck, unit)}");
        }

        string intended = missionHome != null ? $"the mission names {missionHome.getName()}" : "the mission names none";
        string skipped = helicopterDecks > 0 ? $", {helicopterDecks} helicopter-only decks left out" : "";
        ModLog.Info($"[fuel] {name} ({unit._obp._objectIniName}) has no home base, {intended}. Why each field won't take it{skipped}:");
        if (lines.Count == 0)
            ModLog.Info("[fuel]   no friendly deck or field at all");
        foreach (string line in lines)
            ModLog.Info(line);
    }

    private static string Why(ObjectBase site, FlightDeck deck, ObjectBase unit)
    {
        string ini = unit._obp._objectIniName;
        List<ObjectBaseParameters.UnitRoles> roles = unit._obp._unitRoles;
        Aircraft aircraft = unit as Aircraft;
        bool large = aircraft != null && aircraft.Ap._isLarge;
        bool veryLarge = aircraft != null && aircraft.Ap._isVeryLarge;
        VehicleType type = aircraft == null ? VehicleType.Helicopter
            : aircraft.Ap._isVTOL ? VehicleType.VTOL
            : VehicleType.Plane;
        string size = veryLarge ? "very large" : large ? "large" : "small";

        var parts = new List<string>();
        parts.Add(site.CanAcceptAircraft(unit, message: false, checkDistance: false) ? "game: TAKES it" : "game: refuses");

        if (deck._vehiclesOnBoard.Any(v => v._fileName == ini))
        {
            parts.Add("type: in its aircraft list");
        }
        else
        {
            string aboard = string.Join(", ", deck._vehiclesOnBoard.Where(v => v.Numbers > 0).Select(v => v._fileName));
            parts.Add($"type: NOT in its aircraft list ({deck._vehiclesOnBoard.Count} types; aboard: {(aboard.Length > 0 ? aboard : "none")})");
        }

        int spots = deck._recoveryPoints.Count(p => p.IsAllowed(type));
        int fitting = deck._recoveryPoints.Count(p => p.IsAllowed(type) && p.CheckSupport(ini, roles, large, veryLarge));
        parts.Add(spots == 0 ? $"runway: no landing spot for {type}"
            : fitting == 0 ? $"runway: too small for a {size} aircraft"
            : $"runway: fits ({fitting} of {deck._recoveryPoints.Count} landing spots)");

        parts.Add(deck._elevators.Any(e => e.CheckSupport(ini, roles, large, veryLarge))
            ? "hangar: fits"
            : $"hangar: too small for a {size} aircraft");

        int taken = deck.TotalVehiclesOnBoardIncludingCooldown() + deck.TotalVehiclesInAir() + deck.TotalVehiclesNotInFlight();
        parts.Add($"room: {taken} of {deck.MaxAircraftOnBoard} taken{(taken >= deck.MaxAircraftOnBoard ? " - FULL" : "")}");

        if (deck.Inoperable.Value)
            parts.Add("deck: OUT OF ACTION");
        if (type == VehicleType.Helicopter && !deck.CanRecoverHelicopterInCurrentSeaState(unit))
            parts.Add("sea state: too high");
        return string.Join(" | ", parts);
    }
}
