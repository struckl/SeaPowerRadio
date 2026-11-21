using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;
using UnityEngine;

namespace SeaPowerRadio.Air;

/// <summary>
/// Fuel calls against the nearest place the aircraft can land - its home base or any friendly deck or airfield
/// that would take it (ObjectBase.CanAcceptAircraft, the game's own rebase check):
/// - Joker - at cruise speed only just enough fuel left to get there: time to think about going home.
/// - Bingo - only just enough even at the most economical speed: it has to go now.
/// - Fuel emergency - not enough at all, even flying economy: tanker or it goes in the water.
/// The game itself only knows "bingo" against the home base: once fuel only just covers the way home at best
/// efficiency it turns the aircraft home (BingoFuel state) and says no more than "returning to base". That moment
/// is a bingo call too - the game can get there first when another deck is nearer than home, or at high time
/// compression between two scans. Aircraft nothing in the mission can take have no home base: the game drops a
/// mission's HomeBase when that field's air group doesn't carry the type (SceneCreator.RebaseAircraft), never sends
/// them home and lets them fall out of the sky when the tank runs dry. They call all the same, against the nearest
/// friendly field or deck they would call home - where the player sees the range circle stop reaching - and by
/// the share of a full tank when there is none.
/// Fuel model as in the game: <c>RangeInKm</c> is the fuel left, in km at best efficiency; a km flown costs the
/// consumption coefficient of the speed and altitude flown (Aircraft.UpdateFuelConsumption,
/// Helicopter.UpdateFuelConsumption).
/// </summary>
internal sealed class FuelMonitor
{
    private enum Level { None, Joker, Bingo, Emergency }

    private sealed class State
    {
        public Level Said;
        public float LastFuel;
        public float NextTrace;
    }

    private const float ScanInterval = 0.5f;       // real seconds: at high time compression a lot happens in between
    private const float TraceInterval = 30f;       // verbose log: each aircraft's sums this often
    private const int CruiseSpeedIndex = 2;        // "Cruise" - what the game uses for "can it reach the tanker"
    private const float JokerMargin = 1.10f, JokerReserveKm = 10f;
    private const float BingoMargin = 1.05f, BingoReserveKm = 5f;
    // Nowhere to land: share of a full tank.
    private const float NoDeckJoker = 0.25f, NoDeckBingo = 0.15f, NoDeckEmergency = 0.05f;
    private const float RefuelKm = 1f;             // fuel up by more than this between scans = refuelled in the air
    private const float HeliCruiseShare = 0.7f;    // helicopter cruise speed as a share of its top speed

    private readonly RadioPlayer player;
    private readonly Dictionary<int, State> states = new Dictionary<int, State>();
    private readonly HashSet<int> failed = new HashSet<int>();
    private float nextScan;

    public FuelMonitor(RadioPlayer player) => this.player = player;

    public void Clear()
    {
        states.Clear();
        failed.Clear();
    }

