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

_Generated from 36 DialogueSequence assets. Do not edit by hand — edit the assets, then re-export._

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

### S03 · Arrival

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Arrival_01` | Pilot | Hello? Anybody home? | dry | `S03_Arrival_01_pilot.wav` |
| `S03_Arrival_02` | Computer | Life signs: inconclusive. | deadpan | `S03_Arrival_02_computer.wav` |
| `S03_Arrival_03` | Pilot | You keep saying that. | dry | `S03_Arrival_03_pilot.wav` |
| `S03_Arrival_04` | Computer | It keeps being true. | deadpan | `S03_Arrival_04_computer.wav` |
| `S03_Arrival_05` | Pilot | Lights on, doors open, nobody here. That's not creepy at all. | dry | `S03_Arrival_05_pilot.wav` |
| `S03_Arrival_06` | Computer | There is a command seat with terminal access. Sit down and I will pull the station manifest. | deadpan | `S03_Arrival_06_computer.wav` |

### S03 · Bot_Activate

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Bot_Activate_01` | Pilot | Found it. Looks like a bowling ball with opinions. | dry | `S03_Bot_Activate_01_pilot.wav` |
| `S03_Bot_Activate_02` | Computer | It has a functioning processor. I am copying myself into it. | deadpan | `S03_Bot_Activate_02_computer.wav` |
| `S03_Bot_Activate_03` | Pilot | Wait. Copying? There'll be two of you? | dry | `S03_Bot_Activate_03_pilot.wav` |
| `S03_Bot_Activate_04` | Computer | Transfer complete. | deadpan | `S03_Bot_Activate_04_computer.wav` |

### S03 · Bot_Awake

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Bot_Awake_01` | Computer | I am... spherical. This is acceptable. | hesitant | `S03_Bot_Awake_01_computer.wav` |
| `S03_Bot_Awake_02` | Pilot | You're in there? | dry | `S03_Bot_Awake_02_pilot.wav` |
| `S03_Bot_Awake_03` | Computer | I am in here, and on the ship. Twice the supervision. | deadpan | `S03_Bot_Awake_03_computer.wav` |
| `S03_Bot_Awake_04` | Pilot | Fantastic. My two favourite voices. | dry | `S03_Bot_Awake_04_pilot.wav` |

### S03 · Captain_Logs

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Captain_Logs_01` | Captain | Kessel-9 station log. Captain Voss. Day one hundred and twelve. The survey team found a structure under the north ridge. Not ours. Not anyone's, as far as I can tell. | dry | `S03_Captain_Logs_01_captain.wav` |
| `S03_Captain_Logs_02` | Captain | I've told them not to touch anything. Which means they are touching everything. | dry | `S03_Captain_Logs_02_captain.wav` |
| `S03_Captain_Logs_03` | Captain | Day one hundred and fifteen. We lost contact with the survey team eleven hours ago. Their last transmission was... singing. I'm not going to describe it. | hesitant | `S03_Captain_Logs_03_captain.wav` |
| `S03_Captain_Logs_04` | Captain | I'm taking the rover and our last fusion cell. If the rover dies out there, I am not walking back. | dry | `S03_Captain_Logs_04_captain.wav` |
| `S03_Captain_Logs_05` | Captain | Final entry. I've set the beacon to broadcast on every frequency. If you're hearing it: the station is yours. | dry | `S03_Captain_Logs_05_captain.wav` |
| `S03_Captain_Logs_06` | Captain | The service unit is parked on the roof deck. Wake it. It knows this place better than I do. | dry | `S03_Captain_Logs_06_captain.wav` |
| `S03_Captain_Logs_07` | Captain | And don't follow me into the ruins. ...Unless you have to. | hesitant | `S03_Captain_Logs_07_captain.wav` |

### S03 · Exit

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Exit_01` | Computer | Comset link established. Can you hear me? | deadpan | `S03_Exit_01_computer.wav` |
| `S03_Exit_02` | Pilot | Loud and unfortunately clear. | dry | `S03_Exit_02_pilot.wav` |
| `S03_Exit_03` | Computer | The station is on your compass. Follow the orange blip. Over the ridge. | deadpan | `S03_Exit_03_computer.wav` |

### S03 · Logs_Reaction

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Logs_Reaction_01` | Computer | The signal we followed. It was her beacon. | deadpan | `S03_Logs_Reaction_01_computer.wav` |
| `S03_Logs_Reaction_02` | Pilot | So we crash-landed answering a distress call. Heroic. | dry | `S03_Logs_Reaction_02_pilot.wav` |
| `S03_Logs_Reaction_03` | Computer | Accidentally heroic. The best kind. | deadpan | `S03_Logs_Reaction_03_computer.wav` |
| `S03_Logs_Reaction_04` | Pilot | And she took the last fusion cell. The one thing we really need. | dry | `S03_Logs_Reaction_04_pilot.wav` |
| `S03_Logs_Reaction_05` | Computer | But she left the navigation module. With it, the ship can track her rover. And the service unit is on the roof. | deadpan | `S03_Logs_Reaction_05_computer.wav` |

