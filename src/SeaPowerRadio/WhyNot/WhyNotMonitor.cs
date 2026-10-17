using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;
using UnityEngine;

namespace SeaPowerRadio.WhyNot;

/// <summary>
/// Says why an ordered engagement isn't happening. The game decides this every frame but never tells the player:
/// <list type="bullet">
/// <item>A weapon system working on the order sits in a blocking <c>WeaponSystem.EngageState</c> (target not in
/// rear aspect, out of envelope, no radar track, nose not on target…) for more than <c>StateHold</c> s.</item>
/// <item>An order waits with no weapon system taking it: every tube/launcher for that ammunition is reloading
/// ("ready in four minutes"), the target is held by sonar only while the weapon needs a radar track, or nothing
/// is left to fire.</item>
/// <item>An order disappears without anything being launched (the game dropped it).</item>
/// <item>A fighter told to shoot is busy defending against a missile.</item>
/// </list>
/// Each reason is spoken by the unit itself (as a reply, so it is in front of the net), raised as a game alert and
/// logged; the same reason from the same unit at most once per <c>Cooldown</c> s.
/// </summary>
internal sealed class WhyNotMonitor
{
    private sealed class UnitTrack
    {
        public readonly Dictionary<EngageTask, float> PendingSince = new Dictionary<EngageTask, float>();
        public readonly HashSet<EngageTask> Known = new HashSet<EngageTask>();
        public readonly Dictionary<WeaponSystem, (WeaponSystem.EngageState state, float since)> States =
            new Dictionary<WeaponSystem, (WeaponSystem.EngageState, float)>();
        public readonly Dictionary<string, float> LastSaid = new Dictionary<string, float>();
        public float LastAny = -100f;
    }

    private const float ScanInterval = 0.5f;
    private const float StateHold = 2.0f;
    private const float PendingHold = 3.0f;
    private const float Cooldown = 40f;
    private const float UnitGap = 6f;

    private readonly RadioPlayer player;
    private readonly Dictionary<int, UnitTrack> units = new Dictionary<int, UnitTrack>();
    private float nextScan;

    // Units whose orders the player just cleared (cease fire, the queue overlay): a vanished order there is the
    // player's doing, not the game dropping it.
    private static readonly Dictionary<int, float> ClearedAt = new Dictionary<int, float>();

    public static void MarkCleared(ObjectBase unit)
    {
        if (unit != null)
            ClearedAt[unit.GetInstanceID()] = Time.unscaledTime;
    }

    public WhyNotMonitor(RadioPlayer player) => this.player = player;

    public void Clear() => units.Clear();

