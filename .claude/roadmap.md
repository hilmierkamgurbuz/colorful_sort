# roadmap — what is built, what is next, and what each next task must not lose

<!-- Written so a fresh session (or a fresh person) can pick the work up without the
     chat history. It carries ORDER and CARRIED CONSTRAINTS only; the reasoning
     behind a constraint lives in decisions.md, fingerprint.md, scope.md and
     reference/colorful-sort-mechanics.md, which are the authorities. If this file
     and one of those disagree, they win and this file is stale.

     Update it at the end of any task that finishes or reorders work. -->

## Done

- **Board core + Content data types** — 2026-08-17. Engine-free puzzle rules (move
  rule, win, deadlock, ice/cover/mystery, recorded undo, seeded RNG) plus the
  authored data types and their conversion. 22 files, 39 tests green.
- **Attempt scramble → per-level variants** — 2026-08-17. A level offers a small
  config-sized set of looks; each is a pure function of (level index, variant index).
  4 files, suite now 49 tests green.

- **Boot skeleton + Core services** — 2026-08-18. `Core`'s own asmdef (D-016), the
  versioned JSON save with one writer and a migration gate (D-018, D-019), additive
  screen scene flow, the attempt seed source (D-017), `GameRoot` as the composition
  root, the scene bootstrapper and the unity-dev map exporter. 14 files, 23 new tests.
  `Boot`/`Menu`/`Game` exist, Build Settings is `Boot, Menu, Game`, the template
  leftovers are gone and `unitymap.md` is an editor export with 0 missing scripts.

- **The look layer — art import pass, brick skins and materials** — 2026-08-18. The 23
  gameplay and background sprites put on the pack's contract (PPU 512, slot pivots, the two
  Tiled borders); 12 `BlockSkin` assets with their meshes wired and their colours authored;
  12 URP materials generated from those colours; `BlockSkinSet` with 11 colour rows and the
  `?`-brick slot (D-020, D-021). 5 files, plus the map-durability fix (D-022).

- **BoardView part 1 — the board on screen** — 2026-08-18. `ColumnMetrics`/`BoardLayout`
  (a column's size read off its sprite's border, D-025; framing computed from the board,
  D-026) with 11 EditMode tests, `BoardLayoutConfig`, `BoardView`/`ColumnView`/`BlockView`/
  `BlockPool`, asmdefs for `Content` and `BoardView` (D-024), `Meta`'s `AttemptStarter`
  (D-027), and the editor command that builds the four prefabs and wires the Game scene.
  12 files. The fixture board renders: three filled columns, a mystery column showing `?`
  bricks under a visible top, an empty column, a taller ice column with its icicles, and a
  closed cover. One preflight assumption was falsified by the first render — the layering
  stand-off has to clear a brick's *volume*, not its centre (D-028).

- **BoardView part 2 — tap input and the brick animation** — 2026-08-18. `BoardLayout.SlotAt`
  (the tap as the inverse of the layout, D-029) with four more EditMode tests, `BoardInput`
  (one `Pointer.current` read per frame on the new input backend), `BoardAnimationConfig` and
  its asset, `BoardMoveAnimator` (lift / arc / drop, settling whatever it interrupts, D-031),
  the selection / move / cancel logic in `BoardView`, the brick mirror plus `Thaw` in
  `ColumnView` (D-030), and won/deadlocked log lines in `AttemptStarter`. The editor side is
  closed too: `Data/Config/BoardAnimationConfig.asset` is authored (0.75 / 0.12 / 0.22 / 1.0 /
  0.10), `Column_Ice` carries its thawed sprite, and `--Board--` in `Game.unity` carries
  `BoardInput` and `BoardMoveAnimator` with the config wired. The user played it: lift, move
  and cancel work.

- **Level editor window + authored column placement** — 2026-08-18. `Tools > Colorful Sort >
  Level Editor` (`LevelEditorWindow` + `LevelBoardDrawer`): pick or create a level, set index,
  difficulty and the layout grid, place columns by clicking the grid's empty cells, move one
  with an armed "Move to cell" that swaps with whoever is there, paint cells from the project's
  own `BlockSkinSet` palette (hidden `?` cells included), and read `LevelDefinition.Validate()`
  as a live banner. Written through `SerializedProperty` rather than new setters on `Content`
  (D-032). A level now authors **where** each column stands — one grid cell per slot, empty
  meaning the plain fill (D-033) — carried through `AttemptStarter` into `BoardView.Open` and
  turned into positions by a `BoardLayout` that takes the placement instead of deriving it from
  a dense index; each row is centred on its own span (D-034). 7 files, `BoardLayoutTests` now 21
  tests. **The suite ran green for the first time: 93 EditMode tests, 0 failures** (Board, Core,
  BoardView). `Level_0000.asset` was authored in the window with a real placement, previewed
  through its variants, played, and the maps were re-exported from the Editor.

Verification note: the board is playable and was played, and Unity compiled every affected
assembly with zero errors (the DLLs in `Library/ScriptAssemblies/` are the evidence). The Test Runner
has since confirmed it: 93 EditMode tests green across Board, Core and BoardView (2026-08-18),
which is the first recorded run this project has. The `[Core] Save refused: …` warnings in that
run are the save tests doing their job (D-019), not a fault.

- **The UI layer — foundation, HUD, popups and the route in** — 2026-08-18. Three tasks,
  verified together because the last one is what made the first two visible at all.

  **4A** — `UI`'s assembly and `Meta`'s alongside it, because an asmdef cannot reference
  `Assembly-CSharp` and `UI` could not otherwise have seen `AttemptStarter` (D-036); one
  `UiStyleConfig` asset with the TMP material generated from it; `PopupHost` persistent on
  Boot with its own canvas at sorting 200 and the project's single `EventSystem`;
  `SafeAreaPanel`; the gameplay HUD; the Pause popup. `BoardInput` learned to refuse a press
  that lands on UI (D-037). The 34 UI sprites joined the import pass with the pack's 9-slice
  borders. 14 files.

  **4B** — `AttemptStarter` forwards `Won`/`Deadlocked` as `AttemptWon`/`AttemptDeadlocked`,
  so `UI` never subscribes to a `BoardSession` and needs no `Board` reference (D-038);
  `Popup_Win` and `Popup_Fail`. 7 files, no asmdef and no blueprint change.

  **4C-a** — the menu's Play button, and the lesson that came with it: 4A and 4B were both
  finished and neither had ever been seen working, because the route they depend on was
  scheduled after them. Boot carries the popup host *and* the single `EventSystem`, so
  without a way to load Game over Boot the gear was silent — uGUI processes no pointer input
  with no EventSystem and says nothing about it. 3 files.

  Verified by the user, 2026-08-18: the whole chain runs — Boot → Menu → Play → Game → gear →
  Pause → win → Win popup → Continue → Menu.

  **Rule this earned:** a task whose result can only be seen through a screen that does not
  exist yet is not finishable. Either the route comes first, or the two ship together.


- **Board — the two boosters that change the rules** — 2026-08-18. `TryAddColumn` appends one
  empty `Normal` column whose capacity is the largest already on the board, refused at
  `LevelData.MaxColumns`; `TryShuffle` rearranges the readable cells of unlocked columns among
  themselves with Fisher-Yates in exactly n-1 draws, keeps every column's fill count, leaves
  hidden cells alone (D-011), and lets a completed colour complete with every consequence.
  `BoardMove` gained the discriminator its own note had predicted. 5 files, 12 new EditMode
  tests, no UI and no editor step.

  Two things were caught while writing rather than after. A shuffle **records** the colour
  every cell held instead of leaving undo to recompute the permutation from the RNG (D-041) —
  the derived design was this task's own preflight assumption and was exactly the failure the
  same preflight named as its worst risk. And the 16-column ceiling already existed as
  `LevelData.MaxColumns`, so the constant added beside it was deleted rather than kept as a
  second copy of one number.

  Verified by the user, 2026-08-18: the EditMode suite is green, which is this task's whole
  proof — `Board` is engine-free and answers to its tests, not to a screen.


- **Meta — the progression slice** — 2026-08-18. The game knows where the player is.
  `Progression` is plain C# over `SaveData` and the single writer of `currentLevelOrdinal`,
  `cleared` and `plays` — which `SaveData`'s own documentation had already assigned to `Meta`;
  `Core` still owns the bytes. `AttemptStarter` takes all three answers from progression and
  lost its serialized level, variant and attempt fields. The level editor rebuilds
  `LevelDatabase` from the level folder in plaque order. 12 files, and the first EditMode tests
  over `Meta` — 11 of them, because this is the first code whose mistakes are written to a
  player's disk and a save cannot be undone the way a board can.

  Two corrections rather than additions: the seed is built from the level **ordinal**, which is
  what `fingerprint.md` always said and the code did not do (D-039); and reading the attempt
  ordinal is separated from recording the play, so a level that refuses to open no longer
  consumes an attempt (D-040) — which also removed 4A's temporary shape, since `Restart` now
  deals a genuinely different board.

  Verified by the user's play session, 2026-08-18, in the editor log: `variant 0` on the first
  attempt and `variant 1` with a different seed on the second, and the last-level branch
  reporting "the database ends here" and returning to the menu.


