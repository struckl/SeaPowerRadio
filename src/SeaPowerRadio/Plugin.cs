using System.Runtime.CompilerServices;
using AnchorChain;

namespace SeaPowerRadio;

/// <summary>
/// Entry point, run by either mod loader: R.A.T.S. Keel or Anchor Chain (both call TriggerEntryPoint of an [ACPlugin] class).
/// Two builds of the same sources: SeaPowerRadio.dll for the 3D-globe game and SeaPowerRadioFlat.dll (FLATMAP) for 0.8.7's
/// flat map; each stays off on the other game (ModEntry, in the shared skeleton).
/// </summary>
[ACPlugin(PluginGuid, Name, Version)]
public class Plugin : IAnchorChainMod
{
    public const string Guid = "dev.sewerlabs.seapowerradio";
    public const string Version = "1.6.0";
    public const string Name = "R.A.T.S. Radio";

#if FLATMAP
    // Both loaders skip a plugin whose GUID they have already seen, so the two builds register under different ones.
    public const string PluginGuid = Guid + ".flatmap";
#else
    public const string PluginGuid = Guid;
#endif

    public void TriggerEntryPoint() => ModEntry.Trigger(Start);

    // Apart from the check in Trigger, so nothing in here is compiled on a game it isn't for.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Start() => ModEntry.Start<RadioHost>(Settings.Bind, Hooks.Declare, Announce);

    // After the hooks went on and before the host starts.
    private static void Announce()
    {
        Voices.VoicePacks.Discover(System.Version.Parse(Version));
        Roster.RosterService.Init();
        ModLog.Info($"Features: {HookRegistry.Summary()}");
    }
}
