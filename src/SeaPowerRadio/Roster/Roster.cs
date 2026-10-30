using System.Collections.Generic;
using System.Linq;
using System.Text;
using SeaPower;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Voices;

namespace SeaPowerRadio.Roster;

/// <summary>One voice on the net: a player unit, or an aircraft in a hangar that may launch later.</summary>
internal sealed class RosterElement
{
    public string Key;             // stable across reloads, see Roster.BuildKey*
    public ObjectBase Unit;        // null for an air-wing slot whose aircraft is not airborne
    public string ClassName;
    public CallsignKind Kind;
    public string Callsign;        // generated fallback ("Viper 1-1"), used when the in-game name can't be said
    public VoicePersona Voice;
    public GameName BoundName;     // the in-game name this element's voice was chosen for, if any

    // Air-wing slots only.
    public ObjectBase Carrier;
    public string AircraftIni;
    public string Squadron;
    public int Slot;

    public bool IsAirWingSlot => Carrier != null;
}

/// <summary>
/// Everyone the player can hear in this mission. Built once when the mission is ready: every player unit, then
/// every aircraft each player carrier or airbase holds. Hangar aircraft do not exist as objects until they launch,
/// so they are slots ("Mirage F1, squadron 1, aircraft 3"); a launch claims the lowest free slot of its type and
/// the aircraft speaks with that slot's callsign and voice. Recovery destroys the aircraft object, which frees the
/// slot again for the next launch.
/// </summary>
internal sealed class Roster
{
    private readonly List<RosterElement> elements = new List<RosterElement>();
    private readonly Dictionary<int, RosterElement> byUnit = new Dictionary<int, RosterElement>();
    private readonly Callsigns callsigns;
    private readonly VoiceAssignment voices;

    public string MissionSeed { get; }
    public IReadOnlyList<RosterElement> Elements => elements;

    public Roster(string missionSeed, IEnumerable<VoicePersona> voicePool)
    {
        MissionSeed = missionSeed;
        callsigns = new Callsigns(missionSeed);
        voices = new VoiceAssignment(voicePool, missionSeed);
    }

    public int VoicePoolSize => voices.PoolSize;

    public RosterElement Find(ObjectBase unit)
    {
        if (unit == null) return null;
        byUnit.TryGetValue(unit.GetInstanceID(), out RosterElement element);
        return element;
    }

    /// <summary>Build from the live mission. Order matters for determinism: everything is sorted by stable keys.</summary>
    public void Build()
    {
        List<ObjectBase> units = PlayerUnits().OrderBy(u => u.UniqueID).ToList();

        // Aircraft in a formation share a flight word; positions follow the formation's station order.
        var placedFlights = new HashSet<UnitFormation>();
        foreach (ObjectBase unit in units)
        {
            if (!IsAir(unit)) continue;
            UnitFormation formation = unit.Formation;
            if (formation != null && formation.Stations.Count > 1)
            {
                if (!placedFlights.Add(formation)) continue;
                AddFlight(formation.Stations.Select(s => s.UnitObject).Where(u => u != null).ToList());
            }
            else
            {
                AddFlight(new List<ObjectBase> { unit });
            }
        }

        foreach (ObjectBase unit in units.Where(u => !IsAir(u)))
            AddUnit(unit, KindOf(unit), callsigns.Single(KindOf(unit)));

        foreach (ObjectBase deckOwner in units.Where(u => u._obp?._flightDeck != null))
            AddAirWing(deckOwner);

        // Named units first – their names exist only in a few voices – then everyone else from the pool.
        foreach (RosterElement e in elements.Where(e => e.BoundName != null))
            e.Voice = voices.Prefer(e.Key, e.BoundName.Voices);
        foreach (RosterElement e in elements.Where(e => e.Voice == null))
            e.Voice = voices.For(e.Key);
    }

    /// <summary>
    /// "Female voices" was switched off during the mission: every element with a woman's voice gets a man's (a unit
    /// whose name exists only in women's voices keeps it). Returns how many changed.
    /// </summary>
    public int Revoice()
    {
        int changed = 0;
        foreach (RosterElement e in elements)
        {
            if (e.Voice == null || VoiceAssignment.IsAllowed(e.Voice))
                continue;
            VoicePersona before = e.Voice;
            e.Voice = voices.Rebind(e.Key, e.BoundName?.Voices);
            if (e.Voice != before)
                changed++;
        }
        return changed;
    }

