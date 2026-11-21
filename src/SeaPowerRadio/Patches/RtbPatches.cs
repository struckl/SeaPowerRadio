using System.Runtime.CompilerServices;
using SeaPower;
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
    public static void Declare()
    {
        var feature = Rig.Hooks.Add("RtbHighlight", HookStage.Load,
            "returning aircraft are not marked yellow", optional: true);
        feature.Hook("SeapowerUI.MapContactVisuals", "Resolve", new[] { typeof(IUnit), typeof(MapViewModel) }, typeof(RtbPatches),
                postfix: nameof(ResolvePostfix))
            .Hook("SeapowerUI.MapUnitViewModel", "UpdateContactDisplay", new[] { typeof(IUnit) }, typeof(RtbPatches),
                postfix: nameof(SymbolPostfix))
            .Hook("SeapowerUI.MapUnitViewModel", "Update", System.Type.EmptyTypes, typeof(RtbPatches),
                prefix: nameof(SymbolUpdatePrefix))
            .Hook("SeapowerUI.UnitPostureBrushConverter", "Convert",
                new[] { typeof(object[]), typeof(System.Type), typeof(object), typeof(System.Globalization.CultureInfo) },
                typeof(RtbPatches), postfix: nameof(SymbolBrushPostfix));
        feature.Hook("SeapowerUI.FormationManagerView", ".ctor", System.Type.EmptyTypes, typeof(RtbPatches),
            postfix: nameof(ViewPostfix));
    }

    private static bool Marked(IUnit unit)
    {
        if (!RtbMarker.Enabled)
            return false;
        ObjectBase o = unit?.BaseObject;
        return o != null && o._taskforce == Globals._playerTaskforce && !unit.IsDormant.Value && RtbMarker.IsRtb(o);
    }

    private static void ResolvePostfix(IUnit unit, MapViewModel map, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result) =>
        Recolour(unit, ref __result, () => RtbMarker.Brush(map?.PaletteOrDay));

    private static void Recolour(IUnit unit, ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) result,
        System.Func<Noesis.SolidColorBrush> brush)
    {
        try
        {
            if (Marked(unit))
                result = (result.Posture, brush());
        }
        catch (System.Exception ex)
        {
            Rig.Log.Debug($"[rtb] map colour: {ex.Message}");
        }
    }

    /// <summary>Last state each map symbol was drawn with (true = yellow), to mark it dirty when that changes.</summary>
    private static readonly ConditionalWeakTable<MapUnitViewModel, StrongBox<bool>> Drawn =
        new ConditionalWeakTable<MapUnitViewModel, StrongBox<bool>>();

    private static void SymbolPostfix(MapUnitViewModel __instance, IUnit unit, bool ____isNight,
        ref (MapVisualPosture Posture, Noesis.SolidColorBrush Brush) __result)
    {
        try
        {
            bool marked = Marked(unit);
            StrongBox<bool> drawn = Drawn.GetValue(__instance, _ => new StrongBox<bool>());
            if (drawn.Value != marked)
            {
                drawn.Value = marked;
                // The symbol itself is coloured by UnitPostureBrushConverter from UnitPalette: make it convert again.
                RaiseChanged?.Invoke(__instance, "UnitPalette");
            }
            if (marked)
                __result = (__result.Posture, RtbMarker.Brush(____isNight));
        }
        catch (System.Exception ex)
        {
            Rig.Log.Debug($"[rtb] symbol colour: {ex.Message}");
        }
    }

    private static readonly System.Action<MapUnitViewModel, string> RaiseChanged = MakeRaiseChanged();

    private static System.Action<MapUnitViewModel, string> MakeRaiseChanged()
    {
        try
        {
            return HarmonyLib.AccessTools.MethodDelegate<System.Action<MapUnitViewModel, string>>(
                HarmonyLib.AccessTools.Method(typeof(MapUnitViewModel), "OnPropertyChanged", new[] { typeof(string) }));
        }
        catch (System.Exception ex)
        {
            Rig.Log.Debug($"[rtb] no OnPropertyChanged: {ex.Message}");
            return null;
        }
    }

    /// <summary>The map symbol (the aircraft shape, not only its label and heading line) in yellow too.</summary>
    private static void SymbolBrushPostfix(object[] values, ref object __result)
    {
        try
        {
            if (values == null || values.Length < 4 || !(values[3] is MapUnitViewModel vm) || !Marked(vm.Unit))
                return;
            __result = RtbMarker.Brush(MissionEditorSideColorBrushConverter.IsDarkMap(values[1] as Noesis.ResourceDictionary));
        }
        catch (System.Exception ex)
        {
            Rig.Log.Debug($"[rtb] symbol brush: {ex.Message}");
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
            Rig.Log.Debug($"[rtb] symbol update: {ex.Message}");
        }
    }

    private static void ViewPostfix(FormationManagerView __instance) => RtbMarker.ViewCreated(__instance);
}
