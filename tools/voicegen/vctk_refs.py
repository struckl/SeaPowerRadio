"""Build Chatterbox reference clips from the CSTR VCTK Corpus (CC BY 4.0).

For every persona in cast.py: read one row group of the speaker's mic1 recordings from the Hugging Face parquet
mirror (jspaulsen/vctk -- silence-trimmed VCTK 0.92) over HTTP range requests, so only ~20 MB per speaker are
downloaded instead of the 17.6 GB corpus. Utterances are joined with short pauses until the clip is ~12 s, then
loudness-normalized. Output: tools/voicegen/refs/<persona>.wav (24 kHz mono).

vctk/index.json maps speaker -> "shard:rowgroup:mic" -> row count (built once by scanning the speaker_id column).
vctk/speaker-info.txt is the corpus's own speaker list (age, gender, accent, region).

Usage (Chatterbox venv):
    .venv-chatterbox/Scripts/python tools/voicegen/vctk_refs.py [--force] [--voices us_m_01,...]
"""

from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

import numpy as np
import pyarrow.parquet as pq
import soundfile as sf
from huggingface_hub import HfFileSystem

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cast import BY_ID, CAST  # noqa: E402
from radio import run_ffmpeg  # noqa: E402

HERE = Path(__file__).resolve().parent
REFS = HERE / "refs"
INDEX = json.loads((HERE / "vctk" / "index.json").read_text(encoding="utf-8"))
SHARD = "datasets/jspaulsen/vctk/data/train-{:05d}-of-00034.parquet"
TARGET_SECONDS = 12.0
MIN_UTTERANCE = 1.5
GAP = 0.25


def row_group_for(speaker: str) -> tuple[int, int]:
    """The row group holding the most mic1 recordings of this speaker."""
    groups = [(n, k) for k, n in INDEX[speaker].items() if k.endswith(":mic1")] or \
             [(n, k) for k, n in INDEX[speaker].items()]
    _, key = max(groups)
    shard, rg, _ = key.split(":")
    return int(shard), int(rg)


def utterances(fs: HfFileSystem, speaker: str) -> list[tuple[np.ndarray, int]]:
    shard, rg = row_group_for(speaker)
    with fs.open(SHARD.format(shard), "rb", block_size=1 << 20) as f:
        table = pq.ParquetFile(f).read_row_group(rg, columns=["id", "speaker_id", "mic_id", "audio"])
    rows = [r for r in table.to_pylist() if r["speaker_id"] == speaker]
    mic1 = [r for r in rows if r["mic_id"] == "mic1"] or rows
    out = []
    for r in sorted(mic1, key=lambda r: r["id"]):
        data, sr = sf.read(io.BytesIO(r["audio"]["bytes"]), dtype="float32", always_2d=False)
        if data.ndim > 1:
            data = data.mean(axis=1)
        out.append((data, sr))
    return out


def build(fs: HfFileSystem, persona_id: str) -> None:
    persona = BY_ID[persona_id]
    clips = utterances(fs, persona.vctk)
    sr = clips[0][1]
    gap = np.zeros(int(GAP * sr), dtype=np.float32)
    parts, total = [], 0.0
    for data, rate in clips:
        if rate != sr or len(data) / rate < MIN_UTTERANCE:
            continue
        parts += [data, gap]
        total += len(data) / rate + GAP
        if total >= TARGET_SECONDS:
            break
    raw = REFS / f"{persona_id}_raw.wav"
    sf.write(str(raw), np.concatenate(parts), sr)
    # EBU R128 loudness so every reference drives the cloner at the same level.
    run_ffmpeg(["-i", str(raw), "-af", "loudnorm=I=-20:TP=-2:LRA=11", "-ac", "1", "-ar", "24000",
                str(REFS / f"{persona_id}.wav")], raw.name)
    raw.unlink()
    print(f"{persona_id:8} {persona.vctk}  {total:5.1f}s  ({persona.note})", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", help="comma-separated persona ids (default: all)")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    ids = args.voices.split(",") if args.voices else [p.id for p in CAST]
    REFS.mkdir(exist_ok=True)
    fs = HfFileSystem()
    for pid in ids:
        if args.force or not (REFS / f"{pid}.wav").exists():
            build(fs, pid)


if __name__ == "__main__":
    main()
