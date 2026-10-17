"""In-game unit names, spoken in the voices bound to them.

Ships, submarines and shore units call themselves by the short name the game gives them ("Pargo" for
"Pargo SSN-650", from language_en/vessel_names.ini and land_units_names.ini). Player aircraft call themselves
"<squadron callsign> <number>" ("Lancer 3", wingmen in a formation "Lancer 3-2"), with the callsign words from
language_en/aircraft_names.ini.

There are thousands of names, so not every voice can say every name. Instead each name is bound to a few voices,
picked by a stable hash of the name:
  * a ship name to SHIP_VOICES voices -- the mission gives the ship the first of them that is still free, so a
    ship also keeps the same voice in every mission;
  * a squadron callsign word to SQUADRON_VOICES voices -- the pilots flying under that callsign get those voices.
The numbers after a callsign word (cn_N, fp_FP) exist in every voice.

Output for the plugin: mod/voices/names.txt  (key|spoken name|kind|voice,voice,...).
Run `python tools/voicegen/names.py` after a game update to refresh the list (needs the game install).
"""

from __future__ import annotations

import hashlib
import re
from dataclasses import dataclass
from pathlib import Path

GAME_LANGUAGE = Path(r"E:\Games\steamapps\common\Sea Power\Sea Power_Data\StreamingAssets\original\language_en")
NAMES_FILE = Path(__file__).resolve().parents[2] / "mod" / "voices" / "names.txt"

SHIP_VOICES = 3
SQUADRON_VOICES = 8
SKIP_SECTIONS = ("civ_", "all_", "sea_", "test", "tgt_")

ONES = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]
TEENS = ["ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
         "nineteen"]
TENS = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"]


@dataclass(frozen=True)
class Name:
    key: str        # clip key: nm_<slug> (ship/shore) or sq_<slug> (squadron callsign word)
    text: str       # as the game writes it
    spoken: str     # as the TTS should read it
    kind: str       # "unit" or "squadron"


def slug(text: str) -> str:
    """Must match Radio/Names.cs Slug()."""
    s = re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")
    return s or "x"


def number_words(n: int) -> str:
    if n < 10:
        return ONES[n]
    if n < 20:
        return TEENS[n - 10]
    if n < 100:
        return TENS[n // 10] + ("-" + ONES[n % 10] if n % 10 else "")
    return " ".join(ONES[int(c)] for c in str(n))


def spoken(text: str) -> str:
    """How the TTS should read a name: digits as words, letters and digits apart, dots and underscores as spaces
    ("Type 22" -> "Type twenty-two", "5N62 FCR" -> "five N sixty-two FCR", "911.LTSG" -> "nine one one LTSG")."""
    t = re.sub(r"(?<=\d)(?=[A-Za-z])|(?<=[A-Za-z])(?=\d)", " ", text)
    t = re.sub(r"[._/]+", " ", t).replace("&", " and ")
    t = re.sub(r"\d+", lambda m: number_words(int(m.group())), t)
    return re.sub(r"\s+", " ", t).strip()


def read_text(path: Path) -> str:
    """The game's language files are UTF-8 or Windows-1252 depending on the file."""
    data = path.read_bytes()
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("cp1252")


def read_sections(path: Path) -> list[tuple[str, list[str]]]:
    sections, current, lines = [], None, []
    for raw in read_text(path).splitlines():
        line = raw.strip()
        if line.startswith("["):
            if current is not None:
                sections.append((current, lines))
            current, lines = line.strip("[]").split("]")[0], []
        elif current is not None:
            lines.append(line)
    if current is not None:
        sections.append((current, lines))
    return sections


def unit_names() -> list[str]:
    names: set[str] = set()
    for file in ("vessel_names.ini", "land_units_names.ini"):
        for section, lines in read_sections(GAME_LANGUAGE / file):
            if section.startswith("*") or section.lower().startswith(SKIP_SECTIONS):
                continue
            for line in lines:
                m = re.match(r"(Variant\d+|Default)=(.*)", line)
                if not m:
                    continue
                parts = [p.strip() for p in m.group(2).split(",")]
                short = parts[1] if len(parts) > 1 and parts[1] else parts[0]
                if short and re.search(r"[A-Za-z]", short):
                    names.add(short)
    return sorted(names)


def squadron_words() -> list[str]:
    words: set[str] = set()
    for raw in read_text(GAME_LANGUAGE / "aircraft_names.ini").splitlines():
        line = raw.strip()
        if not line.startswith("Callsigns="):
            continue
        for group in line[len("Callsigns="):].split("|"):
            for word in group.split(",")[1:]:
                word = word.strip()
                if word and re.search(r"[A-Za-z]", word):
                    words.add(word)
    return sorted(words)


def bound_voices(key: str, personas: list[str], count: int) -> list[str]:
    """`count` distinct personas for a name, stable across runs (rendezvous hashing)."""
    ranked = sorted(personas, key=lambda p: hashlib.sha1(f"{key}|{p}".encode()).hexdigest())
    return ranked[:count]


def all_names() -> list[Name]:
    out: dict[str, Name] = {}
    for n in unit_names():
        k = "nm_" + slug(n)
        out.setdefault(k, Name(k, n, spoken(n), "unit"))
    for w in squadron_words():
        k = "sq_" + slug(w)
        out.setdefault(k, Name(k, w, spoken(w), "squadron"))
    return list(out.values())


def bindings(personas: list[str]) -> dict[str, list[str]]:
    return {n.key: bound_voices(n.key, personas, SHIP_VOICES if n.kind == "unit" else SQUADRON_VOICES)
            for n in all_names()}


def write_names_file(personas: list[str]) -> None:
    names = all_names()
    bound = bindings(personas)
    lines = ["# key|name as in game|kind|voices -- generated by tools/voicegen/names.py"]
    lines += [f"{n.key}|{n.text}|{n.kind}|{','.join(bound[n.key])}" for n in names]
    NAMES_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {len(names)} names to {NAMES_FILE.name}")


if __name__ == "__main__":
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from cast import CAST
    write_names_file([p.id for p in CAST])
