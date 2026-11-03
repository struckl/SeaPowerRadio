# Name aliases (mod ships)

A ship or shore unit says its in-game short name only if that name was rendered as a clip in its voice. The names come
from the base game's language files (`tools/voicegen/names.py` → `voices/names.txt`, about 3,000 names, each in three
voices). Mod ships often carry a hull prefix ("USS Pargo") or a name of their own, find no clip, and fall back to a
generated callsign. Aliases fix the first case completely and the second as far as a base-game name fits.

## Files

Read at every mission start, in this order; a later line for the same game name wins:

1. `rats-radio-aliases.txt` in the base mod's folder: the default `*prefix` list. Overwritten by updates.
2. `rats-radio-aliases.txt` in any other mod folder (`StreamingAssets/<folder>`, Workshop items), so a ship mod can ship
   aliases for its ships.
3. `BepInEx/config/SeaPowerRadio/aliases.txt`, the player's own. Created with commented examples when missing.

## Format

```
# comment
game name = name to say | voice
USS Pargo = Pargo
HMS Bristol = Bristol | gb_m_06
HMS Bristol = | gb_m_06
CVN-68* = Nimitz
*prefix HNoMS, HSwMS
```

- **game name**: the unit's short name or full name as the game shows it. Compared like `names.txt` keys (lower case,
  anything but letters and digits counts as one `_`), so case and punctuation do not matter. A trailing `*` matches every
  name that starts with it; the longest match wins.
- **name to say**: a name from `names.txt` (any pack's). Optional when a voice is given.
- **voice**: a persona folder of the unit's pack (`gb_m_06`) or `packId/personaId`. Pins the unit to that voice for every
  mission; it wins over the campaign memory and the name's own voices, and stays when female voices are switched off. If
  the name has no clip in that voice, the unit keeps the voice and says its generated callsign. A voice of another pack than
  the one the unit's country uses is ignored (logged).
- **`*prefix`**: comma- or space-separated hull prefixes. A name without its own clip and without an alias is tried again
  with the prefix removed (`USS Pargo`, `USS. Pargo`, `USS-Pargo` → `Pargo`).

## Lookup

For a ship (aircraft are not aliased: formations rename them), with its short name, then its full name:
alias → its name to say; else the name itself; else the name without a prefix. The first that has a clip in the unit's
pack is the one it says (`Roster.NameOf`). The voice pin is applied when the roster is built, before the campaign memory.

## Log

`[aliases] N aliases, M prefixes from K file(s)` at every mission start; a warning for a line without `=`, a name no pack
has, or a voice that is not installed. With the roster log on, aliased units carry `(alias)`.
