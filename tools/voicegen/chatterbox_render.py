"""Render takes with Chatterbox (Resemble AI, MIT) on the local GPU -- the A/B alternative to edge-tts.

Same takes, same cuts, same radio chain as generate.py; only the engine differs:

* Voices are cloned from a ~12 s reference clip per persona (refs/<persona>.wav). For the A/B the references are
  the edge-tts voices of v2b, so timbre stays comparable and only naturalness differs.
* Delivery follows the phrase's urgency: priority 1-2 ("missile launch, defending!") gets high `exaggeration` and
  low `cfg_weight` (more drama, steadier pacing), confirmations stay calm.
* Chatterbox reports no word boundaries, so takes are aligned afterwards with a CTC forced aligner (align.py,
  transcript-constrained) and cut in the middle of the gap between segments, exactly like generate.py.

Every Chatterbox output carries Resemble's inaudible Perth watermark.

Runs in its own venv (torch + CUDA):
    python -m venv .venv-chatterbox
    .venv-chatterbox/Scripts/python -m pip install "torch>=2.9" "torchaudio>=2.9" --index-url https://download.pytorch.org/whl/cu128
    .venv-chatterbox/Scripts/python -m pip install chatterbox-tts==0.1.7 edge-tts==7.3.1
    .venv-chatterbox/Scripts/python tools/voicegen/chatterbox_render.py --sample --voices us_m_03,us_m_07,us_f_03
Output: mod/voices/cb_<persona>/ -- reel.py --voices cb_us_m_03,cb_us_m_07,cb_us_f_03 builds the scenario.
"""

from __future__ import annotations

import argparse
import asyncio
import re
import sys
import zlib
from pathlib import Path

import soundfile as sf
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import BY_ID, REPO, Persona  # noqa: E402
from align import ForcedAligner, normalize_words  # noqa: E402
from phrases import PHRASES, TAKES, Take  # noqa: E402
from radio import run_ffmpeg, voice_chain  # noqa: E402

OUT_ROOT = REPO / "mod" / "voices"
REFS = Path(__file__).resolve().parent / "refs"
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

# Spoken in the persona's edge-tts voice to give Chatterbox something to clone: radio register, numbers, a
# callsign, ~12 seconds.
REFERENCE_TEXT = (
    "Alright, copy that. We're about ten miles out, heading two seven zero, angels twenty. "
    "Two-one has the lead, I'll call it when we're in range. Fuel's fine for now, no problems here."
)

PRIORITY = {p.key: p.priority for p in PHRASES}


def delivery(tk: Take) -> tuple[float, float]:
    """(exaggeration, cfg_weight) from the urgency of the phrase in this take."""
    prio = min((PRIORITY.get(re.sub(r"_\d+$", "", k), 6) for k in tk.keys), default=6)
    if prio <= 2:
        return 0.85, 0.30
    if prio == 3:
        return 0.70, 0.35
    if prio == 4:
        return 0.60, 0.40
    if prio == 7:
        return 0.45, 0.50
    return 0.50, 0.50


async def make_reference(persona: Persona, dest: Path) -> None:
    import edge_tts
    tmp = dest.with_suffix(".mp3")
    await edge_tts.Communicate(REFERENCE_TEXT, voice=persona.voice, rate=persona.rate, pitch=persona.pitch).save(str(tmp))
    run_ffmpeg(["-i", str(tmp), "-ac", "1", "-ar", "24000", str(dest)], tmp.name)
    tmp.unlink()


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


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True)
    parser.add_argument("--sample", action="store_true")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    from chatterbox.tts import ChatterboxTTS

    personas = [BY_ID[v] for v in args.voices.split(",")]
    REFS.mkdir(exist_ok=True)
    for p in personas:
        ref = REFS / f"{p.id}.wav"
        if not ref.exists():
            asyncio.run(make_reference(p, ref))

    print(f"loading Chatterbox on {DEVICE} ...", flush=True)
    model = ChatterboxTTS.from_pretrained(device=DEVICE)
    aligner = ForcedAligner(DEVICE)
    takes = [t for t in TAKES if not args.sample or t.sample]

    tmp = REPO / "tools" / "voicegen" / "out"
    tmp.mkdir(parents=True, exist_ok=True)
    for p in personas:
        out_dir = OUT_ROOT / f"cb_{p.id}"
        out_dir.mkdir(parents=True, exist_ok=True)
        done = 0
        for i, tk in enumerate(takes):
            if not args.force and all((out_dir / f"{k}.mp3").exists() for k in tk.keys):
                continue
            exaggeration, cfg = delivery(tk)
            torch.manual_seed(zlib.crc32(f"{p.id}|{tk.text}".encode()))
            wav = model.generate(tk.text, audio_prompt_path=str(REFS / f"{p.id}.wav"),
                                 exaggeration=exaggeration, cfg_weight=cfg)
            raw = tmp / f"cb_{p.id}_{i}.wav"
            sf.write(str(raw), wav.squeeze(0).cpu().numpy(), model.sr)
            try:
                bounds = cut_points(tk, aligner.word_times(wav.cpu(), model.sr, tk.text))
            except (AssertionError, ValueError, KeyError) as ex:
                print(f"  ! {p.id}: alignment mismatch {ex}")
                continue
            last = len(tk.segments) - 1
            for n, (seg, (start, end)) in enumerate(zip(tk.segments, bounds)):
                if seg.key:
                    voice_chain(raw, out_dir / f"{seg.key}.mp3", start, end, n == 0, n == last)
                    done += 1
            raw.unlink()
        print(f"cb_{p.id:8} {done} clips", flush=True)


if __name__ == "__main__":
    main()