    /// <summary>The in-game name a unit says: a ship's short name, or an aircraft's squadron callsign word.</summary>
    public static GameName NameOf(ObjectBase unit)
    {
        if (unit == null) return null;
        if (IsAir(unit))
            return Names.TryParseAircraft(unit.Name.Value, out string word, out _, out _) ? Names.SquadronWord(word) : null;
        return Names.Unit(unit._obp?._shortName) ?? Names.Unit(unit.getName());
    }

    private void AddFlight(List<ObjectBase> members)
    {
        CallsignKind kind = KindOf(members[0]);
        List<string> names = callsigns.Flight(kind, members.Count);
        for (int i = 0; i < members.Count; i++)
            AddUnit(members[i], kind, names[i]);
    }

    private void AddUnit(ObjectBase unit, CallsignKind kind, string callsign)
    {
        if (Find(unit) != null) return;
        var element = new RosterElement
        {
            Key = "u" + unit.UniqueID,
            Unit = unit,
            ClassName = unit.ClassName,
            Kind = kind,
            Callsign = callsign,
            BoundName = NameOf(unit),
        };
        elements.Add(element);
        byUnit[unit.GetInstanceID()] = element;
    }

    private void AddAirWing(ObjectBase deckOwner)
    {
        FlightDeck deck = deckOwner._obp._flightDeck;
        foreach (VehicleTypeOnBoard type in deck._vehiclesOnBoard.OrderBy(t => t._fileName))
        {
            CallsignKind kind = KindOf(type);
            foreach (Squadron squadron in type.Squadrons.OrderBy(s => s._name))
            {
                if (squadron.Numbers <= 0) continue;
                // Four to a flight, the way the deck launches them.
                List<string> names = callsigns.Flight(kind, squadron.Numbers);
                for (int slot = 0; slot < squadron.Numbers; slot++)
                {
                    var element = new RosterElement
                    {
                        Key = $"w{deckOwner.UniqueID}|{type._fileName}|{squadron._name}|{slot}",
                        ClassName = type.DisplayName,
                        Kind = kind,
                        Callsign = names[slot],
                        Carrier = deckOwner,
                        AircraftIni = type._fileName,
                        Squadron = squadron._name,
                        Slot = slot,
                    };
                    elements.Add(element);
                }
            }
        }
    }

    /// <summary>
    /// A deck launched an aircraft: bind it to the lowest free slot of its type and squadron on that deck. Anything
    /// not foreseen in the roster (air support, a squadron added mid-mission) gets a fresh element.
    /// </summary>
    public RosterElement BindLaunch(ObjectBase deckOwner, string aircraftIni, string squadron, ObjectBase aircraft)
    {
        RosterElement existing = Find(aircraft);
        if (existing != null) return existing;

        RosterElement slot = elements.FirstOrDefault(e =>
            e.IsAirWingSlot && e.Carrier == deckOwner && e.AircraftIni == aircraftIni && e.Squadron == squadron
            && e.Unit == null); // Unity null: a recovered aircraft's object is destroyed → slot free again
        if (slot == null)
            return AddLate(aircraft);

        slot.Unit = aircraft;
        byUnit[aircraft.GetInstanceID()] = slot;
        // The deck picks the callsign at launch ("Lancer 3"); that word only exists in a few voices.
        GameName name = NameOf(aircraft);
        if (name != null && !voices.IsAmong(slot.Voice, name.Voices))
        {
            slot.BoundName = name;
            slot.Voice = voices.Prefer(slot.Key + "|" + name.Key, name.Voices);
        }
        return slot;
    }

    /// <summary>A player unit that was not there at mission start (trigger-enabled, air support…).</summary>
    public RosterElement AddLate(ObjectBase unit)
    {
        RosterElement existing = Find(unit);
        if (existing != null) return existing;
        CallsignKind kind = KindOf(unit);
        if (IsAir(unit))
            AddFlight(new List<ObjectBase> { unit });
        else
            AddUnit(unit, kind, callsigns.Single(kind));
        RosterElement added = Find(unit);
        if (added != null && added.Voice == null)
            added.Voice = added.BoundName != null ? voices.Prefer(added.Key, added.BoundName.Voices) : voices.For(added.Key);
        return added;
    }

    /// <summary>
    /// The element that speaks for a unit: its roster entry, or a late entry for a player unit nobody foresaw
    /// (air support, a unit enabled by a trigger). Null for units that aren't the player's.
    /// </summary>
    public RosterElement Resolve(ObjectBase unit)
    {
        if (unit == null) return null;
        RosterElement element = Find(unit);
        if (element != null) return element;
        return unit.IsPlayerObject && unit.isUnit() ? AddLate(unit) : null;
    }

