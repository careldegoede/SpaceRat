"""Generate voice audition samples for Space Rat with Kokoro (offline TTS).

Usage (from D:/repos/Unity/SpaceRat/Audio):
    Kokoro/.venv/Scripts/python.exe Kokoro/voice_samples.py

Writes WAVs to Audio/Samples/: one file per candidate voice per role, each reading a few
real lines from Docs/Voice.md, plus a short pilot/computer exchange for the top picks.
"""
from pathlib import Path

import numpy as np
import soundfile as sf
from kokoro_onnx import Kokoro

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / "Samples"
OUT.mkdir(exist_ok=True)

kokoro = Kokoro(str(HERE / "models" / "kokoro-v1.0.onnx"), str(HERE / "models" / "voices-v1.0.bin"))

PILOT_LINES = [
    "Ugh... why is everything red? And hot. Why is it hot?",
    "Great. Love that. Who am I?",
    "That's the most terrifying thing anyone has ever said to me.",
    "The rock hit me.",
]
COMPUTER_LINES = [
    "Pilot, respond. This is not a drill. I checked.",
    "We are on a collision course with a star. Red and hot are its main features.",
    "The ship's computer. The one who has been shouting at you for six minutes.",
    "Minor impact. Please stop hitting rocks.",
]

# (voice id, language) - a = American English, b = British English.
PILOT_VOICES = [
    ("am_michael", "en-us"), ("am_adam", "en-us"), ("am_fenrir", "en-us"), ("am_puck", "en-us"),
    ("bm_george", "en-gb"), ("bm_lewis", "en-gb"), ("bm_fable", "en-gb"),
    # Pilot gender is still open, so some female options too.
    ("af_sarah", "en-us"), ("af_nicole", "en-us"), ("af_heart", "en-us"), ("bf_isabella", "en-gb"),
]
COMPUTER_VOICES = [
    ("bf_emma", "en-gb"), ("bf_alice", "en-gb"), ("af_kore", "en-us"), ("af_aoede", "en-us"),
    ("af_nova", "en-us"), ("am_onyx", "en-us"), ("bm_daniel", "en-gb"), ("am_echo", "en-us"),
]

SR = 24000


def say(text, voice, lang, speed=1.0):
    audio, sr = kokoro.create(text, voice=voice, speed=speed, lang=lang)
    assert sr == SR
    return audio


def gap(seconds):
    return np.zeros(int(SR * seconds), dtype=np.float32)


def write(name, parts):
    audio = np.concatenate(parts)
    peak = np.max(np.abs(audio)) or 1.0
    audio = audio / peak * 0.89  # about -1 dB peak
    sf.write(OUT / name, audio, SR, subtype="PCM_16")
    print("  ", name, f"{len(audio) / SR:.1f}s")


available = set(kokoro.get_voices())

print("Pilot candidates:")
for v, lang in PILOT_VOICES:
    if v not in available:
        print("   (missing)", v); continue
    parts = []
    for line in PILOT_LINES:
        parts += [say(line, v, lang, 1.05), gap(0.55)]
    write(f"Pilot_{v}.wav", parts)

print("Computer candidates:")
for v, lang in COMPUTER_VOICES:
    if v not in available:
        print("   (missing)", v); continue
    parts = []
    for line in COMPUTER_LINES:
        parts += [say(line, v, lang, 0.92), gap(0.6)]
    write(f"Computer_{v}.wav", parts)

# A short exchange so you can hear two voices together.
EXCHANGE = [
    ("c", "Pilot. Wake up."),
    ("p", "Ugh... why is everything red? And hot. Why is it hot?"),
    ("c", "We are on a collision course with a star. Red and hot are its main features."),
    ("p", "Great. Love that. Who am I?"),
    ("c", "Your identity records are currently inaccessible."),
    ("p", "Convenient. And who are you?"),
    ("c", "The ship's computer. The one who has been shouting at you for six minutes."),
]
PAIRS = [
    (("am_michael", "en-us"), ("bf_emma", "en-gb")),
    (("bm_george", "en-gb"), ("af_kore", "en-us")),
    (("af_sarah", "en-us"), ("bm_daniel", "en-gb")),
]
print("Exchanges:")
for (pv, pl), (cv, cl) in PAIRS:
    parts = []
    for who, line in EXCHANGE:
        parts += [say(line, pv, pl, 1.05) if who == "p" else say(line, cv, cl, 0.92), gap(0.45)]
    write(f"Exchange_pilot-{pv}_computer-{cv}.wav", parts)

print("Done ->", OUT)