## In progress

### 3C. Solvability search — code written, first use pending
`BoardSolver` answers what `Validate()` cannot: a legal board can still be impossible.
A depth-first search with a visited set, driving a real `BoardSession` through `TryMove`
and `Undo` rather than re-implementing the move rule, reported in the level editor on a
**Check solvability** button next to the validity banner and dropped the moment the board
changes. Three verdicts: solvable (with the moves), unsolvable, and **not proven** when a
limit was hit — a cap that is reached is ignorance, never a verdict. Depth is capped at
`BoardMoveHistory.MaxEntries`, because one move deeper and the history would drop the entry
the search needs to undo. 9 EditMode tests, including replaying the returned solution
through the real rules and a locked ice column checked against the same board unlocked.

It lives in `Board` behind `#if UNITY_EDITOR` rather than under `Assets/Editor/`, which is a
change from what this entry used to say: an editor folder compiles where no test assembly
can reach it, and an unverified validator is worse than code that never ships (D-035).

Still owed, the user's clicks: run the EditMode suite, then press **Check solvability** on a
level that works and on one deliberately broken, and re-export the maps.

### 5B-3. The booster bar and the view's three new mutations — code written, first use pending
The three boosters are real on screen. `BoardView` learned to mirror what it never could:
`Resync` rebuilds every column and brick from `Board`'s state after an undo, an added column or
a shuffle, rather than reversing what it thinks changed (D-044) — and an added column has no
authored slot, so the view takes the first free grid cell and grows a row when there is none.
`AttemptStarter` carries the three commands and raises `BoardChanged` from the session's own
events, so the bar re-reads what is available without polling. `BoosterButton` is one component
for all three. 7 files.

**Boosters are free** (D-043) — no coins, no charges — which cancelled 5B-2 outright. The pack
ships no booster icons, so the buttons carry text on the green shell until the user dresses them.
`Boosters` is now recorded in the blueprint as a system that deliberately has **no code folder**;
`check_blueprint.py` will keep warning about it, and that warning is the design rather than a gap.

Still owed, the user's clicks: `Tools > Colorful Sort > Build UI`, then play from Boot and press
each button. The one that matters is **undo after a shuffle and after an add-column** — a resync
that forgot a cover, a thaw or a revealed cell leaves a board that looks plausible and disagrees
with `Board`, and the symptom only shows on the *next* tap.

### UX-1. A UI prefab editing environment — code written, first use pending
The popups exist only as prefabs, because `PopupHost` instantiates them and nothing keeps a
disabled copy in a scene — so Prefab Mode opened them with no canvas around them and an 880×900
panel floated at a size that meant nothing. `Tools > Colorful Sort > Set Up UI Prefab Editing`
builds `Assets/Editor/PrefabEnvironments/UI.unity` (one canvas, built by calling
`UiFactory.EnsureCanvas` rather than retyping the two numbers every screen scales by) and
assigns it to `EditorSettings.prefabUIEnvironment`. 3 files, no runtime code.

Still owed, the user's clicks: run the command once, then double-click
`Assets/Prefabs/UI/Popup_Pause.prefab` and confirm it appears at real proportions.

### UI-2. On/off icon toggles and the Music setting — code written, first use pending
The Pause row is three toggles now: sound, **music** and vibration, each drawing its state as a
pair of icons from the second UI set rather than one icon under a diagonal bar (D-045). `Music`
is a real persisted setting — `GameRoot.MusicOn` over `SaveData.musicOn` — which made this the
first **save version bump**: `CurrentVersion` is 2, and the 1 -> 2 step turns music on, because a
file written before the field existed parses to `false` and that is indistinguishable from a
player who muted it. Restart left the row for a wide button; a command has no on/off state.
7 files, 1 new migration test. Nothing plays music yet — there is no audio system, and `SoundOn`
has been in the same position since Boot was built.

Still owed, the user's clicks: in `Popup_Pause`, set the Music button's `Setting` dropdown to
**Music** (it still reads `Sound`, so it currently toggles the sound flag) and assign
**On Icon** / **Off Icon** on all three toggles — the component logs and does nothing until both
are there. Then play from Boot, press each one, and restart to confirm the flags survive.

