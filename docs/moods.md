# Moods (1.5.0)

Every voice of the NATO (English) pack has three more deliveries, rendered with Chatterbox on the render PC
(2026-11-08 to 11-11, report in the private repo `struckl/rats-render-handover`, `results/`):

| Mood | Clips per voice | exaggeration / cfg_weight |
| --- | --- | --- |
| stressed | the whole voice (catalogue + its ship and squadron names), 58,191 in all | 1.0 / 0.3 |
| happy | 31 keys (`tools/render-app/mood-keys/happy.txt` on the render branch) | 0.7 / 0.5 |
| sad | 15 keys (`sad.txt`) | 0.35 / 0.6 |

## The Moods pack

A separate Workshop item, so the base stays at its size. Folder (staged by `tools/package.ps1 -Moods`, installed locally
to `StreamingAssets/SeaPowerRadioMoods`): `moodpack.ini`, `_info.ini`, `CREDITS.txt`, `<persona>/<mood>/<key>.mp3`.

```ini
[Moods]
Pack=nato-en              ; the voice pack whose personas these are
Moods=stressed,happy,sad
Version=1.0.0
MinBaseVersion=1.5.0
```

Discovery is the voice-pack scan (`VoicePacks.Discover`): every mod folder (StreamingAssets, Workshop items) with a
`moodpack.ini` at the top or in `voices/` adds a mood root to that pack. A pack's own folder counts too, if its personas
have mood sub folders (the repo's `mod/voices` after a render). Mood clips are keys `mood/key` (`VoicePack.PathOf`).
The base package never carries mood folders (`robocopy /XD stressed happy sad`).

## Which mood a call gets (`Radio/Moods.cs`)

1. The call's own event: SplashOne, SplashTrack, TargetDestroyed, Shack, HitTrack, MissionComplete, ObjectiveComplete,
   SAR pickups and deliveries, recoveries, ContactRegained → **happy**; MissionFailed, ObjectiveFailed, SarFull,
   SarSurvivors, Winchester, BatteryLow, NeedToCharge, DeckDamaged, ContactFaded(s) → **sad**.
2. Warnings (missile/torpedo inbound, we're hit, flooding, bingo, emergencies) and weapons calls → **stressed**; the unit
   stays stressed 60 s after its own warning and 20 s after its own weapons call, for every call it makes.
3. An acknowledgement within 40 s after the unit's own success → **happy**.
4. Else neutral. Per clip: a key the mood has no take for plays neutral (so a happy "splash one" keeps the neutral
   callsign). State is per roster element and cleared at mission end. Setting `2. Radio` / `Moods` (default on).

## Not done

- Not run in the game yet.
- The 0.9.0 South Atlantic names (236 ship and squadron names, Belgrano, 25 de Mayo, Ark Royal ...) have no clips in any
  mood or in the base yet: those units fall back to the generated callsign.
