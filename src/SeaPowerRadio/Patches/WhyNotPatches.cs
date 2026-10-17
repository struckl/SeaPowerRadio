using SeaPower;
using SeaPowerRadio.WhyNot;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Cease fire clears a unit's orders on purpose; marking it keeps the why-not monitor from reporting those orders
/// as dropped by the game.
/// </summary>
internal static class WhyNotPatches
{
    public const string FeatureName = "WhyNot";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeaPower.ObjectBase", "CeaseFire", null, typeof(WhyNotPatches), prefix: nameof(CeaseFirePrefix));

    private static void CeaseFirePrefix(ObjectBase __instance) => WhyNotMonitor.MarkCleared(__instance);
}
