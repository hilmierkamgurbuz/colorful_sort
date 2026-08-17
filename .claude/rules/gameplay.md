---
paths:
  - "Assets/Scripts/Gameplay/**"
---
<!-- Path-scoped rule — loading behavior: see the note in ui.md. -->

# Gameplay domain rules

`Assets/Scripts/Gameplay/` holds three systems — `Board` (rules), `BoardView`
(scene side) and `Boosters`. The split below is the whole point of the folder;
a file that blurs it is in the wrong system.

## Board — the rules

- No `UnityEngine` type appears in `Board`. Not `Vector3`, not `Mathf`, not
  `MonoBehaviour`, not `[SerializeField]`. Board is plain C# so the puzzle can
  be unit-tested and undo can be proved exact.
- Board state is addressed as `(columnIndex, cellIndex)`, cells indexed
  **bottom-up**. No world position, no pixel value, no float ever enters a rule.
- Every mutation goes through a move object that can revert itself. There is no
  "just set this field" path into board state.
- Randomness comes only from the attempt's seeded RNG, and each draw is recorded
  in the move history. A mystery reveal and a shuffle are moves like any other.
- Legal-move tests are pure functions of state: same input, same answer, no
  side effects, no logging.
- Win and deadlock are computed from state, never tracked as a flag that some
  other code remembers to set.

## BoardView — the scene side

- Reads `Board`; never writes to it. A tap becomes a *command* sent to `Board`,
  and the view redraws from whatever `Board` reports back.
- Blocks come from a pool. There is one `Block` prefab; the colour and the
  symbol mesh are applied at spawn from the skin set — never one prefab per
  colour.
- No `GameObject.Find`, no `Camera.main` in a per-frame path, no `GetComponent`
  in an update loop. References are bound as `reference-binding.md` decides.
- Animation is view-only. A board mutation is committed in `Board` the moment
  the move is legal; the tween is what the player watches afterwards, and
  interrupting it never leaves the two out of sync.
- Sorting order is the art pack's, not a local invention: background 0 ·
  column 10 · bricks 20 · cover/ice/mystery overlay 30.

## Both

- Gameplay code holds no reference to `UI`. The arrow is one-way: UI → gameplay.
- No allocation inside frame-frequency loops. With n ≤ 128 blocks the cost model
  will usually pick the readable design — but a per-frame allocation is a
  structural defect, not a performance trade-off, so it is out regardless.
- Timings, costs and tuning numbers are read from `Data/Config/`, never typed
  into a field default and never left as a literal in a method.
