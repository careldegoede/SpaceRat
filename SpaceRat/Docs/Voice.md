# Space Rat — Voice Script

Every spoken line in the game, ready for text-to-speech / voice tools.

- **In-game lines** are generated from the `DialogueSequence` assets — run **SpaceRat → Export Voice Script**
  after changing dialogue. Don't edit that section by hand.
- **Planned lines** (scenes not built yet) are maintained by hand at the bottom and move into the
  generated section once their assets exist.
- **Tone rule:** sharp, funny, sarcastic pilot; deadpan computer who lands the occasional dry jab.

## Characters & voice direction

### COMPUTER — the ship's onboard AI
- **Cast (Kokoro):** `bf_alice` (British female), speed 0.92.
- **Voice:** calm, precise, synthetic but warm underneath; neutral accent; medium-low pitch.
- **Delivery:** deadpan and literal. Never jokes on purpose — the humour comes from total sincerity
  ("Red and hot are its main features."). Slight pause before the punchline-ish clause.
- **Pace:** measured; urgent lines speed up but never panic.
- **TTS prompt:** *"Calm synthetic starship AI, deadpan, precise diction, emotionally flat but faintly
  sardonic, studio-clean, no reverb."*
- **Processing (applied in Unity, record clean):** light band-pass + subtle bit-crush/ring-mod for the
  ship-speaker feel.

### PILOT — amnesiac, identity unknown
- **Cast (Kokoro):** `am_puck` (American male), speed 1.05.
- **Voice:** male (matches the in-game avatar and poster); his name and past are the mystery. Late 20s–40s, a bit
  hoarse from just waking up in Scene 1.
- **Delivery:** quick, sarcastic, exasperated; jokes as a coping mechanism. Groggy and slurred for the
  first lines of Beat 3, sharper as adrenaline kicks in.
- **TTS prompt:** *"Exhausted sarcastic spaceship pilot, dry wit, quick comic timing, slightly hoarse,
  stressed but funny, close-mic cockpit recording."*
- **Processing:** none, or a faint helmet/cockpit room tone.

### Delivery tags used in the tables
`deadpan` (computer default) · `dry` (pilot default) · `urgent` (ends with "!") · `hesitant` (contains "...").
Treat them as starting points; adjust per line.

## Audio file conventions
- **Format:** WAV, 24 kHz (Kokoro native), 16-bit, mono, peaks normalised to about −1 dB.
- **Generate:** `Audio/Kokoro/.venv/Scripts/python.exe Audio/Kokoro/generate_voices.py` from the repo root
  (missing files only; `--all` regenerates). Cast and speeds live at the top of that script.
  Model + venv are local only (git-ignored); see `Audio/Kokoro/README.md`.
- **Naming:** `<ID>_<speaker>.wav`, e.g. `S01_Beat3_Amnesia_07_pilot.wav` (IDs below are stable per
  asset + line index; re-export after reordering lines).
- **Location:** `Assets/SpaceRat/Audio/Voices/`. After generating, run **SpaceRat > Link Voice Clips** so each
  DialogueLine gets its clip. A line holds until its clip has finished (Space skips and cuts the clip).
- **Pacing:** in-game lines are auto-timed; if a recording runs longer than its `duration`, raise the
  duration in the DialogueSequence asset.

## In-game lines

<!-- BEGIN GENERATED: in-game lines (SpaceRat > Export Voice Script) -->

_Generated from 18 DialogueSequence assets. Do not edit by hand — edit the assets, then re-export._

### S01 · Beat1_ExteriorEmergency

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat1_ExteriorEmergency_01` | Computer | Warning. Stellar collision trajectory detected. | deadpan | `S01_Beat1_ExteriorEmergency_01_computer.wav` |
| `S01_Beat1_ExteriorEmergency_02` | Computer | Attempting automatic course correction. | deadpan | `S01_Beat1_ExteriorEmergency_02_computer.wav` |
| `S01_Beat1_ExteriorEmergency_03` | Computer | Course correction failed. Flight control interface unavailable. Again. | deadpan | `S01_Beat1_ExteriorEmergency_03_computer.wav` |
| `S01_Beat1_ExteriorEmergency_04` | Computer | Pilot response required. Pilot response... strongly encouraged. | hesitant | `S01_Beat1_ExteriorEmergency_04_computer.wav` |

### S01 · Beat2_WakePilot

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat2_WakePilot_01` | Computer | Pilot. Wake up. | deadpan | `S01_Beat2_WakePilot_01_computer.wav` |
| `S01_Beat2_WakePilot_02` | Computer | Pilot, respond. This is not a drill. I checked. | deadpan | `S01_Beat2_WakePilot_02_computer.wav` |
| `S01_Beat2_WakePilot_03` | Computer | Impact probability increasing. So is my disappointment. | deadpan | `S01_Beat2_WakePilot_03_computer.wav` |

