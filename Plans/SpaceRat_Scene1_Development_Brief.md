# SPACE RAT — Scene 1: Awakening

## Goal
Build a small playable cinematic introduction in Unity. A damaged spacecraft tumbles toward a sun; its onboard computer cannot access flight systems and wakes an amnesiac pilot. The player eventually takes manual control and narrowly escapes. **The escape must be playable, not just a cutscene.**

## Decisions (2026-11-15) — these override conflicting text below
- **Environment:** Unity 6000.7.0b1 (beta), URP 17.7.0, Input System 6.7.0, uGUI 2.7.0 (TMP). Target: **Windows PC** only.
- **Build fix applied:** `com.unity.pipeline` 1.0.0 (Unity AI bridge — required, keep) moved CLI attributes into `Unity.Pipeline.Attributes`; builtin URP Core 17.7.0's CLI asmdef didn't reference it. URP Core is now **embedded** at `Packages/com.unity.render-pipelines.core` with that reference added (same fix as NoClue). Remove the embedded copy once Unity ships a fixed URP Core.
- **Camera:** add **Cinemachine**.
- **Docs:** `Docs/` lives in the Unity project root (`SpaceRat/SpaceRat/Docs`).
- **Ownership:** Claude builds all Scene 1 code incl. dialogue UI. Codex is used separately for UI polish on Claude's recommendations.
- **Assets:** **greybox first** (primitives, emissive sun, procedural skybox, placeholder audio); swap in Asset Store content after the loop works. Unity caches redirected to D: (`ASSETSTORE_CACHE_PATH=D:\UnityCache\AssetStore`, `UPM_CACHE_ROOT=D:\UnityCache\upm`).
- **Source control:** user is creating a blank GitHub repo; init with Unity `.gitignore` once available.
- **Flow:** Beats 1–2 exterior cinematic → on wake-up, cut **into cockpit view**: `Images/Futuristic Cockpit Control Console.png` (RGBA, transparent windows) as a full-screen overlay with the 3D sun visible through the windows.
- **Controls:** W = nose down / S = nose up (flight-sim, same as Scene 2), A/D yaw; sluggish with drift. No other player input except a dev skip key.
- **Difficulty:** tense, ~25 s escape window, narrow corridor.
- **Failure:** crash into sun → **restart the whole scene** (cinematics skippable).
- **Dialogue:** auto-timed lines (Space skips ahead for testing).
- **Post FX:** URP Volume — DoF/vignette for wake-up blur, bloom for the sun, heat-driven color tint.
- **Acceptance (added):** a fresh clone opens with zero compile errors.
- **Asset packs chosen:** Simple Space Starter Kit, Sparrow Fighter Spacecraft (**hero ship: blue Sparrow**, URP prefab `Assets/SpaceRat/Prefabs/Ship/Sparrow_blue_URP.prefab`), SpaceSkies Free (**skybox: Green 2K**). See `Plans/Links.txt`.
- **Dialogue tone (standing rule):** pilot ↔ computer lines are **sharp, funny, sarcastic** — mostly from the pilot; the computer is a deadpan straight man with the occasional dry jab. Keep plot beats and the identity mystery intact. Beats 1–3 live in `Assets/SpaceRat/ScriptableObjects/Dialogue/`. Planned Beat 4–5 lines:
  - Computer: "Manual control link established." / "Ease us away from the star. Gently. Unlike your wake-up." / "Heat levels critical. Keep turning!"
  - Pilot (while steering): "I'm turning! Is there a faster turning?"
  - Computer: "Collision course avoided." · Pilot: "You're welcome. Now tell me who I am." · Computer: "I am unable to answer that question." · Pilot: "Of course you are."
  - On crash/restart: Computer: "Impact." (fade to white)
- **After Scene 1:** use Unity's **Starter Assets – First Person Character Controller** for walking around (check Cinemachine 3 compatibility on import). Not used in Scene 1 — the pilot stays seated.

## Development tools
- **Claude Code:** Unity C# gameplay, scene assembly scripts, dialogue, camera and state machine.
- **Codex CLI:** independent build-error diagnosis, reviews, editor utilities, asset integration and tests.
- **Image generation:** 2D pilot/computer portraits, UI artwork and visual references. Images are not automatically usable 3D models.
- **Unity Editor:** import assets, build scenes, animation, lighting, audio and playtesting.

## First: fix the Unity project
1. Record Unity Editor version, render pipeline (URP/HDRP/Built-in), target platform and package versions.
2. Collect the **first** compiler error and the complete Console log. Diagnose the earliest root cause before fixing secondary errors.
3. Check Package Manager and `Packages/manifest.json` for Unity AI-related packages, dependencies and version compatibility. The user intentionally enabled Unity AI communication features: **do not remove or disable them without approval**.
4. Distinguish C# compiler errors, assembly definition errors, package-resolution failures, editor-only API leakage and runtime exceptions.
5. Make minimal, reversible fixes; verify a clean editor compile and Play Mode before proceeding.
6. Keep credentials, tokens and local configuration out of source control.

