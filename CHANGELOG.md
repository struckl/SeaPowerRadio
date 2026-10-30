# Changelog

## 1.1.0 – settings in the Options menu, female voices, static, call volumes, overlapping voices

Player requests. **Not yet run in the game** (built and checked offline for both game versions: `modtool compat radio`).

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

## 1.0.4 – 2026-10-28 – both game versions: 0.8.7 and the 3D globe build (game build 26223125, the 11 November release)

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