    public void Tick()
    {
        if (!Settings.WhyNot.Value || Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + ScanInterval;
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return;
        foreach (RosterElement e in roster.Elements)
        {
            ObjectBase unit = e.Unit;
            if (unit == null || unit.IsDestroyed || !unit.IsPlayerUnit)
                continue;
            try { Scan(e, unit); }
            catch (Exception ex) { ModLog.Debug($"[why-not] scan {unit.getName()}: {ex.Message}"); }
        }
    }

    private void Scan(RosterElement e, ObjectBase unit)
    {
        int id = unit.GetInstanceID();
        if (!units.TryGetValue(id, out UnitTrack track))
            units[id] = track = new UnitTrack();
        float now = Time.unscaledTime;
        List<EngageTask> tasks = unit._currentEngageTasks;
        List<WeaponSystem> systems = unit._obp?._weaponSystems ?? new List<WeaponSystem>();

        // Orders that vanished without a launch: the game gave up on them.
        bool clearedByPlayer = ClearedAt.TryGetValue(id, out float clearedAt) && now - clearedAt < 3f;
        foreach (EngageTask gone in track.Known.Where(t => !tasks.Contains(t)).ToList())
        {
            track.Known.Remove(gone);
            track.PendingSince.Remove(gone);
            if (clearedByPlayer || gone._isAutoAttack || gone._targetObject == null || gone._targetObject.IsDestroyed)
                continue;
            bool launched = systems.Any(w => GameTime.missionElapsedTime - w._lastLaunchTime < 5.0);
            if (!launched)
                Report(e, unit, track, DroppedReason(unit, gone), gone, "order dropped without a launch");
        }
        foreach (EngageTask t in tasks)
            track.Known.Add(t);

        if (tasks.Count == 0)
        {
            track.States.Clear();
            return;
        }

        // A fighter that is defending against a missile never gets its nose on the target.
        if (unit is Aircraft && unit.StateMachine?.CurrentStateName?.IndexOf("Evasion", StringComparison.OrdinalIgnoreCase) >= 0
            || unit is Aircraft && unit.StateMachine?.CurrentStateName?.IndexOf("Evade", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Report(e, unit, track, "WhyNot_Defending", tasks[0], "missile evasion state");
            return;
        }

        // Weapon systems working on an order, stuck in a blocking state.
        foreach (WeaponSystem w in systems)
        {
            if (!w._executingEngageTask || w._takenEngageTask == null)
            {
                track.States.Remove(w);
                continue;
            }
            WeaponSystem.EngageState state = w._engageState;
            string reason = ReasonFor(state, w, unit);
            if (reason == null)
            {
                track.States.Remove(w);
                continue;
            }
            if (!track.States.TryGetValue(w, out var held) || held.state != state)
            {
                track.States[w] = (state, now);
                continue;
            }
            if (now - held.since >= StateHold)
                Report(e, unit, track, reason, w._takenEngageTask, $"{w._vwp?._systemName ?? "weapon"} state {state}");
        }

        // Orders nobody has taken yet.
        foreach (EngageTask t in tasks)
        {
            if (systems.Any(w => w._executingEngageTask && w._takenEngageTask == t))
            {
                track.PendingSince.Remove(t);
                continue;
            }
            if (!track.PendingSince.TryGetValue(t, out float since))
            {
                track.PendingSince[t] = now;
                continue;
            }
            if (now - since < PendingHold)
                continue;
            string reason = PendingReason(unit, t, out string detail);
            if (reason != null)
                Report(e, unit, track, reason, t, detail);
        }
    }

    /// <summary>Blocking engage states and what the unit says about them. Null = working normally.</summary>
    private static string ReasonFor(WeaponSystem.EngageState state, WeaponSystem w, ObjectBase unit)
    {
        switch (state)
        {
            case WeaponSystem.EngageState.NotInRearAspect: return "WhyNot_NotInRearAspect";
            case WeaponSystem.EngageState.TargetTooFar:
            case WeaponSystem.EngageState.TargetTooFarToOpenSystem: return "WhyNot_TargetTooFar";
            case WeaponSystem.EngageState.MissileCanNotReachTarget: return "WhyNot_CantReach";
            case WeaponSystem.EngageState.TargetTooClose: return "WhyNot_TargetTooClose";
            case WeaponSystem.EngageState.TargetIsNotDetectableByFCR:
            case WeaponSystem.EngageState.PrelaunchSensorAlignment:
                return TargetSonarOnly(w._takenEngageTask?._targetObject, unit) ? "WhyNot_SonarOnly" : "WhyNot_NoRadarLock";
            case WeaponSystem.EngageState.TargetIsNotDetectableBySearchSensor:
                return TargetSonarOnly(w._takenEngageTask?._targetObject, unit) ? "WhyNot_SonarOnly" : "WhyNot_NoSensorTrack";
            case WeaponSystem.EngageState.TargetIsBeyondFCRHorizon: return "WhyNot_BeyondRadarHorizon";
            case WeaponSystem.EngageState.NoLOSToTarget: return "WhyNot_NoLineOfSight";
            case WeaponSystem.EngageState.LauncherTooLow: return "WhyNot_TooLow";
            case WeaponSystem.EngageState.LauncherTooHigh: return "WhyNot_TooHigh";
            case WeaponSystem.EngageState.TargetIsNotInFiringArcs:
            case WeaponSystem.EngageState.TargetIsNotInVerticalArc: return "WhyNot_NotInFiringArc";
            case WeaponSystem.EngageState.TVSeekerCantSeeTarget:
            case WeaponSystem.EngageState.LaserCantPaintTarget: return "WhyNot_SeekerCantSee";
            case WeaponSystem.EngageState.SpeedTooLow:
            case WeaponSystem.EngageState.SpeedTooHigh: return "WhyNot_SpeedWrong";
            case WeaponSystem.EngageState.EngagementNotAllowed: return "WhyNot_NotAllowed";
            default: return null;
        }
    }

    /// <summary>Why an order waits with no weapon system on it. Null when there is nothing certain to say.</summary>
    private static string PendingReason(ObjectBase unit, EngageTask t, out string detail)
    {
        detail = null;
        List<WeaponSystem> all = unit.GetWeaponSystemsForAmmunition(t._ammoId) ?? new List<WeaponSystem>();
        var live = all.Where(w => !w.Inoperable.Value && !w._isOffline).ToList();
        if (live.Count == 0 || unit.getAmmunitionAmountByName(t._ammoId) < 1)
        {
            detail = $"no usable system / no {t._ammoId} left";
            return "WhyNot_NoAmmo";
        }
        if (live.All(w => w._isReloading || w._executingEngageTask))
        {
            var reloading = live.Where(w => w._isReloading).ToList();
            if (reloading.Count > 0)
            {
                float seconds = reloading.Min(w => Math.Max(0f, w._remainingReloadTime));
                detail = $"all {t._ammoId} systems busy/reloading, first ready in {seconds:F0} s";
                return ReloadPhrase(seconds);
            }
            return null; // busy with an earlier order: that is the queue, not a problem
        }
        if (TargetSonarOnly(t._targetObject, unit) && NeedsNonSonarTrack(unit, t))
        {
            detail = "target held by sonar only";
            return "WhyNot_SonarOnly";
        }
        return null;
    }

    private static string DroppedReason(ObjectBase unit, EngageTask t)
    {
        if (TargetSonarOnly(t._targetObject, unit) && NeedsNonSonarTrack(unit, t))
            return "WhyNot_SonarOnly";
        if (unit.getAmmunitionAmountByName(t._ammoId) < 1)
            return "WhyNot_NoAmmo";
        return "WhyNot_CantSeeTarget";
    }

    public static string ReloadPhrase(float seconds)
    {
        if (seconds < 45f) return "WhyNot_ReloadSoon";
        int minutes = Mathf.CeilToInt(seconds / 60f);
        return minutes <= 10 ? "WhyNot_ReloadIn" + minutes : "WhyNot_ReloadLong";
    }

    /// <summary>The player's picture of the target comes from sonar only (no radar, visual or ESM track).</summary>
    private static bool TargetSonarOnly(ObjectBase target, ObjectBase unit)
    {
        if (target == null) return false;
        Vehicle v = target.GetMapVehicle(Globals._playerTaskforce);
        if (v == null) return false;
        SensorSystem.SensorType type = v.Type.Value;
        return type == SensorSystem.SensorType.PassiveSonar || type == SensorSystem.SensorType.ActiveSonar;
    }

    /// <summary>Anything that isn't a sonar-homing weapon needs a position fix a sonar bearing doesn't give.</summary>
    private static bool NeedsNonSonarTrack(ObjectBase unit, EngageTask t)
    {
        Ammunition ammo = t._ammunition ?? unit.getAmmunitionByName(t._ammoId);
        if (ammo?._ap == null) return true;
        switch (ammo._ap._guidanceType)
        {
            case AmmunitionParameters.GuidanceType.ActiveSonarHoming:
            case AmmunitionParameters.GuidanceType.PassiveSonarHoming:
            case AmmunitionParameters.GuidanceType.ActivePassiveSonarHoming:
            case AmmunitionParameters.GuidanceType.WakeHoming:
                return false;
            default:
                return true;
        }
    }

    private void Report(RosterElement e, ObjectBase unit, UnitTrack track, string phrase, EngageTask task, string detail)
    {
        float now = Time.unscaledTime;
        if (track.LastSaid.TryGetValue(phrase, out float last) && now - last < Cooldown)
            return;
        if (now - track.LastAny < UnitGap)
            return;
        track.LastSaid[phrase] = now;
        track.LastAny = now;

        string weapon = AmmoName(unit, task);
        string target = task?._targetObject != null ? $"track {task._targetObject.GetMapContactID()}" : "position";
        string text = $"{unit.getName()}: {AlertText(phrase)} ({weapon} → {target})";
        ModLog.Info($"[why-not] {text} [{detail}]");
        try { Globals._mainGameViewModel?.RaiseAlert(unit, text); }
        catch (Exception ex) { ModLog.Debug($"[why-not] alert failed: {ex.Message}"); }

        Transmission t = TransmissionBuilder.Build(e, phrase, null);
        if (t != null)
            player.Submit(t);
    }

    private static string AmmoName(ObjectBase unit, EngageTask task)
    {
        if (task == null) return "weapon";
        Ammunition ammo = task._ammunition ?? unit.getAmmunitionByName(task._ammoId);
        return ammo?._ap?._displayedName ?? task._ammoId;
    }

    /// <summary>The on-screen line next to the spoken one.</summary>
    public static string AlertText(string phrase)
    {
        if (phrase.StartsWith("WhyNot_ReloadIn"))
            return $"Reloading, ready in {phrase.Substring("WhyNot_ReloadIn".Length)} min";
        switch (phrase)
        {
            case "WhyNot_ReloadSoon": return "Reloading, ready in under a minute";
            case "WhyNot_ReloadLong": return "Reloading, ready in more than 10 min";
            case "WhyNot_NotInRearAspect": return "Unable – target not in rear aspect (IR missile)";
            case "WhyNot_TargetTooFar": return "Unable – target outside launch envelope";
            case "WhyNot_CantReach": return "Unable – missile cannot reach the target";
            case "WhyNot_TargetTooClose": return "Unable – inside minimum range";
            case "WhyNot_NoRadarLock": return "Negative – no radar lock / fire control track";
            case "WhyNot_NoSensorTrack": return "Unable – no sensor track on the target";
            case "WhyNot_SonarOnly": return "Negative – sonar contact only, no targeting track for this weapon";
            case "WhyNot_BeyondRadarHorizon": return "Unable – target below the radar horizon";
            case "WhyNot_NoLineOfSight": return "Unable – no line of sight";
            case "WhyNot_TooLow": return "Unable – too low to launch";
            case "WhyNot_TooHigh": return "Unable – too high to launch";
            case "WhyNot_NotInFiringArc": return "Turning – target not in the firing arc";
            case "WhyNot_SeekerCantSee": return "Negative – seeker can't see the target in these conditions";
            case "WhyNot_SpeedWrong": return "Unable – wrong speed for launch";
            case "WhyNot_NotAllowed": return "Negative – not cleared to engage";
            case "WhyNot_NoAmmo": return "Negative – nothing left for that target";
            case "WhyNot_CantSeeTarget": return "Order dropped – no sensor can see the target";
            case "WhyNot_Defending": return "Unable – defending against a missile";
            case "WhyNot_HoldingFormation": return "Unable – wingman holds formation on lead (VID needs a detached aircraft)";
            default: return phrase;
        }
    }
}
