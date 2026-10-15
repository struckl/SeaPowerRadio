"""The NATO/English voice cast for Sea Power Radio.

Every persona is one person on the net: a Microsoft Edge neural voice (edge-tts, same engine as Ratten Chatter)
plus a fixed rate and pitch offset. Base voices are reused with clearly different offsets to get more distinct
people than there are base voices — the plugin hands every element of a mission its own persona, so the pool size
is the number of elements that can talk before anyone has to share.

Accents are spread on purpose (US, UK, Canada, Australia, New Zealand, Ireland): a NATO task group is not staffed
by forty identical Americans. Child-sounding voices (en-US-Ana, en-GB-Maisie) are left out.

Run this file to write ../../mod/voices/personas.txt, which the plugin reads at startup:

    python tools/voicegen/cast.py
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Persona:
    id: str
    voice: str
    gender: str          # "m" / "f"
    accent: str          # "us", "gb", "ca", "au", "nz", "ie"
    rate: str = "+0%"
    pitch: str = "+0Hz"
    language: str = "en"


def _p(id_: str, voice: str, gender: str, rate: str = "+0%", pitch: str = "+0Hz") -> Persona:
    accent = id_.split("_")[0]
    return Persona(id_, voice, gender, accent, rate, pitch)


CAST: list[Persona] = [
    # --- United States, male: every base voice twice, once lower/slower, once brighter/quicker -------------
    _p("us_m_01", "en-US-AndrewNeural", "m", "-3%", "-6Hz"),
    _p("us_m_02", "en-US-AndrewNeural", "m", "+5%", "+5Hz"),
    _p("us_m_03", "en-US-AndrewMultilingualNeural", "m", "-6%", "-10Hz"),
    _p("us_m_04", "en-US-AndrewMultilingualNeural", "m", "+2%", "+2Hz"),
    _p("us_m_05", "en-US-BrianNeural", "m", "+3%", "+0Hz"),
    _p("us_m_06", "en-US-BrianNeural", "m", "-4%", "-8Hz"),
    _p("us_m_07", "en-US-BrianMultilingualNeural", "m", "+6%", "+4Hz"),
    _p("us_m_08", "en-US-BrianMultilingualNeural", "m", "-2%", "-5Hz"),
    _p("us_m_09", "en-US-ChristopherNeural", "m", "-4%", "+0Hz"),
    _p("us_m_10", "en-US-ChristopherNeural", "m", "+4%", "+6Hz"),
    _p("us_m_11", "en-US-EricNeural", "m", "-5%", "-6Hz"),
    _p("us_m_12", "en-US-EricNeural", "m", "+3%", "+3Hz"),
    _p("us_m_13", "en-US-GuyNeural", "m", "-6%", "-10Hz"),
    _p("us_m_14", "en-US-GuyNeural", "m", "+2%", "+2Hz"),
    _p("us_m_15", "en-US-RogerNeural", "m", "+6%", "+4Hz"),
    _p("us_m_16", "en-US-RogerNeural", "m", "-3%", "-6Hz"),
    _p("us_m_17", "en-US-SteffanNeural", "m", "-7%", "-8Hz"),
    _p("us_m_18", "en-US-SteffanNeural", "m", "+2%", "+3Hz"),
    # --- United States, female -----------------------------------------------------------------------------
    _p("us_f_01", "en-US-AriaNeural", "f", "-2%", "-4Hz"),
    _p("us_f_02", "en-US-AvaNeural", "f", "+2%", "-2Hz"),
    _p("us_f_03", "en-US-AvaMultilingualNeural", "f", "-3%", "-5Hz"),
    _p("us_f_04", "en-US-EmmaNeural", "f", "-4%", "-6Hz"),
    _p("us_f_05", "en-US-EmmaMultilingualNeural", "f", "+4%", "+0Hz"),
    _p("us_f_06", "en-US-JennyNeural", "f", "+1%", "-3Hz"),
    _p("us_f_07", "en-US-MichelleNeural", "f", "-3%", "-5Hz"),
    # --- United Kingdom --------------------------------------------------------------------------------------
    _p("gb_m_01", "en-GB-RyanNeural", "m", "-1%", "-4Hz"),
    _p("gb_m_02", "en-GB-RyanNeural", "m", "+5%", "+5Hz"),
    _p("gb_m_03", "en-GB-ThomasNeural", "m", "-3%", "-6Hz"),
    _p("gb_m_04", "en-GB-ThomasNeural", "m", "+5%", "+6Hz"),
    _p("gb_f_01", "en-GB-LibbyNeural", "f", "+2%", "+0Hz"),
    _p("gb_f_02", "en-GB-SoniaNeural", "f", "+4%", "-2Hz"),
    # --- Canada ----------------------------------------------------------------------------------------------
    _p("ca_m_01", "en-CA-LiamNeural", "m", "+2%", "+0Hz"),
    _p("ca_m_02", "en-CA-LiamNeural", "m", "-5%", "-8Hz"),
    _p("ca_f_01", "en-CA-ClaraNeural", "f", "-1%", "-3Hz"),
    # --- Australia -------------------------------------------------------------------------------------------
    _p("au_m_01", "en-AU-WilliamMultilingualNeural", "m", "-3%", "-4Hz"),
    _p("au_m_02", "en-AU-WilliamMultilingualNeural", "m", "+5%", "+5Hz"),
    _p("au_f_01", "en-AU-NatashaNeural", "f", "+1%", "-3Hz"),
    # --- New Zealand -----------------------------------------------------------------------------------------
    _p("nz_m_01", "en-NZ-MitchellNeural", "m", "-3%", "-5Hz"),
    _p("nz_m_02", "en-NZ-MitchellNeural", "m", "+4%", "+5Hz"),
    _p("nz_f_01", "en-NZ-MollyNeural", "f", "+5%", "+0Hz"),
    # --- Ireland ---------------------------------------------------------------------------------------------
    _p("ie_m_01", "en-IE-ConnorNeural", "m", "-2%", "-4Hz"),
    _p("ie_m_02", "en-IE-ConnorNeural", "m", "+6%", "+7Hz"),
    _p("ie_f_01", "en-IE-EmilyNeural", "f", "+3%", "+0Hz"),
]

BY_ID = {p.id: p for p in CAST}
assert len(BY_ID) == len(CAST), "duplicate persona id"

REPO = Path(__file__).resolve().parents[2]
PERSONAS_FILE = REPO / "mod" / "voices" / "personas.txt"


def write_personas_file() -> None:
    PERSONAS_FILE.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Sea Power Radio voice personas -- generated by tools/voicegen/cast.py, do not edit by hand.",
        "# id|language|gender|accent",
    ]
    lines += [f"{p.id}|{p.language}|{p.gender}|{p.accent}" for p in CAST]
    PERSONAS_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {len(CAST)} personas to {PERSONAS_FILE}")


if __name__ == "__main__":
    write_personas_file()
