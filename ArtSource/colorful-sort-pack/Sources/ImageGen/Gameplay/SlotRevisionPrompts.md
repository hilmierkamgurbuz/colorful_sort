# Two-lobe and integrated-ice slot prompts

Mode: built-in ImageGen. The gameplay screenshot and supplied crop were used as
visual references. The dark studded 3D gameplay cube in the reference is expressly
excluded from the 2D slot artwork.

## Normal slot — final prompt

```text
Use case: stylized-concept
Asset type: Unity 2D mobile sorting-puzzle modular empty-slot raster source sheet
Primary request: Generate exactly THREE isolated modular parts for one narrow empty vertical sorting slot, faithful to a casual mobile reference:
A) TOP: the tray roof silhouette itself forms EXACTLY TWO equal soft rounded humps/lobes side-by-side, with one shallow V-shaped valley between them. The lobes are integrated into the thin tray rim, NOT separate cylindrical studs and NOT a brick plate. From the two outside corners, exactly two thin straight parallel side rails descend to a perfectly flat bottom crop edge.
B) MIDDLE REPEAT: one tall rectangular 512-unit cavity segment with exactly two thin straight parallel rails and a dark flat indigo inner channel. Include exactly ONE short thin centered horizontal recessed groove inside the channel, never a full-width crossbar. Top and bottom crop edges remain plain and flat with no groove at either edge.
C) BOTTOM: the two rails end cleanly in one very thin shallow rounded lower rim/sill, like a simple closed boundary line. It is NOT a separate foot, pill, platform, brick, tray, U-bowl, stud plate, or puzzle piece. Flat join edge at top, softly rounded lower corners, minimal height.
Scene/backdrop: genuine transparent RGBA background; three parts fully separated vertically with generous transparent spacing; no checkerboard or colored backdrop
Subject: narrow empty front-facing sorting tray, constant width and symmetry
Style/medium: polished 2.5D casual mobile game raster sprite; understated smooth molded toy plastic; simple and readable; front-facing orthographic
Composition/framing: strict front view, no perspective; identical x width and rail thickness; every part fully visible
Lighting/mood: restrained top-left highlight, subtle inner depth only
Color palette: channel #2D2F63, dark contour #26235B, rail #494582, subtle violet highlight #7771BD
Materials/textures: smooth plastic, restrained bevel, no grain
Constraints: exactly TWO integrated rounded roof lobes; one shallow center valley; exactly two thin rails; one centered short groove in middle; true alpha; equal modular joins; no attached shadow
Avoid: separate cylinder studs, brick studs, third lobe, three scallops, cloud, crown, puzzle shape, thick pill bottom, foot, platform, U-shaped/hollow bottom, ice, frost, cyan, cubes, symbols, letters, numbers, full-width crossbars, groove on crop edges, background glow, perspective, isometric view, checkerboard, watermark
```

## Integrated frozen slot — final prompt

```text
Use case: stylized-concept
Asset type: Unity 2D mobile sorting-puzzle modular frozen-slot raster source sheet
Primary request: Generate exactly THREE isolated modular parts for one narrow empty frozen sorting slot:
A) FROZEN TOP: the thin tray roof silhouette forms EXACTLY TWO equal soft rounded integrated humps/lobes with one shallow V valley; no separate studs or plate. Exactly two thin parallel rails descend to a flat crop edge. Geometry matches a normal dark-indigo slot. Add only a VERY SUBTLE cool cyan rim tint on the outer rail edges; no snow, ice chunks, icicles, frost shelf, or crystal growth on this top part.
B) FROZEN MIDDLE REPEAT: one tall 512-unit dark indigo cavity segment with exactly two thin straight parallel rails, flat clean crop edges, and exactly ONE short centered horizontal recessed groove inside. Only a faint cold cyan edge glow on the rails, slightly stronger toward the lower portion but no physical ice, snow, crystals, shelves, or bumps. It must repeat 1-to-6 times without multiplying ice.
C) FROZEN BOTTOM: the same two rails end in a very thin simple lower rim. Directly fused to that rim is one narrow translucent aqua frost shelf, with EXACTLY THREE integrated icicles hanging beneath it: shorter left, one clearly longest center, shorter right. No brick, no cube, no stud, no plate, no platform, no U-bowl. Cyan-white frost occupies only the lowest 25-to-35 percent of the bottom assembly and fades upward into the indigo rails. All ice stays within the slot width and has generous transparent padding below the tips.
Scene/backdrop: genuine transparent RGBA background; place all three parts fully separated vertically; no checkerboard or colored backdrop
Subject: narrow front-facing empty frozen slot, constant width and symmetry
Style/medium: polished 2.5D casual mobile game raster sprite; smooth molded toy plastic; chunky readable faceted ice only at bottom; front-facing orthographic
Composition/framing: strict front view, identical x width and rail thickness, complete uncropped pieces
Lighting/mood: restrained top-left plastic highlight; cool cyan-white glints contained to rails/bottom ice
Color palette: channel #2D2F63, contour #26235B, rail #494582; faint rim #8FDFF5; frost #B9E3F7, ice #92D7F2 and #4BA8D8
Materials/textures: smooth dark toy plastic; broad clean translucent ice facets at bottom only; no grain
Constraints: exactly TWO integrated top lobes; one centered groove in middle; exactly THREE bottom icicles; bottom has no studs/plate/cube; true alpha; seamless equal-width crop joins
Avoid: separate cylinder studs, brick studs, four studs, gameplay cube, third lobe, cloud/crown/puzzle top, thick pill base, foot, platform, U-shaped bottom, physical ice on top/middle, frost at repeat seams, more than three icicles, detached crystals, full-width crossbar, groove on crop edges, symbols, text, perspective, isometric, checkerboard, watermark
```
