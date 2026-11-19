using SeapowerUI.ViewModels;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The bottom-right message list: every RaiseAlert overload ends in the AlertyMcAlertFace setter, which appends to
/// TopAlerts and evicts the oldest beyond three. The prefix does the same, but keeps messages whose radio call
/// is still waiting (Radio/AlertLink). R.A.T.S. Command Tools makes the mentions in a message links with its own,
/// higher-priority prefix on the same setter, so it runs before this one.
/// </summary>
internal static class AlertPatches
{
    public static void Declare() => HookRegistry.Add("AlertLink", HookStage.Load,
            "messages are not kept until their call is spoken", optional: true)
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "set_AlertyMcAlertFace", new[] { typeof(Alert) },
            typeof(AlertPatches), prefix: nameof(SetPrefix));

    private static bool SetPrefix(MainGameViewModel __instance, Alert value)
    {
        if (value == null)
            return true;
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
