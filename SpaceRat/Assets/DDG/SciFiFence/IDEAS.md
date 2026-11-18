# Sci-Fi Fence — Ideas

Prefabs: `SciFiFence_Panel` (includes its start post) and `SciFiFence_Post` (end post).
Use with FenceLayout: Panel → Fence prefab 0, Post → End post prefab, then click **Calculate length & end offset from prefabs** (gives length 2.5, end offset Z 1.25).

## Visual polish
- [ ] Enable Bloom in the URP Volume so the glow materials pop
- [ ] Animated energy field: scroll or pulse emission (small shader or script)
- [ ] Beacon blink on post caps
- [ ] Replace primitive cubes with proper modelled meshes (bevels, panel lines)
- [ ] Trim texture / decals (hazard stripes, serial numbers)

## Variants
- [ ] Colour variants: red (locked / danger), green (safe), amber (warning)
- [ ] Damaged panel: flickering field, missing beams, sparks — mix in via extra fence prefab slots
- [ ] Low wall version (no field, half height) for walkways
- [ ] Corner / junction post with a bigger beacon

## Gameplay
- [x] Alarm mode: `FenceAlarm` on every piece flashes glow + field blue ↔ red (with flicker); `FenceAlarmController` on a fence root toggles a whole fence (`SetAlarm` / `ToggleAlarm`)
- [ ] Alarm siren audio + link alarm to a trigger / terminal
- [ ] Field can be switched off (terminal, power switch or door-lock link)
- [ ] Damage or knockback on contact while powered
- [ ] Hum audio loop near the field, zap sound on contact
- [ ] Gate panel that opens and closes

## Tech notes
- Glow parts don't cast shadows; one BoxCollider per prefab
- Materials: `M_SciFiFence_Metal`, `_Trim`, `_Glow` (emissive), `_Field` (transparent, double-sided)
