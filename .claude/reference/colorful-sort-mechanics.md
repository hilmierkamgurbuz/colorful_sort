# reference — Colorful Sort mechanics (distilled)

<!-- DISTILLATION FILE. The reference game was read once (4 in-game
     screenshots supplied by the user + store research). From here on, every
     mechanic decision quotes THIS file, not the screenshots and not the web.
     A line marked `OPEN:` is not yet known — it is an OPEN QUESTION for the
     user, never a guess.

     Sources read once:
     - user screenshots: main menu, gameplay Level 79 "Super Hard",
       pause popup, settings popup
     - Google Play: "Colorful Sort - Block Puzzle" (com.block.color.sort)
     - genre confirmations: Block Sort / Water Sort family listings
     - the art pack's own contract: ArtSource/colorful-sort-pack/README.md +
       Unity_Import_Guide.md + AssetManifest.json
-->

## 1. Family and core rule

Block-sort (water-sort family), rendered as LEGO-style bricks in vertical
columns. Blocks stack bottom-up in a column of fixed cell capacity.

- A **block** carries a logical colour id. Its visual is a colour **and** an
  embossed symbol; symbol and colour are bound 1:1, so the symbol is a
  colour-blind aid, not a second dimension. Reference pairs seen in the
  screenshot: paw=brown, crown=yellow, cat=orange, heart=red, drop=blue,
  star=green, dino=dark-green, flower=pink.
- **Move**: tap a source column → the contiguous same-colour **run** on top
  lifts. Tap a destination → the run lands if the destination is empty or its
  top block has the same colour, and only as many blocks as still fit. Tapping
  the lifted source again cancels the selection.
- **Win**: every colour ends up gathered in one column. A column that holds a
  single colour and nothing else is "solved".
- **Lose**: no legal move remains (deadlock). There is **no move counter** and
  no timer in the reference HUD.

## 2. Column kinds (seen in Level 79)

Level 79 shows 12 columns laid out 2 rows × 6, capacity **4** cells each.
Column capacity is therefore per-level data, not a constant.

