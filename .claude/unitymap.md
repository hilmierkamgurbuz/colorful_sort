<!-- stamp: 2026-08-20T23:29Z source-sig:6396e3e163c0 scenes:4 prefabs:10 generator:python-fallback status: DEGRADED 86 missing-script -->
# unitymap — scene and prefab structure

Read this instead of opening a `.unity`/`.prefab` file. Tree indentation is
the GameObject hierarchy; `[...]` lists the components on the object;
`refs:` lists serialized reference slots and whether the Inspector has
something in them. `*` marks a prefab instance.

Staleness: `source-sig` is derived from scene/prefab mtimes. Regenerate with
`python3 .claude/hooks/build_unitymap.py` or, for real type information, the
Unity menu item Tools > unity-dev > Export unitymap.

## Findings
- MISSING SCRIPT | Assets/Editor/PrefabEnvironments/UI.unity | --UI--
- MISSING SCRIPT | Assets/Prefabs/UI/BoosterButton.prefab | BoosterButton
- MISSING SCRIPT | Assets/Prefabs/UI/BoosterButton.prefab | Count
- MISSING SCRIPT | Assets/Prefabs/UI/BoosterButton.prefab | CountBadge
- MISSING SCRIPT | Assets/Prefabs/UI/BoosterButton.prefab | Icon
- MISSING SCRIPT | Assets/Prefabs/UI/BoosterButton.prefab | PlusBadge
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Amount
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Blurb
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Body
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Buy
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Coin
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | CoinPill
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Icon
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Price
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Scrim
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | Title
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_BoosterShop.prefab | ttt
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Fail.prefab | Body
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Fail.prefab | Quit_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Fail.prefab | Retry_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Fail.prefab | Text (TMP)
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Fail.prefab | Title
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Body
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Continue_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Music
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Music_off
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Music_on
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Quit_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Restart_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Sound
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Text (TMP)
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Title
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | Vibration
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | close_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | haptic_off
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | haptic_on
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | sound_off
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Pause.prefab | sound_on
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | Amount
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | Body
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | Coin
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | NEXT_Button
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | Text (TMP)
- MISSING SCRIPT | Assets/Prefabs/UI/Popup_Win.prefab | Title
- MISSING SCRIPT | Assets/Scenes/Boot.unity | --UI--
- MISSING SCRIPT | Assets/Scenes/Boot.unity | EventSystem
- MISSING SCRIPT | Assets/Scenes/Boot.unity | Scrim
- MISSING SCRIPT | Assets/Scenes/Game.unity | --Camera--
- MISSING SCRIPT | Assets/Scenes/Game.unity | --Light--
- MISSING SCRIPT | Assets/Scenes/Game.unity | --UI--
- MISSING SCRIPT | Assets/Scenes/Game.unity | DifficultyLabel
- MISSING SCRIPT | Assets/Scenes/Game.unity | Gear
- MISSING SCRIPT | Assets/Scenes/Game.unity | Icon
- MISSING SCRIPT | Assets/Scenes/Game.unity | LevelLabel
- MISSING SCRIPT | Assets/Scenes/Game.unity | Plaque
- MISSING SCRIPT | Assets/Scenes/Menu.unity | --Camera--
- MISSING SCRIPT | Assets/Scenes/Menu.unity | --UI--
- MISSING SCRIPT | Assets/Scenes/Menu.unity | Background
- MISSING SCRIPT | Assets/Scenes/Menu.unity | Label
- MISSING SCRIPT | Assets/Scenes/Menu.unity | Play
- ... 52 more

## SCENE Assets/Editor/PrefabEnvironments/UI.unity   (1 object(s))
- --UI--  [MISSING SCRIPT (guid:dc42784c), MISSING SCRIPT (guid:0cd44c10), Canvas, RectTransform]

