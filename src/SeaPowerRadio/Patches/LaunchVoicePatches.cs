using SeaPower;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Patches;

/// <summary>
/// A launch line ("ASHMFired", "WeaponAway"…) only carries the launching unit. <c>WeaponBase.LaunchVoiceover</c>
/// asks for it right away, so the prefix notes the weapon and the stock voice router, called in the same frame,
/// picks the line that fits it (Radio/StockVoiceRouter.LaunchPhrase). Missing hook = the stock key decides, as before.
/// </summary>
internal static class LaunchVoicePatches
{
    public const string FeatureName = "LaunchLine";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeaPower.WeaponBase", "LaunchVoiceover", System.Type.EmptyTypes, typeof(LaunchVoicePatches),
            prefix: nameof(LaunchVoiceoverPrefix));

    private static void LaunchVoiceoverPrefix(WeaponBase __instance) => StockVoiceRouter.NoteLaunch(__instance);
}
