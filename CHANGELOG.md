# Changelog

## 1.4.4 – 2026-11-11 – declared for game 0.9.0

- **Declared for game 0.9.0.** The mod's `_info.ini` now says it fits 0.8.7 up to every 0.9.x, so the game no longer lists it as "not declared to be compatible", and Steam hands this and later updates to 0.9.0 players (the game skips Workshop updates whose declaration does not fit its version). Checked offline against 0.9.0 (build 261111): every game type, member, Harmony target and by-name lookup the mod uses is there; not run in the game on 0.9.0 yet.

## 1.4.3 – 2026-11-10 – squadrons.txt matches the names players see

- **Squadron rules now apply** (bug report: `Viper = female` left the T-20 squadron launching as "Viper 1" with mixed
  voices). 1.4.1 compared the rule only with the mission file's internal key ("Squadron1"), never with a name the player
  sees, so a rule by name could not match. A rule now matches the squadron's shown name, its short name, its callsign
  words ("Viper") and the internal key. The hangar lines in the log list all of them. Compiled only, not run in the game.

## 1.4.2 – 2026-11-09 – callsign words in aliases.txt

- **An alias can keep a generated callsign** (bug report: `Akira O-ren = Victory` logged "no voice pack has the name
  Victory"). `aliases.txt` only accepted base-game ship names; it now also takes the ship, submarine and shore callsign
  words (Victory, Courage, Castle, …). The ship gets that callsign in every mission. No other unit is handed the word, and
  it wins over a campaign's remembered callsign. Generation now skips a taken word before falling back to a numbered one.
  The template in a newly created `aliases.txt` lists the words. Compiled only, not run in the game.

## 1.4.1 – 2026-11-09 – voice gender per squadron

Compiled for both game versions against the reference copies (`_gamerefs`); `python tools/modtool.py compat` passes. Not run in the game.

- **Voice gender per squadron, in a text file** (no menu setting): `BepInEx/config/SeaPowerRadio/squadrons.txt`, lines like
  `Bear Aces = female` or `Ghostriders = male`. Every pilot of a matching air-wing squadron speaks with a voice of that
  gender, over the Female voices setting. A name without `*` matches every squadron whose name contains it, `*` is a
  wildcard, the last matching line wins. Applies from the next mission; the file is created with examples. A squadron
  callsign word that exists only in voices of the other gender keeps that voice. See [docs/squadron-voices.md](docs/squadron-voices.md).

## 1.4.0 – 2026-11-09 – accents follow the nation, random voices each mission, female voices off

- Fuel calls on globe beta 261107: the game replaced its fuel curves with a performance model; the calls read the new model and the old curves by name. Run in the game on 261107: all five aircraft in the test mission watched, no scan errors.

Run in the game on the globe beta (build 261107) on 2026-11-09: the mod starts under Keel and a mission's radio calls play (a missile warning call in Intro 01), and the fuel calls were watched on five aircraft earlier. Not run in the game: the accent matching by nation, random voices each mission and the female-voices default (they only change which voice is picked); 0.8.7. Built for both versions against the reference copies (`_gamerefs`); `python tools/modtool.py compat` passes.

- **Fuel calls follow the new fuel model of build 261107.** The game dropped `AircraftParameters._altitudeEfficiencyCurve` /
  `_rangeMultiplierCurve` and `HelicopterParameters._hoveringFuelConsumption` and now keeps the fuel burn per km itself
  (`_fuelPerKmCruise`, `_fuelPerKmNow` in kg per km, refreshed by the game). Radio reads those two values (economy = the
  game's cruise rate, the one it reckons the way home with; cruise = the greater of that and the rate at the speed flown)
  and converts them to the units of the range figure with the game's own `CruiseKmForFuel`. Older builds (0.8.7, globe
  26223125) keep the curve model. The members are looked up by reflection, so the same DLL loads on every build; if neither
  model can be read, the fuel calls stay off and one line goes to the log, nothing is thrown. The fuel reserve the game
  holds back is not subtracted: the calls come slightly later than the game's own bingo.
- **Female voices default to off.** A config saved with the old default (on) is switched to off once (the setting
  `1. General / Config migration` is the marker); a player who turns it back on keeps it.
- **New setting "Random voices each mission"** (default on, config `2. Radio` and Options > Audio). A fresh start of a mission
  shuffles the voices with a new random salt, and also the choice among a named unit's candidate voices, so ships change voice
  too. Loading a save reuses the salt of that mission's latest start (`BepInEx/config/SeaPowerRadio/voice-salts.txt`; none
  on record = the plain seed). Generated callsigns do not change. Not applied in linear campaigns (the voice memory decides)
  and dynamic campaigns (`CampaignMain` loaded, or the mission file `Campaign Mission.ini`). Off = the voices of 1.3.0.
