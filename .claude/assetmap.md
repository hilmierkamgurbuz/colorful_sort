<!-- stamp: 2026-08-21T07:59Z source-sig:f3bb00e35580 assets:55 prefabs:10 scenes:4 asmdefs:11 -->
# assetmap — asset inventory (data, prefabs, load surface, assemblies)

Regenerate with `python3 .claude/hooks/build_assetmap.py`. `data-source.md`
reads the ScriptableObject section before deciding where a value lives;
the cost model reads the load-surface section before pricing a load.

## Assemblies (.asmdef) — the real compile boundary

- Assets/Scripts/Content/ColorfulSort.Content.asmdef | name: ColorfulSort.Content | refs: ColorfulSort.Board | platforms: all | covers: Assets/Scripts/Content/**
- Assets/Scripts/Core/ColorfulSort.Core.asmdef | name: ColorfulSort.Core | refs: - | platforms: all | covers: Assets/Scripts/Core/**
- Assets/Scripts/Gameplay/Board/ColorfulSort.Board.asmdef | name: ColorfulSort.Board | refs: - | platforms: all | covers: Assets/Scripts/Gameplay/Board/**
- Assets/Scripts/Gameplay/BoardView/ColorfulSort.BoardView.asmdef | name: ColorfulSort.BoardView | refs: ColorfulSort.Board,ColorfulSort.Content,Unity.InputSystem,UnityEngine.UI | platforms: all | covers: Assets/Scripts/Gameplay/BoardView/**
- Assets/Scripts/Meta/ColorfulSort.Meta.asmdef | name: ColorfulSort.Meta | refs: ColorfulSort.Board,ColorfulSort.Content,ColorfulSort.Core,ColorfulSort.BoardView | platforms: all | covers: Assets/Scripts/Meta/**
- Assets/Scripts/UI/ColorfulSort.UI.asmdef | name: ColorfulSort.UI | refs: ColorfulSort.Content,ColorfulSort.Core,ColorfulSort.Meta,UnityEngine.UI,Unity.TextMeshPro | platforms: all | covers: Assets/Scripts/UI/**
- Assets/Tests/Board/ColorfulSort.Board.Tests.asmdef | name: ColorfulSort.Board.Tests | refs: ColorfulSort.Board,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/Board/**
- Assets/Tests/BoardView/ColorfulSort.BoardView.Tests.asmdef | name: ColorfulSort.BoardView.Tests | refs: ColorfulSort.BoardView,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/BoardView/**
- Assets/Tests/Content/ColorfulSort.Content.Tests.asmdef | name: ColorfulSort.Content.Tests | refs: ColorfulSort.Content,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/Content/**
- Assets/Tests/Core/ColorfulSort.Core.Tests.asmdef | name: ColorfulSort.Core.Tests | refs: ColorfulSort.Core,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/Core/**
- Assets/Tests/Meta/ColorfulSort.Meta.Tests.asmdef | name: ColorfulSort.Meta.Tests | refs: ColorfulSort.Meta,ColorfulSort.Core,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/Meta/**

## ScriptableObject assets

- Assets/Art/Models/Blocks/Symbols/Symbol_Cat.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Cloud.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Crown.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Dino.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Drop.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Fish.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Flower.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Moon.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Paw.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Question.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Rocket.asset | type: - | script: -
- Assets/Art/Models/Blocks/Symbols/Symbol_Star.asset | type: - | script: -
- Assets/Data/Blocks/BlockSkinSet.asset | type: BlockSkinSet | script: Assets/Scripts/Content/BlockSkinSet.cs
- Assets/Data/Blocks/Skin_Base.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Cat.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Cloud.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Crown.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Dino.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Drop.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Fish.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Flower.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Moon.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Paw.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Question.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Rocket.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Blocks/Skin_Star.asset | type: BlockSkin | script: Assets/Scripts/Content/BlockSkin.cs
- Assets/Data/Config/BoardAnimationConfig.asset | type: BoardAnimationConfig | script: Assets/Scripts/Gameplay/BoardView/BoardAnimationConfig.cs
- Assets/Data/Config/BoardLayoutConfig.asset | type: BoardLayoutConfig | script: Assets/Scripts/Gameplay/BoardView/BoardLayoutConfig.cs
- Assets/Data/Config/DisplayConfig.asset | type: DisplayConfig | script: Assets/Scripts/Core/DisplayConfig.cs
- Assets/Data/Config/ProgressionConfig.asset | type: ProgressionConfig | script: Assets/Scripts/Meta/ProgressionConfig.cs
- Assets/Data/Config/UiStyleConfig.asset | type: UiStyleConfig | script: Assets/Scripts/UI/UiStyleConfig.cs
- Assets/Data/Levels/LevelDatabase.asset | type: LevelDatabase | script: Assets/Scripts/Content/LevelDatabase.cs
- Assets/Fonts/Libre_Baskerville 22.32.13/LibreBaskerville-Italic-VariableFont_wght SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/LibreBaskerville-VariableFont_wght SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-Bold SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-BoldItalic SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-Italic SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-Medium SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-MediumItalic SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-Regular SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-SemiBold SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Libre_Baskerville 22.32.13/static/LibreBaskerville-SemiBoldItalic SDF.asset | type: guid:71c1514a | script: ?
- Assets/Fonts/Titan_One/TitanOne-Regular SDF.asset | type: guid:71c1514a | script: ?
- Assets/Settings/DefaultVolumeProfile.asset | type: guid:d7fd9488 | script: ?
- Assets/Settings/Mobile_RPAsset.asset | type: guid:bf2edee5 | script: ?
- Assets/Settings/Mobile_Renderer.asset | type: guid:de640fe3 | script: ?
- Assets/Settings/PC_RPAsset.asset | type: guid:bf2edee5 | script: ?
- Assets/Settings/PC_Renderer.asset | type: guid:de640fe3 | script: ?
- Assets/Settings/SampleSceneProfile.asset | type: guid:d7fd9488 | script: ?
- Assets/Settings/UniversalRenderPipelineGlobalSettings.asset | type: guid:2ec995e5 | script: ?
- Assets/TextMesh Pro/Resources/TMP Settings.asset | type: guid:2705215a | script: ?
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset | type: guid:71c1514a | script: ?
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset | type: guid:71c1514a | script: ?
- Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset | type: guid:84a92b25 | script: ?
- Assets/TextMesh Pro/Resources/Style Sheets/Default Style Sheet.asset | type: guid:ab2114bd | script: ?

## Prefabs

- Assets/Prefabs/BoardView/Block.prefab | variant-of: - | scripts: BlockView
- Assets/Prefabs/BoardView/Column.prefab | variant-of: - | scripts: ColumnView
- Assets/Prefabs/BoardView/Column_Covered.prefab | variant-of: Assets/Prefabs/BoardView/Column.prefab | scripts: -
- Assets/Prefabs/BoardView/Column_Ice.prefab | variant-of: Assets/Prefabs/BoardView/Column.prefab | scripts: -
- Assets/Prefabs/BoardView/Column_ice1.prefab | variant-of: - | scripts: ColumnView
- Assets/Prefabs/UI/BoosterButton.prefab | variant-of: - | scripts: BoosterButton
- Assets/Prefabs/UI/Popup_BoosterShop.prefab | variant-of: - | scripts: BoosterShopPopup, CoinHud
- Assets/Prefabs/UI/Popup_Fail.prefab | variant-of: - | scripts: FailPopup
- Assets/Prefabs/UI/Popup_Pause.prefab | variant-of: - | scripts: SettingToggleButton, PausePopup
- Assets/Prefabs/UI/Popup_Win.prefab | variant-of: - | scripts: WinPopup

## Scenes

- Assets/Editor/PrefabEnvironments/UI.unity
- Assets/Scenes/Boot.unity
- Assets/Scenes/Game.unity
- Assets/Scenes/Menu.unity

## Runtime load surface

- Assets/TextMesh Pro/Resources | 21 file(s) | loaded by string at runtime; ships in every build
