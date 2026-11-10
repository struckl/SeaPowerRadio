# Squadron voices (male or female per squadron)

Text file only, no setting in the game's menu: `<Sea Power>\BepInEx\config\SeaPowerRadio\squadrons.txt`. It is created
with commented examples at the first mission after the update; Workshop updates never touch it. Read at every mission
start, so an edit applies from the next mission.

```
# squadron name = male | female
Bear Aces = female        # the E-2C squadron: all its crews speak with women's voices
Ghostriders = male        # every pilot of the F-4 squadron speaks with a man's voice
VF-11* = male             # '*' is a wildcard: every squadron name that starts with VF-11
```

- **Which name?** Any name the squadron goes by: the name the game shows for the air wing, its short name, or its callsign
  (`Viper = female` covers a squadron whose aircraft launch as "Viper 1", "Viper 2" ...). The mission file's key
  ("Squadron1") works too. The log (`BepInEx\LogOutput.log`) lists every hangar slot at mission start as
  `[hangar <carrier> / <names> #1]` with all of these, and a squadron with a rule is marked `(squadron: f)` or `(squadron: m)`.
- **Matching.** Without a `*` the rule matches every squadron with a name that *contains* it; case and punctuation are ignored.
  With a `*` it is a wildcard over the whole name (`VF-11*` starts with, `*Aces` ends with, `*Ghost*` contains).
- **Several lines match.** The last one in the file wins.
- **Beats the "Female voices" setting** for that squadron: a `female` squadron speaks with women even while the setting is
  off, a `male` squadron with men even while it is on. Every other unit follows the setting as before.
- **Accents still apply.** The voice is chosen among the voices of that gender as usual (nation's accents first, unused
  voices first).
- **Callsign words.** A squadron callsign ("Lancer 3") only exists in a few rendered voices. If none of them has the gender you
  asked for, the callsign wins and that aircraft keeps a voice of the other gender.
- **Only squadrons of a carrier or airbase air wing.** Single ships and aircraft that do not come from an air wing (air
  support, trigger-enabled units) are not covered; pin a ship to one voice with `aliases.txt` ([name-aliases.md](name-aliases.md)).
- **Campaigns.** A voice a linear campaign remembers is kept only if it fits the rule; otherwise it is chosen anew.