- **Accent matching.** A voice pack may carry `accents.txt` (`nationId|accent,accent,...`, best first). The base pack:
  US us,ca; UK gb,sc,ni; Canada ca,us; Australia au; NewZealand nz,au. A nation's units take unused voices of its first
  accent, then the next; when the family is taken, voices are shared inside it. With female voices off a family without a man
  moves on to the next accent (New Zealand to Australian). Nations not listed (the other NATO countries) use the whole pack
  as before. Alias pins and remembered campaign voices win; the female-voices switch during a mission keeps working.
- **Names bound by nation.** `tools/voicegen/names.py` now binds ship names and squadron words to the accent family of the
  nation they belong to (the game's language files: section banner, section prefix, hull prefix), 3 men and 1 woman per ship
  name as far as the family has them. Names of no known or unlisted nation keep their old binding. **The new name clips must
  be rendered** (`tools/voicegen/rerender_names_1.4.txt`); until then a ship says its generated callsign.
- Voice pack `nato-en` 1.1.0 (names.txt, accents.txt).

## 1.3.0 – 2026-11-03 – name aliases for mod ships

**Not run in the game.** Built for both versions, `python tools/modtool.py compat` passes. Vanilla ships are not affected:
the aliases and prefixes are only tried for a name that has no clip of its own (or that a player listed). If loading the
alias files fails, the mission goes on without aliases.

- **Hull prefixes stripped.** A ship whose name has no clip is looked up again without a hull prefix (USS, HMS, HMCS, HMAS,
  FGS, FS, INS, HNLMS and about 25 more, `rats-radio-aliases.txt` in the mod folder), so a modded "USS Pargo" says "Pargo"
  instead of a generated callsign.
- **`aliases.txt`** in `BepInEx/config/SeaPowerRadio/` (created with examples at the first mission; Workshop updates
  never touch it): `game name = name to say | voice`. Maps a mod ship onto any name that has clips, optionally pins it to one
  voice (`gb_m_06` or `nato-en/gb_m_06`), or only pins the voice (`= | gb_m_06`). A `*` at the end of the game name matches
  every name that starts with it; `*prefix` lines add prefixes. A pinned voice wins over the campaign memory and stays when
  female voices are switched off; if the name has no clip in that voice, the ship keeps the voice and says its callsign.
  Read at every mission start. A line whose name or voice does not exist is logged. The roster log marks aliased ships.
- **Ship mods can ship aliases.** A `rats-radio-aliases.txt` in any mod folder (StreamingAssets or Workshop) is read too;
  the player's own file wins.

## 1.2.0 – 2026-11-03 – voice packs: every country picks its own voice pack

**Run in the game on 0.8.7 (flat map); the 3D-globe build is not run yet.** Built for both versions (default and
`-p:GameBuild=Flat`), `python tools/modtool.py compat` passes. In the game on 0.8.7: pack discovery, a NATO mission, a Soviet
mission on the game voice and on a test pack, the Options page and the campaign voice memory (`docs/voice-packs.md`, section
"In-game test").

**What players of non-NATO sides will notice: they now hear the game's own voice by default** (the Soviets: the game's
Russian voice), not the English radio voices of 1.1.x. Reason: the 43 voices are English and the base ships them as the
**NATO (English)** pack, which lists the NATO group (US, UK, Canada, France, Germany, Italy, the Netherlands, Belgium, Denmark, Norway, Iceland, Portugal, Greece, Turkey, Spain, plus Australia and New Zealand). Anyone who wants the old behaviour
for another country picks *NATO (English)* for it in Options > Audio > Rats Radio > Voice packs (or sets the country to
`nato-en` in the config). NATO players notice nothing: their voices, callsigns and the seed per mission are
unchanged.

