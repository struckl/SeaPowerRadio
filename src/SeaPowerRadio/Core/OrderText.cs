using SeaPower;

namespace SeaPowerRadio.Core;

/// <summary>How an attack order is named on screen: the weapon's display name and the target's track number.</summary>
internal static class OrderText
{
    public static string Weapon(ObjectBase unit, EngageTask task) =>
        (task._ammunition ?? unit.getAmmunitionByName(task._ammoId))?._ap?._displayedName ?? task._ammoId;

    public static string Target(EngageTask task) =>
        task._targetObject != null ? $"Track {task._targetObject.GetMapContactID()}" : "position";
}
