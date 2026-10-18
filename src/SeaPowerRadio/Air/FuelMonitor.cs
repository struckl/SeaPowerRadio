using System;
using System.Collections.Generic;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;
using UnityEngine;

namespace SeaPowerRadio.Air;

/// <summary>
/// Fuel calls against the nearest place the aircraft can land – its home base or any friendly deck or airfield
/// that would take it (ObjectBase.CanAcceptAircraft, the game's own rebase check):
/// <list type="bullet">
/// <item><b>Joker</b> – at cruise speed only just enough fuel left to get there: time to think about going home.</item>
/// <item><b>Bingo</b> – only just enough even at the most economical speed: it has to go now.</item>
/// <item><b>Fuel emergency</b> – not enough at all, even flying economy: tanker or it goes in the water.</item>
/// </list>
/// The game itself only knows "bingo" against the home base, and acts on it (the aircraft turns home); these calls
/// only report. Fuel model as in the game: <c>RangeInKm</c> is the fuel left, in km at best efficiency; a km flown
/// costs the consumption coefficient of the speed and altitude flown (Aircraft.UpdateFuelConsumption,
/// Helicopter.UpdateFuelConsumption).
/// </summary>
internal sealed class FuelMonitor
{
    private enum Level { None, Joker, Bingo, Emergency }

    private sealed class State
    {
        public Level Said;
        public float LastFuel;
    }

    private const float ScanInterval = 2f;
    private const int CruiseSpeedIndex = 2;        // "medium" – what the game uses for "can it reach the tanker"
    private const float JokerMargin = 1.10f, JokerReserveKm = 10f;
    private const float BingoMargin = 1.05f, BingoReserveKm = 5f;
    private const float KmPerMapUnit = 67.200066f / 1000f;

    private readonly RadioPlayer player;
    private readonly Dictionary<int, State> states = new Dictionary<int, State>();
    private float nextScan;

    public FuelMonitor(RadioPlayer player) => this.player = player;

    public void Clear() => states.Clear();