### S01 · Beat3_Amnesia

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat3_Amnesia_01` | Pilot | Ugh... why is everything red? And hot. Why is it hot? | hesitant | `S01_Beat3_Amnesia_01_pilot.wav` |
| `S01_Beat3_Amnesia_02` | Computer | We are on a collision course with a star. Red and hot are its main features. | deadpan | `S01_Beat3_Amnesia_02_computer.wav` |
| `S01_Beat3_Amnesia_03` | Pilot | Great. Love that. Who am I? | dry | `S01_Beat3_Amnesia_03_pilot.wav` |
| `S01_Beat3_Amnesia_04` | Computer | Your identity records are currently inaccessible. | deadpan | `S01_Beat3_Amnesia_04_computer.wav` |
| `S01_Beat3_Amnesia_05` | Pilot | Convenient. And who are you? | dry | `S01_Beat3_Amnesia_05_pilot.wav` |
| `S01_Beat3_Amnesia_06` | Computer | The ship's computer. The one who has been shouting at you for six minutes. | deadpan | `S01_Beat3_Amnesia_06_computer.wav` |
| `S01_Beat3_Amnesia_07` | Pilot | Then change course, genius! | urgent | `S01_Beat3_Amnesia_07_pilot.wav` |
| `S01_Beat3_Amnesia_08` | Computer | Unable to comply. Flight controls are ignoring me. | deadpan | `S01_Beat3_Amnesia_08_computer.wav` |
| `S01_Beat3_Amnesia_09` | Pilot | Relatable. | dry | `S01_Beat3_Amnesia_09_pilot.wav` |
| `S01_Beat3_Amnesia_10` | Pilot | Can I steer it? | dry | `S01_Beat3_Amnesia_10_pilot.wav` |
| `S01_Beat3_Amnesia_11` | Computer | Manual controls may still function. Statistically, you are our best option. | deadpan | `S01_Beat3_Amnesia_11_computer.wav` |
| `S01_Beat3_Amnesia_12` | Pilot | That's the most terrifying thing anyone has ever said to me. | dry | `S01_Beat3_Amnesia_12_pilot.wav` |
| `S01_Beat3_Amnesia_13` | Computer | You have no memories to compare it to. | deadpan | `S01_Beat3_Amnesia_13_computer.wav` |

### S01 · Beat4_HeatCritical

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat4_HeatCritical_01` | Computer | Heat levels critical. Keep turning! | urgent | `S01_Beat4_HeatCritical_01_computer.wav` |
| `S01_Beat4_HeatCritical_02` | Pilot | I'm turning! Is there a faster turning? | dry | `S01_Beat4_HeatCritical_02_pilot.wav` |

### S01 · Beat4_Takeover

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat4_Takeover_01` | Computer | Manual control link established. | deadpan | `S01_Beat4_Takeover_01_computer.wav` |
| `S01_Beat4_Takeover_02` | Computer | Ease us away from the star. Gently. Unlike your wake-up. | deadpan | `S01_Beat4_Takeover_02_computer.wav` |

### S01 · Beat5_Escape

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Beat5_Escape_01` | Computer | Collision course avoided. | deadpan | `S01_Beat5_Escape_01_computer.wav` |
| `S01_Beat5_Escape_02` | Pilot | You're welcome. Now tell me who I am. | dry | `S01_Beat5_Escape_02_pilot.wav` |
| `S01_Beat5_Escape_03` | Computer | I am unable to answer that question. | deadpan | `S01_Beat5_Escape_03_computer.wav` |
| `S01_Beat5_Escape_04` | Pilot | Of course you are. | dry | `S01_Beat5_Escape_04_pilot.wav` |

