# R.A.T.S. Radio: feature inventory and split proposal

Analysis of 2026-11-11, from the code on branch `radio-1.5` (`src/SeaPowerRadio/**`, 7,848 lines), `Settings.cs`,
`PatchRegistry.cs`, the README and `docs/`. **Decided and built the same day (branch `radio-2.0`):** option B with Radio requiring the tools (steps 1-3 at once), the
mod named **R.A.T.S. Command Tools** (`struckl/SeaPowerCommandTools`), English as its own item **R.A.T.S. Radio English**
(`struckl/SeaPowerRadioEnglish`), the cross-mod contract and line packs in [rats-bus.md](rats-bus.md), and a one-time 2.0
notice for players who had Radio before. Below is the analysis as written before the decision; where it differs, the
build follows rats-bus.md (no shared-source folder and no copies kept in Radio: Radio 2.0 dropped the features, Command
Tools stands down under a Radio older than 2.0).

The question behind it: a player who wants, say, the order queue window (F8) but not the voices has to take the
whole radio today (the Workshop item is about 400 MB of voices). Which features could stand alone, and is it worth it?

---

## 1. The building blocks every feature sits on

| Block | Files | What it is |
|---|---|---|
| Entry and loader glue | `Plugin.cs` | `[ACPlugin]`/`IAnchorChainMod` entry, globe/flat gate (`SeapowerUI.MapDisplayState`), AppDomain "already loaded" marker, starts everything |
| Patch registry | `PatchRegistry.cs`, `Patches/*.cs` | 11 named Harmony features; a feature whose target is missing is switched off as a whole and logged |
| Mission lifecycle | `Patches/MissionPatches.cs`, `Core/EventBus.cs` | mission ready (`MainGameViewModel.OnStart`), mission end (`GameMain.OnDestroy`), aircraft launched (`FlightDeck.launchVehicle`) |
| Host | `RadioHost.cs` | one DontDestroyOnLoad MonoBehaviour that ticks every monitor and owns the radio player |
| Settings | `Settings.cs` | one BepInEx config file `dev.sewerlabs.seapowerradio.cfg`, numbered sections 1-10 |
| Roster | `Roster/Roster.cs`, `RosterService.cs` | every player unit and hangar slot as a `RosterElement`, **with its voice, callsign, nation and pack**. It is the voice binding and, at the same time, the only list of "my units" the other features use |
| Radio net | `Radio/RadioPlayer.cs`, `Transmission.cs`, `RadioFx.cs`, `ClipLibrary.cs`, `Catalog.cs`, `CallCategory.cs` | queue, priorities, overlap, brevity, clip loading, radio sound |
| Logging | `ModLog.cs` | |

The roster is the main knot: why-not, contacts, fuel, SAR and even the queue window reach for
`RosterService.Current`, although most of them only need "the player's units", not their voices.

---

## 2. Every feature

Legend: **Audio** = needs the voice library to be useful. **Cut** = how cleanly it can leave Radio (A clean, B some
untangling, C deep in the voice core).

### 2a. Voice features (the radio itself)