## PREFAB Assets/Prefabs/BoardView/Block.prefab   (2 object(s))
- Block  [Transform, MeshFilter, MeshRenderer, BlockView]  refs: meshFilter=set, meshRenderer=set, plume=set
  - Plume  [Transform, ParticleSystem, ParticleSystemRenderer]  refs: LightsModule=NULL, ShapeModule=NULL, SubModule=NULL, UVModule=NULL, moveWithCustomTransform=NULL

## PREFAB Assets/Prefabs/BoardView/Column.prefab   (8 object(s))
- Column  [Transform, ColumnView]  refs: blockRoot=set, cellDivider=set, coverBottomCap=NULL, coverCell=NULL, coverRoot=set, coverSeparator=NULL, coverTopCap=NULL, finish=set, rise=set, settledShadow=set, slot=set, thawedSlot=NULL
  - Slot  [Transform, SpriteRenderer]
  - Blocks  [Transform]
  - Cover  [Transform]
  - Base  [Transform, MeshFilter, MeshRenderer]
  - SettledShadow  [Transform, SpriteRenderer]
  - Finish  [Transform, ParticleSystem, ParticleSystemRenderer]  refs: LightsModule=NULL, ShapeModule=NULL, SubModule=NULL, UVModule=NULL, moveWithCustomTransform=NULL
  - Rise  [Transform, ParticleSystem, ParticleSystemRenderer]  refs: LightsModule=NULL, ShapeModule=NULL, SubModule=NULL, UVModule=NULL, moveWithCustomTransform=NULL

## PREFAB Assets/Prefabs/BoardView/Column_Covered.prefab   variant-of: Assets/Prefabs/BoardView/Column.prefab   (1 object(s))
- * Column_Covered  (prefab instance of Assets/Prefabs/BoardView/Column.prefab)

## PREFAB Assets/Prefabs/BoardView/Column_Ice.prefab   variant-of: Assets/Prefabs/BoardView/Column.prefab   (1 object(s))
- * Column_Ice  (prefab instance of Assets/Prefabs/BoardView/Column.prefab)

## PREFAB Assets/Prefabs/BoardView/Column_ice1.prefab   (11 object(s))
- Column_ice1  [Transform, ColumnView]  refs: blockRoot=set, cellDivider=set, coverBottomCap=NULL, coverCell=NULL, coverRoot=set, coverSeparator=NULL, coverTopCap=NULL, finish=set, rise=set, settledShadow=set, slot=set, thawedSlot=NULL
  - Slot  [Transform, SpriteRenderer]
  - Blocks  [Transform]
  - Cover  [Transform]
  - ice_frost_band  [Transform, SpriteRenderer]
  - ice_crystal_center  [Transform, SpriteRenderer]
  - ice_crystal_center (1)  [Transform, SpriteRenderer]
  - Base  [Transform, MeshFilter, MeshRenderer]
  - SettledShadow  [Transform, SpriteRenderer]
  - Finish  [Transform, ParticleSystem, ParticleSystemRenderer]  refs: LightsModule=NULL, ShapeModule=NULL, SubModule=NULL, UVModule=NULL, moveWithCustomTransform=NULL
  - Rise  [Transform, ParticleSystem, ParticleSystemRenderer]  refs: LightsModule=NULL, ShapeModule=NULL, SubModule=NULL, UVModule=NULL, moveWithCustomTransform=NULL

## PREFAB Assets/Prefabs/UI/BoosterButton.prefab   (5 object(s))
- BoosterButton  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8), BoosterButton]  refs: button=set, countBadge=set, countLabel=set, icon=set, plusBadge=set
  - Icon  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
  - CountBadge  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Count  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
  - PlusBadge [inactive]  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]

## PREFAB Assets/Prefabs/UI/Popup_BoosterShop.prefab   (14 object(s))
- Popup_BoosterShop  [RectTransform, BoosterShopPopup]  refs: blurbLabel=set, buyButton=set, buyLabel=NULL, closeButton=set, coinHud=set, iconImage=set, priceLabel=set, titleLabel=set
  - Scrim  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
  - Body  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Title  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - ttt  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Icon  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Blurb  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Icon  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Buy  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Coin  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
      - Price  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
  - CoinPill  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), CoinHud]  refs: amountLabel=set, style=set
    - Coin  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Amount  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL

