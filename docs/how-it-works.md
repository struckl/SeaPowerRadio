# How it works – the game systems Sea Power Radio hooks into

Research notes from building the mod, against **Sea Power 0.8.6** (Unity 6000.0.84f1, Mono). They describe what the
game does; where the plugin uses it, the file is named. Class and method names
come from the game's `Sea Power_Data/Managed/Seapower-Scripts.dll`; `File.cs:NNN` references point into a local
decompile of it (see [Decompiling](#decompiling)) and drift with every game update. The patch targets the plugin
really uses are listed in `src/SeaPowerRadio/PatchRegistry.cs`; a target a game update removes switches off only its
feature.

## Mod loading

- **Anchor Chain** (Workshop 3380210757, https://github.com/SeaPower-Modders/AnchorChain) is the community
  chainloader. Its preloader (`ACPreloader.zip` = BepInEx 5.4 + Doorstop) goes into the game folder once; the
  Workshop plugin then scans every mod directory (`FileManager.Directories`: Workshop items and
  `StreamingAssets/<folder>`) for DLLs and calls `IAnchorChainMod.TriggerEntryPoint()` on classes tagged
  `[ACPlugin(guid, name, version)]`. Harmony comes from BepInEx's HarmonyX.
- The game knows about it (`ModMenuViewModel` checks for loaded `AnchorChain` / `BepInEx` assemblies); Workshop
  items that depend on Anchor Chain are flagged as DLL mods.
- **voice.ini is a merged virtual file** (`Utils.openVirtualIniFile("audio/voiceover", "voice", "voice")`): `user/`
  and mod folders add or override keys. Identify Expanded, for one, ships its own keys and plays them through
  `VoiceMessage.PlayVoiceMessage(key, nation)` – so unknown keys must be passed through or mapped, never dropped.

## The radio sound

The voices are rendered offline (`tools/voicegen`), but a radio chain baked into every clip would put a mic click in
the middle of a stitched sentence (callsign + phrase + number). So the chain is split:

- **Voice half, baked into each clip:** band-limit (~300–3400 Hz), presence boost around 2 kHz, hard compression,
  soft clipping, band-limit again – the "through a handset" colour.
- **Transmission half, once per transmission at runtime** (`Radio/RadioFx.cs`, procedural, cheap): open-carrier
  hiss that starts a beat before the mic click, sparse velvet-noise crackle (denser on a weaker signal), the click,
  the voice, a short hang and the squelch tail – built into one clip per transmission, so the stitched pieces play
  without gaps.

The technique comes from Ratten Chatter (https://github.com/struckl/RattenChatter), a radio chatter mod for Nuclear
Option.

### Joining the pieces

Every clip is cut out of a whole spoken sentence, and the takes speak a pause at each cut so it falls into silence
(`tools/voicegen/phrases.py`). Each clip keeps its half of that pause – which also puts a pause where nobody makes
one: "bearing, | two seven three" added up to 0.15–0.55 s of silence before the number (player feedback after 1.0.0:
"long pauses between words and numbers"). So `RadioFx.Build` cuts the silence at every join back to what
`TransmissionBuilder.MaxPause` allows, from the middle of the pause:

| Join | Most silence left |
| --- | --- |
| word ↔ number, no pause in the text ("bearing 273", "make my depth 150 feet") | 0.05 s |
| between digit groups ("seven zero · three four") | 0.08 s |
| a pause the speaker makes anyway: after the callsign, at a comma of the text ("track 7034, faded", "climbing, angels twenty" – `catalog.txt` columns 8 and 9) | 0.25 s |

Silence at the end of a clip: 10 ms frames below -40 dBFS (breath and room tone sit there, under the hiss bed). At
the start of a clip only what lies before the onset of the first word: the run of frames above -55 dBFS that leads
into speech is kept, because after the band-limit the "s" of "seven", the "th" of "three" and the "f" of "four"
are that quiet. Cutting at -40 dBFS on both sides closed the pauses further but cost 10 % more word errors in a
speech recognizer over all 43 voices ("seven" → "even"); the asymmetric cut leaves the word errors where they were
and halves the silence at number joins (median 0.17 → 0.09 s, 90th percentile 0.31 → 0.16 s). `reel.py` joins the
same way (`--as-cut` for the old joins).

## Stock voice system (what the mod replaces)

Class `SeaPower.VoiceMessage : Singleton<VoiceMessage>` (`Seapower/VoiceMessage.cs`).

- **Single funnel.** Every stock line goes through public methods that all end in two private ones:
  `PlayVoiceMessage(clip, speaker|nation)`, `PlayVoiceSequence(clip, speaker|nation, number, format)`,
  `PlayAircraftAltitude(…)`, `AddVoiceToSequence(…)` → private
  `SubmitMessage(string clip, ObjectBase speaker, string nation, int number, string format, bool sequence, bool altitude)`
  and `AppendMessage(string clip, ObjectBase speaker, string nation, int number, string format)`.
- ⇒ The stock voice router is a **Harmony prefix on `SubmitMessage` and `AppendMessage`** that returns `false` and
  hands `(key, speaker, nation, number, format)` on. The speaker is the actual `ObjectBase` for unit lines and `null`
  for task force reports (contacts) – then the mod picks the reporting unit itself. Every stock event arrives with its
  semantic key; nothing has to be rediscovered.
- Playback: two `AudioSource`s on the mixer group `audio/Master → Voice` (2D, `ignoreListenerPause`). The mod plays on
  the same group, so the game's voice volume slider still applies.
- The game's queue logic: priority ≤ 2 is urgent (pre-empts); `command:*` 0.3 s debounce and same-owner supersede;
  `contact:new|classified|faded` with a 2 s gap between contact reports, stale after 8 s; routine lines stale after
  20 s; at most 64 pending; the second channel ducked to 0.45 (option `SimultaneousVoiceovers`).
- Voice profiles: `[VoiceProfiles]` in voice.ini maps a nation to profiles (`US=us_male_1|us_male_2`,
  `UK=uk_male_1`, `Soviet=wp_male_1`, …). `ObjectBase.setNation()` gives each voiced unit a slot (spawn index within
  its nation, never reset between missions) → `ResolveVoiceProfile(nation, slot)`: two US voices for a whole fleet.
  Task force reports use the nation overloads – no per-unit voice at all.
- Numbers: `NumberToVoiceSequence` (`DIGITS`, `DIGITS_AIRCRAFT`, `DIGITS_EXCITED`, `FULL`), an altitude builder with
  thousands logic, chained ending clips (`TurnToCourse` → `HelmAye`). Clips are WAVs loaded on demand, 64 MB LRU.

Full inventory of the stock lines: [stock-voice-events.md](stock-voice-events.md) – every phrase key with priority,
number type, variants and call sites.

### "Contact faded" spam

Key `ContactFaded`, one call site: `Taskforce.CheckContactFadedMessage()`. Every 3 s of mission time it flushes the
contacts that faded; one → `PlayVoiceSequence("ContactFaded", nation, trackId)`, several → only a text alert. **No
hysteresis, no per-contact cooldown**: a flickering contact is re-announced every few seconds. (A stored "report
after +5 s" timestamp is never read.) Without any mod: Options → Audio → **Report Faded Contacts** off. A silent-WAV
override in voice.ini doesn't work – the track number digits still play. The mod mutes the game's three contact
callouts (`Taskforce.CheckNewContactMessage`, `CheckContactClassifiedMessage`, `CheckContactFadedMessage`) and keeps
its own book per contact (`Contacts/ContactReporter.cs`): hysteresis, cooldowns, flicker detection.

### Keys that don't say what the mod's lines say

The mod's phrases are worded differently from the stock clips, so a few keys need the situation, not just the key
(`Radio/StockVoiceRouter.cs`, `Refine`):

- **Speed telegraph** – `SetSpeed_Ahead_1_3 / _2_3 / _Standard / _Full / _Flank` name the new setting ("ahead two
  thirds"); the mod's lines say the direction ("increasing speed", "slowing down"), which comes from the setting
  before. The context menu (`ObjectSpeed`) asks for the line *before* `setTelegraph`, so `getTelegraph()` is still
  the old one; the keyboard (`Vessel.increase/decreaseTelegraph`) asks after it and leaves the old one in
  `_previousTelegraphSetting`. (On a submarine the keyboard path says nothing at all: `Submarine.setTelegraph`
  overwrites `_previousTelegraphSetting` before `ReportTelegraph` compares it.)
- **Altitude** – `SetAltitude` (number = feet, rounded to thousands above 999) comes after `DesiredAltitude` is set
  (`ObjectAltitude`, the altitude slider, `Helicopter` preset keys); the direction is `transform.position.y` against
  it. The mod's variants are "climbing, {alt}" and "leveling off, {alt}" – no "descending" line is rendered yet, so
  a descent gets "leveling off". `MakeMyDepth` needs nothing: "make my depth, {num} feet" has no direction word.
- **Launch lines** – `WeaponBase.LaunchVoiceover` picks a ship's or submarine's key from the target only: missile at
  an air target `SAMFired`, at a `LandUnit` `ASUMFired` (so a Tomahawk land attack, not an anti-submarine missile),
  at anything else or a position `ASHMFired`; every other weapon (torpedoes, `Type=ASROC`) `WeaponAway`. A prefix on
  `LaunchVoiceover` (feature `LaunchLine`) notes the weapon, and the line follows its ammunition: torpedo or torpedo
  payload → "torpedo away", `*gm-84*` → "harpoon away", other missile at a ship → "anti-ship away", the rest →
  "missile away". Aircraft keep the game's choice (fox one/two/three, magnum, bombs away…).

## Why not – the game has the reasons, it just doesn't say them

### Order-time refusals

Every refusal of a player order follows one pattern: the reason text comes from
`LanguageResourceHandler.getText("OrderLog", <ReasonKey>)`, goes to `MainGameViewModel.RaiseAlert(unit, …)`, and the
voice plays a generic `UnableToComply`, `WrongTarget`, `TargetRequired` or `Negative` – so the reason is on screen,
but the voice only says no.

Reason keys (`language_en/ui.ini`, `[OrderLog]`): `NoSuitableAmmo`, `WrongTarget`, `SeekerCantSeeTarget` (TV seeker at
night), `TargetRequired`, `TargetRequiredForJam/Laser`, `CannotJam`, `CannotLaser`, `VisualIdentifyAbortFuel/Speed`,
`CanNotRebase` + `RebaseUnsupported/NoSpace/TooFar/SeaState`, towed-array and mast keys, `UnableToUseSnorkelAtThisSpeed`,
`PlayerLaunchMalfunction`, `PlayerSeekerMalfunction`, flight deck `UnableToLaunchNoAmmo/SeaState`, …

These cover order-time checks only. Two common cases stay silent in the game: an attack order that is accepted but
never fires, and a VID order a wingman acknowledges but never flies.

### Accepted, but the weapon never fires

Pipeline: `AttackingState.MouseAction` (click-time checks – the only ones with feedback) → `AddAttackTask` →
`AttackTask.OnExecute` (voice `EngageTrack`) → `ObjectBase.InsertEngageTask` → every frame
`ObjectBase.HandleEngageTasks()` (assign to a weapon system, align) → `WeaponSystemHardpoint.OnUpdate()`
(`CheckMaxRange` → `UpdateEngageInterruptors` → `CheckRange` → `CheckFiringConstraints` → `ExecuteLaunch`) →
`WeaponBase.CommonLaunchSettings` → `RecordWeaponLaunch` + `LaunchVoiceover`.

**`WeaponSystem.EngageState`** (`_engageState`, per weapon system, re-evaluated every frame while engaging) already
holds the reason – `WhyNot/WhyNotMonitor.cs` reads it and speaks it once it has blocked an order for a moment: `TargetTooClose`, `TargetTooFar`, `MissileCanNotReachTarget`, `NoLOSToTarget`,
`TargetIsNotDetectableByFCR`, `TargetIsBeyondFCRHorizon`, **`NotInRearAspect`**, `LauncherTooHigh/Low`,
`TargetIsNotInFiringArcs`, `PrelaunchSensorAlignment`, `TVSeekerCantSeeTarget`, `SpeedTooLow/High`,
`EngagementNotAllowed`, `FirerateDelay`, … – localized (`ui.ini` `Status/Weapon_<state>`) but shown only for the
selected unit while engaging. Caveats: hardpoints don't reset it when a task ends; `CheckingAbort` on a guided weapon
means no free radar channel or radar off. More detail: `ObjectBase.EngagementIssue` (ObscuredByAltitude, InLookdown,
NotEmitting, Jammed, …) and `SensorSystem.SensorIssue`.

Silent drops in `HandleEngageTasks`: the task is deleted when the air target can't be seen (`RequireVisionToEngage`),
when no usable weapon system is left, or after `TacticalDoctrine.RemoveEngageTaskAfter`; it is kept but skipped while
beyond shot range, not yet processed by the AI, or without a free mount (busy, reloading, no free guiding sensor for
SARH). An aircraft being shot at keeps evading (evasion states outrank aligning) and never gets its ±10° firing arc.

Typical example: a Mirage with R550 Magic (IR, rear-aspect only, `TargetRearCone=230`) against MiG-23s coming
head-on → `NotInRearAspect`; with Super 530F (SARH, needs the Cyrano IV to track; one target channel) →
`TargetIsNotDetectableByFCR`, `PrelaunchSensorAlignment` or `CheckingAbort`. "R" / "E" on a contact are detection
sources of the whole side (radar / ESM), not a lock; ROE "Weapons Free" doesn't matter for manual shots.

### VID acknowledged, wingman stays in formation

`UnitSelectedState.Identify` / `IdentifyState.MouseAction` play `VisualIdentify`, then for aircraft only set
`_ai._objectToIdentify` – no order, no detach from the formation (move orders do call
`UnitFormation.MakeUnitActIndependently`). In the aircraft state machine (`Aircraft.initStates`) `IdentifyAirContact`
is reachable only from `Default`; a wingman sits in `MovingInFormation`, which has no way there, so it keeps formation
until the contact is identified or lost. Helicopters and ships do break formation (VID is an any-state transition for
them). Aborts of `IdentifyAirContact` (out of range, can't intercept, track lost) are never reported for aircraft.

Other silent overrides: `Order.setOrder` ignores units that aren't controllable (after bingo fuel, which also clears
engage tasks every tick); moving an aircraft cancels its attack (`CeaseFire`); `WaypointsCancelWeaponsFree` flips
Free to Tight; leader orders fan out to the wingmen; idle wingmen are pulled back into formation;
`PatrolZone.EnforceCAPPursuitBuffer` cancels attacks and VIDs outside the CAP buffer; the AI drops aircraft targets
when a closer allied shooter holds them, on Winchester, or when the chase is hopeless.

ROE (`ObjectBase._weaponStatus`: Tight, Free, Hold) is not checked on the manual engage path – Hold only stops AI
attacks; identification isn't required for a manual shot.

The F10 dev menu ("General" tab) shows the current order, station role, state machine state, engage tasks and
fuel/RTB; "Object to Identify" is on the Sensors tab.

## Mission lifecycle and roster

- **Mission ready** = postfix on `SeapowerUI.ViewModels.MainGameViewModel.OnStart()` (single caller, one frame after
  `SceneCreator.LoadMission` has loaded task forces, units, hangars, formations and saves).
- **Mission unloaded** = prefix on `SeaPower.GameMain.OnDestroy()` – GameMain lives in the mission scene, so this
  covers exit, restart and the campaign path. Don't patch coroutines (`LoadMission`, `DoUnload`): Harmony fires when
  the enumerator is created. `VoiceMessage.OnStart` runs once at boot, not per mission.
- **Mission seed** (stable voices per mission): the mission file (`SceneCreator.MissionIniBaseFile` for saves, else
  the relative `Globals.currentMissionFilePath`), lower-cased, FNV hash. Instant Action always writes
  `user/missions/_InstantAction.ini`, and random spawns use the unseeded `Globals._rnd`, so such rosters can vary.
- Units: `ObjectBase` → `Vessel`, `Submarine` (not a Vessel), `Aircraft`, `Helicopter`, `LandUnit`. Player units:
  `Globals._playerTaskforce._taskforceUnits` plus `_taskforceDisabledUnits` (enabled later by triggers), without
  life rafts. Identity: `UniqueID` survives save/load; names are rewritten by formations ("Leader-N").
- Formations: `UnitFormation.Stations` (join order), `LeaderStation`, `Station.UnitObject`,
  `Station.UnitActsIndependently`.

### Launchable air wings

A hangar is `unit._obp._flightDeck` (carriers, helicopter ships, airbases): `_vehiclesOnBoard` → `VehicleTypeOnBoard`
(type, roles, squadrons) → `Squadron` (name, numbers, callsigns). Hangar aircraft are anonymous counts – no
`ObjectBase` until launch. **Launch** = postfix on `FlightDeck.launchVehicle(…)` (`__result` is the new aircraft; up
to four launched together form a vic). On recovery the aircraft object is destroyed and the count goes up again, so
the roster pre-creates slots per carrier, type and squadron, and a launch claims the lowest free one. Other spawn
paths: placed aircraft, air support, save loads; every object that joins a task force later passes
`ObjectsManager.AddObjectToItsTaskforce` (the mod uses it to see life rafts appear).

## Sensors, contacts, identification

- A **track** (`TrackComponent`: source unit, sensor, sensor type, epoch, max age) per unit and sensor is fused into
  a **`Vehicle`** per contact list (`Id`, side, `Identified`, `IsDormant` = faded, `IsClassified`, `Class`,
  `Position`, `CurrentTracks()`). Lists are `PlottingTable`s: one per unit, one shared per task force.
- `SensorType`: Visual, TVSeeker, SearchRadar, FCR, ARS, ActiveSonar, PassiveSonar, ActiveIntercept, ESM, MAD, … –
  IR reports as Visual.
- Lifecycle: new = `PlottingTable.NewVehicle` (fires before the data – wait ~1 s); faded = `IsDormant` false → true
  when the fix is older than its max age; regained = true → false (no stock callout); dropped = deleted after 10 s
  (air) / 300 s (sea) and **a new Vehicle with a new Id** if it comes back.
- Fusion and identification run in Burst-compiled ECS jobs – not patchable – and the game never records why or by
  whom. The mod subscribes to each contact's side (`Vehicle.UnitTaskforce`) and, when it becomes known, looks at the
  contact's tracks: their source unit and sensor say who classified it and how (visual, ESM, sonar, radar …).
- Track numbers are plain ints (own units from 1001, contacts from 7001). Bearing/range: from the perceived
  position, not the true one.
- Threats: `ObjectBase.set_ThreateningMissile` (`MissileIncoming*`, 5 s global cooldown), `set_ThreatingTorpedo`,
  `RegisterRadarLockWarningSource` (`TargetedByRadar`, 30 s), `ReportUnderFireFrom` (guns).

## Patch rules

Never patch `DM.I/D/E` (empty logging stubs, inlined), coroutines or ECS job lambdas. Every target is resolved
through `PatchRegistry` before anything is patched; a missing one disables its feature and logs, never crashes.

## Decompiling

```bash
ilspycmd -p -o _decomp/Seapower-Scripts "<GameDir>/Sea Power_Data/Managed/Seapower-Scripts.dll" -r "<GameDir>/Sea Power_Data/Managed"
```

Keep the output out of any repository: it is the game's copyrighted code.
