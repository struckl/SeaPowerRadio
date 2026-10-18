using SeaPower;
using SeaPowerRadio.Air;

namespace SeaPowerRadio.Patches;

/// <summary>
/// Search-and-rescue events for Air/SarReporter: a life raft is registered (ObjectsManager.AddObjectToItsTaskforce),
/// a SAR zone sends a unit to a raft (PatrolZone.AssignSARRescueRoute), a unit picks survivors up
/// (ObjectBase.CheckForRaftPickUp – the count is the change of the task force's rescued total; the game's own
/// one-line "Picked up survivors" message is replaced by the mod's, which says how many and how full), an aircraft
/// lands (FlightDeck.createRecoveryTask, at the end of the landing circuit).
/// </summary>
internal static class SarPatches
{
    public const string FeatureName = "SarCalls";

    [System.ThreadStatic] private static int inPickup;

    public static Feature Declare() => new Feature(FeatureName)
        .Hook("SeaPower.ObjectsManager", "AddObjectToItsTaskforce", null, typeof(SarPatches), postfix: nameof(Registered))
        .Hook("SeaPower.PatrolZone", "AssignSARRescueRoute", null, typeof(SarPatches), postfix: nameof(Assigned))
        .Hook("SeaPower.ObjectBase", "CheckForRaftPickUp", null, typeof(SarPatches),
            prefix: nameof(PickupBefore), postfix: nameof(PickupAfter))
        .Hook("SeapowerUI.ViewModels.MainGameViewModel", "RaiseAlert", new[] { typeof(ObjectBase), typeof(string) },
            typeof(SarPatches), prefix: nameof(Filter))
        .Hook("SeaPower.FlightDeck", "createRecoveryTask", null, typeof(SarPatches), postfix: nameof(Recovering));

    private static bool Active => Settings.SarCalls != null && Settings.SarCalls.Value && SarReporter.Instance != null;

    private static void Registered(ObjectBase obj)
    {
        if (!Active || obj?._obp?._unitRoles == null || !obj._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.LifeRaft))
            return;
        try { SarReporter.Instance.RaftAppeared(obj); }
        catch (System.Exception ex) { ModLog.Debug($"[sar] raft: {ex.Message}"); }
    }

    private static void Assigned(ObjectBase rescuer, ObjectBase raft)
    {
        if (!Active) return;
        try { SarReporter.Instance.EnRoute(rescuer, raft); }
        catch (System.Exception ex) { ModLog.Debug($"[sar] en route: {ex.Message}"); }
    }

    private static void PickupBefore(ObjectBase __instance, out int __state)
    {
        __state = __instance._taskforce != null ? __instance._taskforce._survivorsResqued : 0;
        inPickup++;
    }

    private static void PickupAfter(ObjectBase __instance, int __state)
    {
        if (inPickup > 0) inPickup--;
        if (!Active || __instance._taskforce == null)
            return;
        int picked = __instance._taskforce._survivorsResqued - __state;
        if (picked <= 0)
            return;
        try { SarReporter.Instance.PickedUp(__instance, picked); }
        catch (System.Exception ex) { ModLog.Debug($"[sar] pickup: {ex.Message}"); }
    }

    // The game's "Picked up survivors." – raised inside CheckForRaftPickUp – gives way to the mod's message.
    private static bool Filter() => inPickup == 0 || !Active;

    private static void Recovering(FlightDeck __instance, ObjectBase objectToRecover)
    {
        if (!Active) return;
        try { SarReporter.Instance.Landed(objectToRecover, __instance._baseObject); }
        catch (System.Exception ex) { ModLog.Debug($"[sar] landed: {ex.Message}"); }
    }
}
