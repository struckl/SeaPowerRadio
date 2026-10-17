using System.IO;
using BepInEx;
using BepInEx.Configuration;

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
        RadioNoise = file.Bind("2. Radio", "Radio noise", 1.0f,
            new ConfigDescription("How much hiss and crackle sits under the voices. 0 = clean.", new AcceptableValueRange<float>(0f, 3f)));

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
