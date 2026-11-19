using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;
using UniRx;
using Unity.Entities;

namespace SeaPowerRadio.Contacts;

/// <summary>
/// Contact calls, replacing the game's (which has no hysteresis and repeats "contact faded" on every flicker):
/// - New contact: spoken by the unit whose sensor found it, with the bearing from that unit. Several new
/// contacts within a few seconds -> one "multiple contacts" call. Contacts that keep turning up one by one in the
/// same place (a raid found aircraft by aircraft) are one group: called once, once more as "multiple contacts" when
/// it reaches three, then only messages.
/// - Faded: only after the contact stayed lost for <c>FadeHysteresis</c> s (regained earlier = silence), at most
/// once per contact per <c>ContactCooldown</c> s. Several fades together -> "lost N contacts".
/// - Flicker: a contact that drops out <c>FlickerCount</c> times within a minute is called "intermittent" once,
/// then stays quiet until it has been stable for two minutes.
/// - Classified hostile: "track 7034, hostile" plus what identified it (visual, ESM, sonar, radar...), and the
/// log line says which unit and sensor.
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

    /// <summary>New contacts already called in one place, so the next one there isn't news on its own.</summary>
    private sealed class Group
    {
        public string Domain;
        public GeoPosition At;
        public double LastAt;
        public int Count;
        public bool MultipleSaid;
    }

    private const double AggregateWindow = 3.0;
    private const double FlickerWindow = 60.0;
    private const double StableAgain = 120.0;
    private const double GroupMemory = 120.0;      // mission seconds since the group's latest new contact
    private const float GroupRadiusNm = 10f;
    private const int MultipleAt = 3;               // contacts in one group that make it "multiple contacts"
    private const double PositionWait = 1.0;       // a new vehicle has no position on the frame it appears
    private const double NewCallWait = 3.0;        // the longest a new-contact call waits for positions
    private const double RegainedGap = 30.0;       // lost-call to regained-call: shorter is the same flicker
    private const int MaxLinkedMessages = 4;

    private readonly RadioPlayer player;
    private readonly Dictionary<Vehicle, Contact> contacts = new Dictionary<Vehicle, Contact>();
    private readonly List<Group> groups = new List<Group>();
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
        groups.Clear();
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
            pendingNew.Add((v, Now + PositionWait));
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
                    Say(SpeakerFor(c.Vehicle), "ContactIntermittent", c.Id, c.Vehicle, "Track ${TrackId} intermittent");
                }
            }
        }
        else
        {
            if (c.ReportedFaded && Now - c.LastFadedCall > RegainedGap && !c.Intermittent)
                Say(SpeakerFor(c.Vehicle), "ContactRegained", c.Id, c.Vehicle, "Regained Track ${TrackId}");
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
        ModLog.Info($"[contact] track {c.Id} classified {relation} - by {(unit != null ? unit.getUIDAndName() : "?")}" +
                    $" via {sensorName ?? sensor.ToString()} (best track: {v.Type.Value})");
        if (!Settings.ContactReports.Value)
            return;

        // Every classification gets a message (with a clickable track); hostile ones - and neutral ones if
        // configured - are also called out.
        RosterElement speaker = Speaker(unit);
        string what = v.Identified.Value && v.BaseObject != null ? v.BaseObject.ClassName : Domain(v);
        string by = CauseText(sensor);
        RaiseLinked(speaker, v, $"Track ${{TrackId}} classified {RelationText(relation)} - {what}{(by != null ? ", " + by : "")}");
        List<Alert> alerts = AlertLink.TakeFor(RaiserFor(speaker));   // taken whether it is spoken or not
        bool speak = relation == RelationsState.Hostile || (relation == RelationsState.Neutral && Settings.AnnounceNeutral.Value);
        if (!speak || speaker == null)
            return;
        Transmission t = TransmissionBuilder.Start(speaker);
        string phrase = relation == RelationsState.Hostile ? "TrackHostile" : "TrackNeutral";
        if (!TransmissionBuilder.Append(t, phrase, c.Id))
            return;
        string cause = CauseOf(sensor);
        if (cause != null)
            TransmissionBuilder.Append(t, cause, null);
        SpokenReport.Submit(player, t, alerts);
    }

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
        // Wait until every new contact has a position, but never longer than NewCallWait after the first one.
        if (pendingNew.Count == 0 || (pendingNew.Any(p => p.at > Now) && pendingNew[0].at > Now - (NewCallWait - PositionWait)))
            return;
        var batch = pendingNew.Select(p => p.vehicle).Where(Relevant).ToList();
        pendingNew.Clear();
        if (batch.Count == 0)
            return;
        Vehicle first = batch[0];
        RosterElement speaker = SpeakerFor(first);
        if (speaker == null) return;
        int bearing = Bearing(speaker, first);
        bool speak = NewsFor(first, batch.Count, out bool multiple);
        string phrase = multiple ? "NewContactsMultiple" : NewContactPhrase(first);
        // One message per contact, each with a clickable track number (a few at most; the rest as a summary).
        foreach (Vehicle v in batch.Take(MaxLinkedMessages))
            RaiseLinked(speaker, v, $"New {Domain(v)} contact - Track ${{TrackId}}, bearing {Bearing(speaker, v):000}");
        if (batch.Count > MaxLinkedMessages)
            RaiseLinked(speaker, null, $"{batch.Count - MaxLinkedMessages} more new contacts: Tracks " +
                                       string.Join(", ", batch.Skip(MaxLinkedMessages).Select(v => v.Id)));
        List<Alert> alerts = AlertLink.TakeFor(RaiserFor(speaker));   // taken whether it is spoken or not
        if (speak)
            SpokenReport.Say(player, speaker, phrase, bearing, alerts);
        ModLog.Info($"[contact] new: {string.Join(", ", batch.Select(v => v.Id))} reported by {speaker.Callsign}" +
                    (speak ? "" : " (same group as an earlier call, not spoken)"));
    }

    /// <summary>
    /// Is a batch of new contacts news on the net? Aircraft of a raid (ships of a group) are often found one by one,
    /// seconds apart: the first is called, the group once more as "multiple contacts" when it reaches three, the
    /// rest only get their messages - instead of "new air contact, bearing 327" five times in a row.
    /// </summary>
    private bool NewsFor(Vehicle first, int count, out bool multiple)
    {
        string domain = Domain(first);
        GeoPosition at = first.Position.Value;
        groups.RemoveAll(g => Now - g.LastAt > GroupMemory);
        Group group = groups.FirstOrDefault(g => g.Domain == domain && Distance.Nm(g.At, at) <= GroupRadiusNm);
        if (group == null)
        {
            groups.Add(new Group { Domain = domain, At = at, LastAt = Now, Count = count, MultipleSaid = count >= MultipleAt });
            multiple = count >= MultipleAt;
            return true;
        }
        group.At = at;
        group.LastAt = Now;
        group.Count += count;
        multiple = true;
        if (group.MultipleSaid || group.Count < MultipleAt)
            return false;
        group.MultipleSaid = true;
        return true;
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
                foreach (Contact c in pendingFaded.Take(MaxLinkedMessages))
                    RaiseLinked(speaker, c.Vehicle, "Lost contact - Track ${TrackId}");
                if (pendingFaded.Count > MaxLinkedMessages)
                    RaiseLinked(speaker, null, $"Lost {pendingFaded.Count - MaxLinkedMessages} more contacts: Tracks " +
                                               string.Join(", ", pendingFaded.Skip(MaxLinkedMessages).Select(c => c.Id)));
                List<Alert> alerts = AlertLink.TakeFor(RaiserFor(speaker));
                if (pendingFaded.Count == 1)
                    SpokenReport.Say(player, speaker, "ContactFaded", pendingFaded[0].Id, alerts);
                else
                    SpokenReport.Say(player, speaker, "ContactsFaded", Math.Min(pendingFaded.Count, 9), alerts);
            }
        }
        pendingFaded.Clear();
    }

    private static readonly Dictionary<string, string> NewContactPhrases = new Dictionary<string, string>
    {
        { "surface", "NewContactSurface" }, { "subsurface", "NewContactSubsurface" },
        { "ground", "NewContactLand" }, { "air", "NewContactAir" },
    };

    private static string NewContactPhrase(Vehicle v) => NewContactPhrases[Domain(v)];

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
        ObjectBase from = RaiserFor(speaker);
        return from == null ? 0 : Distance.Bearing(from._geoPosition, v.Position.Value);
    }

    private void Say(RosterElement speaker, string phrase, int number, Vehicle v, string text)
    {
        if (speaker == null || !Settings.ContactReports.Value) return;
        RaiseLinked(speaker, v, text);
        List<Alert> alerts = AlertLink.TakeFor(RaiserFor(speaker));
        SpokenReport.Say(player, speaker, phrase, number, alerts);
    }

    /// <summary>The unit a message is "from": the speaker, or the task force's command voice.</summary>
    private static ObjectBase RaiserFor(RosterElement speaker) =>
        speaker?.Unit != null ? speaker.Unit : RosterService.Current?.CommandVoice()?.Unit;

    /// <summary>
    /// A message bottom right in which the reporting unit and the track number are both links: the game's
    /// RaiseAlert(raising, about, text) turns ${Name} into a link to the reporter and ${TrackId} into a link that
    /// jumps to the contact - the stock contact messages only link the reporter, if anything.
    /// </summary>
    private static void RaiseLinked(RosterElement speaker, Vehicle v, string text)
    {
        ObjectBase raiser = RaiserFor(speaker);
        ObjectBase about = v?.BaseObject;
        if (raiser == null || about == null)
            text = text.Replace("${TrackId}", v?.Id.ToString() ?? "?");    // no object to link the number to
        GameMessage.Raise("contact", raiser, about, raiser != null ? "${Name}: " + text : text);
    }

    private static string Domain(Vehicle v)
    {
        switch (v?.BaseObject?._type)
        {
            case ObjectBase.ObjectType.Vessel: return "surface";
            case ObjectBase.ObjectType.Submarine:
            case ObjectBase.ObjectType.Torpedo:
            case ObjectBase.ObjectType.Biologic: return "subsurface";
            case ObjectBase.ObjectType.LandUnit: return "ground";
            default: return "air";
        }
    }

    private static string RelationText(RelationsState r) =>
        r == RelationsState.Hostile ? "HOSTILE" : r == RelationsState.Neutral ? "neutral" : r == RelationsState.Friendly ? "friendly" : "unknown";

    // The message's words for each spoken cause (CauseOf).
    private static readonly Dictionary<string, string> CauseTexts = new Dictionary<string, string>
    {
        { "IdByVisual", "visual ID" }, { "IdByEsm", "by ESM" }, { "IdBySonar", "by sonar" },
        { "IdByRadar", "by radar" }, { "IdByIntel", "by intel / datalink" },
    };

    private static string CauseText(SensorSystem.SensorType sensor)
    {
        string cause = CauseOf(sensor);
        return cause != null && CauseTexts.TryGetValue(cause, out string text) ? text : null;
    }
}
