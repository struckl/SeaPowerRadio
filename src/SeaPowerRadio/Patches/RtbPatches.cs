using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeaPowerRadio.Air;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Returning aircraft yellow on the map: every map symbol takes its colour from MapContactVisuals.Resolve; the
/// postfix swaps it for own aircraft in the ReturnToBase state. The formation manager view is remembered when it
/// is built, so Air/RtbMarker can colour its rows.
/// </summary>
internal static class RtbPatches
{
    public const string FeatureName = "RtbHighlight";

    public static Feature Declare() => new Feature(FeatureName)
#if FLATMAP
        .Hook("SeapowerUI.MapContactVisuals", "Resolve", new[] { typeof(IUnit) }, typeof(RtbPatches),
            postfix: nameof(ResolvePostfix))
#else
        .Hook("SeapowerUI.MapContactVisuals", "Resolve", new[] { typeof(IUnit), typeof(MapViewModel) }, typeof(RtbPatches),
            postfix: nameof(ResolvePostfix))
#endif
        .Hook("SeapowerUI.FormationManagerView", ".ctor", System.Type.EmptyTypes, typeof(RtbPatches),
            postfix: nameof(ViewPostfix));

#if FLATMAP
    private static void ResolvePostfix(IUnit unit, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
#else
    private static void ResolvePostfix(IUnit unit, MapViewModel map, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
#endif
    {
        if (!RtbMarker.Enabled)
            return;
        try
        {
            ObjectBase o = unit?.BaseObject;
            if (o == null || o._taskforce != Globals._playerTaskforce || unit.IsDormant.Value || !RtbMarker.IsRtb(o))
                return;
#if FLATMAP
            __result = (__result.Posture, RtbMarker.Brush());
#else
            __result = (__result.Posture, RtbMarker.Brush(map?.PaletteOrDay));
#endif
        }
        catch (System.Exception ex)
        {
            ModLog.Debug($"[rtb] map colour: {ex.Message}");
        }
    }

    private static void ViewPostfix(FormationManagerView __instance) => RtbMarker.ViewCreated(__instance);
}
