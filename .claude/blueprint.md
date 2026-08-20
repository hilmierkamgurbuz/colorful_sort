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
- Core — boot, scene flow, save file I/O, audio, haptics, settings toggles, service access, and the
  app's own frame-rate ceiling (`DisplayConfig`, read once at boot — Unity has no project setting
  for it, so somebody has to say the number and it is data, D-100) — depends on: -
- Board — deterministic puzzle logic: board state, legal-move test, apply/undo, ice/cover/mystery modifiers, win and deadlock detection, seeded attempt RNG — depends on: -
- Content — level definitions, column layouts, difficulty labels, and the colourId → (colour, symbol) skin mapping; converts authored assets into Board's plain input contract — depends on: Board
- BoardView — scene side of the board: builds columns and bricks from the **attempt**, animates lift/drop/reveal, hit-tests taps, mirrors Board state. Its scale comes from the art (a column's 9-slice border, D-025) and its framing from the board it is given (D-026); the only authored numbers are three gaps in `Data/Config/` — depends on: Board, Content
- Boosters — the add-column, undo and shuffle commands. **Not a code folder:** the three are board mutations, so they live in `Board` with the rules and tests that prove them (`BoardSession.Undo/TryAddColumn/TryShuffle`), and "which button sends which" is one `UI` component (`BoosterButton`). Charges and cost came BACK in 5B-4 (D-091, reopening D-043): a press costs a charge, a charge is bought with coins, and both live in `Meta` — `PlayerEconomy` over the save, priced by `ProgressionConfig` — while the three mutations in `Board` never learn that one of them was paid for. A system whose whole content is three method calls still does not earn an assembly; the line stays so its arrows remain written down — depends on: Board, Meta
- Meta — progression: current level, coins, hearts and refill, booster inventory, level cleared state. It also owns the attempt seam: (level, variant, attempt ordinal) becomes a `BoardSession` here and is handed to the view, so progression policy never reaches the renderer (D-027) — depends on: Core, Content, Board, BoardView
- UI — HUD (level plaque, gear, coin pill, booster bar), popups (pause, settings, win, fail, booster shop), main menu; reads state and sends commands — depends on: Board, Meta, Boosters, Content, Core
- Tooling — editor-only project tools: the scene bootstrapper, the map exporter, the level editor window (D-010) and the sprite import pass. Never in a build, so its arrows cannot reach runtime — depends on: Core, Content

## Scene inventory

<!-- Every scene, starting with the boot/persistent scene. One line each:
     name — role — load mode (single | additive + trigger) — what lives
     in it. -->
- Boot — persistent Core services, never unloaded; loads the first screen on start — single, first in Build Settings — one `--Systems--` root carrying `GameRoot` (save, scene flow, the settings toggles; audio and haptics join it in task 1B) and the project's single `AudioListener`, plus a `--UI--` root carrying the persistent popup canvas (Overlay, sorting 200), `PopupHost` and the project's single `EventSystem`. The coin-flight layer 5B-4 added here is gone: a win says what it paid instead of throwing coins at a counter (D-092)
- Menu — main menu: level button, coins, hearts, progress bar, Settings popup — additive, loaded by Core on boot and after quitting a level — `--Camera--` and `--UI--` carrying an Overlay canvas at sorting 100, a `SafeArea` panel and `MainMenu`. It carries **no** popup host: there is exactly one, on Boot, because it has to outlive every screen swap (D-036-adjacent, 4A). Today the screen holds only the Play button — the counters and the background arrive with 4C, after the progression slice gives them something true to show
- Game — the playable board — additive, loaded by Core when a level starts, unloaded on quit — orthographic board camera, `--Board--` carrying `BoardView`, `BoardInput` and `BoardMoveAnimator` with `Columns` and `Pool` children, `--Systems--` carrying the attempt starter, `--Light--`, and `--UI--` carrying the gameplay HUD (Overlay canvas at sorting 100, a `SafeArea` panel, the level plaque, the gear and the booster bar's three `BoosterButton` instances — and deliberately no coin counter, since the only balance in the game is in the booster shop, where it can be spent (D-092))
- UI — **editor-only**, and the one scene that is not a screen: the environment Unity opens a UI prefab inside, so Prefab Mode shows an 880x900 panel at the size the game will rather than floating at a scale that means nothing (UX-1). Never in Build Settings, never loaded at runtime — opened by the editor itself when a UI prefab is double-clicked, through `EditorSettings.prefabUIEnvironment` — it lives under `Assets/Editor/PrefabEnvironments/` and holds one canvas built by calling `UiFactory.EnsureCanvas`, so the two numbers every screen scales by are stated in exactly one place

## Prefab inventory

<!-- Every prefab: name — owning system — variant-of (or -) — where it is
     instantiated from (authoring | spawner). scene-structure.md decides
     what becomes a prefab. -->
- Block — BoardView — variant-of: - — spawned by BoardView's block pool; the symbol mesh and colour are applied at spawn from the skin set, so there is ONE block prefab, not one per colour
- Column — BoardView — variant-of: - — spawned by the board builder; base normal column
- Column_Ice — BoardView — variant-of: Column — spawned by the board builder for ice columns
- Column_Covered — BoardView — variant-of: Column — spawned by the board builder for covered columns
- Column_ice1 — BoardView — variant-of: - — authoring, user-authored and kept on purpose: a hand-assembled ice column (its own `ice_frost_band` and `ice_crystal_*` children rather than the pack's integrated ice sprite); nothing instantiates it — the board builder spawns `Column_Ice` — so it is a reference/spare, not a runtime prefab
- Popup_Pause — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Settings — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Win — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_Fail — UI — variant-of: - — authoring, instantiated by the popup host
- Popup_BoosterShop — UI — variant-of: - — authoring, instantiated by the popup host; ONE prefab for all three boosters, its title, blurb and buy caption written at runtime from `UiStyleConfig` and its icon handed over by the button that opened it. It carries the game's only coin pill (`CoinHud`) at its top-left, and its own full-screen `Scrim` as first child — a second darkening layer over the host's, which stays for every other popup and for the tap it blocks (D-092, D-093)
- BoosterButton — UI — variant-of: - — authoring, three instances in the Game scene, differing only in which command they send and what they say; the pack ships no booster icons, so they carry text until the user dresses them

## Hierarchy conventions

<!-- Per-scene root objects and naming. Keep runtime-moved objects shallow. -->
- root objects, in this order: `--Systems--`, `--Camera--`, `--Board--`, `--UI--`, `--Light--`
- `--Light--` exists wherever 3D bricks do: they carry URP **Lit** materials because the
  symbol is embossed geometry, so an unlit board would read as flat colour. The light's
  angle is a look decision and the scene is its authority, never code
- a persistent service object stays a **root** object: `DontDestroyOnLoad` keeps root
  objects only, so Boot's `--Systems--` carries `GameRoot` itself rather than parenting
  it. The single `AudioListener` lives there too — one listener that outlives every
  screen swap, instead of one per screen camera
- naming: PascalCase, no spaces; prefab instances keep the prefab name plus an index suffix (`Column_03`)
- runtime-moved objects stay shallow: a Block is a direct child of the board root while it flies, and is re-parented to its Column only once it settles
- sorting order is fixed by the art pack: background 0 · column 10 · 3D bricks 20 · cover/ice/mystery overlay 30 · screen UI 100+
- one orthographic camera renders the board; screen UI is a separate Screen Space - Overlay canvas
- there are exactly two UI canvases and they never merge: the screen's own HUD at sorting order
  100, and Boot's persistent popup canvas at 200. "On top" is therefore a fact about the canvas
  rather than about the order objects were added in, and the popup canvas needs no camera, which
  is what lets it outlive the additive Menu/Game swap
- exactly one `EventSystem` exists, on Boot, carrying `InputSystemUIInputModule` — this project
  runs the new input backend only (`activeInputHandler: 1`), so the legacy module is compiled out
  and choosing it leaves every button silently dead
- only what is meant to be pressed is a raycast target. A decorative panel or plaque left as one
  sits over the board and swallows the taps underneath it, because `BoardInput` refuses any press
  that hits a UI graphic (D-037)

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
  Scripts/UI/          ← .cs: HUD, popups, menu (codemap: ui)
  Scripts/Content/     ← .cs: level + skin data types and their lookup (codemap: content)
  Scripts/Meta/        ← .cs: progression, currencies, hearts, booster inventory (codemap: meta)
  Tests/Board/         ← .cs: unit tests for the engine-free Board assembly (codemap: tests)
  Tests/Core/          ← .cs: save, scene flow and seed tests (codemap: tests)
  Tests/BoardView/     ← .cs: layout and hit-test maths (codemap: tests)
  Tests/Meta/          ← .cs: progression tests — the save's writer, so it is tested (codemap: tests)
  Editor/              ← editor-only tools: level editor window, sprite import pass, map exporter (codemap: editor)
  Editor/PrefabEnvironments/ ← a scene Unity opens UI prefabs inside (Prefab Mode). It is a
                         TOOL, not a game scene: it is under Editor/ so no build sees it, and it is
                         deliberately absent from the scene inventory above, which lists what the
                         game loads. Rebuild it with Tools > Colorful Sort > Set Up UI Prefab Editing
  Data/Levels/         ← Levels.json, every level in one compact file, one per line, plus the
                         LevelDatabase asset that points at it as a TextAsset. NOT assets: a
                         level is decoded into a transient object and never saved (D-085)
  Data/Blocks/         ← BlockSkinSet + per-colour skin assets (assetmap)
  Data/Config/         ← game config: hearts, booster costs, animation timings, and what the
                         app asks of the display — the frame-rate ceiling (assetmap)
  Prefabs/BoardView/   ← Block, Column, Column_Ice, Column_Covered (assetmap + unitymap)
  Prefabs/UI/          ← popups, booster button (assetmap + unitymap)
  Scenes/              ← .unity files (mirrors the scene inventory)
  Art/Models/Blocks/   ← 12 brick FBX meshes, one per symbol
  Art/Models/Blocks/Symbols/ ← each brick's engraved symbol lifted out on its own, one mesh
                         asset per skin. DERIVED, not authored: generated from the FBX above by
                         Tools > Colorful Sort > Create Block Skins and rebuilt on every run, so
                         a re-exported brick cannot leave an old symbol behind (D-080). It is the
                         shape the sparks a landing throws are drawn with
  Art/Materials/       ← brick materials (one per logical colour, plus the symbol's darker twin),
                         the slot base plate's, and the lifted run's additive glow
  Art/Materials/UI/    ← the one generated TextMeshPro material carrying the reference text style
  Art/Shaders/         ← the project's own shaders. One so far: the additive glow behind a lifted
                         run, because URP ships no additive sprite material (D-061)
  Art/Sprites/Backgrounds/  ← menu + gameplay backgrounds, tiling pattern
  Art/Sprites/Gameplay/     ← slot, ice, cover, mystery sprites
  Art/Sprites/UI/           ← Buttons/ HUD/ Icons/ Popups/ Settings/
  Audio/               ← sfx and music
  TextMesh Pro/        ← TMP's own imported essentials (font asset + shaders). Unity writes this
                         folder, not us; it is here so check_blueprint.py does not read it as drift
  Settings/            ← URP render pipeline assets. NO LONGER "template, left in place":
                         both RP assets point their m_VolumeProfile at SampleSceneProfile, so it
                         is the colour pipeline every scene renders through and it is authored
                         now — vignette off, tonemapping None, because a tonemap between an
                         authored brick colour and the screen means the two can never be equal
                         (D-095). Ambient is the SCENE's, not this folder's (D-095)
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
