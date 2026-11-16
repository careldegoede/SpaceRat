# Kokoro voice generation

Offline text-to-speech for every spoken line in Space Rat, using
[Kokoro](https://github.com/thewh1teagle/kokoro-onnx) (Apache-2.0 model, runs on the CPU, no account
or API key). The model and the Python environment stay on your machine (git-ignored). Only the
scripts and the generated WAVs are committed.

## One-time setup (Windows)

From the repo root:

```powershell
# 1. Python env (tested with Python 3.13)
python -m venv Audio/Kokoro/.venv
Audio/Kokoro/.venv/Scripts/python.exe -m pip install kokoro-onnx soundfile numpy

# 2. Model files (~350 MB total) into Audio/Kokoro/models/
mkdir Audio/Kokoro/models
curl -L -o Audio/Kokoro/models/kokoro-v1.0.onnx https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.onnx
curl -L -o Audio/Kokoro/models/voices-v1.0.bin  https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin
```

Known-good versions: `kokoro-onnx 0.6.1`, `onnxruntime 1.32.0`, `numpy 2.5.5`, `soundfile 0.14.0`.

## Generating the game's lines

1. In Unity, after adding or changing dialogue: **SpaceRat > Export Voice Script**. This rewrites the
   generated tables in `SpaceRat/Docs/Voice.md` (ID, speaker, line, delivery, file name).
2. Generate the WAVs (only missing files; `--all` regenerates everything):

   ```powershell
   Audio/Kokoro/.venv/Scripts/python.exe Audio/Kokoro/generate_voices.py
   Audio/Kokoro/.venv/Scripts/python.exe Audio/Kokoro/generate_voices.py --all
   ```

   Output goes to `SpaceRat/Assets/SpaceRat/Audio/Voices/` as `<ID>_<speaker>.wav`.
3. Back in Unity: **SpaceRat > Link Voice Clips**. This assigns each clip to its `DialogueLine.voice`
   and logs any line that has no file.

**If you edit a line's text**, delete its WAV (or run with `--all`) or the old audio stays.
Reordering lines in a sequence changes their IDs, so re-export, regenerate with `--all` and re-link.

## Cast and tuning

Set at the top of `generate_voices.py`:

| Speaker  | Voice      | Accent  | Speed |
|----------|------------|---------|-------|
| Computer | `bf_alice` | British | 0.92  |
| Pilot    | `am_puck`  | American| 1.05  |

The delivery column in `Voice.md` adjusts speed per line: `urgent` (line ends with `!`) ×1.12,
`hesitant` (contains `...`) ×0.92. Each clip gets 50 ms of silence before and 150 ms after, and
peaks are normalised to about −1 dB. Format: 24 kHz, 16-bit, mono.

## Auditioning other voices

```powershell
Audio/Kokoro/.venv/Scripts/python.exe Audio/Kokoro/voice_samples.py
```

This writes sample WAVs for a list of candidate voices to `Audio/Samples/` (git-ignored), each reading a few
real lines. Pick one, update `CAST` in `generate_voices.py`, then regenerate with `--all`.

## In game

`DialogueBox` plays the clip when a line is shown. A line stays on screen for its scripted
duration or until its clip has finished, whichever is longer, so voices never overlap. Space skips
a line and cuts its clip. In the Comset (on-foot) style, the computer's voice gets a radio band-pass.
