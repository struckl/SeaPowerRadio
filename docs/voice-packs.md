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

## Campaigns: a unit keeps its voice and callsign

Added 2026-11-01 (Lukas): in a campaign every unit keeps the same voice and callsign for the whole campaign.

- **Why a seed is not enough.** `RosterService.MissionSeed()` and `Roster.Build()` shuffle per mission file, and the pool order
  shifts whenever the set of units changes. So a campaign has a **voice memory**, a small file
  `BepInEx/config/SeaPowerRadio/campaigns/<campaign id>.txt`, one line per unit: `key|packId/personaId|callsign`
  (`Roster/CampaignMemory.cs`). Delete a line to give that unit a new voice.
- **Is this a campaign?** `SceneCreator._isLinearCampaign` (public in 0.8.7 and the globe build; true when a linear campaign is
  playing a mission). Single missions, instant action and dynamic campaigns (`CampaignMain`) get no memory: their units carry no
  stable id in the decompiled sources (no `_campaignTag` is written for them), and a ship's name alone would be shared by
  unrelated missions. Nothing changes there.
- **Campaign id** = the folder of the mission file (the seed `RosterService` already builds, which for a save is the base
  mission file), lower-cased and sanitised; never the save file, so a new playthrough of the same campaign gets the same voices.
  The game itself names the runtime save from the same folder (`linear_campaign_<folder>.sav`).
- **Unit key.** `ObjectBase._campaignTag` (mission ini key `CampaignTag`; the game's stable per-unit id across the missions of a
  linear campaign; aircraft get theirs from `FlightDeck._campaignUnitsOnBoard` when they launch), else a ship's or shore
  unit's name. Aircraft without a tag are not remembered (formations rename them).
- **Flow.** At roster build: first every element gets the voice and callsign remembered for its key, then the named units, then the
  rest from the pool (remembered voices and callsigns are reserved, so new units avoid them while the pool lasts), then the memory is
  written (new units added). A hangar slot is bound to its memory entry when the aircraft launches and its tag is known. Late units
  are handled the same way.
- **When the memory is not used (the unit is reassigned and the file updated):** the pack of the unit's country is not the remembered
  one (setting changed, pack uninstalled), the persona no longer exists, Female voices is off and it is a woman's voice (as today,
  also live during the mission), or the unit's in-game name does not exist in that voice.
- **Not verified in the game:** that `_campaignTag` is set when the roster is built and that decks hand it over at launch. In the
  roster log (`[campaign] '<id>': N units remembered`) the second mission of a campaign shows it.

## Implementation notes (1.2.0, found while building it)

- **Where the nation comes from.** Player units: `ObjectBase.getNation()` (`Nation.Value`), the raw id (`US`, `Soviet`,
  `North_Korea`). The stock voice profile (`getVoiceoverNation()`, `us_male_1`…) is *not* used. The nation list of the game is the
  `[Nations]` section of `language_<lang>/nations.ini` (merged virtual file: `LocalizationLoader.OpenVirtualLocalizationFile(
  "language_xx", "nations", "nations")?.GetSectionKeyValues("Nations")`), identical in `decomp-0.8.7` and `decomp-beta`. The mod
  drops `Undefined`, `All`, `Civilian`, `Pirates`, `Terrorists` (no countries) and shows the rest, 73 ids in the 0.8.7 game
  (full list in `how-it-works.md`, section Nations). **Not confirmed in the game:** that a player unit's `getNation()` equals these
  ids (the decompiled `ObjectBase.setNation` and the `[VoiceProfiles]` keys use the same strings, so it should).
- **Discovery reads the disk, not `FileManager`.** `FileManager.Directories` differs between 0.8.7 and the globe build (the globe
  one has layers and mount points), so the mod lists `Application.streamingAssetsPath` sub folders (not `original`, `user`,
  `EntityScenes`), the Steam Workshop folder of the game (`<steamapps>/workshop/content/1286220/*`, found from `Application.dataPath`)
  and the base mod's own folder. It does not check whether a folder is ticked in the game's mod list.
- **Stock map.** A pack needs `catalog.txt`, `personas.txt`; `names.txt` and `stockmap.txt` are optional (without its own
  `stockmap.txt` a pack takes the base's, since the stock keys are the game's and language-independent).
