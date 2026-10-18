"""The phrase catalog, and how each piece is recorded.

What makes stitched TTS sound robotic is that every piece is spoken as its own sentence: it starts with a fresh
pitch reset, ends with a full fall, and the pauses between pieces are guessed. So nothing here is rendered on its
own. Every clip is cut out of a *take* -- a whole sentence spoken in one go -- at word boundaries:

    take:   "Viper two-one, | unable, target not in rear aspect."
    keep:    (carrier)        WhyNot_NotInRearAspect_1

The phrase keeps the melody it has in the middle of a real transmission, and the cut sits in the middle of the
natural pause, so callsign + phrase glue back together with exactly the pause the voice made itself. Callsign
words, flight-position pairs and number pieces are recorded the same way, each in the slot it will be used in, so
any combination stitches like one utterance (unit selection, the classic trick of concatenative TTS).

Phrase text may hold ONE number slot: {trk} track number, {brg} bearing / course (3 digits), {num} a number up to
three digits (depth), {alt} altitude, {count} 2-9. The phrase is then cut into <key>_<n>_a (before the number) and
<key>_<n>_b (after it, if anything follows); a dummy number is spoken in the slot so both halves keep the melody
they have around a real number. Without a slot the clip is <key>_<n>.

Numbers are assembled by the plugin from pieces:
    track 7034      -> pm_70 + pe_34            (pair mid + pair end: "seven zero, three four")
    bearing 273     -> brg_273                  (a whole bearing)
    depth 150       -> dm_1 + pe_50             ("one, five zero")
    depth 50        -> pe_50
    altitude 15000  -> alt_15  ("angels fifteen"); below 1000 ft: alt_lo_5 ("five hundred feet")
    count 3         -> cnt_3

Write numbers as words -- the TTS reads digits like a calculator. "niner", not "tree"/"fife".
`sample=True` marks the subset rendered for listening tests.

STOCK_MAP at the bottom maps every stock voice.ini key the game plays to the catalog key that replaces it
(None = intentionally silent, the mod builds that report itself).
"""

from __future__ import annotations

import re
from pathlib import Path
from dataclasses import dataclass

DIGIT_WORDS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"]
COUNT_WORDS = {2: "two", 3: "three", 4: "four", 5: "five", 6: "six", 7: "seven", 8: "eight", 9: "nine"}
TENS = {2: "twenty", 3: "thirty", 4: "forty", 5: "fifty", 6: "sixty"}
TEENS = ["ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
         "nineteen"]
ONES = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]


def digits(number: str) -> str:
    return " ".join(DIGIT_WORDS[int(c)] for c in number)


def cardinal(n: int) -> str:
    """1..69 as words ("angels twenty-five")."""
    if n < 10:
        return ONES[n]
    if n < 20:
        return TEENS[n - 10]
    tens, ones = divmod(n, 10)
    return TENS[tens] + ("-" + ONES[ones] if ones else "")


# ----------------------------------------------------------------------------------------------------------------
# Takes
# ----------------------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Segment:
    text: str               # spoken text of this slice, incl. its punctuation
    key: str | None = None  # output clip name, None = context only (spoken, then thrown away)


@dataclass(frozen=True)
class Take:
    segments: tuple[Segment, ...]
    jitter: int = 3          # prosody wobble for engines that take it (edge-tts)
    sample: bool = False
    priority: int = 6        # drives delivery (Chatterbox exaggeration)

    @property
    def text(self) -> str:
        return " ".join(s.text for s in self.segments)

    @property
    def keys(self) -> list[str]:
        return [s.key for s in self.segments if s.key]


def take(*segments: Segment, jitter: int = 3, sample: bool = False, priority: int = 6) -> Take:
    return Take(tuple(segments), jitter, sample, priority)


CARRIER_CALLSIGN = "Viper two-one,"
CALLSIGN_TAIL = "ready for tasking."
DUMMY = {
    "trk": "seven zero, three four",
    "brg": "zero niner zero",
    "num": "one, five zero",
    "alt": "angels twenty",
    "count": "three",
}


# ----------------------------------------------------------------------------------------------------------------
# Phrases
# ----------------------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Phrase:
    key: str
    variants: tuple[str, ...]
    priority: int = 6        # voice.ini scale: 1 abandon, 2 hit/incoming, 3 kill, 4 firing, 5 contacts, 6 nav, 7 ack
    sample: bool = False
    note: str = ""


def P(key: str, *variants: str, priority: int = 6, sample: bool = False, note: str = "") -> Phrase:
    return Phrase(key, tuple(variants), priority, sample, note)


