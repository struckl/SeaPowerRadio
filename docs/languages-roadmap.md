# Roadmap: other languages

Today every player unit speaks English with a NATO callsign – also when you play the Soviet side or Argentina. The
game itself voices six languages; Rats Radio should match: a unit speaks its nation's language.

The public version (phases and languages, no dates) is the *Roadmap* section of the Workshop page
([workshop/description.bbcode](../workshop/description.bbcode)); the phase numbers here are the same.

## Structure

- **Base mod** (the Workshop item 3826433579): the plugin plus the 43 English voices, as now. Most players fly NATO,
  and existing subscribers lose nothing.
- **One Workshop item per language** (a *voice pack*): only voices and the files that belong to the language. Each
  lists the base mod as a Required Item, like the base lists Anchor Chain – subscribing to a pack offers to subscribe
  to the base too. About 9 MB per voice, ~150–200 MB per pack.
- A pack never needs a plugin update of its own; the base learns once how to find and use packs.

Which nations speak which language, from the game's own `voice.ini` `[VoiceProfiles]`:

| Pack | Nations | Stock voice today |
| --- | --- | --- |
| English (in the base) | US, UK, Australia, New Zealand, everyone without a pack | us_male_1/2, uk_male_1, au_male_1 |
| Russian | Soviet | wp_male_1 |
| Spanish | Argentina, Chile, Cuba, Spain | ar_male_1 |
| Chinese | China | cn_male_1 |
| Japanese | Japan | ja_male_1 |

## Phase 1 – base mod 1.1.0: voice packs (≈ 2 days, no new voices)

- Packs found in every mod folder the game scans (Workshop items and `StreamingAssets/<folder>`), by a
  `voicepack.ini`: language, nations, pack version, lowest base version it needs.
- A pack brings its personas, `catalog.txt` (same phrase keys as English, so every feature works unchanged), names
  and its rules for numbers and callsigns (Soviet altitudes in metres and numeric callsigns; Spanish "rumbo cero
  nueve cero", Argentine flight callsigns).
- The player side's nation picks the language; no pack installed → English, exactly as now.
- `package.ps1 -Pack <language>` stages a pack; `tools/workshop-upload` already uploads to any item id.
- Released as a normal Workshop update. English-only players notice nothing; the in-game test is the 1.0.x checklist.

## Phase 2 – Russian (≈ 3 days of work + 1 day of unattended rendering)

The first pack builds the pipeline every later one reuses:

1. **Phrases:** the ~190 phrases (2–3 variants each) and the number and callsign pieces in Soviet naval and naval
   aviation radio language, reviewed by a native speaker.
2. **Speakers:** reference recordings with a free licence for 15–20 voices (VCTK is English only) – Mozilla Common
   Voice (CC0); licence and credits checked before rendering.
3. **Pipeline:** Chatterbox's multilingual model (MIT; covers Russian, Spanish, Chinese, Japanese) instead of the
   English one, and a Russian word-timing (CTC) model in `align.py` – the cutting code stays.
4. **Pilot:** three voices first, listening reel; only then the full render (~1 h per voice on the current GPU,
   game-aware, overnight).
5. **Check:** `check_library.py`, then a mission played as the Soviet side.
6. **Release:** new Workshop item "Sea Power: Rats Radio – Русский", base as Required Item; zip on the GitHub
   release; posts and a short video.

## Phase 3 – Spanish (≈ 2 days + 1 day rendering)

The pipeline from phase 2 is reused; the work is phrases, review, speakers (Common Voice has accent labels, so
Argentine speakers can be picked; Multilingual LibriSpeech, CC BY 4.0, as a second source) and a test as Argentina.
Promoted with the game's *Royal Navy, Argentina, South Atlantic 1982* campaigns, which the main menu announces for
11 November.

## Phase 4 – Chinese and Japanese (later)

Fewer players fly them, and both need more care: word timing without spaces between words, and reviewers who know
period naval radio language. After the first two packs show how many people subscribe.

## Calendar (decided 2026-10-27: Russian first, then Spanish)

| When | What |
| --- | --- |
| 28–30 Oct | Phase 1: base 1.1.0 with voice packs – released with English only |
| 30 Oct – 6 Nov | Phase 2: Russian phrases and review, speakers, pipeline, three-voice pilot, full render, test |
| ~7 Nov | Russian pack released |
| from ~8 Nov | Phase 3: Spanish (~1 week) – lands around the South Atlantic update (11 Nov) |

## Decisions

1. **Order** – Russian, then Spanish (decided 2026-10-27). English stays in the base.
2. **Reviewers:** a native speaker per language – from the Discord communities, or someone you know?
3. **Voices per pack:** 15–20, men only for both navies and air forces of the period?

## Risks

- The multilingual model's quality per language is unknown → the three-voice pilot comes before the full render.
- The nation names the game passes (`Soviet`, `Argentina`, …) for player units have to be confirmed in-game in
  phase 1.
- Grammar: numbers agree with nouns in Russian and Spanish; radio style reads numbers digit by digit, which avoids
  most of it.
