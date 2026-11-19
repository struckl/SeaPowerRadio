"""Where the voice tools read and write."""

from __future__ import annotations

import os
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
# The base voice pack: clips per persona and the data files the plugin reads (catalog, stock map, names ...).
VOICES = REPO / "mod" / "voices"
# The game install, for its unit names (names.py). SEAPOWER_GAME_DIR overrides it.
GAME_DIR = Path(os.environ.get("SEAPOWER_GAME_DIR") or r"E:\Games\steamapps\common\Sea Power")