Still owed in the editor, and not code: `Popup_Win` has no button yet (`continueButton` empty),
`Popup_Fail`'s title reads `'No Moves` instead of `NO MOVES LEFT`, the booster buttons lost their
icons in the re-skin, and neither `Menu` nor `Game` has a background — the menu's is a stretched
UI image, the gameplay one has to be a world sprite parented to the camera, because a Screen
Space - Overlay canvas draws over the board.

### V-1. The board fits the screen, forgets an undone column, faces the player — code written, first use pending
Three defects the first playable render exposed, all in `BoardView`. The camera is now framed
against the band the HUD leaves — `topReserve` 0.15 and `bottomReserve` 0.18 in
`BoardLayoutConfig`, measured off the plaque's 255 px and the booster bar's 300 px — and pushed
into the middle of that band, so the board no longer slides under either (D-046). Placement moved
into `BoardLayout.PlaceColumns` as a pure function deriving every layout from the level's authored
one, which is what makes an undone add-column give its cell **and** its row back; the old
`EnsurePlacementFor` could only grow. And every brick was showing its blank back: the embossed
symbol is on the mesh's +Z face while the camera looks along +Z, so the `Block` prefab now turns
180° on Y (D-047). 6 files, 10 new EditMode tests.

Still owed, the user's clicks: run the BoardView EditMode suite, then play from Boot and check
three things — the board clears the plaque and the booster bar, the bricks show their symbols, and
add-column followed by undo returns the board to its authored shape.

### V-2. The column's new anatomy — code written, first use pending
The user's hand-drawn column art is on the project's contract and the column is built out of it:
`slot.png` as the tray at its own measured 160 px per unit (interior 1.175 units around a 1.0-unit
brick, tray 1.244 wide so no level re-frames), `slot_bolme` drawn once per cell boundary by
`ColumnView`, and `Block_Base` as the 3D plate the bricks stand on — placed by measurement, its top
face on the first cell's floor, which is what the tray's 46 px skirt was chosen to match. Every
column sprite's border now means skirt and crown only, with the pack's borders re-stated to the
skirts they already had, and the draw mode comes from the sprite instead of the prefab (D-048).
`BoardViewPrefabFactory` repairs existing prefabs part by part instead of demanding they be deleted.

The brick symbols are also finally fixed, and not by the prefab turn alone: `ColumnView.Place` was
forcing `Quaternion.identity` on every seated brick, overriding the prefab's facing at runtime
(D-049). That is why D-047 changed the asset and nothing on screen.

5 files, 2 new metrics tests. Still owed, the user's clicks: `Tools > Colorful Sort > Apply Art
Import Settings` (the three new files are still plain textures), then `Tools > Colorful Sort > Build
BoardView Prefabs`, then set `Column_Ice`'s **Thawed Slot** to `slot` by hand — it is a prefab
variant, deliberately not touched by the repair pass. Then play and report the tray, the dividers,
the base plate and the symbols.

### V-3. The angled camera — code written, first use pending
`--Camera--` leans 25° into a board that stays upright, so the bricks read three-dimensionally and
the trays are seen slightly from above. The scene owns the angle and the framing reads it (D-050):
an upright board projects by the cosine of the tilt, so the camera zooms in rather than out, and the
position is rebuilt from the camera's own up and forward — from the distance along its view ray,
which makes framing a fixed point instead of creeping every resync. The tap became a ray against the
board's plane; `ScreenToWorldPoint` returns a near-plane point that drifts a whole row once the
camera leans. Backgrounds needed nothing: they are children of the camera. 4 files, 5 new tests
including a real camera round trip at 0° and 25°.

Still owed, the user's clicks: run the BoardView EditMode suite, then play and **tap the top and
bottom rows of a two-row level** — that is the check the ray-plane fix exists for. If the top row
looks tight, `cameraPadding` in `BoardLayoutConfig` is the number to raise; the angle itself is on
`--Camera--` and can be tuned freely.

### V-3b. Bricks seated at both ends — code written, first use pending
Two alignment defects the tilted render exposed, both single measured numbers (D-051). The base
plate's studs stood exposed under the lowest brick, because V-2 aligned them with the cell floor
rather than with where a brick one cell lower would end its studs — a brick's body starts a little
above its floor, which is the whole gap. And the tray's wavy top floated over the highest brick's
studs, because its top border was set at a generous 40 px while the drawn interior begins between 7
and 28 px from the edge; at 28 the wave is still protected from the nine-slice stretch and the studs
end up inside it. 3 files.

Still owed, the user's clicks: `Apply Art Import Settings`, then `Build BoardView Prefabs` — the
repair pass now re-seats a plate that already exists — then look at the top and bottom of a full
column.

### V-5. The symbol reads, the plate is tray-coloured — code written, first use pending
The re-authored meshes (verified by measurement: body exactly one cell, no draft, studs 12, symbol 6
deep on +Z, two slots named Body/Symbol) came with a slot nothing filled. `BlockSkin` now carries a
symbol material, `BlockSkinFactory` generates one per colour as the skin's own colour times
`BlockSkinSet.symbolShade` (0.65) — derived, not authored, so the colour map stays the single
authority (D-052) — and `BlockView` assigns both slots from a field-held array. The base plate got
`Slot_Base.mat`, a URP Lit material whose colour is sampled from the tray PNG, which is what stops it
rendering magenta on the FBX's own shader. `BlockSkinFactory` also learned to skip `Block_Base`: it
shares the models folder and the `Block_` prefix and would otherwise grow a skin of its own. 6 files.

Still owed, the user's clicks: `Create Block Skins` (writes the twelve symbol materials and fills the
new slot — the board refuses to draw until this has run once), then `Build BoardView Prefabs`
(creates `Slot_Base.mat`, re-materials and re-seats the plate), then play.

### V-6. The tuned column is the column — code written, first use pending
The user tuned a live column until the bricks fitted — tray scale (0.9470175, 0.9952835) with a 0.009
centring nudge that turns out to be exactly the tray sprite's off-centre opacity, plate scale 1.0972
at y 0.20196 — and play-mode values do not survive. They are now in `Column.prefab`, which
`Column_Ice` and `Column_Covered` inherit as variants. The build tool no longer rewrites an existing
plate's transform (D-053): the derived seating is what a *new* plate gets, and a designer's eye beats
the formula once the plate exists. Only a missing or FBX-imported material is still replaced. 2 files.

Still owed, the user's clicks: open `Column.prefab` and read the four values back, run `Build
BoardView Prefabs` and confirm it no longer reports re-seating, then play.

### V-7. Smaller columns, room to lift — done, pending a look
`rowGap` 0.6 -> 1.2 and `cameraPadding` 0.5 -> 1.0 in `BoardLayoutConfig`, no code (D-054). The gap is
sized by the lift rather than by eye: a brick lifted from the lower row's top cell had 0.025 of
clearance, and the 25° tilt projects the upper plate's depth about 0.46 units down the screen, which
is what hid the lifted brick behind it. The padding is what makes the columns read about 16% smaller.

Still owed, the user's look: lift a brick from the lower row's top cell and confirm it clears the row
above. Both numbers are live in the Inspector — `cameraPadding` for size, `rowGap` for spacing.

### V-4a. The tap hands the selection over — code written, first use pending
`BoardView.Move`'s refusal path cancels and selects instead of returning (D-055), so tapping cat and
then moon drops the cat and lifts the moon. The outgoing run snaps rather than tweens: the animator
settles a motion when the next starts, and two concurrent groups belong with V-4d. 1 file, no tests
— the tap path is a MonoBehaviour with a camera, a session and an animator, and the BoardView test
assembly covers the pure layout maths only.

Still owed, the user's clicks: lift a run and tap another colour (first drops, second lifts), tap the
same column (still cancels), tap an empty or locked column (selection just drops).

### V-4b. A finished colour's column is locked — code written, first use pending
`BoardRules.HoldsCompletedColour` refuses the column of a completed colour in `CanLift` and as a
move's source (D-056). Completeness, not fullness: a full mixed column still lifts, and a colour
shorter than its column is finished anyway. The rule sits in `BoardRules`, so the tap, the deadlock
test and `BoardSolver` all read the same narrower move set — putting it in the view would have let the
editor call a level solvable that a player cannot solve. 3 files, 7 new tests.

Two findings worth carrying: the preflight's feared **false deadlock is provably impossible** (a
finished colour's only possible destination is an empty column, and an empty column serves every
unlocked column equally, so if the lock took the last move every non-empty column would already be
finished — the win), and the tests had to be *played* into position rather than authored, because
`BoardState` refuses a level that starts with a colour already gathered. What the lock does cost is
strategic: a level needing a finished colour to vacate a differently-sized column is now unsolvable,
and the solver says so at authoring time.

Still owed, the user's clicks: run the Board EditMode suite (46+7 tests), then play — complete a
colour and confirm the column ignores taps, while a full-but-mixed column still lifts, and undo of the
completing move opens it again.

### V-4c. The finished column settles — code written, first use pending
`slot_completed_shadow` fades in over the whole tray in front of the bricks while they darken, on a
coroutine — the pattern the move animator already uses, so no column runs an `Update`. The whole thing
is **derived**: every rebuild re-asks `BoardRules.HoldsCompletedColour` and dresses the column
instantly, so undo and every booster resync are right with no bookkeeping, and the predicate that
locks a column is the one that dresses it (D-057). The darkening is a `MaterialPropertyBlock` per
material slot multiplying the colour read from the material itself, so nothing is cloned and nothing is
written down; `Apply` clears it too, which is what stops a darkened brick escaping its column through
the pool. 6 files. Numbers in `BoardAnimationConfig`: 0.25 s, shade 0.75, shadow alpha 0.55.

Still owed, the user's clicks: `Build BoardView Prefabs` (adds the shadow renderer and wires the
view's animation config), then play — finish a colour and watch it sink, press Undo and watch it come
back, and press a booster afterwards to confirm a rebuilt board is still settled.

### V-4d. The lift's feel — code written, first use pending (glow deferred)
A rising run rocks once and lands level; a lifted one keeps rocking. The first attempt slid the run
sideways; the user's reading is a **diagonal tip**, so the whole stack now turns about its own centre —
top-left corner out, then top-right (D-059). The roll goes in front of each brick's own rotation so the
180° that faces its symbol survives, and `Settle` restores rotations as well as places, because nothing
else in the view writes a rotation and a brick left tipped would stay tipped. The config refuses a
fractional cycle count, which is what lands the run level. `Play` stops the rock first, so a cancel or a
move starts from the run's true place, and `IsBusy` is deliberately untouched — the tap that hands the
selection over has to land mid-rock. 4 files; `liftTiltDegrees` 7, `liftTiltCycles` 1,
`idleTiltDegrees` 3, `idleTiltPeriod` 1.6.

Still owed, the user's clicks: lift a run (one tip on the way up, level at the top, then a small
diagonal rock), tap another column mid-rock (the tap must land; the run must go down upright from its
anchor), and press a booster mid-rock (the board must rebuild clean, with no brick left tilted).

Carried, and small: `BoardView.animation` hides Unity's legacy `Component.animation`, which the compiler
warns about. Renaming it needs `[FormerlySerializedAs]` so the scene keeps its reference *and* a change
to the string `BoardViewPrefabFactory` wires it by — one file outside this task's manifest, so it waits
for the next one.

### V-4f. The glow behind a lifted run — code written, first use pending
The last piece of V-4. A lifted run carries one glow for the whole board, tinted to its colour, sized
to its length plus `glowPadding` 0.18, and **parented to the run's bottom brick** — which is why it
needs no per-frame code: the rock, the rise and the drop all move that brick (D-060). It is detached
and disabled on every path that ends a selection, since the pool re-parents a brick without its
children.

The art is the completed-column shadow with its pixels whitened (`block_glow.png`, generated from it,
alpha untouched). That correction came out of building it: `SpriteRenderer.color` multiplies, so the
original dark navy could only darken — a tint cannot light dark art. Same import contract, because it
is the same drawing. No bloom and no additive material yet: the Game camera has post-processing off,
so HDR emission would render as nothing; a bright tint reads as light against the purple background,
and additive is the next step if it is not enough. 6 files, and the CS0108 warning V-4d left on
`BoardView.animation` is gone — renamed with `[FormerlySerializedAs]` so the scene keeps its reference.

Still owed, the user's clicks: `Tools > Colorful Sort > Apply Art Import Settings` (the new sprite),
then `Wire Game Scene` (creates `Columns/Glow` and fills the slot), then play — lift one brick and a
run of three, check the glow spans each and rocks with it, and that it vanishes on both a drop and a
move.

### V-4g. The glow wraps any run and burns — code written, first use pending
Two corrections from watching V-4f on screen (D-061). The halo had been fitted to one brick by scaling
it, which would have been half again too tall on a run of three; the reach below (0.606) and above
(0.233) is a fact about the sprite's transparent padding, so it moved into `size`, where the nine-slice
holds the rim and stretches only the middle — one brick and three now get the same halo. And it did not
glow at all: alpha blending can only lighten, so the project gained its first shader — unlit, additive,
texture times vertex colour, one intensity — and `Block_Glow.mat` generated from it by the wiring tool.
No bloom, no camera change: additive needs no post FX. 6 files.

Still owed, the user's clicks: `Wire Game Scene` (creates the material and puts it on the glow), then
play — lift one brick and a run of three, confirm the same rim on both, and say whether it is bright
enough. `_Intensity` on `Block_Glow.mat` is the one dial; bloom is the step after that, with its mobile
cost stated.

### V-4h. The glow is a tray of light — code written, first use pending
Rebuilt from `slot.png` instead of the completed-shadow (D-062): it fits the bricks by construction —
they live in that tray — and it is solid rather than hollow, because the tray's body is opaque. Sizing
comes from the sprite's own `ColumnMetrics`, so a run's glow is a tray of that height and the two
authored reach numbers are gone rather than retuned. The white-out was additive clipping: a saturated
tint at intensity 1.6 loses its channel ratio, so the tint is now the skin's colour mixed towards white
(`glowLift` 0.4) and the shader's default intensity is 0.8. 6 files.

Still owed, the user's clicks: `Apply Art Import Settings` (the sprite's contract changed), **pull
`_Intensity` on the existing `Block_Glow.mat` from 1.6 down to about 0.8** — the tool creates a material
once and never re-tunes it, so the old value is still in there — then play and lift a one-brick and a
three-brick run. Two dials from there: `_Intensity` for heat, `glowLift` for how pale the hue is.

### V-4i. The glow, measured against the run and lit as neon — code written, first use pending
Two corrections from the user's own tuning (D-063). The tray-derived sizing of V-4h was tidy and wrong:
the tray's skirt and crown made the light wider and lower than the eye wants, so the numbers are the
run's now — outset 0.152, below 0.578, above 0.157, plus a 0.009 nudge for a drawing that is not quite
centred. Anchored on the run they hold for any length, which a hand-set scale cannot. And the colour:
mixing towards white desaturates, which is the opposite of neon, so the tint is the hue divided by its
strongest channel — orange stays orange and only gets brighter — with `glowLift` down to 0.15 for the
hot core. 3 files.

Still owed: play and lift a one-brick and a three-brick run. The remaining dials are `_Intensity` on
`Block_Glow.mat` (still 1.6 in the existing asset — pull it towards 0.8) and `glowLift`.

### V-4j. The glow finally carries its colour — code written, first use pending
The white glow was never the tint maths: with the SRP batcher on, Unity leaves a sprite's vertices white
and passes `SpriteRenderer.color` per draw in `_RendererColor`, so a shader reading only `COLOR` draws
white whatever it is tinted (D-064). The shader now multiplies both, exactly as Unity's own sprite
shaders do. It also stops clipping: additive loses the channel ratio that *is* the colour, so when the
peak passes 1 the whole triple is scaled down instead — the hue survives and brightness just stops
rising. `_Intensity` default is 1.5, the value the user settled on. 1 file.

Still owed: play and lift runs of each colour — each glow should be that brick's hue, brighter and
lighter, not white. If a colour wants to be brighter than its pure hue, that is bloom's job and needs
post-processing enabled on the Game camera, with its cost stated first.

### V-4k. The glow's colour, delivered — code written, first use pending
The white glow survived two fixes because both were aimed at the wrong thing. A diagnostic settled it:
the renderer held the correct light pink, on our material and shader, sprite bound and size right, while
the screen stayed white — so the tint was being dropped between the renderer and the shader. Neither the
vertex colour nor `_RendererColor` carries it on this path, so the shader now has its own `_Tint`, set
per renderer through a `MaterialPropertyBlock` (no cloning, one material for every colour). 2 files, and
the diagnostic log is gone (D-065).

Still owed: play and lift runs of a few colours — each glow should now be that brick's hue, lighter and
brighter. Dials unchanged: `_Intensity` on the material, `glowLift` in the config.

### V-4l. Real neon — overbright plus bloom — written, first use pending
The colours were right and the glow was still dim, because the shader's peak cap left nothing above
bloom's threshold to turn into light (D-066). Both pipeline assets already render HDR, so the cap is gone;
the Game camera now renders post-processing, and the Bloom override that was already in the global volume
profile at intensity 0 is on at threshold 0.9 / intensity 0.9 / scatter 0.6 / 4 iterations / half
downscale / no high-quality filtering. 3 files.

This is the first change in the series with a **per-frame GPU cost** — a whole-screen chain every frame,
about half a millisecond to a millisecond on a phone — and the first thing to cut if a device runs tight.
Carried note: if HDR is ever turned off, the shader's cap has to come back, and the symptom without it is
the white wash V-4h to V-4k already chased.

Still owed, the user's look: lift runs of a few colours and say whether it reads as neon; then whether the
rest of the screen — the plaque, the white text, the pale bricks — has gone hazy, which would be
`threshold` too low rather than `intensity` too high.

### V-4m. Straight edges on the glow — done, pending a look
The ripple was the tray art's own edge multiplied by the nine-slice stretch: 6 px of wander either side,
71 distinct alpha profiles in 188 stretched rows (D-067). Every row of that band is now the median alpha
per column, so one clean profile is repeated and the edge is straight by construction; the top wave and
the bottom base are untouched, being in border regions that never stretch. The cap's narrower silhouette
left a 7 px step at the upper seam, so the border's last eight rows ramp into the band's profile — inside
a border, which keeps it a short shoulder. Measured after: 0 px deviation, one profile, no step at either
seam, wave and base byte-identical to the art. 1 file.

Still owed, the user's look: lift a run, follow the long edge, and check the glow's top still lines up
with the studs — that is the part deliberately left as drawn.

### V-4n. The glow's corner and the boosters' icons — code written, first use pending
The sharp corner was the median profile eating the drawing's corner radius: it settles at y 40 while the
glow's top border was 28, so a third of the curve was inside the stretched band. The glow now carries its
own border with a 42 px top — same drawing as the tray, different job, so the borders differ on purpose —
and the corner is byte-identical to the art again with the band still a single clean profile (D-068). The
three boost icons moved to `Art/Sprites/UI/Icons/`, the booster prefab gains an `Icon` child on every run,
and each instance is dressed with its own sprite outside the branch that builds the bar. 6 files.

Still owed, the user's clicks: `Apply Art Import Settings`, then `Build UI`, then look at the corner and
the three boosters. If a booster stays blank, the console line to read is the one naming it.

### V-7a. Where an added column goes, and two things that drew nothing — code written, first use pending
The placement rule is the emptiest row that is not yet five wide, tie to the row above, and a new row
below only when every row is full (D-070). That one comparison gives the alternation asked for — the
first addition goes up, the next finds the lower row emptier and goes down — and keeps the board square.
The limit also had to become the placement grid's width, since a cell is `row × width + column` and a
three-wide level could otherwise never grow a five-wide row; `Open` widens the grid and renumbers the
level's own cells into it, which moves nothing because each row is centred on its own occupied span.
Six new layout tests pin all of it, including the tie and the undo case.

The booster icons stayed blank because the `Icon` child was ensured but its *sprite* never was — an Image
with no sprite draws nothing. The prefab carries a default now, and the dressing pass may replace any
sprite from its own list. The missing base plate has no explanation that reading the code can produce, so
`ColumnView.Build` now names the three faults that could cause it and stays silent otherwise (D-071).
7 files.

Still owed, the user's clicks: `Build UI` (the three boosters should stop being blank), then a level —
add columns one at a time and watch where they land, and read the console if a plate is still missing. The
line will name the column and the numbers; if nothing is printed, the plate is present, enabled and
seated, and the next thing to look at is the Scene view with that column's `Base` selected.

### V-8a. Bricks enter from above, one at a time — code written, first use pending
Travelling stopped being a whole-run motion. `PlayEntry` flies each brick on its own, a stagger after
the one before, along a path that crosses to above the target column and then drops in; the crossing
height is that column's own top plus `entryClearance`, raised to clear both ends so a move into a
lower row cannot sag. `BoardView.Move` reverses the run once, so the topmost brick leaves first and
lands lowest — a look, not a rule, since a moved run is one colour (D-072). `arcHeight` became
`entryClearance` via `FormerlySerializedAs`. Six tests pin the path: both ends exact, clamped
outside them, crossing finished before the drop starts, x held all the way down, no sag, and a
refused share still landing. 5 files.

Still owed, the user's look: move a run of two or three and watch the queue — do they come over the
mouth of the slot, and is the stagger (0.07 s) the right wait? `entryClearance` 0.7, `entryStagger`
0.07 and `entryTravelShare` 0.6 are all in `Data/Config/BoardAnimationConfig.asset`.

### V-8b. The trail behind a flying brick — code written, first use pending
A `TrailRenderer` on a child of the brick, lit with the brick's own colour through the additive
shader's `_Tint` — so the streak inherits the entry path with no per-frame code, and `NeonOf` moved
to `BlockView` because the glow and the trail are now the same light with one formula (D-073). It
starts when the flight starts and stops feeding when the brick lands, so the tail fades behind it;
`Apply` clears it, or a pooled brick would draw a line across the board. Seconds and width are
config (they must agree with the travel duration); the curve, gradient and alignment are the
prefab's. `EnsureBlockTrail` reaches the prefab that already exists. Six tests pin the neon formula.
6 files.

Superseded by V-8b2 the moment it was seen on screen: a line is not what the reference shows.

### V-8b2. The trail is a plume — code written, first use pending
The reference is a wide soft column of light filling the corridor, so the streak became a
`ParticleSystem` on the brick's child: world simulation space (a puff stays where it was dropped),
emission by distance (the brick's movement paints it, and one waiting its turn lays nothing), noise
for the wander, and a generated soft puff texture — a clean circle reads as beads (D-074). Colour is
the system's start colour, so one material still serves twelve. The tool deletes the `Trail` child
and `Block_Trail.mat` it made last task, since nothing else referenced them. 7 files.

Still owed, the user's clicks: **Apply Art Import Settings**, then **Build BoardView Prefabs**, then
move a brick. `trailTime` 0.45 (a puff's life), `trailWidth` 0.8 (its size in cells) and
`trailDensity` 14 (puffs per cell) are in `Data/Config/BoardAnimationConfig.asset`; the noise and the
fade curves are on the prefab's `Plume` child, to be tuned by eye against the reference.

### V-8c. A finished column celebrates — code written, first use pending
One coroutine on the column plays hop → sparks → settle, because the order is the effect (D-075).
The hop is written from the seated positions and ends by reseating, so an interruption cannot leave a
brick off its cell; the symbol's flash is `SetShade`'s mechanism with the multiplier above 1 on slot 1
only, so the shape lights up and bloom carries it; the sparks come from one particle system per
column, emitted at each brick's position — 16 systems rather than 128. `SetSettled` stops a
celebration in flight, and a rebuilt board still settles instantly with no party. `settleDuration`
0.25 → 0.4, the "touch slower" asked for. 6 files.

### V-8c2. The celebration, corrected — code written, first use pending
Four faults from the first look, two of them the same mistake: asking the board a standing question
when a change was wanted. The trigger moved off `ColourCompleted` (which fires at the tap, before the
bricks fly) onto `BoardMove.CompletedColours`, read at `Land` — which fixes both "it starts before the
brick arrives" and "every finished column hops again", since only this move's colour celebrates now.
The hop became staggered, bottom brick first, each symbol lighting as its brick leaves its cell and
staying lit until they are all down, held 0.2 s, then let go in the scatter. Nine numbers travel as
one `CelebrationLook` (D-076). Hop doubled to 0.56, settle tripled to 1.2 — where the shadow's fade
and the bricks' darkening always came from one number in one lerp, so they agree by construction.
4 files.

Still owed, the user's look: **Build BoardView Prefabs** if it has not been run since V-8c (each
column prefab needs its `Sparkle`), then finish a colour and watch the order. `celebrationHop` 0.35,
`celebrationHopDuration` 0.56, `celebrationHopStagger` 0.12, `celebrationGlowHold` 0.2,
`celebrationSymbolGlow` 3, `celebrationSparks` 14 and `settleDuration` 1.2 are in
`Data/Config/BoardAnimationConfig.asset`; the spark's own look (speed, gravity, lifetime) is on the
column prefab's `Sparkle` child.

### V-8d/V-8f/V-8g. A brick lands, and the three reasons nothing had ever been drawn — code written, first use pending
The first look reported no particles at all and a console full of "Particle Velocity curves must all be
in the same mode", and both had the same shape of cause: a fault only a script can create, sitting in a
prefab the tool that created it could no longer reach.

The column's system was dressed `playOnAwake = false` and was only ever **emitted into** — but a system
that has never been played is stopped, and a stopped system does not simulate. So V-8c's celebration
sparks had never been seen either; the plume's own code (`Clear(); Play();`) had been right since the
day it was written and the column's path was written without it. Separately, `DressPlume` wrote
`velocity.y` as a two-constant range and left `x` and `z` single constants, which Unity refuses — a
fault that cannot be made in the Inspector, where the three axes move together.

What that earned is a **repair pass that runs every build**, over exactly the set of things that are not
tuning: play-on-awake, simulation space, max particles, cleared emission rates, the velocity axes' mode,
and zero gravity on the burst whose authored climb depends on having none. D-053's create-once split
still holds for sizes, colours and curves — but a system that emits on its own or carries a pull the
view assumed away is not tuned, it is broken (D-078). Seventh meeting with the create-only trap.

The effect itself is the user's spec: on **every** placement 12 small sparks rise from under each brick
that arrived, fading, exactly two cells — twice a brick — and when the move **finishes the colour**
they come instead at the middle of the slot the moment the glow lets go, rise and fall back. Two
systems per column, because `EmitParams` overrides a particle's position, velocity, lifetime, size and
colour but never its **gravity**: `Rise` carries none, `Finish` does. The single `Sparkle` they replace
is retired by name, which keeps both create-only and runs the migration once. 6 files, 7 EditMode tests.

Still owed, the user's look: **Build BoardView Prefabs** first — without it neither system exists and
the screen is unchanged, which looks exactly like the code being wrong — then move a run into a column
without finishing a colour, then finish one. `landingSparks` 12, `landingRiseHeight` 2,
`landingRiseSeconds` 0.55, `landingSparkDrop` 0.35, `celebrationSparks` 14, `celebrationBurstRise` 3 and
`celebrationBurstSeconds` 0.9 are in `Data/Config/BoardAnimationConfig.asset`; each burst's spread,
size and fade are on its own child of the column prefab.

Carried, unverified: whether `EmitParams.velocity` replaces the shape-derived velocity or adds to it.
Either way they rise; the difference is how much they spread, which is a number tuned by eye.

**Third round.** The systems were built correctly and were still invisible, and the reason was the material they shared: `Block_Plume.mat` is the *glow's* shader, whose depth test exists to hide everything but a rim behind a lifted run — so a spark emitted inside a brick's own mesh was drawn and correctly occluded. The shader gained `_ZTest` (default LessEqual, so the glow is untouched by construction) and the bursts moved to `Block_Spark.mat` at `Always`. The symbols also let go slowly now — `celebrationGlowFade` 0.6 — and the finish burst waits for that fade, because "when the glow ends" is the moment asked for (D-079).

**A diagnostic ships with it and is meant to be deleted.** The first burst of a play session prints where it fired, how many particles the system then holds and what it draws with. **0** means the emit path is still wrong; **12** means they exist and are not being drawn. The depth test is an inference, and this project has already paid twice for guessing a fourth time instead of logging once (D-065).

The plume is left alone on purpose: same material, almost certainly the same fault, but nobody has reported it and a plume in front of every column is a look rather than a correction.

### V-8e. The sparks take the symbol's shape — code written, first use pending
The symbol is not a sprite and never was: it is submesh 1 of the brick mesh, which is why a skin
carries a second material for it. `SymbolMeshBuilder` lifts it out into one mesh asset per skin under
`Art/Models/Blocks/Symbols/`, `BlockSkin` gains `sparkMesh`, and both bursts draw mesh particles in the
brick's own colour — the `NeonOf` light the glow and the plume already share, now behind one accessor
instead of two copies. 9 files, 7 new EditMode tests in `Content`'s first test assembly.

Three transforms are baked into the extraction and each is a fact rather than a taste: a **half turn
about Y**, because the `Block` prefab carries exactly that so its symbol faces the camera and a particle
mesh gets no transform — without it every spark is mirrored and back-turned; **recentred on its own
bounds**, because a particle draws around its position while the symbol sits on the brick's face; and
**scaled to one unit across**, so a particle size means the same for all twelve and survives a re-skin
(D-080).

The extraction rebuilds every pass, unlike the mesh reference beside it in the same factory. That
distinction is the rule this project keeps paying for: a reference to authored art is created once and
then owned by whoever tunes it, but derived data is stale the moment its source changes.

Still owed, the user's clicks: **Create Block Skins** first (it writes the twelve symbol meshes), then
**Build BoardView Prefabs** (it makes `Block_Symbol.mat`), then play. If the sparks come out as soft
puffs with a warning naming `Create Block Skins`, the first command has not run. `Block_Spark.mat` is
kept: pointing the two renderers back at it is the whole of the revert if symbol sparks read worse.

Known limit, written down rather than discovered: one renderer draws one mesh, so particles still alive
from an earlier burst of a *different* colour take the new shape when the next burst sets it.

### V-9. Three legs, and the bursts get their size — code written, first use pending
Four adjustments from the first look at working sparks, and one fault the first of them uncovered.

**The flight is three straight legs** — up out of its own column, across at the crossing height, down
through the target's mouth. The old path drove x and y from one eased value, so a brick left its
column diagonally and came at the slot from the side. Time is split in proportion to each leg's own
length, so the brick holds one speed round the corners and the shape needs no authored share:
`entryTravelShare` is deleted, because what it described no longer exists.

**That uncovered the second fault.** The crossing height was measured off the target column alone,
which was harmless while the first leg drifted upward — the brick was never level with the column it
was still inside. A straight rise turns a run leaving the bottom of a tall column sideways *within*
it, through its own upper bricks. The apex now clears both mouths (D-081).

**The bursts**: both spark sizes are config now (`landingSparkSize` 0.35, `celebrationSparkSize`
0.45), the finish burst spans the middle two cells rather than one point, and the placement burst
decelerates to a stop exactly at its authored height exactly as it fades — `landingRiseSeconds` 0.55
to 0.9. 8 files, the path tests rewritten around the shape.

Still owed, the user's look: **Build BoardView Prefabs**, then move a run across a row and into the
row above, and finish a colour. The two sizes and the durations are all in
`Data/Config/BoardAnimationConfig.asset`, tunable without a compile — which was the point of moving
them there.

### V-10. The bursts are driven, not dressed — code written, first use pending
Nine adjustments from the second look, and one line moved.

**The line**: every number asked for over three rounds of this effect — size, count, spread, wander,
gravity, emission shape, the shape of the motion — lived on a prefab, so every one cost a rebuild and
a round-trip. The two burst systems are now *driven*: the view writes the region, the noise and the
motion per burst out of the config. The prefab keeps the renderer and the fade curves. D-053 is not
abandoned, it is moved to where the authority actually is (D-082).

**The placement burst** dips before it climbs, which made its motion a velocity curve rather than a
speed plus a pull — three phases, and an acceleration is one. Its area is measured and divided out, so
the authored climb survives any dip: **a deeper dip changes the shape, not the destination.** It also
comes off 80% of the brick's base and wanders instead of rising dead straight, fewer and much slower.

**The finish burst** throws once out of an area spanning the middle two cells, on a computed arc whose
apex sits at the halfway mark, fewer sparks.

**And the glow's fade stopped smoothstepping.** It was reported as the burst arriving late; nothing was
waiting. Smoothstep's slope is zero at the end, so its last third is invisible — and a symbol stops
reading as lit earlier still, once it drops under bloom's threshold. An easing chosen for how a motion
starts and stops is the wrong one for a value whose *end has to be noticed*.

Still owed, the user's look: **no prefab rebuild this time** — play, then tune the asset. `landingSparks`
6, `landingRiseSeconds` 1.6, `landingSparkSpread` 0.8, `landingSparkWander` 0.25, `landingSparkDip` 0.35,
`celebrationSparks` 8, `celebrationBurstRise` 4.5, `celebrationBurstSeconds` 1.1 and
`celebrationBurstSpread` 0.7 are all in `Data/Config/BoardAnimationConfig.asset`.

Watch for: the finish burst's arc is deterministic now, so only the area spreads the sparks. If it reads
as too uniform, that is the first thing to look at.

### V-11. The burst lands with the hop, and every spark goes its own way — code written, first use pending
Six adjustments, one meaning change and one retired guarantee.

**The finish's fade and its burst overlap** instead of queueing: the light starts going out as the
bricks come home, the sparks come out a lead into that, and the two end together. This moment has now
had three shapes — hold/fade/throw, throw-at-the-end-of-fade, and this — and it is the first that reads
as one event rather than two near each other. Still one coroutine (D-075's real point).
`celebrationGlowHold` kept its name and changed what it measures: **how long the fade has been running
when the burst fires**, not how long the symbols stay lit before it starts.

**Every spark now goes its own way.** Each velocity axis carries a *pair* of curves and Unity draws each
particle its own place between them, so no two sparks travel together — different destinations, not just
different starting points. The cost is deliberate: `landingRiseHeight` **stops being exact and becomes
the average**, ending the promise D-078 made and D-081 and D-082 both preserved. Sparks that all travel
the same distance are sparks in formation, which was the complaint (D-083).

**And the dials are findable.** The asset's headers are regrouped so HOW MANY, HOW BIG AN AREA and HOW
FAST sit together at the top of each burst.

Still owed, the user's look: **no prefab rebuild** — play, then tune
`Data/Config/BoardAnimationConfig.asset`. Watch for the tallest sparks overshooting: at a large
`landingSparkScatter` the top of the band is noticeably above `landingRiseHeight`, which is the trade
this task made on purpose.

### V-12. An added column takes its turn — code written, first use pending
The add-column booster stops choosing the **emptiest** row and starts taking rows **in turn**: one to
the top, one to the row below, round again, a full row stepped over, and a new row at the bottom when
a whole lap finds nowhere to stand (D-084, replacing D-070).

The two old and new rules agree on an even board, which is why the old one lasted: the sequence it was
written from never exercised the case where they differ. On a lopsided board, choosing the emptiest
poured columns into whichever row was behind — so an addition could land at the bottom while the top
row still had room.

**A skipped row is not owed a turn.** With the lower row full, the top row takes two additions in a
row. That is the visible consequence and the thing to check.

2 files, the placement tests rewritten around the new rule. No asset, no prefab, no scene.

Still owed, the user's clicks: run the EditMode suite, then play and press **Add column** several
times on a level authored five wide or narrower. On Level 79 — authored 2 rows of **6**, against a
limit of 5 — both rows are already past the limit, so the first added column starts a third row and the
board becomes 6 + 6 + 1. That was chosen deliberately; `maxColumnsPerRow` in
`Data/Config/BoardLayoutConfig.asset` is where it would change.

### V-13. Levels become one compact JSON file — code written, first use pending
Levels are no longer ScriptableObject assets. They are lines in `Assets/Data/Levels/Levels.json`, one
per line, and the level editor reads and writes that file whole. 8 files, 8 new EditMode tests.

**What kept it small.** The obvious move — turning `LevelDefinition` into a plain C# class — would have
rewritten the thousand-line editor window and rippled through `Meta` and `UI`. But the request is about
*where levels live*, not what the type is: so `LevelDefinition` stays a `ScriptableObject` and levels
are decoded into **transient** instances that are never saved. The editor's whole `SerializedProperty`
surface (D-032), `AttemptStarter`, `GameplayHud` and `ToLevelData()` are untouched (D-085).

**The format**: `<kind><capacity>[:cells][#thaw][/cover]` per column — `n i v m` for the kinds, a letter
per colour, `*` before a hidden cell. Level 0 went from 1770 bytes of YAML to about 200. Text rather
than gzip on purpose, and one line per level, because a level that decodes to the *wrong* board still
plays — a readable diff is how that gets caught, not a nicety.

**Loading**: the file is parsed into rows once and a level is built only when it is played. Two thousand
rows are small structs; two thousand levels would be two thousand ScriptableObjects at boot.
`LevelDatabase` holds the file as a `TextAsset`, so `Resources/` and string paths stay out.

Still owed, the user's clicks: run the EditMode suite, open **Tools > Colorful Sort > Level Editor**,
confirm Level 0 loads with the board it had, edit and press **Save**, then **delete
`Assets/Data/Levels/Level_0000.asset`** from the Project window — Unity takes its `.meta` with it,
which is why that one is yours.

### V-14. Levels are numbered from 1, and the menu names the next one — code written, first use pending
`Levels.json` is empty and `Level_0000.asset` is gone: levels are authored from scratch, from 1.
8 files, 2 tests rewritten around the new state.

**Numbering is a rule now, not a habit.** `LevelDefinition.Validate` refuses an index below 1 — nothing
the game computes could enforce it, since progression counts ordinals and the database never renumbers
(D-085), so the validator is the only place it can be true. The editor's New Level starts there too.

**The menu button says `LEVEL 1`**, in capitals authored in `UiStyleConfig` rather than upper-cased in
code. It works the number out from the save's ordinal and the database, because `AttemptStarter` is in
the Game scene and does not exist while the menu is up. The in-game plaque was left alone.

**With no levels the button says `NO LEVELS YET` and goes disabled** rather than opening a Game scene
with nothing to build a board from. That is the project's state right now, and the same state a
mis-typed level file produces later (D-086).

Still owed, the user's clicks: run the EditMode suite, `Tools > Colorful Sort > Build UI` (the menu has
three new references), then author level 1 in the Level Editor and press Save — the button should read
`LEVEL 1` and become pressable.

