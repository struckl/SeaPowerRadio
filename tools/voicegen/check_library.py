"""Is the voice library complete? Every persona in cast.py must have every clip of phrases.TAKES.

Prints a summary (and the first missing clips per persona). Exit code 0 = complete, 2 = clips missing.

    python tools/voicegen/check_library.py
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import CAST, REPO  # noqa: E402
from phrases import takes_for  # noqa: E402

VOICES = REPO / "mod" / "voices"


def main() -> int:
    missing_total = 0
    total = 0
    for persona in CAST:
        keys = [k for t in takes_for(persona.id) for k in t.keys]
        total += len(keys)
        have = {p.stem for p in (VOICES / persona.id).glob("*.mp3") if not p.name.endswith(".part.mp3")}
        missing = [k for k in keys if k not in have]
        missing_total += len(missing)
        if missing:
            print(f"{persona.id:8} missing {len(missing):4}: {', '.join(missing[:6])}{' ...' if len(missing) > 6 else ''}")
    print(f"{len(CAST)} personas, {total} clips: {missing_total} missing")
    return 2 if missing_total else 0


if __name__ == "__main__":
    sys.exit(main())