PHRASES: list[Phrase] = [
    # === Acknowledgements =========================================================================================
    P("Confirmed", "copy.", "wilco.", "roger, wilco.", priority=7, sample=True),
    P("Affirmative", "affirmative.", "that's affirm.", priority=7),
    P("Negative", "negative.", "negative, unable.", priority=7),
    P("UnableToComply", "unable.", "unable to comply.", priority=7),

    # === Movement =================================================================================================
    P("TurnToCourse", "coming to course {brg}.", "steady on {brg}.", priority=6),
    P("TurnAircraft", "roger, turning {brg}.", "heading {brg}.", priority=6),
    P("Moving", "moving.", "on the way.", priority=6, note="land units"),
    P("OnStation", "on station.", "in position.", priority=6),
    P("ReturningToFormation", "returning to station.", "rejoining the formation.", priority=6),
    P("ReturningToFormationAir", "rejoining on lead.", "coming back to the flight.", priority=6),
    P("SetAltitude", "climbing, {alt}.", "leveling off, {alt}.", priority=6),
    P("SetSpeedSlow", "slowing down.", "reducing speed.", priority=6),
    P("SetSpeedFull", "ahead full.", "increasing speed.", priority=6),
    P("SetSpeedFlank", "ahead flank!", "flank speed.", priority=6),
    P("AllStop", "all stop.", "all engines stop.", priority=6),
    P("BackFull", "all back full.", priority=6),
    P("SpeedLoiter", "going to loiter speed.", priority=6),
    P("SpeedCruise", "cruise speed.", priority=6),
    P("SpeedMax", "max speed.", "pushing it up.", priority=6),
    P("Afterburner", "going burner.", "in burner.", priority=6),
    P("DropTanks", "dropping tanks.", priority=5),
    P("DropTanksCombat", "tanks gone, I'm clean.", priority=5),

    # === Sensors ==================================================================================================
    P("RadarOn", "radar on.", "radar's up.", priority=6),
    P("RadarOff", "radar off.", "going silent on radar.", priority=6),
    P("SonarActiveOn", "going active on sonar.", "pinging.", priority=6),
    P("SonarActiveOff", "sonar passive.", priority=6),
    P("TowedArrayOut", "streaming the array.", "towed array deploying.", priority=6),
    P("TowedArrayIn", "recovering the array.", priority=6),
    P("Emcon", "going EMCON.", "all emitters off.", priority=6),
    P("DippingSonarDeploy", "in the dip.", "dipping now.", priority=6),
    P("DippingSonarRetract", "breaking dip.", priority=6),
    P("BeingJammed", "I'm being jammed.", "heavy jamming on my scope.", priority=5),

    # === Contacts =================================================================================================
    P("NewContactAir", "new air contact, bearing {brg}.", "contact, air, bearing {brg}.", priority=5, sample=True),
    P("NewContactSurface", "new surface contact, bearing {brg}.", "surface contact, bearing {brg}.", priority=5),
    P("NewContactSubsurface", "possible sub, bearing {brg}.", "subsurface contact, bearing {brg}.", priority=5),
    P("NewContactLand", "ground contact, bearing {brg}.", priority=5),
    P("NewContactsMultiple", "multiple contacts, bearing {brg}.", "several new contacts, bearing {brg}.",
      priority=5),
    P("EsmContact", "ESM contact, bearing {brg}.", "picking up emissions, bearing {brg}.", priority=5),
    P("RadarEmission", "radar emitter, bearing {brg}.", priority=5),
    P("LaunchTransient", "launch transient, bearing {brg}!", priority=3),
    P("BuoyContact", "buoy's hot.", "contact on the buoys.", priority=5),
    P("MadContact", "MAD, MAD, MAD!", "mad man, marking.", priority=5),
    P("ContactFaded", "lost contact, track {trk}.", "track {trk}, faded.", priority=5, sample=True),
    P("ContactsFaded", "contacts lost, {count}.", priority=5, note="mod aggregation"),
    P("ContactIntermittent", "track {trk}, intermittent.", "track {trk}, in and out.", priority=5,
      note="mod flicker detection"),
    P("ContactRegained", "regained track {trk}.", priority=5),
    P("TrackHostile", "track {trk}, hostile.", "track {trk}, confirmed hostile.", priority=5),
    P("TrackNeutral", "track {trk}, neutral.", "track {trk}, confirmed neutral.", priority=5),
    P("TrackFriendly", "track {trk}, friendly.", "track {trk}, confirmed friendly.", priority=5),
    P("BanditBandit", "bandit, bandit.", "bandits.", priority=5, sample=True),
    P("MerchantTraffic", "looks like merchant traffic.", priority=5),
    # T3 -- who / what identified it
    P("IdByVisual", "visual ID.", "eyes on, visual ID.", priority=5),
    P("IdByEsm", "ID off ESM.", "ESM matches.", priority=5),
    P("IdBySonar", "sonar classification.", priority=5),
    P("IdByRadar", "radar classification.", priority=5),
    P("IdByFiredUpon", "they're shooting at us.", "they just fired.", priority=4),
    P("IdByIntel", "per intel.", "command reports.", priority=5),
    P("IdByDatalink", "on the link.", priority=5),

    # === Weapons ==================================================================================================
    P("EngageTrack", "engaging track {trk}.", "committing on track {trk}.", priority=4, sample=True),
    P("HoldFireTrack", "holding fire on track {trk}.", priority=4),
    P("CeaseFire", "cease fire.", "check fire, check fire.", priority=4),
    P("SamReady", "SAMs ready.", priority=4),
    P("SamAway", "birds away!", "missile away!", priority=4),
    P("SalvoFire", "salvo away.", "rounds complete.", priority=4),
    P("AshmAway", "harpoon away!", "anti-ship away!", priority=4),
    P("AsumAway", "torpedo away.", "fish in the water.", priority=4),
    P("AAM_SARH_Fired", "fox one.", "fox one, fox one.", priority=4, sample=True),
    P("AAM_IR_Fired", "fox two.", "fox two, fox two.", priority=4, sample=True),
    P("AAM_ARS_Fired", "fox three.", "fox three, fox three.", priority=4),
    P("Magnum", "magnum, magnum.", priority=4),
    P("BombsAway", "bombs away.", "pickle, pickle.", priority=4),
    P("BuoyAway", "buoy away.", priority=4),
    P("TorpedoAwayAir", "torpedo away.", priority=4),
    P("WeaponAway", "weapon away.", priority=4),
    P("GunsGuns", "guns, guns.", "guns!", priority=4),
    P("ChaffOut", "chaff out.", "chaff, chaff.", priority=4),
    P("DecoyOut", "decoys away.", priority=4),
    P("Jamming", "jamming track {trk}.", priority=4),
    P("StopJamming", "jammer off.", priority=4),
    P("Lasing", "lasing track {trk}.", priority=4),
    P("StopLasing", "laser off.", priority=4),
    P("Winchester", "winchester.", "winchester, no more ordnance.", priority=6, sample=True),

    # === Hits / kills =============================================================================================
    P("SplashTrack", "splash track {trk}!", "track {trk}, he's down!", priority=3),
    P("HitTrack", "hit on track {trk}!", priority=3),
    P("SplashOne", "splash one!", "splash one bandit!", "good kill, good kill!", priority=3, sample=True),
    P("TargetDestroyed", "target destroyed.", "good hits, target's burning.", priority=4),
    P("Shack", "shack!", "good hits on the ground.", priority=4),

    # === Threats / damage =========================================================================================
    P("MissileIncoming", "missile launch, defending!", "missile inbound, breaking!", priority=2, sample=True),
    P("SamLaunch", "SAM launch, SAM launch!", "SAM in the air, defending!", priority=2),
    P("Vampire", "vampire, vampire, bearing {brg}!", priority=2),
    P("TorpedoIncoming", "torpedo in the water, bearing {brg}!", priority=2),
    P("RadarLock", "we're being painted, bearing {brg}!", priority=2),
    P("UnderFire", "taking fire!", "we're under fire!", priority=2),
    P("Break", "break, break!", priority=2),
    P("WeAreHit", "we're hit!", "we've been hit!", priority=2),
    P("DamagedNeedHelp", "we're damaged, need assistance.", priority=2),
    P("Flooding", "flooding, we're losing her!", priority=1),
    P("AbandonShip", "abandon ship! abandon ship!", priority=1),
    P("Ejecting", "I'm hit, I'm going down!", "ejecting, ejecting!", priority=1),

    # === Fuel / RTB / air ops =====================================================================================
    P("BingoFuel", "bingo, bingo. Returning to base.", "bingo fuel, RTB.", priority=2, sample=True),
    # The mod's own fuel calls (Air/FuelMonitor.cs), against the nearest deck or field that can take the aircraft:
    # joker = at cruise speed only just enough left to get there, bingo = only just enough even at the most
    # economical speed, emergency = not enough at all.
    P("FuelJoker", "joker fuel.", "joker, joker.", "we're at joker.", priority=4,
      note="mod: at cruise speed only just enough fuel to reach the nearest deck"),
    P("FuelBingo", "bingo fuel, request RTB.", "bingo, bingo. We need to head for a deck now.", priority=2,
      note="mod: only just enough fuel to reach the nearest deck at the most economical speed"),
    P("FuelEmergency", "fuel emergency, we won't make it to a deck.",
      "emergency fuel. We need a tanker or we're going in the water.", priority=1,
      note="mod: not enough fuel to reach any deck"),

    # Search and rescue (Air/SarReporter.cs): survivors in the water -> rescuer on the way -> pickup -> full ->
    # delivered on deck.
    P("SarSurvivors", "survivors in the water, bearing {brg}.", "raft in the water, bearing {brg}.", priority=4,
      note="mod: own life rafts appeared (ship sunk, aircrew down)"),
    P("SarEnRoute", "proceeding to the survivors.", "rescue en route.", priority=5,
      note="mod: a SAR zone sent this unit to a raft"),
    P("SarPickup", "one survivor aboard.", "pickup complete, one aboard.", priority=4,
      note="mod: pickup, one aboard"),
    P("SarPickupCount", "pickup complete, {count} souls aboard.", "survivors aboard, {count} total.", priority=4,
      note="mod: pickup, 2-9 aboard"),
    P("SarFull", "cabin's full, no more room.", "we're full up, can't take any more.", priority=4,
      note="mod: transport capacity reached"),
    P("SarDelivered", "survivors delivered.", "on deck, survivors are off.", priority=6,
      note="mod: landed with survivors aboard"),
    P("ReturnToBase", "RTB.", "returning to base.", priority=6),
    P("AircraftLaunching", "launching aircraft.", priority=6),
    P("AllAircraftLaunched", "all aircraft away.", priority=6),
    P("AllAircraftRecovered", "all aircraft recovered.", priority=6),
    P("HeloLaunching", "launching the helo.", priority=6),
    P("HeloLaunched", "helo's airborne.", priority=6),
    P("HeloRecovered", "helo recovered.", priority=6),
    P("DeckDamaged", "flight deck damaged.", priority=6),
    P("ColdCat", "cold cat!", priority=6),
    P("WaveOff", "wave off, wave off.", priority=6),

    # === Submarine ================================================================================================
    P("MakeDepth", "make my depth, {num} feet.", priority=6),
    P("PeriscopeDepth", "periscope depth.", priority=6),
    P("Surface", "surface the boat.", priority=6),
    P("EmergencySurface", "emergency surface!", priority=2),
    P("EmergencyDeep", "emergency deep!", priority=2),
    P("ScopeUp", "up scope.", priority=6),
    P("ScopeDown", "down scope.", priority=6),
    P("MastUp", "raising the mast.", priority=6),
    P("MastDown", "lowering the mast.", priority=6),
    P("SnorkelUp", "snorkeling.", priority=6),
    P("SnorkelDown", "secure snorkel.", priority=6),
    P("SilentRunning", "rig for quiet.", priority=6),
    P("BatteryLow", "battery's getting low.", priority=6),
    P("NeedToCharge", "we need to charge batteries.", priority=6),

    # === ROE ======================================================================================================
    P("WeaponsFree", "weapons free.", priority=7),
    P("WeaponsTight", "weapons tight.", priority=7),
    P("WeaponsHold", "weapons hold.", priority=7),

    # === Command / mission ========================================================================================
    P("NewIntel", "new intel received.", priority=2),
    P("NewTasking", "new tasking.", priority=2),
    P("MissionComplete", "mission complete.", priority=2),
    P("MissionFailed", "mission failed.", priority=2),
    P("ObjectiveComplete", "objective complete.", priority=2),
    P("ObjectiveFailed", "objective failed.", priority=2),
    P("Identify", "VID on track {trk}.", "going for a visual on track {trk}.", priority=4),

    # === Why-not (mod-only) =======================================================================================
    P("WhyNot_NotInRearAspect", "unable, target not in rear aspect.", "negative, no tail shot.",
      priority=4, sample=True, note="EngageState.NotInRearAspect -- R550 head-on"),
    P("WhyNot_TargetTooFar", "unable, target outside envelope.", "negative, out of range.",
      priority=4, sample=True, note="EngageState.TargetTooFar / TargetTooFarToOpenSystem"),
    P("WhyNot_CantReach", "negative, missile can't reach him.", priority=4,
      note="EngageState.MissileCanNotReachTarget"),
    P("WhyNot_TargetTooClose", "unable, too close for missiles.", "negative, inside minimum range.",
      priority=4, note="EngageState.TargetTooClose"),
    P("WhyNot_NoRadarLock", "negative, no lock.", "unable, radar can't hold the target.",
      priority=4, sample=True, note="EngageState.TargetIsNotDetectableByFCR / PrelaunchSensorAlignment"),
    P("WhyNot_BeyondRadarHorizon", "negative, he's below my radar horizon.", priority=4,
      note="EngageState.TargetIsBeyondFCRHorizon"),
    P("WhyNot_NoRadarChannel", "unable, radar's busy on another target.", priority=4,
      note="CheckingAbort on a SARH weapon: no free channel"),
    P("WhyNot_NotInFiringArc", "negative, nose not on target.", "stand by, coming around.",
      priority=4, note="EngageState.TargetIsNotInFiringArcs / NotInVerticalArc"),
    P("WhyNot_NoLineOfSight", "negative, no line of sight.", priority=4, note="EngageState.NoLOSToTarget"),
    P("WhyNot_TooLow", "unable, too low to launch.", priority=4, note="EngageState.LauncherTooLow"),
    P("WhyNot_TooHigh", "unable, too high to launch.", priority=4, note="EngageState.LauncherTooHigh"),
    P("WhyNot_SpeedWrong", "unable, wrong speed for launch.", priority=4, note="EngageState.SpeedTooLow/High"),
    P("WhyNot_SeekerCantSee", "negative, seeker can't see it in these conditions.", priority=4,
      note="EngageState.TVSeekerCantSeeTarget / OrderLog SeekerCantSeeTarget"),
    P("WhyNot_Defending", "unable, I'm defending!", "negative, defensive, can't commit!",
      priority=3, sample=True, note="aircraft in missile evasion state"),
    P("WhyNot_HoldingFormation", "unable, holding formation on lead.", "negative, I'm tied to lead.",
      priority=6, sample=True, note="T2: wingman VID while MovingInFormation"),
    P("WhyNot_VidOutOfFuel", "unable, not enough gas for that intercept.", priority=6,
      note="VID aborted: beyond range"),
    P("WhyNot_VidCantIntercept", "unable, can't catch him.", priority=6, note="VID aborted: too slow"),
    P("WhyNot_CantSeeTarget", "negative, no visual and no radar on that one.", priority=4,
      note="HandleEngageTasks dropped: CantSeeAirTarget"),
    P("WhyNot_NoAmmo", "negative, nothing left for that target.", priority=6, note="OrderLog NoSuitableAmmo"),
    P("WhyNot_WrongTarget", "negative, wrong weapon for that target.", priority=6, note="OrderLog WrongTarget"),
    P("WhyNot_TargetRequired", "say target?", "need a target for that.", priority=6,
      note="OrderLog TargetRequired"),
    P("WhyNot_NotControllable", "unable, we're bingo and heading home.", priority=6,
      note="IsControllable false after BingoFuel"),
    P("WhyNot_NoMount", "stand by, no launcher free.", priority=4, note="isWeaponSystemUsable: busy/reloading"),
    P("WhyNot_Evaluating", "stand by, still evaluating the track.", priority=4,
      note="AI.IsProcessedForEngagement (OODA)"),
    P("WhyNot_CannotJam", "negative, can't jam that one.", priority=6, note="OrderLog CannotJam"),
    P("WhyNot_CannotLaser", "negative, can't put the laser on that.", priority=6, note="OrderLog CannotLaser"),
    P("WhyNot_SeaState", "unable, sea's too rough.", priority=6, note="flight deck / mast sea state"),
    P("WhyNot_RebaseNoSpace", "negative, no room on deck.", priority=6, note="CanNotRebase + RebaseNoSpace"),
    P("WhyNot_RebaseTooFar", "negative, too far to make it.", priority=6, note="CanNotRebase + RebaseTooFar"),
    P("WhyNot_RebaseUnsupported", "negative, can't take that type aboard.", priority=6,
      note="CanNotRebase + RebaseUnsupported"),
    P("WhyNot_ReloadIn1", "stand by, reloading, ready in one minute.", "reloading, give me one minute.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn2", "stand by, reloading, ready in two minutes.", "reloading, give me two minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn3", "stand by, reloading, ready in three minutes.", "reloading, give me three minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn4", "stand by, reloading, ready in four minutes.", "reloading, give me four minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn5", "stand by, reloading, ready in five minutes.", "reloading, give me five minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn6", "stand by, reloading, ready in six minutes.", "reloading, give me six minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn7", "stand by, reloading, ready in seven minutes.", "reloading, give me seven minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn8", "stand by, reloading, ready in eight minutes.", "reloading, give me eight minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn9", "stand by, reloading, ready in nine minutes.", "reloading, give me nine minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadIn10", "stand by, reloading, ready in ten minutes.", "reloading, give me ten minutes.", priority=4,
      note="fire order while the weapon system reloads (_isReloading, _remainingReloadTime)"),
    P("WhyNot_ReloadSoon", "stand by, reloading, ready in under a minute.", "reloading, almost there.", priority=4),
    P("WhyNot_ReloadLong", "stand by, reloading, that'll take a while.", priority=4),
    P("WhyNot_SonarOnly", "negative, that's a sonar contact only, no targeting track.",
      "unable, sonar track only, no fire control solution.", priority=4,
      note="radar / ARS weapon ordered against a contact held only by sonar"),
    P("WhyNot_NoSensorTrack", "unable, no sensor track on that target.", priority=4,
      note="EngageState.TargetIsNotDetectableBySearchSensor"),
    P("WhyNot_NotAllowed", "negative, not cleared to engage.", priority=4, note="EngageState.EngagementNotAllowed"),
    P("Queued_2", "copy, queued, number two in line.", priority=6, note="order stacked behind another"),
    P("Queued_3", "copy, queued, number three in line.", priority=6),
    P("Queued_4", "copy, queued, number four in line.", priority=6),
    P("Queued_5", "copy, queued, five in line.", priority=6),
]

