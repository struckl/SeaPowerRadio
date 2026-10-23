using SeapowerUI;
using SeaPowerRadio.Formation;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The role line in the formation manager: once a FormationManagerView has loaded its XAML (its constructor), its
/// row templates get the extra line (Formation/RoleLine). Same hook as the RTB colouring, which already runs at load.
/// </summary>
internal static class UnitRolePatches
{
    public const string FeatureName = "UnitRoles";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeapowerUI.FormationManagerView", ".ctor", System.Type.EmptyTypes, typeof(UnitRolePatches),
            postfix: nameof(ViewPostfix));

    private static void ViewPostfix(FormationManagerView __instance) => RoleLine.ViewCreated(__instance);
}