## PREFAB Assets/Prefabs/UI/Popup_Fail.prefab   (7 object(s))
- Popup_Fail  [RectTransform, FailPopup]  refs: quitButton=set, retryButton=set
  - Body  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Title  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Retry_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Quit_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL

## PREFAB Assets/Prefabs/UI/Popup_Pause.prefab   (20 object(s))
- Popup_Pause  [RectTransform, PausePopup]  refs: closeButton=set, continueButton=set, playerIdLabel=NULL, quitButton=set, restartButton=set
  - Body  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Row  [RectTransform]
      - Sound  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8), SettingToggleButton]  refs: button=set, offIcon=set, onIcon=set
        - sound_on  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
        - sound_off  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
      - Music  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8), SettingToggleButton]  refs: button=set, offIcon=set, onIcon=set
        - Music_on  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
        - Music_off  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
      - Vibration  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8), SettingToggleButton]  refs: button=set, offIcon=set, onIcon=set
        - haptic_on  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
        - haptic_off  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - Quit_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Continue_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Restart_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Title  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - close_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]

## PREFAB Assets/Prefabs/UI/Popup_Win.prefab   (8 object(s))
- Popup_Win  [RectTransform, WinPopup]  refs: continueButton=set, rewardLabel=set, rewardRow=set
  - Body  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
    - NEXT_Button  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1), MISSING SCRIPT (guid:4e29b1a8)]
      - Text (TMP)  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Title  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL
    - Reward  [RectTransform]
      - Coin  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:fe87c0e1)]
      - Amount  [RectTransform, CanvasRenderer, MISSING SCRIPT (guid:f4688fdb)]  refs: parentLinkedComponent=NULL

## SCENE Assets/Scenes/Boot.unity   (4 object(s))
- --UI--  [PopupHost, MISSING SCRIPT (guid:dc42784c), MISSING SCRIPT (guid:0cd44c10), Canvas, RectTransform]  refs: scrim=set, stack=set
  - Scrim  [MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer, RectTransform]
  - EventSystem  [Transform, MISSING SCRIPT (guid:01614664), MISSING SCRIPT (guid:76c392e4)]
- --Systems--  [AudioListener, GameRoot, Transform]  refs: display=set

## SCENE Assets/Scenes/Game.unity   (20 object(s))
- --Board--  [Transform, BoardView, BoardMoveAnimator, BoardInput]  refs: animationConfig=set, animator=set, blockPrefab=set, boardCamera=set, columnRoot=set, coveredColumn=set, glow=set, iceColumn=set, idleBlockRoot=set, input=set, layout=set, normalColumn=set, skins=set, config=set
  - Columns  [Transform]
    - Glow  [SpriteRenderer, Transform]
  - Pool  [Transform]
- --Light--  [Light, Transform, MISSING SCRIPT (guid:474bcb49)]
- --UI--  [RectTransform, MISSING SCRIPT (guid:dc42784c), MISSING SCRIPT (guid:0cd44c10), Canvas]
  - SafeArea  [RectTransform, SafeAreaPanel]
    - Hud  [RectTransform, GameplayHud]  refs: addColumnButton=set, attempt=set, boosterShopPopupPrefab=set, difficultyLabel=set, failPopupPrefab=set, gearButton=set, levelLabel=set, pausePopupPrefab=set, shuffleButton=set, style=set, undoButton=set, winPopupPrefab=set
      - Plaque  [RectTransform, MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer]
        - LevelLabel  [RectTransform, MISSING SCRIPT (guid:f4688fdb), CanvasRenderer]  refs: parentLinkedComponent=NULL
        - DifficultyLabel  [RectTransform, MISSING SCRIPT (guid:f4688fdb), CanvasRenderer]  refs: parentLinkedComponent=NULL
      - Gear  [RectTransform, MISSING SCRIPT (guid:4e29b1a8), MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer]
        - Icon  [RectTransform, MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer]
      - BoosterBar  [RectTransform]
