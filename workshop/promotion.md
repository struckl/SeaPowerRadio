# Rats Radio – release and promotion

Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579 (item 3826433579, uploaded 2026-10-24 21:46, public, tags Overhaul, Realism, Audio, Scripting, Effects)
Video: https://youtu.be/pGwOIPXckpY (1.0.1, 1:49 – before/after, then the radio net in Dangerous Straits; `workshop/radio-net-1.0.1.mp4`, not in git) – on the Workshop page since 2026-10-27
First video: https://youtu.be/g7P1pg2J0Lk (1.0.0, 70 s, unlisted since 2026-10-27; `workshop/radio-net.mp4`, not in git)
Source: https://github.com/struckl/SeaPowerRadio · Release zip (manual install): GitHub release v1.0.0 (draft until
published)
Images: `workshop/screenshots/` – 01-radio-net, 02-unit-roles-rtb, 03-message-log, 04-unit-roles

---

## 0. Steam Workshop upload (Main menu → Mods → Create Mod)

**Uploaded 2026-10-24 21:46 – item 3826433579.** Both fixes below are done: the description since 2026-10-27
(with the roadmap section, set through `tools/workshop-upload description`), CREDITS.txt with the 1.0.1 upload.
They were needed because the upload came seven minutes before the AI wording was taken out of the texts:

1. **Description:** on the Workshop page → Owner Controls → *Edit title & description* → replace the text with
   [description.bbcode](description.bbcode) as it is now (the AI wording is gone from it).
2. **CREDITS.txt in the package:** Create Mod again → *Existing item: Sea Power: Rats Radio* → same folder
   (`StreamingAssets\SeaPowerRadio` already has the new CREDITS.txt) → change note "Credits text". Steam only
   sends the changed file, so this upload is small. Leave name, description, preview and tags as they are.

The table below is how the first upload was done.

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerRadio` – double-click into it before confirming |
| Name | Sea Power: Rats Radio |
| Description | [description.bbcode](description.bbcode) (6.6k of Steam's 8k characters) |
| Preview image | `StreamingAssets\SeaPowerRadio\preview.jpg` |
| Tags | Audio, Scripting (uploaded with Overhaul, Realism, Audio, Scripting, Effects) |
| Dependencies | `3380210757` (Anchor Chain) |
| Visibility | Hidden first, Public once the subscribed copy works |
| Change log | First release: a voice and callsign for every unit, the stock voice lines re-spoken by the unit that said them, units that tell you why they won't fire, an order queue with single deletes, contact reports without spam, clickable messages, fuel, RTB and SAR calls, unit roles in the formation manager. |

The first upload takes a while (~390 MB, ~57,000 files). Afterwards, on the Workshop page (Owner Controls → Add/edit
images & videos): **the YouTube video first** (https://youtu.be/g7P1pg2J0Lk), then the screenshots 01, 02, 03, 04.

---

## 1.0.1 – YouTube video for the update (uploaded 2026-10-27: https://youtu.be/pGwOIPXckpY)

The same video went up twice; the copy of 2026-10-25 (https://youtu.be/C4FX7CLvc4c) is the one to delete or unlist.

File: `workshop/radio-net-1.0.1.mp4` (not in git; 1:48, 2560×1440, 30 fps, -15 LUFS) – before/after intro with the
same call in 1.0.0 and 1.0.1, then the radio net live in *Dangerous Straits 1985* at 4× time (contact reports with
bearings, hostile tracks, Yak-38s inbound, "birds away", "we're hit", "splash track 7030"), end card for the
Workshop. Recorded 2026-10-25 with the shot director; contact reports at full volume for the take (the default
mixes them at 0.5).

YouTube Studio → Create → Upload videos → the file. Not made for kids, category Gaming.

**Title**

Sea Power: Rats Radio 1.0.1 – bearings and track numbers without the pause

**Description**

```
Rats Radio 1.0.1 – the update to the radio mod for Sea Power. After 1.0.0 players noticed a pause between a word and its number ("bearing … two seven three"). That pause is gone: bearings, track numbers and depths are now said in one breath, the way they sound on a real radio net. Pauses the sentence really has – after the callsign, "track 7034, faded" – stay.

0:00 Before and after
0:10 The radio net live: Dangerous Straits 1985, the Iowa group in the Aegean at 4× time
1:10 Yak-38s inbound: air contacts, birds away, we're hit, splash

