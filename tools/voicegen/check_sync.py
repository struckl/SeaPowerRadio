"""Consistency checks between the voice catalog, the plugin and the game. Run before a full render.

1. Callsign words in phrases.py == the word lists in src/SeaPowerRadio/Voices/Callsigns.cs.
2. Every stock voice key the game can play (voice.ini [US] + literal call sites, docs/stock-voice-events.md) has
   an entry in phrases.STOCK_MAP.
3. Every catalog key referenced by STOCK_MAP exists.

Exit code 1 on any mismatch.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from phrases import CALLSIGN_WORDS, PHRASES, STOCK_MAP  # noqa: E402

REPO = HERE.parents[1]
problems: list[str] = []

cs = (REPO / "src" / "SeaPowerRadio" / "Voices" / "Callsigns.cs").read_text(encoding="utf-8")
block = cs.split("Words =", 1)[1].split("};", 1)[0]
cs_words = re.findall(r'"([A-Z][a-z]+)"', block)
if sorted(cs_words) != sorted(CALLSIGN_WORDS):
    problems.append(f"callsign words differ: only C# {set(cs_words) - set(CALLSIGN_WORDS)}, "
                    f"only Python {set(CALLSIGN_WORDS) - set(cs_words)}")

inventory = (REPO / "docs" / "stock-voice-events.md").read_text(encoding="utf-8")
stock_keys = set(re.findall(r"^\| [^|]+ \| `([^`]+)` \|", inventory, flags=re.M))
stock_keys |= set(re.findall(r"^- `([^`]+)` –", inventory, flags=re.M))
missing = sorted(stock_keys - set(STOCK_MAP))
if missing:
    problems.append(f"stock keys without STOCK_MAP entry: {missing}")

phrase_keys = {p.key for p in PHRASES}
dangling = {k: v for k, v in STOCK_MAP.items() if v is not None and v not in phrase_keys}
if dangling:
    problems.append(f"STOCK_MAP targets missing from PHRASES: {dangling}")

if problems:
    print("\n".join("x " + p for p in problems))
    sys.exit(1)
print(f"ok: {len(cs_words)} callsign words, {len(stock_keys)} stock keys mapped, {len(phrase_keys)} phrases")
