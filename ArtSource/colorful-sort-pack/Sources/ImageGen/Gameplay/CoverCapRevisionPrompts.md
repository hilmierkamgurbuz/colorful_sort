# Cover cap revision — built-in ImageGen prompts

The two supplied cover crops and the original gameplay screenshot were used as
visual references. Blue/green side strips, the water-drop glyph, black/navy band,
purple slot slab, cubes, and other gameplay layers are explicitly excluded.

## Blocky top cap

```text
Use case: stylized-concept
Asset type: Unity 2D mobile sorting-puzzle covered-column top cap raster sprite
Primary request: Generate ONE isolated thick horizontal top cap for a narrow covered gameplay column. It must be a boxy rounded rectangle, about 2.1 times wider than it is tall—not a long capsule or pill. The cap is slightly wider than the vertical beige cell beneath it. The smooth upper face is softly domed with modest corner rounding. Across the FRONT LOWER HALF, add one thin rounded inset/recessed panel line that follows the lower and side edges, like a shallow molded front panel. Add a restrained darker lower bevel and small soft contact shadow directly beneath the cap.
Scene/backdrop: genuine transparent RGBA background, one centered object only, generous transparent padding, no checkerboard or colored backdrop
Subject: empty warm beige/taupe toy-plastic cover cap, strict front view
Style/medium: polished 2.5D casual mobile game raster sprite, smooth molded plastic, chunky but simple, front-facing orthographic
Composition/framing: full uncropped object, symmetrical, width-to-height near 2.1:1, no perspective, no rotation
Lighting/mood: soft top-left studio light, restrained glossy highlight
Color palette: main beige/taupe #C7A89C, light face #DFC5B7, inset shade #B48F86, lower bevel #8F6F6A, subtle dark plum-brown outline #59466B
Materials/textures: smooth toy plastic, no grain
Constraints: boxy rounded rectangle; front lower inset line clearly visible but thin; true alpha; no content or attachment rails
Avoid: long capsule, pill button, fully oval ends, extreme corner rounding, text, glyph, colored strip, blue side pieces, water drop, black strip, purple base, studs, cubes, ice, wood grain, perspective, checkerboard, watermark
```

## Full-cell bottom cap

```text
Use case: stylized-concept
Asset type: Unity 2D mobile sorting-puzzle covered-column final-cell raster sprite
Primary request: Generate ONE isolated full-height terminal beige cover cell. It is a tall narrow front panel, about 0.82 times as wide as it is tall. The TOP EDGE is a perfectly flat horizontal crop/join edge with straight vertical beige/taupe sides so it can replace the last repeat cell seamlessly. The upper 75-to-80 percent remains a plain smooth warm beige center face with very restrained same-color side bevels. Only the LOWER 20-to-25 percent transitions into a soft terminal closure: vertical sides continue down, the two bottom corners become modestly rounded/chamfered, and one thin straight warm-taupe lower bevel closes the panel. It must remain a single vertical panel, not a separate horizontal base.
Scene/backdrop: genuine transparent RGBA background, one centered object only, generous transparent gutter around the object, no checkerboard or colored backdrop
Subject: empty warm beige/taupe covered-column last cell, strict front view
Style/medium: polished soft 2.5D casual mobile game raster art, smooth molded toy plastic, simple readable geometry, front-facing orthographic
Composition/framing: exact front view, full uncropped panel, no perspective or rotation; flat top join; small lower corner rounding only
Lighting/mood: restrained top-left cream highlight and soft lower-right taupe bevel
Color palette: main face #C7A89C, highlight #DFC5B7, side/lower bevel #A98279, subtle outline #725A64
Materials/textures: smooth toy plastic, no grain
Constraints: empty face; top edge perfectly flat; vertical sides; bottom closure only in lowest 20-to-25 percent; true alpha; no attached pedestal
Avoid: blue or colored side strips, water drop, glyph, icon, text, black/navy strip, purple slot base, colored rail, pill, capsule, U-tray, horizontal platform, top cap, studs, cube, ice, exaggerated shadow, perspective, checkerboard, watermark
```
