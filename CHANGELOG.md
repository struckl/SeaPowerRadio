# Changelog

## 1.0.2 – 2026-10-25

- Refusals: a ship ordered to fire a fixed-launcher missile (Harpoon) out of range said "negative, sonar contact
  only" – the order waits while the ship turns, and the mod didn't see the game's own reason there. It now says
  "unable, target outside envelope", like the game's status bar. "Sonar contact only" is only said when sonar is the
  only sensor holding the contact (the group's own passive sonar hearing a ship that radar and ESM also hold made it
  look like a sonar contact).
- No more repeated calls: a sinking ship's rafts, put out one by one, give one "survivors in the water" instead of
  one per raft; a raid found aircraft by aircraft gives one "new air contact" and one "multiple contacts" instead of
  "new air contact, bearing 327" for every aircraft. The messages bottom right still list every raft and track.

## 1.0.1 – 2026-10-25

- No more long pauses before numbers: "bearing two seven three", "track seven zero three four" run together the
  way they are said. The silence at every join between clips is cut back – to 0.05 s between a word and its number,
  0.08 s between digit groups; pauses the sentence has (after the callsign, "track 7034, faded") stay, up to 0.25 s.
  Same voices, no re-render; `catalog.txt` gained two columns (does the text pause before / after the number).

## 1.0.0 – 2026-10-24 (first Workshop release)

Tested on Sea Power 0.8.7.

- 43 pre-rendered voices (based on speakers of the VCTK corpus, rendered with Chatterbox) through a radio chain;
  every player unit and every aircraft a carrier or airbase can launch gets its own voice and a NATO callsign, stable
  per mission.
- The game's stock voice lines muted and re-spoken by the unit that said them; brevity on the net (callsign only
  when the speaker changes).
- Why-not diagnostics: units say why they don't fire or follow an order; order queue window (F8).
- Contact reports without spam (hysteresis, cooldowns, flicker detection), track numbers clickable; every unit and
  track a message mentions is a link to it.
- Fuel calls (joker / bingo / emergency against the nearest field that takes the aircraft), RTB aircraft yellow on
  the map and in the formation manager, search and rescue calls.
- Unit roles under every unit in the formation manager, in the game's own words and language.
