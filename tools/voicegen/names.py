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

Since 1.4.0 a name is bound by nation: the game's language files say which country a name belongs to (the section banner
of vessel_names.ini / land_units_names.ini / aircraft_names.ini, the section prefix, a hull prefix in the full name), and
mod/voices/accents.txt says which accents that nation's units use. A name of a listed nation is bound to voices of that
accent family only (3 men and 1 woman for a ship, 8 voices with at least 3 men and a woman for a squadron word, as far as
the family has them); a name with no known nation, or only nations without accents, keeps the old mix of all voices
(and exactly its old binding). `python tools/voicegen/names.py --stats` prints how the names resolve.

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

SHIP_VOICES = 3          # old binding (names without a nation)
SQUADRON_VOICES = 8
SHIP_MEN, SHIP_WOMEN = 3, 1                 # nation-bound ship name: men + women of the accent family
SQUADRON_MIN_MEN, SQUADRON_WOMEN = 3, 1     # nation-bound squadron word: SQUADRON_VOICES voices, at least these
ACCENTS_FILE = Path(__file__).resolve().parents[2] / "mod" / "voices" / "accents.txt"
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


# ---- nation of a name -----------------------------------------------------------------------------------------------
# The game files group their sections under banner lines ("[*** Canada ***]"); a section prefix or a hull prefix in a
# full name is more exact. Nation ids are the ones of language_en/nations.ini. No nation = civil / generic.

BANNER_NATION = {
    "peoples republic of china": "China", "china": "China", "france": "France", "germany": "Germany", "india": "India",
    "israel": "Israel", "iran": "Iran", "iraq": "Iraq", "japan": "Japan", "libya": "Libya", "norway": "Norway",
    "pakistan": "Pakistan", "portugal": "Portugal", "spain": "Spain", "canada": "Canada", "australia": "Australia",
    "uk": "UK", "united kingdom": "UK", "usa": "US", "us nato": "US", "soviet union": "Soviet", "warsawpact": "Soviet",
    "north vietnam": "Vietnam", "argentina": "Argentina", "iceland": "Iceland", "finland": "Finland",
}
SECTION_NATION = {
    "plan": "China", "pla": "China", "nv": "Vietnam", "fr": "France", "fgs": "Germany", "ir": "Iran", "jmsdf": "Japan",
    "jsdf": "Japan", "ln": "Libya", "knm": "Norway", "pns": "Pakistan", "nrp": "Portugal", "es": "Spain", "rcn": "Canada",
    "ran": "Australia", "rn": "UK", "raf": "UK", "usn": "US", "usa": "US", "us": "US", "wp": "Soviet", "is": "Iceland",
    "iqa": "Iraq", "fin": "Finland", "swe": "Sweden",
}
HULL_NATION = {   # prefix of a full name ("TCG Savastepe D348"); INS is Indian or Israeli, the banner decides
    "USS": "US", "USNS": "US", "USCGC": "US", "HMS": "UK", "HMCS": "Canada", "HMAS": "Australia", "HMNZS": "NewZealand",
    "HMSAS": "South_Africa", "SAS": "South_Africa", "FGS": "Germany", "FS": "France", "HNLMS": "Netherlands",
    "HDMS": "Denmark", "HNOMS": "Norway", "HSWMS": "Sweden", "ITS": "Italy", "ESPS": "Spain", "SPS": "Spain",
    "NRP": "Portugal", "TCG": "Turkey", "HS": "Greece", "ORP": "Poland", "ROKS": "South_Korea", "ROCS": "RoC",
    "JS": "Japan", "JDS": "Japan", "PNS": "Pakistan", "ENS": "Egypt", "IRIS": "Iran", "ARA": "Argentina",
}
INS_BY_BANNER = {"israel": "Israel", "india": "India"}

NATION_FILES = ("vessel_names.ini", "land_units_names.ini")


def read_sections_with_banner(path: Path) -> list[tuple[str, str, list[str]]]:
    """(banner, section, lines): banner = the last '[*** X ***]' line before the section, lower-cased."""
    out, banner = [], ""
    for section, lines in read_sections(path):
        if section.startswith("*"):
            banner = section.strip("* ").strip().lower()
        elif section.startswith("-") or section.startswith(" "):
            continue            # sub-banner ("---- Aircraft ----")
        else:
            out.append((banner, section, lines))
    return out