    public void Tick()
    {
        if (!Settings.FuelCalls.Value || Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + ScanInterval;
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return;
        foreach (RosterElement e in roster.Elements)
        {
            ObjectBase unit = e.Unit;
            if (unit == null || unit.IsDestroyed || !unit.IsPlayerUnit || !(unit is Aircraft || unit is Helicopter))
                continue;
            try { Scan(e, unit); }
            catch (Exception ex) { ModLog.Debug($"[fuel] {unit.getName()}: {ex.Message}"); }
        }
    }

    private void Scan(RosterElement e, ObjectBase unit)
    {
        int id = unit.GetInstanceID();
        bool inFlight = unit is Aircraft aircraft ? aircraft._isInFlight : ((Helicopter)unit)._isInFlight;
        if (!inFlight || unit._unlimitedFuel)
        {
            states.Remove(id);                      // on deck: the next sortie starts fresh
            return;
        }
        if (!Burn(unit, out float cruise, out float economy))
            return;
        (ObjectBase deck, float km) = NearestDeck(unit);
        if (deck == null)
            return;

        float fuel = unit.RangeInKm.Value;
        Level level = fuel < km * economy ? Level.Emergency
            : fuel < km * economy * BingoMargin + BingoReserveKm ? Level.Bingo
            : fuel < km * cruise * JokerMargin + JokerReserveKm ? Level.Joker
            : Level.None;

        if (!states.TryGetValue(id, out State s))
            states[id] = s = new State { LastFuel = fuel };
        if (fuel > s.LastFuel + 1f && level < s.Said)
            s.Said = level;                         // refuelled in the air: calls can come again
        s.LastFuel = fuel;

        string stateName = unit.StateMachine?.CurrentStateName;
        if (stateName == "BingoFuel" && s.Said < Level.Bingo)
            s.Said = Level.Bingo;                   // the game has called bingo itself and is taking it home
        else if (RtbMarker.IsRtb(unit) && s.Said < Level.Joker)
            s.Said = Level.Joker;                   // already on its way home: no joker call

        if (level <= s.Said)
            return;
        s.Said = level;
        Announce(e, unit, level, deck, km, fuel, level == Level.Joker ? cruise : economy);
    }

    private void Announce(RosterElement e, ObjectBase unit, Level level, ObjectBase deck, float km, float fuel, float burn)
    {
        float nm = km / 1.852f;
        float rangeNm = fuel / Math.Max(1f, burn) / 1.852f;
        string phrase, text;
        switch (level)
        {
            case Level.Joker:
                phrase = "FuelJoker";
                text = $"${{Name}}: Joker fuel - ${{Target}} is {nm:0} nm, range at cruise {rangeNm:0} nm";
                break;
            case Level.Bingo:
                phrase = "FuelBingo";
                text = $"${{Name}}: BINGO fuel - ${{Target}} is {nm:0} nm, range at best economy {rangeNm:0} nm. Send it home now";
                break;
            default:
                phrase = "FuelEmergency";
                text = $"${{Name}}: FUEL EMERGENCY - ${{Target}} is {nm:0} nm, range only {rangeNm:0} nm. Needs a tanker";
                break;
        }
        ModLog.Info($"[fuel] {text.Replace("${Name}", unit.getName()).Replace("${Target}", deck.getName())}");
        try { Globals._mainGameViewModel?.RaiseAlert(unit, deck, text); }
        catch (Exception ex) { ModLog.Debug($"[fuel] alert failed: {ex.Message}"); }

        List<Alert> alerts = AlertLink.TakeFor(unit);
        Transmission t = TransmissionBuilder.Build(e, phrase, null);
        if (t == null)
            return;                                 // not rendered for this voice yet: the message has to do
        t.Alerts.AddRange(alerts);
        player.Submit(t);
    }

    /// <summary>Fuel per km flown (in the game's "km at best efficiency"): at cruise and at the cheapest speed.</summary>
    private static bool Burn(ObjectBase unit, out float cruise, out float economy)
    {
        cruise = economy = 0f;
        if (unit is Aircraft a)
        {
            AircraftParameters ap = a.Ap;
            if (ap?._speedValuesInMach == null || ap._speedValuesInMach.Length == 0 || ap._emptyMass <= 0f)
                return false;
            float mass = 1f + ap._loadoutMass / ap._emptyMass;
            float altitude = Mathf.Max(0.05f, ap._altitudeEfficiencyCurve.Evaluate(a.getHeightInFeet()));
            float atSpeed(int i, float alt) => ap._rangeMultiplierCurve.Evaluate(ap._speedValuesInMach[i]) / alt * mass;
            cruise = Mathf.Max(1f, atSpeed(Math.Min(CruiseSpeedIndex, ap._speedValuesInMach.Length - 1), altitude));
            // Economy may climb to cruise altitude on the way (the curve is 1 there), never worse than here.
            float best = float.MaxValue;
            for (int i = 0; i < ap._speedValuesInMach.Length; i++)
                best = Math.Min(best, atSpeed(i, Math.Max(altitude, 1f)));
            economy = Mathf.Max(1f, best);
            return true;
        }
        if (unit is Helicopter h)
        {
            HelicopterParameters hp = h._hp;
            if (hp == null || hp._emptyMass <= 0f)
                return false;
            float mass = 1f + hp._loadoutMass / hp._emptyMass;
            float hover = Mathf.Clamp01(hp._hoveringFuelConsumption);
            // Per km: mass * (hover * vmax / v + 1 - hover) – flat out is the cheapest, the hover burn is per hour.
            economy = mass;
            cruise = mass * (hover / 0.7f + 1f - hover);
            return true;
        }
        return false;
    }

    /// <summary>The closest place this aircraft can land: its home base, or any friendly deck/field that takes it.</summary>
    private static (ObjectBase deck, float km) NearestDeck(ObjectBase unit)
    {
        ObjectBase best = null;
        float bestKm = float.MaxValue;
        void Consider(ObjectBase site)
        {
            float km = (float)unit.Position.Value.GetDistance(site.Position.Value) * KmPerMapUnit;
            if (km < bestKm)
            {
                best = site;
                bestKm = km;
            }
        }

        ObjectBase home = unit._homeBase;
        if (home != null && !home.IsDestroyed && home._obp?._flightDeck != null && !home._obp._flightDeck.Inoperable.Value)
            Consider(home);
        Taskforce side = unit._taskforce;
        if (side?._taskforceUnits != null)
        {
            foreach (ObjectBase site in side._taskforceUnits)
            {
                if (site == null || site == home || site == unit || site.IsDestroyed || !site._hasControl)
                    continue;
                if (site.CanAcceptAircraft(unit, message: false, checkDistance: false))
                    Consider(site);
            }
        }
        return (best, bestKm);
    }
}
