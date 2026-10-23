using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;

namespace SeaPowerRadio.Formation;

/// <summary>
/// What a unit is, in a word or two and in the game's own language: ships and submarines their type from the
/// names file ("Destroyer", "Attack Submarine", "Aircraft Carrier"), aircraft the mission their loadout is for
/// ("CAP", "Strike", "SEAD", "AEW"), helicopters their type ("ASW Helicopter"). A group: the role all its members
/// share, nothing when they differ.
/// </summary>
internal static class UnitRole
{
    // Loadout families (keys of loadout_names.ini) and the text that names them. Variants are matched by prefix and
    // share the family's name: StrikeLongRange / StrikePrecision → Strike, ASWHunter → ASW, SEADAdvanced → SEAD.
    // First match wins, so AirToAirIntercept sits before AirToAir.
    private static readonly (string prefix, string section, string key)[] Missions =
    {
        ("AirToAirIntercept", "StationRole", "Intercept"),
        ("AirToAir", "StationRole", "CAP"),
        ("AEW", "StationRole", "AEW"),
        ("SEAD", "LoadoutNames", "SEAD"),
        ("Strike", "LoadoutNames", "Strike"),
        ("CAS", "LoadoutNames", "Strike"),
        ("AntiArmor", "LoadoutNames", "Strike"),
        ("LandAttack", "LoadoutNames", "Strike"),
        ("AntiShip", "LoadoutNames", "AntiShip"),
        ("ASW", "LoadoutNames", "ASW"),
        ("Recon", "LoadoutNames", "Recon"),
        ("EW", "LoadoutNames", "EW"),
        ("Tanker", "LoadoutNames", "Tanker"),
        ("Transport", "LoadoutNames", "Transport"),
    };

    /// <summary>A formation manager row: a group, a unit, or a group member (station).</summary>
    public static string ForRow(object row)
    {
        switch (row)
        {
            case UnitFormation formation:
                return ForGroup(formation);
            case Station station:
                return Of(station.UnitObject);
            case ObjectBase unit:
                return Of(unit);
            default:
                return "";
        }
    }

    public static string Of(ObjectBase unit)
    {
        ObjectBaseParameters p = unit == null ? null : unit._obp;
        if (p == null)
            return "";
        if (unit is Aircraft)
            return Mission(p._loadoutVariant) ?? TypeName(p);
        return TypeName(p);   // ships, submarines, helicopters, ground units
    }

    // Same members as the game's own summary line under the group name (UnitFormation.InFormationSummary).
    private static string ForGroup(UnitFormation formation)
    {
        List<string> roles = formation.Stations?
            .Where(s => s.UnitObject != null && !s.UnitObject.Disabled)
            .Select(s => Of(s.UnitObject))
            .Distinct()
            .ToList();
        return roles != null && roles.Count == 1 ? roles[0] : "";
    }

    /// <summary>Null for loadouts that don't say what the aircraft is for: Default, Ferry, Empty, Gun, cargo.</summary>
    private static string Mission(string loadout)
    {
        if (string.IsNullOrEmpty(loadout))
            return null;
        foreach (var (prefix, section, key) in Missions)
        {
            if (loadout.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Text(section, key);
        }
        return null;
    }

    // _typeFullName comes from the "Type=" line of vessel_names / aircraft_names in the game's language
    // ("DD,Destroyer" → Destroyer, "ASW Helicopter" → ASW Helicopter).
    private static string TypeName(ObjectBaseParameters p)
    {
        string name = Usable(p._typeFullName) ? p._typeFullName : Usable(p._typeAbbr) ? p._typeAbbr : "";
        return name.Trim();
    }

    private static bool Usable(string s) => !string.IsNullOrWhiteSpace(s) && s != "MISSING";

    private static string Text(string section, string key)
    {
        if (!Singleton<LanguageResourceHandler>.InstanceExists())
            return key;
        return Singleton<LanguageResourceHandler>.Instance.getText(section, key, logError: false, verboseError: false);
    }
}
