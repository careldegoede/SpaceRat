# SPACE RAT — Scene 3: The Crash Site

## Purpose
Direct continuation of Scene 2's cut to black ("Pilot? ... Please respond."). The pilot wakes in the wrecked
cockpit, learns there may be a settlement nearby, takes a comset so the computer can stay in contact, and walks
there on foot in first person. The scene introduces walking/first-person play and seeds the next goal: **parts to fix
the ship**. End when the settlement comes into view.

**Tone:** standing rule — sharp, sarcastic pilot; deadpan computer with dry jabs. Identity mystery stays intact.
**Target first prototype:** 5–7 minutes. **No combat yet** (laser pistol comes later).

## Scene outline

### 1. Wake up in the wreck (cinematic, cockpit)
- Fade in from black on the Scene 1 cockpit room, now wrecked: emergency red light, sparks, light smoke, a cracked
  console screen, the seat tilted. Reuse the Scene 1 wake-up blink (eyes-closed volume + eyelid).
- Pilot (Man_03) slumped in the pilot_seat (Mixamo Sitting Idle + slump), then sits up.
- Auto-timed dialogue (Space skips in dev).

**Dialogue (draft)**

**COMPUTER:** Pilot? ... Please respond.
**PILOT:** Ow. Everything. Ow.
**COMPUTER:** You are alive. Statistically, that was not the expected outcome.
**PILOT:** Thanks for the vote of confidence. Where are we?
**COMPUTER:** On the planet. Specifically, in a hole we made in the planet.
**PILOT:** Comforting. Anything out there? People? Help? A bar?
**COMPUTER:** During descent I detected structures roughly two kilometres north-east. Possibly a settlement.
**PILOT:** Possibly?
**COMPUTER:** I was busy crashing.
**PILOT:** Fair. Can you call them?
**COMPUTER:** Long-range communications are damaged. Take the comset from the console. I can reach you through it. It may also reach whoever lives there.
**PILOT:** Great. A walkie-talkie and a ship that hates me.
**COMPUTER:** I do not hate you. I have not decided yet.

### 2. Outside the wreck (cinematic → first person)
- Fade out, fade in: exterior, the pilot standing beside the crashed blue Sparrow in the crater — smoke, scorched
  ground, debris trail, the ship half-buried and tilted.
- Third-person establishing shot, then the camera **moves smoothly into the pilot's head → first-person mode**
  (full-body FPS: Character Viewer head-bone technique, Kevin Iglesias idle/walk/run on the Humanoid).
- Computer now speaks **through the comset** (subtitle label "COMSET", radio-filtered voice).

**COMPUTER (comset):** Comset link established. Can you hear me?
**PILOT:** Loud and unfortunately clear.
**COMPUTER (comset):** Head north-east. Follow your compass. Try not to fall into anything.

### 3. The walk (playable, first person)
- **Controls:** WASD walk, mouse look, Shift run (Unity Input System, rebindable). Gamepad later.
- **Compass HUD:** NSEW tape across the top using `Images/Futuristic Cyan-Orange Sci-Fi Compass HUD.png`
  (black cut out like the radar). Heading readout in degrees.
- **Settlement is out of view at the start** (behind a ridge, ~1.5 km north-east). It **appears on the compass/HUD**
  as a marker only once in detection range (or after a computer "ping" mid-walk), with distance. The structures
  themselves become visible when the pilot crests the ridge.
- **No fail state.** Wandering off course → dry comset nags (rate-limited), like Scene 2.
- Optional: pick up a scrap/part on the way (seeds the parts quest).

**Walking banter (spread over the walk, triggered by distance travelled)**

**PILOT:** So... how the heck are we getting off this rock?
**COMPUTER:** The ship requires a new reactor regulator. Ours is currently in three pieces. Two of them are on fire.
**PILOT:** And where do we get one of those?
**COMPUTER:** Settlements tend to have spare parts. Or people who will sell them.
**PILOT:** With what money?
**COMPUTER:** You have charm.
**PILOT:** Do I?
**COMPUTER:** Unknown. Your records are missing.
**PILOT:** ...That was uncalled for.

**COMPUTER:** I will also need a new navigation core. Mine currently identifies this planet as "Error".
**PILOT:** Honestly? Fitting.

**COMPUTER:** One more thing. The signal we followed. It originates from the settlement.
**PILOT:** So we crash-landed next to the one thing calling us. Coincidence?
**COMPUTER:** I am not programmed for suspicion.
**PILOT:** Lucky for you, I am.

**Off-course nags (comset, randomised, rate-limited)**
- "North-east. It is the direction you are not walking in."
- "The compass is the round thing at the top of your vision. It is there to help."
- "You are walking back to the ship. I am flattered, but no."

### 4. The settlement (end of scene)
- Cresting the ridge: the settlement comes into view (greybox structures from the Sci-Fi Styled Modular Pack —
  walls, doors, lights, a tall antenna/beacon blinking at the signal frequency).
- HUD marker: **SETTLEMENT — 300 m**.

**COMPUTER:** Structures ahead. Life signs: inconclusive.
**PILOT:** "Inconclusive" is my least favourite word.
**COMPUTER:** Noted. I will use it more often.

- Fade out → Scene 4 handoff.

## Systems / reuse

| Need | Plan |
|---|---|
| Island terrain, crater, water, sky | Scene 3 terrain test → island with ocean (in progress), Terrain Sample layers, Free Real Skies tinted |
| Wrecked cockpit | Scene 1 cockpit room (modular pack) with damage: red light, sparks, smoke, tilted seat |
| Seated / waking pilot | PilotSeating (Mixamo Sitting Idle + slump) |
| Crashed ship | Blue Sparrow URP prefab, tilted in the crater, smoke particles, scorch layer, debris |
| On-foot player | Adventure Character Man_03 + CharacterController + Kevin Iglesias idle/walk/run; full-body FPS camera (head bone collapse) |
| Compass HUD | New `CompassHud` (scrolling NSEW tape + markers) from the cyan-orange compass image |
| Dialogue / voice | DialogueSequence assets in `ScriptableObjects/Dialogue/Scene03`; Voice.md export; COMSET speaker tag |
| Director | `Scene03Director`: WakeUp → Briefing → ExitFade → ThirdToFirst → Walk → SettlementInView → Complete |

## Open questions (to decide before building)
1. **Time of day** on the planet: day, dusk or night? (affects sky choice, lighting, mood)
2. **Settlement:** inhabited, abandoned, or "lights on, nobody home"? (Scene 4 hook)
3. **Walk length:** ~1.5 km is ~3–4 min walking, ~1.5 min running. Shorter?
4. **Comset as an object:** visible on the pilot's wrist/belt, or just implied by the "COMSET" subtitle?
5. **Pick up a part on the way?** (small first taste of the parts quest)
6. **Third person allowed** during the walk (camera toggle), or first person only?
