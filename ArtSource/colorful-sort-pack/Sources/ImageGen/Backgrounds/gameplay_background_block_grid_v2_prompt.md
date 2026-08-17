# Gameplay Background — ImageGen Block Grid V2

Generation method: OpenAI built-in ImageGen, two local reference images

Input roles:

- `GeneratedAssets/Backgrounds/gameplay_background_puzzle_silhouettes.png`: edit target before the V2 revision
- `photo_2026-08-17 10.38.14.jpeg`: visual reference only for background motif scale, spacing, and feeling

```text
Use case: precise-object-edit
Asset type: full-screen 2D mobile puzzle-game gameplay background
Input images: Image 1 is the edit target. Image 2 is a visual reference ONLY for the faint background motif scale, spacing, and casual-game feeling. Do not reproduce any UI, level pieces, slots, icons, text, ads, characters, or foreground content from Image 2.
Primary request: Replace every long two-stud 1×2 brick motif in Image 1 with one much smaller, nearly square toy cube/block motif. Absolutely no jigsaw shapes.
Preserve exactly: Image 1's smooth rich violet gradient, darker edge vignette, softly illuminated calm central gameplay area, purple-on-purple finish, 9:16 full-bleed composition, and clean raster quality.
Correct block motif: a compact, nearly square chunky toy block with a square/rhombus top face. The top face shows exactly FOUR very faint circular stud or round recess impressions arranged as a 2×2 square. The block has only shallow isometric thickness extending toward the lower-right of the canvas. Its top-face receding axis/diagonal points toward the upper-right (NE), with lighting softly from the upper-left. It must read as a square cube/block, never as a long rectangular brick.
Orientation consistency: every motif must use exactly the same isometric view, same size, same NE-facing top-face axis, same shallow down-right depth, same four-stud 2×2 arrangement, and same soft lighting.
Pattern layout: use a regular isometric grid with wide even spacing. Align each row cleanly; offset every alternate row horizontally by exactly half the horizontal grid step, then repeat this two-row rhythm down the full canvas. Keep motifs small and widely spaced, with no random placements, rotations, size changes, or oversized objects. Edge motifs may be cropped naturally.
Visibility: motifs must be extremely understated, about 3–6% visual contrast against the local purple background, soft and slightly hazy. Keep the center quiet and readable for gameplay; the pattern may fade slightly more in the central region while its grid rhythm remains coherent.
Style/medium: polished casual mobile-game 2D raster background; soft matte-plastic embossed silhouettes; restrained depth; no hard outlines.
Composition/framing: exact 9:16 portrait, edge-to-edge, no border, no frame, no focal object.
Text: none.
Constraints: change only the decorative motif pattern; opaque RGB background, no transparency. No jigsaw or puzzle-piece silhouette anywhere. Exactly four top studs/recesses per motif in a 2×2 layout. No long 1×2 or 1×4 bricks. No strict unshifted rectangular rows. No mixed shapes, mixed orientations, mixed scales, or random scattering. No UI, HUD, buttons, gameplay columns, slots, characters, scenery, logos, text, numbers, or watermark.
Avoid: strong/high-contrast motifs, large objects, bright highlights, black outlines, neon glow, multicolor elements, deep 3D perspective, obvious shadows, visible seams, and a completely flat empty background.
```

Outputs:

- Raw ImageGen output: `gameplay_background_block_grid_v2_imagegen_raw.png` (941 × 1672, RGB PNG)
- Archived Unity-ready final: `gameplay_background_block_grid_v2_final_1440x2560.png` (1440 × 2560, RGB PNG)
- Active Unity path: `../../../Backgrounds/gameplay_background_puzzle_silhouettes.png` (overwritten by explicit request)
