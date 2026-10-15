"""Word timestamps for a known transcript: CTC forced alignment with an English wav2vec2 model.

Engines like Chatterbox report no word boundaries, but take cutting needs them. Since the transcript is known, a
character-level CTC model (facebook/wav2vec2-base-960h) plus a Viterbi pass over the trellis gives each word's
start and end. torchaudio 2.9 dropped its forced_align API, so the trellis/backtrack below is the standard
algorithm from the old torchaudio tutorial, written out.
"""

from __future__ import annotations

import re
from dataclasses import dataclass

import torch
import torchaudio

MODEL_ID = "facebook/wav2vec2-base-960h"
SAMPLE_RATE = 16000


def normalize_words(text: str) -> list[str]:
    """Upper-case letters and apostrophes only; hyphenated words split ("two-one" -> TWO, ONE)."""
    text = text.upper().replace("-", " ")
    return [w for w in (re.sub(r"[^A-Z']", "", t) for t in text.split()) if w]


@dataclass
class _Point:
    token: int
    frame: int


class ForcedAligner:
    def __init__(self, device: str) -> None:
        from transformers import Wav2Vec2ForCTC, Wav2Vec2Processor

        self.device = device
        self.processor = Wav2Vec2Processor.from_pretrained(MODEL_ID)
        self.model = Wav2Vec2ForCTC.from_pretrained(MODEL_ID).to(device).eval()
        vocab = self.processor.tokenizer.get_vocab()
        self.blank = vocab[self.processor.tokenizer.pad_token]
        self.sep = vocab["|"]
        self.vocab = vocab

    def word_times(self, wav: torch.Tensor, sr: int, text: str) -> list[tuple[float, float]]:
        """(start, end) in seconds for every word of normalize_words(text)."""
        mono = wav.mean(dim=0) if wav.dim() > 1 else wav
        audio = torchaudio.functional.resample(mono, sr, SAMPLE_RATE)
        inputs = self.processor(audio.numpy(), sampling_rate=SAMPLE_RATE, return_tensors="pt")
        with torch.inference_mode():
            logits = self.model(inputs.input_values.to(self.device)).logits[0]
        emission = torch.log_softmax(logits, dim=-1).cpu()

        words = normalize_words(text)
        transcript = "|" + "|".join(words) + "|"
        tokens = [self.vocab[c] for c in transcript]
        path = self._backtrack(self._trellis(emission, tokens), emission, tokens)

        # Frames per token index, then per word (tokens between separators).
        frames: dict[int, list[int]] = {}
        for p in path:
            frames.setdefault(p.token, []).append(p.frame)
        seconds_per_frame = audio.size(0) / emission.size(0) / SAMPLE_RATE
        result = []
        i = 1  # skip leading "|"
        for w in words:
            first, last = i, i + len(w) - 1
            start = min(frames[first]) * seconds_per_frame
            end = (max(frames[last]) + 1) * seconds_per_frame
            result.append((start, end))
            i = last + 2  # skip the separator
        return result

    def _trellis(self, emission: torch.Tensor, tokens: list[int]) -> torch.Tensor:
        num_frames, num_tokens = emission.size(0), len(tokens)
        trellis = torch.zeros((num_frames, num_tokens))
        trellis[1:, 0] = torch.cumsum(emission[1:, self.blank], 0)
        trellis[0, 1:] = -float("inf")
        trellis[-num_tokens + 1:, 0] = float("inf")
        tok = torch.tensor(tokens)
        for t in range(num_frames - 1):
            trellis[t + 1, 1:] = torch.maximum(
                trellis[t, 1:] + emission[t, self.blank],
                trellis[t, :-1] + emission[t, tok[1:]],
            )
        return trellis

    def _backtrack(self, trellis: torch.Tensor, emission: torch.Tensor, tokens: list[int]) -> list[_Point]:
        t, j = trellis.size(0) - 1, trellis.size(1) - 1
        path = [_Point(j, t)]
        while j > 0:
            if t <= 0:
                raise ValueError("alignment failed (audio shorter than transcript)")
            stayed = trellis[t - 1, j] + emission[t - 1, self.blank]
            changed = trellis[t - 1, j - 1] + emission[t - 1, tokens[j]]
            t -= 1
            if changed > stayed:
                j -= 1
            path.append(_Point(j, t))
        return path[::-1]