# ----------------------------------------------------------------------------------------------------------------
# Callsign words -- MUST match src/SeaPowerRadio/Voices/Callsigns.cs (checked by check_sync.py).
# ----------------------------------------------------------------------------------------------------------------
AIR_WORDS = [
    "Viper", "Cobra", "Falcon", "Razor", "Lancer", "Sabre", "Jester", "Rapier", "Talon", "Venom", "Ghost", "Raptor",
    "Hammer", "Anvil", "Mace", "Dagger", "Tusk", "Spear", "Brick", "Boxer",
    "Trident", "Pelican", "Albatross", "Corsair", "Neptune", "Marlin",
    "Seahawk", "Dipper", "Lynx", "Kingfish", "Dusty", "Rotor",
    "Magic", "Drover", "Tankard", "Wizard", "Mercury", "Darkstar",
]
SINGLE_WORDS = [
    "Bulwark", "Sentinel", "Victory", "Warden", "Guardian", "Valiant", "Resolute", "Steadfast", "Vigilant",
    "Defiant", "Intrepid", "Courage",
    "Shadow", "Silent", "Deepwater", "Barracuda",
    "Castle", "Bastion", "Harbor", "Lighthouse",
]
CALLSIGN_WORDS = AIR_WORDS + SINGLE_WORDS