def section_nation(banner: str, section: str, use_prefix: bool) -> str | None:
    prefix = section.lower().split("_")[0]
    if use_prefix and prefix in SECTION_NATION:
        return SECTION_NATION[prefix]
    return BANNER_NATION.get(banner)


def hull_nation(full: str, banner: str) -> str | None:
    word = full.split(" ")[0].rstrip(".").upper()
    if word == "INS":
        return INS_BY_BANNER.get(banner, "India")
    return HULL_NATION.get(word)


def unit_names_by_nation() -> dict[str, set[str]]:
    """Every ship / shore unit short name with the nations it is used by (an empty set = no known nation)."""
    names: dict[str, set[str]] = {}
    for file in NATION_FILES:
        for banner, section, lines in read_sections_with_banner(GAME_LANGUAGE / file):
            if section.startswith("*") or section.lower().startswith(SKIP_SECTIONS):
                continue
            base = section_nation(banner, section, use_prefix=True)
            for line in lines:
                m = re.match(r"(Variant\d+|Default)=(.*)", line)
                if not m:
                    continue
                parts = [p.strip() for p in m.group(2).split(",")]
                short = parts[1] if len(parts) > 1 and parts[1] else parts[0]
                if short and re.search(r"[A-Za-z]", short):
                    nation = hull_nation(parts[0], banner) or base
                    names.setdefault(short, set())
                    if nation:
                        names[short].add(nation)
    return names


def squadron_words_by_nation() -> dict[str, set[str]]:
    """Every callsign word with the nations of the aircraft that use it (aircraft sections: the banner decides)."""
    words: dict[str, set[str]] = {}
    for banner, section, lines in read_sections_with_banner(GAME_LANGUAGE / "aircraft_names.ini"):
        nation = section_nation(banner, section, use_prefix=False)
        for raw in lines:
            line = raw.strip()
            if not line.startswith("Callsigns="):
                continue
            for group in line[len("Callsigns="):].split("|"):
                for word in group.split(",")[1:]:
                    word = word.strip()
                    if word and re.search(r"[A-Za-z]", word):
                        words.setdefault(word, set())
                        if nation:
                            words[word].add(nation)
    return words


def unit_names() -> list[str]:
    return sorted(unit_names_by_nation())


def squadron_words() -> list[str]:
    return sorted(squadron_words_by_nation())


def read_accents() -> dict[str, list[str]]:
    """mod/voices/accents.txt: nation id -> accents, best first."""
    out: dict[str, list[str]] = {}
    if ACCENTS_FILE.exists():
        for raw in ACCENTS_FILE.read_text(encoding="utf-8").splitlines():
            line = raw.strip()
            if line and not line.startswith("#") and "|" in line:
                nation, accents = line.split("|", 1)
                out[nation.strip()] = [a.strip() for a in accents.split(",") if a.strip()]
    return out


def bound_voices(key: str, personas: list[str], count: int) -> list[str]:
    """`count` distinct personas for a name, stable across runs (rendezvous hashing)."""
    ranked = sorted(personas, key=lambda p: hashlib.sha1(f"{key}|{p}".encode()).hexdigest())
    return ranked[:count]


def persona_parts(persona_id: str) -> tuple[str, str]:
    """(accent, gender) of a persona id '<accent>_<gender>_<nn>'."""
    accent, gender, _ = persona_id.split("_")
    return accent, gender


def family_voices(key: str, personas: list[str], accents: list[str], count: int, men: int, women: int) -> list[str]:
    """Voices of an accent family for a name, stable across runs (the same rendezvous rank as bound_voices):
    `men` men and `women` women (as far as the family has them), then filled up to `count` by rank."""
    pool = [p for p in personas if persona_parts(p)[0] in accents]
    ranked = sorted(pool, key=lambda p: hashlib.sha1(f"{key}|{p}".encode()).hexdigest())
    m = [p for p in ranked if persona_parts(p)[1] == "m"]
    f = [p for p in ranked if persona_parts(p)[1] == "f"]
    chosen = m[:men] + f[:women]
    for p in ranked:
        if len(chosen) >= count:
            break
        if p not in chosen:
            chosen.append(p)
    return sorted(chosen, key=ranked.index)


def name_family(nations: set[str], accents: dict[str, list[str]]) -> list[str] | None:
    """The accent family of a name: the accents of its listed nations (in accents.txt order); None if none is listed."""
    listed = [n for n in accents if n in nations]
    if not listed:
        return None
    family: list[str] = []
    for n in listed:
        for a in accents[n]:
            if a not in family:
                family.append(a)
    return family


