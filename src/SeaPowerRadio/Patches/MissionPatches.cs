using SeaPower;
using SeaPowerRadio.Core;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Mission lifecycle (docs/how-it-works.md, "Mission lifecycle and roster"):
/// - <c>MainGameViewModel.OnStart</c> - called once from <c>SceneCreator.LoadMission</c> after every unit,
/// formation, hangar and save state is loaded. That is "mission ready".
/// - <c>GameMain.OnDestroy</c> - GameMain lives in the mission scene, so it dies on every unload path
/// (exit, restart, campaign).
/// - <c>FlightDeck.launchVehicle</c> - returns the freshly created aircraft; its args say which hangar slot.
/// </summary>
internal static class MissionPatches
{
    public const string FeatureName = "Mission";

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "OnStart", System.Type.EmptyTypes,
            typeof(MissionPatches), postfix: nameof(MissionReady))
        .Hook("SeaPower.GameMain", "OnDestroy", System.Type.EmptyTypes,
            typeof(MissionPatches), prefix: nameof(MissionUnloading))
        .Hook("SeaPower.FlightDeck", "launchVehicle", null,
            typeof(MissionPatches), postfix: nameof(AircraftLaunched));

    private static void MissionReady() => EventBus.PublishMissionStarted();

    private static void MissionUnloading() => EventBus.PublishMissionEnded();

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
