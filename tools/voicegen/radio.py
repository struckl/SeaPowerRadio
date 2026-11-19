"""The radio sound, split in two halves.

Ratten Chatter bakes the whole radio chain into every clip. That works for whole lines, but Sea Power Radio
stitches fragments (callsign + phrase + digits) into one transmission, and a mic click or squelch tail baked into
every fragment would click mid-sentence. So the chain is split:

* VOICE half (baked into every fragment, offline, here): trim TTS silence, band-limit to a comms channel, presence
  bump, hard compression (transmitter ALC), soft clipping (grit), re-band-limit, fixed ceiling.
* TRANSMISSION half (once per transmission, at runtime in the plugin's RadioFx): open-carrier hiss that starts a
  beat before the mic click, velvet-noise crackle, mic click, squelch tail, the odd dropout.

`transmission()` below renders the transmission half offline as well, step for step like the plugin's RadioFx and
from the same table (rules.py: profiles, joins, loudness gain, soft limiter), so a listening sample sounds like the
game. Only the random numbers (click, tail, noise) come from another generator.
"""

from __future__ import annotations

import math
import random
import subprocess
import sys
from array import array
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from rules import DEFAULT_PROFILE, FX, PROFILES  # noqa: E402

SAMPLE_RATE = 24000
OUT_SAMPLE_RATE = 22050

# Foreground voices replace the stock callouts, so they sit well above Ratten Chatter's background level (0.28).
VOICE_CEILING = 0.62

VOICE = dict(hp=300, lp=3300, presence=4.0, ratio=5, makeup=4, drive=0.72)



def run_ffmpeg(args: list[str], what: str) -> None:
    result = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args],
                            capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit(f"ffmpeg failed on {what}:\n{result.stderr}")