def all_names() -> list[Name]:
    return [n for n, _ in all_names_with_nations()]


def all_names_with_nations() -> list[tuple[Name, set[str]]]:
    out: dict[str, tuple[Name, set[str]]] = {}
    for n, nations in sorted(unit_names_by_nation().items()):
        k = "nm_" + slug(n)
        out.setdefault(k, (Name(k, n, spoken(n), "unit"), nations))
    for w, nations in sorted(squadron_words_by_nation().items()):
        k = "sq_" + slug(w)
        out.setdefault(k, (Name(k, w, spoken(w), "squadron"), nations))
    return list(out.values())


def bindings(personas: list[str]) -> dict[str, list[str]]:
    accents = read_accents()
    result: dict[str, list[str]] = {}
    for n, nations in all_names_with_nations():
        family = name_family(nations, accents)
        if family is None:        # no known nation, or none with accents: the old mix of every voice
            result[n.key] = bound_voices(n.key, personas, SHIP_VOICES if n.kind == "unit" else SQUADRON_VOICES)
        elif n.kind == "unit":
            result[n.key] = family_voices(n.key, personas, family, SHIP_MEN + SHIP_WOMEN, SHIP_MEN, SHIP_WOMEN)
        else:
            result[n.key] = family_voices(n.key, personas, family, SQUADRON_VOICES, SQUADRON_MIN_MEN, SQUADRON_WOMEN)
    return result


def write_names_file(personas: list[str]) -> None:
    names = all_names()
    bound = bindings(personas)
    lines = ["# key|name as in game|kind|voices -- generated by tools/voicegen/names.py"]
    lines += [f"{n.key}|{n.text}|{n.kind}|{','.join(bound[n.key])}" for n in names]
    NAMES_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {len(names)} names to {NAMES_FILE.name}")


def stats() -> None:
    """How the names resolve to nations (coverage) and to accent families."""
    from collections import Counter
    accents = read_accents()
    items = all_names_with_nations()
    for label, kind in (("ship / shore names", "unit"), ("squadron words", "squadron")):
        group = [ns for n, ns in items if n.kind == kind]
        listed = sum(1 for ns in group if name_family(ns, accents))
        print(f"{label}: {len(group)}; with a nation {sum(1 for ns in group if ns)}; "
              f"in a nation with accents {listed}; old mix {len(group) - listed}")
    print(f"all names: {len(items)}; with a nation {sum(1 for _, ns in items if ns)}; accent-bound "
          f"{sum(1 for _, ns in items if name_family(ns, accents))}")
    c = Counter(tuple(sorted(ns)) for _, ns in items)
    for combo, count in c.most_common(30):
        print(f"  {count:5}  {', '.join(combo) or '(none)'}")


def read_previous_binding() -> dict[str, list[str]]:
    previous: dict[str, list[str]] = {}
    if NAMES_FILE.exists():
        for raw in NAMES_FILE.read_text(encoding="utf-8").splitlines():
            if raw and not raw.startswith("#"):
                f = raw.split("|")
                if len(f) >= 4:
                    previous[f[0]] = f[3].split(",")
    return previous


def write_rerender_list(personas: list[str], previous: dict[str, list[str]], out: Path) -> int:
    """voice|key for every bound clip that is not rendered yet: new against the previous names.txt and not on disk."""
    voices_dir = NAMES_FILE.parent
    pairs = sorted((v, k) for k, vs in bindings(personas).items() for v in vs)
    new_pairs = [(v, k) for v, k in pairs if v not in previous.get(k, [])]
    missing = [(v, k) for v, k in new_pairs if not (voices_dir / v / f"{k}.mp3").exists()]
    lines = ["# voice|key -- name clips bound by names.py (1.4.0 nation binding) that are not rendered yet.",
             f"# {len(missing)} clips ({len(new_pairs)} bindings are new against the previous names.txt). The render tool renders",
             "# every bound clip that is missing, so `chatterbox_render.py --voices all` picks these up without this list."]
    lines += [f"{v}|{k}" for v, k in missing]
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(missing)


if __name__ == "__main__":
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from cast import CAST
    ids = [p.id for p in CAST]
    if "--stats" in sys.argv:
        stats()
    else:
        previous = read_previous_binding()
        write_names_file(ids)
        out = Path(__file__).resolve().parent / "rerender_names_1.4.txt"
        count = write_rerender_list(ids, previous, out)
        print(f"{count} clips to render, listed in {out.name}")
