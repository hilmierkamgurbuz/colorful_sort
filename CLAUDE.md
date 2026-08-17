# Colorful Sort

<!-- 200-LINE LIMIT: This file is fully loaded every session and survives
     compaction. If it grows, both context space and adherence drop. Move
     detail into .claude/rules/ or .claude/ files. This comment block does
     not enter context. -->

## Phase

- **Phase:** production
<!-- production | shipping — release-grade in both; the phase only sets the
     proof standard for optimization (cost threshold vs measurement).
     Transitions happen only via the phase-transition audit (skill router). -->

## Invariants

<!-- Every item must be BINARY: violated / not violated at a glance.
     No "would be nice" items. No two rules may contradict each other.
     Day-zero items are STRUCTURAL, not optimizations. -->
- Content data (numbers, curves, tables, text) is never embedded in code; it is read from data.
- Save data carries a version number; unversioned saves are never written.
- Every piece of data has a single writer; if a second writer appears, code halts.
- Dependency arrows between systems are one-directional (plan: .claude/decisions.md).
- C# changes are made only with the Edit/Write tools; writing files via Bash
  is forbidden.
- `Board` rule code references no `UnityEngine` type. The puzzle is plain C# and
  unit-testable; anything that needs the engine lives in `BoardView`.
- `BoardView` and `UI` never write to `Board` state. They read it and send commands.
- Every board mutation is recorded as a move; nothing changes the board off the
  history, or undo cannot be exact.
- Gameplay randomness comes only from the attempt's seeded RNG.
  `UnityEngine.Random` and unseeded `System.Random` never appear in gameplay code.
- Level data stores logical colour ids only — never an RGB value, a symbol name,
  or a mesh reference. The `colourId → (colour, symbol)` mapping lives in exactly
  one asset, so a re-skin is a data edit.
- 1 logical cell = 1 Unity unit (art pack: 512 px @ PPU 512). No pixel constant
  is hard-coded in gameplay or view code.
- No text is baked into art. All runtime text is TextMeshPro.

## Fingerprint summary

<!-- Full version in .claude/fingerprint.md — this block is a few-line copy. -->
- Space: 2.5D — discrete column×cell grid for rules, 3D bricks + world sprites under one orthographic camera; 1 cell = 1 unit; portrait 1440×2560
- Determinism: required — undo must be exact, so one seeded RNG per level attempt and every draw recorded in the move history
- Authorities: board state + move history → Board · levels and the skin mapping → Content · progression → Meta · save file → Core
- Scale: ≤16 columns × ≤8 cells, ≤128 blocks, ≤12 colours, ≤256 history entries, ≤2000 levels

## Pointers

- **Start here every task:** .claude/index.md (system → where it lives)
- **What to build next:** .claude/roadmap.md (ordered remaining work, and the
  constraints each task must not lose)
- Scope: .claude/scope.md · Fingerprint: .claude/fingerprint.md
- Blueprint (systems/scenes/prefabs/folders): .claude/blueprint.md
- Reference-game mechanics (distilled, incl. open questions): .claude/reference/colorful-sort-mechanics.md
- Art pack contract + production sources: ArtSource/colorful-sort-pack/
- Decisions: .claude/decisions.md · Code map: .claude/codemap-*.md
- Scene map: .claude/unitymap.md · Asset map: .claude/assetmap.md
- Shard definition: .claude/shards.json · Domain rules: .claude/rules/