### V-15. A new level has room, and two tests that were wrong — code written, first use pending
Three defects from V-13/V-14, all introduced by me.

**A new level started on a 1×1 grid.** Valid data, and a dead end: a column is added by clicking a grid
cell, so one cell meant one column and no second could ever be placed. It looked like a working level,
which is why it cost a playtest rather than a compile. A blank level now starts on **2×6** — Level 79's
own shape, so it begins looking like the levels this game has.

**The numbering test's fixture was an illegal board.** `n2:aa` is a capacity-2 column holding two of one
colour — a colour already gathered, refused outright (D-013) — so it failed for a reason that had
nothing to do with numbering, and "level 1 is accepted" could never pass. It is `n2:ab` now.

**And the shipped-file test asserted the level file was empty**, which would have failed the moment the
first level was saved. It now asserts what lasts: the file reads, and every level in it validates and is
numbered from 1 (D-087).

Still owed, the user's clicks: run the EditMode suite, then Level Editor → **New Level** → a 2×6 grid
with room to place columns in.

### V-16. Progression that advances, and a win the player sees land — code written, first use pending
Two faults reported together. 5 files.

**Next always reopened level 1**, and it took two defensible pieces of code to do it. `Progression`
takes the level *count* as a number and `AttemptStarter` cached the instance — so one built when the
database held a single level never learned there were four, and `HasNext` stayed false. That alone is a
stale read. What made it permanent: `CompleteCurrentLevel` returned early on an already-cleared level
**without advancing**, so the first win marked level 1 cleared, could not advance, and every win since
hit the early return. The player was pinned by a **save**, not by something a restart clears. Existing
saves heal on the next win — no migration (D-088).

