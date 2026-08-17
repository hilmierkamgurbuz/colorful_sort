# fingerprint — project profile

<!-- The AI drafts it, the USER VERIFIES it (mandatory — this file is about
     the project's intent). Any unanswered field is written as OPEN; progress
     does not stop, and the field surfaces as an assumption in the preflight
     of the first task that touches it. The summary is copied into the root
     CLAUDE.md. -->

- **Space model:** 2.5D. Logic is a **discrete grid**: a board is an array of
  columns, each column an array of cells, indexed bottom-up — no float
  positions in the rules. Presentation is 3D brick meshes plus 2D world-space
  sprites under one **orthographic** camera. Unit size is **fixed and given by
  the art pack**: 1 logical cell = 512 px = 1 Unity unit
  (`Pixels Per Unit: 512`). Portrait, reference resolution 1440×2560; Canvas
  Scaler reference 1080×1920, `Match Width Or Height: 0.5`.

- **Determinism:** Required, and it is a hard requirement rather than a nicety.
  Undo must reproduce the exact board it left, and the Shuffle booster injects
  randomness into a live board. So: one **seeded RNG per level attempt**, its
  seed stored with the attempt, and every consumption of it recorded in the move
  history. The **Shuffle booster** is the RNG's only consumer. A Mystery reveal is
  not one: what sits under a `?` is authored and fixed when the level opens (D-011,
  user-confirmed). Neither is the per-attempt look that stops a replay from
  replaying memorised taps — a level offers a small config-sized set of variants,
  each a pure function of (level index, variant index) built from a stream private
  to that level, so choosing one never moves the attempt's cursor (D-014, D-015).
  Board rules are integer/enum only — no float tolerance question arises,
  because no float ever enters a rule.

- **Data authorities:** one writer each; a second writer halts the code.
  - board state (columns, cells, colour ids, modifier state) → `Board`
  - move history + attempt RNG → `Board`
  - level definitions, column layout, capacities, difficulty label → `Content`
    (read-only at runtime)
  - `colourId → (colour, symbol mesh)` skin mapping → `Content`
    (`BlockSkinSet` asset — the single place a re-skin happens)
  - progression: current level, coins, hearts, booster charges → `Meta`
  - the save file on disk → `Core` (save service; `Meta` asks, `Core` writes)
  - audio/vibration/settings toggles → `Core`
  - every visual transform, tween and sprite on the board → `BoardView`
    (it reads `Board`, it never writes to it)

- **Scale magnitudes (n):** deliberately tiny — this is why the cost model will
  almost always pick the readable design.
  - columns per board ≤ **16** (reference worst case seen: 12)
  - cells per column ≤ **8** (reference seen: 4)
  - blocks alive on a board ≤ **128**
  - distinct colours per level ≤ **12** (12 symbol meshes exist)
  - move-history entries per attempt ≤ **256**
  - simultaneously animating blocks ≤ **8** (one lifted run)
  - levels in the database ≤ **2000**
  - UI elements per screen ≤ **80**

- **Persistence:** JSON file in `Application.persistentDataPath`, written by
  `Core`. Saved: schema version, current level index, per-level cleared/best
  state, coins, hearts + their refill timestamp, booster charges, sound and
  vibration toggles, player id. Versioning: an integer `saveVersion` field is
  written on every save and a migration step runs on load; an unversioned save
  is never written. An in-progress board is **not** saved — quitting a level
  restarts it. (Reference behaviour unconfirmed; recorded as an assumption, not
  a finding.)

- **Network model:** none. No server, no account, no cloud save, no
  lockstep — so no authority model is needed and `ownership.md` stays a purely
  local question. Ad and IAP SDKs are out of scope for now; when they arrive
  they are edge services called by `Meta`, never authorities over game state.

- **Performance budget:** 60 fps → **16.6 ms/frame** on a mid-tier Android
  phone; the board is nearly static, so the steady-state target is well under
  half of that. Memory ceiling **≤ 350 MB** total on device. Level load
  (data → built board → first interactive frame) **≤ 100 ms**, so switching
  levels needs no loading screen. Draw calls on the gameplay scene ≤ 60.

- **Target platform/version:** Android (min API 24) and iOS (min 13), portrait
  only. Unity **6000.0.80f1**, URP **17.0.4**, Input System 1.19.0,
  TextMeshPro via `com.unity.ugui` 2.0.0. Engine facts pin to this version.