    public void Tick()
    {
        if (!Settings.FuelCalls.Value || Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + ScanInterval;
        Roster.Roster roster = RosterService.Current;
        Taskforce side = Globals._playerTaskforce;
        if (roster == null || side?._taskforceUnits == null)
            return;
        // Every player aircraft in the air, not only those in the roster: air support that turns up mid-mission
        // gets its element (and voice) here if nothing has spoken for it yet.
        foreach (ObjectBase unit in side._taskforceUnits.ToList())
        {
            if (unit == null || unit.IsDestroyed || !(unit is Aircraft || unit is Helicopter))
                continue;
            int id = unit.GetInstanceID();
            try
            {
                Scan(roster, unit, id);
            }
            catch (Exception ex)
            {
                if (failed.Add(id))
                    Rig.Log.Warn($"[fuel] {unit.getName()}: no fuel calls, scan failed: {ex}");
            }
        }
    }

    /// <summary>What one scan measured for an aircraft in flight.</summary>
    private sealed class Reading
    {
        public ObjectBase Deck;     // where it can land; with CanLand false the field it would call home; null = none
        public bool CanLand;
        public float Km;            // to Deck
        public float Fuel;          // RangeInKm: fuel left, in km at best efficiency
        public float Full;
        public float Cruise, Economy;   // fuel per km flown (see Burn)
    }

    private void Scan(Roster.Roster roster, ObjectBase unit, int id)
    {
        if (!InFlight(unit))
        {
            states.Remove(id);                      // on deck: the next sortie starts fresh
            return;
        }
        RosterElement speaker = roster.Resolve(unit);
        if (speaker == null)
            return;
        Reading r = Read(unit);
        if (r == null)
        {
            if (failed.Add(id))
                Rig.Log.Warn($"[fuel] {unit.getName()}: no fuel calls, its fuel model can't be read");
            return;
        }
        Level level = LevelOf(r);
        State s = StateFor(unit, id, r);
        if (r.Fuel > s.LastFuel + RefuelKm && level < s.Said)
            s.Said = level;                         // refuelled in the air: calls can come again
        s.LastFuel = r.Fuel;
        level = AgreeWithGame(unit, s, r, level);
        Trace(unit, s, r, level);

        if (level <= s.Said)
            return;
        s.Said = level;
        Announce(speaker, unit, level, r);
    }

    private static bool InFlight(ObjectBase unit)
    {
        bool flying = unit is Aircraft aircraft ? aircraft._isInFlight : ((Helicopter)unit)._isInFlight;
        return flying && !unit._unlimitedFuel;
    }

    private static Reading Read(ObjectBase unit)
    {
        if (!Burn(unit, out float cruise, out float economy))
            return null;
        (ObjectBase deck, float km) = NearestDeck(unit);
        bool canLand = deck != null;
        if (!canLand)
            (deck, km) = NearestField(unit);
        return new Reading
        {
            Deck = deck, CanLand = canLand, Km = km,
            Fuel = unit.RangeInKm.Value, Full = Math.Max(1f, unit._obp._maxRangeInKm),
            Cruise = cruise, Economy = economy,
        };
    }

    private static float JokerKm(Reading r) => r.Km * r.Cruise * JokerMargin + JokerReserveKm;

    private static float BingoKm(Reading r) => r.Km * r.Economy * BingoMargin + BingoReserveKm;

    private static Level LevelOf(Reading r)
    {
        if (r.Deck != null)
            return r.Fuel < r.Km * r.Economy ? Level.Emergency
                : r.Fuel < BingoKm(r) ? Level.Bingo
                : r.Fuel < JokerKm(r) ? Level.Joker
                : Level.None;
        return r.Fuel < r.Full * NoDeckEmergency ? Level.Emergency
            : r.Fuel < r.Full * NoDeckBingo ? Level.Bingo
            : r.Fuel < r.Full * NoDeckJoker ? Level.Joker
            : Level.None;
    }

    /// <summary>The aircraft's state for this sortie; a new sortie is described in the log once.</summary>
    private State StateFor(ObjectBase unit, int id, Reading r)
    {
        if (states.TryGetValue(id, out State s))
            return s;
        states[id] = s = new State { LastFuel = r.Fuel };
        Describe(unit, r);
        if (unit._homeBase == null)
        {
            try { LandingCheck.Log(unit); }
            catch (Exception ex) { Rig.Log.Warn($"[fuel] {unit.getName()}: landing check failed: {ex.Message}"); }
        }
        return s;
    }

    /// <summary>
    /// The game's own view wins: gone bingo before us and taking it home = the bingo call, against home; already on
    /// its way home = no joker call.
    /// </summary>
    private static Level AgreeWithGame(ObjectBase unit, State s, Reading r, Level level)
    {
        if (unit.StateMachine?.CurrentStateName == "BingoFuel" && level < Level.Bingo && unit._homeBase != null)
        {
            r.Deck = unit._homeBase;
            r.CanLand = true;
            r.Km = Km(unit, r.Deck);
            return Level.Bingo;
        }
        if (RtbMarker.IsRtb(unit) && s.Said < Level.Joker)
            s.Said = Level.Joker;
        return level;
    }

    private static void Trace(ObjectBase unit, State s, Reading r, Level level)
    {
        if (!Rig.Settings.Verbose || Time.unscaledTime < s.NextTrace)
            return;
        s.NextTrace = Time.unscaledTime + TraceInterval;
        Rig.Log.Debug($"[fuel] {unit.getName()}: {r.Fuel:0} km fuel, {(r.Deck != null ? $"{r.Deck.getName()} {r.Km:0} km" : "no deck")}, " +
                     $"level {level}, said {s.Said}, state {unit.StateMachine?.CurrentStateName}");
    }

    /// <summary>Once per sortie: what the calls will be measured against, so the log shows the monitor is on it.</summary>
    private static void Describe(ObjectBase unit, Reading r)
    {
        string name = unit.getName();
        if (r.Deck == null)
        {
            Rig.Log.Info($"[fuel] watching {name}: {r.Fuel:0} km of fuel ({r.Fuel / r.Full:P0}), nothing in the mission can take it " +
                        $"- joker / bingo / emergency at {NoDeckJoker:P0} / {NoDeckBingo:P0} / {NoDeckEmergency:P0} of a full tank");
            return;
        }
        string where = r.CanLand ? "" : " (can't land there - nothing in the mission takes it, the game won't send it home)";
        Rig.Log.Info($"[fuel] watching {name}: {r.Fuel:0} km of fuel, {r.Deck.getName()} {r.Km:0} km away{where} " +
                    $"- joker below {JokerKm(r):0} km, bingo below {BingoKm(r):0} km (moves with the distance)");
    }

    private void Announce(RosterElement speaker, ObjectBase unit, Level level, Reading r)
    {
        float burn = level == Level.Joker ? r.Cruise : r.Economy;
        float rangeNm = r.Fuel / Math.Max(1f, burn) / Distance.KmPerNm;
        string phrase = level == Level.Joker ? "FuelJoker" : level == Level.Bingo ? "FuelBingo" : "FuelEmergency";
        string text = r.Deck != null ? DeckText(level, r, rangeNm) : TankText(level, r, rangeNm);
        GameMessage.RaiseLogged("fuel", unit, r.Deck, text);
        List<Alert> alerts = AlertLink.TakeFor(unit);
        SpokenReport.Say(player, speaker, phrase, null, alerts);    // no clip for this voice yet: the message has to do
    }

    private static string DeckText(Level level, Reading r, float rangeNm)
    {
        float nm = r.Km / Distance.KmPerNm;
        string where = r.CanLand ? "" : " (can't land there)";
        switch (level)
        {
            case Level.Joker:
                return $"${{Name}}: Joker fuel - ${{Target}} is {nm:0} nm{where}, range at cruise {rangeNm:0} nm";
            case Level.Bingo:
                return $"${{Name}}: BINGO fuel - ${{Target}} is {nm:0} nm{where}, range at best economy {rangeNm:0} nm. " +
                       (r.CanLand ? "Send it home now" : "Nothing in the mission takes it, the game won't bring it home");
            default:
                return $"${{Name}}: FUEL EMERGENCY - ${{Target}} is {nm:0} nm{where}, range only {rangeNm:0} nm. Needs a tanker";
        }
    }

    private static string TankText(Level level, Reading r, float rangeNm)
    {
        string left = (r.Fuel / r.Full).ToString("P0");
        switch (level)
        {
            case Level.Joker:
                return $"${{Name}}: Joker fuel - {left} left, range at cruise {rangeNm:0} nm. Nowhere in the area to land";
            case Level.Bingo:
                return $"${{Name}}: BINGO fuel - {left} left, range at best economy {rangeNm:0} nm. Nowhere in the area to land";
            default:
                return $"${{Name}}: FUEL EMERGENCY - {left} left, range only {rangeNm:0} nm. Needs a tanker";
        }
    }

    /// <summary>
    /// Fuel per km flown, in the unit of <c>RangeInKm</c> ("km at best efficiency"): at cruise and at the cheapest speed.
    /// Two game models, both reached by reflection so that one DLL loads and stays quiet on every build (a member missing on
    /// one build must never be named in a method that is JIT-compiled there):
    /// - Builds from 261107: the game keeps <c>_fuelPerKmCruise</c> (best-range speed / cruise step) and
    /// <c>_fuelPerKmNow</c> (at the speed flown), kg per km, and reckons the way home with the cruise rate
    /// (Aircraft/Helicopter.UpdateFuelConsumption). economy = that cruise rate, cruise = the greater of it and the rate at
    /// the speed flown; both go through <c>ObjectBase.CruiseKmForFuel</c> into the units of <c>RangeInKm</c>.
    /// - Older builds (0.8.7, globe 26223125): the altitude and range-multiplier curves, and the hover share of the
    /// helicopter, as before.
    /// If neither model can be read: false, no fuel calls, nothing thrown.
    /// </summary>
    private static bool Burn(ObjectBase unit, out float cruise, out float economy)
    {
        try
        {
            if (BurnPerKm(unit, out cruise, out economy) || BurnCurves(unit, out cruise, out economy))
                return true;
        }
        catch (Exception ex)
        {
            if (!burnErrorLogged)
            {
                burnErrorLogged = true;
                Rig.Log.Warn($"[fuel] fuel rate unreadable, no fuel calls: {ex.Message}");
            }
        }
        cruise = economy = 0f;
        return false;
    }

    private static bool burnErrorLogged;
    private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed class PerKmType
    {
        public bool Ok;
        public FieldInfo Cruise, Now;
        public MethodInfo Refresh;
    }

    private static readonly Dictionary<Type, PerKmType> perKmTypes = new Dictionary<Type, PerKmType>();
    private static MethodInfo cruiseKmForFuel;
    private static bool cruiseKmResolved;

    private static PerKmType PerKmOf(Type t)
    {
        if (perKmTypes.TryGetValue(t, out PerKmType m))
            return m;
        m = new PerKmType
        {
            Cruise = t.GetField("_fuelPerKmCruise", AnyMember),
            Now = t.GetField("_fuelPerKmNow", AnyMember),
            Refresh = t.GetMethod("RefreshFuelRates", AnyMember, null, new[] { typeof(bool) }, null),
        };
        m.Ok = m.Cruise != null && m.Cruise.FieldType == typeof(float) && m.Now != null && m.Now.FieldType == typeof(float);
        perKmTypes[t] = m;
        return m;
    }

    private static bool BurnPerKm(ObjectBase unit, out float cruise, out float economy)
    {
        cruise = economy = 0f;
        PerKmType m = PerKmOf(unit.GetType());
        if (!m.Ok)
            return false;
        if (!cruiseKmResolved)
        {
            cruiseKmResolved = true;
            cruiseKmForFuel = typeof(ObjectBase).GetMethod("CruiseKmForFuel", AnyMember, null, new[] { typeof(float) }, null);
        }
        if (cruiseKmForFuel == null || cruiseKmForFuel.ReturnType != typeof(float))
            return false;
        m.Refresh?.Invoke(unit, new object[] { false });    // the game's own once-a-second refresh of the two fields
        float kgCruise = (float)m.Cruise.GetValue(unit);
        float kgNow = (float)m.Now.GetValue(unit);
        if (!(kgCruise > 0f) || float.IsInfinity(kgCruise))
            return false;
        economy = (float)cruiseKmForFuel.Invoke(unit, new object[] { kgCruise });
        float now = kgNow > 0f && !float.IsInfinity(kgNow) ? (float)cruiseKmForFuel.Invoke(unit, new object[] { kgNow }) : 0f;
        cruise = Mathf.Max(economy, now);
        return economy > 0f;
    }

    /// <summary>The members of the old curve model on one parameter type (null entries: not on this build).</summary>
    private sealed class CurveType
    {
        public bool Ok;
        public FieldInfo Mach, Empty, Load, Altitude, RangeMult, Hover;
    }

    private static CurveType aircraftCurves, heliCurves;

    private static CurveType CurvesOf(ref CurveType cache, object parameters, bool heli)
    {
        if (cache != null)
            return cache;
        Type t = parameters.GetType();
        CurveType c = new CurveType
        {
            Empty = t.GetField("_emptyMass", AnyMember),
            Load = t.GetField("_loadoutMass", AnyMember),
        };
        if (heli)
        {
            c.Hover = t.GetField("_hoveringFuelConsumption", AnyMember);
            c.Ok = c.Empty != null && c.Load != null && c.Hover != null;
        }
        else
        {
            c.Mach = t.GetField("_speedValuesInMach", AnyMember);
            c.Altitude = t.GetField("_altitudeEfficiencyCurve", AnyMember);
            c.RangeMult = t.GetField("_rangeMultiplierCurve", AnyMember);
            c.Ok = c.Empty != null && c.Load != null && c.Mach != null && c.Altitude != null && c.RangeMult != null;
        }
        cache = c;
        return c;
    }

    private static bool BurnCurves(ObjectBase unit, out float cruise, out float economy)
    {
        cruise = economy = 0f;
        if (unit is Aircraft a)
        {
            object ap = a.Ap;
            if (ap == null)
                return false;
            CurveType c = CurvesOf(ref aircraftCurves, ap, false);
            if (!c.Ok)
                return false;
            float[] speeds = (float[])c.Mach.GetValue(ap);
            float empty = (float)c.Empty.GetValue(ap);
            AnimationCurve alt = c.Altitude.GetValue(ap) as AnimationCurve;
            AnimationCurve rng = c.RangeMult.GetValue(ap) as AnimationCurve;
            if (speeds == null || speeds.Length == 0 || empty <= 0f || alt == null || rng == null)
                return false;
            float mass = 1f + (float)c.Load.GetValue(ap) / empty;
            float altitude = Mathf.Max(0.05f, alt.Evaluate(a.getHeightInFeet()));
            float mach = speeds[Math.Min(CruiseSpeedIndex, speeds.Length - 1)];
            economy = Mathf.Max(1f, mass);
            cruise = Mathf.Max(economy, rng.Evaluate(mach) / altitude * mass);
            return true;
        }
        if (unit is Helicopter h)
        {
            object hp = h._hp;
            if (hp == null)
                return false;
            CurveType c = CurvesOf(ref heliCurves, hp, true);
            if (!c.Ok)
                return false;
            float empty = (float)c.Empty.GetValue(hp);
            if (empty <= 0f)
                return false;
            float mass = 1f + (float)c.Load.GetValue(hp) / empty;
            float hover = Mathf.Clamp01((float)c.Hover.GetValue(hp));
            // Per km: mass * (hover * vmax / v + 1 - hover) - flat out is the cheapest, the hover burn is per hour.
            economy = mass;
            cruise = mass * (hover / HeliCruiseShare + 1f - hover);
            return true;
        }
        return false;
    }

    private static float Km(ObjectBase unit, ObjectBase site) =>
        Distance.Km(unit.Position.Value, site.Position.Value);

    /// <summary>The closest place this aircraft can land: its home base, or any friendly deck/field that takes it.</summary>
    private static (ObjectBase deck, float km) NearestDeck(ObjectBase unit)
    {
        ObjectBase best = null;
        float bestKm = float.MaxValue;
        void Consider(ObjectBase site)
        {
            float km = Km(unit, site);
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

    /// <summary>
    /// Nothing in the mission takes it: the nearest friendly field or deck it would call home all the same. Aircraft:
    /// airfields and decks that carry fixed-wing aircraft; helicopters: any deck.
    /// </summary>
    private static (ObjectBase field, float km) NearestField(ObjectBase unit)
    {
        ObjectBase best = null;
        float bestKm = float.MaxValue;
        Taskforce side = unit._taskforce;
        if (side?._taskforceUnits == null)
            return (null, 0f);
        foreach (ObjectBase site in side._taskforceUnits)
        {
            FlightDeck deck = site == null ? null : site._obp?._flightDeck;
            if (deck == null || site == unit || site.IsDestroyed)
                continue;
            if (unit is Aircraft && !(site is LandUnit)
                && (deck._vehiclesOnBoard == null || !deck._vehiclesOnBoard.Any(v => v._type != VehicleType.Helicopter)))
                continue;
            float km = Km(unit, site);
            if (km < bestKm)
            {
                best = site;
                bestKm = km;
            }
        }
        return (best, best != null ? bestKm : 0f);
    }
}