**The win panel opened at the tap**, before the bricks had flown, because `BoardSession.Won` fires the
instant a move is legal. `BoardView` now raises `BoardShown` when it has caught up — bricks landed,
every column this move set celebrating finished and settled — and the win waits for it. `Resync` raises
it immediately, which is what keeps a win by *shuffle* from waiting for a landing that never comes.

Still owed, the user's clicks: run the EditMode suite, then win level 1 — the panel should arrive after
the last column has sunk into its slot, and **Next** should open level 2. Then check the other path:
win by pressing **Shuffle** into the last colour.

### V-17. The fixture that broke three rules — code written, first use pending
`Level_NumberedBelowOne_IsRefused` failed three times for three different reasons, none of them the
rule it tests: a colour already gathered (D-013), then `LevelData.MinColumns` being 2 while the fixture
had one column. I diagnosed the first two from reading alone and was wrong about which was failing both
times.

The fixture now comes from a `MinimalLevel` helper whose every choice answers a specific rule, and every
guard between the fixture and the assertion was read end to end and checked against it. The helper is
offered rather than imposed — the other tests in that file build illegal levels on purpose, because a
round-trip has to work on those too (D-089).

Still owed, the user's clicks: run the EditMode suite. 204 tests, and this was the only red one.

### V-18. The brick left behind by a partial move stays down — code written, first use pending
Lift a run of two into a column with room for one: one brick flies, and the other hangs in the air.

