using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Core;
using SeaPowerRadio.Roster;
using SeaPowerRadio.Voices;
using UnityEngine;
using Variant = SeaPowerRadio.Radio.ClipRules.Variant;

namespace SeaPowerRadio.Radio;

/// <summary>
/// What the router carries from one game call to the next. The game hands a voice line over with no id that ties it
/// to the call it belongs to, so the order in which the game calls stands in for one. Each rule below holds in the
/// game today and nothing enforces it; this is the one place that relies on them:
/// - The pieces the game appends to a call (VoiceMessage.AppendMessage) follow that call at once, with no other call
///   in between: whether the call was left to the game decides for its pieces (<see cref="PassAppended"/>).
/// - WeaponBase.LaunchVoiceover asks for its launch line right after our prefix saw the weapon, in the same frame: a
///   weapon noted in another frame belongs to another line (<see cref="LaunchIn"/>).
/// - A message bottom right and the call that speaks it are raised in the same frame, by the game and by
///   the why-not feature alike (<see cref="AlertLink.TakeFor"/>).
/// - The why-not checks of the Air and WhyNot folders run in the same game call, before the line reaches the router.
/// </summary>
internal static class CallOrder
{
    /// <summary>True while the pieces the game appends belong to a line that was left to the game: they play too.</summary>
    public static bool PassAppended;

    private static WeaponBase launching;
    private static int launchingFrame = -1;

    public static void NoteLaunch(WeaponBase weapon)
    {
        launching = weapon;
        launchingFrame = Time.frameCount;
    }

    /// <summary>The weapon whose launch the game announces in <paramref name="frame"/>, if our prefix saw it.</summary>
    public static WeaponBase LaunchIn(int frame) => frame == launchingFrame ? launching : null;
}

/// <summary>
/// Decides, for every voice line the game asks for, whether the mod speaks it instead. Called synchronously from
/// the VoiceMessage prefix; returning true mutes the stock line.
/// - Known key with a phrase -> the speaking unit's own voice and callsign say it.
/// - Known key without a phrase (contact report building blocks...) -> muted; the mod reports contacts itself.
/// - Pieces the game appends to a call it already made ("...track 7034", "salvo") -> muted with the call.
/// - Unknown key (another mod's lines) -> the stock system plays it as before.
/// </summary>
internal static class StockVoiceRouter
{
    public static RadioPlayer Player;

    // The CIWS appends its kill report to the sequence it is in (WeaponSystemCIWS): a line of its own, not a piece.
    private const string AppendedOwnLine = "SplashedTrack";

    public static bool Handle(StockVoiceRequest request)
    {
        if (!Settings.ReplaceStockVoices.Value || Player == null)
            return false;
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return false; // menus, editor: no mission roster to speak with

        // Whose line is it, and which pack speaks for that unit's country? No pack: the game's own voice plays.
        RosterElement speaker = request.Speaker != null ? roster.Resolve(request.Speaker) : roster.CommandVoice();
        if (speaker != null && speaker.Voice == null)
            return StockPlays(request);
        Catalog catalog = speaker != null ? speaker.Voice.Pack.Catalog : VoicePacks.StockCatalog;
        return catalog != null && Route(request, speaker, catalog);
    }

    private static bool Route(StockVoiceRequest request, RosterElement speaker, Catalog catalog)
    {
        if (!catalog.TryStock(request.Key, out string phrase, out string numberKind))
            // Unknown key. A piece appended to a task force report is one of the game's contact building blocks
            // built at runtime - swallow it with the rest of that report. Anything else (other mods' lines) plays.
            return request.Appended && request.Speaker == null && !CallOrder.PassAppended;
        if (request.Appended && request.Key != AppendedOwnLine)
            return !CallOrder.PassAppended;
        if (phrase == null)
        {
            CallOrder.PassAppended = false;
            return true;
        }
        if (speaker == null)
            return true; // an enemy or neutral unit "talking": the player's net doesn't hear it

        int? number = numberKind != null && request.Number >= 0 ? request.Number : (int?)null;
        int variant = 0;
        phrase = Refine(phrase, request, speaker.Voice.FullId, ref number, ref variant);
        Transmission t = TransmissionBuilder.Build(speaker, phrase, number, variant);
        if (t == null)
            return StockPlays(request); // the pack has no clip for this line: the game's own line plays
        CallOrder.PassAppended = false;
        t.Alerts.AddRange(AlertLink.TakeFor(request.Speaker));
        Player.Submit(t);
        return true;
    }