SAMPLE_WORDS = {"Viper", "Sentinel"}
SAMPLE_FLIGHTPOS = {"21", "22"}
SAMPLE_NUMBERS = {"brg_090", "brg_273", "pm_70", "pe_34", "pe_35"}

SLOT = re.compile(r"\{(trk|brg|num|alt|count)\}")


def phrase_takes(phrase: Phrase) -> list[Take]:
    takes = []
    for n, text in enumerate(phrase.variants, start=1):
        segs = [Segment(CARRIER_CALLSIGN)]
        m = SLOT.search(text)
        if m is None:
            segs.append(Segment(text, f"{phrase.key}_{n}"))
        else:
            before, after = text[:m.start()].strip(), text[m.end():].strip()
            number = DUMMY[m.group(1)]
            if after and after[0] in ".,!?":   # punctuation right after the number belongs to the number
                number, after = number + after[0], after[1:].strip()
            # A spoken pause on both sides of the number: every cut then falls into silence instead of into running
            # speech, which is what made the first library's numbers sound chopped.
            if before:
                segs.append(Segment(before if before[-1] in ",.!?" else before + ",", f"{phrase.key}_{n}_a"))
            if after:
                segs.append(Segment(number if number[-1] in ",.!?" else number + ","))
                segs.append(Segment(after, f"{phrase.key}_{n}_b"))
            else:
                segs.append(Segment(number.rstrip(".,!?") + "."))
        takes.append(take(*segs, jitter=4, sample=phrase.sample, priority=phrase.priority))
    return takes


