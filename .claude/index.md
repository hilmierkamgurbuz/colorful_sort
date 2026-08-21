<!-- stamp: 2026-08-21 systems:8 unmapped:1 unassigned-files:0 -->
# index — system to location, one screen

Step 1 of `procedures/locate.md`: read this before anything else, and only
descend into blueprint/codemap/unitymap for what this table points at.
Regenerate with `python3 .claude/hooks/build_index.py`; it joins existing
maps and never invents a name.

| system | shard(s) | entry files | scenes | prefabs | data | status |
|---|---|---|---|---|---|---|
| Board | gameplay, tests | Assets/Scripts/Gameplay/Board/BlockColourId.cs, Assets/Scripts/Gameplay/Board/BoardColumn.cs (+19) | Game | - | - | OK |
| BoardView | gameplay, tests | Assets/Scripts/Gameplay/BoardView/BoardLayout.cs, Assets/Scripts/Gameplay/BoardView/BoardMoveAnimator.cs (+14) | Game | Block, Column, Column_Ice, Column_Covered, Column_ice1 | Assets/Data/Config | OK |
| Boosters | - | - | - | - | - | UNMAPPED — blueprint system with no code |
| Content | content, tests | Assets/Scripts/Content/ColorfulSort.Content.asmdef, Assets/Scripts/Content/LevelCodec.cs (+10) | - | - | Assets/Data/Blocks, Assets/Data/Levels | OK |
| Core | core, tests | Assets/Scripts/Core/AttemptSeedSource.cs, Assets/Scripts/Core/ColorfulSort.Core.asmdef (+12) | Boot, Menu, Game | - | Assets/Data/Config | OK |
| Meta | meta, tests | Assets/Scripts/Meta/ColorfulSort.Meta.asmdef, Assets/Scripts/Meta/PlayerEconomy.cs (+7) | - | - | Assets/Data/Config | OK |
| Tooling | editor | Assets/Editor/ArtImportPass.cs, Assets/Editor/BlockSkinFactory.cs (+5) | - | - | - | OK |
| UI | ui | Assets/Scripts/UI/ColorfulSort.UI.asmdef, Assets/Scripts/UI/PopupHost.cs (+12) | Boot, Menu, Game, UI | Popup_Pause, Popup_Settings, Popup_Win, Popup_Fail, Popup_BoosterShop, BoosterButton | Assets/Data/Config | OK |

## Gaps
- 2 flagged codemap line(s) excluded from this table (STALE)
