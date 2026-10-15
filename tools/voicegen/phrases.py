"""The phrase catalog, and how each piece is recorded.

What makes stitched TTS sound robotic is that every piece is spoken as its own sentence: it starts with a fresh
pitch reset, ends with a full fall, and the pauses between pieces are guessed. So nothing here is rendered on its
own. Every clip is cut out of a *take* -- a whole sentence spoken in one go -- at the word boundaries edge-tts
reports:

    take:   "Viper two-one, | unable, target not in rear aspect."
    keep:    (carrier)        WhyNot_NotInRearAspect_1

The phrase keeps the melody it has in the middle of a real transmission, and the cut sits in the middle of the
natural pause, so callsign + phrase glue back together with exactly the pause the voice made itself. Callsign
words, flight-position pairs and number groups are recorded the same way, each in the slot it will be used in
("<word> two-one", "Viper <flight-pos>", "bearing <three digits>", "track <hi> <lo>"), so any combination
stitches like one utterance (unit selection, the classic trick of natural-sounding concatenative TTS).

Numbers are always written as words -- the TTS reads digits like a calculator. "niner", but not "tree"/"fife".
`sample=True` marks the subset rendered for the listening test.
"""

from __future__ import annotations

from dataclasses import dataclass

DIGIT_WORDS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"]


def digits(number: str) -> str:
    return " ".join(DIGIT_WORDS[int(c)] for c in number)


# ----------------------------------------------------------------------------------------------------------------
# Takes
# ----------------------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Segment:
    text: str              # spoken text of this slice, incl. its punctuation
    key: str | None = None  # output clip name, None = carrier (spoken for context, then thrown away)


@dataclass(frozen=True)
class Take:
    segments: tuple[Segment, ...]
    jitter: int = 3        # rate/pitch wobble for this take (unit-selection pieces get less)
    sample: bool = False

    @property
    def text(self) -> str:
        return " ".join(s.text for s in self.segments)

    @property
    def keys(self) -> list[str]:
        return [s.key for s in self.segments if s.key]


def take(*segments: Segment, jitter: int = 3, sample: bool = False) -> Take:
    return Take(tuple(segments), jitter, sample)


CARRIER_CALLSIGN = "Viper two-one,"
DUMMY_BEARING = "zero niner zero."
DUMMY_TRACK = "seven zero three four."
CALLSIGN_TAIL = "ready for tasking."


# ----------------------------------------------------------------------------------------------------------------
# Phrases. Keys of stock voice.ini events keep the stock name so the plugin can map 1:1.
# `then` = what follows in the transmission ("bearing" / "track"): a dummy number is spoken after the phrase so
# it keeps its "more is coming" melody.
# ----------------------------------------------------------------------------------------------------------------
@dataclass(frozen=True)
class Phrase:
    key: str
    variants: tuple[str, ...]
    priority: int = 6              # same scale as voice.ini: 1 = most urgent, 7 = confirmations
    then: str | None = None        # "bearing" | "track"
    sample: bool = False
    note: str = ""


def P(key: str, *variants: str, priority: int = 6, then: str | None = None, sample: bool = False,
      note: str = "") -> Phrase:
    return Phrase(key, tuple(variants), priority, then, sample, note)


