"""Does every clip say its text? Transcribe the library with a speech recognizer and flag the clips that don't.

check_library.py finds clips that are missing; this finds clips that are there but wrong -- a cut that took the first
sound off ("'tand by" for "stand by" in 22 voices from 1.0.0 to 1.0.2, "peed" for "speed"), a take where the model
mumbled or said something else -- without listening to 26,000 takes.

Every clip is heard inside a call, joined the way the plugin joins it (reel.py say() / number()): a recognizer
without a language model guesses at a short word alone ("knots" -> "nots"), in a sentence it gets it right.

    phrase     Viper | two-one, | coming to, | one, | five zero | knots.     SetSpeedKnots_1_a, SetSpeedKnots_1_b
    number     Viper | two-one, | new air contact, bearing, | two seven three.          brg_273
    callsign   Lancer | two-one, | copy.                                                cs_lancer
    name       Pargo, | copy.                    nm_pargo (only with --names: the recognizer can't spell most names)

Per clip: is its first word heard right (see matches()), and the character error rate (CER) of its own stretch,
letters aligned without spaces so "zerazira one" still lines up with "zero zero one". A sound heard in a joint counts
against the clip after it. Per key: in how many voices the first word was right. The recognizer misses a first word
now and then, more in some accents, so one voice says little; across the 43 English voices phrase keys reach a median
of 41 (climbing 42, descending 39, making 41), and a key below --min-right (70 %: 30/43) is a defect of its text or the
cutter for every voice: the old "speed {num} knots" was right in 17/43 (the cut took the "s"), the flight positions
after an air callsign word in a median 16/43 -- "Viper six-two, ready" has no pause to cut in, so "Viper" kept the
"t" of "two" from its own take and "six" lost its "s". Such a defect needs a fix in phrases.py / chatterbox_render.py
before its takes are rendered again; a re-render alone brings it back. Clips that open their take (callsign words,
names) were never cut at the start, so their first word never fails a key.

Single clips that came out garbled -- a word said twice, the next word of the take cut in -- stand out against the
same key in the other voices (CER --cer-margin above its median) and are listed worst first with the voice id;
--write-keys saves the listed keys for chatterbox_render.py --keys-file, which needs a new --seed-offset (the same
seed renders the same take again).

Recognizers, greedy CTC decoding (a language model would correct exactly the errors this looks for), picked by the
voice's language in <library>/personas.txt; --model overrides:
    en  facebook/wav2vec2-base-960h                    the aligner's model (align.py)
    ru  jonatasgrosman/wav2vec2-large-xlsr-53-russian  Common Voice test CER 2.9 %
    es  jonatasgrosman/wav2vec2-large-xlsr-53-spanish  Common Voice test CER 2.6 %
The expected text is the English catalog (phrases.py) plus names.txt; a language pack's catalog plugs in at
clip_texts(). Start a new language with an empty SOUNDS_LIKE and add the words its recognizer mishears in every voice.

Exit code 0 = every key at or above --min-right, 1 = a key below it. The whole English library (43 voices, 46,000
clips) takes ~18 min on the GPU, ~6 min per voice on the CPU.

    .venv-chatterbox/Scripts/python tools/voicegen/check_asr.py --voices all --all
    .venv-chatterbox/Scripts/python tools/voicegen/check_asr.py --voices all --keys-file tools/voicegen/rerender_keys.txt
    .venv-chatterbox/Scripts/python tools/voicegen/check_asr.py --voices us_m_01,gb_f_02 --keys SetSpeedKnots_1_a,brg_273
"""

from __future__ import annotations

import argparse
import statistics
import sys
import time
import unicodedata
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path

import soundfile as sf
import torch
import torchaudio

sys.path.insert(0, str(Path(__file__).resolve().parent))

import reel  # noqa: E402
from cast import REPO  # noqa: E402
from names import spoken  # noqa: E402
from phrases import AIR_WORDS, PHRASES, SLOT, TAKES  # noqa: E402

