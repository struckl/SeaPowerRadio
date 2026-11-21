# Sea Power: R.A.T.S. Radio

Radio voices for [Sea Power: Naval Combat in the Missile Age](https://store.steampowered.com/app/1286220/): every unit of
yours talks in its own voice and callsign, and says why it is not doing what you ordered.

| | |
| --- | --- |
| Questions | [R.A.T.S. Discord](https://discord.gg/CzuHd4f8Yf): help, ideas, talk; the first place to ask |
| Version | `1.7.0` (unreleased) |
| Game | Sea Power 0.9.3 (build 261116), the current main version; run in the test VM |
| Needs | [R.A.T.S. Keel](https://steamcommunity.com/sharedfiles/filedetails/?id=3835527459), the mod loader, and R.A.T.S. Rigging, the part the R.A.T.S. mods share (a copy comes with this mod for now) |
| Get it | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579) (about 910 MB) |
| Settings | Options > Audio > Rats Radio, or `BepInEx\config\dev.sewerlabs.seapowerradio.cfg` |
| Bug reports | [R.A.T.S. feedback board](https://hub.sewerlabs.dev/s/radio) |
| Video | [YouTube, 1:49](https://youtu.be/pGwOIPXckpY) |
| Source | R.A.T.S. Source License, see `LICENSE` |

![The message log full of radio calls over the Aegean](workshop/screenshots/01-radio-net.jpg)

## Features

- 43 voices in eight English accents, each unit gets its own the moment it exists; accents follow the nation (1.4.0).
- Ships and shore units say their in-game name, aircraft their squadron callsign, others a NATO callsign ([details](docs/features.md)).
- The game's voice lines are muted and spoken by the unit that said them; other mods' lines play as before.
- Moods (1.5.0): stressed, happy and sad takes for warnings, kills and failures ([docs/moods.md](docs/moods.md)).
- Random voices each mission, female voices and gender per squadron (`squadrons.txt`) are switchable.
- A radio net: urgent calls cut in, orders are answered first, stale calls are dropped, up to 3 voices overlap.
- Why-not: the reasons the game never says (out of envelope, no lock, reloading, no line of sight and more).
- Contact calls without spam, joker and bingo fuel calls, landing check, search and rescue.
- Messages link to the unit or track on the map; returning aircraft turn yellow; role lines in the formation manager.
- Order queue window (F8): each stacked attack order with its own delete button.
- Voice packs per country (Options > Audio > Rats Radio > Voice packs): [docs/voice-packs.md](docs/voice-packs.md).

Full detail: [docs/features.md](docs/features.md).

## Install

1. Install [R.A.T.S. Keel](https://steamcommunity.com/sharedfiles/filedetails/?id=3835527459), the mod loader
   (subscribe, switch it on in the Mods menu, run its setup once).
2. Subscribe to [Sea Power: R.A.T.S. Radio](https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579) on the Workshop.
3. Start a mission. `<Sea Power>\BepInEx\LogOutput.log` lists the voice packs and the roster (unit, callsign, voice, nation).

Manual install: unzip the release into `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerRadio\`.

## Settings

| Setting | Default | What |
| --- | --- | --- |
| Female voices | off | units use female voices too |
| Random voices | on | a fresh mission start shuffles the voices |
| Moods | on | stressed, happy and sad takes |
| Overlapping voices | on | a new call may start while the last one plays |
| Radio volume, radio static | 100 % | overall level; static 0 to 150 % |
| Call volumes | per kind | contacts, combat, warnings, replies, other |
| Voice packs | Auto | per country: pack, game voice or Auto |

- All settings: `<Sea Power>\BepInEx\config\dev.sewerlabs.seapowerradio.cfg`; ship names in `aliases.txt`, squadron gender in `squadrons.txt` (formats in [docs/features.md](docs/features.md)).
- Quick fix for "contact faded" spam without any mod: Options > Audio > Report Faded Contacts > off.

## Building

- Needs the .NET SDK (8+), a Sea Power install and the SeaPowerRigging repo (R.A.T.S. Rigging) next to this one.
- One build for the game's current main version.

```bash
dotnet build src/SeaPowerRadio/SeaPowerRadio.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sea Power"
```

Details, voice pipeline and continuing on another PC: [docs/building.md](docs/building.md). Releases:
[docs/workshop.md](docs/workshop.md);
game internals: [docs/how-it-works.md](docs/how-it-works.md).

## Credits and license

- Code and voices: [R.A.T.S. Source License](LICENSE); read it, fork it, send fixes; no re-uploads, no reuse in other projects. Releases before 2026-11-18 stay MIT (code) and CC BY 4.0 (voices).
- Voices are pre-rendered with [Chatterbox](https://github.com/resemble-ai/chatterbox) (MIT) from speakers of the CSTR VCTK Corpus, licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); credits in [VOICES-LICENSE.md](VOICES-LICENSE.md).
- Corpus: Yamagishi, Veaux, MacDonald (2019). *CSTR VCTK Corpus (version 0.92)*, University of Edinburgh, https://doi.org/10.7488/ds/2645.
- Radio chain from [Ratten Chatter](https://github.com/struckl/RattenChatter); word alignment with wav2vec2-base-960h (Apache-2.0).
- Built on [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX) and [Anchor Chain](https://github.com/SeaPower-Modders/AnchorChain).
- Works with [R.A.T.S. Topo](https://github.com/struckl/SeaPowerTopo) and [R.A.T.S. Pop-Out](https://github.com/struckl/SeaPowerPopOut). No game files are changed.
- Not affiliated with Triassic Games or MicroProse.
