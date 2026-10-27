using System;
using System.Linq;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Core;
using SeaPowerRadio.Roster;
using UnityEngine;

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
        int variant = 0;
        phrase = Refine(phrase, request, speaker.Voice?.Id, ref number, ref variant);
        Transmission t = TransmissionBuilder.Build(speaker, phrase, number, variant);
        if (t == null)
            return true;
        t.Alerts.AddRange(AlertLink.TakeFor(request.Speaker));
        Player.Submit(t);
        return true;
    }

    /// <summary>
    /// The speed slider (ships, submarines, aircraft) sets knots and only raises a message – the game has no voice
    /// line for it. Called after that message (Patches/SpeedOrderPatches), so the call takes it along.
    /// </summary>
    public static void SpeedInKnots(ObjectBase unit, float knots)
    {
        if (!Settings.ReplaceStockVoices.Value || !Catalog.Loaded || Player == null || unit == null || knots < 0f)
            return;
        RosterElement speaker = RosterService.Current?.Resolve(unit);
        if (speaker == null)
            return;
        int value = Mathf.RoundToInt(knots);
        Transmission t = value == 0
            ? TransmissionBuilder.Build(speaker, "AllStop", null)
            : TransmissionBuilder.Build(speaker, "SetSpeedKnots", value);
        if (t == null)
            return;
        t.Alerts.AddRange(AlertLink.TakeFor(unit));
        Player.Submit(t);
    }

    // Variants of tools/voicegen/phrases.py that are only said where they fit.
    private const int AheadFull = 1;        // SetSpeedFull: "ahead full."
    private const int IncreasingSpeed = 2;  // SetSpeedFull: "increasing speed."
    private const int AntiShip = 2;         // AshmAway: "anti-ship away!" (1 = "harpoon away!")
    private const int Climbing = 1;         // SetAltitude: "climbing, {alt}."
    private const int LevelingOff = 2;      // SetAltitude: "leveling off, {alt}."
    private const int Descending = 3;       // SetAltitude: "descending, {alt}."

    private const float FeetPerUnit = 220.47266f; // the game's own conversion (ObjectAltitude)

    private static WeaponBase launching;
    private static int launchingFrame = -1;

    /// <summary>The weapon whose launch the game is about to announce (Patches/LaunchVoicePatches).</summary>
    public static void NoteLaunch(WeaponBase weapon)
    {
        launching = weapon;
        launchingFrame = UnityEngine.Time.frameCount;
    }

    /// <summary>
    /// Say what is actually going to happen instead of the stock acknowledgement:
    /// <list type="bullet">
    /// <item>A speed or altitude order: which way it goes (<see cref="SpeedPhrase"/>, <see cref="AltitudePhrase"/>).</item>
    /// <item>A launch: the line that fits the weapon (<see cref="LaunchPhrase"/>).</item>
    /// <item>An attack order stacked behind earlier ones: "copy, queued, number three in line" (the stock line is
    /// played before the order joins the queue, so the queue holds the earlier ones).</item>
    /// <item>A VID order to a wingman flying in formation: "unable, holding formation on lead". The aircraft state
    /// machine only starts a visual ID from its default state, never from formation flight, so the order would sit
    /// there unnoticed (docs/recon.md section 6). With "Detach wingman for VID" on, the wingman is released from the
    /// formation and the VID goes ahead instead.</item>
    /// </list>
    /// </summary>
    private static string Refine(string phrase, StockVoiceRequest request, string voice, ref int? number, ref int variant)
    {
        ObjectBase unit = request.Speaker;
        if (unit == null)
            return phrase;
        if (request.Key.StartsWith("SetSpeed_Ahead_", StringComparison.Ordinal))
            return SpeedPhrase(phrase, request.Key, unit, voice, ref variant);
        if (request.Key == "SetAltitude")
            return AltitudePhrase(phrase, unit, voice, ref variant);
        if (request.Key == "ASHMFired" || request.Key == "ASUMFired" || request.Key == "WeaponAway")
            return LaunchPhrase(phrase, request, ref variant);
        if (!Settings.WhyNot.Value)
            return phrase;

        if (phrase == "EngageTrack" && !(unit is Aircraft))
        {
            int ahead = unit._currentEngageTasks.Count(t => !t._isAutoAttack);
            string queued = "Queued_" + Math.Min(ahead + 1, 5);
            if (ahead >= 1 && Catalog.CanSay(queued, voice))
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

    /// <summary>
    /// The stock telegraph lines name the new setting ("ahead two thirds"); ours say both the way the speed goes and
    /// the step ("coming up to two thirds", "slowing to one third"), so the direction comes from the setting before.
    /// Picked in the menu, the line is asked for before the new setting is applied (<c>getTelegraph()</c> is still
    /// the old one); by keyboard, after it (the old one is in <c>_previousTelegraphSetting</c>). Without this,
    /// 1/3 → 2/3 said "slowing down". A voice without the step lines says the direction only.
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
                variant = AheadFull; // names the step, up or down
                return "SetSpeedFull";
            default: // 1/3, 2/3, standard
                string step = setting == ObjectSpeed.SpeedSettings.Ahead_1_3 ? "OneThird"
                    : setting == ObjectSpeed.SpeedSettings.Ahead_2_3 ? "TwoThirds" : "Standard";
                string named = (faster ? "SpeedUp" : "SpeedDown") + step;
                if (Catalog.CanSay(named, voice))
                    return named;
                if (!faster)
                    return "SetSpeedSlow";
                variant = IncreasingSpeed;
                return "SetSpeedFull";
        }
    }

    /// <summary>
    /// "SetAltitude" comes once the new altitude is set (<c>DesiredAltitude</c>: preset menu, slider, helicopter
    /// keys), so the direction is where the aircraft is now against where it is told to go: "climbing, angels
    /// twenty" below it, "descending, angels five" above it, "leveling off, …" at the altitude it already holds (or
    /// on the way down in a voice without the descending line). Picked at random, a descent said "climbing" half the
    /// time.
    /// </summary>
    private static string AltitudePhrase(string phrase, ObjectBase unit, string voice, ref int variant)
    {
        float climbFeet = (unit.DesiredAltitude.Value - unit.transform.position.y) * FeetPerUnit;
        variant = climbFeet > 100f ? Climbing
            : climbFeet < -100f && Catalog.CanSay(phrase, voice, haveNumber: true, only: Descending) ? Descending
            : LevelingOff;
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
        WeaponBase w = launching;
        if (w == null || launchingFrame != request.Frame || w._launchPlatform != request.Speaker || request.Speaker.IsAirUnit)
            return phrase;
        AmmunitionParameters ap = w._ap;
        if (ap == null)
            return phrase;
        if (ap._type == Ammunition.Type.Torpedo && ap._subType != Ammunition.Type.MOSS
            || ap._submunitionType == Ammunition.Type.Torpedo)
            return "AsumAway"; // a torpedo, or a missile carrying one (ASROC, Ikara, SS-N-14): "torpedo away"
        if (ap._type != Ammunition.Type.Missile)
            return phrase;
        if (ap._ammunitionFileName?.IndexOf("gm-84", StringComparison.OrdinalIgnoreCase) >= 0)
            return "AshmAway"; // RGM/UGM-84 Harpoon
        if (w.CurrentIntendedTargetObject is Vessel || w.CurrentIntendedTargetObject is Submarine)
        {
            variant = AntiShip;
            return "AshmAway";
        }
        return "SamAway"; // land attack, a position: "birds away", "missile away"
    }
}
