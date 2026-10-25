# In-game test checklists

Log: `<Sea Power>\BepInEx\LogOutput.log` (overwritten on every game start). Config:
`<Sea Power>\BepInEx\config\dev.sewerlabs.seapowerradio.cfg`. Installed mod (local build):
`<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerRadio\` (DLL + 43 voices, ~390 MB).

The checklists are in the order the features were built; each still applies.

## Roster and the new voices (2026-10-17)

The game's own voice lines are now replaced: every unit speaks with its own voice and callsign, through a radio
filter. Contact reports come from the mod (new / lost / intermittent / hostile), spoken by the unit whose sensor
found the contact. (The why-not lines have their own checklist below.)

1. **Start Sea Power** (fresh start – BepInEx only hooks in at process start).
2. Main menu → Mods: "Sea Power Radio" is listed and enabled.
3. Start a mission with player ships, aircraft **and** a carrier or airbase (e.g. the Mirage F1 mission).
4. Listen for 5–10 minutes and play normally:
   - **No stock voice** should be audible (the old computer-like voices).
   - Orders (move, attack, altitude, speed, sensors on/off) are acknowledged by the selected unit in its own voice:
     "Viper two-one, copy." / "Sentinel, coming to course two seven zero."
   - Different units sound like different people; nobody repeats the identical line twice in a row.
   - Calls never overlap; an urgent call ("missile launch, defending!") cuts into a routine one.
   - Contacts: "new air contact, bearing …" from the unit that found it; "lost contact" only after it stayed gone
     ~20 s; a flickering contact is called "intermittent" once instead of spamming.
   - A hostile classification comes with its cause ("track 7034 is hostile. Visual ID.").
   - Launch an aircraft from the carrier – it uses its hangar callsign and voice.
5. Expected in the log: `Voice catalog: 164 phrases, 206 stock keys mapped`, `Roster for mission
   …`, `[radio] >> …` lines for every call, `[contact] …` lines (who detected / classified what, via which sensor).

Things worth noting: lines that sound wrong for the situation, missing calls you'd expect, calls that are too
frequent, volume (config `2. Radio → Volume`) and hiss level (`Radio noise`).

Mix hierarchy (2026-10-17): **replies** to your orders (acks, VID, engage, course/speed/altitude, sensors,
refusals) play first, at full level and with less noise; **alerts** (missile inbound, hit, bingo) just as loud and
cut in; **reports** (contacts, launches, status) sit about 6 dB behind them – config `Reports volume` (0–1).

Quick switches (config file, then restart the mission): `Replace stock voices = false` brings the game's voices
back; `Contact reports = false` silences the contact calls.

Side effect to watch: with the preloader in place, your subscribed DLL mods **BetterTacMap** and
**IdentifyExpanded** load too (they target 0.8.4 / 0.8.3). If something breaks, disable them in the Mods menu
first. IdentifyExpanded's own voice lines keep playing in its own voice (the mod only replaces lines it knows).

## Why not – refusals explained, order queue (2026-10-17)

The unit says why an order isn't being carried out, and the same reason appears as a game alert (message log)
and in `LogOutput.log` as `[why-not] …`. Alerts work right away; the new spoken lines (reload time, sonar only,
queue position) need the voice render that is running – until it is deployed those stay text-only.

1. **Sonar-only target:** order an anti-ship / radar-guided missile at a contact you only hold on sonar →
   "Negative, that's a sonar contact only, no targeting track."
2. **Reloading:** order a torpedo shot while the tubes reload → "Stand by, reloading, ready in four minutes."
   (exact minutes from the game).
3. **Stacked orders:** give a ship or sub several attack orders → from the second one on it says its place in
   the queue ("copy, queued, number three in line").
4. **Order queue window:** select a unit with stacked attack orders → window top right lists them with status
   (engage state / reload time / waiting) and a ✕ per order. Delete 3 and 4, keep 1, 2 and 5. F8 hides it.
   Check that clicking ✕ does not also click through into the map.
   *Fixed 2026-10-18:* ✕ did nothing – the game's UI (NoesisView.OnGUI) used up the click before the window saw
   it. The window now gets clicks first (GUI.depth) and deletes on mouse-down; `LogOutput.log` shows `[queue] … deleted order`.
   *Still nothing 2026-10-19* (no `[queue]` line at all – IMGUI mouse events never reach the window). Now the ✕ buttons
   are hit-tested against the mouse read from the Input System in Update; IMGUI stays as a second path.
5. **Mirage case (T1):** R550 at a head-on target → "Unable, target not in rear aspect"; Super 530F without a
   radar track → "Negative, no lock" / "radar can't hold the target".
6. **Wingman VID (T2):** order a wingman in a formation to VID → "Unable, holding formation on lead". With config
   `4. Why not → Detach wingman for VID = true` the wingman leaves the formation and does the VID instead.
7. **Dropped orders:** when the game silently drops an order (no sensor can see the target any more), the unit
   says so instead of nothing happening.

Config: `4. Why not → Explain refusals`, `5. Order queue → Show order queue / Toggle key`.

**Messages bottom right (2026-10-18):** the message that belongs to a radio call stays visible until the call has
been spoken (the game keeps only three and fades them after a few seconds), and the one being spoken right now
has a thin amber bar in front of it (was a full amber background until 2026-10-19). Config `2. Radio → Keep messages until spoken`.

**Callsign only on a change of speaker (2026-10-19):** decided when a call goes on the air (queue order can differ
from submit order): the same unit as the call before just goes on without its name; a wingman of the same flight
right after another member only says its position ("two, fox three", clip `cn_<pos>`); anyone else says the full
name. The message bottom right always names the unit. Log: `[radio] >> … (same unit) …` / `(2) …`. Config
`2. Radio → Callsign on every call` (off).

**Contact messages with track links (2026-10-18):** the mod writes its own contact messages instead of the game's
(new / classified / lost / intermittent / regained): `<unit>: New air contact - Track 23, bearing 045`. Both the
reporting unit **and the track number are links** – clicking the track jumps to the contact. Up to four contacts
get a message each, more are summed up in one line. Every classification gets a message (hostile, neutral,
friendly, with the class if identified and the sensor), only hostile ones are spoken. The game's own contact
messages are swallowed while `3. Contacts → Contact reports` is on.

**Every mention is a link (2026-10-18):** after the game has built a message, the mod turns every unit and track it
mentions into a link that moves the camera there – own units, identified contacts (by name) and track numbers
("Track 23", "Tracks 12, 13 and 14"), not only the one unit and one track the game links itself. Applies to the
game's messages and the mod's own (why-not messages now link the unit and the target track too). Config
`2. Radio → Clickable units and tracks`.

**Fuel calls (2026-10-18, fixed 2026-10-23):** against the nearest place the aircraft can land (home base or any
friendly deck/field that takes it – the game's own rebase check). Fuel model as in the game (`RangeInKm` = fuel in
km at best efficiency, a km costs the consumption coefficient of speed and altitude):
- *Joker* – fuel < distance × cruise burn (speed "Cruise" at the current altitude) × 1.1 + 10 km. Not called while
  already RTB.
- *Bingo* – fuel < distance × cheapest burn × 1.05 + 5 km. The cheapest burn is what the game reckons the way home
  with (distance × load factor), so this comes just before the game's own bingo. If the game still gets there first
  (BingoFuel state: a deck nearer than home, or high time compression), the mod calls bingo then, against home.
- *Fuel emergency* – fuel < distance × cheapest burn: it can't make any deck.
- *Nowhere to land* – the game only keeps a mission's `HomeBase=` if that field's air group carries the type
  (SceneCreator.RebaseAircraft → CanAcceptAircraft). Task-force mission 14: the F/A-18As and the P-3C have
  `HomeBase=` Broome Field, whose air group is SH-2F, A-4G, A-7E, EA-6B and Mirage III. So they have **no home base**:
  the game never sends them home, the blue range circle is just their range left, and they fall in the water when
  the tank runs dry. The mod measures them against the nearest friendly airfield (fixed-wing) or deck
  (helicopters) all the same and says it can't land there. With no field at all (A-4Ks in mission 12): joker /
  bingo / emergency at 25 / 15 / 5 % of a full tank.
Each once per sortie; again after air-to-air refuelling. Message with the deck (link), distance and range left.
Helicopters: flat out is the cheapest per km (hover burn is per hour), cruise = 70 %. Every airborne player
aircraft is watched, every 0.5 s real time.

What went wrong before the 2026-10-23 fix: when the game turned an aircraft home on bingo before the mod's own
threshold, the mod took that as "already called" and stayed silent – the game itself only says "returning to base".
Aircraft with nowhere to land got no calls at all – that was the reported case (mission 14, range circle short of
Broome Field, nothing called, nothing sent home). Scan errors only showed with `Verbose log` on.

Log lines (`LogOutput.log`):
- `[fuel] watching Raven 1: 1480 km of fuel, Broome Field 12 km away - joker below 36 km, bingo below 21 km (moves
  with the distance)` – once per sortie, as soon as the aircraft is airborne. **An airborne aircraft without this
  line is not being watched – that is a bug.** The thresholds grow as it flies away.
- `[fuel] watching Sabre 1-1: 2100 km of fuel, Broome Field 330 km away (can't land there - nothing in the mission
  takes it, the game won't send it home) - joker below 690 km, bingo below 455 km (moves with the distance)`
- `[fuel] watching Spear 1-1: 900 km of fuel (60 %), nothing in the mission can take it - joker / bingo / emergency
  at 25 % / 15 % / 5 % of a full tank` – no friendly field at all.
- `[fuel] Raven 1: Joker fuel - Broome Field is 113 nm, range at cruise 140 nm`, then `… BINGO fuel - …` and
  `… FUEL EMERGENCY - …` – the calls, also as a message bottom right; the spoken call follows as
  `[radio] queued …: Raven 1, FuelJoker` / `FuelBingo` / `FuelEmergency`.
- For an aircraft with no home base, once per mission, why each friendly field won't take it (fixed-wing:
  helicopter-only decks left out). `game:` is the game's own verdict; the rest is every check, not only the first
  that fails – if only `type: NOT in its aircraft list` fails, adding the type to that field would be enough:
  ```
  [fuel] Sabre 1-1 (usn_fa-18a) has no home base, the mission names Broome Field. Why each field won't take it, 5 helicopter-only decks left out:
  [fuel]   Broome Field (the mission's HomeBase): game: refuses | type: NOT in its aircraft list (40 types; aboard: usn_sh-2f, ran_a-4g, …) | runway: fits (1 of 1 landing spots) | hangar: fits | room: 13 of 20 taken
  ```
  Other answers: `runway: too small for a large aircraft` (P-3C on a small field), `hangar: too small …`,
  `room: … - FULL`, `deck: OUT OF ACTION`, `sea state: too high` (helicopters).
