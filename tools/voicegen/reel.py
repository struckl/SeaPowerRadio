"""Listening sample: stitch rendered fragments into radio transmissions the way the plugin will.

Writes to tools/voicegen/samples/:
  scenario.mp3        a short engagement on the net, three voices (T1/T2 cases included)
  <persona>.mp3       one reel per voice: every sample phrase, prefixed with that voice's callsign

Usage:
    python tools/voicegen/reel.py --voices us_m_01,gb_m_01,us_f_01
"""

from __future__ import annotations

import argparse
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import REPO  # noqa: E402
from phrases import PHRASES  # noqa: E402
from radio import concat_files, silence, transmission  # noqa: E402

VOICES = REPO / "mod" / "voices"
OUT = Path(__file__).resolve().parent / "samples"

# Gap after a callsign before the message, and between digits. Real radio leaves a beat after the callsign.
GAP_AFTER_CALLSIGN = 0.12
GAP_DIGITS = 0.03
GAP_BEFORE_NUMBER = 0.08


def clip(voice: str, key: str) -> Path:
    path = VOICES / voice / f"{key}.mp3"
    if not path.exists():
        sys.exit(f"missing {path} -- run generate.py --sample first")
    return path


def variant(voice: str, key: str, rng: random.Random) -> Path:
    n = len(next(p for p in PHRASES if p.key == key).variants)
    return clip(voice, f"{key}_{rng.randint(1, n)}")


def callsign(voice: str, word: str, flightpos: str | None) -> list[tuple[Path, float]]:
    parts = [(clip(voice, f"cs_{word}"), 0.0)]
    if flightpos:
        parts.append((clip(voice, f"flightpos_{flightpos}"), 0.02))
    return parts


def digits(voice: str, number: str) -> list[tuple[Path, float]]:
    parts = []
    for i, d in enumerate(number):
        kind = "end" if i == len(number) - 1 else "mid"
        parts.append((clip(voice, f"digit_{d}_{kind}"), GAP_BEFORE_NUMBER if i == 0 else GAP_DIGITS))
    return parts


def say(voice: str, cs: tuple[str, str | None], key: str, rng: random.Random,
        number: str | None = None) -> list[tuple[Path, float]]:
    parts = callsign(voice, *cs)
    parts.append((variant(voice, key, rng), GAP_AFTER_CALLSIGN))
    if number:
        parts += digits(voice, number)
    return parts


def build_reel(lines: list[tuple[list[tuple[Path, float]], str, float]], dest: Path, tag: str) -> None:
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
    args = parser.parse_args()
    lead, wing, ship = args.voices.split(",")
    OUT.mkdir(exist_ok=True)
    rng = random.Random("reel")

    v21 = ("viper", "21")
    v22 = ("viper", "22")
    sen = ("sentinel", None)

    # A short engagement. Pause = silence after the transmission.
    scenario = [
        (say(ship, sen, "NewContact", rng, "090"), "station", 1.4),
        (say(ship, sen, "ClassifiedHostileAir", rng), "station", 1.6),
        (say(lead, v21, "EngageTrack", rng, "7034"), "near", 1.2),
        (say(lead, v21, "WhyNot_NotInRearAspect", rng), "near", 1.0),       # T1: R550 head-on
        (say(wing, v22, "WhyNot_HoldingFormation", rng), "near", 1.4),     # T2: wingman VID
        (say(lead, v21, "WhyNot_NoRadarLock", rng), "near", 1.2),          # T1: 530F without lock
        (say(lead, v21, "AAM_SARH_Fired", rng), "cockpit_hot", 0.9),
        (say(wing, v22, "MissileIncoming", rng), "cockpit_hot", 0.8),
        (say(wing, v22, "WhyNot_Defending", rng), "cockpit_hot", 1.5),
        (say(lead, v21, "AirHitAir", rng), "cockpit_hot", 1.6),
        (say(ship, sen, "ContactFaded", rng), "station", 1.4),
        (say(wing, v22, "Winchester", rng), "far", 1.2),
        (say(lead, v21, "BingoFuel", rng), "far", 1.0),
        (say(ship, sen, "Confirmed", rng), "station", 1.0),
    ]
    build_reel(scenario, OUT / "scenario.mp3", "scenario")
    print(f"wrote {OUT / 'scenario.mp3'}")

    # Per-voice reels: every sample phrase, every variant.
    for voice, cs in ((lead, v21), (wing, v22), (ship, sen)):
        lines = []
        for phrase in PHRASES:
            if not phrase.sample:
                continue
            for n in range(1, len(phrase.variants) + 1):
                parts = callsign(voice, *cs) + [(clip(voice, f"{phrase.key}_{n}"), GAP_AFTER_CALLSIGN)]
                if phrase.key == "EngageTrack":
                    parts += digits(voice, "7034")
                if phrase.key == "NewContact":
                    parts += digits(voice, "270")
                lines.append((parts, "near", 0.9))
        build_reel(lines, OUT / f"{voice}.mp3", voice)
        print(f"wrote {OUT / f'{voice}.mp3'}")


if __name__ == "__main__":
    main()