PHRASES: list[Phrase] = [
    # --- Confirmations (stock 1A / 2A) ---------------------------------------------------------------------------
    P("Confirmed", "copy.", "wilco.", "roger, wilco.", priority=7, sample=True),
    P("Negative", "negative.", "negative, unable.", priority=7),
    P("UnableToComply", "unable.", "unable to comply.", priority=7),

    # --- Weapons (stock 1G / 2D) ---------------------------------------------------------------------------------
    P("EngageTrack", "engaging track", "committing on track", priority=4, then="track", sample=True),
    P("AAM_SARH_Fired", "fox one.", "fox one, fox one.", priority=4, sample=True),
    P("AAM_IR_Fired", "fox two.", "fox two, fox two.", priority=4, sample=True),
    P("AircraftGunsFired", "guns, guns.", "guns!", priority=4),
    P("Winchester", "winchester.", "winchester, no more ordnance.", priority=6, sample=True),

    # --- Kills (stock 1H / 2F) -----------------------------------------------------------------------------------
    P("AirHitAir", "splash one!", "splash one bandit!", "good kill, good kill!", priority=3, sample=True),

    # --- Threats (stock 2C) --------------------------------------------------------------------------------------
    P("MissileIncoming", "missile launch, defending!", "missile inbound, breaking!", priority=2, sample=True),

    # --- Fuel / RTB (stock 2A) -----------------------------------------------------------------------------------
    P("BingoFuel", "bingo, bingo. Returning to base.", "bingo fuel, RTB.", priority=2, sample=True),

    # --- Contacts (stock 1F) -------------------------------------------------------------------------------------
    P("NewContact", "new contact, bearing", "contact, bearing", priority=5, then="bearing", sample=True),
    P("ClassifiedHostileAir", "bandit, bandit.", "contact is hostile.", priority=5, sample=True),
    P("ContactFaded", "lost contact.", "contact faded.", priority=5, sample=True),
    P("ContactIntermittent", "contact intermittent.", "contact's in and out.", priority=5,
      note="mod-only: flicker detection"),

    # --- Why-not (mod-only) --------------------------------------------------------------------------------------
    P("WhyNot_NotInRearAspect", "unable, target not in rear aspect.", "negative, no tail shot.",
      priority=4, sample=True, note="EngageState.NotInRearAspect -- R550 head-on"),
    P("WhyNot_TargetTooFar", "unable, target outside envelope.", "negative, out of range.",
      priority=4, sample=True, note="EngageState.TargetTooFar / MissileCanNotReachTarget"),
    P("WhyNot_TargetTooClose", "unable, too close for missiles.", "negative, inside minimum range.",
      priority=4, note="EngageState.TargetTooClose"),
    P("WhyNot_NoRadarLock", "negative, no lock.", "unable, radar can't hold the target.",
      priority=4, sample=True, note="EngageState.TargetIsNotDetectableByFCR / PrelaunchSensorAlignment"),
    P("WhyNot_NoRadarChannel", "unable, radar's busy on another target.", priority=4,
      note="CheckingAbort on a SARH weapon: no free channel"),
    P("WhyNot_NotInFiringArc", "negative, nose not on target.", "stand by, coming around.",
      priority=4, note="EngageState.TargetIsNotInFiringArcs / NotInVerticalArc"),
    P("WhyNot_Defending", "unable, I'm defending!", "negative, defensive, can't commit!",
      priority=3, sample=True, note="aircraft in missile evasion state"),
    P("WhyNot_HoldingFormation", "unable, holding formation on lead.", "negative, I'm tied to lead.",
      priority=6, sample=True, note="T2: wingman VID while MovingInFormation"),
    P("WhyNot_CantSeeTarget", "negative, no visual and no radar on that one.", priority=4,
      note="HandleEngageTasks dropped: CantSeeAirTarget"),
    P("WhyNot_SeekerCantSee", "negative, seeker can't see it in these conditions.", priority=4,
      note="OrderLog SeekerCantSeeTarget"),
    P("WhyNot_NoAmmo", "negative, nothing left for that target.", priority=6, note="OrderLog NoSuitableAmmo"),
    P("WhyNot_WrongTarget", "negative, wrong weapon for that target.", priority=6, note="OrderLog WrongTarget"),
]

# Callsign words from the plugin's Callsigns.cs word lists (kept in sync by hand; see README).
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
SAMPLE_BEARINGS = {"090", "270"}
SAMPLE_TRACK = {"70", "34"}


def build_takes() -> list[Take]:
    takes: list[Take] = []

    # Phrases, spoken after a carrier callsign (and before a dummy number where one follows).
    for phrase in PHRASES:
        for n, text in enumerate(phrase.variants, start=1):
            segs = [Segment(CARRIER_CALLSIGN), Segment(text, f"{phrase.key}_{n}")]
            if phrase.then == "bearing":
                segs.append(Segment(DUMMY_BEARING))
            elif phrase.then == "track":
                segs.append(Segment(DUMMY_TRACK))
            takes.append(take(*segs, jitter=4, sample=phrase.sample))

    # Aircraft callsigns: the word in "<word> two-one" position, the flight-position in "Viper <fp>" position.
    for word in AIR_WORDS:
        takes.append(take(Segment(word, f"cs_{word.lower()}"), Segment("two-one,"), Segment(CALLSIGN_TAIL),
                          jitter=1, sample=word in SAMPLE_WORDS))
    for flight in range(1, 10):
        for pos in range(1, 5):
            fp = f"{flight}{pos}"
            takes.append(take(Segment("Viper"), Segment(f"{DIGIT_WORDS[flight]}-{DIGIT_WORDS[pos]},", f"fp_{fp}"),
                              Segment(CALLSIGN_TAIL), jitter=1, sample=fp in SAMPLE_FLIGHTPOS))
    # Ship / sub / shore callsigns: one word.
    for word in SINGLE_WORDS:
        takes.append(take(Segment(f"{word},", f"cs_{word.lower()}"), Segment(CALLSIGN_TAIL),
                          jitter=1, sample=word in SAMPLE_WORDS))

    # Bearings in 5 degree steps, spoken after "bearing".
    for deg in range(0, 360, 5):
        b = f"{deg:03d}"
        takes.append(take(Segment("bearing"), Segment(f"{digits(b)}.", f"brg_{b}"),
                          jitter=1, sample=b in SAMPLE_BEARINGS))

    # Track numbers: four digits as two pairs. High pair before a dummy low pair, low pair after a dummy high pair.
    for pair in range(100):
        p = f"{pair:02d}"
        takes.append(take(Segment("track"), Segment(digits(p), f"trk_hi_{p}"), Segment("one one."),
                          jitter=1, sample=p in SAMPLE_TRACK))
        takes.append(take(Segment("track seven zero"), Segment(f"{digits(p)}.", f"trk_lo_{p}"),
                          jitter=1, sample=p in SAMPLE_TRACK))
    return takes


TAKES = build_takes()
_keys = [k for t in TAKES for k in t.keys]
assert len(set(_keys)) == len(_keys), "duplicate clip key"
