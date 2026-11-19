# R.A.T.S. Radio: feature detail

Everything the mod does, in full. The short list is in the [README](../README.md).

## Voices

- 43 voices: American, Canadian, English, Scottish, Irish, Northern Irish, Australian and New Zealand accents, men and
  women. Every player unit, and every aircraft a carrier or airbase can launch, gets its own voice the moment it exists.
- Ships and shore units say their in-game short name ("Iowa", "Pargo"), aircraft their squadron callsign from the game
  ("Lancer 3-2"), everyone else a generated NATO callsign ("Viper two-one", "Sentinel").
- Every clip is cut from a whole spoken sentence, so callsign, phrase, bearing, track number, altitude and count join
  like one person talking: no pause before a number ("bearing two seven three") unless the sentence has one
  ("track 7034, faded").
- The game's voice lines are muted and spoken again by the unit that said them. Voice lines of other mods play as before.
  - Speed and altitude orders say which way they go and where to: "coming up to two thirds", "descending, angels
    five". A speed set in knots too.
  - Launch calls name the weapon that is leaving: "harpoon away", "anti-ship away", "missile away", "torpedo away".
- Accents follow the nation (1.4.0): the US and Canada speak with American and Canadian voices, the UK with English,
  Scottish and Northern Irish ones, Australia with Australian, New Zealand with New Zealand then Australian.
  - Units get unused voices of their nation's accents first and share inside them when those run out.
  - A ship's name is rendered in voices of its own nation's accents. Until those clips are rendered, the ship says its
    generated callsign.
  - The list is data in the pack (`accents.txt`). Countries not listed use the whole pack. Pins in `aliases.txt` and
    remembered campaign voices win.
- Random voices each mission (on by default since 1.4.0): a fresh start of a mission shuffles the voices. Callsigns stay.
  - Loading a save keeps the voices of that mission's latest start (`BepInEx/config/SeaPowerRadio/voice-salts.txt`).
  - Linear campaigns keep their remembered voices; dynamic campaigns are not shuffled.
  - Off: a mission always has the same voices.
- Female voices are off by default since 1.4.0. Switched on, units use them too. Off, a unit whose name was rendered
  only in a female voice keeps it.
- Voice gender per squadron, in a text file (no menu entry): `BepInEx/config/SeaPowerRadio/squadrons.txt`, lines like
  `Bear Aces = female` or `Viper = male` (squadron name or callsign). It wins over the Female voices setting.
  Details: [docs/squadron-voices.md](docs/squadron-voices.md).
- Moods (1.5.0): every voice also has stressed, happy and sad takes.
  - Stressed: warnings and weapons calls, and the unit for a minute after its own warning (missile inbound, we're hit,
    flooding).
  - Pleased: a kill, a hit, a rescue, mission success, and the unit's next acknowledgements.
  - Low: a failure, survivors in the water, Winchester, a lost contact.
  - A line the mood has no take for is spoken calmly. The takes live in the voice pack
    (`<persona>/<mood>/<key>.mp3`): [docs/moods.md](docs/moods.md).

## The net

- A unit says its name only when it takes over the net from someone else. The same unit again just goes on, and a
  wingman of the same flight gives its number only ("two, fox three"). The setting Callsign on every call (config file
  only, off by default) makes every call start with the name.
- Urgent calls (missile inbound, we're hit, bingo) cut in.
- Replies to your orders go before routine reports and are mixed louder and cleaner. Routine traffic plays at half
  level by default (Reports volume in the config file).
- Stale calls are dropped: a late "copy" is worse than none.
- The same phrase from a whole flight is said at most twice.
- Radio sound per transmission: carrier hiss, mic click, crackle, squelch tail. It plays through the game's voice
  channel, so the game's voice volume slider applies.
- Overlapping voices (on by default): a new call starts 0.6 to 1.4 seconds after the last one began, while that one is
  still playing. Up to 3 at once (Voices at once in the config file, 2 to 4), panned a little left and right, never the
  same unit twice. Off: one voice at a time.
- The message of a call stays bottom right until it has been spoken, with a thin marker on the one being spoken.

## Reports

- Why-not: the reasons the game knows but never says, spoken by the unit, as a message and in the log.
  - Not in rear aspect, outside the launch envelope, can't reach, inside minimum range, no radar lock, no sensor track,
    sonar contact only, below the radar horizon, no line of sight, too low or too high, not in the firing arc, seeker
    can't see the target, wrong speed, not cleared, reloading (with the time left), nothing left to fire, defending
    against a missile, order dropped by the game.
  - A wingman holding formation instead of flying a VID (optionally released from formation, so the VID happens).
  - Who identified a contact and with what sensor.
  - A reason about a target is said once, not again while the ship closes the range, and once for a whole formation.
- Contacts without spam: faded and regained contacts with hysteresis, cooldowns and flicker detection. A raid found
  aircraft by aircraft is called once, and once more as "multiple contacts".
- Fuel: joker, bingo and fuel emergency against the nearest deck or airfield that can take the aircraft.
  - Joker: only just enough at cruise speed. Bingo: only just enough at the most economical speed. Emergency: not
    enough at all.
  - Bingo comes no later than the game turning the aircraft home on its own.
  - Aircraft the game gives no home base (no field in the mission carries the type) call against the nearest friendly
    field, or at 25 / 15 / 5 % of a full tank without one.
- Landing check: when an aircraft has no home base, the log says for each friendly deck or field why it cannot take it
  (aircraft list, deck state, sea state, room, landing spot, hangar elevator).
- Search and rescue: survivors in the water (bearing, once per group of rafts), rescuer proceeding, pickup complete
  with souls aboard, cabin full, survivors delivered on deck.

## On the map and in the windows

- Every unit and track a message mentions is a link that moves the camera there: own units, contacts, track numbers.
- Aircraft on their way home are yellow on the map and in the formation manager; returning groups sink to the bottom
  of the formation manager.
- A small line under every unit in the formation manager says what it is, in the game's own words and language: ship
  type (Destroyer, Frigate, Attack Submarine), the mission an aircraft is loaded for (CAP, Strike, SEAD, Anti Ship,
  ASW, AEW), helicopter type. Groups show the role their members share.
- Order queue window (F8): the selected unit's stacked attack orders, each with a delete button (the game only has
  "cease fire", which drops all of them) and what it is waiting for: the weapon's state or the reload time left.