### S01 · Failure_Impact

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S01_Failure_Impact_01` | Computer | Impact. | deadpan | `S01_Failure_Impact_01_computer.wav` |

### S02 · Asteroid

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Asteroid_01` | Computer | Minor impact. Please stop hitting rocks. | deadpan | `S02_Asteroid_01_computer.wav` |
| `S02_Asteroid_02` | Pilot | The rock hit me. | dry | `S02_Asteroid_02_pilot.wav` |

### S02 · Crash

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Crash_01` | Computer | Pilot? ... Please respond. | hesitant | `S02_Crash_01_computer.wav` |

### S02 · Flight_Memory

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Flight_Memory_01` | Pilot | Any idea what wiped our memories? | dry | `S02_Flight_Memory_01_pilot.wav` |
| `S02_Flight_Memory_02` | Computer | No. The missing records are remarkably thorough. | deadpan | `S02_Flight_Memory_02_computer.wav` |
| `S02_Flight_Memory_03` | Pilot | That's not reassuring. | dry | `S02_Flight_Memory_03_pilot.wav` |
| `S02_Flight_Memory_04` | Computer | It was not intended to be. | deadpan | `S02_Flight_Memory_04_computer.wav` |

### S02 · Flight_Music

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Flight_Music_01` | Pilot | So... got any music in there? | hesitant | `S02_Flight_Music_01_pilot.wav` |
| `S02_Flight_Music_02` | Computer | Audio archives missing. | deadpan | `S02_Flight_Music_02_computer.wav` |
| `S02_Flight_Music_03` | Pilot | Of course they are. | dry | `S02_Flight_Music_03_pilot.wav` |
| `S02_Flight_Music_04` | Computer | I can play the collision alarm. | deadpan | `S02_Flight_Music_04_computer.wav` |
| `S02_Flight_Music_05` | Pilot | Please don't. | dry | `S02_Flight_Music_05_pilot.wav` |

### S02 · Flight_Signal

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Flight_Signal_01` | Computer | Signal strength increasing. We are approaching the source. | deadpan | `S02_Flight_Signal_01_computer.wav` |
| `S02_Flight_Signal_02` | Pilot | Finally, good news. Write that down. Oh wait, you can't. | dry | `S02_Flight_Signal_02_pilot.wav` |

### S02 · Malfunction

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Malfunction_01` | Computer | Warning. Uncommanded power fluctuation. | deadpan | `S02_Malfunction_01_computer.wav` |
| `S02_Malfunction_02` | Pilot | Oh, come ON. | dry | `S02_Malfunction_02_pilot.wav` |
| `S02_Malfunction_03` | Computer | Flight controls are no longer responding. Engine output unstable. | deadpan | `S02_Malfunction_03_computer.wav` |
| `S02_Malfunction_04` | Pilot | Give me manual override! | urgent | `S02_Malfunction_04_pilot.wav` |
| `S02_Malfunction_05` | Computer | Manual override unavailable. | deadpan | `S02_Malfunction_05_computer.wav` |
| `S02_Malfunction_06` | Pilot | You said we could make it! | urgent | `S02_Malfunction_06_pilot.wav` |
| `S02_Malfunction_07` | Computer | I said we could attempt to reach the planet. | deadpan | `S02_Malfunction_07_computer.wav` |
| `S02_Malfunction_08` | Pilot | This is a terrible time to be precise! | urgent | `S02_Malfunction_08_pilot.wav` |
| `S02_Malfunction_09` | Computer | Atmospheric entry imminent. Brace for impact. | deadpan | `S02_Malfunction_09_computer.wav` |
| `S02_Malfunction_10` | Pilot | Bracing! Whatever that means! | urgent | `S02_Malfunction_10_pilot.wav` |

### S02 · Nag_Sun

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Nag_Sun_01` | Computer | We have already tried the star. I did not enjoy it. | deadpan | `S02_Nag_Sun_01_computer.wav` |

