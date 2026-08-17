---
paths:
  - "Assets/Scripts/UI/**"
---
<!-- Path-scoped rule: loads only when a matching file is read, not on every tool
     use. It IS re-loaded after a compaction (InstructionsLoaded fires with
     load_reason: compact), so the risk is not compaction — it is that a
     decision taken before any matching file is opened never sees this file.
     Such a rule belongs in the preflight too. Verify loading with /memory. -->

# UI domain rules

- UI READS game state; it never writes. Write requests go to the owner as commands.
- UI updates happen via event subscription; state is never polled per frame.
  A coin counter that reads `Meta` in `Update()` is a defect, not a shortcut.
- Popups are prefabs instantiated by a popup host and are stacked, not
  hand-toggled. Only the host decides what is on top and what closes.
  Pause and Settings share their body layout — one prefab per popup, but the
  shared row of round toggle buttons is one reusable component.
- Buttons use the art pack's three states through
  `Selectable / Transition: Sprite Swap`: `_normal`, `_pressed`, `_disabled`.
  A button that tints a single sprite instead is not matching the reference.
- All text is TextMeshPro. No text is baked into a sprite, and no label is
  assembled by concatenating in a per-frame path. Reference style: off-white
  fill `#FFF6D6`, dark purple/brown outline, soft drop shadow.
- Canvas Scaler is `Scale With Screen Size`, reference 1080×1920,
  `Match Width Or Height: 0.5`. Layout anchors to `Screen.safeArea`; nothing is
  positioned by trusting where a background image happens to put it, because
  19.5:9 and 20:9 crop the background.
- Screen UI lives on its own Screen Space - Overlay canvas at sorting order
  100+, above the board's world-space sprites.
- A UI script does not load a scene, spend a currency or mutate a board itself.
  It asks `Core`, `Meta` or `Boosters` to do it and re-renders from the result.
