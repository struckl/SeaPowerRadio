using System.Collections.Generic;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Roster;

namespace SeaPowerRadio.Radio;

/// <summary>
/// The end of every report of this mod: the call is queued with the messages raised for its speaker in the same
/// frame (<see cref="AlertLink.TakeFor"/>, taken by the caller right after raising them, spoken or not).
/// </summary>
internal static class SpokenReport
{
    /// <summary>Queue the call with its messages; a call that could not be built (no clip for this voice) is dropped.</summary>
    public static void Submit(RadioPlayer player, Transmission call, List<Alert> alerts)
    {
        if (call == null)
            return;
        call.Alerts.AddRange(alerts);
        player.Submit(call);
    }

    public static void Say(RadioPlayer player, RosterElement speaker, string phrase, int? number, List<Alert> alerts) =>
        Submit(player, TransmissionBuilder.Build(speaker, phrase, number), alerts);
}
