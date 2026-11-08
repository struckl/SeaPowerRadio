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

## In-game test (2026-11-01, 0.8.7 flat map, flat DLL)

Run with Lukas's OK. The 1.2.0 DLLs and pack files were laid over both installed copies (StreamingAssets and the Workshop item),
plus a dummy pack (`test-dummy`, three relabelled English voices) in `StreamingAssets/RadioTestDummy`. Afterwards everything went
back to 1.1.0 and the dummy and its campaign memory were deleted.

- **Discovery:** both packs found; the base pack found in both copies, the StreamingAssets one kept (`found twice` info line).
- **NATO mission** (Intro 01 Red Sea Breakout): nation id `US`, three elements on `nato-en`, contact calls spoken. Not compared
  voice by voice with 1.1.0.
- **Soviet side, Auto** (Warsaw Pact > Breakthrough): all 64 elements `(game voice)`. `NewTaskReceived` and `NewContactMultiple`
  go to the game, and the mod queues nothing. Note: the task-force nation came as `soviet` once and as `Soviet` once.
- **Options page:** the Voice packs section with Installed line and NATO (17) / Warsaw Pact (7) / Other (53) groups that expand and
  collapse. The country is shown as `USSR` (id `Soviet`). The dropdown offers Auto (Game voice) / Game voice / NATO (English) /
  Test dummy (English). Choosing Test dummy for USSR wrote `Soviet = test-dummy` to the config at once.
- **Soviet side, test-dummy:** after reloading the mission, the roster used `test-dummy` voices, and `NewTasking` was spoken in a
  dummy voice.
- **Campaign** (Strike Group Molniya, mission 1): `[campaign] ... 4 units remembered`. The memory file holds `t:p1`..`t:p4`
  (the four ships' `CampaignTag`). Missions 2–5 use the same tags `p1`..`p4`, so the ships keep their voices there. **Not run:**
  mission 2 itself (needs mission 1 finished). Aircraft without a tag (the Yak-38 and Ka-25 placed in the mission) are not
  remembered, as designed. Left with Accept: no campaign save was written.
- No Radio exception in the log. **Not run:** the globe build, a dropdown change surviving a restart.

## Accents per nation and random voices (1.4.0)

- **`accents.txt`** (optional, in the pack folder): `nationId|accent,accent,...`, best first; accents are the `accent` column of
  `personas.txt`. A nation that is not listed uses the whole pack. A pack can declare its own accents and nations with data only.
  Base pack: `US|us,ca`, `UK|gb,sc,ni`, `Canada|ca,us`, `Australia|au`, `NewZealand|nz,au`.
- **Assignment** (`VoiceAssignment.For/Prefer`, accents of the unit's own nation): unused voices of the first accent (in the
  mission's shuffle order), then the next accent; once the whole family is used the voices are shared round-robin inside it,
  never a foreign accent. Used voices are tracked per pack, so two nations with overlapping families (US and Canada) avoid each
  other's voices while some are free. A woman is skipped with Female voices off, so a family without a man (New Zealand) moves to
  its next accent. A family with no allowed voice at all uses the whole pack. A named unit picks among the voices its name was
  rendered in that are of its nation's accents; if the name has none of them, the name's voices stay (the name matters more).
- Pins from `aliases.txt` and remembered campaign voices are applied first and unchanged. `Revoice` passes the same accents.
- **Random voices** (`Settings.RandomVoices`, `RosterService.RandomVoiceSalt`, `Roster/VoiceSalts.cs`): a salt is added to the voice
  seed (`voices|<mission>|salt:<salt>` for NATO, `voices|<mission>|salt:<salt>|<packId>` for others) and to the ranking of a named
  unit's candidate voices (`hash(seed|name|element|voice)`). Fresh start = new salt, saved per mission seed; a save loads the saved
  salt (none: plain seed). Callsigns are seeded without the salt. No salt in a linear campaign (`SceneCreator._isLinearCampaign`,
  memory in use) or a dynamic one (`Singleton<CampaignMain>` exists while a mission is fought from it, and the mission file is
  `user/missions/Campaign Mission.ini`; same in 0.8.7 and the globe build, both decompiled).
- **Names by nation** (`tools/voicegen/names.py`): a nation per name from the game's language files (banner lines of
  `vessel_names.ini` / `land_units_names.ini` / `aircraft_names.ini`, section prefix, hull prefix of the full name); a name used
  by several nations takes the union of their families; names without a listed nation keep the old three/eight voices of the
  whole cast. `python tools/voicegen/names.py --stats` prints the coverage.

## To test in the game (1.4.0)

1. Female voices off on a fresh config and after the one-time switch of an old config (on stays on after the user turned it on).
2. Random voices: the same mission started twice sounds different; loading a save keeps the voices of the latest start; a linear
   campaign keeps its remembered voices; a dynamic campaign mission has the same voices at every start.
3. Accents: a US task force speaks with us/ca voices only, a British one with gb/sc/ni, an Australian one with au; ships whose
   name clips are rendered say their names in those voices, others their generated callsign.
4. Options > Audio shows the new checkbox on both builds.

