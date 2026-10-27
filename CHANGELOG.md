# Changelog

## 1.0.3 – 2026-10-27

- Refusals no longer repeat while the ship closes the range: a unit gives the same reason about the same target
  once (again after 5 minutes at the earliest) instead of every 40 seconds, and a formation ordered to attack
  together gets one call – the first ship to give a reason says it, the others only put up their message.
- Speed orders say the right way and where to: 1/3 → 2/3 said "slowing down". Now "coming up to two thirds",
  "slowing to one third", "coming up to standard" …, "ahead full" and "ahead flank" as before. A speed set in knots
  with the slider, which had no voice line at all, is said too: "coming to one five knots" / "making one five knots".
- Altitude orders too: a descent said "climbing, angels five" half the time. Now "climbing, …" below the new
  altitude, "descending, …" above it, "leveling off, …" at the altitude it already holds – always with the ordered
  altitude ("angels twenty", "five hundred feet"). Depth orders already name the depth.
- New lines in all 43 voices: the six telegraph steps above, "descending, …" and the two knots lines.
- No more clipped first sounds: "stand by, reloading …", "steady on …" and "splash one" lost their "s" in about
  half the voices ("'tand by") – the clip was cut in the short silence between the s and the t / p. Those lines are
  cut again in every voice.
- Launch calls fit the weapon: a Tomahawk at a land target said "torpedo away" (the game's line for a ship's
  missile at a land target was taken for an anti-submarine missile), at a position "harpoon away". Now "harpoon
  away" only for Harpoons, "anti-ship away" for other missiles at a ship, "birds away" / "missile away" for land
  attack, and "torpedo away" / "fish in the water" for torpedoes and ASROC (they said "weapon away").

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