Same 43 voices, nothing re-rendered – the Workshop update is only 0.1 MB.
Also out, 1.0.2: ships give the right reason when they can't fire, and a sinking ship or an incoming raid is called once instead of per raft or per aircraft.

Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
Needs Anchor Chain: https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757
Source (MIT): https://github.com/struckl/SeaPowerRadio
All features in the first video (1.0.0): https://youtu.be/g7P1pg2J0Lk
Voices based on speakers of the CSTR VCTK corpus (University of Edinburgh, CC BY 4.0), rendered with Chatterbox.
```

Chapters need at least three timestamps from 0:00, each chapter 10 s or longer – hence 0:10, not 0:09.

**The old video (https://youtu.be/g7P1pg2J0Lk)** – keep it (views, and the README and the 1.0.0 release link it).
Its live title ends in a stray "radio net" ("…(mod, sound on)radio net"); new title:

Rats Radio – a voice for every unit in Sea Power (mod, sound on)

Description: these lines in front of the existing one (section 1 below):

```
Update: Rats Radio 1.0.2 is out – bearings and track numbers without the pause, the right reason when a ship can't fire, and survivors and raids called once instead of per raft or aircraft. New video: https://youtu.be/pGwOIPXckpY
This video shows 1.0.0.
```

Optionally an end screen (Studio → Editor → End screen → Video) on the old video pointing to the new one.

Afterwards: the new link on the Workshop page (Owner Controls → Add/edit images & videos, in front of the 1.0.0
video), in the README and at the top of this file.

---

## Update posts – 1.0.1 + 1.0.2 (2026-10-25)

Both updates are live on the Workshop (subscribers get them automatically) and on GitHub, the new video is on
YouTube and on the Workshop page. The video file itself is too big for Discord without Nitro (41 MB, the 1080p
preview 28 MB – the free limit is 10 MB); the YouTube link shows as a playable video there.

### Reddit – r/SeaPower_NCMA (video post, sound on)

Upload `workshop/radio-net-1.0.1.mp4` as the post's video (native video autoplays in the feed).

**Title**

Rats Radio update: bearings and track numbers now sound like a real radio net – before/after (sound on)

**Body**

Thanks for all the feedback on the first version of Rats Radio! The one I heard most: a pause between a word and its number – "bearing … two seven three". Two updates are out on the Workshop, subscribers get them automatically:

**1.0.1 – no more pause before numbers**

- Bearings, track numbers and depths are said in one breath now, the way they sound on a real net – the video starts with the same call before and after
- Pauses the sentence really has stay: after the callsign, "track 7034, faded"
- Same 43 voices, nothing re-rendered – the update is tiny

**1.0.2 – the right reason, and fewer repeats**

- A ship ordered to fire Harpoons at a target out of range said "sonar contact only" – now it says "unable, target outside envelope", like the game's status bar
- A sinking ship's rafts give one "survivors in the water" instead of one per raft
- A raid found aircraft by aircraft gives one "new air contact" and one "multiple contacts" instead of a call for every aircraft

The video: Dangerous Straits 1985, the Iowa group in the Aegean at 4× time – contact reports with bearings, hostile tracks, then the Yak-38s come in.

**Coming next: the radio in your side's language**

- Phase 1 – voice packs: the language follows your side; English stays as it is
- Phase 2 – Русский: the Soviet navy and naval aviation, with Soviet callsigns
- Phase 3 – Español: for the upcoming South Atlantic 1982 update – Argentina, plus Chile, Cuba and Spain
- Phase 4 – 中文 / 日本語

Each language comes as its own Workshop item that adds to Rats Radio.

Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
Source (MIT): https://github.com/struckl/SeaPowerRadio

Keep it coming – especially calls that sound wrong for the situation or come too often. BepInEx\LogOutput.log helps a lot.

### Discord – BuOrd: Naval Modding Community, `#sp-mods`

```
## Rats Radio 1.0.2 🔊 https://youtu.be/pGwOIPXckpY
Two updates, both on the Workshop (subscribers get them automatically):
- **No more pause before numbers** – bearings, track numbers and depths are said in one breath, like a real net; the video starts with before/after
- **The right reason** – a ship firing Harpoons out of range said "sonar contact only"; now "target outside envelope", like the game's status bar
- **Fewer repeats** – one "survivors in the water" per sinking instead of one per raft, one call per raid instead of one per aircraft
**Coming next – your side's language:** voice packs, then **Русский** (Soviet navy and naval aviation), then **Español** for the upcoming South Atlantic 1982 update (Argentina, Chile, Cuba, Spain), later 中文 / 日本語 – each as its own Workshop item.
**Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
Thanks for the feedback – keep it coming!
```

