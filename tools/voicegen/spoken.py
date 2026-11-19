"""Numbers as the TTS should read them: words, never digits (it reads digits like a calculator)."""

from __future__ import annotations

# Radio digits: "niner", not "nine".
DIGIT_WORDS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"]
ONES = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]
TEENS = ["ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen",
         "nineteen"]
TENS = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"]


def digits(number: str) -> str:
    """Digit by digit, radio style: "273" -> "two seven three"."""
    return " ".join(DIGIT_WORDS[int(c)] for c in number)


def number_words(n: int) -> str:
    """0-99 as a cardinal ("twenty-five"); larger numbers digit by digit with plain "nine"."""
    if n < 10:
        return ONES[n]
    if n < 20:
        return TEENS[n - 10]
    if n < 100:
        return TENS[n // 10] + ("-" + ONES[n % 10] if n % 10 else "")
    return " ".join(ONES[int(c)] for c in str(n))