def voice_chain(src: Path, dest: Path, start: float | None = None, end: float | None = None,
                trim_start: bool = True, trim_end: bool = True) -> None:
    """Bake the voice half into one clip cut from a take.

    start/end: slice of the take in seconds (None = from the beginning / to the end). A side that was cut in the
    middle of a pause keeps its half of the pause -- that is what makes stitched clips glue back together with the
    voice's own timing -- so silence is only trimmed on the take's real outer edges.
    """
    v = VOICE
    pre = ["aformat=sample_fmts=fltp:channel_layouts=mono", f"aresample={SAMPLE_RATE}"]
    if start is not None or end is not None:
        rng = []
        if start is not None:
            rng.append(f"start={start:.4f}")
        if end is not None:
            rng.append(f"end={end:.4f}")
        pre += [f"atrim={':'.join(rng)}", "asetpts=PTS-STARTPTS"]
        # A few ms of fade where we cut, so a cut inside running speech never clicks.
        if start is not None and start > 0:
            pre.append("afade=t=in:st=0:d=0.006")
        if end is not None:
            pre += ["areverse", "afade=t=in:st=0:d=0.006", "areverse"]
    if trim_start:
        pre.append("silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.005")
    if trim_end:
        # Trailing via reverse: silenceremove's stop_* mode cuts at the first internal pause.
        pre += ["areverse", "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.01", "areverse"]
    chain = ",".join(pre + [
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
    # Write next to the target and rename, so a render killed mid-write never leaves a truncated clip that a
    # resumed run would mistake for a finished one.
    part = dest.with_name(dest.stem + ".part.mp3")
    run_ffmpeg(["-i", str(src), "-af", chain, "-c:a", "libmp3lame", "-q:a", "4",
                "-ar", str(OUT_SAMPLE_RATE), "-ac", "1", str(part)], src.name)
    part.replace(dest)


def decode(clip: Path) -> array:
    """A clip as mono float samples at OUT_SAMPLE_RATE, the way the game loads it."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(clip), "-f", "f32le", "-ac", "1",
                          "-ar", str(OUT_SAMPLE_RATE), "-"], capture_output=True, check=True).stdout
    x = array("f")
    x.frombytes(raw)
    return x


def rms(x, start: int, count: int) -> float:
    if count <= 0:
        return 0.0
    return math.sqrt(sum(s * s for s in x[start:start + count]) / count)


def trailing_silence(x, rate: int) -> int:
    """Silence at the end of a clip in samples: whole frames below the speech level (RadioFx.TrailingSilence)."""
    frame = max(1, int(FX["frame"] * rate))
    for f in range(len(x) // frame):
        if rms(x, len(x) - (f + 1) * frame, frame) >= FX["sound_rms"]:
            return f * frame
    return 0


def leading_silence(x, rate: int) -> int:
    """Silence at the start: everything before the frames above the onset level that run into the first frame of
    speech, so the weak "s" of "seven" stays and a breath before it goes (RadioFx.LeadingSilence)."""
    frame = max(1, int(FX["frame"] * rate))
    frames, f = len(x) // frame, 0
    while f < frames and rms(x, f * frame, frame) < FX["sound_rms"]:
        f += 1
    if f == frames:
        return 0
    while f > 0 and rms(x, (f - 1) * frame, frame) >= FX["onset_rms"]:
        f -= 1
    return f * frame


def append_joined(voice: list[float], nxt, rate: int) -> None:
    """Butt nxt on, or cross-fade it in where both sides of the seam are loud (RadioFx.AppendJoined)."""
    edge = int(FX["edge"] * rate)
    fade = min(int(FX["crossfade"] * rate), min(len(voice), len(nxt)) // 3)
    if fade < 8 or len(voice) < edge or len(nxt) < edge:
        voice.extend(nxt)
        return
    half = rate // 2
    hot_end = rms(voice, len(voice) - edge, edge) > FX["hot_edge_ratio"] * rms(
        voice, max(0, len(voice) - half), min(len(voice), half))
    hot_start = rms(nxt, 0, edge) > FX["hot_edge_ratio"] * rms(nxt, 0, min(len(nxt), half))
    if not hot_end and not hot_start:
        voice.extend(nxt)
        return
    start = len(voice) - fade
    for i in range(fade):
        t = (i + 0.5) / fade
        voice[start + i] = voice[start + i] * math.cos(t * math.pi * 0.5) + nxt[i] * math.sin(t * math.pi * 0.5)
    voice.extend(nxt[fade:])


def join_voice(parts: list[tuple[Path, float]], rate: int) -> list[float]:
    """Clips back to back, the silence at each join cut back to what it allows (RadioFx.JoinVoice).

    parts: (clip, most silence in seconds where it joins the one before -- the plugin's TransmissionBuilder.MaxPause;
    the first is ignored; float("inf") = as cut).
    """
    voice: list[float] = []
    trailing = 0
    for i, (clip, max_pause) in enumerate(parts):
        data = decode(clip)
        end_quiet = trailing_silence(data, rate)
        if i == 0:
            voice.extend(data)
        else:
            keep = int(min(max_pause, 10.0) * rate)
            leading = leading_silence(data, rate)
            if trailing + leading > keep:
                keep_end = min(trailing, keep // 2)
                keep_start = min(leading, keep - keep_end)
                keep_end = min(trailing, keep - keep_start)
                del voice[len(voice) - (trailing - keep_end):]
                data = data[leading - keep_start:]
                end_quiet = min(end_quiet, len(data))
            append_joined(voice, data, rate)
        trailing = end_quiet
    return voice


def band_limit(x: list[float], rate: int, hp: float, lp: float) -> None:
    """Two-pole high-pass and low-pass (cascaded one-poles), in place (RadioFx.BandLimit)."""
    a_lp = 1 - math.exp(-2 * math.pi * lp / rate)
    a_hp = 1 - math.exp(-2 * math.pi * hp / rate)
    l1 = l2 = h1 = h2 = 0.0
    for i, s in enumerate(x):
        l1 += a_lp * (s - l1)
        l2 += a_lp * (l1 - l2)
        h1 += a_hp * (l2 - h1)
        y = l2 - h1
        h2 += a_hp * (y - h2)
        x[i] = y - h2


def between(rng: random.Random, name: str) -> float:
    return rng.uniform(FX[name + "_min"], FX[name + "_max"])


def noise(rng: random.Random, length: int, level: float) -> list[float]:
    return [(rng.random() * 2 - 1) * level for _ in range(length)]


def burst(rng: random.Random, rate: int, name: str, exponential: bool) -> list[float]:
    """Mic click or squelch tail: band-limited noise under a falling envelope (RadioFx.Burst)."""
    seconds, level = between(rng, name + "_seconds"), between(rng, name + "_level")
    count = max(1, int(seconds * rate))
    b = noise(rng, count, 1.0)
    band_limit(b, rate, FX[name + "_hp"], FX[name + "_lp"])
    for i in range(count):
        x = i / count
        b[i] *= (math.exp(-5 * x) if exponential else 1 - math.log10(1 + 9 * x)) * level
    return b


def speech_rms(speech: list[float]) -> float:
    """RMS over the voiced part only (pauses between words would drag the average down)."""
    voiced = [s * s for s in speech if abs(s) >= FX["speech_gate"]]
    return max(1e-4, math.sqrt(sum(voiced) / len(voiced))) if voiced else FX["target_speech_rms"]


def soft_limit(x: float) -> float:
    knee = FX["limit_knee"]
    a = abs(x)
    if a <= knee:
        return x
    room = FX["limit_ceiling"] - knee
    y = knee + room * math.tanh((a - knee) / room)
    return -y if x < 0 else y


def transmission(parts: list[tuple[Path, float]], dest: Path, profile: str, seed: str,
                 noise_scale: float = 1.0, level: float = 1.0) -> None:
    """Stitch fragments into one transmission and add the transmission half, step for step like the plugin's
    RadioFx.Build with the same table (rules.py). Only the random numbers differ (another generator).

    parts: see join_voice. noise_scale and level: the plugin's radio static setting and call level (1 = default).
    """
    rng = random.Random(seed)
    p = PROFILES.get(profile, PROFILES[DEFAULT_PROFILE])
    rate = OUT_SAMPLE_RATE
    speech = join_voice(parts, rate)
    band_limit(speech, rate, p["hp"], p["lp"])

    preroll = int(rate * between(rng, "preroll"))
    click = burst(rng, rate, "click", exponential=True)
    hang = int(rate * between(rng, "hang"))
    tail = burst(rng, rate, "tail", exponential=False)
    mix = [0.0] * preroll + click + speech + [0.0] * hang + tail

    hiss = noise(rng, len(mix), p["hiss"] * noise_scale)
    band_limit(hiss, rate, FX["hiss_hp"], FX["hiss_lp"])
    weak = FX["weak_signal_density"] if p["hiss"] > FX["weak_signal_hiss"] else 1
    density = between(rng, "crackle_density") * weak
    crackle = [((1 if rng.random() < 0.5 else -1) * p["crackle"] * noise_scale if rng.random() < density else 0.0)
               for _ in range(len(mix))]
    band_limit(crackle, rate, FX["crackle_hp"], FX["crackle_lp"])

    gain = min(max(FX["target_speech_rms"] * level / speech_rms(speech), FX["gain_min"]), FX["gain_max"])
    out = array("f", (soft_limit((m + h + c) * gain) for m, h, c in zip(mix, hiss, crackle)))
    result = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "f32le", "-ar", str(rate),
                             "-ac", "1", "-i", "-", "-c:a", "libmp3lame", "-q:a", "4", str(dest)],
                            input=out.tobytes(), capture_output=True)
    if result.returncode != 0:
        sys.exit(f"ffmpeg failed on {dest.name}:\n{result.stderr.decode(errors='replace')}")


def silence(dest: Path, seconds: float) -> None:
    run_ffmpeg(["-f", "lavfi", "-i", f"anullsrc=r={OUT_SAMPLE_RATE}:cl=mono", "-t", str(seconds),
                "-c:a", "libmp3lame", "-q:a", "4", str(dest)], dest.name)


def concat_files(files: list[Path], dest: Path) -> None:
    lst = dest.with_suffix(".txt")
    lst.write_text("".join(f"file '{f.as_posix()}'\n" for f in files), encoding="utf-8")
    run_ffmpeg(["-f", "concat", "-safe", "0", "-i", str(lst), "-c:a", "libmp3lame", "-q:a", "4",
                "-ar", str(OUT_SAMPLE_RATE), "-ac", "1", str(dest)], dest.name)
    lst.unlink()