def build_takes() -> list[Take]:
    takes: list[Take] = []
    for phrase in PHRASES:
        takes += phrase_takes(phrase)

    # Aircraft callsigns: the word in "<word> two-one" position, the flight-position in "Viper <fp>" position.
    for word in AIR_WORDS:
        takes.append(take(Segment(word, f"cs_{word.lower()}"), Segment("two-one,"), Segment(CALLSIGN_TAIL),
                          jitter=1, sample=word in SAMPLE_WORDS))
    for flight in range(1, 10):
        for pos in range(1, 5):
            fp = f"{flight}{pos}"
            takes.append(take(Segment("Viper"), Segment(f"{DIGIT_WORDS[flight]}-{DIGIT_WORDS[pos]},", f"fp_{fp}"),
                              Segment(CALLSIGN_TAIL), jitter=1, sample=fp in SAMPLE_FLIGHTPOS))
    for word in SINGLE_WORDS:
        takes.append(take(Segment(f"{word},", f"cs_{word.lower()}"), Segment(CALLSIGN_TAIL),
                          jitter=1, sample=word in SAMPLE_WORDS))

    # Number pieces, each recorded between spoken pauses so the cuts fall into silence:
    #   brg_DDD  a whole bearing / course ("bearing, two seven three.")
    #   pm_PP    first pair of a track number, more to come ("track, seven zero, one one.")
    #   pe_PP    closing pair ("track, seven zero, three four.")
    #   dm_D     leading digit of a three-digit number (depth: "depth, one, five zero.")
    for deg in range(360):
        b = f"{deg:03d}"
        takes.append(take(Segment("bearing,"), Segment(f"{digits(b)}.", f"brg_{b}"),
                          jitter=1, sample=f"brg_{b}" in SAMPLE_NUMBERS))
    for d in range(10):
        takes.append(take(Segment("depth,"), Segment(f"{DIGIT_WORDS[d]},", f"dm_{d}"), Segment("five zero."),
                          jitter=1, sample=f"dm_{d}" in SAMPLE_NUMBERS))
    for pair in range(100):
        p = f"{pair:02d}"
        takes.append(take(Segment("track,"), Segment(f"{digits(p)},", f"pm_{p}"), Segment("one one."),
                          jitter=1, sample=f"pm_{p}" in SAMPLE_NUMBERS))
        takes.append(take(Segment("track, seven zero,"), Segment(f"{digits(p)}.", f"pe_{p}"),
                          jitter=1, sample=f"pe_{p}" in SAMPLE_NUMBERS))
    # Altitude: angels 1..60 (thousands of feet), and the low ones in hundreds.
    for k in range(1, 61):
        takes.append(take(Segment("climbing,"), Segment(f"angels {cardinal(k)}.", f"alt_{k}"), jitter=1))
    for h in (1, 2, 3, 5, 8):
        takes.append(take(Segment("descending,"), Segment(f"{ONES[h]} hundred feet.", f"alt_lo_{h}"), jitter=1))
    for c, word in COUNT_WORDS.items():
        takes.append(take(Segment("contacts lost,"), Segment(f"{word}.", f"cnt_{c}"), jitter=1))
    return takes