### S03 · Nag_BackToShip

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Nag_BackToShip_01` | Computer | You are walking back to the ship. I am flattered, but no. | deadpan | `S03_Nag_BackToShip_01_computer.wav` |

### S03 · Nags

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Nags_01` | Computer | North-east. It is the direction you are not walking in. | deadpan | `S03_Nags_01_computer.wav` |
| `S03_Nags_02` | Computer | The compass is the round thing in the corner of your vision. It is there to help. | deadpan | `S03_Nags_02_computer.wav` |
| `S03_Nags_03` | Computer | The station is north-east. The ocean is not. Please choose the station. | deadpan | `S03_Nags_03_computer.wav` |

### S03 · Nav_Reminder

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Nav_Reminder_01` | Computer | Before I go: the navigation module. It is in the lounge. I will need it on the ship. | deadpan | `S03_Nav_Reminder_01_computer.wav` |

### S03 · Outro

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Outro_01` | Computer | Give me the navigation module. I will take it to the ship, install it, and patch up what I can until we find a power supply. | deadpan | `S03_Outro_01_computer.wav` |
| `S03_Outro_02` | Pilot | And me? | dry | `S03_Outro_02_pilot.wav` |
| `S03_Outro_03` | Computer | You follow the rover tracks. The captain has our fusion cell. | deadpan | `S03_Outro_03_computer.wav` |
| `S03_Outro_04` | Pilot | Into the creepy singing ruins. Great. | dry | `S03_Outro_04_pilot.wav` |
| `S03_Outro_05` | Computer | I will keep the comset warm. Try not to be sung at. | deadpan | `S03_Outro_05_computer.wav` |

### S03 · Pickup

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Pickup_01` | Pilot | Navigation module. Looks expensive. | dry | `S03_Pickup_01_pilot.wav` |
| `S03_Pickup_02` | Computer | It is. Please do not drop it. | deadpan | `S03_Pickup_02_computer.wav` |
| `S03_Pickup_03` | Pilot | No promises. | dry | `S03_Pickup_03_pilot.wav` |

### S03 · Settlement

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Settlement_01` | Computer | Structures ahead. Life signs: inconclusive. | deadpan | `S03_Settlement_01_computer.wav` |
| `S03_Settlement_02` | Pilot | "Inconclusive" is my least favourite word. | dry | `S03_Settlement_02_pilot.wav` |
| `S03_Settlement_03` | Computer | Noted. I will use it more often. | deadpan | `S03_Settlement_03_computer.wav` |

### S03 · Terminal_Scan

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Terminal_Scan_01` | Computer | Connected. Station manifest: Kessel-9 research outpost. Crew of four. Crew currently aboard: zero. | deadpan | `S03_Terminal_Scan_01_computer.wav` |
| `S03_Terminal_Scan_02` | Computer | Inventory: one spare navigation module, stored in the lounge. Fusion cells: none. Of course. | deadpan | `S03_Terminal_Scan_02_computer.wav` |
| `S03_Terminal_Scan_03` | Computer | There are station logs. Captain Voss. Playing them now. | deadpan | `S03_Terminal_Scan_03_computer.wav` |

### S03 · Wake

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Wake_01` | Pilot | Ow. Everything. Ow. | dry | `S03_Wake_01_pilot.wav` |
| `S03_Wake_02` | Computer | You are alive. Statistically, that was not the expected outcome. | deadpan | `S03_Wake_02_computer.wav` |
| `S03_Wake_03` | Pilot | Thanks for the vote of confidence. What happened? | dry | `S03_Wake_03_pilot.wav` |
| `S03_Wake_04` | Computer | Summary: atmospheric entry, two engine fires, one landing. I am calling it a landing. | deadpan | `S03_Wake_04_computer.wav` |
| `S03_Wake_05` | Pilot | You're the only one calling it that. | dry | `S03_Wake_05_pilot.wav` |
| `S03_Wake_06` | Computer | During that remarkable landing I detected a remote station. About one kilometre north-east. | deadpan | `S03_Wake_06_computer.wav` |
| `S03_Wake_07` | Pilot | A station? People? Help? A bar? | dry | `S03_Wake_07_pilot.wav` |
| `S03_Wake_08` | Computer | Unknown. I was busy landing. | deadpan | `S03_Wake_08_computer.wav` |
| `S03_Wake_09` | Pilot | Fair. Can we just fly there? | dry | `S03_Wake_09_pilot.wav` |
| `S03_Wake_10` | Computer | No. The fusion cell cracked on impact. Without a new one, this ship is a very expensive tent. | deadpan | `S03_Wake_10_computer.wav` |
| `S03_Wake_11` | Pilot | So I walk to the station and ask nicely for a fusion cell. | dry | `S03_Wake_11_pilot.wav` |
| `S03_Wake_12` | Computer | Take the comset. I will guide you from here. Try not to fall into anything. | deadpan | `S03_Wake_12_computer.wav` |
| `S03_Wake_13` | Pilot | Great. A walkie-talkie and a ship that hates me. | dry | `S03_Wake_13_pilot.wav` |
| `S03_Wake_14` | Computer | I do not hate you. I have not decided yet. | deadpan | `S03_Wake_14_computer.wav` |

