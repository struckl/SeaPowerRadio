"""The rules the voice tools and the plugin share: which clips exist, how they are joined, how a call sounds.

This file is the one source. The plugin reads it as src/SeaPowerRadio/Radio/ClipRules.g.cs, written by
rules_cs.py; check_sync.py fails while that file is out of date. Change a value here, run
`python tools/voicegen/rules_cs.py`, and both sides follow.

Ranges decide which clip files exist in every voice: changing one means rendering clips (or leaves the plugin asking
for clips nobody rendered).
"""

from __future__ import annotations

# Callsign words per kind (the plugin's CallsignKind), in this order: the plugin shuffles each list with a seed per
# mission, so reordering a list hands units other callsigns. Keep them speakable on a noisy net: two syllables or a
# strong single one, no two words that sound alike. Each is rendered in every voice as cs_<word>.
CALLSIGN_WORDS: dict[str, list[str]] = {
    "Fighter": ["Viper", "Cobra", "Falcon", "Razor", "Lancer", "Sabre", "Jester", "Rapier", "Talon", "Venom", "Ghost",
                "Raptor"],
    "Attack": ["Hammer", "Anvil", "Mace", "Dagger", "Tusk", "Spear", "Brick", "Boxer"],
    "Patrol": ["Trident", "Pelican", "Albatross", "Corsair", "Neptune", "Marlin"],
    "Helicopter": ["Seahawk", "Dipper", "Lynx", "Kingfish", "Dusty", "Rotor"],
    "Support": ["Magic", "Drover", "Tankard", "Wizard", "Mercury", "Darkstar"],
    "Ship": ["Bulwark", "Sentinel", "Victory", "Warden", "Guardian", "Valiant", "Resolute", "Steadfast", "Vigilant",
             "Defiant", "Intrepid", "Courage"],
    "Submarine": ["Shadow", "Silent", "Deepwater", "Barracuda"],
    "Shore": ["Castle", "Bastion", "Harbor", "Lighthouse"],
}
AIR_KINDS = ("Fighter", "Attack", "Patrol", "Helicopter", "Support")      # "<word> two-one"
SINGLE_KINDS = ("Ship", "Submarine", "Shore")                              # the word alone

# Clip key ranges, inclusive.
FLIGHTS = (1, 9)              # fp_<flight><position>: "Viper two-one"
POSITIONS = (1, 4)
CALLSIGN_NUMBERS = (1, 20)    # cn_<n>: the number after a game callsign word ("Lancer three")
ANGELS = (1, 60)              # alt_<thousands of feet>
LOW_ALTITUDE_HUNDREDS = (1, 2, 3, 5, 8)    # alt_lo_<h>: "five hundred feet", below 1000 ft
COUNTS = (2, 9)               # cnt_<n>

# Clips that hold (part of) a number. Where two of them meet, or one meets a word, the pause is closed up.
NUMBER_PIECES = ("brg_", "pm_", "pe_", "dm_", "alt_", "cnt_")

# Most silence left where two clips join. The takes speak a pause at every cut, so the cut falls into silence, also
# where nobody pauses ("bearing, | two seven three"): there the two halves added up to a quarter to half a second,
# which made the numbers sound read off a card.
NUMBER_PAUSE = 0.05     # word <-> number, no pause in the text: "bearing two seven three"
GROUP_PAUSE = 0.08      # between the digit groups: "seven zero, three four"
SPOKEN_PAUSE = 0.25     # a pause the speaker makes anyway (after the callsign, at a comma)

# Names of game units and squadrons as clip keys (nm_<slug>, sq_<slug>): lower case, every run of other characters
# one underscore, trimmed; an empty result is "x".
SLUG_NOT = "[^a-z0-9]+"
SLUG_EMPTY = "x"

# The voice.ini priority scale: 1 abandon, 2 hit/incoming, 3 kill, 4 firing, 5 contacts, 6 nav, 7 ack.
ALERT_MAX_PRIORITY = 2      # at or below: an alert (loud, cuts in, hot cockpit sound)
ROUTINE_MIN_PRIORITY = 5    # at or above: a routine report that goes stale soon
REPLY_PRIORITY = 7          # an acknowledgement: a reply to the player

# Phrase groups (keys of phrases.PHRASES; check_sync.py fails on a key that is not one).
# Call volumes of the options page (the plugin's CallCategory): contacts and combat by phrase; then warnings (every
# alert, and these) and orders (replies, why-not); the rest is "other".
CATEGORY_CONTACTS = [
    "NewContactAir", "NewContactLand", "NewContactSubsurface", "NewContactSurface", "NewContactsMultiple",
    "ContactFaded", "ContactIntermittent", "ContactRegained", "ContactsFaded",
    "IdByDatalink", "IdByEsm", "IdByFiredUpon", "IdByIntel", "IdByRadar", "IdBySonar", "IdByVisual",
    "TrackFriendly", "TrackHostile", "TrackNeutral",
    "BanditBandit", "BuoyContact", "MadContact", "EsmContact", "RadarEmission", "MerchantTraffic",
]
CATEGORY_COMBAT = [
    "AAM_ARS_Fired", "AAM_IR_Fired", "AAM_SARH_Fired", "AshmAway", "AsumAway", "BombsAway", "BuoyAway",
    "ChaffOut", "DecoyOut", "GunsGuns", "Magnum", "SalvoFire", "SamAway", "SamReady", "Shack",
    "SplashOne", "SplashTrack", "HitTrack", "TargetDestroyed", "TorpedoAwayAir", "WeaponAway", "Winchester",
    "LaunchTransient",
]
# Urgent calls about the mission, not about danger to the unit: "other", not warnings.
MISSION_ALERTS = ["MissionComplete", "MissionFailed", "NewIntel", "NewTasking", "ObjectiveComplete", "ObjectiveFailed"]
# Warnings although not alerts.
OTHER_WARNINGS = ["BeingJammed", "DeckDamaged"]
# Moods (mood packs): the call's own event decides first.
MOOD_HAPPY = [
    "SplashOne", "SplashTrack", "TargetDestroyed", "Shack", "HitTrack", "MissionComplete", "ObjectiveComplete",
    "SarPickup", "SarPickupCount", "SarPickupMany", "SarDelivered", "HeloRecovered", "AllAircraftRecovered",
    "ContactRegained",
]
MOOD_SAD = [
    "MissionFailed", "ObjectiveFailed", "SarFull", "SarSurvivors", "Winchester", "BatteryLow", "NeedToCharge",
    "DeckDamaged", "ContactFaded", "ContactsFaded",
]
PHRASE_GROUPS = dict(Contacts=CATEGORY_CONTACTS, Combat=CATEGORY_COMBAT, MissionAlerts=MISSION_ALERTS,
                     OtherWarnings=OTHER_WARNINGS, Happy=MOOD_HAPPY, Sad=MOOD_SAD)

