# Releasing on the Steam Workshop

The game uploads Workshop items itself: **Main menu → Mods → Create Mod**, signed in with the Steam account that
owns the item. No SteamCMD needed.

## Before a release

1. Bump the version in `src/SeaPowerRadio/Plugin.cs` (`Version`) and `src/SeaPowerRadio/SeaPowerRadio.csproj`
   (`<Version>`), add the entry to [CHANGELOG.md](../CHANGELOG.md).
2. Check `mod/_info.ini`: Mod Manager name and description, and `[Compatibility]` (`ApproximateVersion` = the game
   version tested against; it accepts later patch versions of that minor version).
3. New phrases since the last release? Render them (`tools/voicegen`, see the README), then
   `.venv-chatterbox/Scripts/python tools/voicegen/check_library.py` – nothing may be missing.
4. Run the checklists in [testing.md](testing.md) on the current game version.
5. Close Sea Power, then stage and install the release folder:

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1 -Install -Zip
   ```

   This mirrors exactly what players get into `<Sea Power>\Sea Power_Data\StreamingAssets\SeaPowerRadio\` (DLL,
   `_info.ini`, `LICENSE.txt`, `CREDITS.txt`, `voices\` with ~57,000 clips, ~390 MB) and writes
   `dist\SeaPowerRadio-<version>.zip` for the GitHub release (manual installs).

## Upload

Start Sea Power → Mods → Create Mod:

| Field | Value |
| --- | --- |
| Existing item | pick *Sea Power: Rats Radio* – Workshop item [3826433579](https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579), first uploaded 2026-10-24 as 1.0.0 |
| Folder | `StreamingAssets\SeaPowerRadio` (the folder `package.ps1 -Install` filled). **Double-click** `SeaPowerRadio` so the picker is *inside* it, then confirm: the picker takes the folder it is in, a single click only highlights. Confirming in `StreamingAssets` itself gets "… is a forbidden path!" (the game refuses `StreamingAssets` and `StreamingAssets\original`) |
| Name | Sea Power: Rats Radio (the naming of "Sea Power: Rats Topographic Map" and "Sea Power: Rats Pop-Out Windows") |
| Description | [workshop/description.bbcode](../workshop/description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerRadio\preview.jpg` (put there by `package.ps1`: the picker only browses StreamingAssets and only takes .jpg; Steam wants it under 1 MB) |
| Tags | Audio, Scripting |
| Dependencies | `3380210757` (Anchor Chain) – shows as a required item on the Workshop page |
| Visibility | first upload **Hidden** (or Friends only); Public once the subscribed copy is tested |
| Change log | the CHANGELOG entry |

The first upload takes a while (~390 MB in ~57,000 files). Later updates only send and download the files that
changed, so a plugin-only update is small for players.

After the first upload, on the Workshop page itself (Steam web → Edit → Add/edit images & videos):

- the screenshots from [workshop/screenshots/](../workshop/screenshots), in their numbered order:

  | Image | Shows |
  | --- | --- |
  | 01-radio-net | the message log full of radio calls over the Aegean (*Dangerous Straits 1985*): new air contacts with bearings, a hostile classification by ESM, a flight on position – every unit and track a link |
  | 02-unit-roles-rtb | the formation manager with each ship's role line, the F-5 pair heading home in yellow on the map |
  | 03-message-log | close-up of the message log |
  | 04-unit-roles | close-up of the role lines (Battleship, Destroyer, Missile Cruiser …) |

- the sound sample: `workshop/radio-net.mp4` (70 s, 1080p, the radio net while Yak-38s close in on the Iowa
  group; not in git) uploaded to YouTube, then added as a video – the Workshop only embeds YouTube.

Then the Workshop link into the README and the description, and the release zip as a GitHub release (`v<version>`).

## Remaking the screenshots and the sample

[tools/shot-director](../tools/shot-director) is the dev-only plugin (never shipped) that takes commands from
`<Sea Power>\BepInEx\shotdirector.cmd` and logs to `shotdirector.log`; the full command list is at the top of
`Director.cs`. Build it with `dotnet build tools/shot-director -c Release` (deploys to `StreamingAssets\ShotDirector\`),
start the game through Steam, wait for the main menu, then for example:

```
load missions/NATO/Dangerous Straits 1985.ini
click 3141 2067          (Play – twice, the first click only focuses the game)
click 1920 1605          (Start mission)
key f6                   (Topographic Map layer off, if installed)
tc 8 … tc 1              (let contacts come in)
select Iowa
attack Iowa | 7021 | usn_rgm-84c
rtb F-5A 343 MAI-1
window formation | window eventlog
center 37.30 25.95 9.3
quit
```

Grab the main monitor from outside the game (the game's own `ScreenCapture` fails while Pop-Out has a window on
another monitor) with DPI awareness on, at 3840x2160, and crop the top 46 px (Steam's performance overlay). For
these shots Pop-Out's remembered popped windows were cleared for the session, so the formation manager and the
message log opened on the main screen. The sample was recorded with Desktop Duplication + NVENC for the picture
(`ffmpeg -f lavfi -i ddagrab=…`) and a WASAPI loopback of the speakers for the sound (Python `soundcard`), muxed and
scaled to 1080p. Afterwards: `quit`, remove `StreamingAssets\ShotDirector\`, put back any config the run changed.

The order queue window (F8) did not show up in this run, although Iowa had attack orders waiting – probably
because the director's `select` doesn't select a unit the way a click on the map does (F8 also toggles the window
off; it starts shown). A screenshot of it is still missing.

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerRadio` folder** (or rename it): with two copies only the
first one Anchor Chain finds starts, and the log says it skipped the other. The subscribed copy lives in
`steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the roster checklist; then switch the item
to Public.

## Still to do for the first release

- A screenshot of the order queue (F8) and of a spoken refusal ("Unable – target outside launch envelope").
- Settings inside the game (most players won't edit the BepInEx config).
- Test matrix: other game languages (unit roles, callsign words), large missions (many units), the game's own
  voice volume settings.