MODELS = {
    "en": "facebook/wav2vec2-base-960h",
    "ru": "jonatasgrosman/wav2vec2-large-xlsr-53-russian",
    "es": "jonatasgrosman/wav2vec2-large-xlsr-53-spanish",
}
# Words the recognizer hears as something else in every voice although the clip is fine (letters only, no spaces).
SOUNDS_LIKE: dict[str, dict[str, set[str]]] = {
    "en": {
        "knots": {"not", "nots", "notes", "knot", "nod", "nods", "note"},
        "two": {"to", "too", "tu"},
        "four": {"for", "fore", "foor"},
        "cruise": {"crew", "cruw", "cru"},
        "max": {"mack", "mac", "mak"},
        "cease": {"seize", "seeze"},
        "radar": {"rateour", "radeour", "ratear", "rayedour", "radour", "radeor", "rateou", "ratour", "raidour",
                  "rateor"},
        "in": {"im"},
        "up": {"op", "ob", "ub"},
        "say": {"sa", "se"},
        "rtb": {"rtib", "artb", "atb"},
        "esm": {"esa", "esn", "esam", "ersm", "yesan", "yesim"},
    },
}
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
PAD = 0.1  # s of silence around a call, room for a word at the very edge
MIN_LETTERS = 6  # a single clip is judged by its CER only from this many letters on

# Clips heard around the one under test (left out where a voice lacks them).
CALLSIGN = ["cs_viper", "fp_21"]
ACK = "Confirmed_1"  # "copy."
SAMPLE_VALUES = {"trk": "7034", "brg": "273", "num": "150", "alt": "20000", "count": "3"}  # as in reel.py
NUMBER_CALLS = {  # piece prefix -> clips before it, clips after it
    "brg_": (["NewContactAir_1_a"], []),                    # new air contact, bearing, | two seven three.
    "pm_": (["ContactFaded_1_a"], ["pe_34"]),               # lost contact, track, | seven zero, | three four.
    "pe_": (["ContactFaded_1_a", "pm_70"], []),
    "dm_": (["MakeDepth_1_a"], ["pe_50", "MakeDepth_1_b"]),  # make my depth, | one, | five zero | feet.
    "alt_lo_": (["SetAltitude_3_a"], []),                   # descending, | five hundred feet.
    "alt_": (["SetAltitude_1_a"], []),                      # climbing, | angels twenty.
    "cnt_": (["ContactsFaded_1_a"], []),                    # contacts lost, | three.
}
AIR = {w.lower() for w in AIR_WORDS}
PHRASE = {p.key: p for p in PHRASES}
CATALOG = {s.key: s.text for t in TAKES for s in t.segments if s.key}
# Clips that open their take were never cut at the start, so their first word can't fail a key (CER still counts):
# the callsign words, and the names.
TAKE_START = {t.segments[0].key for t in TAKES if t.segments[0].key}


# ----------------------------------------------------------------------------------------------------------------
# What each clip should say, and the call it is heard in
# ----------------------------------------------------------------------------------------------------------------
def name_texts(library: Path) -> dict[str, tuple[str, list[str]]]:
    """names.txt: clip key -> (spoken name, voices bound to it)."""
    path = library / "names.txt"
    out = {}
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if line and not line.startswith("#"):
                key, text, _, voices = line.split("|")
                out[key] = (spoken(text), voices.split(","))
    return out


def clip_texts(voice: str, names: dict[str, tuple[str, list[str]]]) -> dict[str, str]:
    """Clip key -> its words: the catalog, plus the in-game names bound to this voice."""
    texts = dict(CATALOG)
    texts.update({k: text for k, (text, voices) in names.items() if voice in voices})
    return texts


def kind(key: str) -> str:
    if key.startswith(("nm_", "sq_")):
        return "name"
    if key.startswith(tuple(NUMBER_CALLS)):
        return "number"
    if key.startswith(("cs_", "fp_", "cn_")):
        return "callsign"
    return "phrase"


