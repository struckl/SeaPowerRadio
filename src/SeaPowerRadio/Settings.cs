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

    public static ConfigEntry<bool> LogRoster;
    public static ConfigEntry<bool> LogStockVoice;
    public static ConfigEntry<bool> SuppressStockVoice;

    public static void Init()
    {
        var file = new ConfigFile(Path.Combine(Paths.ConfigPath, Plugin.Guid + ".cfg"), saveOnInit: true);

        Enabled = file.Bind("1. General", "Enable mod", true,
            "Master switch. Needs a restart.");
        VerboseLog = file.Bind("1. General", "Verbose log", false,
            "Write debug detail to BepInEx/LogOutput.log.");

        LogRoster = file.Bind("9. Diagnostics", "Log roster", true,
            "On mission start, log every player element with its callsign and voice.");
        LogStockVoice = file.Bind("9. Diagnostics", "Log stock voice events", true,
            "Log every voice line the game asks for (key, speaker, number).");
        SuppressStockVoice = file.Bind("9. Diagnostics", "Suppress stock voices", false,
            "Mute the game's own voice lines. Turned on once the mod's own voices are in.");
    }
}
