# blueprint — architecture plan: systems, scenes, prefabs, folders

<!-- The AI drafts it at bootstrap, the USER approves it. After that it
     changes only through preflight-declared tasks. This file is the single
     authority for "what goes where" — codemap, unitymap and assetmap all
     hang off it, and index.md is built by joining them to it.

     It is MACHINE-CHECKED: `python3 .claude/hooks/check_blueprint.py`
     compares every section below against the disk and against the codemap
     `sys:` fields, and the postflight quotes its output. A system name here
     is therefore a contract, not a label — spell it in `sys:` exactly as it
     is spelled here. -->

## Systems and dependencies

<!-- system → system arrows, ONE-directional. A bidirectional arrow is an
     ownership problem: apply procedures/ownership.md before writing it
     down. One line per system: name — responsibility — depends-on.

     The spine of this design: Board is pure C# with no UnityEngine types in
     its rules, so the whole puzzle is unit-testable and undo is provably
     exact. Everything visual hangs off it one-way.

     That is why the arrow runs Content -> Board and not the other way: Board
     owns a plain-C# `LevelData` input contract and `Content`'s authored
     ScriptableObjects convert themselves into it. Board's assembly definition
     sets `noEngineReferences: true`, so the invariant is enforced by the
     compiler rather than by review — Board physically cannot see UnityEngine,
     which it could not do if it had to read a ScriptableObject. -->
- Core — boot, scene flow, save file I/O, audio, haptics, settings toggles, service access — depends on: -
- Board — deterministic puzzle logic: board state, legal-move test, apply/undo, ice/cover/mystery modifiers, win and deadlock detection, seeded attempt RNG — depends on: -
- Content — level definitions, column layouts, difficulty labels, and the colourId → (colour, symbol) skin mapping; converts authored assets into Board's plain input contract — depends on: Board
- BoardView — scene side of the board: builds columns and bricks, animates lift/drop/reveal, hit-tests taps, mirrors Board state — depends on: Board, Content
- Boosters — the add-column, undo and shuffle commands, their charges and their cost — depends on: Board, Meta
- Meta — progression: current level, coins, hearts and refill, booster inventory, level cleared state — depends on: Core, Content
- UI — HUD, popups (pause, settings, win, fail), main menu; reads state and sends commands — depends on: Board, Meta, Boosters, Content, Core
- TemplateLeftovers — TEMPORARY: files the Unity 3D-project template shipped, pending removal; no game code depends on them — depends on: -

## Scene inventory

<!-- Every scene, starting with the boot/persistent scene. One line each:
     name — role — load mode (single | additive + trigger) — what lives
     in it. -->
- Boot — persistent services, never unloaded; decides where to go on start — single, first in Build Settings — GameRoot with Core services (save, audio, haptics, settings) and the persistent Camera-independent UI root
- Menu — main menu: level button, coins, hearts, progress bar, Settings popup — additive, loaded by Core on boot and after quitting a level — menu background, HUD, popup host
- Game — the playable board — additive, loaded by Core when a level starts, unloaded on quit — orthographic board camera, board root, gameplay HUD, booster bar
- SampleScene — TEMPORARY: the Unity 3D-template sample scene, owned by TemplateLeftovers — single — template content only; deleted together with `Assets/TutorialInfo/` and `Assets/Readme.asset` in the task that creates Boot/Menu/Game

## Prefab inventory

<!-- Every prefab: name — owning system — variant-of (or -) — where it is
     instantiated from (authoring | spawner). scene-structure.md decides
     what becomes a prefab. -->
- Block — BoardView — variant-of: - — spawned by BoardView's block pool; the symbol mesh and colour are applied at spawn from the skin set, so there is ONE block prefab, not one per colour
- Column — BoardView — variant-of: - — spawned by the board builder; base normal column
- Column_Ice — BoardView — variant-of: Column — spawned by the board builder for ice columns
- Column_Covered — BoardView — variant-of: Column — spawned by the board builder for covered columns
- Popup_Pause — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Settings — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Win — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Fail — UI — variant-of: - — authoring, instantiated by the popup host
- BoosterButton — UI — variant-of: - — authoring, three instances in the Game scene

