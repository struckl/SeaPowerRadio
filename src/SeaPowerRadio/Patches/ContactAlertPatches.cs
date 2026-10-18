using SeaPower;

namespace SeaPowerRadio.Patches;

/// <summary>
/// The game's own contact messages (new / classified / faded) come from three Taskforce methods. While the mod
/// reports contacts itself – with anti-spam and links to the track – the messages those methods raise are
/// swallowed; the methods themselves still run, so their bookkeeping stays intact.
/// </summary>
internal static class ContactAlertPatches
{
    public const string FeatureName = "ContactAlerts";

    [System.ThreadStatic] private static int inStockContactReport;

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeaPower.Taskforce", "CheckNewContactMessage", System.Type.EmptyTypes, typeof(ContactAlertPatches),
            prefix: nameof(Enter), postfix: nameof(Leave))
        .Hook("SeaPower.Taskforce", "CheckContactClassifiedMessage", System.Type.EmptyTypes, typeof(ContactAlertPatches),
            prefix: nameof(Enter), postfix: nameof(Leave))
        .Hook("SeaPower.Taskforce", "CheckContactFadedMessage", System.Type.EmptyTypes, typeof(ContactAlertPatches),
            prefix: nameof(Enter), postfix: nameof(Leave))
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "RaiseAlert", new[] { typeof(string) },
            typeof(ContactAlertPatches), prefix: nameof(Filter))
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "RaiseAlert", new[] { typeof(ObjectBase), typeof(string) },
            typeof(ContactAlertPatches), prefix: nameof(Filter));

    private static void Enter() => inStockContactReport++;

    private static void Leave()
    {
        if (inStockContactReport > 0) inStockContactReport--;
    }

    private static bool Filter() =>
        inStockContactReport == 0 || !Settings.ContactReports.Value;
}
