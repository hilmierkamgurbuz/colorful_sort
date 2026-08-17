# Complete 2-Cell Slot — Full ImageGen Rerender Provenance

## Final source policy

Each final sprite comes from exactly one complete, blank-canvas, built-in
ImageGen render. The current Gameplay PNGs and modular top/middle/bottom
sprites were never supplied to ImageGen and are never read by the strict
processor.

Reference inputs supplied to each selected call:

1. `photo_2026-08-17 10.38.14.jpeg` — palette, toy-plastic material,
   upper-left lighting, and (for frozen only) bottom ice style.
2. `codex-clipboard-0c9a3ea2-7823-4ac8-babc-2692dded7ffa.png` — shallow
   two-lobe crown silhouette.
3. `codex-clipboard-1d13a53e-f014-4b22-88dd-5151dea285e6.png` — continuous
   straight rails, cavity, and recessed groove appearance.
4. `codex-clipboard-4bde352d-9a7d-4839-b6e3-c25eeec5baf7.png` — integrated
   four-stud 2×2 base.

Selected direct built-in outputs:

| Variant | Built-in output | Workspace raw | SHA-256 |
|---|---|---|---|
| Normal | `exec-13bcf0d8-cbf3-42a5-9899-da84dd227ffd.png` | `slot_complete_2cell_full_rerender_raw.png` | `df7c230376ab9e5e1156a55e8ec38c407e0671a582207055ea4a8d553e479ad1` |
| Frozen | `exec-9d7b081e-e17b-4815-85e8-bf87757c6c16.png` | `slot_ice_complete_2cell_full_rerender_raw.png` | `d17b364c13a5178b7422ea83e2f9ab661947ab5bde71b3f9502f8ede49ef5472` |

Earlier blank-canvas candidates are retained as `*_attemptN_raw.png` for
audit only. They were rejected by the strict 512-pitch/canvas-fit or
normal-vs-frozen footprint gate and were never activated.

## Exact selected normal prompt

```text
Use case: stylized-concept
Asset type: Unity 2D transparent world-space sprite, complete two-cell toy slot
Primary request: From a blank canvas, render ONE single continuous manufactured slot object coherently in one pass. It is one uninterrupted molded object, not a collage, assembly, stack, edit, or combination of parts.
Input images: Image 1 supplies only gameplay palette/material/light. Image 2 is the authoritative shallow two-lobe crown shape. Image 3 supplies only the straight continuous rails, continuous cavity, and recessed groove shape; ignore black screenshot defects. Image 4 supplies only the integrated four-stud base shape.
Scene/backdrop: true transparent RGBA exterior; no floor, white, checkerboard, or background.
Subject: tall slender front-facing orthographic dark indigo/violet two-cell receiving slot. Two straight parallel rails run continuously crown-to-base, at constant x-position, thickness, material, contour, and highlight. One uninterrupted navy cavity. Exactly TWO identical centered short recessed horizontal groove marks. Exactly FOUR low studs in a 2 columns x 2 rows layout on the integrated base.
Mandatory geometric proportions: Let D equal the center-to-center distance between the two groove marks. The complete object height from crown peak to bottom base edge must be approximately 3.0 × D and must never exceed 3.15 × D. The complete object width must be approximately 0.82–0.90 × D. From crown peak to first groove is about 0.85 × D; groove-to-groove is exactly 1.0 × D; second groove to bottom edge is about 1.15 × D. Make the groove spacing visibly large enough to satisfy these ratios. These whole-object ratios are more important than filling the canvas.
Crown: exactly two broad low equal lobes with peaks around 27% and 73% outer width, one smooth shallow center valley. Peak rise is only 10–12% of outer object width; center valley descends about half that rise. Smooth inner/outer contours with constant crown thickness, no heart-shaped notch, ripple, kink, or third lobe.
Style/medium: polished mobile-game 3D toy plastic, soft bevel, subtle ambient occlusion, clean sprite render.
Composition/framing: portrait canvas, horizontally symmetric, complete object centered with generous transparent padding and no clipping. Rails perfectly vertical and parallel.
Lighting/mood: soft upper-left studio light; one slim continuous lavender highlight per rail.
Color palette: cavity #24275F/#2A2D65, rails/base #4237A1/#514D9A, restrained lavender. No cyan, frost, ice, colored blocks.
Constraints: single continuous render; exactly two grooves; exactly four 2x2 studs; ratios above; transparent outside; no text, glyph, watermark.
Avoid: joined parts, seams, modular pieces, local discontinuity, dark patches, black stains, stretched/wavy/pinched rails, rail-width changes, warped crown, deep heart notch, extra groove/stud, separate cube, U-foot, halo.
```

## Exact selected frozen prompt

```text
Use case: stylized-concept
Asset type: transparent Unity sprite of one complete frozen two-cell slot
From a blank canvas render ONE continuous manufactured object coherently in one pass. No edit, collage, joined sections, assembly, or modular pieces.
Use screenshot only for indigo toy-plastic palette/light and bottom ice; crown crop only for shallow two-lobe top; middle crop only for perfectly straight rails, continuous cavity, and thin recessed grooves while ignoring defects; base crop only for integrated 2×2 studs.
True transparent exterior.
Front orthographic symmetric slot with two constant-width parallel rails and one uninterrupted navy cavity. Exactly TWO identical short horizontal recessed grooves. Exactly FOUR low studs arranged 2×2 on the integrated purple base. Directly under the purple lip only, one frost shelf with exactly THREE tips, short-left/long-center/short-right.
Proportion gate: complete purple body outer width is 46–47% of portrait canvas width. The groove-center distance visually equals the purple outer width, within 5%. Put first groove in the upper quarter of the complete object, about 24–26%; put second groove just below the middle, about 52–54%; their gap is therefore about 28% total height. Overall crown-to-center-ice-tip height is approximately 3.5–3.7 groove gaps. The slot should look moderately slender: neither narrow like a thin tube nor wide like a panel.
Crown exactly two broad low lobes, shallow valley, no heart/kink/ripple, constant thickness. Rails straight with same width/highlight on every row. Ice only bottom 14%, exactly three icicles.
Polished mobile-game toy plastic, soft upper-left light, navy #24275F cavity, violet #4237A1 rails, cyan #92D7F2 ice. Full centered object with transparent padding.
No text/watermark/background/seams/dark patches/wavy rails/width changes/extra groove/stud/icicle/upper ice/cube/U-foot/halo.
```

## Strict raster processing

Canonical processor:
`process_slot_complete_2cell_full_rerender_strict.py`

Allowed final operations only:

1. Remove the baked light/checker matte and clean the alpha fringe using RGB
   sampled from the same complete raw render.
2. Crop once to the complete subject bounding box.
3. Apply one whole-render affine scale with one scalar, identical for X and Y.
   The scalar is `512 / raw_groove_spacing`.
4. Place the complete transformed render on the requested transparent canvas.

There is no piecewise or regional mapping, non-uniform resize, modular sprite
read, artwork compositing, vector replacement, or programmatic geometry.