## Hierarchy conventions

<!-- Per-scene root objects and naming. Keep runtime-moved objects shallow. -->
- root objects, in this order: `--Systems--`, `--Camera--`, `--Board--`, `--UI--`
- naming: PascalCase, no spaces; prefab instances keep the prefab name plus an index suffix (`Column_03`)
- runtime-moved objects stay shallow: a Block is a direct child of the board root while it flies, and is re-parented to its Column only once it settles
- sorting order is fixed by the art pack: background 0 · column 10 · 3D bricks 20 · cover/ice/mystery overlay 30 · screen UI 100+
- one orthographic camera renders the board; screen UI is a separate Screen Space - Overlay canvas

## Folder layout

<!-- Canonical tree. It and `.claude/shards.json` describe the same folders
     from two sides: change one, change the other in the same task, or
     every file lands in the catch-all shard. A new file with no place in
     this tree is a blueprint update FIRST, a file second.
     check_blueprint.py reports folders on disk that no line here covers. -->
```
Assets/
  Scripts/Core/        ← .cs: boot, scene flow, save, audio, haptics, settings (codemap: core)
  Scripts/Gameplay/Board/     ← .cs: engine-free puzzle rules; own asmdef, noEngineReferences (codemap: gameplay)
  Scripts/Gameplay/BoardView/ ← .cs: scene side of the board — columns, bricks, animation, input (codemap: gameplay)
  Scripts/Gameplay/Boosters/  ← .cs: add-column, undo, shuffle commands (codemap: gameplay)
  Scripts/UI/          ← .cs: HUD, popups, menu (codemap: ui)
  Scripts/Content/     ← .cs: level + skin data types and their lookup (codemap: content)
  Scripts/Meta/        ← .cs: progression, currencies, hearts, booster inventory (codemap: meta)
  Tests/Board/         ← .cs: unit tests for the engine-free Board assembly (codemap: tests)
  Editor/              ← editor-only tools: level editor window, sprite import pass, map exporter (codemap: editor)
  Data/Levels/         ← level definition assets (assetmap)
  Data/Blocks/         ← BlockSkinSet + per-colour skin assets (assetmap)
  Data/Config/         ← game config: hearts, booster costs, animation timings (assetmap)
  Prefabs/BoardView/   ← Block, Column, Column_Ice, Column_Covered (assetmap + unitymap)
  Prefabs/UI/          ← popups, booster button (assetmap + unitymap)
  Scenes/              ← .unity files (mirrors the scene inventory)
  Art/Models/Blocks/   ← 12 brick FBX meshes, one per symbol
  Art/Materials/       ← brick materials, one per logical colour
  Art/Sprites/Backgrounds/  ← menu + gameplay backgrounds, tiling pattern
  Art/Sprites/Gameplay/     ← slot, ice, cover, mystery sprites
  Art/Sprites/UI/           ← Buttons/ HUD/ Icons/ Popups/ Settings/
  Audio/               ← sfx and music
Settings/              ← URP render pipeline assets (Unity template, left in place)
```

<!-- Production art sources (ImageGen raws, python processors, legacy SVG,
     drafts, the pack's own README / import guide / manifest / QA report) live
     in `<root>/ArtSource/`, deliberately OUTSIDE `Assets/` so Unity never
     imports them. They stay in version control; they are just not game data.

     `Data/` holds assets, not scripts, so it is inventoried by assetmap.md,
     not by a codemap shard. `Resources/` and `StreamingAssets/` are absent
     on purpose: everything in them ships in every build and is loaded by
     string. Adding one is an architectural decision — it goes to
     decisions.md with its `affects:` field, not into this tree quietly. -->