    /// <summary>
    /// Who speaks for the task force when the game reports without a speaker (mission updates, intel): the first
    /// player ship still afloat, else any element that has a unit.
    /// </summary>
    public RosterElement CommandVoice()
    {
        RosterElement ship = elements.FirstOrDefault(e => !e.IsAirWingSlot && e.Unit != null && !e.Unit.IsDestroyed
                                                         && (e.Kind == CallsignKind.Ship || e.Kind == CallsignKind.Shore));
        return ship ?? elements.FirstOrDefault(e => e.Unit != null && !e.Unit.IsDestroyed) ?? elements.FirstOrDefault();
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Roster for mission '{MissionSeed}': {elements.Count} elements, voice pool {voices.PoolSize}");
        foreach (RosterElement e in elements)
        {
            string who = e.IsAirWingSlot
                ? $"[hangar {e.Carrier.getName()} / {e.Squadron} #{e.Slot + 1}]"
                : e.Unit.getUIDAndName();
            string says = e.BoundName != null ? e.BoundName.Text + (e.BoundName.Squadron ? " (squadron)" : "") : "gen: " + e.Callsign;
            sb.AppendLine($"  {says,-24} {e.Voice?.Id ?? "-",-10} {e.Kind,-10} {e.ClassName,-22} {who}");
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------------------------------------------------

    private static IEnumerable<ObjectBase> PlayerUnits()
    {
        Taskforce player = Globals._playerTaskforce;
        if (player == null) yield break;
        // Active units plus those a trigger will enable later — they should have their voice from the start.
        foreach (ObjectBase unit in player._taskforceUnits.Concat(player._taskforceDisabledUnits))
        {
            if (unit == null || !unit.isUnit()) continue;
            if (unit._obp != null && unit._obp._unitRoles.Contains(ObjectBaseParameters.UnitRoles.LifeRaft)) continue;
            if (unit._type == ObjectBase.ObjectType.Biologic) continue;
            yield return unit;
        }
    }

    private static bool IsAir(ObjectBase unit) =>
        unit._type == ObjectBase.ObjectType.Aircraft || unit._type == ObjectBase.ObjectType.Helicopter;

    private static CallsignKind KindOf(ObjectBase unit)
    {
        switch (unit._type)
        {
            case ObjectBase.ObjectType.Vessel: return CallsignKind.Ship;
            case ObjectBase.ObjectType.Submarine: return CallsignKind.Submarine;
            case ObjectBase.ObjectType.LandUnit: return CallsignKind.Shore;
            case ObjectBase.ObjectType.Helicopter: return CallsignKind.Helicopter;
            default: return KindOfAircraft(unit._obp?._unitRoles);
        }
    }

    private static CallsignKind KindOf(VehicleTypeOnBoard type) =>
        type._type == VehicleType.Helicopter ? CallsignKind.Helicopter : KindOfAircraft(type._unitRoles);

    private static CallsignKind KindOfAircraft(List<ObjectBaseParameters.UnitRoles> roles)
    {
        if (roles == null) return CallsignKind.Fighter;
        bool Has(ObjectBaseParameters.UnitRoles r) => roles.Contains(r);
        if (Has(ObjectBaseParameters.UnitRoles.AEW) || Has(ObjectBaseParameters.UnitRoles.EW)
            || Has(ObjectBaseParameters.UnitRoles.ESM) || Has(ObjectBaseParameters.UnitRoles.Recon)
            || Has(ObjectBaseParameters.UnitRoles.Transport) || Has(ObjectBaseParameters.UnitRoles.Targeting))
            return CallsignKind.Support;
        if (Has(ObjectBaseParameters.UnitRoles.MPA) || Has(ObjectBaseParameters.UnitRoles.ASW))
            return CallsignKind.Patrol;
        if (Has(ObjectBaseParameters.UnitRoles.Fighter) || Has(ObjectBaseParameters.UnitRoles.AAW))
            return CallsignKind.Fighter;
        if (Has(ObjectBaseParameters.UnitRoles.Attack) || Has(ObjectBaseParameters.UnitRoles.Bomber)
            || Has(ObjectBaseParameters.UnitRoles.HeavyBomber) || Has(ObjectBaseParameters.UnitRoles.SEAD)
            || Has(ObjectBaseParameters.UnitRoles.ASuW))
            return CallsignKind.Attack;
        return CallsignKind.Fighter;
    }
}
