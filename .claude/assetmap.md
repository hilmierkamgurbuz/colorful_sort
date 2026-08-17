<!-- stamp: 2026-08-17T15:00Z source-sig:69eaaad471b4 assets:8 prefabs:0 scenes:1 asmdefs:2 -->
# assetmap — asset inventory (data, prefabs, load surface, assemblies)

Regenerate with `python3 .claude/hooks/build_assetmap.py`. `data-source.md`
reads the ScriptableObject section before deciding where a value lives;
the cost model reads the load-surface section before pricing a load.

## Assemblies (.asmdef) — the real compile boundary

- Assets/Scripts/Gameplay/Board/ColorfulSort.Board.asmdef | name: ColorfulSort.Board | refs: - | platforms: all | covers: Assets/Scripts/Gameplay/Board/**
- Assets/Tests/Board/ColorfulSort.Board.Tests.asmdef | name: ColorfulSort.Board.Tests | refs: ColorfulSort.Board,UnityEngine.TestRunner,UnityEditor.TestRunner | platforms: Editor | covers: Assets/Tests/Board/**

## ScriptableObject assets

- Assets/Readme.asset | type: Readme | script: Assets/TutorialInfo/Scripts/Readme.cs
- Assets/Settings/DefaultVolumeProfile.asset | type: guid:d7fd9488 | script: ?
- Assets/Settings/Mobile_RPAsset.asset | type: guid:bf2edee5 | script: ?
- Assets/Settings/Mobile_Renderer.asset | type: guid:de640fe3 | script: ?
- Assets/Settings/PC_RPAsset.asset | type: guid:bf2edee5 | script: ?
- Assets/Settings/PC_Renderer.asset | type: guid:de640fe3 | script: ?
- Assets/Settings/SampleSceneProfile.asset | type: guid:d7fd9488 | script: ?
- Assets/Settings/UniversalRenderPipelineGlobalSettings.asset | type: guid:2ec995e5 | script: ?

## Prefabs

- (none)

## Scenes

- Assets/Scenes/SampleScene.unity

## Runtime load surface

- (none) — nothing is string-loaded; every asset is a direct reference