- **Fallback to the game.** `StockVoiceRouter.Handle` returns false (the stock line plays) when the unit's country has no pack or
  the pack cannot say the line (no variant rendered, or a number piece missing). The pieces the game appends to that same call
  (`AppendMessage`) then also play (`passAppended`). A line the router swallows on purpose (mod-only building blocks) is still
  swallowed. Speed in knots and the mod's own calls (why not, fuel, SAR, contacts) build no transmission for a Game voice unit.
  Contact reports of a Game voice side: the mod's contact book is silent; the game's three contact callouts stay muted for the
  text part by `ContactAlertPatches` (the mod still raises its own linked messages) and the stock voice line plays.
- **Seeds.** The NATO pack keeps the 1.1.0 seeds (`voices|<mission>`, `callsigns|<mission>`), so a NATO mission gets the same
  voices and callsigns as before. Other packs shuffle with `voices|<mission>|<packId>`; callsign words are shared per *language*.
- **Settings binding.** The per-country entries are bound on first use; `nations.txt` ids and the keys of an earlier run's
  `10. Voice packs` section are bound at start so that BepInEx's save never drops a choice.
- **Not in 1.2.0:** a changed choice does not rebind a running mission (next mission start), and Language rules other than `en`.
- **Group `NATO`** (`nations.txt`) holds the NATO members of the period plus Australia and New Zealand, and the pack's `Nations=`
  lists exactly that group, so every NATO country gets the English voices under `Auto` (Lukas, 2026-11-01: "yes for now"). Keep
  `voicepack.ini` and the NATO lines of `nations.txt` in step; a country dropped from `Nations=` falls back to the game's voice.

## Offline test (what was run, 2026-11-01)

A scratch folder outside the repo built like the game's Workshop folder: the built `SeaPowerRadio.dll` with `voices/` (the real
catalog files and four real persona folders) as item `3826433579`, a dummy pack `test-dummy` (Nations=Soviet, three English
voices relabelled `test_m_01..03`, no `stockmap.txt`, version 1.1.0), a lower-version duplicate of it, and a pack whose
`MinBaseVersion=9.0.0`. A small console harness loads the DLL and drives it by reflection (BepInEx config in the scratch folder).
Result (all passing): both packs found, duplicate keeps 1.1.0, newer-base pack skipped with a warning; `US`/`UK` Auto → `nato-en`,
`Soviet` Auto → `test-dummy`, `Poland` Auto → game voice, `Game voice`, an explicit pack, and an uninstalled pack id (acts as Auto,
logs once); the dummy voice finds a phrase and its number clips, a phrase the pack lacks gives no variant (so the router leaves it to
the game), the pack without `stockmap.txt` uses the base's; voice assignment has one pool per pack (`test-dummy/test_m_02`, …).
The dummy's audio was never committed or staged anywhere but the scratch folder. **Not testable offline:** the roster (needs game
objects), `StockVoiceRouter.Handle` end to end, the options page and playback.

## To test in the game (with Lukas's OK, `game-testing.md`)

1. 1.1.0 checklist on 0.8.7 and on the globe build; a NATO mission sounds unchanged (same voices and callsigns as 1.1.x).
2. A Soviet mission: the game's Russian voice speaks every line; no mod voices, callsigns or radio noise; contact reports and
   why-not calls silent (their messages still show); the roster log lists the units as `(game voice)`.
3. Soviet set to *NATO (English)* in the options: from the next mission the old behaviour; the choice survives a restart.
4. The dummy pack (a scratch copy in `StreamingAssets/<folder>`, never published) set for Soviet: its voices speak, a line it lacks
   plays the game's own line (check the numbers: pieces and appended parts).
5. Options > Audio: the Voice packs part builds on both versions (Expander and ComboBox in both Noesis versions); groups start
   collapsed, expand/collapse works, drop-downs open, change and persist; installed packs line; the list of countries is complete
   and in the right groups; nothing breaks the rest of the Audio page.
6. A mixed task force (US ship with a Soviet unit, if a mission has one): each unit its own country's voice.
7. **Voice stays across two campaign missions:** play two missions of one linear campaign; the units that appear in both have the same
   voice and callsign (roster log + `BepInEx/config/SeaPowerRadio/campaigns/<id>.txt`), including after a save/load and in a new
   playthrough; a unit new in mission 2 gets a voice nobody remembered used; switching a country's pack or Female voices off changes
   the affected units and updates the file.
8. `getNation()` equals the nation ids of `nations.ini` for player units (the roster log prints the nation next to each voice).
