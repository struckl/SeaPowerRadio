using System;
using System.Linq;
using SeaPower;
using SeaPowerRadio.Core;
using SeaPowerRadio.Roster;

namespace SeaPowerRadio.Radio;

/// <summary>
/// Decides, for every voice line the game asks for, whether the mod speaks it instead. Called synchronously from
/// the VoiceMessage prefix; returning true mutes the stock line.
/// <list type="bullet">
/// <item>Known key with a phrase → the speaking unit's own voice and callsign say it.</item>
/// <item>Known key without a phrase (contact report building blocks…) → muted; the mod reports contacts itself.</item>
/// <item>Pieces the game appends to a call it already made ("…track 7034", "salvo") → muted with the call.</item>
/// <item>Unknown key (another mod's lines) → the stock system plays it as before.</item>
/// </list>
/// </summary>
internal static class StockVoiceRouter
{
    public static RadioPlayer Player;

    public static bool Handle(StockVoiceRequest request)
    {
        if (!Settings.ReplaceStockVoices.Value || !Catalog.Loaded || Player == null)
            return false;
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return false; // menus, editor: no mission roster to speak with
        if (!Catalog.TryStock(request.Key, out string phrase, out string numberKind))
            // Unknown key. A piece appended to a task force report is one of the game's contact building blocks
            // built at runtime – swallow it with the rest of that report. Anything else (other mods' lines) plays.
            return request.Appended && request.Speaker == null;
        if (request.Appended && request.Key != "SplashedTrack")
            return true;
        if (phrase == null)
            return true;

        RosterElement speaker = request.Speaker != null ? roster.Resolve(request.Speaker) : roster.CommandVoice();
        if (speaker == null)
            return true; // an enemy or neutral unit "talking": the player's net doesn't hear it

        int? number = numberKind != null && request.Number >= 0 ? request.Number : (int?)null;
        phrase = Refine(phrase, request, ref number);
        Transmission t = TransmissionBuilder.Build(speaker, phrase, number);
        if (t == null)
            return true;
        t.Alerts.AddRange(AlertLink.TakeFor(request.Speaker));
        Player.Submit(t);
        return true;
    }

    /// <summary>
    /// Say what is actually going to happen instead of the stock acknowledgement:
    /// <list type="bullet">
    /// <item>An attack order stacked behind earlier ones: "copy, queued, number three in line" (the stock line is
    /// played before the order joins the queue, so the queue holds the earlier ones).</item>
    /// <item>A VID order to a wingman flying in formation: "unable, holding formation on lead". The aircraft state
    /// machine only starts a visual ID from its default state, never from formation flight, so the order would sit
    /// there unnoticed (docs/recon.md section 6). With "Detach wingman for VID" on, the wingman is released from the
    /// formation and the VID goes ahead instead.</item>
    /// </list>
    /// </summary>
    private static string Refine(string phrase, StockVoiceRequest request, ref int? number)
    {
        ObjectBase unit = request.Speaker;
        if (unit == null || !Settings.WhyNot.Value)
            return phrase;

        if (phrase == "EngageTrack" && !(unit is Aircraft))
        {
            int ahead = unit._currentEngageTasks.Count(t => !t._isAutoAttack);
            string queued = "Queued_" + Math.Min(ahead + 1, 5);
            if (ahead >= 1 && Catalog.CanSay(queued, RosterService.Current?.Find(unit)?.Voice?.Id))
            {
                number = null;
                return queued;
            }
        }

        if (phrase == "Identify" && unit is Aircraft aircraft && aircraft.InAirFormation())
        {
            if (Settings.DetachForVid.Value && aircraft.Formation != null)
            {
                aircraft.Formation.MakeUnitActIndependently(aircraft);
                ModLog.Info($"[why-not] {unit.getName()}: detached from formation for VID");
                return phrase;
            }
            string text = "${Name}: " + WhyNot.WhyNotMonitor.AlertText("WhyNot_HoldingFormation");
            ModLog.Info("[why-not] " + text.Replace("${Name}", unit.getName()));
            try { Globals._mainGameViewModel?.RaiseAlert(unit, text); }
            catch (Exception) { /* the alert is a nice-to-have */ }
            number = null;
            return "WhyNot_HoldingFormation";
        }
        return phrase;
    }
}