`Move` already re-seated the remainder and said so in a comment — the comment was right and the code
did not work. `PlayEntry` runs next and goes through `Load`, which calls `StopIdle`, which puts every
rocking brick back on its **lifted** anchor, including the one just seated. The fix was reversed two
calls later.

What hid it: on a full move every rocking brick is also a flying one, so the stale restore is
overwritten each frame before anything is drawn. The leftover of a *partial* move is the only brick
nobody writes again — a case a playtest finds and a full-move test never does.

One line of order: end the rock before rearranging what it holds (D-090). No unit test, deliberately —
the fault is the order of two MonoBehaviour calls with no pure function between them.

Still owed, the user's look: lift a run of two into a column with one free cell. The brick that stays
behind should drop back into its own column.

### 5B-4. Coins, booster charges and the buy popup — code written, first use pending
The economy D-043 cancelled is back (D-091), in the shape D-042 left room for. `PlayerEconomy`
is the single writer of `save.coins` and `save.boosters`; `ProgressionConfig` carries the
starting coins, the three starting charges, what a first clear pays and a `BoosterOffer` row per
booster; `AttemptStarter` spends a charge after `Board` accepts a mutation, pays a first clear
and sells packs. On screen: a coin pill top-left (`CoinHud`), a red count badge that becomes a
green plus at zero, `Popup_BoosterShop` behind that plus, and a win whose coins fly from the
popup's middle into the pill while the number climbs (`CoinFlight`, sorting 300). 13 files,
10 new Meta tests.

