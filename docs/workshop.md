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
| Existing item | first upload: none (creates the item); later: pick *Sea Power Radio* |
| Folder | `StreamingAssets\SeaPowerRadio` (the folder `package.ps1 -Install` filled) |
| Name | Sea Power Radio |
| Description | [workshop/description.bbcode](../workshop/description.bbcode) |
| Preview image | `workshop\preview.jpg` (the picker only takes .jpg; Steam wants it under 1 MB) |
| Tags | Audio, Scripting |
| Dependencies | `3380210757` (Anchor Chain) – shows as a required item on the Workshop page |
| Visibility | first upload **Hidden** (or Friends only); Public once the subscribed copy is tested |
| Change log | the CHANGELOG entry |

The first upload takes a while (~390 MB in ~57,000 files). Later updates only send and download the files that
changed, so a plugin-only update is small for players.

After the first upload: add the Workshop link to the README and the description, a short audio/video sample or more
screenshots on the Workshop page (Steam web → Edit → Add/edit images & videos), and the release zip as a GitHub
release (`v<version>`).

## Testing the Workshop copy

Subscribe, then **remove your local `StreamingAssets\SeaPowerRadio` folder** (or rename it): with two copies only the
first one Anchor Chain finds starts, and the log says it skipped the other. The subscribed copy lives in
`steamapps\workshop\content\1286220\<item id>\`. Start a mission and run the roster checklist; then switch the item
to Public.

## Still to do for the first release

- Screenshots and a sound sample: `workshop/preview.jpg` (Workshop preview), `workshop/thumbnail.png` (Mod Manager
  picture); a short clip of the radio net in action helps more than any text.
- Settings inside the game (most players won't edit the BepInEx config).
- Test matrix: other game languages (unit roles, callsign words), large missions (many units), the game's own
  voice volume settings.
