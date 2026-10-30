using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SeaPowerRadio;

/// <summary>
/// BepInEx config at <c>BepInEx/config/dev.sewerlabs.seapowerradio.cfg</c>. Sections are numbered so they sort in
/// the order a player would look for them.
/// </summary>
internal static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> VerboseLog;

    public static ConfigEntry<bool> ReplaceStockVoices;
    public static ConfigEntry<float> Volume;
    public static ConfigEntry<float> RadioNoise;
    public static ConfigEntry<float> ReportVolume;
    public static ConfigEntry<bool> LinkAlerts;
    public static ConfigEntry<bool> LinkMentions;
    public static ConfigEntry<bool> CallsignEveryCall;
    public static ConfigEntry<bool> FemaleVoices;
    public static ConfigEntry<bool> OverlapVoices;
    public static ConfigEntry<int> MaxOverlap;

    // Per-category volume of the calls (multiplier on top of Volume), see Radio/CallCategory.
    public static ConfigEntry<float> VolumeContacts;
    public static ConfigEntry<float> VolumeCombat;
    public static ConfigEntry<float> VolumeWarnings;
    public static ConfigEntry<float> VolumeOrders;
    public static ConfigEntry<float> VolumeOther;

    public static ConfigEntry<bool> WhyNot;
    public static ConfigEntry<bool> DetachForVid;
    public static ConfigEntry<bool> QueueOverlay;
    public static ConfigEntry<KeyCode> QueueOverlayKey;

    public static ConfigEntry<bool> FuelCalls;
    public static ConfigEntry<bool> RtbHighlight;
    public static ConfigEntry<bool> SarCalls;

    public static ConfigEntry<bool> UnitRoles;

    public static ConfigEntry<bool> ContactReports;
    public static ConfigEntry<float> FadeHysteresis;
    public static ConfigEntry<float> ContactCooldown;
    public static ConfigEntry<int> FlickerCount;
    public static ConfigEntry<bool> AnnounceNeutral;

    public static ConfigEntry<bool> LogRoster;
    public static ConfigEntry<bool> LogStockVoice;
    public static ConfigEntry<bool> LogRadio;

    private static ConfigFile file;
    private static float saveAt = -1f;

    /// <summary>
    /// The options page changes values while a slider is dragged; the file is written once, a moment after the last
    /// change, instead of on every step.
    /// </summary>
    public static void SaveSoon() => saveAt = Time.unscaledTime + 1f;

    public static void FlushIfDue()
    {
        if (saveAt < 0f || Time.unscaledTime < saveAt)
            return;
        saveAt = -1f;
        try { file?.Save(); }
        catch (System.Exception ex) { ModLog.Warn($"Could not save the settings: {ex.Message}"); }
    }

    public static void Init()
    {
        file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);
        file.SaveOnConfigSet = false;

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");

        ReplaceStockVoices = file.Bind("2. Radio", "Replace stock voices", true,
            "Mute the game's voice lines and have every unit say them in its own voice and callsign.");
        Volume = file.Bind("2. Radio", "Volume", 1.0f,
            new ConfigDescription("Master radio volume on top of the game's voice volume slider (1 = default, above 1 is made louder in the clip). Also on the Audio page of the Options.", new AcceptableValueRange<float>(0f, 2f)));
        ReportVolume = file.Bind("2. Radio", "Reports volume", 0.5f,
            new ConfigDescription("Level of the background traffic (contacts, launches, status) relative to replies to your " +
                                  "orders and alerts, which always play at full level. 0.5 = about 6 dB quieter.",
                                  new AcceptableValueRange<float>(0f, 1f)));
        LinkAlerts = file.Bind("2. Radio", "Keep messages until spoken", true,
            "The message bottom right that belongs to a radio call stays visible until the call has been spoken, " +
            "and is highlighted while it is being spoken.");
        LinkMentions = file.Bind("2. Radio", "Clickable units and tracks", true,
            "Every unit and track a message mentions (bottom right and in the message log) is a link that moves " +
            "the camera there - own units, contacts, track numbers - not only the ones the game links itself.");
        CallsignEveryCall = file.Bind("2. Radio", "Callsign on every call", false,
            "Say the unit's name at the start of every call. Off: only when a different unit talks than the one " +
            "before - the same unit again just goes on, a wingman of the same flight only gives its number.");
        RadioNoise = file.Bind("2. Radio", "Radio noise", 1.0f,
            new ConfigDescription("Static: how much hiss and crackle sits under the voices. 1 = the default amount, 0 = clean " +
                                  "voice. Also on the Audio page of the game's Options (0-150 %). Applies from the next call.",
                                  new AcceptableValueRange<float>(0f, 3f)));
        FemaleVoices = file.Bind("2. Radio", "Female voices", true,
            "Off: only male voices are picked for units. Takes effect at the next mission start; switching it off during " +
            "a mission moves the speakers that have a female voice to a male one at once. A unit whose name exists only " +
            "in a female voice keeps it.");
        OverlapVoices = file.Bind("2. Radio", "Overlapping voices", true,
            "Several stations can talk over each other, like a busy net: a new call starts while another is still " +
            "playing (never the same unit twice at once). Off: one call at a time, the rest wait in the queue.");
        MaxOverlap = file.Bind("2. Radio", "Voices at once", 3,
            new ConfigDescription("Most calls playing at the same time when Overlapping voices is on.", new AcceptableValueRange<int>(2, 4)));

        WhyNot = file.Bind("4. Why not", "Explain refusals", true,
            "Units say why an ordered engagement isn't happening (reloading, no radar track, out of envelope, sonar " +
            "contact only...), tell you their place in the queue, and why a wingman won't VID.");
        DetachForVid = file.Bind("4. Why not", "Detach wingman for VID", false,
            "A wingman ordered to VID is released from its formation so the VID actually happens (the game would " +
            "keep it in formation). Off = it just tells you it is holding formation.");
        QueueOverlay = file.Bind("5. Order queue", "Show order queue", true,
            "Small window listing the selected unit's stacked attack orders, each with a delete button.");
        QueueOverlayKey = file.Bind("5. Order queue", "Toggle key", KeyCode.F8,
            "Shows / hides the order queue window.");

        FuelCalls = file.Bind("6. Air ops", "Fuel calls", true,
            "Aircraft and helicopters call joker, bingo and fuel emergency against the nearest deck or airfield that " +
            "can take them: joker = at cruise speed only just enough fuel to get there, bingo = only just enough even " +
            "at the most economical speed, emergency = not enough at all. Aircraft nothing in the mission can take " +
            "call against the nearest friendly field all the same, or at 25 / 15 / 5 % of a full tank without one.");
        RtbHighlight = file.Bind("6. Air ops", "Highlight RTB", true,
            "Aircraft returning to base are yellow on the map and in the formation manager, and returning groups " +
            "move to the bottom of the formation manager.");
        SarCalls = file.Bind("6. Air ops", "Search and rescue calls", true,
            "Survivors in the water, rescuer proceeding, pickup complete with souls aboard, cabin full, survivors " +
            "delivered - spoken and as messages (replaces the game's one-line pickup message).");

        UnitRoles = file.Bind("7. Formation manager", "Show unit roles", true,
            "A small line under each unit says what it is: the ship type (Destroyer, Frigate, Attack Submarine), " +
            "the mission an aircraft is loaded for (CAP, Strike, SEAD, AEW...), the helicopter type (ASW " +
            "Helicopter). Groups show the role their members share. Takes effect the next time the formation " +
            "manager opens.");

        ContactReports = file.Bind("3. Contacts", "Contact reports", true,
            "New, lost, intermittent and hostile contacts are called out by the unit that detected them.");
        FadeHysteresis = file.Bind("3. Contacts", "Lost after (seconds)", 20f,
            "A contact must stay lost this long before it is called out; regained earlier = silence.");
        ContactCooldown = file.Bind("3. Contacts", "Per-contact cooldown (seconds)", 120f,
            "The same call for the same contact at most once in this time.");
        FlickerCount = file.Bind("3. Contacts", "Flicker drops per minute", 4,
            "A contact lost this often within a minute is called intermittent once, then stays quiet until stable.");
        AnnounceNeutral = file.Bind("3. Contacts", "Announce neutral classification", false,
            "Also call out contacts classified neutral (merchant traffic can be a lot).");

        VolumeContacts = file.Bind("8. Call volumes", "Contacts and tracks", 1.0f,
            new ConfigDescription("Volume of new / lost / identified contact and track calls (multiplier, 1 = default).",
                                  new AcceptableValueRange<float>(0f, 2f)));
        VolumeCombat = file.Bind("8. Call volumes", "Weapons and combat", 1.0f,
            new ConfigDescription("Volume of weapon launch, splash, hit, kill and jamming calls.", new AcceptableValueRange<float>(0f, 2f)));
        VolumeWarnings = file.Bind("8. Call volumes", "Warnings and damage", 1.0f,
            new ConfigDescription("Volume of urgent calls: missile or torpedo inbound, we are hit, flooding, bingo, emergencies.",
                                  new AcceptableValueRange<float>(0f, 2f)));
        VolumeOrders = file.Bind("8. Call volumes", "Replies to orders", 1.0f,
            new ConfigDescription("Volume of acknowledgements, status replies and the spoken reasons an order is refused.",
                                  new AcceptableValueRange<float>(0f, 2f)));
        VolumeOther = file.Bind("8. Call volumes", "Air ops and other", 1.0f,
            new ConfigDescription("Volume of launch / recovery, fuel, search and rescue, mission and everything else.",
                                  new AcceptableValueRange<float>(0f, 2f)));

        LogRoster = file.Bind("9. Diagnostics", "Log roster", true,
            "On mission start, log every player element with its callsign and voice.");
        LogStockVoice = file.Bind("9. Diagnostics", "Log stock voice events", true,
            "Log every voice line the game asks for (key, speaker, number).");
        LogRadio = file.Bind("9. Diagnostics", "Log radio calls", true,
            "Log every call the mod queues and speaks.");
        file.Save();   // writes the entries a config from an older version did not have yet
    }
}