# The transmission half of the radio sound (RadioFx in the plugin, radio.transmission() for listening samples).
# Profile: hiss and crackle level, low-pass and high-pass of the voice in Hz.
PROFILES: dict[str, dict[str, float]] = {
    "station": dict(hiss=0.040, crackle=0.05, lp=3400, hp=280),
    "near": dict(hiss=0.060, crackle=0.08, lp=3300, hp=300),
    "far": dict(hiss=0.120, crackle=0.18, lp=2600, hp=420),
    "cockpit_hot": dict(hiss=0.070, crackle=0.10, lp=3100, hp=320),
}
DEFAULT_PROFILE = "near"
FX: dict[str, float] = dict(
    phrase_gap=0.12,          # seconds of silence between two phrases of one call
    # Joins: a cut in running speech (both edges loud) is cross-faded instead of butted together.
    crossfade=0.025,
    edge=0.015,
    hot_edge_ratio=0.5,       # edge RMS vs. clip RMS above this = cut in running speech (about -6 dB)
    # Silence at a join, in frames of this length.
    frame=0.010,
    sound_rms=0.01,           # -40 dBFS: speech; breath and room tone sit below it, under the hiss bed
    onset_rms=0.0018,         # -55 dBFS: the start of a digit ("s", "th", "f") after the band-limit
    # Loudness: the game's voice lines are mastered hot (about -11 LUFS) while the clips sit around -26 LUFS. The
    # voice is brought up to the stock level and the peaks caught by a soft limiter.
    target_speech_rms=0.17,   # RMS of the stock voice clips while speaking (mean volume about -15 dBFS)
    speech_gate=0.005,        # samples below this are pauses, left out of the speech RMS
    gain_min=0.25,
    gain_max=12.0,
    limit_knee=0.6,           # linear below, tanh above
    limit_ceiling=0.98,
    # Random per transmission: (min, max).
    preroll_min=0.08, preroll_max=0.20,             # hiss before the mic click
    click_seconds_min=0.022, click_seconds_max=0.045,
    click_level_min=0.14, click_level_max=0.26,
    hang_min=0.05, hang_max=0.14,                   # after the voice, before the squelch tail
    tail_seconds_min=0.09, tail_seconds_max=0.18,
    tail_level_min=0.18, tail_level_max=0.32,
    crackle_density_min=0.02, crackle_density_max=0.05,
    weak_signal_hiss=0.1,     # a profile with more hiss than this is a weak signal: denser crackle
    weak_signal_density=1.6,
    # Band of each sound, Hz.
    click_hp=700, click_lp=3200,
    tail_hp=500, tail_lp=3400,
    hiss_hp=400, hiss_lp=2600,
    crackle_hp=600, crackle_lp=3200,
)


def low_altitude_step(feet: int) -> int:
    """The rendered hundreds nearest to feet (below 1000 ft); the first of two equally near ones."""
    best = LOW_ALTITUDE_HUNDREDS[0]
    for h in LOW_ALTITUDE_HUNDREDS:
        if abs(h * 100 - feet) < abs(best * 100 - feet):
            best = h
    return best


def number_pieces(kind: str, value: int, has_bearing=lambda key: True) -> list[str]:
    """The clips that say a number: the Python side of the plugin's TransmissionBuilder.Number.

    kind: brg (bearing), alt (feet), count, trk / num (track number, depth). has_bearing(key): whether that whole
    bearing clip exists; libraries rendered before whole bearings existed say dm_ + pe_.
    """
    if kind == "brg":
        b = f"{value % 360:03d}"
        return [f"brg_{b}"] if has_bearing(f"brg_{b}") else [f"dm_{b[0]}", f"pe_{b[1:]}"]
    if kind == "alt":
        if value >= 1000:
            return [f"alt_{min(max(round(value / 1000), ANGELS[0]), ANGELS[1])}"]
        return [f"alt_lo_{low_altitude_step(value)}"]
    if kind == "count":
        return [f"cnt_{min(max(value, COUNTS[0]), COUNTS[1])}"]
    s = str(abs(value))
    if len(s) >= 4:
        return [f"pm_{s[-4:-2]}", f"pe_{s[-2:]}"]
    if len(s) == 3:
        return [f"dm_{s[0]}", f"pe_{s[1:]}"]
    return [f"pe_{abs(value):02d}"]
