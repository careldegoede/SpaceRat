# HUD Bar — redesign prompt

Replacement for `Images/HUD Bar.png`. Same look and layout as the current image; the changes are
the **STAMINA** label and a few production details so it drops straight into the game
(`Art/HUD/HudBar.png`, bars filled by code).

## Prompt

> Futuristic sci-fi game HUD status bar, wide horizontal panel, brushed chrome / gunmetal frame with
> warm gold-orange glowing edge lights, angular chamfered ends, premium AAA game UI style.
> Left: a circular gold-ringed portrait badge with a dark, rim-lit silhouette of a bald, muscular man's
> head and shoulders (the pilot).
> Right of the portrait, three equal sections side by side, each with a small glowing icon and a bold
> italic white sans-serif label above an **empty** bar slot:
> 1. red heart-with-heartbeat icon, label **"HEALTH"**, red neon outlined slot;
> 2. green running-man icon, label **"STAMINA"**, green neon outlined slot;
> 3. blue blaster-pistol icon, label **"WEAPON CHARGE"**, blue neon outlined slot ending in a small gold
>    octagon badge with a blue lightning bolt.
> Bar slots are slanted parallelograms (leaning right) with thin glowing outlines and **completely
> empty, flat pure-black interiors** — no fill, no gradient, no texture inside the slots.
> Pure black background (#000000) around the whole panel, no scenery, no extra text, no watermark,
> straight-on front view, no perspective, crisp edges, high detail.

## Sizes

| What | Size |
|---|---|
| Canvas (the image file) | **2172 × 724 px** (3:1). Larger is fine if the proportions stay the same |
| Panel inside the canvas | ≈ 2164 × 430 px, centred vertically (black above/below) |
| Portrait circle | ≈ 285 px diameter, centre ≈ (250, 382) |
| Bar slots | ≈ 66 px tall, y ≈ 345–415 |
| — Health slot | x ≈ 420–885 (≈ 465 px wide) |
| — Stamina slot | x ≈ 935–1450 (≈ 515 px wide) |
| — Weapon slot | x ≈ 1500–1955 (≈ 455 px wide), then the bolt badge |
| Labels | cap height ≈ 32 px, top at y ≈ 280 |
| **In-game** | top-left corner; at 1920×1080 ≈ **700 × 140 px** (≈ 36 % of screen width), scales with screen height (4K ≈ 1400 × 280) |

## Requirements (for the game, please keep)

- **Size:** 2172 × 724 px, the panel centred, same proportions as the current image.
- **Layout:** keep the portrait, the three sections and the slots where they are now. The bar slots sit
  at roughly y 345–415 px:
  - health ≈ x 420–885
  - stamina ≈ x 935–1450
  - weapon ≈ x 1500–1955, ending at the bolt badge.

  If the layout changes, that's fine. The slot shapes just get re-measured.
- **Slot interiors:** pure black and empty. The code cuts them out and draws animated fills underneath.
- **Background:** pure black (it gets keyed out to transparent). Avoid black inside the frame body
  except in the slots.
- **Labels:** all three labels in the same font, weight, italic and size.
- **No baked-in values:** no numbers, percentages or fill.

## Optional extras (nice to have)

- A second version with the slot outlines dimmed/greyed for **WEAPON CHARGE**, used before the pilot
  has a pistol, or I can dim it in code.
- The portrait on its own (same circle, transparent outside), so it can flash red when he's hit.
