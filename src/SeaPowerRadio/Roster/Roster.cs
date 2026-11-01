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
    public string Callsign;        // generated fallback ("Viper 1-1"), used when the in-game name can't be said; null for Game voice
    public string Nation;          // the unit's own nation (ObjectBase.getNation()), which picks the pack
    public VoicePack Pack;         // the pack that speaks for this element; null = the game's own voice
    public string MemoryKey;       // key in the campaign's voice memory (campaign tag, else the unit's name); null = not remembered
    public VoicePersona Voice;     // null for Game voice (and when the pack has no voice left)
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
    // Callsign words per language (a pack's Language picks the rules), voices per pack: each pack in use shuffles
    // its own pool, so a mixed task force gets every unit a voice of the pack of its own country.
    private readonly Dictionary<string, Callsigns> callsigns = new Dictionary<string, Callsigns>();
    private readonly Dictionary<string, VoiceAssignment> voices = new Dictionary<string, VoiceAssignment>();
    private readonly Dictionary<string, VoicePack> packByNation = new Dictionary<string, VoicePack>(System.StringComparer.OrdinalIgnoreCase);

    public string MissionSeed { get; }
    public IReadOnlyList<RosterElement> Elements => elements;

    private readonly CampaignMemory memory;   // null outside a linear campaign

    public Roster(string missionSeed, CampaignMemory campaign = null)
    {
        MissionSeed = missionSeed;
        memory = campaign;
    }

    /// <summary>Key of a unit in the campaign memory: its campaign tag; failing that a ship's or shore unit's name (aircraft names are rewritten by formations).</summary>
    private string MemoryKeyOf(ObjectBase unit)
    {
        if (memory == null || unit == null) return null;
        if (!string.IsNullOrEmpty(unit._campaignTag)) return "t:" + unit._campaignTag;
        string name = IsAir(unit) ? null : unit.getName();
        return string.IsNullOrEmpty(name) ? null : "n:" + name;
    }

    /// <summary>
    /// Gives the element the voice and callsign the campaign remembers for it. Not when the pack of that country is not the
    /// remembered one (setting changed or pack gone), the voice is gone or not allowed (female voices off), or the unit's name
    /// does not exist in that voice: then it is chosen anew and the memory updated.
    /// </summary>
    private bool ApplyMemory(RosterElement e)
    {
        if (memory == null || e.MemoryKey == null || e.Pack == null || !memory.TryGet(e.MemoryKey, out string voiceId, out string callsign))
            return false;
        int slash = voiceId.IndexOf('/');
        if (slash < 0 || !string.Equals(voiceId.Substring(0, slash), e.Pack.Id, System.StringComparison.OrdinalIgnoreCase))
            return false;
        string persona = voiceId.Substring(slash + 1);
        VoicePersona voice = e.Pack.Personas.FirstOrDefault(p => p.Id == persona);
        if (voice == null || !VoiceAssignment.IsAllowed(voice) || (e.BoundName != null && !e.BoundName.Voices.Contains(persona)))
            return false;
        e.Voice = VoicesOf(e.Pack).Claim(e.Key, voice);
        if (!string.IsNullOrEmpty(callsign) && e.Callsign != null)
            e.Callsign = callsign;
        return true;
    }

    private void Remember(RosterElement e)
    {
        if (memory != null && e.MemoryKey != null && e.Voice != null)
            memory.Set(e.MemoryKey, e.Voice.FullId, e.Callsign);
    }

    /// <summary>The pack for a nation, settled once per mission (a changed setting applies from the next mission).</summary>
    private VoicePack PackForNation(string nation)
    {
        nation ??= "";
        if (!packByNation.TryGetValue(nation, out VoicePack pack))
            packByNation[nation] = pack = VoicePacks.Resolve(nation);
        return pack;
    }

    private Callsigns CallsignsFor(VoicePack pack)
    {
        if (pack == null) return null;
        string language = LanguageRules.For(pack.Language);
        if (!callsigns.TryGetValue(language, out Callsigns c))
        {
            callsigns[language] = c = new Callsigns(MissionSeed);
            if (memory != null)
                foreach (string remembered in memory.Callsigns())
                    c.Reserve(remembered);          // a unit of the campaign keeps its callsign: nobody new gets it
        }
        return c;
    }

    private VoiceAssignment VoicesOf(VoicePack pack)
    {
        if (!voices.TryGetValue(pack.Id, out VoiceAssignment v))
        {
            voices[pack.Id] = v = new VoiceAssignment(pack, MissionSeed);
            if (memory != null)
                foreach (VoicePersona p in pack.Personas)
                    if (memory.IsRemembered(pack.Id + "/" + p.Id))
                        v.Reserve(p.Id);            // remembered voices stay free for their units
        }
        return v;
    }

    private string NationOf(ObjectBase unit) => unit.getNation();

    private void AssignVoice(RosterElement e)
    {
        if (e.Pack == null || e.Voice != null) return;
        if (!ApplyMemory(e))
        {
            VoiceAssignment pool = VoicesOf(e.Pack);
            e.Voice = e.BoundName != null ? pool.Prefer(e.Key, e.BoundName.Voices) : pool.For(e.Key);
        }
        Remember(e);
        memory?.SaveIfDirty();
    }

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
            AddUnit(unit, KindOf(unit), CallsignsFor(PackForNation(NationOf(unit)))?.Single(KindOf(unit)));

        foreach (ObjectBase deckOwner in units.Where(u => u._obp?._flightDeck != null))
            AddAirWing(deckOwner);

        // A campaign's units first get the voices and callsigns it remembers for them.
        foreach (RosterElement e in elements.Where(e => e.Pack != null))
            ApplyMemory(e);

        // Named units next – their names exist only in a few voices – then everyone else from the pool.
        foreach (RosterElement e in elements.Where(e => e.BoundName != null && e.Pack != null && e.Voice == null))
            e.Voice = VoicesOf(e.Pack).Prefer(e.Key, e.BoundName.Voices);
        foreach (RosterElement e in elements.Where(e => e.Voice == null && e.Pack != null))
            AssignVoice(e);

        if (memory != null)
        {
            foreach (RosterElement e in elements)
                Remember(e);
            memory.SaveIfDirty();
            ModLog.Info($"[campaign] '{memory.Id}': {memory.Count} units remembered");
        }
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
            e.Voice = VoicesOf(e.Pack).Rebind(e.Key, e.BoundName?.Voices);
            if (e.Voice != before)
            {
                changed++;
                Remember(e);
            }
        }
        memory?.SaveIfDirty();
        return changed;
    }

    /// <summary>The in-game name a unit says: a ship's short name, or an aircraft's squadron callsign word.</summary>
    public static GameName NameOf(ObjectBase unit, VoicePack pack)
    {
        if (unit == null || pack?.Names == null) return null;
        if (IsAir(unit))
            return Names.TryParseAircraft(unit.Name.Value, out string word, out _, out _) ? pack.Names.SquadronWord(word) : null;
        return pack.Names.Unit(unit._obp?._shortName) ?? pack.Names.Unit(unit.getName());
    }

    private void AddFlight(List<ObjectBase> members)
    {
        CallsignKind kind = KindOf(members[0]);
        // A flight shares its callsign word: the language of the first member a pack speaks for.
        VoicePack lead = members.Select(m => PackForNation(NationOf(m))).FirstOrDefault(p => p != null);
        List<string> names = CallsignsFor(lead)?.Flight(kind, members.Count);
        for (int i = 0; i < members.Count; i++)
            AddUnit(members[i], kind, names != null && PackForNation(NationOf(members[i])) != null ? names[i] : null);
    }

    private void AddUnit(ObjectBase unit, CallsignKind kind, string callsign)
    {
        if (Find(unit) != null) return;
        VoicePack pack = PackForNation(NationOf(unit));
        var element = new RosterElement
        {
            Key = "u" + unit.UniqueID,
            Unit = unit,
            ClassName = unit.ClassName,
            Kind = kind,
            Nation = NationOf(unit),
            Pack = pack,
            Callsign = pack != null ? callsign : null,
            BoundName = NameOf(unit, pack),
            MemoryKey = MemoryKeyOf(unit),
        };
        elements.Add(element);
        byUnit[unit.GetInstanceID()] = element;
    }

    private void AddAirWing(ObjectBase deckOwner)
    {
        FlightDeck deck = deckOwner._obp._flightDeck;
        VoicePack pack = PackForNation(NationOf(deckOwner));
        foreach (VehicleTypeOnBoard type in deck._vehiclesOnBoard.OrderBy(t => t._fileName))
        {
            CallsignKind kind = KindOf(type);
            foreach (Squadron squadron in type.Squadrons.OrderBy(s => s._name))
            {
                if (squadron.Numbers <= 0) continue;
                // Four to a flight, the way the deck launches them.
                List<string> names = CallsignsFor(pack)?.Flight(kind, squadron.Numbers);
                for (int slot = 0; slot < squadron.Numbers; slot++)
                {
                    var element = new RosterElement
                    {
                        Key = $"w{deckOwner.UniqueID}|{type._fileName}|{squadron._name}|{slot}",
                        ClassName = type.DisplayName,
                        Kind = kind,
                        Callsign = names?[slot],
                        Nation = NationOf(deckOwner),
                        Pack = pack,
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
        GameName name = NameOf(aircraft, slot.Pack);
        if (name != null && slot.Pack != null && !VoicesOf(slot.Pack).IsAmong(slot.Voice, name.Voices))
        {
            slot.BoundName = name;
            slot.Voice = VoicesOf(slot.Pack).Prefer(slot.Key + "|" + name.Key, name.Voices);
        }
        // The aircraft knows its campaign tag now (the deck hands it over at launch): the voice it had before, or this one from now on.
        slot.MemoryKey = MemoryKeyOf(aircraft);
        if (slot.MemoryKey != null && slot.Pack != null)
        {
            if (!ApplyMemory(slot))
                Remember(slot);
            memory.SaveIfDirty();
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
            AddUnit(unit, kind, CallsignsFor(PackForNation(NationOf(unit)))?.Single(kind));
        RosterElement added = Find(unit);
        if (added != null)
            AssignVoice(added);
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
        sb.AppendLine($"Roster for mission '{MissionSeed}': {elements.Count} elements, voice pools: " +
                      (voices.Count == 0 ? "none" : string.Join(", ", voices.Select(p => $"{p.Key} {p.Value.PoolSize}"))));
        foreach (RosterElement e in elements)
        {
            string who = e.IsAirWingSlot
                ? $"[hangar {e.Carrier.getName()} / {e.Squadron} #{e.Slot + 1}]"
                : e.Unit.getUIDAndName();
            string says = e.Pack == null ? "(game voice)"
                : e.BoundName != null ? e.BoundName.Text + (e.BoundName.Squadron ? " (squadron)" : "") : "gen: " + e.Callsign;
            sb.AppendLine($"  {says,-24} {e.Voice?.FullId ?? "-",-18} {e.Nation,-10} {e.Kind,-10} {e.ClassName,-22} {who}");
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