## Inventory purchased Unity Asset Store assets
Claude/Codex cannot assume access to the user's purchases. Inventory assets **already imported** under `Assets/`, plus any packages the user chooses to import through Package Manager > My Assets. Check licenses and compatibility. Prioritize reuse for:
- Small spacecraft exterior and cockpit interior
- Sun/star shader, skybox, space environment
- Particle effects, sparks, smoke, warning lights
- UI panels, fonts, portraits and icons
- Alarm, engine, voice and ambient audio

Create `Docs/AssetInventory.md` with asset name, version, path, intended use, dependencies and licensing notes. Do not overwrite vendor files; put customizations in a separate `Assets/SpaceRat/` folder.

## Scene 1 — narrative sequence

### Beat 1: External emergency (cinematic)
A small ship travels through empty space toward a giant sun. Alarms grow louder. External camera tracks the ship as it spins and drifts off course. Red lights flash through the cockpit windows.

**Computer:** "Warning. Stellar collision trajectory detected."
**Computer:** "Attempting automatic course correction."
**Computer:** "Flight control interface unavailable."
**Computer:** "Pilot response required."

### Beat 2: Wake-up attempt (cinematic)
Cut between ship exterior, empty-looking cockpit and a pilot strapped into the chair. The computer repeats increasingly urgent wake-up calls. Screen initially blurred/dark from the pilot's perspective.

**Computer:** "Pilot. Wake up."
**Computer:** "Pilot, respond."
**Computer:** "Impact probability increasing."

### Beat 3: Amnesia (dialogue)
The pilot opens their eyes; warning indicators become visible. Keep the mystery: do not explain the pilot's identity or the ship's mission yet.

**Pilot:** "What...? Where am I?"
**Computer:** "You are aboard this vessel. We are on a collision course with a star."
**Pilot:** "Who am I? What happened?"
**Computer:** "Your identity records are currently inaccessible."
**Pilot:** "Then change course!"
**Computer:** "Unable to comply. Flight control systems are not responding to my commands."
**Pilot:** "Can I steer it?"
**Computer:** "Manual controls may still function. You must try."

### Beat 4: Manual recovery (playable)
Prompt: **TAKE MANUAL CONTROL**. Player gets yaw/pitch control (keyboard and gamepad if available). Start with sluggish handling, unstable roll and strong visual/audio feedback. Keep the controls approachable; avoid realistic orbital mechanics in V1.

**Computer:** "Manual control link established."
**Computer:** "Ease the ship away from the stellar core."
**Computer:** "Heat levels critical. Keep turning!"

### Beat 5: Escape (playable to cinematic)
When the ship reaches a safe trajectory, alarms gradually stop. Camera pulls back to show the ship skimming past the sun and flying into darker space.

**Computer:** "Collision course avoided."
**Pilot:** "Now tell me who I am."
**Computer:** "I am unable to answer that question."

Fade out: **SPACE RAT**. Leave the larger mystery for Scene 2.

## Dialogue UI
Anchor a single reusable dialogue panel to the **bottom center** of the screen with safe margins. Recommended layout:

```text
+-----------------------------------------------------------------------+
| [  PORTRAIT  ]  SPEAKER NAME                                           |
| [  1:1      ]  Dialogue text flows here, 1–3 lines...                 |
| [  IMAGE    ]                                            [ NEXT >> ]   |
+-----------------------------------------------------------------------+
```

- Left: square portrait (computer avatar initially; pilot portrait later).
- Right: speaker name, dialogue text, optional continue indicator.
- Semi-transparent dark panel with restrained cyan accents; readable against bright sun and dark space.
- Responsive Canvas scaling; support 16:9 and narrower aspect ratios.
- TextMeshPro for uGUI implementation (or UI Toolkit if the project already standardizes on it).
- Dialogue advances by button/click/Enter/Space, except time-critical automatic announcements.
- Support optional typewriter effect, subtitles, voice clip, speaker ID, portrait and future branching choices.
- Ensure the panel does not block flight indicators once control begins; switch to compact radio subtitles if necessary.

## Scene architecture
Suggested project layout (adapt to existing project conventions):

```text
Assets/SpaceRat/
  Art/Portraits/
  Art/UI/
  Audio/Alarms/
  Audio/Voices/
  Prefabs/Ship/
  Prefabs/UI/
  Scenes/Scene01_Awakening.unity
  Scripts/Scene01/
  Scripts/Dialogue/
  Scripts/Flight/
  ScriptableObjects/Dialogue/
Docs/
  AssetInventory.md
  Scene01_TestPlan.md
```

Recommended systems:
- `Scene01Director`: explicit states `ExteriorEmergency`, `WakePilot`, `Dialogue`, `ManualRecovery`, `Escape`, `Failure`.
- `DialogueController`: renders speaker, portrait and lines from data (prefer ScriptableObjects or JSON, not hardcoded UI strings).
- `CinematicCameraController`: deterministic scripted camera shots and transitions; use Cinemachine if compatible and already available.
- `ShipFlightController`: simplified manual pitch/yaw, smoothing and configurable control authority.
- `ShipEmergencyEffects`: alarms, flashing lights, vibration/camera shake and heat visuals.
- `CollisionCourseEvaluator`: measures player progress against a **designed escape corridor**, not physically accurate stellar gravity.
- `InputRouter`: maps keyboard/gamepad through Unity Input System if installed.

