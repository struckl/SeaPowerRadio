"""Render the voice library with Chatterbox (Resemble AI, MIT) on the local GPU.

Personas (cast.py) x takes (phrases.py) -> mod/voices/<persona>/<key>.mp3, same cuts and radio chain as the
edge-tts fallback (generate.py):

* Each persona is cloned from its VCTK reference clip (refs/<persona>.wav, built by vctk_refs.py).
* Delivery follows the phrase's urgency: priority 1-2 ("missile launch, defending!") gets high `exaggeration` and
  low `cfg_weight` (more drama, steadier pacing), confirmations stay calm.
* Chatterbox reports no word boundaries, so every take is aligned with a CTC forced aligner (align.py,
  transcript-constrained) and cut in the middle of the gap between segments.
* A take that fails alignment or runs far longer than its text (a sampling hallucination) is re-rolled with a new
  seed, up to MAX_TRIES times.

Resumable: existing clips are skipped. Every Chatterbox output carries Resemble's inaudible Perth watermark.

Own venv (torch + CUDA):
    python -m venv .venv-chatterbox
    .venv-chatterbox/Scripts/python -m pip install "torch>=2.9" "torchaudio>=2.9" --index-url https://download.pytorch.org/whl/cu128
    .venv-chatterbox/Scripts/python -m pip install chatterbox-tts==0.1.7 pyarrow
    .venv-chatterbox/Scripts/python tools/voicegen/vctk_refs.py
    .venv-chatterbox/Scripts/python tools/voicegen/chatterbox_render.py --voices all
    .venv-chatterbox/Scripts/python tools/voicegen/chatterbox_render.py --sample --voices us_m_01,gb_m_02,us_f_01
"""

from __future__ import annotations

import argparse
import sys
import time
import zlib
from pathlib import Path

import soundfile as sf
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import BY_ID, CAST, REPO  # noqa: E402
from align import ForcedAligner, normalize_words  # noqa: E402
from phrases import TAKES, Take  # noqa: E402
from radio import voice_chain  # noqa: E402

OUT_ROOT = REPO / "mod" / "voices"
REFS = Path(__file__).resolve().parent / "refs"
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

MAX_TRIES = 3
SECONDS_PER_WORD_MAX = 0.9  # a take longer than this per word (+1 s) is treated as a hallucination

def delivery(tk: Take) -> tuple[float, float]:
    """(exaggeration, cfg_weight) from the urgency of the phrase in this take."""
    if tk.priority <= 2:
        return 0.85, 0.30
    if tk.priority == 3:
        return 0.70, 0.35
    if tk.priority == 4:
        return 0.60, 0.40
    if tk.priority == 7:
        return 0.45, 0.50
    return 0.50, 0.50


def cut_points(tk: Take, words: list[tuple[float, float]]) -> list[tuple[float | None, float | None]]:
    counts = [len(normalize_words(s.text)) for s in tk.segments]
    assert sum(counts) == len(words), (tk.text, counts, len(words))
    bounds = []
    i = 0
    for n, count in enumerate(counts):
        first, last = i, i + count - 1
        start = None if n == 0 else (words[first - 1][1] + words[first][0]) / 2
        end = None if n == len(counts) - 1 else (words[last][1] + words[last + 1][0]) / 2
        bounds.append((start, end))
        i += count
    return bounds


def render_take(model, aligner: ForcedAligner, persona_id: str, tk: Take, out_dir: Path, raw: Path) -> int:
    exaggeration, cfg = delivery(tk)
    max_seconds = SECONDS_PER_WORD_MAX * len(normalize_words(tk.text)) + 1.0
    for attempt in range(MAX_TRIES):
        torch.manual_seed(zlib.crc32(f"{persona_id}|{tk.text}|{attempt}".encode()))
        wav = model.generate(tk.text, exaggeration=exaggeration, cfg_weight=cfg).cpu()
        seconds = wav.shape[-1] / model.sr
        if seconds > max_seconds:
            print(f"  ~ {persona_id}: {seconds:.1f}s for '{tk.text}' (max {max_seconds:.1f}), re-rolling", flush=True)
            continue
        try:
            bounds = cut_points(tk, aligner.word_times(wav, model.sr, tk.text))
        except (AssertionError, ValueError, KeyError) as ex:
            print(f"  ~ {persona_id}: alignment failed for '{tk.text}' ({ex}), re-rolling", flush=True)
            continue
        sf.write(str(raw), wav.squeeze(0).numpy(), model.sr)
        done = 0
        last = len(tk.segments) - 1
        for n, (seg, (start, end)) in enumerate(zip(tk.segments, bounds)):
            if seg.key:
                voice_chain(raw, out_dir / f"{seg.key}.mp3", start, end, n == 0, n == last)
                done += 1
        raw.unlink()
        return done
    print(f"  ! {persona_id}: gave up on '{tk.text}'", flush=True)
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True, help="comma-separated persona ids, or 'all'")
    parser.add_argument("--sample", action="store_true", help="only takes marked sample=True")
    parser.add_argument("--force", action="store_true", help="re-render existing clips")
    args = parser.parse_args()

    from chatterbox.tts import ChatterboxTTS

    personas = CAST if args.voices == "all" else [BY_ID[v] for v in args.voices.split(",")]
    missing = [p.id for p in personas if not (REFS / f"{p.id}.wav").exists()]
    if missing:
        sys.exit(f"no reference clip for {', '.join(missing)} -- run vctk_refs.py first")

    print(f"loading Chatterbox on {DEVICE} ...", flush=True)
    model = ChatterboxTTS.from_pretrained(device=DEVICE)
    aligner = ForcedAligner(DEVICE)
    takes = [t for t in TAKES if not args.sample or t.sample]

    tmp = REPO / "tools" / "voicegen" / "out"
    tmp.mkdir(parents=True, exist_ok=True)
    for p in personas:
        out_dir = OUT_ROOT / p.id
        out_dir.mkdir(parents=True, exist_ok=True)
        todo = [t for t in takes if args.force or not all((out_dir / f"{k}.mp3").exists() for k in t.keys)]
        if not todo:
            continue
        model.prepare_conditionals(str(REFS / f"{p.id}.wav"))
        started, done = time.time(), 0
        for i, tk in enumerate(todo, start=1):
            done += render_take(model, aligner, p.id, tk, out_dir, tmp / f"{p.id}_take.wav")
            if i % 50 == 0:
                rate = (time.time() - started) / i
                print(f"  {p.id}: {i}/{len(todo)} takes, {rate:.1f}s/take", flush=True)
        print(f"{p.id:8} {done} clips in {time.time() - started:.0f}s", flush=True)


if __name__ == "__main__":
    main()
