namespace SeaPowerRadio;

/// <summary>
/// Every game method the mod hooks, by feature (Rig.Hooks, R.A.T.S. Rigging), all applied at load. The mission hooks
/// and the aircraft launch are required: the roster is built from them and every call is spoken from the roster, so without
/// them the game's own voices play anyway and the mod stays off. Every other feature is optional: a missing target switches
/// only that feature off.
/// </summary>
internal static class Hooks
{
    public static void Declare()
    {
        Patches.StockVoicePatches.Declare();
        Patches.LaunchVoicePatches.Declare();
        Patches.SpeedOrderPatches.Declare();
        Rig.Mission.Declare();
        Patches.AircraftLaunchPatches.Declare();
        Patches.WhyNotPatches.Declare();
        Patches.AlertPatches.Declare();
        Patches.ContactAlertPatches.Declare();
        Patches.RtbPatches.Declare();
        Patches.SarPatches.Declare();
        Patches.UnitRolePatches.Declare();
        Patches.OptionsPatches.Declare();
    }
}
