using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeapowerUI.ViewModels;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;

namespace SeaPowerRadio.Air;

/// <summary>
/// Search and rescue on the net, the way a SAR crew reports it:
/// - Survivors in the water - own life rafts appear (a ship sank, aircrew went down): the closest own
/// unit calls them with the bearing, once per group of rafts.
/// - Proceeding to the survivors - a SAR zone sent a helicopter or ship to a raft.
/// - Pickup complete, N souls aboard - survivors taken aboard (the game's raft pickup); with
/// cabin full when a helicopter has no room left (a SAR zone then sends it home).
/// - Survivors delivered - it lands with survivors aboard.
/// Hooks: Patches/SarPatches. Each call also gets a message bottom right (unit, raft and deck are links).
/// </summary>
internal sealed class SarReporter
{
    public static SarReporter Instance { get; private set; }

    private const double QuietAfterStart = 10.0;   // rafts placed by the mission itself are no news
    private const double RaftBatch = 5.0;          // rafts that appear together make one call
    private const double EnRouteCooldown = 60.0;
    private const double ReportedMemory = 600.0;   // a reported group of survivors stays news this long
    private const float SameGroupNm = 3f;          // a raft this close to a reported one belongs to it

    private readonly RadioPlayer player;
    private readonly List<ObjectBase> newRafts = new List<ObjectBase>();
    private readonly List<(GeoPosition at, double when)> reported = new List<(GeoPosition, double)>();
    private readonly Dictionary<int, double> enRouteSaid = new Dictionary<int, double>();
    private readonly HashSet<int> delivered = new HashSet<int>();
    private double batchDue = -1;

    public SarReporter(RadioPlayer player)
    {
        this.player = player;
        Instance = this;
    }

    private static double Now => GameTime.missionElapsedTime;

    public void Clear()
    {
        newRafts.Clear();
        enRouteSaid.Clear();
        delivered.Clear();
        reported.Clear();
        batchDue = -1;
    }

    public void Tick()
    {
        if (batchDue < 0 || Now < batchDue)
            return;
        batchDue = -1;
        try { ReportRafts(); }
        catch (Exception ex) { Rig.Log.Debug($"[sar] rafts: {ex.Message}"); }
    }

    public void RaftAppeared(ObjectBase raft)
    {
        if (raft == null || raft._taskforce != Globals._playerTaskforce || Now < QuietAfterStart)
            return;
        newRafts.Add(raft);
        if (batchDue < 0)
            batchDue = Now + RaftBatch;
    }

    public void EnRoute(ObjectBase rescuer, ObjectBase raft)
    {
        if (!Own(rescuer) || raft == null)
            return;
        int id = rescuer.GetInstanceID();
        if (enRouteSaid.TryGetValue(id, out double last) && Now - last < EnRouteCooldown)
            return;
        enRouteSaid[id] = Now;
        float nm = Nm(rescuer, raft);
        Say(rescuer, raft, $"${{Name}}: Proceeding to survivors{TrackOf(raft)}, {nm:0.0} nm", "SarEnRoute", null);
    }

    public void PickedUp(ObjectBase rescuer, int count)
    {
        if (!Own(rescuer) || count <= 0)
            return;
        delivered.Remove(rescuer.GetInstanceID());
        bool carries = rescuer.IsAirUnit;           // ships take everyone; aircraft count against their cabin
        int aboard = carries ? rescuer._obp.CurrentTransportedOnBoard.Value : count;
        int capacity = rescuer._obp._transportCapacity;
        bool full = carries && capacity > 0 && aboard >= capacity;
        string people = count == 1 ? "1 survivor" : $"{count} survivors";
        string text = carries
            ? $"${{Name}}: Picked up {people} - {aboard}/{capacity} aboard{(full ? ". Cabin full" : "")}"
            : $"${{Name}}: Picked up {people}";

        RosterElement e = RosterService.Current?.Resolve(rescuer);
        GameMessage.RaiseLogged("sar", rescuer, null, text);
        List<Alert> alerts = AlertLink.TakeFor(rescuer);
        if (e == null)
            return;
        Transmission t = TransmissionBuilder.Start(e);
        bool said = aboard == 1 ? TransmissionBuilder.Append(t, "SarPickup", null)
            : aboard <= 9 ? TransmissionBuilder.Append(t, "SarPickupCount", aboard)   // counts are recorded 2-9
            : TransmissionBuilder.Append(t, "SarPickupMany", null);
        if (full)
            said |= TransmissionBuilder.Append(t, "SarFull", null);
        if (said)
            SpokenReport.Submit(player, t, alerts);
    }

