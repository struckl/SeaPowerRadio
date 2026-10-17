using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;
using UniRx;
using Unity.Entities;
using UnityEngine;

namespace SeaPowerRadio.Contacts;

/// <summary>
/// Contact calls, replacing the game's (which has no hysteresis and repeats "contact faded" on every flicker):
/// <list type="bullet">
/// <item>New contact: spoken by the unit whose sensor found it, with the bearing from that unit. Several new
/// contacts within a few seconds → one "multiple contacts" call.</item>
/// <item>Faded: only after the contact stayed lost for <c>FadeHysteresis</c> s (regained earlier = silence), at most
/// once per contact per <c>ContactCooldown</c> s. Several fades together → "lost N contacts".</item>
/// <item>Flicker: a contact that drops out <c>FlickerCount</c> times within a minute is called "intermittent" once,
/// then stays quiet until it has been stable for two minutes.</item>
/// <item>Classified hostile: "track 7034, hostile" plus what identified it (visual, ESM, sonar, radar…), and the
/// log line says which unit and sensor.</item>
/// </list>
/// All of these are low priority, so the radio drops them first when the net is busy.
/// </summary>
internal sealed class ContactReporter
{
    private sealed class Contact
    {
        public Vehicle Vehicle;
        public int Id;
        public double DormantSince = -1;
        public bool ReportedFaded;
        public double LastFadedCall = -1e9;
        public readonly List<double> Drops = new List<double>();
        public bool Intermittent;
        public double IntermittentCalledAt = -1e9;
        public bool Classified;
        public readonly CompositeDisposable Subscriptions = new CompositeDisposable();
    }

    private const double AggregateWindow = 3.0;
    private const double FlickerWindow = 60.0;
    private const double StableAgain = 120.0;

    private readonly RadioPlayer player;
    private readonly Dictionary<Vehicle, Contact> contacts = new Dictionary<Vehicle, Contact>();
    private readonly List<(Vehicle vehicle, double at)> pendingNew = new List<(Vehicle, double)>();
    private readonly List<Contact> pendingFaded = new List<Contact>();
    private PlottingTable table;
    private double fadedWindowStart = -1;

    public ContactReporter(RadioPlayer player) => this.player = player;

    private static double Now => GameTime.missionElapsedTime;

    public void Attach()
    {
        Detach();
        table = Globals._playerTaskforce?.PlottingTable;
        if (table == null)
            return;
        table.NewVehicle += OnNewVehicle;
        // Contacts already on the plot when the mission became ready: track them, but don't announce them.
        foreach (Vehicle v in table.Vehicles.ToList())
            Track(v, announce: false);
    }

    public void Detach()
    {
        if (table != null)
            table.NewVehicle -= OnNewVehicle;
        table = null;
        foreach (Contact c in contacts.Values)
            c.Subscriptions.Dispose();
        contacts.Clear();
        pendingNew.Clear();
        pendingFaded.Clear();
    }

    private void OnNewVehicle(object sender, Vehicle vehicle)
    {
        try { Track(vehicle, announce: true); }
        catch (Exception ex) { ModLog.Error($"[contact] new vehicle: {ex.Message}"); }
    }

    private static bool Relevant(Vehicle v) =>
        v != null && v.BaseObject != null && !v.BaseObject.IsDestroyed && v.IsVisible() && v.BaseObject.isUnit()
        && v.BaseObject._taskforce != Globals._playerTaskforce && !(v.BaseObject is LaunchTransient);
    // Weapons and decoys are left out on purpose: incoming missiles and torpedoes have their own threat calls, and
    // a salvo would otherwise flood the net with "new contact" / "lost contact".

    private void Track(Vehicle v, bool announce)
    {
        if (!Relevant(v) || contacts.ContainsKey(v))
            return;
        var c = new Contact { Vehicle = v, Id = v.Id, Classified = v.UnitTaskforce.Value != null };
        contacts[v] = c;
        c.Subscriptions.Add(v.IsDormant.Subscribe(dormant => OnDormant(c, dormant)));
        c.Subscriptions.Add(v.UnitTaskforce.Subscribe(tf => OnClassified(c, tf)));
        v.Deleted += (s, e) =>
        {
            c.Subscriptions.Dispose();
            contacts.Remove(v);
        };
        if (announce && Settings.ContactReports.Value)
            pendingNew.Add((v, Now + 1.0)); // the vehicle has no position yet on the frame it appears
    }

