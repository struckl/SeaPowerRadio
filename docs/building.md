# R.A.T.S. Radio: building and continuing

Build notes, the two game versions, the voice pipeline and how to carry on from another PC. Short form in the [README](../README.md).

## Building

Requires the .NET SDK (8+), a local Sea Power install and the [SeaPowerKeel](https://github.com/struckl/SeaPowerKeel)
repo next to this one: the game's assemblies and Keel's copy of Anchor Chain's library are referenced directly
(`-p:AnchorChainDir=...` points at another copy).

```bash
dotnet build src/SeaPowerRadio/SeaPowerRadio.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sea Power"
```

A successful build copies the DLL and `mod/` (voice library, `_info.ini`) into
`<GameDir>/Sea Power_Data/StreamingAssets/SeaPowerRadio/`. `GameDir` can also be set as an environment variable.
Pass `-p:Deploy=false` to build without touching the game folder - and do, while the game is running (it locks the
DLLs).

**Two game versions, two builds.** Sea Power exists in two versions with different map code: 0.8.7's flat map and the
3D-globe build (0.9.x), and a mod compiled against one fails on the other (the map-symbol
hook, the night-colour switch and a field the game turned into a property differ). So the same sources are built twice:
the default build is for the globe game (`SeaPowerRadio.dll`), `-p:GameBuild=Flat` compiles against a copy of 0.8.7's
`Sea Power_Data\Managed` (`-p:FlatManagedDir=...`, default `..\_gamerefs\SeaPower-0.8.7-public\Managed` next to the mod
folders in the workspace) into `SeaPowerRadioFlat.dll`. The package holds both; each looks at the game it runs on and
stays off when it is not the one it was made for (the log says which runs). Anchor Chain skips a plugin whose GUID it
has already seen, so the flat build registers as `dev.sewerlabs.seapowerradio.flatmap`. The start, the hook registry, the
config file, the log and the host object are the workspace's shared skeleton (`SeaPowerCore`, its own repo, compiled into the DLL
as linked source; the csproj imports its `Rats.Mod.props`). `tools/package.ps1` builds and checks both
(`tools/compat-check` in the workspace).

Releases: [docs/workshop.md](docs/workshop.md) (`tools/package.ps1`); in-game test checklists:
[docs/testing.md](docs/testing.md); the game systems the mod hooks into (stock voices, why a unit doesn't fire,
mission lifecycle, contacts) and how to decompile the game: [docs/how-it-works.md](docs/how-it-works.md); the stock
voice lines and where the game raises them: [docs/stock-voice-events.md](docs/stock-voice-events.md).
Voice packs (format, discovery, per-country choice): [docs/voice-packs.md](docs/voice-packs.md); the languages planned
for them (Russian, Spanish, ...): [docs/languages-roadmap.md](docs/languages-roadmap.md). `tools/package.ps1 -Pack <id>`
stages a pack folder for upload.

The rendered voice library (`mod/voices/<persona>/`, mp3 clips) is not in git: it comes with every release
and can be rebuilt with the voice pipeline below.