- `[fuel] <unit>: no fuel calls, scan failed: …` – a bug, keep the log.
- With `1. General → Verbose log = true`, every 30 s per aircraft:
  `[dbg] [fuel] Raven 1: 610 km fuel, Broome Field 140 km, level None, said None, state Default`.

Test recipe – provoke the calls quickly:
1. **Joker → bingo → emergency against a deck (a few minutes at time compression):** a mission with a carrier or
   airfield and a short-legged jet (A-4, A-7, Mirage III – Broome Field in the Pacific Strike task-force missions
   has them). Launch one and note its `[fuel] watching` line. Send it straight away from its base at **max speed /
   afterburner and 200 ft** – that burns three to five times the cruise rate while the way home grows – and run
   time compression. Expected in order: Joker, BINGO (both before the game shows "Bingo fuel" as its status), and
   if you keep it going, FUEL EMERGENCY. Turn it home after joker and refuel from a tanker → calls can come again.
2. **The game goes bingo first:** a helicopter from one ship while another ship nearby takes the same type (the
   alternate is nearer, so the mod measures against it). Hover it far out until the game sends it home on its own
   (status "Bingo fuel", "returning to base"): `[fuel] … BINGO fuel - <home ship> is … nm` at that moment.
3. **Nowhere to land (the reported case):** task-force mission 14 – the F/A-18As (Sabre) start airborne with no
   home base. Their `watching` line names Broome Field with "can't land there". Fight on afterburner and run time
   compression: Joker, then BINGO ("… the game won't bring it home") once fuel only just covers Broome Field at
   economy – well before the blue circle shrinks to nothing. Mission 12 (Spear A-4Ks, no friendly field at all):
   joker at 25 %, bingo at 15 %, emergency at 5 % of a full tank.
