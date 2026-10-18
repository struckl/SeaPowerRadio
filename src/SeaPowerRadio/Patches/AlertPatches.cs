using SeapowerUI.ViewModels;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The bottom-right message list: every RaiseAlert overload ends in the AlertyMcAlertFace setter, which appends to
/// TopAlerts and evicts the oldest beyond three. The prefix does the same, but keeps messages whose radio call
/// is still waiting (Radio/AlertLink). Before that, every unit and track the message mentions is made a link
/// (Radio/MessageLinker) – the message log shows the same message, so it gets the links too.
/// </summary>
internal static class AlertPatches
{
    public const string FeatureName = "AlertLink";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "set_AlertyMcAlertFace", new[] { typeof(Alert) },
            typeof(AlertPatches), prefix: nameof(SetPrefix));

    private static bool SetPrefix(MainGameViewModel __instance, Alert value)
    {
        if (value == null)
            return true;
        MessageLinker.Link(value);
        if (!AlertLink.Enabled)
            return true;
        try
        {
            AlertLink.Show(__instance, value);
            return false;
        }
        catch (System.Exception ex)
        {
            ModLog.Error($"[alerts] {ex.Message}");
            return true;
        }
    }
}
