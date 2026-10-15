"""The phrase catalog: everything a unit can say, as text for the TTS.

Two kinds of entries:

* PHRASES -- whole sentences ("Unable, target not in rear aspect."). Each has a few text variants so a unit never
  says the exact same line twice in a row. Rendered per voice as <key>_<n>.
* FRAGMENTS -- pieces the plugin stitches together at runtime: callsign words, flight-position pairs ("two-one"),
  digits. Every digit exists twice: a *mid* take (rendered with a comma, so the pitch stays up because more is
  coming) and a *final* take (rendered with a full stop, pitch falls). Stitching mid…mid…final is what keeps
  "bearing zero niner zero" from sounding like a calculator.

Numbers are always written as words -- the TTS reads digits like a calculator. Aviation pronunciation: "niner",
"tree" is deliberately NOT used (sounds like a mistake to most players).

`sample=True` marks the subset rendered for the Phase 2 listening test.
"""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True)
class Phrase:
    key: str
    variants: tuple[str, ...]
    priority: int = 6              # same scale as voice.ini: 1 = most urgent, 7 = confirmations
    sample: bool = False
    note: str = ""


@dataclass(frozen=True)
class Fragment:
    key: str
    text: str
    sample: bool = False


def P(key: str, *variants: str, priority: int = 6, sample: bool = False, note: str = "") -> Phrase:
    return Phrase(key, tuple(variants), priority, sample, note)


# ----------------------------------------------------------------------------------------------------------------
# Phrases. Keys of stock voice.ini events keep the stock name so the plugin can map 1:1.
# ----------------------------------------------------------------------------------------------------------------
PHRASES: list[Phrase] = [
    # --- Confirmations (stock 1A / 2A) ---------------------------------------------------------------------------
    P("Confirmed", "Copy.", "Wilco.", "Roger, wilco.", priority=7, sample=True),
    P("Negative", "Negative.", "Negative, unable.", priority=7),
    P("UnableToComply", "Unable.", "Unable to comply.", priority=7),

    # --- Weapons (stock 1G / 2D) ---------------------------------------------------------------------------------
    P("EngageTrack", "Engaging track", "Committing on track", priority=4, sample=True,
      note="followed by track number digits"),
    P("AAM_SARH_Fired", "Fox one.", "Fox one, fox one.", priority=4, sample=True),
    P("AAM_IR_Fired", "Fox two.", "Fox two, fox two.", priority=4, sample=True),
    P("AircraftGunsFired", "Guns, guns.", "Guns!", priority=4),
    P("Winchester", "Winchester.", "Winchester, no more ordnance.", priority=6, sample=True),

    # --- Kills (stock 1H / 2F) -----------------------------------------------------------------------------------
    P("AirHitAir", "Splash one!", "Splash one bandit!", "Good kill, good kill!", priority=3, sample=True),

    # --- Threats (stock 2C) --------------------------------------------------------------------------------------
    P("MissileIncoming", "Missile launch, defending!", "Missile inbound, breaking!", priority=2, sample=True),

    # --- Fuel / RTB (stock 2A) -----------------------------------------------------------------------------------
    P("BingoFuel", "Bingo, bingo. Returning to base.", "Bingo fuel, RTB.", priority=2, sample=True),

    # --- Contacts (stock 1F) -------------------------------------------------------------------------------------
    P("NewContact", "New contact, bearing", "Contact, bearing", priority=5, sample=True,
      note="followed by bearing digits"),
    P("ClassifiedHostileAir", "Bandit, bandit.", "Contact is hostile.", priority=5, sample=True),
    P("ContactFaded", "Lost contact.", "Contact faded.", priority=5, sample=True),
    P("ContactIntermittent", "Contact intermittent.", "Contact's in and out.", priority=5,
      note="mod-only: flicker detection"),

    # --- Why-not (mod-only) --------------------------------------------------------------------------------------
    P("WhyNot_NotInRearAspect", "Unable, target not in rear aspect.", "Negative, no tail shot.",
      priority=4, sample=True, note="EngageState.NotInRearAspect -- R550 head-on"),
    P("WhyNot_TargetTooFar", "Unable, target outside envelope.", "Negative, out of range.",
      priority=4, sample=True, note="EngageState.TargetTooFar / MissileCanNotReachTarget"),
    P("WhyNot_TargetTooClose", "Unable, too close for missiles.", "Negative, inside minimum range.",
      priority=4, note="EngageState.TargetTooClose"),
    P("WhyNot_NoRadarLock", "Negative, no lock.", "Unable, radar can't hold the target.",
      priority=4, sample=True, note="EngageState.TargetIsNotDetectableByFCR / PrelaunchSensorAlignment"),
    P("WhyNot_NoRadarChannel", "Unable, radar's busy on another target.", priority=4,
      note="CheckingAbort on a SARH weapon: no free channel"),
    P("WhyNot_NotInFiringArc", "Negative, nose not on target.", "Stand by, coming around.",
      priority=4, note="EngageState.TargetIsNotInFiringArcs / NotInVerticalArc"),
    P("WhyNot_Defending", "Unable, I'm defending!", "Negative, defensive, can't commit!",
      priority=3, sample=True, note="aircraft in missile evasion state"),
    P("WhyNot_HoldingFormation", "Unable, holding formation on lead.", "Negative, I'm tied to lead.",
      priority=6, sample=True, note="T2: wingman VID while MovingInFormation"),
    P("WhyNot_CantSeeTarget", "Negative, no visual and no radar on that one.", priority=4,
      note="HandleEngageTasks dropped: CantSeeAirTarget"),
    P("WhyNot_SeekerCantSee", "Negative, seeker can't see it in these conditions.", priority=4,
      note="OrderLog SeekerCantSeeTarget"),
    P("WhyNot_NoAmmo", "Negative, nothing left for that target.", priority=6, note="OrderLog NoSuitableAmmo"),
    P("WhyNot_WrongTarget", "Negative, wrong weapon for that target.", priority=6, note="OrderLog WrongTarget"),
]