| # | Feature | What the player gets | Where | Audio | Cut |
|---|---|---|---|---|---|
| 1 | Per-element voices | 43 voices, one per unit and hangar slot, deterministic per mission | `Roster/*`, `Voices/VoiceAssignment.cs`, `VoiceLibrary.cs` | yes | C (core) |
| 2 | Callsigns | in-game short names, squadron callsigns, generated NATO callsigns | `Voices/Callsigns.cs`, `Radio/Names.cs`, `Transmission.CallsignFor` | yes | C |
| 3 | Stock voices replaced | every game voice line re-spoken by the unit that said it; knots, altitude direction, weapon-specific launch calls | `Radio/StockVoiceRouter.cs`; hooks `StockVoice` (`VoiceMessage.SubmitMessage/AppendMessage`), `LaunchLine` (`WeaponBase.LaunchVoiceover`), `SpeedKnots` (`ObjectSpeed.RaiseSpeedAlert`) | yes | C |
| 4 | A real net + brevity | urgent calls cut in, replies before reports, stale calls dropped, callsign only when the speaker changes | `RadioPlayer.cs`, `Transmission.cs` | yes | C |
| 5 | Overlapping voices | up to 3 stations at once, panned | `RadioPlayer.cs`; `2. Radio/Overlapping voices`, `Voices at once` | yes | C |
| 6 | Radio sound | hiss, click, crackle, squelch tail, static amount | `RadioFx.cs`; `Radio noise` | yes | C |
| 7 | Call volumes | master volume plus five per-category sliders | `CallCategory.cs`; `8. Call volumes`; Options page | yes | C |
| 8 | Voice packs | per-country pack choice, game voice fallback, pack discovery | `Voices/VoicePack(s).cs`, `Nations.cs`, `LanguageRules.cs`; `10. Voice packs`; Options page | yes | C |
| 9 | Moods (1.5.0) | stressed / happy / sad takes from the Moods pack | `Radio/Moods.cs`, mood roots in `VoicePack` | yes | C |
| 10 | Female voices | on/off, live revoice when switched off | `Settings`, `RadioHost.OnFemaleVoicesChanged`, `Roster.Revoice` | yes | C |
| 11 | Random voices per mission | salt per mission, kept for saves | `Roster/VoiceSalts.cs` | yes | C |
| 12 | Accents follow the nation | `accents.txt` per pack | `VoicePack.AccentsFor`, `VoiceAssignment` | yes | C |
| 13 | Squadron voice gender | `squadrons.txt` | `Voices/SquadronVoices.cs` | yes | C |
| 14 | Name aliases | `aliases.txt`: mod ships onto rendered names, pinned voices, callsign words | `Radio/NameAliases.cs` | yes | C |
| 15 | Campaign voice memory | same voice and callsign across a linear campaign | `Roster/CampaignMemory.cs` | yes | C |
| 16 | Messages held until spoken | a message bottom right stays until its call was spoken, amber bar on the one speaking | `Radio/AlertLink.cs`; hook `AlertLink` (`MainGameViewModel.set_AlertyMcAlertFace`); `Keep messages until spoken` | only makes sense with voices | B, but no reason to |

All of these are the radio. None is worth a mod of its own; they stay.

### 2b. Features that are mainly text and UI

