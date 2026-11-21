using SeaPower;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Patches;

/// <summary>
/// <c>FlightDeck.launchVehicle</c> returns the freshly created aircraft; its args say which hangar slot. The roster gives the
/// aircraft its voice from it. The mission start and end hooks are Rigging's Rig.Mission (see Hooks).
/// </summary>
internal static class AircraftLaunchPatches
{
    // Required like the mission hooks: without the launch the roster misses every aircraft that takes off.
    public static void Declare() => Rig.Hooks.Add("AircraftLaunch", HookStage.Load, "mod inactive")
        .Hook("SeaPower.FlightDeck", "launchVehicle", null,
            typeof(AircraftLaunchPatches), postfix: nameof(AircraftLaunched));

    private static void AircraftLaunched(FlightDeck __instance, int vehicleIndex, int squadronIndex, ObjectBase __result)
    {
        if (__result == null) return;
        EventBus.PublishAircraftLaunched(new AircraftLaunch
        {
            Deck = __instance,
            VehicleIndex = vehicleIndex,
            SquadronIndex = squadronIndex,
            Aircraft = __result,
        });
    }
}