- **Voice packs.** The base's `voices/` folder is now a pack (`voicepack.ini`: Id, Name, Language, Nations, Version,
  MinBaseVersion) and goes through the same code as packs from other mod folders. At start the mod scans every mod folder
  of the game (Steam Workshop items and `StreamingAssets/<folder>`) for a `voicepack.ini` at the top level or in `voices/`.
  Two packs with the same Id: the higher Version wins. A pack that needs a newer base is skipped with a log line. Personas,
  phrase catalog, in-game names and clips are per pack; a voice is `packId/personaId`. Packs are data only, no DLL.
- **Per unit, by its own nation.** Each player unit speaks with the pack set for *its own* country, so a mixed task force
  works. The setting per country is `Auto` (the installed pack that lists the country, else the game's voice), `Game voice`
  or a pack. With **Game voice** the mod stays silent for those units: the game's own line plays as without the mod, mod-only
  calls (why not, fuel, search and rescue, contact reports) are not spoken (their text messages stay), and the unit gets no
  mod callsign. A line the pack has no clip for also falls back to the game's own line.
- **Settings.** Config section `10. Voice packs`, one entry per country (key = the game's nation id, default `Auto`).
  Options > Audio > Rats Radio has a **Voice packs** part: the installed packs, then the countries in collapsible groups
  (NATO, Warsaw Pact, Other), one drop-down per country. The country list is the game's own (`nations.ini`), the groups come
  from `voices/nations.txt`; a nation that is not listed lands in Other. A changed choice applies from the next mission.
- **Campaigns keep voices.** In a linear campaign every unit keeps its voice and callsign from mission to mission, remembered per
  campaign in `BepInEx/config/SeaPowerRadio/campaigns/<campaign>.txt` (key = the game's campaign tag, else a ship's name). A new
  playthrough of the same campaign gets the same voices. A voice is reassigned (and the file updated) when the country's pack
  changed, the pack is gone or Female voices is off. Single missions are unchanged.
- Number and callsign rules are selected by a pack's `Language` (only `en` exists; an unknown language falls back to it with
  a warning).
- `tools/package.ps1 -Pack <id>` stages a pack folder (`dist/pack-<id>`) for its own Workshop item.

## 1.1.0 – 2026-10-31 – settings in the Options menu, female voices, static, call volumes, overlapping voices

Player requests. **Run in the game on 0.8.7 (2026-10-31):** the Rats Radio group shows in Options > Audio from the main menu
and from the pause menu; slider changes are saved to the config within about a second and kept across menus; the call
categories show in the log (Contacts, Warnings, Combat, Orders); overlapping voices seen (a ship on channel 2 while an
aircraft spoke on channel 1, never the same unit twice); Female voices switched off during a mission moved two speakers to
male voices and the next call used one. Loudness and static were not judged by ear. **The globe build is checked offline
only** (built, `modtool compat radio`); its Options page XAML could not be compared, so if the group is missing there the log
says so and the settings stay in the config file.

- **Settings page**: Options > Audio has a new group **Rats Radio** under the game's own audio settings. It is added when the
  Options page loads (same kind of hook as the formation manager's role line), with the game's own styles; changes apply at
  once and are saved to the BepInEx config (`dev.sewerlabs.seapowerradio.cfg`), which stays the backing store, so existing
  config entries keep working. Accept / Revert on that page do not affect these settings. If the game's page ever changes so
  that the Audio tab cannot be found, the mod logs one line and the settings stay in the config file.
- **Female voices** (default on): off = units get male voices only (new config entry `Female voices`). Applies to the next
  mission; switched off during a mission, the speakers that have a female voice move to a male one at once. A unit whose name
  was rendered only in a female voice keeps it, because saying the right name matters more.
- **Radio static**: the slider is the existing `Radio noise` entry (0 = clean voice, 100 % = the default amount, up to 150 %
  on the page; the config file accepts up to 3). Applies from the next call.
- **Volumes**: `Volume` is now the master radio volume (values above 1 are made louder in the clip, as an audio source cannot go
  above 1) and applies to the call that is playing. New volume per kind of call, in config section `8. Call volumes`:
  contacts and tracks (new / lost / identified), weapons and combat, warnings and damage (inbound missile or torpedo, we're
  hit, flooding, bingo, emergencies), replies to orders (incl. refusals), other (air ops, fuel, search and rescue, mission
  messages). 0-200 %, default 100 % = as before; the existing `Reports volume` (background traffic is half as loud as replies
  and alerts) still applies underneath.
- **Overlapping voices** (default on; `Voices at once`, default 3, in the config): a new call can start 0.6-1.4 s after the
  last one began while another is still playing, on its own audio channel panned a little left or right; never the same unit
  (or roster entry) twice at once. Urgent calls cut into the oldest routine call when all channels are busy. Off = the old
  behaviour, one call at a time.

## 1.0.5 – 2026-10-28 – fix: no mission could be started on 0.8.7 with 1.0.4

**1.0.4 must not be used on 0.8.7.** Its globe DLL stays off there, but Unity's ECS start-up loads every type of every
loaded assembly, and one internal class of the globe DLL (`GameApi`) held a field of a type that exists only in the globe
game (`Func<MapPalette, bool>`). On 0.8.7 that threw a `TypeLoadException` at start-up, ECS failed, and every mission failed to
load with a `NullReferenceException` in `PlottingTableSerializer.RecreateWorldUsingTemp`. The globe build was not affected.
Found by running 1.0.4 on 0.8.7 in the game; the offline compat check looked only at the entry point, not at the types'
fields. Fixed: the field is a plain `Delegate`. `tools/compat-check --classload` (run by `tools/package.ps1`) now checks the
base types, interfaces and fields of every type of each DLL against the other game, so the package cannot be staged with it.
**Run in the game on 0.8.7 (2026-10-28, Dangerous Straits 1985, mouse-free):** the mission loads; the flat build runs and the globe
build stays off; all ten features hooked; roster seed, contact calls and the order call are spoken; returning aircraft (RTB) are
dark gold on the map and in the formation manager; the unit roles show in the formation manager; borders, cities and the
topographic overlay (the other two mods) work next to it. Spoken refusals ("unable, target outside envelope") and SAR calls
were tested in the game on 0.8.7 afterwards (2026-10-28).
The globe build was last run in the game before this change (2026-10-28); the change only touches how one field is typed
(the day/night lookup now goes through a cast), and the globe build is not re-run with it yet.

## 1.0.4 – 2026-10-28 – meant for 0.8.7 and the 3D globe build (game build 26223125, the 11 November release); checked offline with compat-check only, and it broke 0.8.7

**Known problem: on 0.8.7 no mission loads with this version - use 1.0.5.**

Sea Power exists in two versions with different map code, and a mod compiled against one fails on the other (1.0.3
would lose its RTB colours on the new build, and its roster seed reads a game field that is a property there). The
package now carries **two builds of the same sources**: `SeaPowerRadio.dll` for the 3D-globe game and
`SeaPowerRadioFlat.dll` for 0.8.7 (the code of 1.0.3 plus the entry check); each looks at the game it runs on and
stays off on the other, and the log says which runs. Checked offline with `tools/compat-check`: every DLL has nothing
missing on its own game, and its entry point nothing missing on either; all 20 hooked methods and the one internal
field resolve in the new game assembly (except the one below, which differs by version). **Run in the game on the globe build
(2026-10-28, Trollfjorden 1985, mouse-free):** both builds load, the globe build runs and the flat one stays off, all ten
features are hooked, the roster seed works, calls are queued and played with no error; the RTB colours are not seen yet (no
aircraft went bingo). 0.8.7 with the flat build is not yet run.

- **Returning aircraft (RTB highlight)**: the game's map-symbol colour call now takes the map as a second argument
  (`MapContactVisuals.Resolve(unit, map)`), and the global day/night theme is gone - every map has its own palette.
  The hook follows the new signature and takes the yellow for day or night from the palette of the map being drawn;
  the formation manager rows take it from the player's tactical map.

## 1.0.3 – 2026-10-27

- Refusals no longer repeat while the ship closes the range: a unit gives the same reason about the same target
  once (again after 5 minutes at the earliest) instead of every 40 seconds, and a formation ordered to attack
  together gets one call – the first ship to give a reason says it, the others only put up their message.
- Speed orders say the right way and where to: 1/3 → 2/3 said "slowing down". Now "coming up to two thirds",
  "slowing to one third", "coming up to standard" …, "ahead full" and "ahead flank" as before. A speed set in knots
  with the slider, which had no voice line at all, is said too: "coming to one five knots" / "making one five knots".
- Altitude orders too: a descent said "climbing, angels five" half the time. Now "climbing, …" below the new
  altitude, "descending, …" above it, "leveling off, …" at the altitude it already holds – always with the ordered
  altitude ("angels twenty", "five hundred feet"). Depth orders already name the depth.
- New lines in all 43 voices: the six telegraph steps above, "descending, …" and the two knots lines.
- No more clipped first sounds: "stand by, reloading …", "steady on …" and "splash one" lost their "s" in about
  half the voices ("'tand by") – the clip was cut in the short silence between the s and the t / p. Those lines are
  cut again in every voice.
- Launch calls fit the weapon: a Tomahawk at a land target said "torpedo away" (the game's line for a ship's
  missile at a land target was taken for an anti-submarine missile), at a position "harpoon away". Now "harpoon
  away" only for Harpoons, "anti-ship away" for other missiles at a ship, "birds away" / "missile away" for land
  attack, and "torpedo away" / "fish in the water" for torpedoes and ASROC (they said "weapon away").

## 1.0.2 – 2026-10-25

- Refusals: a ship ordered to fire a fixed-launcher missile (Harpoon) out of range said "negative, sonar contact
  only" – the order waits while the ship turns, and the mod didn't see the game's own reason there. It now says
  "unable, target outside envelope", like the game's status bar. "Sonar contact only" is only said when sonar is the
  only sensor holding the contact (the group's own passive sonar hearing a ship that radar and ESM also hold made it
  look like a sonar contact).
- No more repeated calls: a sinking ship's rafts, put out one by one, give one "survivors in the water" instead of
  one per raft; a raid found aircraft by aircraft gives one "new air contact" and one "multiple contacts" instead of
  "new air contact, bearing 327" for every aircraft. The messages bottom right still list every raft and track.

## 1.0.1 – 2026-10-25

- No more long pauses before numbers: "bearing two seven three", "track seven zero three four" run together the
  way they are said. The silence at every join between clips is cut back – to 0.05 s between a word and its number,
  0.08 s between digit groups; pauses the sentence has (after the callsign, "track 7034, faded") stay, up to 0.25 s.
  Same voices, no re-render; `catalog.txt` gained two columns (does the text pause before / after the number).

## 1.0.0 – 2026-10-24 (first Workshop release)

Tested on Sea Power 0.8.7.

- 43 pre-rendered voices (based on speakers of the VCTK corpus, rendered with Chatterbox) through a radio chain;
  every player unit and every aircraft a carrier or airbase can launch gets its own voice and a NATO callsign, stable
  per mission.
- The game's stock voice lines muted and re-spoken by the unit that said them; brevity on the net (callsign only
  when the speaker changes).
- Why-not diagnostics: units say why they don't fire or follow an order; order queue window (F8).
- Contact reports without spam (hysteresis, cooldowns, flicker detection), track numbers clickable; every unit and
  track a message mentions is a link to it.
- Fuel calls (joker / bingo / emergency against the nearest field that takes the aircraft), RTB aircraft yellow on
  the map and in the formation manager, search and rescue calls.
- Unit roles under every unit in the formation manager, in the game's own words and language.
