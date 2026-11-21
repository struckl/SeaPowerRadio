using System;
using SeaPower;

namespace SeaPowerRadio.Core;

/// <summary>
/// A message bottom right, raised the game's way: <c>MainGameViewModel.RaiseAlert(raising, about, text)</c> turns
/// <c>${Name}</c> into a link to the raising unit and <c>${Target}</c> / <c>${TrackId}</c> into links to the object the
/// message is about. Every report of this mod goes through here, so it is raised and logged the same way.
/// </summary>
internal static class GameMessage
{
    /// <summary>Raise the message: with a unit and an object it is about, with a unit only, or bare.</summary>
    public static void Raise(string tag, ObjectBase unit, ObjectBase about, string text)
    {
        try
        {
            var vm = Globals._mainGameViewModel;
            if (vm == null)
                return;
            if (unit != null && about != null)
                vm.RaiseAlert(unit, about, text);
            else if (unit != null)
                vm.RaiseAlert(unit, text);
            else
                vm.RaiseAlert(text);
        }
        catch (Exception ex)
        {
            Rig.Log.Debug($"[{tag}] alert failed: {ex.Message}");
        }
    }

    /// <summary>Log the message as the player reads it (track number unknown here: "?"), then raise it.</summary>
    public static void RaiseLogged(string tag, ObjectBase unit, ObjectBase about, string text, string detail = "")
    {
        Rig.Log.Info($"[{tag}] {Plain(text, unit, about)}{detail}");
        Raise(tag, unit, about, text);
    }

    private static string Plain(string text, ObjectBase unit, ObjectBase about) =>
        text.Replace("${Name}", unit != null ? unit.getName() : "")
            .Replace("${Target}", about != null ? about.getName() : "")
            .Replace("${TrackId}", "?");
}
