"""Render the voice library: personas (cast.py) x takes (phrases.py).

Each take is one edge-tts request with word boundaries switched on. The clips it holds are cut out at the middle of
the gap between the last word of one segment and the first word of the next, then put through the voice half of
the radio chain (radio.py). Output: mod/voices/<persona>/<key>.mp3.

Existing clips are kept, so an interrupted render resumes and an edited phrase only re-renders that phrase
(--force re-renders everything selected). edge-tts is Microsoft's online read-aloud service; the full library is
tens of thousands of requests, so they run with a small concurrency limit -- be nice to it.

Usage:
    python tools/voicegen/generate.py --sample --voices us_m_01,gb_m_01,us_f_01
    python tools/voicegen/generate.py --voices all          # full render
    python tools/voicegen/generate.py --voices all --only WhyNot
"""

from __future__ import annotations

import argparse
import asyncio
import random
import re
import shutil
import sys
import tempfile
from pathlib import Path

import edge_tts

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import BY_ID, CAST, REPO, Persona  # noqa: E402
from phrases import TAKES, Take  # noqa: E402
from radio import voice_chain  # noqa: E402

OUT_ROOT = REPO / "mod" / "voices"
CONCURRENCY = 4
TICKS = 1e7          # edge-tts offsets are in 100 ns units
DECODE_SHIFT = 0.03  # decoded MP3 runs ~30 ms behind the reported offsets (measured)


def jitter(persona: Persona, rng: random.Random, amount: int) -> tuple[str, str]:
    """Per-take prosody wobble -- the single biggest thing separating a person from a screen reader."""
    rate = int(persona.rate.rstrip("%")) + rng.randint(-amount, amount)
    pitch = int(persona.pitch.rstrip("Hz")) + rng.randint(-amount, amount)
    return f"{rate:+d}%", f"{pitch:+d}Hz"


def word_count(text: str) -> int:
    return len([w for w in re.split(r"\s+", text.strip()) if re.search(r"\w", w)])


async def synthesize(persona: Persona, tk: Take, raw: Path) -> list[tuple[float, float]] | None:
    """Speak the whole take; returns (start, end) seconds per word."""
    rng = random.Random(f"{persona.id}|{tk.text}")
    rate, pitch = jitter(persona, rng, tk.jitter)
    for attempt in range(4):
        try:
            words: list[tuple[float, float]] = []
            audio = bytearray()
            comm = edge_tts.Communicate(tk.text, voice=persona.voice, rate=rate, pitch=pitch,
                                        boundary="WordBoundary")
            async for msg in comm.stream():
                if msg["type"] == "audio":
                    audio += msg["data"]
                elif msg["type"] == "WordBoundary":
                    start = msg["offset"] / TICKS
                    words.append((start, start + msg["duration"] / TICKS))
            raw.write_bytes(audio)
            return words
        except Exception as ex:  # network hiccups: back off and retry
            print(f"  ! {persona.id} '{tk.text[:40]}' attempt {attempt + 1}: {type(ex).__name__}: {ex}"[:200],
                  flush=True)
            await asyncio.sleep(2 * (attempt + 1))
    return None


def cut_points(tk: Take, words: list[tuple[float, float]]) -> list[tuple[float | None, float | None]] | None:
    """(start, end) per segment, cut in the middle of the gap between segments. None if words don't line up."""
    counts = [word_count(s.text) for s in tk.segments]
    if sum(counts) != len(words):
        return None
    bounds: list[tuple[float | None, float | None]] = []
    i = 0
    for n, count in enumerate(counts):
        first, last = i, i + count - 1
        start = None if n == 0 else (words[first - 1][1] + words[first][0]) / 2 + DECODE_SHIFT
        end = None if n == len(counts) - 1 else (words[last][1] + words[last + 1][0]) / 2 + DECODE_SHIFT
        bounds.append((start, end))
        i += count
    return bounds


async def render_take(persona: Persona, tk: Take, out_dir: Path, tmp: Path, force: bool,
                      sem: asyncio.Semaphore) -> int:
    wanted = [s for s in tk.segments if s.key and (force or not (out_dir / f"{s.key}.mp3").exists())]
    if not wanted:
        return 0
    raw = tmp / f"{persona.id}_{abs(hash(tk.text))}.mp3"
    async with sem:
        words = await synthesize(persona, tk, raw)
    if words is None:
        return 0
    bounds = cut_points(tk, words)
    if bounds is None:
        print(f"  ! {persona.id}: word boundaries don't match '{tk.text}' ({len(words)} words reported)")
        return 0
    done = 0
    last = len(tk.segments) - 1
    for n, (seg, (start, end)) in enumerate(zip(tk.segments, bounds)):
        if seg not in wanted:
            continue
        await asyncio.to_thread(voice_chain, raw, out_dir / f"{seg.key}.mp3", start, end,
                                n == 0, n == last)
        done += 1
    raw.unlink(missing_ok=True)
    return done


async def build(personas: list[Persona], sample: bool, only: str | None, force: bool) -> None:
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg not found on PATH.")
    takes = [t for t in TAKES if (not sample or t.sample)]
    if only:
        takes = [t for t in takes if any(only.lower() in k.lower() for k in t.keys)]
    sem = asyncio.Semaphore(CONCURRENCY)
    with tempfile.TemporaryDirectory() as tmpdir:
        tmp = Path(tmpdir)
        for persona in personas:
            out_dir = OUT_ROOT / persona.id
            out_dir.mkdir(parents=True, exist_ok=True)
            results = await asyncio.gather(*(render_take(persona, t, out_dir, tmp, force, sem) for t in takes))
            print(f"{persona.id:8} {persona.voice:34} {sum(results):5} clips rendered from {len(takes)} takes",
                  flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True, help="comma-separated persona ids, or 'all'")
    parser.add_argument("--sample", action="store_true", help="only takes marked sample=True")
    parser.add_argument("--only", help="only takes holding a clip key containing this text")
    parser.add_argument("--force", action="store_true", help="re-render existing clips")
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
