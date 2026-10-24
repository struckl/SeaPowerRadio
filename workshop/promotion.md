# Rats Radio – release and promotion

Workshop: *(link after the upload)*
Video: https://youtu.be/g7P1pg2J0Lk (70 s, the radio net while Yak-38s close in on the Iowa group – `workshop/radio-net.mp4`, not in git)
Source: https://github.com/struckl/SeaPowerRadio · Release zip (manual install): GitHub release v1.0.0 (draft until
published)
Images: `workshop/screenshots/` – 01-radio-net, 02-unit-roles-rtb, 03-message-log, 04-unit-roles

---

## 0. Steam Workshop upload (Main menu → Mods → Create Mod)

The game must be closed for `tools\package.ps1 -Install`; the 1.0.0 folder is already installed (2026-10-24).

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerRadio` – double-click into it before confirming |
| Name | Sea Power: Rats Radio |
| Description | [description.bbcode](description.bbcode) (6.6k of Steam's 8k characters) |
| Preview image | `StreamingAssets\SeaPowerRadio\preview.jpg` |
| Tags | Audio, Scripting |
| Dependencies | `3380210757` (Anchor Chain) |
| Visibility | Hidden first, Public once the subscribed copy works |
| Change log | First release: a voice and callsign for every unit, the stock voice lines re-spoken by the unit that said them, units that tell you why they won't fire, an order queue with single deletes, contact reports without spam, clickable messages, fuel, RTB and SAR calls, unit roles in the formation manager. |

The first upload takes a while (~390 MB, ~57,000 files). Afterwards, on the Workshop page (Owner Controls → Add/edit
images & videos): **the YouTube video first** (https://youtu.be/g7P1pg2J0Lk), then the screenshots 01, 02, 03, 04.

---

## 1. YouTube – description for the video (already uploaded)

Paste into the video's description (YouTube Studio → Details):

```
Rats Radio – a Sea Power mod that makes the radio net sound like people instead of a screen reader, and makes your units tell you why they won't do what you ordered. Mission: Dangerous Straits 1985 – Yak-38s closing in on the Iowa surface action group.

• A voice for every unit – 43 people, cloned from real speakers (US, Canadian, English, Scottish, Irish, Northern Irish, Australian, New Zealand), every ship and every aircraft your carriers can launch
• Callsigns – ships say their own name, aircraft their squadron callsign, the rest a NATO callsign; the same voices again after a reload
• The stock lines re-spoken by the unit that said them, with radio hiss, mic click and squelch
• A real net – urgent calls cut in, replies before reports, stale calls dropped, callsign only when the speaker changes
• Why not? – "target not in rear aspect", "outside launch envelope", "no radar lock", "sonar contact only", "reloading, ready in four minutes", "order dropped"… 20+ reasons
• Order queue – delete single attack orders instead of "cease fire" for all
• Contacts without spam – with bearing, lost only after a while, what identified a hostile (visual, ESM, sonar, radar)
• Every unit and track in a message is clickable; messages stay until spoken
• Fuel calls (joker, bingo, emergency) against the nearest deck that can take the aircraft, RTB aircraft in yellow, search and rescue calls
• Unit roles in the formation manager

All pre-rendered – no internet, nothing generated while you play. Needs Anchor Chain.
Workshop: WORKSHOP-LINK
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

- 43 distinct people, cloned from real speakers – American, Canadian, English, Scottish, Irish, Northern Irish, Australian and New Zealand accents, men and women
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

Everything can be switched off in the config. All voices are pre-rendered – nothing is generated while you play, no internet. Needs Anchor Chain.

Workshop: WORKSHOP-LINK
Video: https://youtu.be/g7P1pg2J0Lk
Source (MIT): https://github.com/struckl/SeaPowerRadio

Voices cloned from speakers of the CSTR VCTK corpus (University of Edinburgh, CC BY 4.0). Feedback welcome – especially lines that sound wrong for the situation.

---

## 3. Steam – Sea Power Modding sub-forum, new thread

https://steamcommunity.com/app/1286220/discussions/3/ → Start a New Discussion

**Title**

[Mod] Rats Radio – a voice for every unit, and units that tell you why they won't fire

**Body**: the Workshop description ([description.bbcode](description.bbcode)) as it is, with two lines added at the
end:

```
[url=WORKSHOP-LINK][b]Get it on the Steam Workshop[/b][/url]
Bug reports and suggestions welcome here or on the Workshop page – please attach BepInEx\LogOutput.log.
```

---

## 4. Discord – BuOrd: Naval Modding Community, `#sp-mods` (Naval Technicians)

https://discord.gg/vc8uAVH6Sh – attach 01-radio-net.jpg; the YouTube link shows as a playable video. The `< >` around
the GitHub link keeps Discord from adding a third preview box. Under Discord's 2,000-character limit.

```
## Rats Radio – new Sea Power mod 🔊 https://youtu.be/g7P1pg2J0Lk
The radio net sounds like people instead of a screen reader:
- **43 voices** cloned from real speakers – every ship and every aircraft your carriers can launch gets its own; ships say their name, aircraft their squadron callsign
- **Stock lines replaced** – re-spoken by the unit that said them, with hiss, mic click and squelch
- **A real net** – urgent calls cut in, replies before reports, stale calls dropped, callsign only when the speaker changes
- **Why not?** – units tell you why they won't fire: rear aspect, out of envelope, no radar lock, sonar only, reloading, order dropped… 20+ reasons, plus wingmen that won't VID
- **Order queue (F8)** – delete single attack orders instead of cease fire for all
- **Contacts without spam** – with bearing, what identified a hostile (visual, ESM, sonar, radar)
- **Clickable** – every unit and track in a message jumps the camera there
- **Fuel calls** (joker / bingo / emergency vs the nearest deck that takes you), **RTB in yellow**, **SAR calls**, **unit roles** in the formation manager

All pre-rendered, works offline, every feature switchable. Requires **Anchor Chain**.
**Workshop:** WORKSHOP-LINK
**Source (MIT):** <https://github.com/struckl/SeaPowerRadio>
Feedback welcome!
```

## 5. Discord – MicroProse (Official), Sea Power mods or showcase channel

https://discord.gg/CTDqUUJAVC – a mods / showcase channel if there is one, not the general chat.

```
Made a Sea Power mod for the radio net: **Rats Radio** 🔊 https://youtu.be/g7P1pg2J0Lk
Every unit gets its own voice and callsign (43 voices), the stock lines are re-spoken by the unit that said them, and your units tell you *why* they won't fire – out of envelope, no radar lock, sonar only, reloading. Plus an order queue with single deletes, contacts without spam, clickable messages, fuel and SAR calls. Needs Anchor Chain.
Workshop: WORKSHOP-LINK
```

---

After the upload: replace `WORKSHOP-LINK` with the Workshop URL here, put it in the README and the description, and
publish the GitHub release.
