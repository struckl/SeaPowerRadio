# The R.A.T.S. bus: how mods talk to R.A.T.S. Radio (2.0)

Since 2.0, R.A.T.S. Radio is the radio net only. Voices come as items of their own (voice packs, mood packs, line packs),
and reports come from other mods (R.A.T.S. Command Tools first) that speak through Radio. No mod references another's DLL:
both mod loaders (R.A.T.S. Keel, Anchor Chain) load DLLs with `Assembly.LoadFile`, so shared types across mods would
break. Mods meet in the AppDomain instead, with plain .NET types only, and each works alone.

## Keys

| Key | Type | Set by | Meaning |
|---|---|---|---|
| `rats.radio.version` | `string` | Radio 2.0+ at start | Radio runs and knows this contract |
| `rats.radio.say` | `Func<object, string[], int[], bool>` | Radio 2.0+ at start | speak over the net, see below |
| `rats.commandtools.version` | `string` | Command Tools at start | Command Tools runs |
| `rats.commandtools.features` | `string`, comma list | Command Tools at start | the features it runs: `queue`, `links`, `roles`, `rtb`, `whynot`, `contacts`, `fuel`, `sar` |

Read them when they are needed (a mission, a key press), not in the entry point: the loaders start mods in no fixed
order, and by the first frame every mod has written its keys.

## Speaking: `rats.radio.say`

```csharp
if (AppDomain.CurrentDomain.GetData("rats.radio.say") is Func<object, string[], int[], bool> say)
    say(unit, new[] { "SarPickupCount", "SarFull" }, new[] { 4, -1 });
```

- `unit`: the speaking unit (`SeaPower.ObjectBase`), or `null` for the task force's command voice. Radio uses that unit's
  voice, callsign and mood, and queues the call by its catalog priority (alerts cut in, replies before reports).
- `phrases`: phrase keys of the unit's voice pack catalog (`catalog.txt`), in order, one transmission.
- `numbers`: one per phrase; `-1` = no number. A number goes into the phrase's slot (bearing, track, altitude, count).
- Returns `true` if the call was queued. `false`: no mission, a country that keeps the game's voice, or the voice has no
  clip for the phrase. The caller's own message then stands alone. It never throws.
- **Messages:** raise the message bottom right first (`MainGameViewModel.RaiseAlert(unit, text)`), then call `say` in the
  same frame. Radio takes the messages raised for that unit in that frame and keeps them visible until the call was
  spoken, with the amber bar while it is spoken.

## A mod's own lines: line packs

A mod with lines of its own (say, an automatic search-and-rescue mod) ships them as a **line pack**: a folder in its
Workshop item, no code.

```
<mod folder>/radio/            (or the mod folder itself)
  linepack.ini
  catalog.txt                  rows like the voice pack's: phrase|n|slot|a|b|priority|role|pauseA|pauseB
  us_m_01/AutoSarLaunch_1.mp3  one folder per persona of the voice pack, clips named <phrase>_<n>[_a|_b].mp3
  us_m_01/stressed/...         optional mood takes
```

```ini
[Lines]
Id=rats-autosar
Pack=nato-en            ; the voice pack whose personas these are
Version=1.0.0
MinBaseVersion=2.0.0
Moods=stressed          ; optional: mood folders in the persona folders
```

Radio finds it with the voice packs (every StreamingAssets folder and Workshop item: the top level, `voices/` or
`radio/`), adds its rows to the pack's catalog and looks up clips the pack
lacks in the line pack's folders. The mod then says `say(unit, new[] { "AutoSarLaunch" }, new[] { -1 })`. A line pack for
another language (`Pack=ru`) works the same once that pack exists. The lines are rendered with Radio's voice tools
(`tools/voicegen`, `tools/render-app`), one take per persona, like the base library.

## Packs and versions

| Folder marker | What | Needs |
|---|---|---|
| `voicepack.ini` | a voice pack (a language or an accent family): personas, catalog, names, clips | `MinBaseVersion` |
| `moodpack.ini` | mood takes for a voice pack's personas | `MinBaseVersion` |
| `linepack.ini` | another mod's lines for a voice pack | `MinBaseVersion` 2.0.0 |

A pack whose `MinBaseVersion` is above the running Radio is skipped with a log line. Two copies of one pack id: the higher
`Version` wins. Radio's own `data/` folder holds what is the same for every language: `nations.txt` (country groups) and
`stockmap.txt` (which of the game's voice keys Radio speaks), used by any pack without its own.

An accent pack is a voice pack with `Language=en` (English rules for numbers and callsigns) and the nations it should
speak for in `Nations=` (e.g. Japan for English with a Japanese accent); the player can also pick it per country in
Options > Audio > Rats Radio > Voice packs.

## Command Tools and Radio together

- Command Tools runs its features alone (messages, windows, colours) and voices its reports through `rats.radio.say`
  when Radio is there.
- Radio looks at `rats.commandtools.features`: the game's own contact lines are left to the game unless `contacts` runs
  (then Command Tools reports them); the "queued, number three" and "holding formation" replies need `whynot`.
- Both hook `VoiceMessage.SubmitMessage` (Command Tools only listens, with high Harmony priority, before Radio) and the
  message setter `MainGameViewModel.AlertyMcAlertFace` (Command Tools makes the links first, Radio may then replace the
  setter to keep messages until spoken).
- A Radio older than 2.0 still has these features itself and sets no `rats.radio.version`; Command Tools then stands
  down completely and logs that Radio should be updated.
