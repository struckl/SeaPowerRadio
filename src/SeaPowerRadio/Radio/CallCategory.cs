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
    // Phrase groups of tools/voicegen/rules.py, checked against the phrase catalog by check_sync.py.
    private static readonly HashSet<string> Contacts = PhraseSet(ClipRules.Contacts);
    private static readonly HashSet<string> Combat = PhraseSet(ClipRules.Combat);
    private static readonly HashSet<string> MissionAlerts = PhraseSet(ClipRules.MissionAlerts);
    private static readonly HashSet<string> OtherWarnings = PhraseSet(ClipRules.OtherWarnings);

    public static HashSet<string> PhraseSet(string[] phrases) => new HashSet<string>(phrases, StringComparer.OrdinalIgnoreCase);

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