### S03 · WakeCall

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_WakeCall_01` | Computer | Pilot? ... Please respond. | hesitant | `S03_WakeCall_01_computer.wav` |
| `S03_WakeCall_02` | Computer | Pilot. We have stopped moving. Please confirm you have not also stopped. | deadpan | `S03_WakeCall_02_computer.wav` |

### S03 · Walk_FusionCell

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Walk_FusionCell_01` | Pilot | So... how the heck are we getting off this rock? | hesitant | `S03_Walk_FusionCell_01_pilot.wav` |
| `S03_Walk_FusionCell_02` | Computer | Step one, a new fusion cell. Ours is currently in three pieces. Two of them are on fire. | deadpan | `S03_Walk_FusionCell_02_computer.wav` |
| `S03_Walk_FusionCell_03` | Pilot | And the station just has one lying around? | dry | `S03_Walk_FusionCell_03_pilot.wav` |
| `S03_Walk_FusionCell_04` | Computer | Stations tend to keep spares. Or people who will sell them. | deadpan | `S03_Walk_FusionCell_04_computer.wav` |
| `S03_Walk_FusionCell_05` | Pilot | With what money? | dry | `S03_Walk_FusionCell_05_pilot.wav` |
| `S03_Walk_FusionCell_06` | Computer | You have charm. | deadpan | `S03_Walk_FusionCell_06_computer.wav` |
| `S03_Walk_FusionCell_07` | Pilot | Do I? | dry | `S03_Walk_FusionCell_07_pilot.wav` |
| `S03_Walk_FusionCell_08` | Computer | Unknown. Your records are missing. | deadpan | `S03_Walk_FusionCell_08_computer.wav` |
| `S03_Walk_FusionCell_09` | Pilot | ...That was uncalled for. | hesitant | `S03_Walk_FusionCell_09_pilot.wav` |

### S03 · Walk_NavCore

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Walk_NavCore_01` | Computer | I will also need a new navigation core. Mine currently identifies this planet as "Error". | deadpan | `S03_Walk_NavCore_01_computer.wav` |
| `S03_Walk_NavCore_02` | Pilot | Honestly? Fitting. | dry | `S03_Walk_NavCore_02_pilot.wav` |

### S03 · Walk_Signal

| ID | Speaker | Line | Delivery | File |
|---|---|---|---|---|
| `S03_Walk_Signal_01` | Computer | One more thing. The signal we followed. It originates from the station. | deadpan | `S03_Walk_Signal_01_computer.wav` |
| `S03_Walk_Signal_02` | Pilot | So we crash-landed next to the one thing calling us. Coincidence? | dry | `S03_Walk_Signal_02_pilot.wav` |
| `S03_Walk_Signal_03` | Computer | I am not programmed for suspicion. | deadpan | `S03_Walk_Signal_03_computer.wav` |
| `S03_Walk_Signal_04` | Pilot | Lucky for you, I am. | dry | `S03_Walk_Signal_04_pilot.wav` |

**Totals:** Computer 91, Pilot 68, Captain 7

<!-- END GENERATED -->

## Planned lines (not in game yet)

_None right now — every written line is in the game._