    private void OnDormant(Contact c, bool dormant)
    {
        if (dormant)
        {
            c.DormantSince = Now;
            c.Drops.Add(Now);
            c.Drops.RemoveAll(t => Now - t > FlickerWindow);
            if (!c.Intermittent && c.Drops.Count >= Settings.FlickerCount.Value)
            {
                c.Intermittent = true;
                if (Now - c.IntermittentCalledAt > Settings.ContactCooldown.Value)
                {
                    c.IntermittentCalledAt = Now;
                    Say(SpeakerFor(c.Vehicle), "ContactIntermittent", c.Id);
                }
            }
        }
        else
        {
            if (c.ReportedFaded && Now - c.LastFadedCall > 30 && !c.Intermittent)
                Say(SpeakerFor(c.Vehicle), "ContactRegained", c.Id);
            c.DormantSince = -1;
            c.ReportedFaded = false;
        }
    }

    private void OnClassified(Contact c, Taskforce side)
    {
        if (side == null || c.Classified)
            return;
        c.Classified = true;
        Vehicle v = c.Vehicle;
        RelationsState relation = Globals._playerTaskforce.RelationshipTo(side);
        (ObjectBase unit, SensorSystem.SensorType sensor, string sensorName) = Detector(v);
        ModLog.Info($"[contact] track {c.Id} classified {relation} – by {(unit != null ? unit.getUIDAndName() : "?")}" +
                    $" via {sensorName ?? sensor.ToString()} (best track: {v.Type.Value})");
        if (!Settings.ContactReports.Value)
            return;
        if (relation != RelationsState.Hostile && !(relation == RelationsState.Neutral && Settings.AnnounceNeutral.Value))
            return;

        RosterElement speaker = Speaker(unit);
        if (speaker == null) return;
        Transmission t = TransmissionBuilder.Start(speaker);
        string phrase = relation == RelationsState.Hostile ? "TrackHostile" : "TrackNeutral";
        if (!TransmissionBuilder.Append(t, phrase, c.Id))
            return;
        string cause = CauseOf(sensor);
        if (cause != null)
            TransmissionBuilder.Append(t, cause, null);
        player.Submit(t);
    }

    /// <summary>Called every frame from the host.</summary>
    public void Tick()
    {
        if (table == null)
            return;
        FlushNew();
        CollectFaded();
        FlushFaded();
        foreach (Contact c in contacts.Values)
            if (c.Intermittent && c.Drops.Count > 0 && Now - c.Drops.Last() > StableAgain)
            {
                c.Intermittent = false;
                c.Drops.Clear();
            }
    }

    private void FlushNew()
    {
        // Wait until every new contact has a position, but never longer than 3 s after the first one.
        if (pendingNew.Count == 0 || (pendingNew.Any(p => p.at > Now) && pendingNew[0].at > Now - 2.0))
            return;
        var batch = pendingNew.Select(p => p.vehicle).Where(Relevant).ToList();
        pendingNew.Clear();
        if (batch.Count == 0)
            return;
        Vehicle first = batch[0];
        RosterElement speaker = SpeakerFor(first);
        if (speaker == null) return;
        int bearing = Bearing(speaker, first);
        string phrase = batch.Count >= 3 ? "NewContactsMultiple" : NewContactPhrase(first);
        Transmission t = TransmissionBuilder.Build(speaker, phrase, bearing);
        player.Submit(t);
        ModLog.Info($"[contact] new: {string.Join(", ", batch.Select(v => v.Id))} reported by {speaker.Callsign}");
    }

    private void CollectFaded()
    {
        foreach (Contact c in contacts.Values)
        {
            if (c.DormantSince < 0 || c.ReportedFaded || c.Intermittent)
                continue;
            if (c.Vehicle.BaseObject == null || c.Vehicle.BaseObject.IsDestroyed)
                continue; // a kill, not a lost contact
            if (Now - c.DormantSince < Settings.FadeHysteresis.Value)
                continue;
            c.ReportedFaded = true;
            if (Now - c.LastFadedCall < Settings.ContactCooldown.Value)
                continue;
            c.LastFadedCall = Now;
            if (pendingFaded.Count == 0)
                fadedWindowStart = Now;
            pendingFaded.Add(c);
        }
    }

