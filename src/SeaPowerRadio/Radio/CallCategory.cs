using System;
using System.Collections.Generic;

namespace SeaPowerRadio.Radio;

/// <summary>The groups the call volumes of the options page are set for.</summary>
internal enum CallCategory
{
    Contacts,    // new / lost / identified contacts and tracks
    Combat,      // weapons away, splash, hit, kill
    Warnings,    // missile or torpedo inbound, we are hit, flooding, bingo, emergencies
    Orders,      // acknowledgements, status replies, "unable" and the reasons why
    Other,       // air ops, fuel, search and rescue, mission messages
}

internal static class CallCategories
{
    private static readonly HashSet<string> Contacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NewContactAir", "NewContactLand", "NewContactSubsurface", "NewContactSurface", "NewContactsMultiple",
        "ContactFaded", "ContactIntermittent", "ContactRegained", "ContactsFaded",
        "IdByDatalink", "IdByEsm", "IdByFiredUpon", "IdByIntel", "IdByRadar", "IdBySonar", "IdByVisual",
        "TrackFriendly", "TrackHostile", "TrackNeutral",
        "BanditBandit", "BuoyContact", "MadContact", "EsmContact", "RadarEmission", "MerchantTraffic",
    };

    private static readonly HashSet<string> Combat = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AAM_ARS_Fired", "AAM_IR_Fired", "AAM_SARH_Fired", "AshmAway", "AsumAway", "BombsAway", "BuoyAway",
        "ChaffOut", "DecoyOut", "GunsGuns", "Magnum", "SalvoFire", "SamAway", "SamReady", "Shack",
        "SplashOne", "SplashTrack", "HitTrack", "TargetDestroyed", "TorpedoAwayAir", "WeaponAway", "Winchester",
        "LaunchTransient",
    };

    // Urgent calls that are about the mission, not about danger to the unit, count as "other".
    private static readonly HashSet<string> MissionAlerts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "MissionComplete", "MissionFailed", "NewIntel", "NewTasking", "ObjectiveComplete", "ObjectiveFailed",
    };

    private static readonly HashSet<string> OtherWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BeingJammed", "DeckDamaged",
    };

    public static CallCategory Of(string phrase, CallRole role)
    {
        if (string.IsNullOrEmpty(phrase))
            return CallCategory.Other;
        if (Contacts.Contains(phrase))
            return CallCategory.Contacts;
        if (Combat.Contains(phrase))
            return CallCategory.Combat;
        if (OtherWarnings.Contains(phrase) || (role == CallRole.Alert && !MissionAlerts.Contains(phrase)))
            return CallCategory.Warnings;
        if (role == CallRole.Reply || phrase.StartsWith("WhyNot_", StringComparison.Ordinal))
            return CallCategory.Orders;
        return CallCategory.Other;
    }

    public static float Volume(CallCategory category)
    {
        switch (category)
        {
            case CallCategory.Contacts: return Settings.VolumeContacts.Value;
            case CallCategory.Combat: return Settings.VolumeCombat.Value;
            case CallCategory.Warnings: return Settings.VolumeWarnings.Value;
            case CallCategory.Orders: return Settings.VolumeOrders.Value;
            default: return Settings.VolumeOther.Value;
        }
    }
}
