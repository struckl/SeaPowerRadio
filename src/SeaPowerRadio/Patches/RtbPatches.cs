using System.Runtime.CompilerServices;
using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeaPowerRadio.Air;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Returning aircraft yellow on the map. Sea Power 0.8.7 colours every map symbol through MapContactVisuals.Resolve;
/// since globe build 261107 the symbols work out their colour in MapUnitViewModel.UpdateContactDisplay instead (Resolve
/// only colours the wake bubbles), and only when the symbol is marked dirty. So the globe build swaps the brush there and
/// marks a symbol dirty when its unit starts or stops heading home. The formation manager view is remembered when it is
/// built, so Air/RtbMarker can colour its rows.
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
        .Hook("SeapowerUI.MapUnitViewModel", "UpdateContactDisplay", new[] { typeof(IUnit) }, typeof(RtbPatches),
            postfix: nameof(SymbolPostfix))
        .Hook("SeapowerUI.MapUnitViewModel", "Update", System.Type.EmptyTypes, typeof(RtbPatches),
            prefix: nameof(SymbolUpdatePrefix))
#endif
        .Hook("SeapowerUI.FormationManagerView", ".ctor", System.Type.EmptyTypes, typeof(RtbPatches),
            postfix: nameof(ViewPostfix));

    private static bool Marked(IUnit unit)
    {
        if (!RtbMarker.Enabled)
            return false;
        ObjectBase o = unit?.BaseObject;
        return o != null && o._taskforce == Globals._playerTaskforce && !unit.IsDormant.Value && RtbMarker.IsRtb(o);
    }

#if FLATMAP
    private static void ResolvePostfix(IUnit unit, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
#else
    private static void ResolvePostfix(IUnit unit, MapViewModel map, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
#endif
    {
        try
        {
            if (!Marked(unit))
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

#if !FLATMAP
    /// <summary>Last state each map symbol was drawn with (true = yellow), to mark it dirty when that changes.</summary>
    private static readonly ConditionalWeakTable<MapUnitViewModel, StrongBox<bool>> Drawn =
        new ConditionalWeakTable<MapUnitViewModel, StrongBox<bool>>();

    private static void SymbolPostfix(MapUnitViewModel __instance, IUnit unit, bool ____isNight,
        ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
    {
        try
        {
            bool marked = Marked(unit);
            Drawn.GetValue(__instance, _ => new StrongBox<bool>()).Value = marked;
            if (marked)
                __result = (__result.Posture, RtbMarker.Brush(____isNight));
        }
        catch (System.Exception ex)
        {
            ModLog.Debug($"[rtb] symbol colour: {ex.Message}");
        }
    }

    private static void SymbolUpdatePrefix(MapUnitViewModel __instance)
    {
        try
        {
            if (Drawn.TryGetValue(__instance, out StrongBox<bool> drawn) && drawn.Value != Marked(__instance.Unit))
                __instance.InvalidateContactDisplay();
        }
        catch (System.Exception ex)
        {
            ModLog.Debug($"[rtb] symbol update: {ex.Message}");
        }
    }
#endif

    private static void ViewPostfix(FormationManagerView __instance) => RtbMarker.ViewCreated(__instance);
}