    public void Landed(ObjectBase unit, ObjectBase deck)
    {
        if (!Own(unit) || !unit.IsAirUnit)
            return;
        int aboard = unit._obp.CurrentTransportedOnBoard.Value;
        if (aboard <= 0 || !delivered.Add(unit.GetInstanceID()))
            return;
        string people = aboard == 1 ? "1 survivor" : $"{aboard} survivors";
        Say(unit, deck, $"${{Name}}: On deck {(deck != null ? "${Target}" : "")} - {people} delivered", "SarDelivered", null);
    }

    private void ReportRafts()
    {
        List<ObjectBase> rafts = newRafts.Where(r => r != null && !r.IsDestroyed).ToList();
        newRafts.Clear();
        if (rafts.Count == 0)
            return;
        ObjectBase first = rafts[0];

        // A sinking ship puts out its rafts one at a time over a minute or more: the first ones are the news, the
        // rafts that follow them into the same patch of water are not (five "survivors in the water" in a row).
        reported.RemoveAll(r => Now - r.when > ReportedMemory);
        int known = reported.FindIndex(r => Distance.Nm(r.at, first._geoPosition) < SameGroupNm);
        if (known >= 0)
        {
            reported[known] = (first._geoPosition, Now);
            Rig.Log.Info($"[sar] {rafts.Count} more raft(s) where survivors were already reported - not called again");
            return;
        }
        reported.Add((first._geoPosition, Now));

        RosterElement speaker = Closest(first);
        ObjectBase from = speaker?.Unit;
        if (from == null)
            return;
        int bearing = Distance.Bearing(from._geoPosition, first._geoPosition);
        int people = rafts.Sum(r => Math.Max(0, r._obp?.CurrentTransportedOnBoard.Value ?? 0));
        string what = rafts.Count == 1 ? "raft" : $"{rafts.Count} rafts";
        string souls = people > 0 ? $", {people} aboard" : "";
        Say(from, first, $"${{Name}}: Survivors in the water{TrackOf(first)} - {what}{souls}, bearing {bearing:000}, {Nm(from, first):0.0} nm",
            "SarSurvivors", bearing, speaker);
    }

    private void Say(ObjectBase unit, ObjectBase about, string text, string phrase, int? number, RosterElement e = null)
    {
        e ??= RosterService.Current?.Resolve(unit);
        GameMessage.RaiseLogged("sar", unit, about, text);
        List<Alert> alerts = AlertLink.TakeFor(unit);
        if (e != null)
            SpokenReport.Say(player, e, phrase, number, alerts);    // no clip for this voice yet: the message has to do
    }

    private static bool Own(ObjectBase unit) =>
        unit != null && !unit.IsDestroyed && unit._taskforce == Globals._playerTaskforce;

    private static bool IsRaft(ObjectBase unit) =>
        unit._obp?._unitRoles != null && unit._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.LifeRaft);

    /// <summary>" (Track 23)" when the player's plot has the raft - the message turns it into a link.</summary>
    private static string TrackOf(ObjectBase raft) =>
        raft.GetMapVehicle(Globals._playerTaskforce) != null ? " (Track ${TrackId})" : "";

    private static RosterElement Closest(ObjectBase raft)
    {
        Roster.Roster roster = RosterService.Current;
        if (roster == null)
            return null;
        return roster.Elements
            .Where(e => e.Unit != null && !e.Unit.IsDestroyed && e.Unit.IsPlayerUnit && !IsRaft(e.Unit))
            .OrderBy(e => (e.Unit.transform.position - raft.transform.position).sqrMagnitude)
            .FirstOrDefault() ?? roster.CommandVoice();
    }

    private static float Nm(ObjectBase from, ObjectBase to) => Distance.Nm(from._geoPosition, to._geoPosition);
}