def callsign_number_takes() -> list[Take]:
    """The number after an in-game callsign word ("Lancer | three"), in every voice."""
    words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven",
             "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"]
    return [take(Segment("Viper"), Segment(f"{words[n]},", f"cn_{n}"), Segment(CALLSIGN_TAIL), jitter=1)
            for n in range(1, 21)]


TAKES = build_takes() + callsign_number_takes()


def name_takes(persona_id: str) -> list[Take]:
    """In-game names bound to this voice (names.py): ship/shore names alone, squadron words before a number."""
    import names as _names
    out = []
    bound = _bindings()
    for n in _names.all_names():
        if persona_id not in bound[n.key]:
            continue
        if n.kind == "unit":
            out.append(take(Segment(n.spoken + ",", n.key), Segment(CALLSIGN_TAIL), jitter=1, priority=6))
        else:
            out.append(take(Segment(n.spoken, n.key), Segment("three,"), Segment(CALLSIGN_TAIL), jitter=1,
                            priority=6))
    return out


_BINDINGS: dict[str, list[str]] | None = None


def _bindings() -> dict[str, list[str]]:
    global _BINDINGS
    if _BINDINGS is None:
        import names as _names
        from cast import CAST
        _BINDINGS = _names.bindings([p.id for p in CAST])
    return _BINDINGS


def takes_for(persona_id: str) -> list[Take]:
    """Everything one voice says: the shared catalog plus the in-game names bound to it."""
    return TAKES + name_takes(persona_id)
_keys = [k for t in TAKES for k in t.keys]
assert len(set(_keys)) == len(_keys), "duplicate clip key"

