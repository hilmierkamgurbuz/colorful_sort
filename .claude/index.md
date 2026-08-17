<!-- stamp: 2026-08-17 systems:8 unmapped:5 unassigned-files:0 -->
# index — system to location, one screen

Step 1 of `procedures/locate.md`: read this before anything else, and only
descend into blueprint/codemap/unitymap for what this table points at.
Regenerate with `python3 .claude/hooks/build_index.py`; it joins existing
maps and never invents a name.

| system | shard(s) | entry files | scenes | prefabs | data | status |
|---|---|---|---|---|---|---|
| Board | gameplay, tests | Assets/Scripts/Gameplay/Board/BlockColourId.cs, Assets/Scripts/Gameplay/Board/BoardColumn.cs (+16) | Game | - | - | OK |
| BoardView | - | - | - | Block, Column, Column_Ice, Column_Covered | - | UNMAPPED — blueprint system with no code |
| Boosters | - | - | - | - | - | UNMAPPED — blueprint system with no code |
| Content | content | Assets/Scripts/Content/BlockSkinSet.cs, Assets/Scripts/Content/ColumnDefinition.cs (+4) | SampleScene | - | - | OK |
| Core | - | - | Boot, Menu, Game | - | - | UNMAPPED — blueprint system with no code |
| Meta | - | - | - | - | - | UNMAPPED — blueprint system with no code |
| TemplateLeftovers | core, editor | Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs, Assets/TutorialInfo/Scripts/Readme.cs | SampleScene | - | Assets | OK |
| UI | - | - | Boot, Menu, Game | Popup_Pause, Popup_Settings, Popup_Win, Popup_Fail, BoosterButton | - | UNMAPPED — blueprint system with no code |

## Gaps
- none
