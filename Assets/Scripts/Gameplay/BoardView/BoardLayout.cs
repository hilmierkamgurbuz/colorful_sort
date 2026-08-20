using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// How big one column is, read off its own sprite instead of typed in. The art pack
    /// fixes 1 logical cell = 512 px = 1 Unity unit, and the sprite's 9-slice border is
    /// what says where its cells begin: the tiled middle band is exactly one cell tall,
    /// so everything else follows from three numbers the sprite already carries.
    /// <para>
    /// That is the whole point of computing it: a normal column and an ice column have
    /// different skirts (the ice one hangs icicles below its base), and neither number
    /// belongs in code where it would drift away from the art.
    /// </para>
    /// </summary>
    public readonly struct ColumnMetrics
    {
        public ColumnMetrics(float width, float skirt, float crown, int cellsInSprite)
        {
            if (width <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A column has a positive width.");
            }

            if (skirt < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(skirt), skirt, "A column's skirt is never negative.");
            }

            if (crown < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(crown), crown, "A column's crown is never negative.");
            }

            if (cellsInSprite < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsInSprite), cellsInSprite, "A cell count is never negative.");
            }

            Width = width;
            Skirt = skirt;
            Crown = crown;
            CellsInSprite = cellsInSprite;
        }

        /// <summary>The column's width in units.</summary>
        public float Width { get; }

        /// <summary>How far the first cell's floor sits above the sprite's bottom edge.</summary>
        public float Skirt { get; }

        /// <summary>How much sprite sits above the last cell — the column's crown.</summary>
        public float Crown { get; }

        /// <summary>
        /// How many cells the sprite's middle band already draws — 2 for the generated pack.
        /// Zero means the band is not a whole number of cells, which is the hand-drawn tray: it
        /// carries no per-cell detail, so it is stretched rather than tiled.
        /// </summary>
        public int CellsInSprite { get; }

        /// <summary>
        /// Whether the art tiles in one-cell steps (D-007: that is what makes a column's capacity
        /// visible) or stretches. It is the sprite that decides, not a setting somebody remembers
        /// to match: a band measuring a whole number of cells has per-cell detail to repeat.
        /// </summary>
        public bool TilesPerCell => CellsInSprite >= 1;

        /// <summary>
        /// Reads the metrics out of a column sprite. Fails loudly rather than guessing: a
        /// sprite with no border is one the import pass has not seen, and a silently wrong
        /// cell height is a bug nobody can see in code — only on a phone.
        /// </summary>
        public static ColumnMetrics FromSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }

            float pixelsPerUnit = sprite.pixelsPerUnit;

            if (pixelsPerUnit <= 0f)
            {
                throw new ArgumentException("The column sprite '" + sprite.name + "' has no pixels-per-unit.", nameof(sprite));
            }

            // (left, bottom, right, top), in pixels.
            Vector4 border = sprite.border;

            if (border.y <= 0f || border.w <= 0f)
            {
                throw new ArgumentException(
                    "The column sprite '" + sprite.name + "' carries no 9-slice border, so where its cells start cannot be read. " +
                    "Run Tools > Colorful Sort > Apply Art Import Settings.", nameof(sprite));
            }

            // One rule for every tray, hand-drawn or generated (D-048): the bottom border is the
            // skirt, the top border is the crown, and the band between them is the cells. The
            // pack's art used to fold one cell into its bottom border and this method subtracted
            // it back out, which is a second meaning for the same field — and the new tray, whose
            // skirt is a base plate rather than a drawn cell, cannot be read that way at all.
            float cellPixels = pixelsPerUnit;
            float middlePixels = sprite.rect.height - border.y - border.w;

            if (middlePixels <= 0f)
            {
                throw new ArgumentException(
                    "The column sprite '" + sprite.name + "' has borders that leave no room for a cell between them.",
                    nameof(sprite));
            }

            // Whole number of cells -> the band has per-cell detail and tiles. Anything else is a
            // flat tray that stretches, and saying so here is what keeps the draw mode out of the
            // prefab's hands.
            float cells = middlePixels / cellPixels;
            int wholeCells = Mathf.RoundToInt(cells);
            int cellsInSprite = wholeCells >= 1 && Mathf.Abs(cells - wholeCells) <= 0.02f ? wholeCells : 0;

            return new ColumnMetrics(
                sprite.rect.width / pixelsPerUnit,
                border.y / pixelsPerUnit,
                border.w / pixelsPerUnit,
                cellsInSprite);
        }

        /// <summary>The drawn height of a column of this capacity — the renderer's tiled size.</summary>
        public float Height(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A column holds at least one cell.");
            }

            return Skirt + capacity + Crown;
        }

        /// <summary>
        /// The middle of a cell, measured from the column's bottom edge. Cells are indexed
        /// bottom-up, exactly as the rules index them.
        /// </summary>
        public float CellCentreY(int cellIndex)
        {
            if (cellIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellIndex), cellIndex, "A cell index is never negative.");
            }

            return Skirt + cellIndex + 0.5f;
        }
    }

    /// <summary>
    /// Where the columns go and how much camera it takes to see them. Pure arithmetic on
    /// purpose: it is the one part of the view that can be wrong while looking plausible,
    /// so it is a function of its arguments with no scene, no component and no side effect
    /// — and it carries unit tests, including Level 79's 12×4 shape.
    /// <para>
    /// Runs once per level open, never per frame.
    /// </para>
    /// </summary>
    public static class BoardLayout
    {
        /// <summary>What <see cref="SlotAt"/> returns when the point is off the board.</summary>
        public const int NoSlot = -1;

        /// <summary>
        /// Which slot a point falls in — the inverse of <see cref="SlotBottomCentre"/>, and the
        /// whole hit test. Doing it as arithmetic rather than with colliders keeps sixteen
        /// columns and a hundred bricks collider-free, keeps physics out of a board that has no
        /// physics in it, and makes the tap testable next to the layout it inverts.
        /// <para>
        /// Each slot owns its column plus half of the gap around it, so a row partitions
        /// exactly: a tap inside a row's span always hits one column and there are no dead
        /// lanes between them. Off the board, on an empty row, or in a grid cell nothing was
        /// placed in, it returns <see cref="NoSlot"/> — an authored hole is a hole, not the
        /// nearest column.
        /// </para>
        /// </summary>
        public static int SlotAt(
            Vector2 localPoint,
            IReadOnlyList<int> cells,
            int gridColumns,
            float columnWidth,
            float columnHeight,
            float columnGap,
            float rowGap)
        {
            RequirePlacement(cells, gridColumns);
            RequireSize(columnWidth, columnHeight);
            RequireGaps(columnGap, rowGap);

            Vector2 size = BoardSize(cells, gridColumns, columnWidth, columnHeight, columnGap, rowGap);

            // Half a gap of slack on every side, which is what makes the pitch tile the board.
            float halfHeight = size.y * 0.5f + rowGap * 0.5f;

            if (Mathf.Abs(localPoint.y) > halfHeight)
            {
                return NoSlot;
            }

            int firstRow;
            int lastRow;
            RowRange(cells, gridColumns, out firstRow, out lastRow);

            int rowIndex = Mathf.Clamp(
                Mathf.FloorToInt((halfHeight - localPoint.y) / (columnHeight + rowGap)), 0, lastRow - firstRow);

            int row = firstRow + rowIndex;
            int first;
            int last;

            if (!RowSpan(cells, gridColumns, row, out first, out last))
            {
                return NoSlot;
            }

            // Round-half-up rather than Mathf.RoundToInt, which rounds a boundary to even and
            // would hand two neighbouring columns the same edge.
            float pitch = columnWidth + columnGap;
            int column = Mathf.FloorToInt(localPoint.x / pitch + RowCentre(first, last) + 0.5f);

            if (column < first || column > last)
            {
                return NoSlot;
            }

            return SlotInCell(cells, GridToCell(row, column, gridColumns));
        }

        /// <summary>
        /// Grid cells are numbered the way a level authors them: row by row, left to right,
        /// row 0 at the top of the screen. A cell is a place on the grid; whether a column
        /// stands in it is the level's business.
        /// </summary>
        public static void CellToGrid(int cell, int gridColumns, out int row, out int column)
        {
            if (gridColumns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridColumns), gridColumns, "A grid has at least one column.");
            }

            if (cell < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "A grid cell index is never negative.");
            }

            row = cell / gridColumns;
            column = cell % gridColumns;
        }

        /// <summary>The cell index of a grid position — the inverse of <see cref="CellToGrid"/>.</summary>
        public static int GridToCell(int row, int column, int gridColumns)
        {
            if (gridColumns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridColumns), gridColumns, "A grid has at least one column.");
            }

            if (row < 0 || column < 0 || column >= gridColumns)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(column), row + "," + column, "A grid position is inside a grid " + gridColumns + " columns wide.");
            }

            return row * gridColumns + column;
        }

        /// <summary>
        /// The board's footprint in units, gaps included — measured over the cells columns
        /// were actually placed in, not over the declared grid. A grid with room to spare is
        /// an authoring convenience; framing a screenful of empty cells is not (D-026).
        /// </summary>
        public static Vector2 BoardSize(
            IReadOnlyList<int> cells,
            int gridColumns,
            float columnWidth,
            float columnHeight,
            float columnGap,
            float rowGap)
        {
            RequirePlacement(cells, gridColumns);
            RequireSize(columnWidth, columnHeight);
            RequireGaps(columnGap, rowGap);

            int firstRow;
            int lastRow;
            RowRange(cells, gridColumns, out firstRow, out lastRow);

            float width = 0f;

            for (int row = firstRow; row <= lastRow; row++)
            {
                int first;
                int last;

                if (!RowSpan(cells, gridColumns, row, out first, out last))
                {
                    continue;
                }

                int spanned = last - first + 1;
                width = Mathf.Max(width, spanned * columnWidth + (spanned - 1) * columnGap);
            }

            int rows = lastRow - firstRow + 1;

            return new Vector2(width, rows * columnHeight + (rows - 1) * rowGap);
        }

        /// <summary>
        /// The bottom-centre of a slot, in board-local units, with the whole board centred
        /// on the origin. Bottom-centre because that is the column sprite's pivot: a column
        /// grows upward from its base, so its base is the thing worth positioning.
        /// <para>
        /// Every row is centred on its own occupied span, so three columns over four read as
        /// three centred over four rather than as three pushed left (D-034). A cell left
        /// empty inside a row keeps its width, because that hole was authored on purpose.
        /// One row pitch is used for every row — the tallest column on the board — so a
        /// level that mixes capacities still lands on a straight grid.
        /// </para>
        /// </summary>
        public static Vector2 SlotBottomCentre(
            int slot,
            IReadOnlyList<int> cells,
            int gridColumns,
            float columnWidth,
            float columnHeight,
            float columnGap,
            float rowGap)
        {
            RequirePlacement(cells, gridColumns);
            RequireSize(columnWidth, columnHeight);
            RequireGaps(columnGap, rowGap);

            if (slot < 0 || slot >= cells.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slot), slot, "Slot " + slot + " is outside a board of " + cells.Count + " columns.");
            }

            int row;
            int column;
            CellToGrid(cells[slot], gridColumns, out row, out column);

            int firstRow;
            int lastRow;
            RowRange(cells, gridColumns, out firstRow, out lastRow);

            int first;
            int last;
            RowSpan(cells, gridColumns, row, out first, out last);

            Vector2 size = BoardSize(cells, gridColumns, columnWidth, columnHeight, columnGap, rowGap);

            int rowIndex = row - firstRow;
            float x = (column - RowCentre(first, last)) * (columnWidth + columnGap);
            float y = size.y * 0.5f - (rowIndex + 1) * columnHeight - rowIndex * rowGap;

            return new Vector2(x, y);
        }

        /// <summary>Which slot stands in a grid cell, or <see cref="NoSlot"/> if the cell is empty.</summary>
        public static int SlotInCell(IReadOnlyList<int> cells, int cell)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            // ≤16 columns, at level-open and per-tap frequency: a linear scan against a
            // dictionary's hashing and allocation is the readable side of the cost model.
            for (int slot = 0; slot < cells.Count; slot++)
            {
                if (cells[slot] == cell)
                {
                    return slot;
                }
            }

            return NoSlot;
        }

        /// <summary>The first and last rows any column was placed in.</summary>
        private static void RowRange(IReadOnlyList<int> cells, int gridColumns, out int firstRow, out int lastRow)
        {
            firstRow = int.MaxValue;
            lastRow = int.MinValue;

            for (int slot = 0; slot < cells.Count; slot++)
            {
                int row = cells[slot] / gridColumns;
                firstRow = Mathf.Min(firstRow, row);
                lastRow = Mathf.Max(lastRow, row);
            }
        }

        /// <summary>
        /// The leftmost and rightmost grid columns occupied in one row, or false when the row
        /// holds nothing — which is a row of vertical space the level asked for.
        /// </summary>
        private static bool RowSpan(IReadOnlyList<int> cells, int gridColumns, int row, out int first, out int last)
        {
            first = int.MaxValue;
            last = int.MinValue;

            for (int slot = 0; slot < cells.Count; slot++)
            {
                if (cells[slot] / gridColumns != row)
                {
                    continue;
                }

                int column = cells[slot] % gridColumns;
                first = Mathf.Min(first, column);
                last = Mathf.Max(last, column);
            }

            return first <= last;
        }

        /// <summary>The grid column a row is centred on; a half value when the row is even-wide.</summary>
        private static float RowCentre(int first, int last)
        {
            return (first + last) * 0.5f;
        }

        /// <summary>
        /// Where <paramref name="columnCount"/> columns stand, given what the level authored.
        /// Every placement is derived from the authored one rather than edited in place, which is
        /// what lets the board *shrink*: the add-column booster appends a column and its undo
        /// removes it again, and a grid that only ever grew kept the freed cell — and its extra
        /// row — reserved for a column that no longer exists.
        /// <para>
        /// A column the level did not author takes the first free cell, and when the grid has
        /// none it gains a row (D-034). More columns than the authored grid can hold is therefore
        /// legal; fewer simply drops the tail, because the only column that ever disappears is
        /// the appended one.
        /// </para>
        /// </summary>
        /// <param name="rows">
        /// How many rows the returned placement needs — the authored count, or more when a column
        /// had to start a new row.
        /// </param>
        public static int[] PlaceColumns(
            IReadOnlyList<int> authored,
            int columnCount,
            int gridColumns,
            int authoredRows,
            int maxPerRow,
            out int rows)
        {
            RequirePlacement(authored, gridColumns);

            if (columnCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "A board has at least one column.");
            }

            if (authoredRows < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(authoredRows), authoredRows, "A layout has at least one row.");
            }

            for (int slot = 0; slot < authored.Count; slot++)
            {
                if (authored[slot] < 0 || authored[slot] >= authoredRows * gridColumns)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(authored), authored[slot],
                        "Authored column " + slot + " stands outside the " + authoredRows + "×" + gridColumns + " layout.");
                }
            }

            rows = authoredRows;
            var placement = new int[columnCount];
            int carried = Mathf.Min(columnCount, authored.Count);

            for (int slot = 0; slot < carried; slot++)
            {
                placement[slot] = authored[slot];
            }

            // Whose turn it is. A local rather than anything remembered between calls: the placement
            // is rebuilt from the authored one on every open and every resync, so the same column
            // count has to produce the same board — which is what undo and the boosters lean on
            // (D-057's shape). Starting at the top row every time is what makes that true.
            int turn = 0;

            for (int slot = carried; slot < columnCount; slot++)
            {
                placement[slot] = NextCell(placement, slot, gridColumns, maxPerRow, ref rows, ref turn);
            }

            return placement;
        }

        /// <summary>
        /// Where the next added column goes: the rows take it **in turn**, from the top down, and the
        /// board gains a row below when every one of them is <paramref name="maxPerRow"/> wide
        /// (D-084).
        /// <para>
        /// One to the top row, one to the row under it, round again. A row already at the limit is
        /// stepped over and the turn passes on — it is not owed one later, which is the whole of the
        /// difference between this and counting. So a board whose lower row is full gives the top row
        /// two additions in a row, and a board with a single row simply fills it and then grows a
        /// second, because there is nothing to alternate with.
        /// </para>
        /// <para>
        /// It replaces choosing the *emptiest* row (D-070). That produced the same alternation on an
        /// even board and something else entirely on an uneven one: it would pour columns into
        /// whichever row was behind, so an addition could land at the bottom while the top row still
        /// had room. Taking turns says the same thing about a level board and the right thing about a
        /// lopsided one.
        /// </para>
        /// <para>
        /// Each row is centred on its own occupied span (D-034), so a row that gains a column
        /// re-centres itself and the board stays symmetric without this function knowing anything
        /// about symmetry.
        /// </para>
        /// <para>
        /// The limit governs *added* columns only. A level authored wider than it keeps its shape,
        /// because an authored placement is never re-placed — so on a level authored six wide, both
        /// rows are already past a limit of five and the first added column starts a third row.
        /// </para>
        /// </summary>
        private static int NextCell(
            int[] placement,
            int filled,
            int gridColumns,
            int maxPerRow,
            ref int rows,
            ref int turn)
        {
            // One lap of the rows, beginning wherever the last addition left off.
            for (int step = 0; step < rows; step++)
            {
                int row = (turn + step) % rows;
                int occupied;
                int firstFree = FirstFreeCell(placement, filled, row, gridColumns, out occupied);

                if (firstFree < 0 || occupied >= maxPerRow)
                {
                    continue;
                }

                turn = (row + 1) % rows;
                return firstFree;
            }

            // A full lap and nowhere to stand: the board grows downward, and the turn starts again
            // from the top — every row above the new one is full, so where it points cannot matter
            // until one of them empties, and starting at the top is the rule stated once rather than
            // twice.
            rows++;
            turn = 0;
            return (rows - 1) * gridColumns;
        }

        /// <summary>
        /// The leftmost cell of <paramref name="row"/> that no column stands in, and how many do —
        /// the two things a row is judged on, read in one pass so they cannot disagree.
        /// </summary>
        private static int FirstFreeCell(int[] placement, int filled, int row, int gridColumns, out int occupied)
        {
            occupied = 0;
            int free = -1;

            for (int column = 0; column < gridColumns; column++)
            {
                int cell = row * gridColumns + column;
                bool taken = false;

                for (int slot = 0; slot < filled; slot++)
                {
                    if (placement[slot] == cell)
                    {
                        taken = true;
                        break;
                    }
                }

                if (taken)
                {
                    occupied++;
                }
                else if (free < 0)
                {
                    free = cell;
                }
            }

            return free;
        }

        /// <summary>
        /// The orthographic half-height that shows the whole board plus its padding, on
        /// whatever viewport it is given. Portrait phones are width-bound for a wide board
        /// and height-bound for a tall one, so both are computed and the larger wins.
        /// </summary>
        public static float OrthographicSize(Vector2 boardSize, float viewportAspect, float padding)
        {
            return OrthographicSize(boardSize, viewportAspect, padding, 0f, 0f);
        }

        /// <summary>
        /// The same framing on a screen the board does not own all of. The HUD keeps a band at
        /// the top and the booster bar one at the bottom, given as shares of viewport height;
        /// the board is fitted into what is left, so a camera sized by the full viewport — which
        /// is what the three-argument overload does — is exactly how the board ends up sliding
        /// under the plaque.
        /// <para>
        /// Only the height is affected. A reserved band takes no width, so the width-bound case
        /// is unchanged and a wide board still zooms out the same amount.
        /// </para>
        /// </summary>
        public static float OrthographicSize(
            Vector2 boardSize,
            float viewportAspect,
            float padding,
            float topReserve,
            float bottomReserve)
        {
            return OrthographicSize(boardSize, viewportAspect, padding, topReserve, bottomReserve, 0f);
        }

        /// <summary>
        /// The same framing under a camera tilted <paramref name="tiltDegrees"/> down towards the
        /// board. The board stays upright — only the camera leans — so what the board projects onto
        /// the screen's vertical axis shortens by the cosine of the tilt, and the camera zooms *in*
        /// rather than out. A tilt about the camera's X axis takes no width, so the width-bound case
        /// is untouched.
        /// <para>
        /// The bricks' own depth adds a little of that height back — at 25° a one-cell-deep brick
        /// grows the silhouette by about 0.4 units — and that is what `padding` absorbs. Modelling
        /// the depth here would mean this function had to be told how thick a board is, which is a
        /// fact about the art, not about the layout.
        /// </para>
        /// </summary>
        public static float OrthographicSize(
            Vector2 boardSize,
            float viewportAspect,
            float padding,
            float topReserve,
            float bottomReserve,
            float tiltDegrees)
        {
            if (boardSize.x <= 0f || boardSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(boardSize), boardSize, "A board has a positive size.");
            }

            if (viewportAspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewportAspect), viewportAspect, "A viewport's width/height ratio is positive.");
            }

            if (padding < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(padding), padding, "Padding is never negative.");
            }

            float usableHeight = UsableHeightShare(topReserve, bottomReserve);
            float lean = TiltCosine(tiltDegrees);

            // The board has to fit the *band*, so the camera sees more than the band by exactly
            // the share the bands take. Dividing here is what turns "the board fits the screen"
            // into "the board fits what the HUD left of it".
            float halfHeight = (boardSize.y * lean * 0.5f + padding) / usableHeight;
            float halfWidth = boardSize.x * 0.5f + padding;

            return Mathf.Max(halfHeight, halfWidth / viewportAspect);
        }

        /// <summary>
        /// How far the camera sits above the board's centre so the board lands in the middle of
        /// the band the reserves leave. Positive means the camera looks higher than the board,
        /// which draws the board lower on screen — what a top-heavy HUD asks for.
        /// <para>
        /// Symmetric reserves return zero: two equal bands leave the band's centre exactly where
        /// the screen's centre already is.
        /// </para>
        /// </summary>
        public static float CameraCentreOffset(float orthographicSize, float topReserve, float bottomReserve)
        {
            if (orthographicSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orthographicSize), orthographicSize, "A camera's orthographic size is positive.");
            }

            UsableHeightShare(topReserve, bottomReserve);

            // The band's centre, measured from the viewport's centre in shares of half-height.
            return (topReserve - bottomReserve) * orthographicSize;
        }

        /// <summary>
        /// The cosine of a legal tilt. At 90° the board would project to a line and the framing
        /// would divide by zero, so the angle is refused rather than clamped: a camera looking
        /// along the board's own plane is a scene mistake, and quietly framing something is how it
        /// survives to a build.
        /// </summary>
        private static float TiltCosine(float tiltDegrees)
        {
            float tilt = Mathf.Abs(tiltDegrees);

            if (tilt >= 90f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tiltDegrees), tiltDegrees, "A camera tilted 90° or more sees the board edge-on.");
            }

            return Mathf.Cos(tilt * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The share of the viewport's height the board may use, refusing the case where the two
        /// bands leave nothing: a zero share would hand the camera an infinite size, and a
        /// negative one would frame the board inside out — both of which draw *something*, which
        /// is worse than a stopped build.
        /// </summary>
        private static float UsableHeightShare(float topReserve, float bottomReserve)
        {
            if (topReserve < 0f || bottomReserve < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(topReserve), topReserve + ", " + bottomReserve, "A reserved band is never negative.");
            }

            float usable = 1f - topReserve - bottomReserve;

            if (usable <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(topReserve), topReserve + ", " + bottomReserve,
                    "The reserved bands leave the board no height at all.");
            }

            return usable;
        }

        /// <summary>
        /// A placement is one grid cell per column, in slot order. Duplicates are not checked
        /// here: <c>LevelDefinition.Validate()</c> refuses them at authoring time, which is
        /// where a level with two columns in one cell has to die (D-033).
        /// </summary>
        private static void RequirePlacement(IReadOnlyList<int> cells, int gridColumns)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count < 1)
            {
                throw new ArgumentException("A board has at least one column.", nameof(cells));
            }

            if (gridColumns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridColumns), gridColumns, "A grid has at least one column.");
            }

            for (int slot = 0; slot < cells.Count; slot++)
            {
                if (cells[slot] < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cells), cells[slot], "Slot " + slot + " has no grid cell to stand in.");
                }
            }
        }

        private static void RequireSize(float columnWidth, float columnHeight)
        {
            if (columnWidth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(columnWidth), columnWidth, "A column has a positive width.");
            }

            if (columnHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(columnHeight), columnHeight, "A column has a positive height.");
            }
        }

        private static void RequireGaps(float columnGap, float rowGap)
        {
            if (columnGap < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(columnGap), columnGap, "A gap is never negative.");
            }

            if (rowGap < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rowGap), rowGap, "A gap is never negative.");
            }
        }
    }
}