# ----------------------------------------------------------------------------------------------------------------
# Stock voice.ini key -> catalog key (None = the mod rebuilds this report itself / building block of a stock
# sequence that the mod replaces as a whole). Every key the game can play must be listed (check_sync.py).
# ----------------------------------------------------------------------------------------------------------------
STOCK_MAP: dict[str, str | None] = {
    "Confirmed": "Confirmed", "RogerThat": "Confirmed", "YesCommander": "Confirmed", "Yes": "Confirmed",
    "Affirmative": "Affirmative", "Negative": "Negative", "NegativeAir": "Negative",
    "UnableToComply": "UnableToComply", "CannotComplyNoAmmo": "WhyNot_NoAmmo",
    "ReturningToFormation": "ReturningToFormation", "ReturningToFormationAir": "ReturningToFormationAir",
    "TurnToCourse": "TurnToCourse", "TurnToCourse_Aircraft": "TurnAircraft", "TurnToCourse_Land": "Moving",
    "ChangeCourse": "TurnToCourse", "HelmAye": None, "DiveAye": None,
    "SetSpeed_Ahead_Flank": "SetSpeedFlank", "SetSpeed_Ahead_Full": "SetSpeedFull",
    "SetSpeed_Ahead_Standard": "SetSpeedFull", "SetSpeed_Ahead_2_3": "SetSpeedSlow",
    "SetSpeed_Ahead_1_3": "SetSpeedSlow", "SetSpeed_AllStop": "AllStop", "SetSpeed_BackFull": "BackFull",
    "SetSpeed_Loiter": "SpeedLoiter", "SetSpeed_Cruise": "SpeedCruise", "SetSpeed_Max": "SpeedMax",
    "SetSpeed_Afterburner": "Afterburner", "OnStation": "OnStation", "SetAltitude": "SetAltitude",
    "DropFueltanks": "DropTanks", "DropFueltanksCombat": "DropTanksCombat",
    "ActiveSonarOn": "SonarActiveOn", "ActiveSonarOff": "SonarActiveOff", "VDSDeploy": "TowedArrayOut",
    "VDSRetract": "TowedArrayIn", "TADeploy": "TowedArrayOut", "TARetract": "TowedArrayIn",
    "AirRadarOn": "RadarOn", "AirRadarOff": "RadarOff", "SurfaceRadarOn": "RadarOn", "SurfaceRadarOff": "RadarOff",
    "EMCON": "Emcon", "DippingSonarDeploy": "DippingSonarDeploy", "DippingSonarRetract": "DippingSonarRetract",
    "BeingJammed": "BeingJammed",
    # Contact reports are rebuilt by the mod's contact logic (bearing, type, anti-spam), so the stock building
    # blocks are swallowed.
    "New": None, "NewF": None, "Multiple": None, "NewContact": None, "NewContactMultiple": None,
    "Contact": None, "Contacts": None, "ContactAir": None, "ContactAirF": None, "ContactAirM": None,
    "ContactLand": None, "ContactLandF": None, "ContactLandM": None, "ContactSurface": None,
    "ContactSurfaceF": None, "ContactSurfaceM": None, "ContactSubmerged": None, "ContactSubmergedF": None,
    "ContactSubmergedM": None, "ClassifiedHostileAir": None, "ClassifiedHostileAirMultiple": None,
    "ClassifiedHostileMultiple": None, "ClassifiedContactMultiple": None, "TrackNumber": None,
    "IsClassified": None, "ContactFaded": None,
    "Friendly": None, "Neutral": None, "Hostile": None, "FriendlyF": None, "NeutralF": None, "HostileF": None,
    "FriendlyM": None, "NeutralM": None, "HostileM": None, "FriendlyS": None, "NeutralS": None, "HostileS": None,
    "Aircraft": None, "Helicopter": None, "Vessel": None, "CivShip": None, "Airbase": None, "SAMSite": None,
    "MissileSite": None, "Building": None, "Port": None, "Radar": None, "LandVehicle": None, "Ground": None,
    "SubmergedSubmarine": None, "Biologic": None, "AirTarget": None, "SeaTarget": None, "LandTarget": None,
    "Track": None, "DesignateTrack": None,
    "ESMContact": "EsmContact", "RadarContact": None, "PassiveContact": None, "ActiveContact": None,
    "VisualContact": None, "RadarEmission": "RadarEmission", "LaunchTransient": "LaunchTransient",
    "BouyContact": "BuoyContact", "MADContact": "MadContact",
    "EngageTrack": "EngageTrack", "HoldFireTrack": "HoldFireTrack", "CeaseFire": "CeaseFire",
    "SAMReady": "SamReady", "SAMFired": "SamAway", "ASHMFired": "AshmAway", "ASUMFired": "AsumAway",
    "SalvoFire": "SalvoFire", "LaunchAtTarget": "EngageTrack", "ChaffOut": "ChaffOut", "DecoyOut": "DecoyOut",
    "TargetRequired": "WhyNot_TargetRequired", "WrongTarget": "WhyNot_WrongTarget",
    "Jam": "Jamming", "StopJam": "StopJamming", "DesignateWithLaser": "Lasing", "StopLaser": "StopLasing",
    "CannotJam": "WhyNot_CannotJam", "CannotLaser": "WhyNot_CannotLaser",
    "AAM_SARH_Fired": "AAM_SARH_Fired", "AAM_IR_Fired": "AAM_IR_Fired", "AAM_ARS_Fired": "AAM_ARS_Fired",
    "ARMFiredAir": "Magnum", "TorpedoFiredAir": "TorpedoAwayAir", "BombsAway": "BombsAway",
    "BuoyAway": "BuoyAway", "WeaponAway": "WeaponAway", "AircraftGunsFired": "GunsGuns",
    "Winchester": "Winchester",
    "SplashedTrack": "SplashTrack", "EnemyShipDestroyed": "TargetDestroyed", "HitTrack": "HitTrack",
    "IsHistory": None, "IsToast": None,
    "AirHitAir": "SplashOne", "AirHitSurface": "TargetDestroyed", "AirHitLand": "Shack",
    "MissileIncoming": "MissileIncoming", "MissileIncoming_Sam": "SamLaunch",
    "MissileIncoming_Surface": "Vampire", "EvasiveManuever": "Break", "TorpedoIncoming": "TorpedoIncoming",
    "TargetedByRadar": "RadarLock", "UnderFire": "UnderFire",
    "WeHit": "WeAreHit", "DamagedNeedHelp": "DamagedNeedHelp", "AbandonShip": "AbandonShip",
    "FloodingAbandonShip": "Flooding", "ImGoingDown": "Ejecting",
    "ReturnToBase": "ReturnToBase", "ReturnAir": "ReturnToBase", "BingoFuel": "BingoFuel", "FuelRTB": "BingoFuel",
    "FuelRTBAuto": "BingoFuel",
    "HeloLaunching": "HeloLaunching", "HeloLaunched": "HeloLaunched", "HeloRecovered": "HeloRecovered",
    "AircraftLaunching": "AircraftLaunching", "AllAircraftLaunched": "AllAircraftLaunched",
    "AllAircraftRecovered": "AllAircraftRecovered", "CarrierDeckDamaged": "DeckDamaged", "ColdCat": "ColdCat",
    "WaveOff": "WaveOff",
    "MakeMyDepth": "MakeDepth", "SurfaceTheShip": "Surface", "EmergencySurface": "EmergencySurface",
    "EmergencyDeep": "EmergencyDeep", "PeriscopeDepth": "PeriscopeDepth", "RaiseScope": "ScopeUp",
    "LowerScope": "ScopeDown", "RaiseESM": "MastUp", "LowerESM": "MastDown", "RaiseRadar": "MastUp",
    "LowerRadar": "MastDown", "RaiseSnorkel": "SnorkelUp", "LowerSnorkel": "SnorkelDown",
    "SilentRunning": "SilentRunning", "BatteryLow": "BatteryLow", "NeedToChargeBatteries": "NeedToCharge",
    "WeaponsFree": "WeaponsFree", "WeaponsTight": "WeaponsTight", "WeaponsSafe": "WeaponsHold",
    "NewIntelReceived": "NewIntel", "NewTaskReceived": "NewTasking", "MissionComplete": "MissionComplete",
    "MissionFail": "MissionFailed", "ObjectiveComplete": "ObjectiveComplete", "ObjectiveFailed": "ObjectiveFailed",
    "VisualIdentify": "Identify", "ClassifyTrack": "Identify",
    "Distance": None, "SubSurface": None, "Submarine": None,
    # Built at runtime by Taskforce.CheckContactClassifiedMessage (type + "Multiple"), seen in a real log.
    "ContactAirMultiple": None, "ContactSurfaceMultiple": None, "ContactLandMultiple": None,
    "ContactSubmergedMultiple": None,
}
_phrase_keys = {p.key for p in PHRASES}
_bad = {k: v for k, v in STOCK_MAP.items() if v is not None and v not in _phrase_keys}
assert not _bad, f"STOCK_MAP points at unknown phrases: {_bad}"

