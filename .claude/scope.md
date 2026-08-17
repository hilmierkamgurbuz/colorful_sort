# scope — what the game is

<!-- The AI drafts it, the user approves it. It separates the product shape
     from the technical shape; this file is what prevents scope creep.
     Mechanic detail lives in .claude/reference/colorful-sort-mechanics.md. -->

- **Game:** A 1:1 remake of *Colorful Sort — Block Puzzle*: a block-sort
  (water-sort family) puzzle where LEGO-style bricks stand in vertical columns
  and the player taps a column to lift the same-coloured run off its top and
  drop it onto another column. Each colour carries an embossed symbol so the
  puzzle reads without relying on hue. Levels are transcribed from the
  reference, with one deliberate difference: the symbol set is re-skinned
  (e.g. the reference's cat becomes a moon), which is a data edit, not a code
  change.

- **Core loop:** Open the level → read the columns and plan → tap-tap to move
  runs until every colour is gathered in one column → the level clears, the
  progress bar and the diorama advance, the next level is one tap away. When a
  board deadlocks, a booster (undo / add column / shuffle) buys the mistake
  back, which is what turns a fail into a spend.

- **Win/lose:** Win when every colour occupies a single column and no column
  holds a mixture. Lose on deadlock — no legal move remains. No move counter,
  no timer. (`OPEN-3`: whether deadlock fires an instant fail popup and costs a
  heart is unconfirmed.)

- **Vertical-slice boundary:** one fully representative playable level, at
  release quality:
  - board of N columns × M cells laid out on a grid, driven entirely by level
    data (Level 79's 12 columns × 4 cells is the shape to hit)
  - all four column kinds present: normal, ice-locked, covered, mystery
  - tap-to-lift / tap-to-drop with the real move rule, cancel on re-tap, and
    the reference's brick animation feel (lift, arc, settle)
  - 3D bricks under an orthographic camera over the 2D slot art, at the
    sorting order the art pack specifies
  - win and deadlock both detected and both showing their popup
  - all three boosters functional, including undo across a mystery reveal
  - gameplay HUD (level plaque + difficulty label + gear) and the Pause popup
  - progress saved: current level, coins, hearts, booster charges
  - one re-skin proven: swapping a symbol/colour pair edits one data asset and
    nothing else

- **Release scope:**
  - Android + iOS, portrait, Unity 6000.0.80f1 / URP
  - the transcribed reference level set with per-level difficulty labels
    (`Normal` / `Hard` / `Super Hard`)
  - main menu with coins, hearts, level button and the progress bar
  - Settings and Pause popups exactly as the reference lays them out, plus
    Restore Purchase, Contact Us, Terms of Use, Privacy Policy
  - sound + vibration toggles, persisted
  - boosters purchasable with coins and/or rewarded ads
  - English only (the art bakes no text, so localisation stays cheap later)

- **Out of scope:** (deliberately not built — the art pack excludes the art
  for all of it)
  - the centre 3D LEGO diorama meta and its build-up stages
  - season pass / key-and-chest progression, the timed side events (duck race,
    car race), Claim gift, `ADS Offer!`
  - bottom navigation destinations: Shop, Leaderboard, Collection, Daily
  - ad network and IAP integration (the buttons exist; the SDKs come later)
  - accounts, cloud save, leaderboards, any server
  - procedural level generation — levels are transcribed, not generated
  - landscape orientation, tablet-specific layout