**Pending in the editor:** `Tools > Colorful Sort > Build UI` builds `Popup_BoosterShop` and
`CoinFlyer`, adds both badges to `BoosterButton`, the pill to the Game HUD, the anchor to
`Popup_Win` and the flight layer to Boot — nothing on screen until it has run once. Then
`Tools > unity-dev > Export unitymap`.

Must not lose:
- no `saveVersion` bump was needed and none should be added for this: `coins` and `boosters`
  were kept in the shape by D-042 exactly so a later price would cost `Meta` and nothing else
- seeding is detected by an EMPTY booster list, never by a charge count of zero — zero is a
  player who spent everything, and re-seeding them would refill their boosters for free
- the charge is taken AFTER the mutation is accepted; a refused booster costs nothing
- a first clear pays, a replay does not — the last level would otherwise be an infinite coin tap
- `BoosterId`'s order is the migration for three prefab instances; append only (D-045)
- the flight canvas sorts ABOVE the popup host's, or half of every flight draws behind the popup
- `UI` spends nothing and prices nothing: the shop asks, `Meta` answers (rules/ui.md)

### 5B-5. The counter moves into the shop, the flight goes away — code written, first use pending
The user's pass over 5B-4 on screen (D-092). The coin pill is no longer HUD furniture: `Build UI`
moves the tuned object out of the Game scene into `Popup_BoosterShop`'s top-left and deletes it
from the HUD, so a balance is shown exactly where it can be spent. The coin flight is gone
outright — `CoinFlight.cs`, `CoinFlyer.prefab`, the Boot layer, `Popup_Win`'s `RewardAnchor` and
five style numbers — and the win popup says what it paid instead: a coin and `+20`, hidden when a
replayed level pays nothing. Each `PlusBadge` now mirrors its own `CountBadge`'s rect on every
run, prefab and instances alike. 7 files, no test changes (`Meta` untouched).

**Pending in the editor:** one `Tools > Colorful Sort > Build UI` does the move, the mirror, the
win row and the retirement — run it in the same sitting as pulling this, because the Boot layer
carries a script that no longer exists until the pass deletes the object. Then
`Tools > unity-dev > Export unitymap`.