# ----------------------------------------------------------------------------------------------------------------
# Fragments.
# ----------------------------------------------------------------------------------------------------------------
DIGIT_WORDS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"]

FRAGMENTS: list[Fragment] = []
for d, word in enumerate(DIGIT_WORDS):
    FRAGMENTS.append(Fragment(f"digit_{d}_mid", f"{word},", sample=True))
    FRAGMENTS.append(Fragment(f"digit_{d}_end", f"{word}.", sample=True))

# Flight-position pairs "one-one" … "niner-four" (flight 1-9, position 1-4), spoken as one word pair.
for flight in range(1, 10):
    for pos in range(1, 5):
        FRAGMENTS.append(Fragment(
            f"flightpos_{flight}{pos}", f"{DIGIT_WORDS[flight]}-{DIGIT_WORDS[pos]},",
            sample=(flight, pos) in {(2, 1), (2, 2), (1, 1)}))


def add_callsign_words(words: list[str], sample_words: set[str]) -> None:
    """Callsign words come from the plugin's Callsigns.cs word lists (kept in sync by hand)."""
    for w in words:
        FRAGMENTS.append(Fragment(f"cs_{w.lower()}", f"{w},", sample=w in sample_words))


CALLSIGN_WORDS = [
    # Fighter
    "Viper", "Cobra", "Falcon", "Razor", "Lancer", "Sabre", "Jester", "Rapier", "Talon", "Venom", "Ghost", "Raptor",
    # Attack
    "Hammer", "Anvil", "Mace", "Dagger", "Tusk", "Spear", "Brick", "Boxer",
    # Patrol
    "Trident", "Pelican", "Albatross", "Corsair", "Neptune", "Marlin",
    # Helicopter
    "Seahawk", "Dipper", "Lynx", "Kingfish", "Dusty", "Rotor",
    # Support
    "Magic", "Drover", "Tankard", "Wizard", "Mercury", "Darkstar",
    # Ship
    "Bulwark", "Sentinel", "Victory", "Warden", "Guardian", "Valiant", "Resolute", "Steadfast", "Vigilant",
    "Defiant", "Intrepid", "Courage",
    # Submarine
    "Shadow", "Silent", "Deepwater", "Barracuda",
    # Shore
    "Castle", "Bastion", "Harbor", "Lighthouse",
]
add_callsign_words(CALLSIGN_WORDS, sample_words={"Viper", "Sentinel"})

assert len({p.key for p in PHRASES}) == len(PHRASES), "duplicate phrase key"
assert len({f.key for f in FRAGMENTS}) == len(FRAGMENTS), "duplicate fragment key"