    /// <summary>The game's own line plays (Game voice, or a line the pack lacks), together with what the game appends to it.</summary>
    private static bool StockPlays(StockVoiceRequest request)
    {
        if (!request.Appended)
            CallOrder.PassAppended = true;
        return false;
    }

    /// <summary>
    /// The speed slider (ships, submarines, aircraft) sets knots and only raises a message - the game has no voice
    /// line for it. Called after that message (Patches/SpeedOrderPatches), so the call takes it along.
    /// </summary>
    public static void SpeedInKnots(ObjectBase unit, float knots)
    {
        if (!Settings.ReplaceStockVoices.Value || Player == null || unit == null || knots < 0f)
            return;
        RosterElement speaker = RosterService.Current?.Resolve(unit);
        if (speaker == null || speaker.Voice == null)
            return;      // Game voice: the game has no line for the knots slider, so nothing is said
        int value = Mathf.RoundToInt(knots);
        Transmission t = value == 0
            ? TransmissionBuilder.Build(speaker, "AllStop", null)
            : TransmissionBuilder.Build(speaker, "SetSpeedKnots", value);
        if (t == null)
            return;
        t.Alerts.AddRange(AlertLink.TakeFor(unit));
        Player.Submit(t);
    }

    private const float FeetPerUnit = 220.47266f; // the game's own conversion (ObjectAltitude)
    private const float LevelFeet = 100f;         // closer than this to the new altitude: "leveling off"

    /// <summary>
    /// Say what is actually going to happen instead of the stock acknowledgement:
    /// - A speed or altitude order: which way it goes (<see cref="SpeedPhrase"/>, <see cref="AltitudePhrase"/>).
    /// - A launch: the line that fits the weapon (<see cref="LaunchPhrase"/>).
    /// - The replies of the why-not feature (<see cref="WhyNotReply"/>).
    /// </summary>
    private static string Refine(string phrase, StockVoiceRequest request, string voice, ref int? number, ref int variant)
    {
        ObjectBase unit = request.Speaker;
        if (unit == null)
            return phrase;
        switch (request.Key)
        {
            case "SetAltitude":
                return AltitudePhrase(phrase, unit, voice, ref variant);
            case "ASHMFired":
            case "ASUMFired":
            case "WeaponAway":
                return LaunchPhrase(phrase, request, ref variant);
        }
        if (request.Key.StartsWith("SetSpeed_Ahead_", StringComparison.Ordinal))
            return SpeedPhrase(phrase, request.Key, unit, voice, ref variant);
        if (!Settings.WhyNot.Value)
            return phrase;
        string reply = WhyNotReply(phrase, unit, voice);
        if (reply == null)
            return phrase;
        if (reply != phrase)
            number = null;
        return reply;
    }

    /// <summary>
    /// The lines the why-not feature adds to a stock acknowledgement; null = none:
    /// - An attack order stacked behind earlier ones: "copy, queued, number three in line" (the stock line is played
    /// before the order joins the queue, so the queue holds the earlier ones).
    /// - A VID order to a wingman flying in formation: "unable, holding formation on lead". The aircraft state machine
    /// only starts a visual ID from its default state, never from formation flight, so the order would sit there
    /// unnoticed (docs/how-it-works.md, "VID acknowledged, wingman stays in formation"). With "Detach wingman for VID" on,
    /// the wingman is released from the formation and the VID goes ahead instead; else the "holding formation" message is raised.
    /// </summary>
    private static string WhyNotReply(string phrase, ObjectBase unit, string voice)
    {
        if (phrase == "EngageTrack" && !(unit is Aircraft))
        {
            int ahead = unit._currentEngageTasks.Count(t => !t._isAutoAttack);
            string queued = "Queued_" + Math.Min(ahead + 1, ClipRules.QueuedMax);
            return ahead >= 1 && CanSay(voice, queued) ? queued : null;
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
            return "WhyNot_HoldingFormation";
        }
        return null;
    }

    private static bool CanSay(string voice, string phrase, int only = 0) =>
        Catalog.Of(voice)?.CanSay(phrase, voice, haveNumber: only != 0, only: only) ?? false;

