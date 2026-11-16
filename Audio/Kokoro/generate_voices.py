"""Generate every in-game voice line in Docs/Voice.md with Kokoro (offline TTS).

Usage (from D:/repos/Unity/SpaceRat/Audio):
    Kokoro/.venv/Scripts/python.exe Kokoro/generate_voices.py          # only missing files
    Kokoro/.venv/Scripts/python.exe Kokoro/generate_voices.py --all    # regenerate everything

Reads the generated tables (ID | Speaker | Line | Delivery | File) and writes each File into
SpaceRat/Assets/SpaceRat/Audio/Voices/. Cast and speeds are set below.
"""
import re
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
from kokoro_onnx import Kokoro

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
VOICE_MD = REPO / "SpaceRat" / "Docs" / "Voice.md"
OUT = REPO / "SpaceRat" / "Assets" / "SpaceRat" / "Audio" / "Voices"

# Cast (picked from the audition samples).
CAST = {
    "pilot": ("am_puck", "en-us", 1.05),
    "computer": ("bf_alice", "en-gb", 0.92),
    "captain": ("af_sarah", "en-us", 1.0),     # Captain Voss (Scene 3 station logs): sharp, brisk
}
DELIVERY_SPEED = {"urgent": 1.12, "hesitant": 0.92}   # multiplies the speaker speed
SR = 24000


def lines_from_voice_md():
    text = VOICE_MD.read_text(encoding="utf-8")
    start = text.index("<!-- BEGIN GENERATED")
    end = text.index("<!-- END GENERATED -->")
    row = re.compile(r"^\| `([^`]+)` \| (\w+) \| (.+?) \| (\w+) \| `([^`]+)` \|$")
    for line in text[start:end].splitlines():
        m = row.match(line.strip())
        if m:
            yield m.group(1), m.group(2).lower(), m.group(3).replace("\\|", "|"), m.group(4), m.group(5)


def main():
    regenerate = "--all" in sys.argv
    OUT.mkdir(parents=True, exist_ok=True)
    kokoro = Kokoro(str(HERE / "models" / "kokoro-v1.0.onnx"), str(HERE / "models" / "voices-v1.0.bin"))
    made = skipped = 0
    for line_id, speaker, text, delivery, filename in lines_from_voice_md():
        path = OUT / filename
        if path.exists() and not regenerate:
            skipped += 1
            continue
        if speaker not in CAST:
            print("   no voice cast for", speaker, line_id)
            continue
        voice, lang, speed = CAST[speaker]
        speed *= DELIVERY_SPEED.get(delivery, 1.0)
        audio, sr = kokoro.create(text, voice=voice, speed=speed, lang=lang)
        assert sr == SR
        audio = np.concatenate([np.zeros(int(SR * 0.05), np.float32), audio, np.zeros(int(SR * 0.15), np.float32)])
        peak = float(np.max(np.abs(audio))) or 1.0
        audio = audio / peak * 0.89          # about -1 dB peak
        sf.write(path, audio, SR, subtype="PCM_16")
        made += 1
        print(f"   {filename}  {len(audio) / SR:.1f}s")
    print(f"Done: {made} generated, {skipped} already there -> {OUT}")


if __name__ == "__main__":
    main()
