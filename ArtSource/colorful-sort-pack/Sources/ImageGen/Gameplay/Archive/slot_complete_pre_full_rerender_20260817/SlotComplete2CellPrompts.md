# Complete one-piece two-cell slot — built-in ImageGen prompts

The two raw files were generated with two separate built-in ImageGen calls, one
per distinct variant. Each raw is a complete uninterrupted render; no modular
top/middle/bottom artwork is composited into either final.

## Normal — `slot_complete_2cell_raw.png`

```text
Use case: stylized-concept
Asset type: Unity 2D world-space one-piece raster sprite, complete two-cell sorting slot
Primary request: Generate ONE complete, continuous, very tall narrow two-cell slot as a single uninterrupted render. It must be one physical toy-plastic object from crown through side rails, cavity, four-stud seat, and bottom lip—NOT top/middle/bottom modules and NOT a collage.
Input images: Image 1 is the authoritative bottom-seat shape reference (2x2 studs and lip); Image 2 is the authoritative continuous side-rail, cavity and short recessed-groove reference; Image 3 is the authoritative shallow two-lobe crown silhouette; Image 4 is palette/material reference only—ignore its montage layout and all modular join lines.
Scene/backdrop: genuine transparent RGBA outside the single object, no checkerboard, no colored backdrop, no cast shadow.
Subject: one centered empty front-facing sorting tray exactly TWO cells tall. The narrow side rails run completely unbroken from the crown shoulders to the bottom seat. The recessed back/cavity is one continuous uninterrupted surface.
Style/medium: polished 2.5D casual mobile-game raster sprite, smooth molded toy plastic, strict front-facing orthographic view, symmetric.
Composition/framing: very tall narrow portrait; complete object fully visible; outer slot width about 65% of canvas; generous transparent padding on all sides. Crown at top, long cavity, 2x2 stud seat and dark front lip at bottom.
Lighting/mood: one consistent soft upper-left light over the entire object. Rail thickness, bevel, inner trough, highlight width and material must remain identical at every height.
Color palette: deep navy-indigo cavity #24275F / #2D2F63; dark rail trough #26235B; violet rails #494582 with restrained #7259FF highlight.
Materials/textures: continuous smooth toy plastic, subtle depth, no grain.
Constraints: exactly TWO shallow broad crown lobes with one moderate shallow center valley; exactly TWO evenly spaced short thin horizontal recessed groove markers in the cavity, one per cell; exactly FOUR low flat elliptical studs at bottom arranged 2 back plus 2 front; one integrated dark violet bottom lip. The rails and cavity must have absolutely no join seams, no thickness shifts, no highlight shifts, no tonal bands, no breaks. The two groove markers are the only horizontal lines in the cavity.
Avoid: separate modular pieces, segment boundaries, horizontal join bands, full-width crossbars, broken rails, changing rail width, changing lighting, heart-shaped tall crown, third lobe, extra studs, cubes, colored blocks, symbols, text, watermark, ice, frost, cyan, perspective, isometric yaw, background glow.
```

Reference roles, in order:

1. `codex-clipboard-4bde352d-9a7d-4839-b6e3-c25eeec5baf7.png` — bottom seat/lip shape.
2. `codex-clipboard-1d13a53e-f014-4b22-88dd-5151dea285e6.png` — rail/cavity/groove.
3. `codex-clipboard-0c9a3ea2-7823-4ac8-babc-2692dded7ffa.png` — shallow crown.
4. `SlotStudRevisionPreview.png` — palette/material only.

## Frozen — `slot_ice_complete_2cell_raw.png`