| Kind | What is on screen | Rule |
|---|---|---|
| Normal | plain dark-purple slot | no modifier |
| **Ice** | slot encased in ice, empty, icicles hanging below | unusable until thawed. **Thaws when a colour is completed** — one ice column per completion, so a column carries "thaw after the Nth completed colour" (the shot's three ice columns are N=1,2,3). |
| **Covered** | beige cover over the cells; a colour stripe up the left edge and that colour's symbol on the bottom cell | contents hidden. The symbol on the cover is a **key**: completing a column of that colour opens **every** cover carrying it. Complete the 4-block cat column → all cat-keyed covers open at once. |
| **Mystery** | top block visible, cells below are black `?` bricks | a `?` reveals its colour when it becomes the column's top block. Its contents are **authored and fixed the moment the level opens** — the player simply cannot see them (D-011, user-confirmed). Replay variety comes from §8, not from the reveal. |

Both Ice and Covered therefore hang off the same event — "a colour was just
completed" — but read it differently: Ice counts completions, Covered matches
the completed colour against its key. `Board` raises that one event; the two
modifiers subscribe to it.

The art pack backs all four: `slot_*` (normal), `slot_ice_*` + `ice_*`,
`cover_*` (+ "add the side stripe and symbol as your own decal layer"),
`mystery_face_overlay` + `question_mark_decal`.

## 3. Boosters (gameplay bottom bar, left → right)

| Booster | Icon | Effect | Badge |
|---|---|---|---|
| Add column | stacked-blocks + green `+` | adds one extra empty column | `+` = buy / rewarded ad |
| Undo | curved arrow | reverts the last move | number = charges left (`1` in the shot) |
| Shuffle | crossed arrows + green `+` | reshuffles blocks | `+` = buy / rewarded ad |

Undo forces the board to be a **replayable** state machine, and Shuffle plus
Mystery reveal force the randomness to be **seeded per level attempt** — else
undo cannot reproduce what it undid. See `fingerprint.md` → Determinism.

## 4. Screens and HUD

**Gameplay HUD** — top-left ads/coin offer button; top-centre level plaque
carrying the level number and a difficulty label (`Normal` / `Hard` /
`Super Hard`, skull icon on Super Hard); top-right gear → Pause popup.
Bottom: the three boosters.

**Pause popup** — Restart · Sound toggle · Vibration toggle | Restore Purchase
| Contact Us | Continue · Quit | player id string.

**Settings popup (menu)** — Profile · Sound · Vibration | Restore Purchase |
Contact Us | Terms of Use · Privacy Policy | player id string.

**Main menu** — mascot avatar with badge; coin counter with `+`; hearts
(`5 Full`); season-pass bar (key `9/10`, chest reward, `14d 13h`); side event
buttons (duck `#47`, car, two `START` races); `Claim` gift; `ADS Offer!`;
centre 3D LEGO diorama that builds as levels are cleared; big level button
with the difficulty label; bottom nav Shop · Leaderboard · Home · Collection ·
Daily.

Difficulty labels are authored per level, not derived.

## 5. What this project changes on purpose

Levels are transcribed 1:1 from the reference, but the **skin** is remapped —
e.g. the cat symbol becomes the moon. That is why level data stores a logical
colour id and never a symbol or an RGB value: one `BlockSkinSet` asset owns
`colourId → (colour, symbol mesh)`, and re-skinning the whole game is editing
that one asset. Available symbol meshes: cat, cloud, crown, dino, drop, fish,
flower, moon, paw, question, rocket, star (`Assets/Art/Models/Blocks/`).

## 6. Art contract that constrains the scene

From the pack's import guide — these are given, not chosen:

- 1 logical cell = 512 px = **1 Unity unit** (`Pixels Per Unit: 512`).
- Reference resolution 1440×2560 portrait; Canvas Scaler reference
  1080×1920, `Match Width Or Height: 0.5`.
- Fixed 2-cell columns use `slot_complete_2cell` / `slot_ice_complete_2cell`
  as a single `SpriteRenderer`, pivot bottom-centre, `Simple`. **Level 79 has
  4 cells**, so this project needs the variable-height path: `Draw Mode:
  Tiled` in exact 512 px steps (border `160,832,160,320` normal /
  `160,1152,160,320` ice), or the legacy modular `slot_top` +
  `slot_cell_repeat` + `slot_bottom` triplet. Never free `Sliced` stretch, and
  never mix the normal and ice families.
- Sorting order: background 0 · column 10 · 3D bricks 20 · cover/ice/mystery
  overlay 30 · screen UI 100+.
- No text is baked into any sprite: level number, coins, hearts and panel
  copy are TextMeshPro. Reference text style: off-white fill `#FFF6D6`, dark
  purple/brown outline, soft drop shadow.
- Palette: purple `#7259FF`, outline `#4237A1`, yellow `#FEC901`, orange
  `#F08A00`, green `#00D44C`, red `#FE3C00`, lavender `#9A99FF`, ice
  `#B9E3F7`/`#92D7F2`, cover beige `#C7A89C`, mystery `#2A2835`.
- Buttons ship `_normal` / `_pressed` / `_disabled` → Unity
  `Selectable / Transition: Sprite Swap`.

## 7. Question log

Answered by the user, 2026-08-17:

- `OPEN-1` **Ice thaw** → a completed colour breaks an ice column. Modelled as
  `thawAfterCompletions` per ice column. *Resolved — see §2.*
- `OPEN-2` **Cover reveal** → the cover's symbol is a key; completing that
  colour opens every cover bearing it. *Resolved — see §2.*
- `OPEN-4` **Level transcription** → a custom Unity `EditorWindow` level
  editor: draw the grid, pick column kind and capacity, fill cells from the
  colour palette, save. Solvability validation hangs off the same window.
  *Resolved — `Editor` shard owns it.*
- `OPEN-5` **Mystery contents / replay variety** → what sits under a `?` is fixed
  the moment the level opens and is simply invisible to the player; nothing is
  drawn at reveal time. Variety on a replay comes from permuting the colours and
  the column slots per attempt instead. *Resolved — see §8, D-011 and D-014.*

Still open:

- `OPEN-3` Fail condition: is a deadlock an instant fail popup that costs a
  heart, or does the player sit there until they restart? Blocks: `Meta`
  hearts spend point. Current assumption (D-008-adjacent): deadlock shows a
  fail popup and costs one heart; restart from the popup costs nothing extra.

## 8. Replay: the attempt scramble

Reported by the user, 2026-08-17. A player replaying a level they have already
solved must not be able to repeat a memorised tap sequence: the colours change
places between attempts. Swapping the cat with the moon — and standing the columns
in different slots — leaves the puzzle identical, because both are relabelings
rather than edits: no block gains or loses a neighbour, so a solvable board stays
solvable and a fair board stays fair.

The variety is deliberately **small and enumerable**: the user's answer is that
around five combinations per level is enough. That is not a compromise on variety,
it is what makes the feature reviewable — a designer can look at all five boards a
player can ever meet, and the level editor can validate each.

What that means for this build (D-014, D-015):

- a level offers a small set of **variants**, and a variant is a pure function of
  (level index, variant index): variant 3 of level 79 is always the same board
- how many variants a level offers is a tuning number in `Data/Config/`, and which
  one an attempt plays is `Meta`'s choice — the rules assembly is told an index and
  nothing else
- each variant reads a draw stream private to its level, so choosing a look never
  spends a draw the Shuffle booster is going to want
- the colour permutation is over the ids the level actually uses, so the
  `BlockSkinSet` never needs an entry the level did not ask for
- a covered column's key colour is mapped by the same permutation as the blocks it
  waits for, or the cover would wait for a colour that no longer exists
- what the level editor validates once (D-010) holds for every seed, and the
  scrambled level is put back through the level validator on every attempt anyway
- `BoardView` positions a column by its index in the **attempt's** level, never by
  anything authored, or the reordering never reaches the screen
