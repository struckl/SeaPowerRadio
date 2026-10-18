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

    public static ConfigEntry<bool> WhyNot;
    public static ConfigEntry<bool> DetachForVid;
    public static ConfigEntry<bool> QueueOverlay;
    public static ConfigEntry<KeyCode> QueueOverlayKey;

    public static ConfigEntry<bool> ContactReports;
    public static ConfigEntry<float> FadeHysteresis;
    public static ConfigEntry<float> ContactCooldown;
    public static ConfigEntry<int> FlickerCount;
    public static ConfigEntry<bool> AnnounceNeutral;

    public static ConfigEntry<bool> LogRoster;
    public static ConfigEntry<bool> LogStockVoice;
    public static ConfigEntry<bool> LogRadio;

    public static void Init()
    {
        var file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");

        ReplaceStockVoices = file.Bind("2. Radio", "Replace stock voices", true,
            "Mute the game's voice lines and have every unit say them in its own voice and callsign.");
        Volume = file.Bind("2. Radio", "Volume", 1.0f,
            new ConfigDescription("Radio volume on top of the game's voice volume slider.", new AcceptableValueRange<float>(0f, 2f)));
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
        RadioNoise = file.Bind("2. Radio", "Radio noise", 1.0f,
            new ConfigDescription("How much hiss and crackle sits under the voices. 0 = clean.", new AcceptableValueRange<float>(0f, 3f)));

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

        LogRoster = file.Bind("9. Diagnostics", "Log roster", true,
            "On mission start, log every player element with its callsign and voice.");
        LogStockVoice = file.Bind("9. Diagnostics", "Log stock voice events", true,
            "Log every voice line the game asks for (key, speaker, number).");
        LogRadio = file.Bind("9. Diagnostics", "Log radio calls", true,
            "Log every call the mod queues and speaks.");
    }
}
