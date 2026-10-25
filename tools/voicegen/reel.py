"""Listening sample: stitch clips into radio transmissions the way the plugin will.

Clips already carry their halves of the pauses they were cut in (see phrases.py), so pieces are joined back to back
with no added gap, and the pauses at a number are closed up -- exactly what the plugin's TransmissionBuilder and
RadioFx do.

Writes to tools/voicegen/samples/:
  scenario<suffix>.mp3   a short engagement on the net, three voices (T1/T2 cases included)
  <persona>.mp3          one reel per voice: every sample phrase, prefixed with that voice's callsign

Usage:
    python tools/voicegen/reel.py --voices us_m_01,gb_m_01,us_f_01 [--suffix _v2] [--library <mod/voices>]
    python tools/voicegen/reel.py --voices ... --suffix _as_cut --as-cut     # joins with the pauses as cut
"""

from __future__ import annotations

import argparse
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import REPO  # noqa: E402
from phrases import PHRASES, SLOT, number_pauses  # noqa: E402
from radio import concat_files, silence, transmission  # noqa: E402

VOICES = REPO / "mod" / "voices"
OUT = Path(__file__).resolve().parent / "samples"

# Most silence where two clips join -- the plugin's TransmissionBuilder.MaxPause.
NUMBER_PAUSE = 0.05     # word <-> number, no pause in the text: "bearing two seven three"
GROUP_PAUSE = 0.08      # between the digit groups: "seven zero, three four"
SPOKEN_PAUSE = 0.25     # a pause the speaker makes anyway (after the callsign, at a comma)
NUMBER_PIECES = ("brg_", "pm_", "pe_", "dm_", "alt_", "cnt_")
AS_CUT = False          # --as-cut: join with the pauses as cut, for comparison

Parts = list[tuple[Path, float]]


def clip(voice: str, key: str) -> Path:
    path = VOICES / voice / f"{key}.mp3"
    if not path.exists():
        sys.exit(f"missing {path} -- run generate.py --sample first")
    return path


def pauses_at_number(key: str) -> bool:
    """Does the text pause where this clip meets its number? Only a phrase half can say no (Catalog.PausesAtNumber)."""
    if not key.endswith(("_a", "_b")):
        return True
    phrase_key, _, n = key[:-2].rpartition("_")
    phrase = next((p for p in PHRASES if p.key == phrase_key), None)
    if phrase is None or not n.isdigit() or int(n) > len(phrase.variants):
        return True
    return number_pauses(phrase.variants[int(n) - 1])[0 if key.endswith("_a") else 1]


def max_pause(previous: str, key: str) -> float:
    if AS_CUT:
        return float("inf")
    a, b = previous.startswith(NUMBER_PIECES), key.startswith(NUMBER_PIECES)
    if a and b:
        return GROUP_PAUSE
    if a or b:
        return SPOKEN_PAUSE if pauses_at_number(key if a else previous) else NUMBER_PAUSE
    return SPOKEN_PAUSE


def parts_of(voice: str, keys: list[str]) -> Parts:
    return [(clip(voice, k), max_pause(keys[i - 1], k) if i else 0.0) for i, k in enumerate(keys)]


def number(voice: str, kind: str, value: str) -> list[str]:
    """Number pieces, assembled the way the plugin's TransmissionBuilder.Number does (see phrases.py)."""
    if kind == "brg":                                   # 273 -> brg_273 (older libraries: dm_2 + pe_73)
        b = value.zfill(3)
        return [f"brg_{b}"] if (VOICES / voice / f"brg_{b}.mp3").exists() else [f"dm_{b[0]}", f"pe_{b[1:]}"]
    if kind == "alt":                                   # feet
        feet = int(value)
        return [f"alt_{round(feet / 1000)}"] if feet >= 1000 else [f"alt_lo_{max(1, round(feet / 100))}"]
    if kind == "count":
        return [f"cnt_{value}"]
    v = str(abs(int(value)))                            # trk, num
    if len(v) >= 4:                                     # 7034 -> pm_70 + pe_34
        return [f"pm_{v[-4:-2]}", f"pe_{v[-2:]}"]
    if len(v) == 3:                                     # 150 -> dm_1 + pe_50
        return [f"dm_{v[0]}", f"pe_{v[1:]}"]
    return [f"pe_{v.zfill(2)}"]