| # | Feature | What the player gets | Where (files, hooks, settings) | Audio | Depends on | Cut |
|---|---|---|---|---|---|---|
| 17 | **Order queue window** (F8) | the selected unit's stacked attack orders, each with a delete button and what it waits for (weapon state, reload time) | `WhyNot/QueueOverlay.cs` (165 lines, IMGUI + Input System hit test); no Harmony hook; `5. Order queue` (on/off, key) | no | `RosterService.Current` only as "a mission is running"; calls `WhyNotMonitor.MarkCleared` so a deleted order is not reported as "dropped by the game" | **A** |
| 18 | **Clickable mentions** | every unit and track a message mentions becomes a camera link, bottom right and in the log | `Radio/MessageLinker.cs` (184); runs inside the `AlertLink` prefix on `set_AlertyMcAlertFace`; `Clickable units and tracks` | no | none (reads `Globals._playerTaskforce` itself) | **A** (shares one hook with #16, see 4.3) |
| 19 | **Unit roles** in the formation manager | a third line per row: Destroyer, CAP, Strike, ASW Helicopter ... in the game's own language | `Formation/RoleLine.cs` (187), `UnitRole.cs` (103); hook `UnitRoles` (`FormationManagerView..ctor`); `7. Formation manager` | no | `RtbMarker` only to keep the RTB colour on the new line | **A** |
| 20 | **RTB at a glance** | returning aircraft yellow on the map and in the formation manager, returning groups sorted to the bottom | `Air/RtbMarker.cs` (155); hook `RtbHighlight` (`MapContactVisuals.Resolve`, **signature differs** between globe and flat; `FormationManagerView..ctor`); `Highlight RTB` | no | none | **A** (needs two builds) |
| 21 | **Why-not diagnostics** | why an ordered attack is not happening (~20 reasons), as a message, a spoken reply and a log line; "order dropped by the game"; formation-wide de-duplication | `WhyNot/WhyNotMonitor.cs` (369); hook `WhyNot` (`ObjectBase.CeaseFire`); `4. Why not` | text works without; the reply is spoken | roster (iterates `Roster.Elements` for player units and as speaker), `RadioPlayer`, `AlertLink`; message text from `AlertText()` | **B** |
| 22 | Why-not inside stock lines | "queued, number three" for stacked orders; wingman "holding formation" instead of a silent VID; optional *Detach wingman for VID* | `StockVoiceRouter.PickPhrase` (Queued_N, `Identify`) | the VID message and detach work without audio, but they hang off the stock voice stream | `StockVoice` hook (the game's `Identify` voice event) | **B/C** |
| 23 | **Contact reports** (anti-spam) | the game's contact messages muted and replaced: hysteresis for faded/regained, cooldowns, flicker detection, raids called once, track numbers clickable, "identified by ESM/visual/..." | `Contacts/ContactReporter.cs` (463); hook `ContactAlerts` (three `Taskforce.Check*Message` + `RaiseAlert` filter); `3. Contacts` | text works without; calls are spoken | roster for the speaker and "command voice", `RadioPlayer`, `AlertLink` | **B** |
| 24 | **Fuel calls** | joker / bingo / fuel emergency against the nearest deck or field that would take the aircraft; tank-share fallback for aircraft with no home | `Air/FuelMonitor.cs` (481), `LandingCheck.cs` (108); no hook; `Fuel calls` | text works without | roster (iterates aircraft), `RtbMarker.IsRtb`, `RadioPlayer`, `AlertLink` | **B** |
| 25 | **Search and rescue calls** | survivors in the water with bearing, rescuer en route, pickup with souls aboard and cabin full, delivered on deck; replaces the game's "Picked up survivors" | `Air/SarReporter.cs` (228); hook `SarCalls` (5 targets); `Search and rescue calls` | text works without | roster (speaker), `RadioPlayer`, `AlertLink` | **B** |
| 26 | Options page | "Rats Radio" group on the Audio tab | `Ui/OptionsPage.cs`; hook `OptionsPage` | - | everything voice | stays |

Notes:
- The reporters (#21, #23-25) already raise their **text** whether or not a voice can speak it
  (`TransmissionBuilder.Build` returns null without a voice, the message still goes up). So a text-only version is not a
  new feature, it is the same code with the voice step left out.
- None of #17-#20 needs a single audio file.

---

## 3. Options

### Option A: one small mod per feature

Order Queue, Clickable Messages, Unit Roles, RTB Highlight, Why Not, Contact Filter, Air Ops (fuel + SAR).

- Pro: a player picks exactly what they want; each page is easy to explain.
- Con: seven more Workshop items, each with pictures, posts, releases, `compat`, both loaders, both game builds, and its
  own Options group. The workspace already has seven mods. Four of these are under 300 lines. The maintenance cost per
  item (release, Workshop check, posts) is the same for 165 lines as for 4,000.
- Con: features that depend on each other (queue ↔ why-not, roles ↔ RTB colour, links ↔ held messages) need cross-mod
  signals in every pair.

### Option B: one tools mod for everything that is not voice (recommended)

**Sea Power: R.A.T.S. Command Tools** (Steam title "R.A.T.S. Command Tools"; working name, alternatives: *Ops Board*,
*Bridge Tools*): order queue window, clickable mentions, unit roles, RTB highlight, and, in a second step, the text side of
why-not, contact filter, fuel and SAR. Every feature switchable, as in Radio today.

**R.A.T.S. Radio stays the radio**: everything in 2a, plus the spoken side of the reporters.

- Pro: answers the actual need ("all the useful bits without 400 MB of voices") with one item, one page, one Options group.
- Pro: the four UI features already depend on each other a little; inside one mod that stays simple.
- Con: a player who wants only the queue window gets four more toggles. They are all switchable and default sensibly.

### Option C: leave it as it is

Everything already has its own config switch. Cost zero. But the 400 MB download and the "radio" name keep players who
want the tools and not the voices away, and the tools are invisible on the Workshop under a voice mod's page.

**Recommendation: B, in phases (section 6).** Option A only for the order queue if the owner wants a quick, very small
first item; it can later be folded into Command Tools (an item can be emptied and pointed at the new one).

---

## 4. How it would work

### 4.1 Shared code: shared **sources**, not a shared DLL

- Both loaders load mod DLLs with `Assembly.LoadFile`; the same DLL in two folders becomes two assemblies with separate
  statics (`Plugin.cs` already guards against its own double load). A shared runtime library ("R.A.T.S. Core" item) would
  bring version clashes between mods, load-order questions under two loaders, and a third item every player must have.
  Not worth it.
- Instead: a folder of shared **source files** compiled into each mod (`internal`, so no type clashes between
  assemblies). Candidates: `PatchRegistry`/`Feature`, `ModLog`, `EventBus` + `MissionPatches`, the IMGUI scaling helper,
  a "player units" enumerator, `AlertText`/RaiseAlert helpers, and the feature files themselves (`QueueOverlay`,
  `MessageLinker`, `RoleLine`/`UnitRole`, `RtbMarker`).
- Where it lives: a small repo `struckl/SeaPowerShared` as a git submodule in both mod repos, or a `shared/` folder in
  Command Tools that Radio includes by relative path in its `.csproj`. Submodule is cleaner for two public repos.
- Each mod keeps its own entry (`[ACPlugin]`/`IAnchorChainMod`), its own GUID, its own globe/flat gate. Command Tools
  needs **two builds** (globe and flat) because `MapContactVisuals.Resolve` differs (#20); `compat` decides if the rest
  could have been one DLL.

### 4.2 Both installed: no double patches, no double windows

During the transition (and for players who keep both), the same feature exists in two DLLs. Only one may run it:

- **Claim at start, decide on the first frame.** Each mod writes its claims into the AppDomain at `TriggerEntryPoint`
  (`AppDomain.SetData("rats.feature.queue-window", "<guid>|<shared-source version>")`). Patching and overlays start on
  the first `Update` of the host, when both entry points have run, whatever the loader order. The highest shared-source
  version wins; on a tie, Command Tools wins for the UI features (#17-20).
- **Voiced features (#21-25):** when Radio is installed and on, **Radio owns them** (its implementation is a superset:
  text plus voice). Command Tools stands down for those and only runs them when Radio is absent or switched off.
- The losing copy logs one line ("queue window: provided by R.A.T.S. Command Tools 1.0.0") and greys its setting (or
  says so in its config description).
- Cross-feature signals go through the AppDomain too, with primitive data only (no shared types across assemblies):
  e.g. the queue window's "player cleared the orders of unit <instance id> at <time>", which Radio's why-not reads
  instead of `WhyNotMonitor.MarkCleared` today.

### 4.3 The one shared hook: `MainGameViewModel.set_AlertyMcAlertFace`

Radio's `AlertLink` prefix replaces the setter (returns false) to keep messages until spoken; `MessageLinker` runs inside
that prefix. If the linker moves to Command Tools, its prefix must run **before** Radio's (Harmony `[HarmonyPriority
(Priority.High)]`, or Radio calls the linker through the AppDomain signal when Command Tools is present). With
Harmony priority it is independent of load order. Pop-Out also patches the formation manager (`FormationManagerViewModel.
SelectedTabIndex`), not the view constructor; no clash with #19/#20, but test the formation manager with Pop-Out on.

### 4.4 The roster knot

Text-only reporters need "the player's units", not voices. Split `Roster` into:
- a **unit list** (shared source): player units, airborne aircraft, hangar slots, which unit detected what, the command
  unit of a task force;
- the **voice binding** (Radio only): voice, callsign, pack, campaign memory, aliases, salts.

`WhyNotMonitor`, `ContactReporter`, `FuelMonitor`, `SarReporter` then take a speaker hook that Radio fills (voice the
call) and Command Tools leaves empty. This is the biggest piece of work in the whole split (estimate: 1-2 days for the
roster split, then about half a day per reporter, plus an in-game regression test of every voiced call in Radio).

### 4.5 Existing subscribers keep everything

- **Nothing is taken out of Radio in phase 1.** Radio keeps every feature; Command Tools is purely additive. A Radio
  subscriber notices nothing, except that a Command Tools subscriber's copy takes over the UI features (same behaviour,
  same defaults).
- **Settings:** Command Tools uses its own config file (`dev.sewerlabs.seapowertools.cfg`) with the **same section and
  key names** as Radio's (`5. Order queue`, `7. Formation manager`, `Highlight RTB`, `Clickable units and tracks` ...). On
  its first start, if Radio's config exists and its own has no value yet, it copies those values once. So a player's
  F8 key or "roles off" carries over.
- **Required Items:** do not make Radio require Command Tools, at least not before phase 3. Steam's Required Items only
  prompt when someone subscribes; existing subscribers are not subscribed automatically, so a Radio that had dropped the
  features would silently lose them for everyone already subscribed. Only once Radio has carried both for a while and the
  texts have told players, consider making Command Tools a Required Item of Radio and removing the duplicates.
- Workshop texts: Radio's page lists the tools as before and adds "also available without the voices: R.A.T.S. Command
  Tools"; Command Tools' page says it works alone and together with Radio.

---

## 5. What stays in Radio, what moves

| Stays in Radio only | Moves to Command Tools (Radio keeps a copy until phase 3) |
|---|---|
| everything in 2a (#1-#16) | order queue window (#17) |
| the spoken side of why-not, contacts, fuel, SAR | clickable mentions (#18) |
| "queued, number three" and the other stock-line tweaks (#22, voice) | unit roles (#19) |
| messages held until spoken (#16) | RTB highlight (#20) |
| Options page "Rats Radio" | phase 2: text side of why-not (#21), contact filter (#23), fuel (#24), SAR (#25), the VID "holding formation" message and *Detach wingman for VID* (#22, text part: Command Tools then hooks `VoiceMessage.SubmitMessage` as an **observer only**, never returning false) |

Not worth splitting: held messages (only useful while something speaks), call volumes, anything about voices, packs,
moods, aliases, squadrons, campaign memory, salts; `LandingCheck` (a part of fuel calls, not a feature).

---

## 6. Suggested order

1. **Phase 1, Command Tools 0.1.0 (about 1-2 days):** shared-source folder; queue window, clickable mentions, unit roles,
   RTB highlight; claim/first-frame arbitration; settings import; own small Options group (Interface or Gameplay tab,
   copied from Radio's `OptionsPage` pattern); two builds; `compat`, both loaders. Radio 1.6.x picks up the shared sources
   and the arbitration, behaviour unchanged. Test in the game: each mod alone, both together (no double window, no double
   yellow, links once), with Pop-Out.
2. **Phase 2, Command Tools 0.2.0 + Radio 1.7 (several days):** roster split; text-only why-not, contact filter, fuel, SAR in
   Command Tools; Radio owns them when present. Full regression of Radio's spoken calls.
3. **Phase 3 (optional, later):** Radio requires Command Tools and drops its copies, only if the duplicate builds become a
   burden; announce first.

If the owner wants something within a day instead: the order queue alone as *R.A.T.S. Order Queue* (≈ 200 lines with the
glue), with the claim mechanism from 4.2, and fold it into Command Tools later.

## 7. Honest cons

- Every shared feature is then built into two DLLs; a fix needs two releases (or waits for the next of the other).
- Arbitration and settings import are new code that can go wrong in ways players see (a feature running twice, or not at
  all). It needs the in-game matrix: Radio alone, Tools alone, both, both with one switched off in the mod list.
- Phase 2 touches the most-tested part of Radio (its reporters). Without a real gain for voice players, it is the part to
  do last or not at all.
- The text-only reporters will feel thinner than the radio (no "reply first" ordering, no held messages); that is fine for
  players who chose no voices, but the Command Tools page should not oversell them.
