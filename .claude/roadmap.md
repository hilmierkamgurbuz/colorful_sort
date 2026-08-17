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

Verification note: the Editor was holding the project lock, so the suite was run
outside Unity against the same source files (`Board` is engine-free by design).
**The Editor's Test Runner has not yet confirmed a green run** — Window > General >
Test Runner > EditMode > Run All. Nothing depends on it, but it is the real gate.

## Next, in order

### 1. Scene skeleton + Core services
`Boot` / `Menu` / `Game` scenes, `Core` services: save (JSON in
`Application.persistentDataPath`), scene flow, audio, haptics, settings toggles, and
the attempt seed source. Deletes `SampleScene` and `Assets/TutorialInfo/`, which also
retires the `TemplateLeftovers` system line and its scene/folder entries in
`blueprint.md`, and clears `unitymap.md`'s pre-existing `DEGRADED 3 missing-script`.

Must not lose:
- save data carries `saveVersion` and a migration step; an unversioned save is never
  written (CLAUDE.md invariant, fingerprint.md → Persistence)
- an in-progress board is **not** saved; quitting a level restarts it (D-008)
- the variant **count** is a tuning number and belongs in `Data/Config/`, never in
  code (D-015, `.claude/rules/data.md`)
- `Meta` chooses which variant an attempt plays. Rotating on each replay of a level
  is the intended policy, which means the save file needs a per-level play count
- the save file has exactly one writer: `Core`. `Meta` asks, `Core` writes
  (fingerprint.md → Data authorities)

### 2. BoardView — the board on screen
`Column` / `Column_Ice` / `Column_Covered` / `Block` prefabs, the board builder, tap
input, and the lift / arc / settle animation.

Must not lose:
- a column's screen position comes from its index in the **attempt's** level, never
  from anything authored, or the variant reordering never reaches the screen
  (D-014, D-015, reference §8)
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

### 3. Level editor window + first content
A custom `EditorWindow` (D-010): draw the grid, pick kind and capacity, fill cells,
save, validate. Then transcribe Level 79 (12 columns × 4 cells, 2 rows of 6) and
author the `BlockSkinSet` plus its per-colour skins.

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

### 4. UI — HUD and popups
Gameplay HUD (level plaque, difficulty label, gear), Pause popup, Win and Fail
popups, then the main menu (coins, hearts, level button, progress bar) and Settings.

Must not lose:
- all runtime text is TextMeshPro; no text is baked into art (CLAUDE.md invariant).
  Reference style: off-white `#FFF6D6` fill, dark purple/brown outline, soft shadow
- Canvas Scaler reference 1080×1920, `Match Width Or Height: 0.5`
- buttons ship `_normal` / `_pressed` / `_disabled` → `Transition: Sprite Swap`
- the arrow is one-way: UI → gameplay. Gameplay holds no reference to UI
- popup contents follow the reference layouts (reference §4)

### 5. Boosters + Meta
Undo, add-column and shuffle, their charges and costs; coins, hearts and refill,
booster inventory, level-cleared state, progression.

Must not lose:
- Shuffle is the RNG's only consumer, and every draw it makes is recorded in the move
  history, or undo cannot reproduce what it undid (D-002)
- undo must work across a mystery reveal and across a cover opening; both are already
  recorded and tested
- history is capped at 256 entries and reports what it dropped, so the Undo booster
  can tell the player rather than look broken
- `OPEN-3` has to be answered here: does a deadlock cost a heart? Board only raises
  the event today, so nothing else is blocked until this task

### 6. Phase transition
When the release scope is content-complete, run `phase-transition.md` to move from
`production` to `shipping`. From then on every optimization carries a profiler
number instead of a cost calculation.

## Open questions

- `OPEN-3` — deadlock: instant fail popup that costs a heart, or does the player sit
  there until they restart? Assumed: popup + one heart, restart from the popup free.
  Blocks nothing before task 5. (reference §7)

## Where the truth lives

`decisions.md` (D-001…D-015) · `fingerprint.md` (authorities, scale, budgets) ·
`scope.md` (what the game is, vertical-slice boundary) ·
`reference/colorful-sort-mechanics.md` (mechanics, §8 = the variant scramble) ·
`blueprint.md` (systems, scenes, prefabs, folders) · `index.md` (start every task here)
