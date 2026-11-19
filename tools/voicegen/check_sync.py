"""Consistency checks between the voice catalog, the plugin and the game. Run before a full render and before a build.

1. src/SeaPowerRadio/Radio/ClipRules.g.cs is what rules_cs.py writes from rules.py and phrases.py today (callsign
   words, number ranges, named variants, phrase groups, sound table).
2. mod/voices/catalog.txt and stockmap.txt are what phrases.py writes today.
3. Every phrase key in a phrase group of rules.py (call volumes, moods) exists in phrases.PHRASES.
4. Every named variant (phrases.NAMED_VARIANTS) is one of its phrase's variants.
5. Every stock voice key the game can play (voice.ini [US] + literal call sites, docs/stock-voice-events.md) has
   an entry in phrases.STOCK_MAP, and every catalog key it points at exists.

Exit code 1 on any mismatch.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import rules  # noqa: E402
import rules_cs  # noqa: E402
from paths import REPO  # noqa: E402
from phrases import (CATALOG_FILE, NAMED_VARIANTS, PHRASES, STOCK_MAP, STOCKMAP_FILE,  # noqa: E402
                     catalog_text, stockmap_text)

problems: list[str] = []
phrase_keys = {p.key for p in PHRASES}


def same_text(path: Path, expected: str, fix: str) -> None:
    actual = path.read_text(encoding="utf-8").replace("\r\n", "\n") if path.exists() else ""
    if actual != expected:
        problems.append(f"{path.relative_to(REPO)} is out of date: run {fix}")


same_text(rules_cs.TARGET, rules_cs.render(), "python tools/voicegen/rules_cs.py")
same_text(CATALOG_FILE, catalog_text(), "python tools/voicegen/phrases.py")
same_text(STOCKMAP_FILE, stockmap_text(), "python tools/voicegen/phrases.py")

for group, keys in rules.PHRASE_GROUPS.items():
    unknown = sorted(set(keys) - phrase_keys)
    if unknown:
        problems.append(f"rules.py group {group} names phrases that do not exist: {unknown}")

variants = {p.key: p.variants for p in PHRASES}
for phrase, named in NAMED_VARIANTS.items():
    for name, text in named.items():
        if text not in variants.get(phrase, ()):
            problems.append(f"named variant {phrase}.{name} = {text!r} is no variant of {phrase}")

inventory = (REPO / "docs" / "stock-voice-events.md").read_text(encoding="utf-8")
stock_keys = set(re.findall(r"^\| [^|]+ \| `([^`]+)` \|", inventory, flags=re.M))
stock_keys |= set(re.findall(r"^- `([^`]+)` [-–]", inventory, flags=re.M))
missing = sorted(stock_keys - set(STOCK_MAP))
if missing:
    problems.append(f"stock keys without STOCK_MAP entry: {missing}")

dangling = {k: v for k, v in STOCK_MAP.items() if v is not None and v not in phrase_keys}
if dangling:
    problems.append(f"STOCK_MAP targets missing from PHRASES: {dangling}")

if problems:
    print("\n".join("x " + p for p in problems))
    sys.exit(1)
words = sum(len(w) for w in rules.CALLSIGN_WORDS.values())
print(f"ok: ClipRules.g.cs, catalog and stock map current; {words} callsign words, {len(stock_keys)} stock keys "
      f"mapped, {len(phrase_keys)} phrases, {sum(len(k) for k in rules.PHRASE_GROUPS.values())} group entries")