## Settings and voice packs

- Options > Audio has a group **Rats Radio**: female voices, random voices, moods, overlapping voices, radio volume,
  radio static (0 to 150 %, 100 % = the default amount) and a volume for each kind of call (contact and track
  calls, combat calls, warning calls, replies to orders, other calls). Changes apply at once and are saved.
- Every feature has its own entry in the config file (see Settings).
- The 43 voices are the **NATO (English)** pack. Other languages come later as separate Workshop items (no DLL needed).
- Options > Audio > Rats Radio > **Voice packs** lists every country (NATO, Warsaw Pact, Other) with a drop-down:
  *Auto* (the installed pack for that country, else the game's voice), *Game voice* or a pack.
  - A country without a pack keeps the game's own voice: the Soviets speak the game's Russian. Choose *NATO (English)*
    for that country to get the English radio net.
  - A line a pack lacks is also left to the game.
  - Applies from the next mission. Pack format: [docs/voice-packs.md](docs/voice-packs.md).


Voices are pre-rendered: 43 distinct voices (30 male, 13 female; based on speakers of the VCTK corpus, rendered with
Chatterbox) put through the radio chain from [Ratten Chatter](https://github.com/struckl/RattenChatter). Nothing is
rendered at runtime; everything works offline.

## Config file and text files

  Why not, Order queue, Air ops, Formation manager, Call volumes, Diagnostics, Voice packs.
  - Settings with no menu entry: Callsign on every call, Reports volume, Voices at once, the contact timings (Lost
    after, Per-contact cooldown, Flicker drops per minute), Announce neutral classification, Detach wingman for VID,
    the order queue key (F8), the log switches.
- Text files in `<Sea Power>\BepInEx\config\SeaPowerRadio\`: `aliases.txt` (ship names, below), `squadrons.txt`
  (voice gender per squadron), `voice-salts.txt` (kept by the mod, do not edit).


## Mod ships and their names

A ship says its name only if that name was rendered as a clip (every ship and shore unit
of the base game, `voices/names.txt`); any other name gets a generated callsign. Hull prefixes such as USS, HMS, HMCS or
FGS are stripped automatically, so a modded "USS Pargo" says "Pargo". For everything else there is
`<Sea Power>\BepInEx\config\SeaPowerRadio\aliases.txt` (created at the first mission, Workshop updates never touch it):

```
USS Pargo = Pargo                  # say the name of a base-game ship
HMS Bristol = Bristol | gb_m_06    # and pin it to one voice
HMS Bristol = | gb_m_06            # only pin the voice
CVN-68* = Nimitz                   # '*': every name that starts with it
Akira O-ren = Victory              # or keep a callsign word for good
*prefix HNoMS, HSwMS               # more prefixes to strip
```

It can only point to names that have clips: a base-game ship name, or one of the ship, submarine and shore callsign
words (Bulwark, Sentinel, Victory, Warden, Guardian, Valiant, Resolute, Steadfast, Vigilant, Defiant, Intrepid, Courage,
Shadow, Silent, Deepwater, Barracuda, Castle, Bastion, Harbor, Lighthouse). A word given this way is that ship's alone. A
truly new name needs new clips. Edits apply from the next mission, and the
log says when a line names a ship or voice that does not exist. A ship mod can ship its own `rats-radio-aliases.txt` in
its folder. Details: [docs/name-aliases.md](docs/name-aliases.md).

Interim fix for "contact faded" spam without any mod: Options > Audio > **Report Faded Contacts** > off.