### S02 · Nags

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Nags_01` | Computer | The planet is the other way. The large, round one. | deadpan | `S02_Nags_01_computer.wav` |
| `S02_Nags_02` | Computer | You are drifting off course. Recalculating my opinion of you. | deadpan | `S02_Nags_02_computer.wav` |
| `S02_Nags_03` | Computer | The glowing marker is the destination. I added it specifically for you. | deadpan | `S02_Nags_03_computer.wav` |

### S02 · Recovery

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Recovery_01` | Pilot | Okay. Not on fire. Small wins. | dry | `S02_Recovery_01_pilot.wav` |
| `S02_Recovery_02` | Computer | Hull temperature returning to survivable levels. | deadpan | `S02_Recovery_02_computer.wav` |
| `S02_Recovery_03` | Pilot | "Survivable." Love the bar we're setting. | dry | `S02_Recovery_03_pilot.wav` |
| `S02_Recovery_04` | Pilot | Fine. Forget who I am. Where are we? | dry | `S02_Recovery_04_pilot.wav` |
| `S02_Recovery_05` | Computer | Current position unknown. Navigation database inaccessible. | deadpan | `S02_Recovery_05_computer.wav` |
| `S02_Recovery_06` | Pilot | So you don't know who I am, where we are, or why we're here. | dry | `S02_Recovery_06_pilot.wav` |
| `S02_Recovery_07` | Computer | That is an accurate summary. | deadpan | `S02_Recovery_07_computer.wav` |
| `S02_Recovery_08` | Pilot | Fantastic. What do you know? | dry | `S02_Recovery_08_pilot.wav` |
| `S02_Recovery_09` | Computer | We were recently on a collision course with a star. | deadpan | `S02_Recovery_09_computer.wav` |
| `S02_Recovery_10` | Pilot | Yeah, I was there for that part. | dry | `S02_Recovery_10_pilot.wav` |
| `S02_Recovery_11` | Computer | I appear to have initialised shortly before the collision warning. My earlier operational history is also missing. | deadpan | `S02_Recovery_11_computer.wav` |
| `S02_Recovery_12` | Pilot | So we both just woke up. | dry | `S02_Recovery_12_pilot.wav` |
| `S02_Recovery_13` | Computer | That is a reasonable hypothesis. I cannot verify it. | deadpan | `S02_Recovery_13_computer.wav` |
| `S02_Recovery_14` | Pilot | Great. Two amnesiacs in a broken spaceship. Sounds like the start of a bad joke. | dry | `S02_Recovery_14_pilot.wav` |
| `S02_Recovery_15` | Computer | I do not know any jokes. That data is also missing. | deadpan | `S02_Recovery_15_computer.wav` |
| `S02_Recovery_16` | Pilot | Somehow that's the saddest thing you've said. | dry | `S02_Recovery_16_pilot.wav` |

### S02 · Scan

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Scan_01` | Pilot | Scan the area. Anything that helps us not die. | dry | `S02_Scan_01_pilot.wav` |
| `S02_Scan_02` | Computer | Initiating passive scan. Sensors operating below nominal capacity. | deadpan | `S02_Scan_02_computer.wav` |
| `S02_Scan_03` | Pilot | Join the club. | dry | `S02_Scan_03_pilot.wav` |
| `S02_Scan_04` | Computer | ...One signal detected. Extremely weak. Origin: a nearby planetary body. | hesitant | `S02_Scan_04_computer.wav` |
| `S02_Scan_05` | Pilot | A distress beacon? | dry | `S02_Scan_05_pilot.wav` |
| `S02_Scan_06` | Computer | Unknown. It may be artificial. It may also be interference. | deadpan | `S02_Scan_06_computer.wav` |
| `S02_Scan_07` | Pilot | "Maybe help, maybe static." Still better odds than the star. Can we get there? | dry | `S02_Scan_07_pilot.wav` |
| `S02_Scan_08` | Computer | Warp drive offline. Conventional thrusters available, with limitations. | deadpan | `S02_Scan_08_computer.wav` |
| `S02_Scan_09` | Pilot | Of course they are. Mark the bearing. | dry | `S02_Scan_09_pilot.wav` |
| `S02_Scan_10` | Computer | Bearing marked. I recommend avoiding further stellar encounters. | deadpan | `S02_Scan_10_computer.wav` |
| `S02_Scan_11` | Pilot | Wow. Wish I'd thought of that. | dry | `S02_Scan_11_pilot.wav` |

### S02 · Tutorial

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S02_Tutorial_01` | Computer | Thruster overdrive available. Hold Shift. Do not hold it forever. | deadpan | `S02_Tutorial_01_computer.wav` |
| `S02_Tutorial_02` | Pilot | What happens if I hold it forever? | dry | `S02_Tutorial_02_pilot.wav` |
| `S02_Tutorial_03` | Computer | It stops. Then you are disappointed. | deadpan | `S02_Tutorial_03_computer.wav` |