def call_of(voice: str, key: str) -> list[str]:
    """The clips of the call `key` is heard in, in order (some may not exist; the caller drops those)."""
    k = kind(key)
    if k == "phrase":
        variant = key[:-2] if key.endswith(("_a", "_b")) else key
        phrase, _, n = variant.rpartition("_")
        slot = SLOT.search(PHRASE[phrase].variants[int(n) - 1])
        if slot is None:
            return CALLSIGN + [key]
        number = reel.number(voice, slot.group(1), SAMPLE_VALUES[slot.group(1)])
        return CALLSIGN + [f"{variant}_a"] + number + [f"{variant}_b"]
    if k == "number":
        before, after = next(v for prefix, v in NUMBER_CALLS.items() if key.startswith(prefix))
        return CALLSIGN + before + [key] + after
    if key.startswith("cs_"):
        return [key, "fp_21", ACK] if key[3:] in AIR else [key, ACK]
    if key.startswith(("fp_", "cn_")):
        return ["cs_viper", key, ACK]
    return [key, "cn_3", ACK] if key.startswith("sq_") else [key, ACK]


# ----------------------------------------------------------------------------------------------------------------
# Comparing words
# ----------------------------------------------------------------------------------------------------------------
def words(text: str) -> list[str]:
    """Letters only, case and accents folded, hyphens split: "Two-one, I'm" -> two, one, im."""
    text = unicodedata.normalize("NFKD", text.casefold().replace("-", " "))
    return [w for w in ("".join(c for c in t if c.isalpha()) for t in text.split()) if w]


def cer(got: str, want: str) -> float:
    d = list(range(len(want) + 1))
    for i, a in enumerate(got, 1):
        prev, d[0] = d[0], i
        for j, b in enumerate(want, 1):
            prev, d[j] = d[j], min(d[j] + 1, d[j - 1] + 1, prev + (a != b))
    return d[len(want)] / max(1, len(want))


def align(want: str, got: str) -> list[int | None]:
    """Levenshtein alignment of two strings: for every character of `want`, the index of the character of `got` it
    pairs with (None = not heard). On letters without spaces, so "zerazira one" still lines up with "zero zero one"."""
    n, m = len(want), len(got)
    d = [list(range(m + 1))] + [[i] + [0] * m for i in range(1, n + 1)]
    for i in range(1, n + 1):
        a, row, up = want[i - 1], d[i], d[i - 1]
        for j in range(1, m + 1):
            row[j] = min(up[j] + 1, row[j - 1] + 1, up[j - 1] + (a != got[j - 1]))
    pair: list[int | None] = [None] * n
    i, j = n, m
    while i and j:
        if d[i][j] == d[i - 1][j - 1] + (want[i - 1] != got[j - 1]):
            i, j = i - 1, j - 1
            pair[i] = j
        elif d[i][j] == d[i - 1][j] + 1:
            i -= 1
        else:
            j -= 1
    return pair


ONSETS = ("aeiouy", "csz", "ckq", "gj")  # first letters that spell the same sound often enough ("seize" for "cease")


def matches(want: str, heard: str, sounds: dict[str, set[str]]) -> bool:
    """Heard right: exactly, as one of SOUNDS_LIKE, or -- a word of 4+ letters -- spelled differently but starting
    with the same sound and at most half the letters off ("crew" for "cruise", "boy" for "buoy"). A lost first sound
    fails: "tand" for "stand", "ixteen" for "sixteen"; short words need SOUNDS_LIKE ("on" for "one" may be a lost w)."""
    if heard == want or heard in sounds.get(want, ()):
        return True
    same_onset = heard[:1] == want[:1] or any(heard[:1] in c and want[:1] in c for c in ONSETS)
    return len(want) > 3 and bool(heard) and same_onset and cer(heard, want) <= 0.5


@dataclass
class Verdict:
    first_ok: bool
    first: str      # what the clip's first word was heard as
    cer: float      # of the clip's own stretch, spaces ignored
    heard: str      # the clip's own stretch as heard


