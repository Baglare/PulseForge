# PulseForge UI — "Forge at Night"

Runtime UI (uGUI, legacy `Text`) for menus, briefing and HUD. The look is applied at runtime by
`PulseForgeGameSkin.Apply(root)` from `PulseForgeUIBootstrap`, so it restyles the scene-authored
hierarchy without editing or saving the scene.

## Idea

A forge at night that breathes to a beat: warm blackened iron, molten ember light rising from the floor,
and concentric heat rings that pulse slowly on the menus. While a song is playing the song keeps the beat,
so the rings freeze and dim.

## Tokens (`PulseForgeUITheme`)

| Role | Value | Use |
|---|---|---|
| Backdrop | `#0B0908` | Screen ground |
| Surface / Raised / Soft | `#17120F` / `#221A15` / `#2C221B` | Plates, rows, segments |
| Border | `#45362B` | Plate frames |
| Primary text | `#F5ECE2` | Titles, values |
| Secondary text | `#A9988A` | Labels, helper copy |
| Ember (Primary) | `#FF7A1F` | The single action per screen, selected segments, accents |
| Ember deep / Heat | `#C23B0C` / `#FFB84D` | Floor heat, ring tones, score glow |
| Guard / Strike | `#35D3F3` / `#FF7547` | Gameplay semantics only — never menu decoration |
| Perfect / Good / Miss | `#FFD166` / `#50D890` / `#FF4F61` | Judgement only |

## Components

- **Plate**: a 9-sliced procedural sprite with the top-left and bottom-right corners cut
  (`PulseForgeSkinAssets.Plate`), plus a stroke-only frame child (`Skin Frame`). No rounded corners.
- **Accent**: a 4px ember bar on the top edge of every card (`Skin Accent`); a 5px left bar on the track slot.
- **Buttons**: Primary = ember fill, dark text, heat frame. Secondary = raised iron. Subtle = no frame,
  secondary text. Segment = dim iron; Selected segment = ember-tinted fill with ember frame.
  The graphic stays white and the `ColorBlock` carries the exact colour.
- **Type**: Bahnschrift (OS font, Windows 10+) with fallback to the built-in font. Title 118 bold caps,
  section headers 26–30 caps, rows 18–22, body 17–24.

## Layout rules

- Main menu: identity + track + the forge action on the left; a single **LOADOUT** plate on the right
  (Difficulty, Game Mode, Combat Style) with a quieter **TUNING** group (Coverage, Timing Assist, Detection).
  Selector rows are horizontal: label left, options right.
- Ready: wide briefing plate — song and analysis stats left, loadout right, Start as the largest element.
- Results: the total score is the hero (64 heat with ember glow); tallies on the right; Replay is primary.
- One primary (ember) action per screen. Everything fits 1920×1080 and 1920×1200 without scrolling.