Must not lose:
- the pill is MOVED, never rebuilt: the scene object is the tuned one (D-053's lesson)
- the plus follows the count, not the other way round — a separately nudged plus is overwritten
- the win's number is `Meta`'s `LastWinAward`; only its formatting (`+{0}`) is config
- a replayed level pays nothing and shows nothing, rather than "+0"

### 5B-6. The shop's own scrim, and a one-charge pack — code written, first use pending
The user's second pass over the shop (D-093). `Build UI` gives `Popup_BoosterShop` a full-screen
black 55% `Scrim` as its first child — the popup host's stays, so the two stack and the shop sits
behind about 0.8 — and the offer table drops to 1 charge for 250 coins on all three boosters,
which is a data edit and no code at all. The hand-edited prefab has also lost its Header, its
IconFrame and the buy button's caption; none of that reaches code, since every reference is
serialized rather than found by name.

**Pending in the editor:** `Tools > Colorful Sort > Build UI`, then rename the shop's `ttt` back
to `Close` if that was accidental, then `Tools > unity-dev > Export unitymap`.

Must not lose:
- the host's scrim is not the shop's to remove — it darkens every other popup and blocks the
  tap that would otherwise reach the board (D-037)
- the prefab scrim is added only when absent; its alpha is the user's from then on (D-053)
- the price and the pack size are config, never code (D-091)

### 5B-7. The shop popup repairs its own empty slots — code written, first use pending
Rearranging `Popup_BoosterShop` by hand (the header the title lived in was deleted) left
`titleLabel` empty, and a null label is silently skipped — so every booster's popup showed the
prefab's authored "BOOST" instead of `UNDO` / `EXTRA TUBE` / `SHUFFLE`. `UiFactory` gains
`EnsureBoosterShopWiring`: it fills whichever slots are empty, finding children by name at any
depth, and touches nothing that already points somewhere. 1 file.

**Pending in the editor:** `Tools > Colorful Sort > Build UI` — or drag `Body/Title` into the
component's Title Label slot, which is the same fix by hand.

Must not lose:
- only EMPTY slots are filled: the renamed close button and the hand-picked icon stay wired
  as they are, and `Icon` exists twice in that prefab, so a pass that overwrote would pick one
- children are found at any depth, because the layout has already been rearranged once and a
  lookup tied to the old parent fails for exactly that edit

## Next, in order

### Standing constraints — anything that touches the board view
Task 2 is done and these are what it must keep being true; a re-skin, a new column kind or
the booster work will edit the same files.

Must not lose:
- brick materials are URP **Lit** because the symbol is embossed geometry — so the Game
  scene needs its directional light; deleting it flattens every symbol
- taps are refused while bricks are in flight, and starting a motion settles the one it
  interrupts — that is what keeps the tween from ever disagreeing with `Board` (D-031)
- a thaw swaps the ice column's slot sprite, which moves the cells down by the skirt
  difference; it is safe only because a locked ice column is empty (D-030)
- camera framing (`orthographicSize`) is a tuning number: it comes from `Data/Config/`,
  not from a serialized default (the bootstrapper deliberately left Unity's default)
- a hidden cell draws `BlockSkinSet.HiddenSkin`, and reveals by swapping to its colour's
  skin — `Board` already knows the real colour all along (D-012, D-021)
- a slot's screen position is authored (its grid cell, D-033) but **what stands in it**
  comes from the attempt, never from the authored column order — the placement belongs to
  the slot for exactly that reason, or the variant reordering never reaches the screen
  (D-014, D-015, reference §8)
- each row is centred on its own occupied span and an authored hole keeps its width
  (D-034); the tap test inverts that same rule, so the two change together or not at all
- 1 logical cell = 512 px = 1 Unity unit, `Pixels Per Unit: 512`; no pixel constant
  is hard-coded (CLAUDE.md invariant)
- sorting order is the art pack's: background 0 · column 10 · 3D bricks 20 ·
  cover/ice/mystery overlay 30 · screen UI 100+
- columns use `Draw Mode: Tiled` in exact 512 px steps, borders `160,832,160,320`
  normal / `160,1152,160,320` ice; never free `Sliced`, never mix the two families
  (D-007, reference §6)
- ONE `Block` prefab; colour and symbol mesh are applied at spawn from the skin set
  (D-004), and blocks come from a pool
- the view reads `Board` and sends commands; it never writes board state — which the
  `internal` mutators already make impossible
- animation is view-only: the move is committed the moment it is legal, and
  interrupting a tween never desynchronises the two (`.claude/rules/gameplay.md`)
- `Column` and `Column_Ice` carry four empty `cover*` slots because one `ColumnView`
  serves all three variants: the unitymap's UNASSIGNED lines on those two prefabs are
  expected, not a defect, and a covered-only subclass is the price of silencing them

### 1B. Audio + haptics — waiting on assets, not on code
`AudioService` (music + sfx, honouring the persisted sound toggle), `HapticService`
(vibration toggle), and the sfx bank in `Data/Config/`. Split out of task 1 and parked
here because `Assets/Audio/` holds no clip yet: the two things that drive these
services are gameplay sfx (task 2) and the Settings popup (task 4). The save fields
(`soundOn`, `vibrationOn`) and their single writer (`GameRoot`) already exist, so this
needs no `saveVersion` bump — it runs the moment there are clips, and nothing else in
the roadmap waits on it.

Must not lose:
- the toggles are `Core`'s data, so the writer stays `GameRoot`; the Settings popup
  sends a command, it does not own the value (fingerprint.md → Data authorities)
- the single `AudioListener` lives on Boot's persistent `--Systems--` root, not on a
  screen camera
- clip references live in `Data/Config/`, never as string paths (no `Resources/`)

### 3B. Level 79 — the first transcribed content
Transcribe Level 79 with the window from 3A: 12 columns × 4 cells, laid out 2 rows of 6,
three ice columns thawing at N=1,2,3, the covered columns with their key colours and the
mystery column (reference §2). **Blocked on the source image, not on code** — the repo
holds no screenshot, `.claude/reference/` carries only the distilled markdown, so the
cell-by-cell contents live wherever the user's screenshot is. Whoever has it authors the
level; the asset is `Assets/Data/Levels/Level_0079.asset` (rules/data.md).

### 3 (all parts). Carried constraints

Must not lose:
- `LevelDefinition.Validate()` already runs everything the game would; it is the
  window's "is this shippable" button
- solvability is validated on the **authored** board; every variant inherits the
  verdict, and the window should be able to step through a level's variants
- asset names are stable and meaningful: `Level_0079`, `Skin_Moon` (rules/data.md)
- level data stores logical colour ids only — never an RGB value, a symbol name or a
  mesh reference (D-003, CLAUDE.md invariant)
- a level may not start with a colour already gathered; the board builder refuses it
  (D-013)

### 4C. Main menu and the Settings popup (the rest of it)
What 4C-a deliberately left out: coins, hearts, the progress bar, the menu background, and
the Settings popup. The scene and its Play button already exist — this fills them in, and
turns that button into the reference's level button with the level number on it. This is where 4A's foundation
gets its second screen and the first thing that has to survive the Boot → Menu → Game swap
in both directions.

Must not lose:
- Settings and Pause share their body layout, and their round toggle row is already one
  component (`SettingToggleButton`) — a second copy of it is the thing rules/ui.md forbids
- the coin and heart counters read `Meta`'s economy, which 5A deliberately leaves out: they
  show what the save carries (zeros) until 5B, and that is fine — the level button is the part
  that has to actually work, and 5A is what makes it work
- Quit from a level already returns here; there is no way back into a level until Menu has
  its level button, which is why 4C and task 5 are adjacent

### 4 (all parts). Carried constraints

Must not lose:
- all runtime text is TextMeshPro; no text is baked into art (CLAUDE.md invariant).
  Reference style: off-white `#FFF6D6` fill, dark purple/brown outline, soft shadow — and it
  lives in exactly one place, `Data/Config/UiStyleConfig.asset`, from which the one TMP
  material is generated (D-020's shape). A label that overrides its own material clones it.
- copy that never changes is baked into the prefab that shows it; copy chosen at runtime (the
  difficulty word, the plaque's number) is in the config. Holding both is dual authority
- Canvas Scaler reference 1080×1920, `Match Width Or Height: 0.5`, and layout anchors to
  `Screen.safeArea` — the background is authored at that size and 19.5:9 crops it
- buttons ship `_normal` / `_pressed` / `_disabled` → `Transition: Sprite Swap`
- the arrow is one-way: UI → gameplay. Gameplay holds no reference to UI
- only what is meant to be pressed is a raycast target, or the HUD swallows taps meant for
  the board (D-037)
- exactly one EventSystem, on Boot, with `InputSystemUIInputModule` — the legacy module is
  compiled out of this project and picking it leaves every button silently dead
- popups are prefabs the host instantiates and stacks; nothing is hand-toggled, and a popup
  never destroys itself
- popup contents follow the reference layouts (reference §4)

### 5B (all parts). Carried constraints

Must not lose:
- Shuffle is the RNG's only consumer, and every draw it makes is recorded in the move
  history, or undo cannot reproduce what it undid (D-002) — 5B-1 proved this with tests
- undo must work across a mystery reveal and across a cover opening; both are already
  recorded and tested
- history is capped at 256 entries and reports what it dropped, so the Undo booster can
  tell the player rather than look broken
- an in-progress board is **not** saved; quitting a level restarts it (D-008)
- starting coins and booster charges are seeded by `Meta` from `Data/Config/`: a fresh save
  deliberately carries zeros, so those numbers never sit in C#
- there are no hearts anywhere (D-042); a fail costs nothing

### 6. Phase transition
When the release scope is content-complete, run `phase-transition.md` to move from
`production` to `shipping`. From then on every optimization carries a profiler
number instead of a cost calculation.

## Open questions

- none. `OPEN-3` was the last one and is answered: no hearts, no lives, a deadlock costs
  nothing (D-042).

## Where the truth lives

`decisions.md` (D-001…D-015) · `fingerprint.md` (authorities, scale, budgets) ·
`scope.md` (what the game is, vertical-slice boundary) ·
`reference/colorful-sort-mechanics.md` (mechanics, §8 = the variant scramble) ·
`blueprint.md` (systems, scenes, prefabs, folders) · `index.md` (start every task here)
