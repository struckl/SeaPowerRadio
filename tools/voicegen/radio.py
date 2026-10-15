"""The radio sound, split in two halves.

Ratten Chatter bakes the whole radio chain into every clip. That works for whole lines, but Sea Power Radio
stitches fragments (callsign + phrase + digits) into one transmission, and a mic click or squelch tail baked into
every fragment would click mid-sentence. So the chain is split:

* VOICE half (baked into every fragment, offline, here): trim TTS silence, band-limit to a comms channel, presence
  bump, hard compression (transmitter ALC), soft clipping (grit), re-band-limit, fixed ceiling.
* TRANSMISSION half (once per transmission, at runtime in the plugin's RadioFx): open-carrier hiss that starts a
  beat before the mic click, velvet-noise crackle, mic click, squelch tail, the odd dropout.

`transmission()` below renders the transmission half offline as well, with the same parameters the plugin will
use, so the listening sample sounds exactly like the game will.
"""

from __future__ import annotations

import random
import subprocess
import sys
from pathlib import Path

SAMPLE_RATE = 24000
OUT_SAMPLE_RATE = 22050

# Foreground voices replace the stock callouts, so they sit well above Ratten Chatter's background level (0.28).
VOICE_CEILING = 0.62

VOICE = dict(hp=300, lp=3300, presence=4.0, ratio=5, makeup=4, drive=0.72)

# Transmission flavours, picked per element at runtime: a ship's mast antenna is clean, a jet at range is thin.
TRANSMISSION_PROFILES = {
    "station": dict(hiss=0.040, crackle=0.05, lp=3400, hp=280),
    "near": dict(hiss=0.060, crackle=0.08, lp=3300, hp=300),
    "far": dict(hiss=0.120, crackle=0.18, lp=2600, hp=420),
    "cockpit_hot": dict(hiss=0.070, crackle=0.10, lp=3100, hp=320),
}


def run_ffmpeg(args: list[str], what: str) -> None:
    result = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args],
                            capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"ffmpeg failed on {what}:\n{result.stderr}")


def voice_chain(src: Path, dest: Path) -> None:
    """Bake the voice half into one TTS fragment."""
    v = VOICE
    chain = ",".join([
        "aformat=sample_fmts=fltp:channel_layouts=mono",
        f"aresample={SAMPLE_RATE}",
        # Trim leading and trailing TTS silence; trailing via reverse (stop_* cuts at internal pauses).
        "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.005",
        "areverse",
        "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.01",
        "areverse",
        f"highpass=f={v['hp']}",
        f"lowpass=f={v['lp']}",
        f"equalizer=f=2000:t=q:w=1.3:g={v['presence']}",
        f"acompressor=threshold=-20dB:ratio={v['ratio']}:attack=4:release=90:makeup={v['makeup']}",
        f"asoftclip=type=atan:threshold={v['drive']}:oversample=4",
        f"highpass=f={v['hp']}",
        f"lowpass=f={v['lp']}",
        f"alimiter=limit={VOICE_CEILING}:level=disabled",
        f"aresample={OUT_SAMPLE_RATE}",
    ])
    run_ffmpeg(["-i", str(src), "-af", chain, "-c:a", "libmp3lame", "-q:a", "4",
                "-ar", str(OUT_SAMPLE_RATE), "-ac", "1", str(dest)], src.name)


def transmission(parts: list[tuple[Path, float]], dest: Path, profile: str, seed: str) -> None:
    """Stitch fragments into one transmission and add the transmission half.

    parts: (clip, gap_before_seconds). The first gap is ignored.
    """
    rng = random.Random(seed)
    p = TRANSMISSION_PROFILES[profile]
    inputs: list[str] = []
    for clip, _ in parts:
        inputs += ["-i", str(clip)]

    # Body: fragments with short natural gaps between them.
    labels = []
    graph = []
    for i, (_, gap) in enumerate(parts):
        lead = 0 if i == 0 else int(gap * 1000)
        graph.append(f"[{i}:a]aresample={SAMPLE_RATE},adelay={lead}:all=1[p{i}]")
        labels.append(f"[p{i}]")
    graph.append(f"{''.join(labels)}concat=n={len(parts)}:v=0:a=1[speech]")

    click_dur = round(rng.uniform(0.022, 0.045), 3)
    click_level = round(rng.uniform(0.14, 0.26), 3)
    tail_dur = round(rng.uniform(0.09, 0.18), 3)
    tail_level = round(rng.uniform(0.18, 0.32), 3)
    hang = round(rng.uniform(0.05, 0.14), 3)
    preroll_ms = rng.randint(80, 200)
    density = round(rng.uniform(0.02, 0.05) * (1.6 if p["hiss"] > 0.1 else 1.0), 4)
    seeds = [rng.randrange(1, 2**31) for _ in range(4)]

    graph += [
        f"[speech]lowpass=f={p['lp']},highpass=f={p['hp']},apad=pad_dur={hang}[voice]",
        f"anoisesrc=c=white:r={SAMPLE_RATE}:a=1:d={click_dur}:s={seeds[0]},highpass=f=700,lowpass=f=3200,"
        f"afade=t=out:st=0:d={click_dur}:curve=exp,volume={click_level}[click]",
        f"anoisesrc=c=white:r={SAMPLE_RATE}:a=1:d={tail_dur}:s={seeds[1]},highpass=f=500,lowpass=f=3400,"
        f"afade=t=out:st=0.015:d={round(tail_dur - 0.015, 3)}:curve=log,volume={tail_level}[tail]",
        f"[click][voice][tail]concat=n=3:v=0:a=1,adelay={preroll_ms}:all=1[body]",
        f"anoisesrc=c=white:r={SAMPLE_RATE}:a=1:d=60:s={seeds[2]},highpass=f=400,lowpass=f=2600,"
        f"volume={p['hiss']}[hiss]",
        f"anoisesrc=c=velvet:density={density}:r={SAMPLE_RATE}:a=1:d=60:s={seeds[3]},highpass=f=600,"
        f"lowpass=f=3200,volume={p['crackle']}[crackle]",
        f"[body][hiss][crackle]amix=inputs=3:duration=first:normalize=0,"
        f"alimiter=limit=0.8:level=disabled,aresample={OUT_SAMPLE_RATE}[out]",
    ]
    run_ffmpeg([*inputs, "-filter_complex", ";".join(graph), "-map", "[out]",
                "-c:a", "libmp3lame", "-q:a", "4", "-ar", str(OUT_SAMPLE_RATE), "-ac", "1", str(dest)],
               dest.name)


def silence(dest: Path, seconds: float) -> None:
    run_ffmpeg(["-f", "lavfi", "-i", f"anullsrc=r={OUT_SAMPLE_RATE}:cl=mono", "-t", str(seconds),
                "-c:a", "libmp3lame", "-q:a", "4", str(dest)], dest.name)


def concat_files(files: list[Path], dest: Path) -> None:
    lst = dest.with_suffix(".txt")
    lst.write_text("".join(f"file '{f.as_posix()}'\n" for f in files), encoding="utf-8")
    run_ffmpeg(["-f", "concat", "-safe", "0", "-i", str(lst), "-c:a", "libmp3lame", "-q:a", "4",
                "-ar", str(OUT_SAMPLE_RATE), "-ac", "1", str(dest)], dest.name)
    lst.unlink()