Keep cinematic ship tumbling separate from manual control. During the cinematic, animate a controlled trajectory or scripted transform; on takeover, transfer pose/velocity smoothly and enable the flight controller. Avoid simultaneously driving the same transform from Timeline, physics and gameplay scripts.

## Gameplay rules for prototype
- Initial camera sequence is scripted and repeatable.
- The player cannot steer until the takeover prompt.
- Show simple directional guidance (e.g., reticle/escape corridor marker).
- Provide a generous initial escape window; tune difficulty only after playtesting.
- Success: ship is heading away from the sun and crosses a safe exit trigger.
- Failure: enters the star hazard zone or escape timer expires; offer **Retry Manual Recovery**.
- Allow skipping previously watched cinematic segments for testing.
- No full damage, orbital simulation, inventory, combat or procedural universe in Scene 1.

## Image handoff to Codex
Place downloaded image files into these paths, with descriptive filenames:

| File | Purpose | Suggested size |
|---|---|---|
| `Art/Portraits/computer_avatar.png` | Onboard AI portrait | 1024x1024, square |
| `Art/Portraits/pilot_portrait.png` | Pilot portrait, initially mysterious | 1024x1024, square |
| `Art/UI/dialogue_panel_reference.png` | UI visual reference, **not** necessarily final nine-slice artwork | 1920x1080 |
| `Docs/References/scene01_cockpit_mockup.png` | Cinematic lighting/composition reference | 16:9 |

**Important:** A screenshot/mockup is a reference, not a working Unity scene or a properly sliced UI asset. Codex should rebuild the dialogue panel using Unity UI elements. Keep original image aspect ratios; use alpha transparency for final portrait/icon assets when appropriate.

### Suggested image prompts
**Computer portrait:** "Square 1:1 holographic onboard spaceship computer avatar, friendly but precise minimalist cyan geometric face, dark transparent or easily removable background, readable at 128px, clean science-fiction UI portrait, no text."

**Pilot portrait:** "Square 1:1 portrait of an exhausted unidentified spacecraft pilot in a worn flight suit, dramatic red cockpit emergency lighting, grounded cinematic science fiction, face partly shadowed, no text."

**Dialogue panel concept:** "16:9 clean futuristic video game dialogue UI concept, bottom-center translucent charcoal panel, square character portrait on left, speaker name and three lines of dialogue on right, restrained cyan edge lighting, no unrelated HUD clutter."

## Claude Code implementation request
> Inspect the existing Unity project and imported assets before writing code. Fix the existing build errors, paying special attention to enabled Unity AI packages and compatibility; do not disable them without permission. Implement Scene 1 of Space Rat as a playable prototype using reusable dialogue, scene-state, camera and manual-flight components. Use imported Asset Store assets where appropriate. Keep code under `Assets/SpaceRat`, avoid vendor modifications, document setup steps and validate compilation and Play Mode. Ask before introducing major packages or changing render pipeline.

## Codex CLI review request
> Review the Unity Space Rat Scene 1 implementation. Check compiler errors, package/assembly references, null safety, lifecycle and event cleanup, Input System compatibility, camera-to-flight handoff, scene references and failure/retry flows. Inventory and integrate provided image assets into the appropriate project folders. Recreate the dialogue UI as functional Unity controls rather than placing a flat screenshot on screen. Produce a concise report of changes, unresolved issues and manual editor steps.

## Milestones
1. **Project health:** compile without errors; preserve intentional Unity AI integration.
2. **Asset inventory:** identify usable owned/imported assets.
3. **Greybox cinematic:** ship, sun, camera motion, alarms.
4. **Awakening/dialogue:** portrait panel and scripted dialogue.
5. **Playable escape:** manual control, guidance, success/failure/retry.
6. **Polish:** artwork, lighting, sound, transitions and tuning.

## Acceptance checklist
- [ ] Project compiles cleanly with Unity AI integration intact where feasible.
- [ ] Existing compatible Asset Store assets reused where appropriate.
- [ ] Opening camera clearly shows ship spiraling toward a sun.
- [ ] Computer alarms and wake-up attempts occur in order.
- [ ] Pilot appears confused and does not know their identity.
- [ ] Dialogue panel swaps computer/pilot portraits and text correctly.
- [ ] Manual takeover is explicit and transition is smooth.
- [ ] Player can steer to safety using documented controls.
- [ ] Success, failure and retry work reliably.
- [ ] Scene can be run from a fresh editor launch without hidden setup.

## Reference
Use the generated **Space Rat Scene 1 cockpit mockup** from the conversation as a *visual direction reference*. The user will download that image separately and supply it to Codex. Do not treat the mockup's text or controls as a binding implementation specification.