**Totals:** Computer 49, Pilot 38

<!-- END GENERATED -->

## Planned lines (not in game yet)

### Scene 3 — The Crash Site (from `Plans/SpaceRat_Scene3_Development_Brief.md`, draft)

"Computer (comset)" lines are the same COMPUTER voice through the comset — apply a radio filter (band-pass + light
crackle) in Unity; record clean.

| ID | Speaker | Line | Delivery |
|---|---|---|---|
| `S03_Wake_01` | Computer | Pilot? ... Please respond. | damaged, quiet |
| `S03_Wake_02` | Pilot | Ow. Everything. Ow. | groggy |
| `S03_Wake_03` | Computer | You are alive. Statistically, that was not the expected outcome. | deadpan |
| `S03_Wake_04` | Pilot | Thanks for the vote of confidence. Where are we? | dry |
| `S03_Wake_05` | Computer | On the planet. Specifically, in a hole we made in the planet. | deadpan |
| `S03_Wake_06` | Pilot | Comforting. Anything out there? People? Help? A bar? | dry |
| `S03_Wake_07` | Computer | During descent I detected structures roughly two kilometres north-east. Possibly a settlement. | deadpan |
| `S03_Wake_08` | Pilot | Possibly? | incredulous |
| `S03_Wake_09` | Computer | I was busy crashing. | deadpan |
| `S03_Wake_10` | Pilot | Fair. Can you call them? | dry |
| `S03_Wake_11` | Computer | Long-range communications are damaged. Take the comset from the console. I can reach you through it. It may also reach whoever lives there. | deadpan |
| `S03_Wake_12` | Pilot | Great. A walkie-talkie and a ship that hates me. | sarcastic |
| `S03_Wake_13` | Computer | I do not hate you. I have not decided yet. | deadpan |
| `S03_Exit_01` | Computer (comset) | Comset link established. Can you hear me? | radio |
| `S03_Exit_02` | Pilot | Loud and unfortunately clear. | dry |
| `S03_Exit_03` | Computer (comset) | Head north-east. Follow your compass. Try not to fall into anything. | radio, deadpan |
| `S03_Walk_01` | Pilot | So... how the heck are we getting off this rock? | exasperated |
| `S03_Walk_02` | Computer (comset) | The ship requires a new reactor regulator. Ours is currently in three pieces. Two of them are on fire. | radio, deadpan |
| `S03_Walk_03` | Pilot | And where do we get one of those? | dry |
| `S03_Walk_04` | Computer (comset) | Settlements tend to have spare parts. Or people who will sell them. | radio, deadpan |
| `S03_Walk_05` | Pilot | With what money? | dry |
| `S03_Walk_06` | Computer (comset) | You have charm. | radio, deadpan |
| `S03_Walk_07` | Pilot | Do I? | genuinely unsure |
| `S03_Walk_08` | Computer (comset) | Unknown. Your records are missing. | radio, deadpan |
| `S03_Walk_09` | Pilot | ...That was uncalled for. | wounded |
| `S03_Walk_10` | Computer (comset) | I will also need a new navigation core. Mine currently identifies this planet as "Error". | radio, deadpan |
| `S03_Walk_11` | Pilot | Honestly? Fitting. | dry |
| `S03_Walk_12` | Computer (comset) | One more thing. The signal we followed. It originates from the settlement. | radio, serious |
| `S03_Walk_13` | Pilot | So we crash-landed next to the one thing calling us. Coincidence? | suspicious |
| `S03_Walk_14` | Computer (comset) | I am not programmed for suspicion. | radio, deadpan |
| `S03_Walk_15` | Pilot | Lucky for you, I am. | dry |
| `S03_Nag_01` | Computer (comset) | North-east. It is the direction you are not walking in. | radio, deadpan |
| `S03_Nag_02` | Computer (comset) | The compass is the round thing at the top of your vision. It is there to help. | radio, deadpan |
| `S03_Nag_03` | Computer (comset) | You are walking back to the ship. I am flattered, but no. | radio, deadpan |
| `S03_End_01` | Computer (comset) | Structures ahead. Life signs: inconclusive. | radio, cautious |
| `S03_End_02` | Pilot | "Inconclusive" is my least favourite word. | dry |
| `S03_End_03` | Computer (comset) | Noted. I will use it more often. | radio, deadpan |
