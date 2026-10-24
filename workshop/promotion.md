# Rats Radio – release and promotion

Workshop: *(link after the upload)*
Source: https://github.com/struckl/SeaPowerRadio · Release zip (manual install): GitHub release v1.0.0 (draft until
published)
Images: `workshop/screenshots/` – 01-radio-net, 02-unit-roles-rtb, 03-message-log, 04-unit-roles
Video: `workshop/radio-net.mp4` – 70 s, 1080p, 9.2 MB, the radio net while Yak-38s close in on the Iowa group (not in git)

---

## 0. Steam Workshop upload (Main menu → Mods → Create Mod)

The game must be closed for `tools\package.ps1 -Install`; the 1.0.0 folder is already installed (2026-10-24).

| Field | Value |
| --- | --- |
| Folder | `StreamingAssets\SeaPowerRadio` – double-click into it before confirming |
| Name | Sea Power: Rats Radio |
| Description | [description.bbcode](description.bbcode) |
| Preview image | `StreamingAssets\SeaPowerRadio\preview.jpg` |
| Tags | Audio, Scripting |
| Dependencies | `3380210757` (Anchor Chain) |
| Visibility | Hidden first, Public once the subscribed copy works |
| Change log | First release: a voice and callsign for every unit, the stock voice lines re-spoken by the unit that said them, units that tell you why they won't fire, contact reports without spam, fuel and SAR calls, unit roles in the formation manager. |

The first upload takes a while (~390 MB, ~57,000 files). Afterwards, on the Workshop page (Owner Controls → Add/edit
images & videos): the YouTube video first (section 1), then the screenshots in the order 01, 02, 03, 04.

---

## 1. YouTube – the sound sample

Upload `workshop/radio-net.mp4` (Unlisted is enough for the Workshop and Reddit; Public if you want it found).

**Title**

Rats Radio – a voice for every unit in Sea Power (mod, sound on)

**Description**

Every unit gets its own voice and callsign, the game's stock lines are re-spoken by the unit that said them, and units tell you why they won't fire. 43 voices, all pre-rendered – nothing generated while you play.
Mission: Dangerous Straits 1985 – Yak-38s closing in on the Iowa surface action group.
Workshop: (link)
Source: https://github.com/struckl/SeaPowerRadio

---

## 2. Reddit – r/SeaPower_NCMA (video post, sound on)

Post `radio-net.mp4` as a video – the mod is all about sound, so a video beats a gallery here. If you'd rather post
images: 01-radio-net, 03-message-log, 04-unit-roles, 02-unit-roles-rtb. No flair needed (the sub has none).

**Title**

I made a mod that gives every unit in Sea Power its own voice – and makes them tell you why they won't fire (sound on)

**Body**

The stock radio has two voices per nation and sounds like a screen reader, and when an order silently doesn't happen, nobody tells you why. So I built a radio mod:

- **A voice for every unit** – every ship and aircraft of yours, including what your carriers and airbases can launch, gets its own voice and a NATO callsign ("Viper two-one", "Sentinel"). 43 distinct voices, the same ones again when you reload
- **Stock lines replaced** – re-spoken by the unit that actually said them, through a radio filter with hiss, clicks and squelch
- **Why not?** – "Unable, target outside launch envelope", "Unable, holding formation on lead", reload times, sonar-only contacts
- **No contact spam** – lost / regained / intermittent contacts with sensible delays
- **Everything clickable** – every unit and track number a message mentions jumps the camera there
- **Fuel calls** (joker / bingo against the nearest deck that can take the aircraft), **search and rescue calls**, RTB aircraft in yellow, and each unit's role in the formation manager

All voices are pre-rendered – nothing is generated while you play, no internet needed. Needs Anchor Chain.

Workshop: (link)
Source (MIT): https://github.com/struckl/SeaPowerRadio

The voices are cloned from speakers of the CSTR VCTK corpus (University of Edinburgh, CC BY 4.0). Feedback welcome – especially lines that sound wrong for the situation.

---

## 3. Steam – Sea Power Modding sub-forum, new thread

https://steamcommunity.com/app/1286220/discussions/3/ → Start a New Discussion

**Title**

[Mod] Rats Radio – a voice for every unit, and units that tell you why they won't fire

**Body** (Steam forum BBCode)

The radio net sounds like people instead of a screen reader – and your units tell you [b]why[/b] they aren't doing what you ordered.

[list]
[*][b]A voice for every unit[/b] – every ship, aircraft and helicopter of yours, including what your carriers and airbases can launch, gets its own voice and a NATO callsign for the mission. 43 distinct voices, the same ones again when you reload
[*][b]Stock voice lines replaced[/b] – re-spoken by the unit that actually said them, through a radio filter
[*][b]Brevity on the net[/b] – a unit only gives its callsign when it takes over the net; a wingman just says "two, fox three"
[*][b]Why not?[/b] – rear aspect, out of the launch envelope, reloading, sonar-only contacts, holding formation – spoken, on screen and in the log; order queue window (F8)
[*][b]No contact spam[/b] – lost / regained / intermittent contacts with sensible delays; every unit and track a message mentions is clickable
[*][b]Fuel calls, search and rescue calls[/b], RTB aircraft in yellow, unit roles in the formation manager
[/list]

All voices are pre-rendered – nothing is generated while you play, no internet needed. About 390 MB.

Requires [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757]Anchor Chain[/url].

[url=WORKSHOP-LINK]Workshop page[/url] · [url=https://github.com/struckl/SeaPowerRadio]Source code (MIT)[/url]

Voices cloned from speakers of the CSTR VCTK corpus (University of Edinburgh, CC BY 4.0). Bug reports and suggestions welcome here or on the Workshop page – please attach BepInEx\LogOutput.log.

---

## 4. Discord – BuOrd: Naval Modding Community, `#sp-mods` (Naval Technicians)

https://discord.gg/vc8uAVH6Sh – attach `radio-net.mp4` (9.2 MB, under Discord's 10 MB limit) and 01-radio-net.jpg.
The `< >` around the GitHub link keeps Discord from adding a second preview box.

```
## Rats Radio – new Sea Power mod (sound on 🔊)
The radio net sounds like people instead of a screen reader:
- **A voice for every unit** – every ship and aircraft gets its own voice and a NATO callsign, 43 voices, stable per mission
- **Stock lines replaced** – re-spoken by the unit that said them, through a radio filter
- **Why not?** – units tell you why they won't fire: out of the envelope, rear aspect, reloading, sonar-only, holding formation
- **No contact spam** – lost / regained contacts with sensible delays, every unit and track in a message is clickable
- **Fuel calls, SAR calls**, RTB aircraft in yellow, unit roles in the formation manager

All pre-rendered, works offline. Requires **Anchor Chain**.

**Workshop:** WORKSHOP-LINK
**Source (MIT):** <https://github.com/struckl/SeaPowerRadio>
Feedback welcome!
```

## 5. Discord – MicroProse (Official), Sea Power mods or showcase channel

https://discord.gg/CTDqUUJAVC – a mods / showcase channel if there is one, not the general chat. Attach
`radio-net.mp4`.

```
Made a Sea Power mod for the radio net: **Rats Radio** gives every unit its own voice and callsign, re-speaks the stock lines with the unit that said them, and has your units tell you why they won't fire. 43 pre-rendered voices, no contact spam. Sound on 🔊 Needs Anchor Chain.
Workshop: WORKSHOP-LINK
```

---

After the upload: replace `WORKSHOP-LINK` / "(link)" with the Workshop URL here, put it in the README and the
description, and publish the GitHub release.
