"""Render the voice library: personas (cast.py) x phrases and fragments (phrases.py).

Output: mod/voices/<persona>/<key>.mp3 for fragments, <key>_<n>.mp3 for phrase variants. Existing files are kept,
so an interrupted render resumes where it stopped and editing one phrase only re-renders that phrase (--force
re-renders everything selected).

edge-tts is Microsoft's online read-aloud service. The full library is tens of thousands of requests, so requests
run with a small concurrency limit and a pause between batches -- be nice to it.

Usage:
    python tools/voicegen/generate.py --sample --voices us_m_01,gb_m_01,us_f_01
    python tools/voicegen/generate.py --voices all          # full render
    python tools/voicegen/generate.py --only WhyNot         # keys containing "WhyNot"
"""

from __future__ import annotations

import argparse
import asyncio
import random
import shutil
import sys
import tempfile
from pathlib import Path

import edge_tts

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import BY_ID, CAST, REPO, Persona  # noqa: E402
from phrases import FRAGMENTS, PHRASES  # noqa: E402
from radio import voice_chain  # noqa: E402

OUT_ROOT = REPO / "mod" / "voices"
CONCURRENCY = 4


def jitter(persona: Persona, rng: random.Random, amount: int) -> tuple[str, str]:
    """Per-take prosody wobble -- the single biggest thing separating a person from a screen reader."""
    rate = int(persona.rate.rstrip("%")) + rng.randint(-amount, amount)
    pitch = int(persona.pitch.rstrip("Hz")) + rng.randint(-amount, amount)
    return f"{rate:+d}%", f"{pitch:+d}Hz"


def jobs_for(persona: Persona, sample: bool, only: str | None) -> list[tuple[str, str, int]]:
    """(output stem, text, jitter amount). Fragments get less jitter so stitched digits stay coherent."""
    jobs: list[tuple[str, str, int]] = []
    for phrase in PHRASES:
        if sample and not phrase.sample:
            continue
        for n, text in enumerate(phrase.variants, start=1):
            jobs.append((f"{phrase.key}_{n}", text, 4))
    for frag in FRAGMENTS:
        if sample and not frag.sample:
            continue
        jobs.append((frag.key, frag.text, 2))
    if only:
        jobs = [j for j in jobs if only.lower() in j[0].lower()]
    return jobs


async def render_one(persona: Persona, stem: str, text: str, amount: int, out_dir: Path, tmp: Path,
                     force: bool, sem: asyncio.Semaphore) -> bool:
    dest = out_dir / f"{stem}.mp3"
    if dest.exists() and not force:
        return False
    rng = random.Random(f"{persona.id}|{stem}")
    rate, pitch = jitter(persona, rng, amount)
    raw = tmp / f"{persona.id}_{stem}.mp3"
    async with sem:
        for attempt in range(4):
            try:
                await edge_tts.Communicate(text, voice=persona.voice, rate=rate, pitch=pitch).save(str(raw))
                break
            except Exception as ex:  # network hiccups: back off and retry
                print(f"  ! {persona.id}/{stem} attempt {attempt + 1}: {type(ex).__name__}: {ex}"[:200], flush=True)
                if attempt == 3:
                    return False
                await asyncio.sleep(2 * (attempt + 1))
    await asyncio.to_thread(voice_chain, raw, dest)
    raw.unlink(missing_ok=True)
    return True


async def build(personas: list[Persona], sample: bool, only: str | None, force: bool) -> None:
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg not found on PATH.")
    sem = asyncio.Semaphore(CONCURRENCY)
    with tempfile.TemporaryDirectory() as tmpdir:
        tmp = Path(tmpdir)
        for persona in personas:
            out_dir = OUT_ROOT / persona.id
            out_dir.mkdir(parents=True, exist_ok=True)
            jobs = jobs_for(persona, sample, only)
            results = await asyncio.gather(*(
                render_one(persona, stem, text, amount, out_dir, tmp, force, sem) for stem, text, amount in jobs))
            print(f"{persona.id:8} {persona.voice:34} {sum(results):4} rendered, {len(jobs) - sum(results):4} kept")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True, help="comma-separated persona ids, or 'all'")
    parser.add_argument("--sample", action="store_true", help="only phrases/fragments marked sample=True")
    parser.add_argument("--only", help="only keys containing this text")
    parser.add_argument("--force", action="store_true", help="re-render existing files")
    args = parser.parse_args()

    if args.voices == "all":
        personas = CAST
    else:
        unknown = [v for v in args.voices.split(",") if v not in BY_ID]
        if unknown:
            sys.exit(f"unknown persona(s): {', '.join(unknown)}")
        personas = [BY_ID[v] for v in args.voices.split(",")]
    asyncio.run(build(personas, args.sample, args.only, args.force))


if __name__ == "__main__":
    main()