def judge(clips: list[list[str]], heard: list[str], sounds: dict[str, set[str]]) -> list[Verdict]:
    """One verdict per clip of a call: `clips` holds each clip's expected words, `heard` the call as heard.

    A clip's stretch of what was heard starts right after the previous clip's last letter, so a sound heard in the
    joint counts against the clip after it: "viper | t one one" is a wrong first word for the "one-one"."""
    want = "".join(w for ws in clips for w in ws)
    got = "".join(heard)
    pair = align(want, got)
    word_at = [n for n, w in enumerate(heard) for _ in w]  # letter of `got` -> word of `heard`

    def shown(lo: int, hi: int) -> str:  # got[lo:hi] with the spaces of `heard`
        return "".join(c + (" " if k + 1 < hi and word_at[k + 1] != word_at[k] else "")
                       for k, c in enumerate(got[lo:hi], lo))

    out = []
    a = 0
    done = -1  # last letter of `got` taken by the clips so far
    for n, ws in enumerate(clips):
        b = a + sum(map(len, ws))
        hits = [pair[k] for k in range(a, b) if pair[k] is not None]
        first_hits = [pair[k] for k in range(a, a + len(ws[0]) if ws else a) if pair[k] is not None]
        start = done + 1 if n else (hits[0] if hits else 0)
        end = hits[-1] + 1 if hits else start
        first = got[start:first_hits[-1] + 1] if first_hits else ""
        ok = not ws or (bool(first) and matches(ws[0], first, sounds))
        out.append(Verdict(ok, first, cer(got[start:end], want[a:b]) if ws else 0.0, shown(start, end)))
        done = max(done, end - 1)
        a = b
    return out


# ----------------------------------------------------------------------------------------------------------------
# Hearing
# ----------------------------------------------------------------------------------------------------------------
class Recognizer:
    def __init__(self, model_id: str, device: str) -> None:
        from transformers import Wav2Vec2ForCTC, Wav2Vec2Processor
        from transformers.utils import logging

        logging.set_verbosity_error()  # the load report lists the unused pretraining weights
        logging.disable_progress_bar()
        # Wav2Vec2Processor, not AutoProcessor: the multilingual models ship a KenLM language model that
        # AutoProcessor would decode with, and that would turn "tand" back into "stand".
        self.processor = Wav2Vec2Processor.from_pretrained(model_id)
        self.model = Wav2Vec2ForCTC.from_pretrained(model_id).to(device).eval()
        self.device = device
        self.sr = self.processor.feature_extractor.sampling_rate
        self.pad = torch.zeros(int(PAD * self.sr))

    def __call__(self, audio: torch.Tensor) -> str:
        x = torch.cat([self.pad, audio, self.pad])
        inputs = self.processor(x.numpy(), sampling_rate=self.sr, return_tensors="pt")
        with torch.inference_mode():
            ids = self.model(inputs.input_values.to(self.device)).logits.argmax(-1)[0].cpu()
        return self.processor.decode(ids)


class Clips:
    """One voice's clips, each decoded and resampled once (callsign and carrier clips are in every call)."""

    def __init__(self, folder: Path, sr: int) -> None:
        self.folder, self.sr = folder, sr
        self.cache: dict[str, torch.Tensor | None] = {}

    def get(self, key: str) -> torch.Tensor | None:
        if key not in self.cache:
            path = self.folder / f"{key}.mp3"
            audio = None
            if path.exists():
                data, sr = sf.read(str(path), dtype="float32", always_2d=True)
                audio = torchaudio.functional.resample(torch.from_numpy(data.mean(axis=1)), sr, self.sr)
            self.cache[key] = audio
        return self.cache[key]


@dataclass
class Result:
    voice: str
    key: str
    verdict: Verdict
    want: str
    call: str       # the whole call as heard


def check_voice(voice: str, keys: set[str] | None, library: Path, names: dict, asr: Recognizer,
                sounds: dict[str, set[str]], with_names: bool = False) -> tuple[list[Result], int]:
    """Results for the voice's clips (those in `keys`, or all of them -- names only `with_names`), and how many of
    them are missing."""
    texts = clip_texts(voice, names)
    if keys is None:
        todo = sorted(k for k in texts if with_names or kind(k) != "name")
    else:
        todo = sorted(keys & texts.keys())
    calls: dict[tuple[str, ...], list[str]] = {}
    for key in todo:
        calls.setdefault(tuple(k for k in call_of(voice, key) if k in texts), []).append(key)
    clips = Clips(library / voice, asr.sr)
    results, missing = [], 0
    for call, tested in calls.items():
        parts = [k for k in call if clips.get(k) is not None]
        missing += sum(1 for k in tested if k not in parts)
        tested = [k for k in tested if k in parts]
        if not tested:
            continue
        heard = words(asr(torch.cat([clips.get(k) for k in parts])))
        verdicts = judge([words(texts[k]) for k in parts], heard, sounds)
        for k, v in zip(parts, verdicts):
            if k in tested:
                results.append(Result(voice, k, v, " ".join(words(texts[k])), " ".join(heard)))
    return results, missing