### Discord – MicroProse (Official), the same mods / showcase channel as the launch post

```
Rats Radio update (1.0.2) 🔊 https://youtu.be/pGwOIPXckpY
Bearings and track numbers are now said in one breath like on a real net, ships give the right reason when they can't fire, and survivors and raids are called once instead of per raft or per aircraft. Coming next: the radio in your side's language – Russian first, then Spanish for the upcoming South Atlantic update. On the Workshop – subscribers get it automatically: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
```

---

## 1. YouTube – description for the video (already uploaded)

Paste into the video's description (YouTube Studio → Details):

```
Rats Radio – a Sea Power mod that makes the radio net sound like people instead of a screen reader, and makes your units tell you why they won't do what you ordered. Mission: Dangerous Straits 1985 – Yak-38s closing in on the Iowa surface action group.

• A voice for every unit – 43 voices based on real speakers (US, Canadian, English, Scottish, Irish, Northern Irish, Australian, New Zealand), every ship and every aircraft your carriers can launch
• Callsigns – ships say their own name, aircraft their squadron callsign, the rest a NATO callsign; the same voices again after a reload
• The stock lines re-spoken by the unit that said them, with radio hiss, mic click and squelch
• A real net – urgent calls cut in, replies before reports, stale calls dropped, callsign only when the speaker changes
• Why not? – "target not in rear aspect", "outside launch envelope", "no radar lock", "sonar contact only", "reloading, ready in four minutes", "order dropped"… 20+ reasons
• Order queue – delete single attack orders instead of "cease fire" for all
• Contacts without spam – with bearing, lost only after a while, what identified a hostile (visual, ESM, sonar, radar)
• Every unit and track in a message is clickable; messages stay until spoken
• Fuel calls (joker, bingo, emergency) against the nearest deck that can take the aircraft, RTB aircraft in yellow, search and rescue calls
• Unit roles in the formation manager

All pre-rendered, ships with the mod – no internet needed. Needs Anchor Chain.
Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
Source (MIT): https://github.com/struckl/SeaPowerRadio
Voices: CSTR VCTK Corpus (University of Edinburgh), CC BY 4.0.
```

---

## 2. Reddit – r/SeaPower_NCMA (video post, sound on)

Upload `radio-net.mp4` as the post's video (native video autoplays in the feed; a YouTube link doesn't). No flair
needed (the sub has none). If you'd rather post images: 01-radio-net, 03-message-log, 04-unit-roles, 02-unit-roles-rtb.

**Title**

I made a mod that gives every unit in Sea Power its own voice – and makes them tell you why they won't fire (sound on)

**Body**

The stock radio has two voices per nation and sounds like a screen reader, and when an order silently doesn't happen, nobody tells you why. So I built a radio mod. Everything it does:

**A voice for every unit**

- 43 distinct voices based on real speakers – American, Canadian, English, Scottish, Irish, Northern Irish, Australian and New Zealand accents, men and women
- Every ship, sub, aircraft and helicopter of yours gets its own voice, including every aircraft your carriers and airbases can launch, the moment it takes off
- Ships say their own name ("Iowa"), aircraft their squadron callsign from the game ("Lancer 3-2"), the rest a NATO callsign ("Viper two-one")
- The same voices again when you reload the mission
- Every line is cut from a whole spoken sentence, so callsign, phrase, bearings and track numbers flow like one person talking

**A real radio net**

