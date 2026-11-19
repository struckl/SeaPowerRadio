using SeaPower;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Patches;

/// <summary>
/// A launch line ("ASHMFired", "WeaponAway"...) only carries the launching unit. <c>WeaponBase.LaunchVoiceover</c>
/// asks for it right away, so the prefix notes the weapon and the stock voice router, called in the same frame,
/// picks the line that fits it (Radio/CallOrder, StockVoiceRouter.LaunchPhrase). Missing hook = the stock key decides.
/// </summary>
internal static class LaunchVoicePatches
{
    public static void Declare() => HookRegistry.Add("LaunchLine", HookStage.Load,
            "the launch line may come before the launch message", optional: true)
        .Hook("SeaPower.WeaponBase", "LaunchVoiceover", System.Type.EmptyTypes, typeof(LaunchVoicePatches),
            prefix: nameof(LaunchVoiceoverPrefix));

    private static void LaunchVoiceoverPrefix(WeaponBase __instance) => CallOrder.NoteLaunch(__instance);
}
