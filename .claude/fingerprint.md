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
  randomness into a live board. So: one **seeded RNG per level attempt**, and
  every consumption of it recorded in the move history. The seed is not stored —
  it is *derived*, as a pure function of (level ordinal, attempt ordinal), and the
  save file carries both, so the same attempt is reproducible on any device and
  nothing reads a clock (D-017). The **Shuffle booster** is the RNG's only consumer. A Mystery reveal is
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
  - progression: current level, coins, booster charges → `Meta`
    (no hearts: `OPEN-3` was answered by dropping lives entirely, D-042)
  - the save file on disk → `Core` (save service; `Meta` asks, `Core` writes)
  - audio/vibration/settings toggles → `Core`
  - what the app asks of the display (frame-rate ceiling) → `Core`
    (`DisplayConfig` asset; `GameRoot` reads it once at boot and nothing writes it)
  - every visual transform, tween and sprite on the board → `BoardView`
    (it reads `Board`, it never writes to it)

- **Scale magnitudes (n):** deliberately tiny — this is why the cost model will
  almost always pick the readable design.
  - columns per board ≤ **16** (reference worst case seen: 12)
  - cells per column ≤ **8** (reference seen: 4)
  - blocks alive on a board ≤ **128**
  - distinct colours per level ≤ **11** — `BlockColourId` allows 1..12 and 12 symbol
    meshes exist, but `Block_Question` is the hidden `?` brick rather than a colour's
    symbol (D-021), so a twelfth colour needs a twelfth *colour* mesh
  - move-history entries per attempt ≤ **256**
  - simultaneously animating blocks ≤ **8** (one lifted run)
  - levels in the database ≤ **2000**
  - UI elements per screen ≤ **80**

- **Persistence:** JSON file in `Application.persistentDataPath`, written by
  `Core`. Saved: schema version, current level ordinal, per-level cleared state
  and play count, coins, booster charges, sound, music
  and vibration toggles, player id. No per-level **best** is stored: scope.md has
  neither a move counter nor a timer, so there is nothing to be best at — a star
  rating later is one more `saveVersion` plus a migration step, which is exactly what
  the migration step exists for. The shape on disk is at **version 2**: 1 -> 2 was spent
  on `musicOn`, because a `bool` absent from an older file parses to `false` and that
  cannot be told apart from a player who chose silence (D-045), so the next change
  is 3. The play count is not a statistic either: it is the attempt ordinal the
  seed is derived from (D-017). Versioning: an integer
  `saveVersion` field is written on every save and a migration step runs on load;
  the unused `hearts` and `heartRefillUnixMs` fields stay on disk rather than earning
  a version bump for their removal (D-042);
  an unversioned save is never written, and a file carrying no version or a newer
  one is refused and kept aside rather than rewritten (D-019).
  An in-progress board is **not** saved — quitting a level
  restarts it. (Reference behaviour unconfirmed; recorded as an assumption, not
  a finding.)

- **Network model:** none. No server, no account, no cloud save, no
  lockstep — so no authority model is needed and `ownership.md` stays a purely
  local question. Ad and IAP SDKs are out of scope for now; when they arrive
  they are edge services called by `Meta`, never authorities over game state.

- **Performance budget:** two tiers, and **decisions are judged against the
  floor**. The floor is **60 fps → 16.6 ms/frame** on a mid-tier Android phone:
  that is the hardware that constrains this game, and those phones are 60 Hz, so
  holding every choice to the higher tier would be a strictness the real device
  never asks for. The ceiling is **120 fps → 8.3 ms/frame** on a high-refresh
  screen, and it is *opportunistic*: `DisplayConfig` asks for 120, the display
  grants whatever it has, and the extra frames are headroom rather than a
  requirement (D-100). A frame-frequency decision that fits 16.6 ms passes; one
  that only fits by assuming 120 Hz hardware does not.
  The board is nearly static, so the steady-state target is well under half of
  the floor. Memory ceiling **≤ 350 MB** total on device. Level load
  (data → built board → first interactive frame) **≤ 100 ms**, so switching
  levels needs no loading screen. Draw calls on the gameplay scene ≤ 60.

- **Target platform/version:** Android (min API 24) and iOS (min 13), portrait
  only — and as of D-100 the project settings actually say so: min SDK was 23
  and all four autorotations were allowed, so both had to be brought to the
  plan rather than the plan to them. Unity **6000.0.80f1**, URP **17.0.4**, Input System 1.19.0,
  TextMeshPro via `com.unity.ugui` 2.0.0. Engine facts pin to this version.