4. **Instant (aircraft only):** save the mission while the aircraft is airborne, open the `.sav` in
   `%USERPROFILE%\AppData\LocalLow\Triassic Games\Sea Power\saves\missions\` (text INI), set `RangeInKm=` in the
   aircraft's section just below the joker or bingo threshold from its `watching` line, load the save.

**RTB highlight (2026-10-18):** aircraft in the ReturnToBase state (incl. bingo and winchester) are yellow on the
map; in the formation manager the rows of returning groups/units are yellow and the groups move to the bottom
(within 0.5 s). Config `6. Air ops → Fuel calls / Highlight RTB`.

**Search and rescue (2026-10-18):** the game's rescue model: life rafts (role LifeRaft) appear when a ship sinks or
aircrew goes down; helicopters and ships within reach pick them up (ObjectBase.CheckForRaftPickUp – helicopters
count against `_transportCapacity`); SAR zones send units to rafts and send a full helicopter home. Calls:
- *Survivors in the water* – own rafts appear (batched over 5 s, not in the first 10 s of a mission): the closest own
  unit, with bearing, distance and raft track (link).
- *Proceeding to the survivors* – a SAR zone sends a unit to a raft (once a minute per unit).
- *Pickup complete, N souls aboard* (2-9, else "one survivor aboard") + *cabin full* when at capacity. Replaces the
  game's "Picked up survivors." with `Picked up 2 survivors - 4/6 aboard. Cabin full`.
- *Survivors delivered* – an aircraft with survivors aboard lands (end of the landing circuit).
`[sar]` in `LogOutput.log`. Config `6. Air ops → Search and rescue calls`.
When the game picks up at all (why the stock message is rarely seen): unit option *Pick up survivors* on (default),
not sinking, ≤ 13 kn, within ~280 m of the raft (ships ~550 m); helicopters also ≤ ~17 m above the water and with a
free seat. Only helicopters with `TransportCapacity` in their ini have seats – Alouette II/MD 500 2, AS 350 6,
SH-2F 8, Ka-25/Ka-27/Mi-14 10, Ka-29 16, CH-46 22, SH-3/HSS-2/HH-3 26, SA 321/Z-8 27; all others (e.g. SH-60,
Lynx) have 0 and never pick anyone up. A SAR zone flies the approach for you.

## Formation manager – unit roles (2026-10-23)

Every row in the formation manager gets a third line under name and class, a bit smaller and dimmer, saying what
the unit is. Config `7. Formation manager → Show unit roles` (takes effect the next time the window opens). On the
first formation manager of a session `LogOutput.log` says
`[roles] formation manager: role line added to group, unit and member rows`; a `Warn` naming row kinds instead means
a game update changed the window and those rows stay as they were.

Test in a mission with ships, a submarine, a carrier or airbase and a helicopter deck:
1. Open the formation manager, tab *All*. Ships and submarines show their type from the game's names file:
   *Destroyer*, *Frigate*, *Missile Cruiser*, *Aircraft Carrier*, *Attack Submarine*, *Patrol Submarine*...
2. Launch aircraft with different loadouts (flight deck window). The new rows show the mission of the loadout:
   Air to Air → *CAP*, Air To Air Intercept → *Intercept*, Strike / CAS / Land Attack variants → *Strike*,
   Anti Ship variants → *Anti Ship*, SEAD variants → *SEAD*, ASW variants → *ASW*, AEW → *AEW*, Recon, EW,
   Tanker, Transport. Default / Ferry / Empty loadouts show the airframe type instead (*Fighter*, *Attack*,
   *MPA*, *AEW&C*).
3. Launch a helicopter: its type, e.g. *ASW Helicopter*, *Utility Helicopter*, *MPA Helicopter*.
4. A flight or surface group whose members all share a role shows it under the group summary (a CAP pair:
   *CAP*); a mixed group shows no role line. Expand the group: every member row has its own line.
5. Look: same font as the class line, smaller and dimmer; rows only a small line taller; long roles end in "…"
   instead of pushing the reorder handle away. Right-click and double-click on the role line work like on the rest
   of the row. RTB rows: the role line turns yellow with the rest (`6. Air ops → Highlight RTB`).
6. Turn the switch off, close and reopen the formation manager: no role lines, rows as tall as before.
7. Game language other than English: the roles come in that language (ship and aircraft types from the names
   files, CAP / AEW / Intercept from the formation station roles, the rest from the loadout names).

## Sound samples

`tools/voicegen/reel.py` stitches clips into transmissions like the plugin does → `tools/voicegen/samples/` (the shipped voices: Chatterbox + VCTK).
`--as-cut --suffix _as_cut` renders the same scenario with the joins as cut (before the pauses were closed up).

## Pauses at numbers (2026-10-25)

Feedback on 1.0.0: long pauses between a word and its number. Contact reports and course replies, any mission:

1. "New air contact, bearing two seven three", "coming to course zero niner zero": no audible gap between
   "bearing" / "course" and the number, the digits run together.
2. Track numbers ("engaging track seven zero three four"): a short break between "seven zero" and "three four",
   none before "seven".
3. Pauses the sentence has stay: after the callsign ("Viper two-one, …"), "track seven zero three five, faded",
   "climbing, angels twenty".
4. No clipped digit starts: "seven" is not "even", "three" is not "ree", "four"/"five" keep the "f".


## 1.0.0 – release check (2026-10-24)

The build that goes to the Workshop, installed with `tools/package.ps1 -Install` (game closed).

1. Log: `Sea Power Radio 1.0.0 loaded. Features: …` – no feature marked `(OFF)`; no `[Error  :SeaPowerRadio]`
   lines during the run.
2. Start a mission with aircraft in the air and enemy contacts (e.g. *Dangerous Straits 1985*). Log: `Roster for
   mission …` with every unit, its callsign and voice.
3. Run sections 1 and 2 above (voices and contact reports, why-not, order queue F8) and the formation manager check.
4. `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerRadio\` contains `SeaPowerRadio.dll`, `_info.ini`,
   `LICENSE.txt`, `CREDITS.txt`, `preview.jpg` and `voices\` (43 voice folders and the four .txt files) – and no
   `.pdb`.
