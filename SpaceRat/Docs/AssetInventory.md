# Space Rat — Asset Inventory

Last updated: 2026-11-15 · Unity 6000.7.0b1 · URP 17.7.0

All three packs below are **Asset Store content under the Standard Unity Asset Store EULA**. They are
**git-ignored** because this repository is public and the EULA does not allow redistribution.
Vendor folders are never edited; all customisation lives under `Assets/SpaceRat/`.

## Restoring on a fresh clone

1. Open the project (the embedded URP Core fix makes it compile without these packs).
2. Package Manager → **My Assets** → download + import the three packs below.
   - **Simple Space Starter Kit:** its `.unitypackage` bundles a Unity 2022 package manifest
     (URP / Shader Graph / VFX Graph **14.0.8**). **Do not accept** any package changes it proposes —
     that would downgrade URP. In the import dialog, keep assets only. (VFX Graph is not needed.)
3. Run **SpaceRat → Convert Sparrow Fighter to URP** (regenerates `Assets/SpaceRat/Art/Sparrow/Sparrow_MetallicSmoothness.png`).
4. Run **SpaceRat → Build Scene01 Greybox** if the scene needs rebuilding.

Download cache is redirected off C: via user env vars `ASSETSTORE_CACHE_PATH=D:\UnityCache\AssetStore`
and `UPM_CACHE_ROOT=D:\UnityCache\upm`.

## Packs

| Asset | Publisher | Version | Path | Size | Pipeline status | Intended use |
|---|---|---|---|---|---|---|
| [Sparrow Fighter Spacecraft](https://assetstore.unity.com/packages/3d/vehicles/space/sparrow-fighter-spacecraft-224336) | CGPitbull | 1.0 (2022) | `Assets/Sparrow_Fighter` | 840 MB | Built-in "Autodesk Interactive" → **converted copies** in `Assets/SpaceRat/Art/Sparrow` | **Hero ship (blue)** — Scenes 1–2 |
| [SpaceSkies Free](https://assetstore.unity.com/packages/2d/textures-materials/sky/spaceskies-free-80503) | Pulsar Bytes | 1.0 (2017) | `Assets/SpaceSkies Free` | 483 MB | `Skybox/6 Sided` — works in URP as-is | Skybox (Pink / Green / Purple, 1K/2K/4K) |
| [Simple Space Starter Kit](https://assetstore.unity.com/packages/3d/vehicles/space/simple-space-starter-kit-305055) | Mwester Studios | 1.0 (2025) | `Assets/Mwester Studios` | 2.8 GB | URP Lit/Unlit + Shader Graph — ready | Star + planet (Atmosphere/Star shadergraphs), asteroids, station, SF-47 Viper fighters (backup ship), engine thruster particles |

### Notes per pack

**Sparrow Fighter** — 3 colour variants (blue / grey / orange) sharing one mesh set and PBR maps
(albedo, metallic, roughness, normal, AO, emissive, masks). URP versions:
`Assets/SpaceRat/Prefabs/Ship/Sparrow_{blue,grey,orange}_URP.prefab` (prefab variants of the vendor
prefabs with URP Lit materials). Metallic (R) + smoothness (A = 1 − roughness) are packed into one map
for URP. No scripts. Includes a demo scene and a PSD.

**SpaceSkies Free** — 3 skyboxes as 6-sided materials at 1K/2K/4K. Demo folder contains two small
scripts (`LookCamera.cs`, `SkyboxChanger.cs`) for its demo scene only; they use the legacy Input
Manager and are not used by Space Rat.

**Simple Space Starter Kit** — 4 showcase scenes, ~1,100 textures, 25 prefabs:
asteroids ×5, Planet 1, Star, Station, SF-47 Viper ×9 variants + weapon system, engine thruster
particle system. Shaders: `Star Shader.shadergraph`, `Atmosphere Shader.shadergraph`. Documentation
PDF included in the pack. No scripts.

## Licensing

Standard Unity Asset Store EULA (Extension Asset) for all three: use in your projects, including
commercial; no redistribution of the source assets (hence git-ignored). Credit not required but
appreciated.

## Planned / not yet imported

- **Sci-Fi Pistol [PBR]** — *downloaded, import when needed.* Pilot's default sidearm after Scene 1;
  fires laser shots (built in Assets/SpaceRat). Check URP + any bundled package manifest on import.
- **Too Many Crosshairs** — *downloaded, import when needed.* Crosshair art for on-foot first person.

- **Adventure Character** (Maksim Bugrimov) — humanoid for full-body first person after Scene 1;
  rig type and pipeline unverified.
- **Starter Assets – First Person / Third Person** (Unity) — controllers for after Scene 1; check
  Cinemachine 3 compatibility.
- Asteroid pack and Procedural Planets (see `Plans/Links.txt`) — optional.