**Continuing on another PC** (a fresh clone has only the text files in `mod/voices/`, so `tools/package.ps1` stops with
"Rendered voices missing" and `check_library.py` lists everything): download the zip of the latest
[GitHub release](https://github.com/struckl/SeaPowerRadio/releases) and copy its `SeaPowerRadio\voices\*` into
`mod\voices\`; build the reference clips (`tools/voicegen/refs/`, also not in git) with `vctk_refs.py` as below; then
`check_library.py` shows what is really missing and `chatterbox_render.py` resumes with just those takes. Don't start a
full render without the library: it redoes everything.

## Voice pipeline

`tools/voicegen/` (Python + ffmpeg). Shipped voices: [Chatterbox](https://github.com/resemble-ai/chatterbox) on a
local GPU, each persona rendered from one speaker of the [CSTR VCTK Corpus](https://doi.org/10.7488/ds/2645).
edge-tts (`generate.py`) remains as a fallback engine.

| File | What it is |
| --- | --- |
| `cast.py` | the 43 NATO/English personas, one VCTK speaker each (US, Canada, England, Scotland, Ireland, Northern Ireland, Australia, New Zealand; 30 m / 13 f); writes `mod/voices/personas.txt` |
| `vctk_refs.py` | builds a ~12 s reference clip per persona from VCTK, reading single parquet row groups from Hugging Face instead of the 17.6 GB corpus (`vctk/index.json`, `vctk/speaker-info.txt`) |
| `phrases.py` | phrase catalog (stock keys, why-not reasons, callouts, T3 identification causes) as *takes*: every clip is cut out of a whole sentence (phrases after a carrier callsign, callsign words / flight-position pairs / number pieces each in the slot they're used in), so stitched pieces keep the voice's own melody and pauses. `{trk}`/`{brg}`/`{num}`/`{alt}`/`{count}` slots split a phrase around a number. `STOCK_MAP` maps every stock voice key to its replacement |
| `chatterbox_render.py` | renders the library into `mod/voices/<persona>/` on the GPU; phrase urgency -> `exaggeration`/`cfg_weight`; hallucinated or unalignable takes are re-rolled; resumable |
| `align.py` | CTC forced aligner (wav2vec2) giving word timestamps, since Chatterbox reports none |
| `radio.py` | the radio sound, split into the *voice* half (baked per clip) and the *transmission* half (hiss, click, crackle, squelch, loudness gain and soft limiter - once per transmission, at runtime); its `transmission()` follows the plugin's RadioFx step for step |
| `reel.py` | stitches clips into transmissions like the plugin will, pauses at numbers closed up the same way -> `tools/voicegen/samples/` (`--as-cut`: joins as cut, for comparison; `--library`: another checkout's voices) |
| `names.py` | in-game names: ship/shore short names and squadron callsign words from the game's language files, each bound to a few voices -> `mod/voices/names.txt` |
| `rules.py` | the one source of the rules plugin and tools share: callsign words, clip number ranges, pauses at numbers, phrase groups (call volumes, moods), priority thresholds, the radio sound table |
| `rules_cs.py` | writes `rules.py`, the named variants and a few phrase facts into `src/SeaPowerRadio/Radio/ClipRules.g.cs` |
| `check_sync.py` | checks that `ClipRules.g.cs`, `catalog.txt` and `stockmap.txt` are current, that every phrase group and named variant exists, and that every stock key is mapped - run before a build and a render |
| `check_library.py` | lists takes still missing from the rendered library |
| `full_render.ps1` | detached, game-aware full render (pauses while a game runs), verify, re-roll, deploy |
| `generate.py`, `edge_cast.py` | edge-tts fallback engine and its cast |

```bash
# Chatterbox venv (torch + CUDA, ~5 GB) -- separate from the light .venv used for reels
python -m venv .venv-chatterbox
.venv-chatterbox/Scripts/python -m pip install "torch>=2.9" "torchaudio>=2.9" --index-url https://download.pytorch.org/whl/cu128
.venv-chatterbox/Scripts/python -m pip install chatterbox-tts==0.1.7 pyarrow
.venv-chatterbox/Scripts/python tools/voicegen/vctk_refs.py                      # reference clips
.venv-chatterbox/Scripts/python tools/voicegen/chatterbox_render.py --voices all  # many hours
# or detached and game-aware (stops while Hunt / Sea Power / Nuclear Option / CS2 run and frees the VRAM, resumes
# 2 min after the game closes), then checks the library, re-rolls takes that gave up, deploys the mod into the game
# and with -ShutdownWhenDone powers the PC off (abort: shutdown /a). Log: tools/voicegen/out/full_render.log.
powershell -NoProfile -ExecutionPolicy Bypass -File tools/voicegen/full_render.ps1 -ShutdownWhenDone
.venv-chatterbox/Scripts/python tools/voicegen/check_library.py                  # what's still missing

python -m venv .venv && .venv/Scripts/python -m pip install -r tools/voicegen/requirements.txt
.venv/Scripts/python tools/voicegen/reel.py --voices us_m_01,sc_m_04,us_f_01     # listening sample
```

## Layout

```
src/SeaPowerRadio/        C# plugin (netstandard2.1, BepInEx 5 / HarmonyX)
  Plugin.cs, ModInfo.cs   entry point ([ACPlugin], runs under Keel and Anchor Chain); what the shared skeleton reads (GUID, name, version, log source)
  Hooks.cs                every game hook, grouped by feature (shared HookRegistry); a missing target disables only that
                          feature, except the mission and launch hooks the roster needs (then the mod stays off)
  RadioHost.cs, Settings.cs  the host object (shared ModHost) and the config entries (shared ModSettings)
  Core/EventBus.cs        the aircraft-launched event (mission start/end come from the shared MissionHooks)
  Core/                   GameMessage (the one way a report raises its message bottom right), Distance (map units, km, nm,
                          bearing), OrderText (weapon and track names of an order)
  Patches/                Harmony patches (stock voice capture, launching weapon, aircraft launch, reports, options)
  Roster/                 player elements + hangar slots -> callsign + voice
  Voices/                 callsign generator, voice assignment, voice library and packs,
                          squadron voices (squadrons.txt)
  Radio/                  Catalog (catalog.txt / stockmap.txt), TransmissionBuilder (callsign + phrase + number
                          pieces, how much pause each join keeps), ClipLibrary (on-demand loading, LRU), RadioFx
                          (joins the clips, hiss, click, crackle, squelch),
                          RadioPlayer (priority queue, up to 4 channels for overlapping voices), CallCategory (call kind -> volume),
                          Moods, NameAliases, StockVoiceRouter (stock line -> own voice), SpokenReport (queues a report's call with its messages),
                          AlertLink and MessageLinker (messages kept until spoken, clickable units and tracks)
  Contacts/               ContactReporter: new / lost / intermittent / hostile calls with hysteresis and cooldowns
  WhyNot/                 WhyNotMonitor (engage states, reload ETA, sonar-only, dropped orders -> spoken + alert),
                          QueueOverlay (selected unit's stacked attack orders with delete buttons, F8)
  Ui/                     OptionsPage: the "Rats Radio" group in the Options menu's Audio tab (code-built Noesis controls)
  Air/                    FuelMonitor (joker / bingo calls), RtbMarker (RTB yellow + sorting), SarReporter,
                          LandingCheck (why no deck or field takes an aircraft, logged)
  Formation/              UnitRole (ship type / aircraft mission / helicopter type), RoleLine (adds the role line
                          to the formation manager's row templates when the view is built)
mod/                      files shipped next to the DLL (voices/: personas.txt, catalog.txt, stockmap.txt,
                          names.txt, rendered clips; _info.ini; rats-radio-aliases.txt = default hull prefixes)
tools/voicegen/           offline voice pipeline (cast.py = the persona list; rules.py = the clip and sound rules the
                          plugin shares, written to Radio/ClipRules.g.cs by rules_cs.py; check_sync.py checks both sides)
tools/package.ps1         release build -> dist/SeaPowerRadio/ (+ release zip)
tools/shot-director/      dev-only plugin that drives the game for the Workshop screenshots (never shipped)
docs/                     test checklists, stock voice inventory, Workshop release
```