- --Camera--  [MISSING SCRIPT (guid:a79441f3), Camera, Transform]
  - Background  [Transform, SpriteRenderer]
- --Systems--  [AttemptStarter, Transform]  refs: board=set, database=set, progressionConfig=set
- * AddColumn  (prefab instance of Assets/Prefabs/UI/BoosterButton.prefab)
- * Shuffle  (prefab instance of Assets/Prefabs/UI/BoosterButton.prefab)
- * Undo  (prefab instance of Assets/Prefabs/UI/BoosterButton.prefab)

## SCENE Assets/Scenes/Menu.unity   (7 object(s))
- --Camera--  [MISSING SCRIPT (guid:a79441f3), Camera, Transform]
- --UI--  [RectTransform, MISSING SCRIPT (guid:dc42784c), MISSING SCRIPT (guid:0cd44c10), Canvas]
  - Background  [RectTransform, MISSING SCRIPT (guid:86710e43), MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer]
  - SafeArea  [RectTransform, SafeAreaPanel]
    - Menu  [RectTransform, MainMenu]  refs: database=set, playButton=set, playLabel=set, style=set
      - Play  [RectTransform, MISSING SCRIPT (guid:4e29b1a8), MISSING SCRIPT (guid:fe87c0e1), CanvasRenderer]
        - Label  [RectTransform, MISSING SCRIPT (guid:f4688fdb), CanvasRenderer]  refs: parentLinkedComponent=NULL

## Script index — component name -> codemap path

- AttemptStarter | Assets/Scripts/Meta/AttemptStarter.cs
- BlockView | Assets/Scripts/Gameplay/BoardView/BlockView.cs
- BoardInput | Assets/Scripts/Gameplay/BoardView/BoardInput.cs
- BoardMoveAnimator | Assets/Scripts/Gameplay/BoardView/BoardMoveAnimator.cs
- BoardView | Assets/Scripts/Gameplay/BoardView/BoardView.cs
- BoosterButton | Assets/Scripts/UI/BoosterButton.cs
- BoosterShopPopup | Assets/Scripts/UI/BoosterShopPopup.cs
- CoinHud | Assets/Scripts/UI/CoinHud.cs
- ColumnView | Assets/Scripts/Gameplay/BoardView/ColumnView.cs
- FailPopup | Assets/Scripts/UI/FailPopup.cs
- GameRoot | Assets/Scripts/Core/GameRoot.cs
- GameplayHud | Assets/Scripts/UI/GameplayHud.cs
- MainMenu | Assets/Scripts/UI/MainMenu.cs
- PausePopup | Assets/Scripts/UI/PausePopup.cs
- PopupHost | Assets/Scripts/UI/PopupHost.cs
- SafeAreaPanel | Assets/Scripts/UI/SafeAreaPanel.cs
- SettingToggleButton | Assets/Scripts/UI/SettingToggleButton.cs
- WinPopup | Assets/Scripts/UI/WinPopup.cs

## Preserved notes
>> note: 2026-08-18 — the editor export reports 0 missing scripts for these three scenes. If a later stamp says `generator:python-fallback status: DEGRADED n missing-script`, the components it cannot resolve are `UniversalAdditionalCameraData` on `--Camera--` (a URP *package* script, referenced by guid): the fallback reads YAML from outside Unity and can only resolve local guids. Re-run Tools > unity-dev > Export unitymap to get the truth back; do not go looking for a missing script.
>> note: 2026-08-18 — `UniversalAdditionalCameraData.m_VolumeTrigger = NULL` on both screen cameras is URP's own default, not an unwired slot of ours: URP falls back to the camera's transform when it is empty.
>> note: 2026-08-18 — the four `cover*` slots on `Column` and `Column_Ice` are empty by design: only `Column_Covered` carries cover art, and `ColumnView` reads those fields solely for a Covered column. They show up as UNASSIGNED because one component serves all three variants; a covered-only subclass would silence it, which is a part-2 call, not a wiring gap.
