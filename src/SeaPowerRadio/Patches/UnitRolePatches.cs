using SeapowerUI;
using SeaPowerRadio.Formation;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The role line in the formation manager: once a FormationManagerView has loaded its XAML (its constructor), its
/// row templates get the extra line (Formation/RoleLine). Same hook as the RTB colouring, which already runs at load.
/// </summary>
internal static class UnitRolePatches
{
    public static void Declare() => Rig.Hooks.Add("UnitRoles", HookStage.Load,
            "the formation manager shows no role lines", optional: true)
        .Hook("SeapowerUI.FormationManagerView", ".ctor", System.Type.EmptyTypes, typeof(UnitRolePatches),
            postfix: nameof(ViewPostfix));

    private static void ViewPostfix(FormationManagerView __instance) => RoleLine.ViewCreated(__instance);
}