```text
Use case: stylized-concept
Asset type: Unity 2D world-space one-piece raster sprite, complete frozen two-cell sorting slot
Primary request: Generate ONE complete, continuous, very tall narrow FROZEN two-cell slot as a single uninterrupted render. Preserve the complete normal slot geometry shown in Image 1, but render a separate final frozen sprite. It must remain one physical toy-plastic object from crown through unbroken rails, continuous cavity, four-stud seat, purple front lip and attached ice—NOT modular pieces and NOT a collage.
Input images: Image 1 is the authoritative whole-slot geometry and continuity reference; Image 2 is the authoritative bottom-seat 2x2-stud/lip reference; Image 3 is the authoritative rail/cavity/groove reference; Image 4 is the authoritative shallow crown reference; Image 5 is palette/material and bottom-ice style reference only—ignore its montage layout and segment joins.
Scene/backdrop: genuine transparent RGBA outside the single object, no checkerboard, no colored backdrop, no cast shadow.
Subject: one centered empty front-facing sorting tray exactly TWO cells tall. Narrow side rails run completely unbroken from crown shoulders to the bottom seat. The recessed back/cavity is one continuous uninterrupted surface. Directly below the purple front lip is one integrated translucent frost shelf with exactly THREE icicles.
Style/medium: polished 2.5D casual mobile-game raster sprite, smooth molded toy plastic, strict front-facing orthographic view, symmetric.
Composition/framing: very tall narrow portrait; complete object and all ice tips fully visible; outer slot width about 65% of canvas; generous transparent padding, especially below icicles.
Lighting/mood: one consistent soft upper-left light across the entire object. Rail thickness, bevel, inner trough, highlight width and material stay identical at every height.
Color palette: cavity #24275F / #2D2F63; dark rail trough #26235B; violet rails #494582 with restrained #7259FF highlight; frost #B9E3F7; ice #92D7F2 with cyan-white glints.
Materials/textures: continuous smooth dark toy plastic; chunky translucent faceted ice only below the bottom lip.
Constraints: same shallow exactly TWO-lobe crown; exactly TWO evenly spaced short thin horizontal recessed groove markers, one per cell; exactly FOUR low flat elliptical studs arranged 2 back plus 2 front; same integrated dark-purple front lip. Under that lip add one frost shelf and exactly THREE dominant icicles: short left, longest center, short right. Ice exists only beneath the purple lip; no ice, frost, snow or cyan growth on crown, rails, cavity, grooves, or studs. Rails and cavity have absolutely no join seams, thickness changes, highlight shifts, tonal bands or breaks.
Avoid: separate modules, segment boundaries, horizontal join bands, full-width crossbars, broken rails, changing rail width, tall heart crown, third lobe, extra studs, extra icicles, detached crystals, cubes, colored blocks, symbols, text, watermark, ice anywhere above bottom lip, perspective, isometric yaw, backdrop or background glow.
```

Reference roles, in order:

1. `slot_complete_2cell_raw.png` — whole-slot geometry/continuity target.
2. `codex-clipboard-4bde352d-9a7d-4839-b6e3-c25eeec5baf7.png` — bottom seat/lip.
3. `codex-clipboard-1d13a53e-f014-4b22-88dd-5151dea285e6.png` — rail/cavity/groove.
4. `codex-clipboard-0c9a3ea2-7823-4ac8-babc-2692dded7ffa.png` — crown.
5. `SlotStudRevisionPreview.png` — palette and bottom-ice style only.

## Raster normalization

`process_slot_complete_2cell_imagegen.py` performs only:

- neutral light/checker matte extraction and alpha-edge decontamination using
  neighboring pixels from the same complete ImageGen render;
- one crop around the complete object;
- one premultiplied resize;
- placement on the requested transparent canvas.
- one C1-smooth piecewise vertical resampling of that same complete render so
  the two groove centers are exactly 512 px apart at 512 PPU. Rows 0–320 and
  the second groove/bottom region remain byte-identical; no sections are
  composited or replaced.

It does not composite or borrow any modular slot pieces.

For Unity Tiled compatibility the protected center is top-down
`x=160..479, y=320..831` (320×512). Suggested borders `(L,B,R,T)` are
`(160,832,160,320)` for normal and `(160,1152,160,320)` for frozen.
