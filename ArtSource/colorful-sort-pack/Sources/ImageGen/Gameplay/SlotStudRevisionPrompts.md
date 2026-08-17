# Shallow-crown + 2×2 stud slot revision — ImageGen prompt record

All listed source PNGs were produced with the built-in ImageGen tool. The
active sprites are raster-only derivatives made by
`process_slot_stud_revision_imagegen.py`; no SVG/vector replacement is used.

## Canonical-middle broad-band revision

The active crown, four-stud seat and three-tip ice fringe continue to use the
ImageGen source prompts recorded below. The August 17 broad-band correction did
not request new painted content: it replaced the connection regions with actual
pixels from the canonical ImageGen-derived `slot_cell_repeat.png` and
`slot_ice_middle.png`. In the active processor:

- top rows 160–319 are exactly canonical middle rows 352–511;
- bottom rows 0–127 are exactly canonical middle rows 0–127;
- normal and frozen variants use their own canonical middle;
- 32 px top and 48 px bottom premultiplied transitions connect those canonical
  regions to the prompted ImageGen crown/seat pixels.

This preserves the original prompts verbatim while correcting rail position,
rail thickness, highlight, cavity tone and vertical texture continuity.

## `slot_shallow_crown_raw.png`

```text
Create one isolated modular TOP section for a narrow vertical sorting-game slot. Front-facing orthographic 2.5D casual mobile-game raster sprite, perfectly symmetric. Dark indigo/violet smooth molded toy plastic, deep navy recessed empty cavity, narrow constant-thickness beveled rails, soft upper-left highlight. The top rail itself forms exactly TWO broad equal rounded integrated lobes with one centered valley; no separate cylinder studs and no third lobe. The two side rails continue straight downward. Keep the cavity empty. No horizontal crossbar, no bottom foot, no groove, no block, no glyph, no text. Center the object on a genuine transparent RGBA canvas with generous padding; no checkerboard, no backdrop, no cast shadow outside the sprite. Palette: cavity #2D2F63, dark edge #26235B, violet rail #494582 / #7259FF.
```

The raster processor normalizes this ImageGen silhouette to the measured
reference profile: peaks at 27%/73%, crown rise 14.29% of outer width and
center-valley depth 54% of that rise. It only resamples source pixels.

## `slot_stud_ice_bottom_raw.png`

```text
Create one isolated BOTTOM assembly for a narrow vertical sorting-game slot, viewed straight-on with only a very shallow elevated view of the seat so all four studs are readable; no yaw and no isometric rotation. Two thin dark-indigo side rails and a deep navy cavity descend into one integrated dark-violet 2×2 block seating platform. The platform must have EXACTLY FOUR low flat elliptical studs arranged as two back-row studs plus two front-row studs, symmetric and centered. Plate width about 94% of the outer rail span. Show the darker front lip clearly. Directly under that lip fuse one translucent cyan-white frost shelf with EXACTLY THREE dominant icicles: shorter left, longest center, shorter right. The center tip is about 1.5–1.6 times the side-tip drop. Keep the ice within the slot width. No extra cube, no colored gameplay block, no fifth stud, no symbol, no text, no U-tray, no puzzle foot. Dark indigo/violet molded toy plastic, chunky readable aqua ice facets, upper-left light. Genuine transparent RGBA background, generous padding below the ice, no checkerboard, no backdrop, no watermark.
```

The same four-stud ImageGen seating geometry supplies both active bottom
variants. The normal file stops before frost; the frozen file adds the three-tip
fringe from this same source.

## `slot_stud_bottom_raw.png`

```text
Create one isolated NORMAL bottom assembly for a narrow empty sorting-game slot. Strict front-facing orthographic casual-mobile-game raster art. Two straight dark-indigo/violet plastic side rails frame a navy recessed cavity and terminate around a centered 2×2 seating platform. The seat must contain EXACTLY FOUR low rounded elliptical studs, arranged 2 back plus 2 front, with no extra nub, no cube and no rotation. Include a simple darker violet front lip beneath the plate, fully closed and softly beveled. Smooth molded toy plastic, restrained top-left highlight, symmetric constant width. Absolutely no cyan, frost, snow, ice or icicles. No symbols, text, glyphs, puzzle foot, U-tray or colored block. Isolated on genuine transparent RGBA with no backdrop, checkerboard or external shadow.
```

Only the complete violet lower-lip pixels from this ImageGen source are used to
finish the active normal/frozen 2×2 platform; the stud proportions come from
`slot_stud_ice_bottom_raw.png`.

## Additional ImageGen crown edit prompts retained for traceability

### `slot_flat_crown_normal_raw.png`

```text
Edit this slot top so its upper silhouette becomes nearly flat and reference-like: exactly two very low broad integrated bumps and one shallow centered notch. Remove the two separate cylindrical studs completely. Preserve the front-facing dark-indigo toy-plastic material, narrow rails, cavity, symmetry and lighting. Do not add a horizontal crossbar, a third lobe, a heart-shaped arch, text or glyph. Keep the object isolated.
```

### Reference-proportion edit attempt (`slot_reference_crown_attempt_raw.png`)

```text
Create one modular 2D game-slot TOP sprite. Use the first attached low-resolution screenshot as the authoritative silhouette and proportion reference: copy its nearly-flat top outline with exactly TWO broad shallow equal bumps, peaks near 27% and 73% of width, a single moderate shallow center valley, and straight vertical sides. Do NOT turn it into a tall heart or arch. Use the second attached clean render only for the dark indigo/violet toy-plastic material, front-facing orthographic rendering, narrow beveled rails, dark recessed cavity, and upper-left soft lighting. Required geometry: peak-to-side-shoulder rise about 14–15% of visible outer width; center valley descends about 53–55% of that rise; rail thickness about 5–7% of width. Continue the two straight rails and cavity downward with NO horizontal crossbar or bottom frame line. Exactly two integrated low bumps; no separate cylindrical studs, no third bump. Centered, symmetric, isolated on genuine transparent RGBA background, no checkerboard, no backdrop, no cast shadow, no text, no glyph. Leave ample transparent padding.
```

This last edit was retained as a raw audit source but was not used in the active
sprite because ImageGen exaggerated its center valley. The active crown instead
uses the genuine-alpha source plus deterministic raster normalization.