- All the stock lines – acknowledgements, fox calls, splashes, hits, missile inbound, launches – re-spoken by the unit that actually said them
- Brevity: the callsign only when someone new takes the net; a wingman just says "two, fox three"
- Urgent calls (missile inbound, we're hit, bingo) cut in; replies to your orders go before routine reports and sound louder; a late "copy" is dropped instead of played; a whole flight acknowledging is said at most twice
- Radio sound with carrier hiss, mic click, crackle and squelch tail – and the game's voice volume slider still works

**Why not?**

The game knows every frame why your order isn't happening and never says it. Now your units do: "target not in rear aspect (IR missile)", "outside launch envelope", "inside minimum range", "no radar lock", "sonar contact only", "below the radar horizon", "no line of sight", "too low to launch", "not cleared to engage", "reloading, ready in four minutes", "defending against a missile", and "order dropped – no sensor can see the target" when the game quietly deletes your order. A wingman ordered to VID that stays glued to its lead says so – or, if you want, breaks formation and does it.

**Order queue (F8)**

The selected unit's stacked attack orders, each with its own delete button (the game only has "cease fire" for all of them), and what each one is waiting for.

**Contacts without spam**

- New contacts called by the unit whose sensor found them, with the bearing; several at once become one call
- Lost only after a contact stayed lost for a while; flickering contacts are called "intermittent" once
- Hostile classifications say what identified them: visual, ESM, sonar or radar

**Clickable messages**

Every unit and track number a message mentions takes the camera there – bottom right and in the message log. A call's message stays until it has been spoken, with a marker on the one being spoken.

**Air ops and the formation manager**

- Joker, bingo and fuel emergency against the nearest deck or airfield that can actually take the aircraft – even for aircraft the game gives no home base
- Aircraft heading home turn yellow on the map and in the formation manager
- Search and rescue calls: survivors in the water, pickup complete with souls aboard, cabin full, survivors delivered
- A line under every unit in the formation manager says what it is – Destroyer, Attack Submarine, CAP, Strike, SEAD, ASW Helicopter – in your game language

Everything can be switched off in the config. All voices are pre-rendered and ship with the mod – no internet needed. Needs Anchor Chain.

Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
Video: https://youtu.be/g7P1pg2J0Lk
Source (MIT): https://github.com/struckl/SeaPowerRadio

Voices based on speakers of the CSTR VCTK corpus (University of Edinburgh, CC BY 4.0), rendered with Chatterbox. Feedback welcome – especially lines that sound wrong for the situation.

---

## 3. Steam – Sea Power Modding sub-forum, new thread

https://steamcommunity.com/app/1286220/discussions/3/ → Start a New Discussion

**Title**

[Mod] Rats Radio – a voice for every unit, and units that tell you why they won't fire

**Body**: the Workshop description ([description.bbcode](description.bbcode)) as it is, with two lines added at the
end:

```
[url=https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579][b]Get it on the Steam Workshop[/b][/url]
Bug reports and suggestions welcome here or on the Workshop page – please attach BepInEx\LogOutput.log.
```

---

## 4. Discord – BuOrd: Naval Modding Community, `#sp-mods` (Naval Technicians)

https://discord.gg/vc8uAVH6Sh – attach 01-radio-net.jpg; the YouTube link shows as a playable video. The `< >` around
the GitHub link keeps Discord from adding a third preview box. Under Discord's 2,000-character limit.

```
## Rats Radio – new Sea Power mod 🔊 https://youtu.be/g7P1pg2J0Lk
The radio net sounds like people instead of a screen reader:
- **43 voices** based on real speakers – every ship and every aircraft your carriers can launch gets its own; ships say their name, aircraft their squadron callsign
- **Stock lines replaced** – re-spoken by the unit that said them, with hiss, mic click and squelch
- **A real net** – urgent calls cut in, replies before reports, stale calls dropped, callsign only when the speaker changes
- **Why not?** – units tell you why they won't fire: rear aspect, out of envelope, no radar lock, sonar only, reloading, order dropped… 20+ reasons, plus wingmen that won't VID
- **Order queue (F8)** – delete single attack orders instead of cease fire for all
- **Contacts without spam** – with bearing, what identified a hostile (visual, ESM, sonar, radar)
- **Clickable** – every unit and track in a message jumps the camera there
- **Fuel calls** (joker / bingo / emergency vs the nearest deck that takes you), **RTB in yellow**, **SAR calls**, **unit roles** in the formation manager

All pre-rendered, works offline, every feature switchable. Requires **Anchor Chain**.
**Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
**Source (MIT):** <https://github.com/struckl/SeaPowerRadio>
Feedback welcome!
```

## 5. Discord – MicroProse (Official), Sea Power mods or showcase channel

https://discord.gg/CTDqUUJAVC – a mods / showcase channel if there is one, not the general chat.

```
Made a Sea Power mod for the radio net: **Rats Radio** 🔊 https://youtu.be/g7P1pg2J0Lk
Every unit gets its own voice and callsign (43 voices), the stock lines are re-spoken by the unit that said them, and your units tell you *why* they won't fire – out of envelope, no radar lock, sonar only, reloading. Plus an order queue with single deletes, contacts without spam, clickable messages, fuel and SAR calls. Needs Anchor Chain.
Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3826433579
```

---

Done 2026-10-24: Workshop link here, in the README and the GitHub release.
