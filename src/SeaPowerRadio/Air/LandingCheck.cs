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
    private static ObjectBase MissionHome(ObjectBase unit)
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
        var a = new Airframe(unit);
        var parts = new List<string>
        {
            site.CanAcceptAircraft(unit, message: false, checkDistance: false) ? "game: TAKES it" : "game: refuses",
            TypeListed(deck, a.Ini),
            Runway(deck, a),
            deck._elevators.Any(e => e.CheckSupport(a.Ini, a.Roles, a.Large, a.VeryLarge))
                ? "hangar: fits"
                : $"hangar: too small for a {a.Size} aircraft",
        };

        int taken = deck.TotalVehiclesOnBoardIncludingCooldown() + deck.TotalVehiclesInAir() + deck.TotalVehiclesNotInFlight();
        parts.Add($"room: {taken} of {deck.MaxAircraftOnBoard} taken{(taken >= deck.MaxAircraftOnBoard ? " - FULL" : "")}");

        if (deck.Inoperable.Value)
            parts.Add("deck: OUT OF ACTION");
        if (a.Type == VehicleType.Helicopter && !deck.CanRecoverHelicopterInCurrentSeaState(unit))
            parts.Add("sea state: too high");
        return string.Join(" | ", parts);
    }

    /// <summary>What a landing spot or hangar elevator checks about the aircraft.</summary>
    private readonly struct Airframe
    {
        public readonly string Ini;
        public readonly List<ObjectBaseParameters.UnitRoles> Roles;
        public readonly bool Large, VeryLarge;
        public readonly VehicleType Type;

        public Airframe(ObjectBase unit)
        {
            Ini = unit._obp._objectIniName;
            Roles = unit._obp._unitRoles;
            Aircraft aircraft = unit as Aircraft;
            Large = aircraft != null && aircraft.Ap._isLarge;
            VeryLarge = aircraft != null && aircraft.Ap._isVeryLarge;
            Type = aircraft == null ? VehicleType.Helicopter
                : aircraft.Ap._isVTOL ? VehicleType.VTOL
                : VehicleType.Plane;
        }

        public string Size => VeryLarge ? "very large" : Large ? "large" : "small";
    }

    private static string TypeListed(FlightDeck deck, string ini)
    {
        if (deck._vehiclesOnBoard.Any(v => v._fileName == ini))
            return "type: in its aircraft list";
        string aboard = string.Join(", ", deck._vehiclesOnBoard.Where(v => v.Numbers > 0).Select(v => v._fileName));
        return $"type: NOT in its aircraft list ({deck._vehiclesOnBoard.Count} types; aboard: {(aboard.Length > 0 ? aboard : "none")})";
    }

    private static string Runway(FlightDeck deck, Airframe a)
    {
        int spots = deck._recoveryPoints.Count(p => p.IsAllowed(a.Type));
        int fitting = deck._recoveryPoints.Count(p => p.IsAllowed(a.Type) && p.CheckSupport(a.Ini, a.Roles, a.Large, a.VeryLarge));
        return spots == 0 ? $"runway: no landing spot for {a.Type}"
            : fitting == 0 ? $"runway: too small for a {a.Size} aircraft"
            : $"runway: fits ({fitting} of {deck._recoveryPoints.Count} landing spots)";
    }
}
