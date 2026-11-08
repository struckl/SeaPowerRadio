using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
/// The game itself only knows "bingo" against the home base: once fuel only just covers the way home at best
/// efficiency it turns the aircraft home (BingoFuel state) and says no more than "returning to base". That moment
/// is a bingo call too – the game can get there first when another deck is nearer than home, or at high time
/// compression between two scans. Aircraft nothing in the mission can take have no home base: the game drops a
/// mission's HomeBase when that field's air group doesn't carry the type (SceneCreator.RebaseAircraft), never sends
/// them home and lets them fall out of the sky when the tank runs dry. They call all the same, against the nearest
/// friendly field or deck they would call home – where the player sees the range circle stop reaching – and by
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
    private const int CruiseSpeedIndex = 2;        // "Cruise" – what the game uses for "can it reach the tanker"
    private const float JokerMargin = 1.10f, JokerReserveKm = 10f;
    private const float BingoMargin = 1.05f, BingoReserveKm = 5f;
    // Nowhere to land: share of a full tank.
    private const float NoDeckJoker = 0.25f, NoDeckBingo = 0.15f, NoDeckEmergency = 0.05f;
    private const float KmPerMapUnit = 67.200066f / 1000f;

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
                    ModLog.Warn($"[fuel] {unit.getName()}: no fuel calls, scan failed: {ex}");
            }
        }
    }

    private void Scan(Roster.Roster roster, ObjectBase unit, int id)
    {
        bool inFlight = unit is Aircraft aircraft ? aircraft._isInFlight : ((Helicopter)unit)._isInFlight;
        if (!inFlight || unit._unlimitedFuel)
        {
            states.Remove(id);                      // on deck: the next sortie starts fresh
            return;
        }
        RosterElement e = roster.Resolve(unit);
        if (e == null)
            return;
        if (!Burn(unit, out float cruise, out float economy))
        {
            if (failed.Add(id))
                ModLog.Warn($"[fuel] {unit.getName()}: no fuel calls, its fuel model can't be read");
            return;
        }
        (ObjectBase deck, float km) = NearestDeck(unit);
        bool canLand = deck != null;
        if (!canLand)
            (deck, km) = NearestField(unit);
        float fuel = unit.RangeInKm.Value;
        float full = Math.Max(1f, unit._obp._maxRangeInKm);

        Level level;
        if (deck != null)
            level = fuel < km * economy ? Level.Emergency
                : fuel < km * economy * BingoMargin + BingoReserveKm ? Level.Bingo
                : fuel < km * cruise * JokerMargin + JokerReserveKm ? Level.Joker
                : Level.None;
        else
            level = fuel < full * NoDeckEmergency ? Level.Emergency
                : fuel < full * NoDeckBingo ? Level.Bingo
                : fuel < full * NoDeckJoker ? Level.Joker
                : Level.None;

        if (!states.TryGetValue(id, out State s))
        {
            states[id] = s = new State { LastFuel = fuel };
            Describe(unit, deck, canLand, km, fuel, full, cruise, economy);
            if (unit._homeBase == null)
            {
                try { LandingCheck.Log(unit); }
                catch (Exception ex) { ModLog.Warn($"[fuel] {unit.getName()}: landing check failed: {ex.Message}"); }
            }
        }
        if (fuel > s.LastFuel + 1f && level < s.Said)
            s.Said = level;                         // refuelled in the air: calls can come again
        s.LastFuel = fuel;

        if (unit.StateMachine?.CurrentStateName == "BingoFuel" && level < Level.Bingo && unit._homeBase != null)
        {
            // The game has gone bingo before us and is taking it home: that is the bingo call, against home.
            level = Level.Bingo;
            deck = unit._homeBase;
            canLand = true;
            km = Km(unit, deck);
        }
        else if (RtbMarker.IsRtb(unit) && s.Said < Level.Joker)
        {
            s.Said = Level.Joker;                   // already on its way home: no joker call
        }

        if (Settings.VerboseLog.Value && Time.unscaledTime >= s.NextTrace)
        {
            s.NextTrace = Time.unscaledTime + TraceInterval;
            ModLog.Debug($"[fuel] {unit.getName()}: {fuel:0} km fuel, {(deck != null ? $"{deck.getName()} {km:0} km" : "no deck")}, " +
                         $"level {level}, said {s.Said}, state {unit.StateMachine?.CurrentStateName}");
        }

        if (level <= s.Said)
            return;
        s.Said = level;
        Announce(e, unit, level, deck, canLand, km, fuel, full, level == Level.Joker ? cruise : economy);
    }

    /// <summary>Once per sortie: what the calls will be measured against, so the log shows the monitor is on it.</summary>
    private static void Describe(ObjectBase unit, ObjectBase deck, bool canLand, float km, float fuel, float full,
        float cruise, float economy)
    {
        string name = unit.getName();
        if (deck == null)
        {
            ModLog.Info($"[fuel] watching {name}: {fuel:0} km of fuel ({fuel / full:P0}), nothing in the mission can take it " +
                        $"- joker / bingo / emergency at {NoDeckJoker:P0} / {NoDeckBingo:P0} / {NoDeckEmergency:P0} of a full tank");
            return;
        }
        float joker = km * cruise * JokerMargin + JokerReserveKm;
        float bingo = km * economy * BingoMargin + BingoReserveKm;
        string where = canLand ? "" : " (can't land there - nothing in the mission takes it, the game won't send it home)";
        ModLog.Info($"[fuel] watching {name}: {fuel:0} km of fuel, {deck.getName()} {km:0} km away{where} " +
                    $"- joker below {joker:0} km, bingo below {bingo:0} km (moves with the distance)");
    }

    private void Announce(RosterElement e, ObjectBase unit, Level level, ObjectBase deck, bool canLand, float km,
        float fuel, float full, float burn)
    {
        float rangeNm = fuel / Math.Max(1f, burn) / 1.852f;
        string phrase, text;
        if (deck != null)
        {
            float nm = km / 1.852f;
            string where = canLand ? "" : " (can't land there)";
            switch (level)
            {
                case Level.Joker:
                    phrase = "FuelJoker";
                    text = $"${{Name}}: Joker fuel - ${{Target}} is {nm:0} nm{where}, range at cruise {rangeNm:0} nm";
                    break;
                case Level.Bingo:
                    phrase = "FuelBingo";
                    text = $"${{Name}}: BINGO fuel - ${{Target}} is {nm:0} nm{where}, range at best economy {rangeNm:0} nm. " +
                           (canLand ? "Send it home now" : "Nothing in the mission takes it, the game won't bring it home");
                    break;
                default:
                    phrase = "FuelEmergency";
                    text = $"${{Name}}: FUEL EMERGENCY - ${{Target}} is {nm:0} nm{where}, range only {rangeNm:0} nm. Needs a tanker";
                    break;
            }
        }
        else
        {
            string left = (fuel / full).ToString("P0");
            switch (level)
            {
                case Level.Joker:
                    phrase = "FuelJoker";
                    text = $"${{Name}}: Joker fuel - {left} left, range at cruise {rangeNm:0} nm. Nowhere in the area to land";
                    break;
                case Level.Bingo:
                    phrase = "FuelBingo";
                    text = $"${{Name}}: BINGO fuel - {left} left, range at best economy {rangeNm:0} nm. Nowhere in the area to land";
                    break;
                default:
                    phrase = "FuelEmergency";
                    text = $"${{Name}}: FUEL EMERGENCY - {left} left, range only {rangeNm:0} nm. Needs a tanker";
                    break;
            }
        }
        ModLog.Info($"[fuel] {text.Replace("${Name}", unit.getName()).Replace("${Target}", deck != null ? deck.getName() : "")}");
        try
        {
            if (deck != null)
                Globals._mainGameViewModel?.RaiseAlert(unit, deck, text);
            else
                Globals._mainGameViewModel?.RaiseAlert(unit, text);
        }
        catch (Exception ex) { ModLog.Debug($"[fuel] alert failed: {ex.Message}"); }

        List<Alert> alerts = AlertLink.TakeFor(unit);
        Transmission t = TransmissionBuilder.Build(e, phrase, null);
        if (t == null)
            return;                                 // not rendered for this voice yet: the message has to do
        t.Alerts.AddRange(alerts);
        player.Submit(t);
    }

    /// <summary>
    /// Fuel per km flown, in the unit of <c>RangeInKm</c> ("km at best efficiency"): at cruise and at the cheapest speed.
    /// Two game models, both reached by reflection so that one DLL loads and stays quiet on every build (a member missing on
    /// one build must never be named in a method that is JIT-compiled there):
    /// <list type="bullet">
    /// <item>Builds from 261107: the game keeps <c>_fuelPerKmCruise</c> (best-range speed / cruise step) and
    /// <c>_fuelPerKmNow</c> (at the speed flown), kg per km, and reckons the way home with the cruise rate
    /// (Aircraft/Helicopter.UpdateFuelConsumption). economy = that cruise rate, cruise = the greater of it and the rate at
    /// the speed flown; both go through <c>ObjectBase.CruiseKmForFuel</c> into the units of <c>RangeInKm</c>.</item>
    /// <item>Older builds (0.8.7, globe 26223125): the altitude and range-multiplier curves, and the hover share of the
    /// helicopter, as before.</item>
    /// </list>
    /// If neither model can be read: false, no fuel calls, nothing thrown.
    /// </summary>
    private static bool Burn(ObjectBase unit, out float cruise, out float economy)
    {
        cruise = economy = 0f;
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
                ModLog.Warn($"[fuel] fuel rate unreadable, no fuel calls: {ex.Message}");
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
            cruise = mass * (hover / 0.7f + 1f - hover);
            return true;
        }
        return false;
    }

    private static float Km(ObjectBase unit, ObjectBase site) =>
        (float)unit.Position.Value.GetDistance(site.Position.Value) * KmPerMapUnit;

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
