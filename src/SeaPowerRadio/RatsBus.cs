using System;
using System.Linq;

namespace SeaPowerRadio;

/// <summary>
/// How R.A.T.S. mods find each other at run time without referencing each other's DLLs: values in the AppDomain, of
/// plain BCL types only, so any mod can read them whatever was loaded first. Written down in <c>docs/rats-bus.md</c>;
/// R.A.T.S. Command Tools has the same keys.
/// <list type="bullet">
/// <item><c>rats.radio.version</c> (string): this Radio (2.0 or newer) is running.</item>
/// <item><c>rats.radio.say</c> (<c>Func&lt;object, string[], int[], bool&gt;</c>): other mods speak over the net, see
/// <see cref="RadioApi"/>.</item>
/// <item><c>rats.commandtools.version</c> / <c>rats.commandtools.features</c> (strings): R.A.T.S. Command Tools runs, and
/// which of its features (whynot, contacts ...). Radio adapts its handling of the game's own lines to them.</item>
/// </list>
/// </summary>
internal static class RatsBus
{
    public const string RadioVersionKey = "rats.radio.version";
    public const string RadioSayKey = "rats.radio.say";
    public const string ToolsVersionKey = "rats.commandtools.version";
    public const string ToolsFeaturesKey = "rats.commandtools.features";

    public const string WhyNot = "whynot", Contacts = "contacts";

    public static void AnnounceRadio(string version)
    {
        AppDomain.CurrentDomain.SetData(RadioVersionKey, version);
        AppDomain.CurrentDomain.SetData(RadioSayKey, new Func<object, string[], int[], bool>(RadioApi.Say));
    }

    /// <summary>R.A.T.S. Command Tools is running.</summary>
    public static bool ToolsRunning => AppDomain.CurrentDomain.GetData(ToolsVersionKey) is string;

    /// <summary>R.A.T.S. Command Tools runs this feature (read live: every mod has been loaded by the time it matters).</summary>
    public static bool ToolsHas(string feature) =>
        AppDomain.CurrentDomain.GetData(ToolsFeaturesKey) is string list
        && list.Split(',').Any(f => string.Equals(f.Trim(), feature, StringComparison.OrdinalIgnoreCase));
}