    private void FlushFaded()
    {
        if (pendingFaded.Count == 0 || Now - fadedWindowStart < AggregateWindow)
            return;
        if (Settings.ContactReports.Value)
        {
            RosterElement speaker = SpeakerFor(pendingFaded[0].Vehicle);
            if (speaker != null)
            {
                Transmission t = pendingFaded.Count == 1
                    ? TransmissionBuilder.Build(speaker, "ContactFaded", pendingFaded[0].Id)
                    : TransmissionBuilder.Build(speaker, "ContactsFaded", Math.Min(pendingFaded.Count, 9));
                player.Submit(t);
            }
        }
        pendingFaded.Clear();
    }

    // --------------------------------------------------------------------------------------------------------

    private static string NewContactPhrase(Vehicle v)
    {
        switch (v.BaseObject._type)
        {
            case ObjectBase.ObjectType.Vessel: return "NewContactSurface";
            case ObjectBase.ObjectType.Submarine:
            case ObjectBase.ObjectType.Torpedo:
            case ObjectBase.ObjectType.Biologic: return "NewContactSubsurface";
            case ObjectBase.ObjectType.LandUnit: return "NewContactLand";
            default: return "NewContactAir";
        }
    }

    private static string CauseOf(SensorSystem.SensorType sensor)
    {
        switch (sensor)
        {
            case SensorSystem.SensorType.Visual:
            case SensorSystem.SensorType.TVSeeker: return "IdByVisual";
            case SensorSystem.SensorType.ESM:
            case SensorSystem.SensorType.ActiveIntercept: return "IdByEsm";
            case SensorSystem.SensorType.PassiveSonar:
            case SensorSystem.SensorType.ActiveSonar: return "IdBySonar";
            case SensorSystem.SensorType.SearchRadar:
            case SensorSystem.SensorType.FCR:
            case SensorSystem.SensorType.ARS: return "IdByRadar";
            case SensorSystem.SensorType.Unknown: return "IdByIntel";
            default: return null;
        }
    }

    /// <summary>The newest track's source unit and sensor: who found / identified this contact.</summary>
    private static (ObjectBase unit, SensorSystem.SensorType sensor, string name) Detector(Vehicle v)
    {
        ObjectBase unit = null;
        SensorSystem.SensorType sensor = v.Type.Value;
        string name = null;
        try
        {
            foreach (Entity e in v.ValidTracks())
            {
                TrackComponent? track = TrackEntityUtils.GetTrackComponent(e);
                if (!track.HasValue) continue;
                ObjectBase source = TrackEntityUtils.GetObjectBase(track.Value.Source);
                if (source == null) continue;
                unit = source;
                sensor = track.Value.SensorType;
                name = track.Value.SensorDisplayName.ToString();
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[contact] track read failed: {ex.Message}");
        }
        return (unit, sensor, name);
    }

    private static RosterElement Speaker(ObjectBase detector)
    {
        Roster.Roster roster = RosterService.Current;
        if (roster == null) return null;
        RosterElement e = detector != null ? roster.Resolve(detector) : null;
        return e ?? roster.CommandVoice();
    }

    private static RosterElement SpeakerFor(Vehicle v) => Speaker(Detector(v).unit);

    private static int Bearing(RosterElement speaker, Vehicle v)
    {
        ObjectBase from = speaker.Unit != null ? speaker.Unit : RosterService.Current?.CommandVoice()?.Unit;
        if (from == null)
            return 0;
        float heading = from._geoPosition.HeadingToPosition(v.Position.Value);
        return Mathf.RoundToInt(((heading % 360f) + 360f) % 360f) % 360;
    }

    private void Say(RosterElement speaker, string phrase, int number)
    {
        if (speaker == null || !Settings.ContactReports.Value) return;
        player.Submit(TransmissionBuilder.Build(speaker, phrase, number));
    }
}
