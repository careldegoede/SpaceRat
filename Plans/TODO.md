# Space Rat — TODO / review notes

## Scene 3

- [x] **Review compass + outpost location** — fixed 2026-11-16: radar-style blip (moves in as you approach), hidden within 50 m, solid backing behind the readout (2026-11-16, screenshot `Images/Screenshot 2026-11-16 171030.png`).
  Standing in front of the outpost, the compass says "STATION 22 M" but the blip sits upper-left (near N)
  instead of straight ahead. Check:
  - Is the target (`OutpostGameplay/OutpostEntrance` = MyOutpost's `door_3`) on the side of the building
    rather than the front the player walks up to? If so, move the marker to the real entrance / building centre.
  - Is the blip bearing correct (dial rotation vs. `viewer.eulerAngles.y`, blip angle = bearing − yaw)?
  - Does the outpost itself sit where the walk / ridge / 330 m "Structures ahead" trigger expects
    (MyOutpost is ~50 m off the old `OutpostSite` pad)?
  - Side panel text renders correctly in game (confirmed in the screenshot).

- [ ] **Terminal seat view** (2026-11-16, screenshot `Images/Screenshot 2026-11-16 171204.png`):
  - [x] Camera pulled back/up; monitors now show live sci-fi text (`TerminalScreen`: standby idle,
    typed scan + captain's logs, station status on the right monitor).
  - (was) `ConsoleCamera` is too close/low: monitors fill the frame, keyboard under the nose. Pull back and up so
    it reads as sitting in the chair looking at the desk with both screens.
  - Monitors are black: show the log on them while it plays (emissive screen: "STATION LOG · KESSEL-9",
    waveform / scrolling text), screens off again afterwards.
  - Grainy white speckle on the modular pack surfaces (noisy `base-color-normal` map) is very visible up
    close — fix once for all scenes (e.g. tone down the normal / smoothness on our URP copies).
  - [x] Dialogue text ran past the box with the default font (screenshot 192130) — styles now built from
    scratch and self-repairing (our fonts, word wrap).
  - Keep: the orange under-desk glow / dark station mood.