# ----------------------------------------------------------------------------------------------------------------
# Report
# ----------------------------------------------------------------------------------------------------------------
def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--voices", required=True, help="comma-separated persona ids, or 'all'")
    which = parser.add_mutually_exclusive_group(required=True)
    which.add_argument("--keys", help="comma-separated clip keys")
    which.add_argument("--keys-file", type=Path, help="clip keys, one per line (e.g. the file a partial render used)")
    which.add_argument("--all", action="store_true", help="every clip of the voice except names")
    parser.add_argument("--names", action="store_true",
                        help="with --all: also the in-game names (nm_, sq_) -- the recognizer can't spell most of them")
    parser.add_argument("--library", type=Path, default=REPO / "mod" / "voices", help="default: mod/voices")
    parser.add_argument("--model", help="Hugging Face CTC model instead of the one for the voices' language")
    parser.add_argument("--min-right", type=float, default=0.7,
                        help="a key fails if its first word is heard right in fewer voices than this (default 0.7)")
    parser.add_argument("--min-voices", type=int, default=10,
                        help="judge a key only if heard in at least this many voices (names are bound to 3-8)")
    parser.add_argument("--cer-margin", type=float, default=0.5,
                        help="list a single clip whose CER is this much above its key's median in the other voices")
    parser.add_argument("--show", type=int, default=30, help="how many keys and clips to list")
    parser.add_argument("--write-keys", type=Path, help="write the keys of the listed single clips here (re-render)")
    parser.add_argument("--tsv", type=Path, help="write every judged clip here")
    parser.add_argument("--device", default=DEVICE)
    args = parser.parse_args()

    library = args.library.resolve()
    reel.VOICES = library
    languages = {}
    personas = library / "personas.txt"
    if personas.exists():
        for line in personas.read_text(encoding="utf-8").splitlines():
            if line and not line.startswith("#"):
                pid, lang = line.split("|")[:2]
                languages[pid] = lang
    if args.voices == "all":
        voices = sorted(p.name for p in library.iterdir() if p.is_dir() and next(p.glob("*.mp3"), None))
    else:
        voices = args.voices.split(",")
    keys = None
    if args.keys or args.keys_file:
        text = args.keys.replace(",", "\n") if args.keys else args.keys_file.read_text(encoding="utf-8")
        keys = {k.strip() for k in text.splitlines() if k.strip()}
    names = name_texts(library)
    if keys:
        unknown = sorted(keys - CATALOG.keys() - names.keys())
        if unknown:
            print(f"unknown clip keys, skipped: {', '.join(unknown)}")

    by_language: dict[str, list[str]] = defaultdict(list)
    for v in voices:
        by_language[languages.get(v, "en")].append(v)
    results: list[Result] = []
    missing = 0
    started = time.time()
    for lang, group in by_language.items():
        model_id = args.model or MODELS.get(lang)
        if model_id is None:
            sys.exit(f"no recognizer for language '{lang}' -- pass --model")
        print(f"{lang}: {len(group)} voices, {model_id} on {args.device}", flush=True)
        asr = Recognizer(model_id, args.device)
        for voice in group:
            t = time.time()
            found, lost = check_voice(voice, keys, library, names, asr, SOUNDS_LIKE.get(lang, {}), args.names)
            results += found
            missing += lost
            print(f"  {voice:8} {len(found)} clips in {time.time() - t:.0f}s", flush=True)

    by_key: dict[str, list[Result]] = defaultdict(list)
    for r in results:
        by_key[r.key].append(r)
    stats = []
    for key, rs in by_key.items():
        right = sum(r.verdict.first_ok for r in rs)
        wrong = Counter(r.verdict.first or "-" for r in rs if not r.verdict.first_ok)
        stats.append((right / len(rs), -statistics.median(r.verdict.cer for r in rs), key, right, len(rs), wrong))
    stats.sort()
    failed = {key for share, _, key, _, total, _ in stats
              if total >= args.min_voices and share < args.min_right and key not in TAKE_START and kind(key) != "name"}

    print(f"\n{len(by_key)} keys in {len(voices)} voices, {len(results)} clips, {time.time() - started:.0f}s"
          + (f" ({missing} clips missing, see check_library.py)" if missing else ""))
    shown = stats if len(stats) <= args.show else stats[:max(args.show, len(failed))]
    print(f"first word heard right, per key{'' if shown is stats else f' (lowest {len(shown)})'}; "
          f"below {args.min_right:.0%} in at least {args.min_voices} voices fails:")
    for _, neg_cer, key, right, total, wrong in shown:
        heard = ", ".join(f"{w} {n}" for w, n in wrong.most_common(3))
        print(f"  {'FAIL' if key in failed else '    '} {key:28} {right:3}/{total:<3} CER {-neg_cer:.2f}"
              + (f"  heard: {heard}" if heard else ""))

    by_voice: dict[str, list[Result]] = defaultdict(list)
    for r in results:
        by_voice[r.voice].append(r)
    if len(by_voice) > 1:
        ranked = sorted((sum(r.verdict.first_ok for r in rs) / len(rs), statistics.mean(r.verdict.cer for r in rs), v)
                        for v, rs in by_voice.items())
        print(f"\nfirst word heard right, per voice (lowest {min(len(ranked), 10)}; the recognizer knows some accents "
              f"better than others, a voice far below the rest is worth a listen):")
        for share, mean_cer, v in ranked[:10]:
            print(f"  {v:8} {share:4.0%}  mean CER {mean_cer:.2f}")

    # Single voices: the first word alone says little (the recognizer misses one now and then), a garbled clip does --
    # a word said twice, the next word of the take cut in, mumbling. It stands out against the same key in the other
    # voices; on a word of a few letters the CER is mostly the accent ("razor" -> "rase the"), so those don't count.
    usual = {key: statistics.median(r.verdict.cer for r in rs) if len(rs) >= 3 else 0.0 for key, rs in by_key.items()}
    single = sorted((r for r in results if r.key not in failed and len(r.want.replace(" ", "")) >= MIN_LETTERS
                     and r.verdict.cer - usual[r.key] > args.cer_margin),
                    key=lambda r: usual[r.key] - r.verdict.cer)
    listed = single[:args.show]
    print(f"\nsingle clips heard worse than the same key in the other voices (CER more than {args.cer_margin} above "
          f"its median): {len(single)}" + (f", worst {args.show}:" if len(single) > args.show else ":"))
    for r in listed:
        print(f"  {r.voice:8} {r.key:28} CER {r.verdict.cer:.2f} (usually {usual[r.key]:.2f})  want '{r.want}'  "
              f"heard '{r.verdict.heard}'  (call '{r.call}')")
    if args.write_keys and listed:
        args.write_keys.write_text("".join(f"{k}\n" for k in sorted({r.key for r in listed})), encoding="utf-8")
        print(f"wrote the {len({r.key for r in listed})} keys listed to {args.write_keys}; listen, then re-render "
              f"with\n  chatterbox_render.py --voices {','.join(sorted({r.voice for r in listed}))} "
              f"--keys-file {args.write_keys} --seed-offset 1")
    if args.tsv:
        lines = ["voice\tkey\tfirst_ok\tfirst\tcer\twant\theard\tcall"]
        lines += [f"{r.voice}\t{r.key}\t{int(r.verdict.first_ok)}\t{r.verdict.first}\t{r.verdict.cer:.3f}\t{r.want}"
                  f"\t{r.verdict.heard}\t{r.call}" for r in results]
        args.tsv.write_text("\n".join(lines) + "\n", encoding="utf-8")
    if failed:
        print(f"\n{len(failed)} keys below {args.min_right:.0%}: {', '.join(sorted(failed))}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