def say(voice: str, cs: tuple[str, str | None], key: str, rng: random.Random, value: str | None = None,
        n: int | None = None) -> Parts:
    phrase = next(p for p in PHRASES if p.key == key)
    n = n or rng.randint(1, len(phrase.variants))
    keys = [f"cs_{cs[0]}"] + ([f"fp_{cs[1]}"] if cs[1] else [])
    slot = SLOT.search(phrase.variants[n - 1])
    if slot is None:
        return parts_of(voice, keys + [f"{key}_{n}"])
    for suffix in ("a", "number", "b"):
        if suffix == "number":
            keys += number(voice, slot.group(1), value)
        elif (VOICES / voice / f"{key}_{n}_{suffix}.mp3").exists():
            keys.append(f"{key}_{n}_{suffix}")
    return parts_of(voice, keys)


def build_reel(lines: list[tuple[Parts, str, float]], dest: Path, tag: str) -> None:
    tmp = dest.parent / f"_tmp_{tag}"
    tmp.mkdir(exist_ok=True)
    pieces: list[Path] = []
    for i, (parts, profile, pause) in enumerate(lines):
        tx = tmp / f"tx{i:02d}.mp3"
        transmission(parts, tx, profile, seed=f"{tag}|{i}")
        gap = tmp / f"gap{i:02d}.mp3"
        silence(gap, pause)
        pieces += [tx, gap]
    concat_files(pieces, dest)
    for f in tmp.iterdir():
        f.unlink()
    tmp.rmdir()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True, help="three persona ids: lead, wingman, ship")
    parser.add_argument("--suffix", default="", help="appended to scenario file name")
    parser.add_argument("--no-voice-reels", action="store_true")
    parser.add_argument("--library", type=Path, help="voice library to read (default: mod/voices of this checkout)")
    parser.add_argument("--as-cut", action="store_true", help="join clips with the pauses as cut (no closing up)")
    args = parser.parse_args()
    global VOICES, AS_CUT
    VOICES = args.library or VOICES
    AS_CUT = args.as_cut
    lead, wing, ship = args.voices.split(",")
    OUT.mkdir(exist_ok=True)
    rng = random.Random("reel")

    v21 = ("viper", "21")
    v22 = ("viper", "22")
    sen = ("sentinel", None)

    # A short engagement. Pause = silence after the transmission.
    scenario = [
        (say(ship, sen, "NewContactAir", rng, "090"), "station", 1.4),
        (say(ship, sen, "BanditBandit", rng), "station", 1.6),
        (say(lead, v21, "EngageTrack", rng, "7034"), "near", 1.2),
        (say(lead, v21, "WhyNot_NotInRearAspect", rng), "near", 1.0),       # T1: R550 head-on
        (say(wing, v22, "WhyNot_HoldingFormation", rng), "near", 1.4),     # T2: wingman VID
        (say(lead, v21, "WhyNot_NoRadarLock", rng), "near", 1.2),          # T1: 530F without lock
        (say(lead, v21, "AAM_SARH_Fired", rng), "cockpit_hot", 0.9),
        (say(wing, v22, "MissileIncoming", rng), "cockpit_hot", 0.8),
        (say(wing, v22, "WhyNot_Defending", rng), "cockpit_hot", 1.5),
        (say(lead, v21, "SplashOne", rng), "cockpit_hot", 1.6),
        (say(ship, sen, "ContactFaded", rng, "7035"), "station", 1.4),
        (say(wing, v22, "Winchester", rng), "far", 1.2),
        (say(lead, v21, "BingoFuel", rng), "far", 1.0),
        (say(ship, sen, "Confirmed", rng), "station", 1.0),
    ]
    dest = OUT / f"scenario{args.suffix}.mp3"
    build_reel(scenario, dest, "scenario" + args.suffix)
    print(f"wrote {dest}")
    if args.no_voice_reels:
        return

    # Per-voice reels: every sample phrase, every variant.
    for voice, cs in ((lead, v21), (wing, v22), (ship, sen)):
        lines = []
        for phrase in PHRASES:
            if not phrase.sample:
                continue
            for n in range(1, len(phrase.variants) + 1):
                m = SLOT.search(phrase.variants[n - 1])
                value = {"trk": "7034", "brg": "273", "num": "150", "alt": "20000", "count": "3"}.get(
                    m.group(1) if m else "")
                lines.append((say(voice, cs, phrase.key, rng, value, n), "near", 0.9))
        build_reel(lines, OUT / f"{voice}.mp3", voice)
        print(f"wrote {OUT / f'{voice}.mp3'}")


if __name__ == "__main__":
    main()