# What the number a stock voice call passes means, so the plugin can speak it in the right form.
STOCK_NUMBER: dict[str, str] = {
    "EngageTrack": "trk", "LaunchAtTarget": "trk", "HoldFireTrack": "trk", "SplashedTrack": "trk",
    "HitTrack": "trk", "Jam": "trk", "StopJam": "trk", "DesignateWithLaser": "trk", "StopLaser": "trk",
    "CannotJam": "trk", "CannotLaser": "trk", "VisualIdentify": "trk", "ClassifyTrack": "trk",
    "ContactFaded": "trk", "TorpedoIncoming": "brg", "MissileIncoming": "brg", "MissileIncoming_Surface": "brg",
    "TargetedByRadar": "brg", "ChangeCourse": "brg", "TurnToCourse": "brg", "TurnToCourse_Aircraft": "brg",
    "MakeMyDepth": "num", "SetAltitude": "alt",
}

# How a phrase sits in the mix (Radio/RadioFx in the plugin):
#   reply  - a unit answering the player's order: loudest and cleanest, goes first
#   alert  - urgent (missile inbound, hit, bingo): loud, cuts in
#   report - everything the net says on its own (contacts, launches, status): quieter, in the background
# Default from priority (<=2 alert, 7 reply, else report), with these exceptions:
REPLY_PHRASES = {
    "TurnToCourse", "TurnAircraft", "Moving", "SetAltitude", "SetSpeedSlow", "SetSpeedFull", "SetSpeedFlank",
    "AllStop", "BackFull", "SpeedLoiter", "SpeedCruise", "SpeedMax", "Afterburner", "DropTanks", "DropTanksCombat",
    "RadarOn", "RadarOff", "SonarActiveOn", "SonarActiveOff", "TowedArrayOut", "TowedArrayIn", "Emcon",
    "DippingSonarDeploy", "DippingSonarRetract", "ReturningToFormation", "ReturningToFormationAir",
    "EngageTrack", "HoldFireTrack", "CeaseFire", "Jamming", "StopJamming", "Lasing", "StopLasing", "Identify",
    "ReturnToBase", "MakeDepth", "PeriscopeDepth", "Surface", "ScopeUp", "ScopeDown", "MastUp", "MastDown",
    "SnorkelUp", "SnorkelDown", "SilentRunning", "WeaponsFree", "WeaponsTight", "WeaponsHold",
    "AircraftLaunching", "HeloLaunching",
}


def role_of(p: Phrase) -> str:
    if p.key in REPLY_PHRASES or p.key.startswith(("WhyNot_", "Queued_")) or p.priority >= 7:
        return "reply"
    if p.priority <= 2:
        return "alert"
    return "report"


CATALOG_FILE = Path(__file__).resolve().parents[2] / "mod" / "voices" / "catalog.txt"
STOCKMAP_FILE = Path(__file__).resolve().parents[2] / "mod" / "voices" / "stockmap.txt"


def write_runtime_files() -> None:
    """catalog.txt and stockmap.txt next to the voice library, read by the plugin (Radio/Catalog.cs)."""
    lines = ["# phrase|variant|slot (- = none)|has _a|has _b|priority|role -- generated by tools/voicegen/phrases.py"]
    for p in PHRASES:
        for n, text in enumerate(p.variants, start=1):
            m = SLOT.search(text)
            if m is None:
                lines.append(f"{p.key}|{n}|-|0|0|{p.priority}|{role_of(p)}")
            else:
                has_a = bool(text[:m.start()].strip())
                has_b = bool(text[m.end():].strip().lstrip(".,!?").strip())
                lines.append(f"{p.key}|{n}|{m.group(1)}|{int(has_a)}|{int(has_b)}|{p.priority}|{role_of(p)}")
    CATALOG_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    lines = ["# stock key|phrase (- = swallowed)|number kind (- = none) -- generated by tools/voicegen/phrases.py"]
    for key in sorted(STOCK_MAP):
        lines.append(f"{key}|{STOCK_MAP[key] or '-'}|{STOCK_NUMBER.get(key, '-')}")
    STOCKMAP_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {CATALOG_FILE.name} and {STOCKMAP_FILE.name}")


if __name__ == "__main__":
    write_runtime_files()
