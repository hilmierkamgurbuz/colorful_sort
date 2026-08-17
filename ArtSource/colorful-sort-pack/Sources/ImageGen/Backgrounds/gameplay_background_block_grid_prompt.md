# Gameplay Background — ImageGen Block Grid Revision

Generation method: OpenAI built-in ImageGen, two local reference images

Input roles:

- `GeneratedAssets/Backgrounds/gameplay_background_puzzle_silhouettes.png`: edit target before this revision
- `photo_2026-08-17 10.38.14.jpeg`: background-style reference only

```text
Use case: precise-object-edit
Asset type: full-screen 2D mobile puzzle-game gameplay background
Input images: Image 1 is the edit target. Image 2 is a background-style reference only; ignore and do not reproduce any UI, text, gameplay pieces, ads, buttons, slots, icons, or objects from Image 2.
Primary request: Edit Image 1 by replacing all existing jigsaw-puzzle-piece silhouettes and all irregular large cube silhouettes with one orderly repeating grid of identical toy-block silhouettes.
Preserve exactly: the rich purple-on-purple palette, smooth violet gradient, gentle edge vignette, soft casual-game finish, full-bleed 9:16 portrait composition, and calm readable central gameplay area of Image 1.
New motif: one identical chunky rectangular toy block, shown as a soft low-contrast isometric silhouette. Each block has exactly two round studs or circular recesses visible on its top face. The line through those two circles must point diagonally toward the upper right of the canvas (southwest-to-northeast / NE diagonal). Every block must use exactly the same isometric orientation, same size, same silhouette, and same stud direction.
Pattern layout: arrange the identical blocks in a clearly regular, evenly spaced orthogonal grid of aligned rows and columns across the full background. Keep spacing consistent. Partial blocks may be cropped only at the outer canvas edges. Make the pattern very subtle, purple-on-purple, about 4–7% tonal contrast, softly embedded into the background. It may be slightly quieter through the central play area but the grid alignment must remain consistent.
Style/medium: polished clean 2D raster mobile-game background with restrained soft depth; no hard outlines.
Composition/framing: exact 9:16 portrait, edge-to-edge, no border, no frame, no central focal object.
Text: none.
Constraints: change only the decorative silhouettes in Image 1; do not introduce any jigsaw shape anywhere. No random placement, no mixed block shapes, no mixed scales, no rotated variants, no isolated oversized objects. No UI, HUD, text, numbers, slots, columns, characters, scenery, logos, or watermark. RGB opaque background; no transparency.
Avoid: any jigsaw/puzzle-piece shape, scattered layout, staggered layout, random rotations, blocks facing different directions, single-stud blocks, more than two studs, strong 3D rendering, high contrast, colorful objects, black outlines, bright highlights, neon glow, visible seams, or a completely flat empty background.
```

Outputs:

- Raw ImageGen output: `gameplay_background_block_grid_imagegen_raw.png` (941 × 1672, RGB PNG)
- Archived Unity-ready final: `gameplay_background_block_grid_final_1440x2560.png` (1440 × 2560, RGB PNG)
- Active Unity path: `../../../Backgrounds/gameplay_background_puzzle_silhouettes.png` (overwritten by explicit request)
