# Voice packs (base 1.2.0)

Design agreed with Lukas on 2026-11-01. This replaces phase 1 of [languages-roadmap.md](languages-roadmap.md); the
later phases (Russian, Spanish, ...) stay as planned there.

## Goal

- Rats Radio becomes modular: the base mod ships the **NATO (English)** pack, which is today's 43 voices. More
  languages come later as separate Workshop items (*voice packs*) that need no DLL of their own.
- Every country can be given its own voice pack in the settings.
- A country with no pack speaks with **the game's own voice**, so the stock line plays. At release only NATO has
  our voices. Playing the Soviets means hearing the game's Russian voice (`wp_male_1`), not English.

## Pack format

A pack is a folder with a `voicepack.ini`:

```ini
[Pack]
Id=nato-en                 ; unique, folder-independent, stored in the config
Name=NATO (English)        ; shown in the dropdowns
Language=en                ; picks the number / callsign rules in the base (see below)
Nations=US,UK,Australia,NewZealand,Canada   ; countries that use it under "Auto"
Version=1.0.0
MinBaseVersion=1.2.0       ; older base: pack is skipped with a log warning
```

Next to it go the existing files, unchanged in format: `personas.txt`, `catalog.txt`, `names.txt`, `stockmap.txt`
(if the pack needs its own) and one folder of clips per persona.

- The base's `mod/voices/` becomes this pack (`mod/voices/voicepack.ini` is added). NATO goes through the same code
  path as every other pack.
- **Discovery:** at startup, scan every folder the game loads mods from (Workshop items and
  `StreamingAssets/<folder>`, found the same way the game and Anchor Chain do) for `voicepack.ini` at the top level
  or in `voices/`. If two packs have the same `Id`, keep the higher `Version` and log it.
- Persona ids only need to be unique inside a pack. Internally a voice is `packId/personaId`.
- **Language rules** (numbers, altitudes, callsign formats) stay in code, keyed by `Language`. The first pack of a
  new language may need one base update for its rules; more packs in the same language are data only. `en` is
  today's code; an unknown language falls back to `en` rules with a warning.

## Choosing the voice per unit

- For each player unit, look up its **own nation**. In a mixed task force each unit uses its own country's
  setting.
- Setting values per country:
  - `Auto` (default): the installed pack whose `Nations` lists the country, otherwise **Game voice**.
  - `Game voice`: the mod never speaks for this country's units, and the stock lines play as without the mod.
  - `<pack id>`: that pack.
- `VoiceAssignment` gets one pool per pack in use (today the pool is filtered to `en`). Shuffle, uniqueness, the
  female voices toggle and named units' preferred voices work per pack exactly as now.
- **Missing voice:** if the unit's pack has no clip for a line (no phrase key in its catalog, or no pack at all),
  `StockVoiceRouter.Handle` returns false, so the game's own line plays. Calls that exist only in the mod (why not,
  fuel, SAR, contact reports) have no stock line. For a Game voice country they stay silent; their text message
  still shows.
- Callsigns: a Game voice unit keeps the game's naming (no mod callsign in text either). A unit with a pack gets
  its callsign from that pack's `names.txt` and language rules.
- A changed setting takes effect at the next mission start (like Female voices). It may rebind live if that is
  easy and safe, but this is not required.

## Settings

- Config section `10. Voice packs`: one `ConfigEntry<string>` per country, key = the game's nation id,
  default `Auto`. Unknown values (a pack that was uninstalled) act as `Auto` and log once.
- **Options page** (Audio tab, under the existing Rats Radio group): a subsection "Voice packs" with
  **collapsible groups**: NATO, Warsaw Pact, Other (neutral / non-aligned). Each group lists its countries, one row
  per country: name + dropdown (`Auto (<what it resolves to>)`, `Game voice`, every installed pack by `Name`).
  Groups start collapsed. A line above lists the installed packs with their versions.
- The country list comes from the **game's own nation data** (every nation the game knows, also outside a
  mission). The group of each nation comes from a small table in the base (`mod/voices/nations.txt`,
  `nationId|group`). Nations not in it go to Other, so a game update with new nations needs no mod update.
- Find out in the decompiled sources (`_gamerefs/decomp-0.8.7`, `_gamerefs/decomp-beta`) where the nation list
  lives and which nation strings player units and `VoiceMessage` carry. Confirm in the game (with his OK) that they
  match. The roadmap lists this as a risk.

## Both game versions

The new code touches the options page (Noesis) and the voice routing only. Build both variants
(`-p:GameBuild=Flat`), and `python tools/modtool.py compat` must pass. The dropdown/expander controls must exist
in both builds' Noesis.

## Testing

- Offline: a **dummy pack** (`Id=test-dummy`, 2–3 English voices copied and relabelled, `Nations=Soviet`) in a
  scratch mod folder outside the repo, **never published or committed** with audio. Log output shows discovery,
  the resolved pack per country and the per-unit voice in the roster log.
- In the game (only with Lukas's OK, see `docs/game-testing.md`): the 1.1.0 checklist on 0.8.7 and the globe build,
  plus: a NATO mission sounds unchanged; a Soviet mission plays the game's Russian voice; the dummy pack set for
  Soviet speaks in its voices; a dropdown change survives a restart; groups expand/collapse.

## Release

Base 1.2.0 with NATO only. English-only players notice nothing, but **players of non-NATO sides now hear the
game's voice instead of English** by default. Say this in the CHANGELOG and the change note. They can pick NATO
for that country to get the old behaviour. `package.ps1 -Pack <id>` stages a pack folder for upload later.