    /// <summary>
    /// The stock telegraph lines name the new setting ("ahead two thirds"); ours say both the way the speed goes and
    /// the step ("coming up to two thirds", "slowing to one third"), so the direction comes from the setting before.
    /// Picked in the menu, the line is asked for before the new setting is applied (<c>getTelegraph()</c> is still
    /// the old one); by keyboard, after it (the old one is in <c>_previousTelegraphSetting</c>). Without this,
    /// 1/3 -> 2/3 said "slowing down". A voice without the step lines says the direction only.
    /// </summary>
    private static string SpeedPhrase(string phrase, string key, ObjectBase unit, string voice, ref int variant)
    {
        if (!Enum.TryParse(key.Substring("SetSpeed_".Length), out ObjectSpeed.SpeedSettings setting))
            return phrase;
        int ordered = (int)setting;
        int current = unit.getTelegraph();
        int before = current != ordered ? current : unit._previousTelegraphSetting;
        if (before == ordered)
            return "Confirmed"; // the setting it already has
        bool faster = ordered > before;
        switch (setting)
        {
            case ObjectSpeed.SpeedSettings.Ahead_Flank:
                return phrase;
            case ObjectSpeed.SpeedSettings.Ahead_Full:
                variant = Variant.SetSpeedFull_AheadFull; // names the step, up or down
                return "SetSpeedFull";
            default: // 1/3, 2/3, standard
                string step = setting == ObjectSpeed.SpeedSettings.Ahead_1_3 ? "OneThird"
                    : setting == ObjectSpeed.SpeedSettings.Ahead_2_3 ? "TwoThirds" : "Standard";
                string named = (faster ? "SpeedUp" : "SpeedDown") + step;
                if (CanSay(voice, named))
                    return named;
                if (!faster)
                    return "SetSpeedSlow";
                variant = Variant.SetSpeedFull_IncreasingSpeed;
                return "SetSpeedFull";
        }
    }

    /// <summary>
    /// "SetAltitude" comes once the new altitude is set (<c>DesiredAltitude</c>: preset menu, slider, helicopter
    /// keys), so the direction is where the aircraft is now against where it is told to go: "climbing, angels
    /// twenty" below it, "descending, angels five" above it, "leveling off, ..." at the altitude it already holds (or
    /// on the way down in a voice without the descending line). Picked at random, a descent said "climbing" half the
    /// time.
    /// </summary>
    private static string AltitudePhrase(string phrase, ObjectBase unit, string voice, ref int variant)
    {
        float climbFeet = (unit.DesiredAltitude.Value - unit.transform.position.y) * FeetPerUnit;
        variant = climbFeet > LevelFeet ? Variant.SetAltitude_Climbing
            : climbFeet < -LevelFeet && CanSay(voice, phrase, Variant.SetAltitude_Descending) ? Variant.SetAltitude_Descending
            : Variant.SetAltitude_LevelingOff;
        return phrase;
    }

    /// <summary>
    /// <c>WeaponBase.LaunchVoiceover</c> picks a ship's or submarine's launch line from the target alone: a missile
    /// at a land unit is "ASUMFired", at a ship or a position "ASHMFired", anything else (torpedoes, ASROC)
    /// "WeaponAway". Our lines name the weapon, so they follow the weapon instead: a Tomahawk at a land target said
    /// "torpedo away", at a position "harpoon away".
    /// </summary>
    private static string LaunchPhrase(string phrase, StockVoiceRequest request, ref int variant)
    {
        WeaponBase w = CallOrder.LaunchIn(request.Frame);
        if (w == null || w._launchPlatform != request.Speaker || request.Speaker.IsAirUnit)
            return phrase;
        AmmunitionParameters ap = w._ap;
        if (ap == null)
            return phrase;
        if (ap._type == Ammunition.Type.Torpedo && ap._subType != Ammunition.Type.MOSS
            || ap._submunitionType == Ammunition.Type.Torpedo)
            return "AsumAway"; // a torpedo, or a missile carrying one (ASROC, Ikara, SS-N-14): "torpedo away"
        if (ap._type != Ammunition.Type.Missile)
            return phrase;
        if (IsHarpoon(ap))
            return "AshmAway";
        if (w.CurrentIntendedTargetObject is Vessel || w.CurrentIntendedTargetObject is Submarine)
        {
            variant = Variant.AshmAway_AntiShip;
            return "AshmAway";
        }
        return "SamAway"; // land attack, a position: "birds away", "missile away"
    }

    // RGM/UGM-84: the game has no weapon family field, only the ammunition file name.
    private static bool IsHarpoon(AmmunitionParameters ap) =>
        ap._ammunitionFileName?.IndexOf("gm-84", StringComparison.OrdinalIgnoreCase) >= 0;
}
