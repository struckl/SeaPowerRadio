using SeapowerUI;
using SeaPowerRadio.Ui;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The radio settings in the game's Options menu: once an OptionsMenuView has loaded its XAML (its constructor,
/// also run for the page opened from the pause menu and again after Revert), the "Rats Radio" group is added to
/// its Audio tab (Ui/OptionsPage). Same kind of hook as the formation manager's role line.
/// </summary>
internal static class OptionsPatches
{
    public const string FeatureName = "OptionsPage";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeapowerUI.OptionsMenuView", ".ctor", System.Type.EmptyTypes, typeof(OptionsPatches),
            postfix: nameof(ViewPostfix));

    private static void ViewPostfix(OptionsMenuView __instance) => OptionsPage.ViewCreated(__instance);
}
