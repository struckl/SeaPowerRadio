"""Listening sample: stitch clips into radio transmissions the way the plugin will.

Clips already carry their halves of the natural pauses (see phrases.py), so pieces are joined back to back with no
added gap -- exactly what the plugin's PhraseAssembler does.

Writes to tools/voicegen/samples/:
  scenario<suffix>.mp3   a short engagement on the net, three voices (T1/T2 cases included)
  <persona>.mp3          one reel per voice: every sample phrase, prefixed with that voice's callsign

Usage:
    python tools/voicegen/reel.py --voices us_m_01,gb_m_01,us_f_01 [--suffix _v2]
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

Parts = list[tuple[Path, float]]


def clip(voice: str, key: str) -> Path:
    path = VOICES / voice / f"{key}.mp3"
    if not path.exists():
        sys.exit(f"missing {path} -- run generate.py --sample first")
    return path


def callsign(voice: str, word: str, flightpos: str | None) -> Parts:
    parts = [(clip(voice, f"cs_{word}"), 0.0)]
    if flightpos:
        parts.append((clip(voice, f"fp_{flightpos}"), 0.0))
    return parts


def number(voice: str, kind: str, value: str) -> Parts:
    if kind == "bearing":
        return [(clip(voice, f"brg_{value}"), 0.0)]
    return [(clip(voice, f"trk_hi_{value[:2]}"), 0.0), (clip(voice, f"trk_lo_{value[2:]}"), 0.0)]


def say(voice: str, cs: tuple[str, str | None], key: str, rng: random.Random, value: str | None = None,
        n: int | None = None) -> Parts:
    phrase = next(p for p in PHRASES if p.key == key)
    n = n or rng.randint(1, len(phrase.variants))
    parts = callsign(voice, *cs) + [(clip(voice, f"{key}_{n}"), 0.0)]
    if phrase.then:
        parts += number(voice, phrase.then, value)
    return parts


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
                value = {"track": "7034", "bearing": "270"}.get(phrase.then or "")
                lines.append((say(voice, cs, phrase.key, rng, value, n), "near", 0.9))
        build_reel(lines, OUT / f"{voice}.mp3", voice)
        print(f"wrote {OUT / f'{voice}.mp3'}")


if __name__ == "__main__":
    main()
