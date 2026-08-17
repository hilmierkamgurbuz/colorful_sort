using System;
using System.Collections.Generic;

namespace ColorfulSort.Board
{
    /// <summary>
    /// One authored cell of a column: which colour stands there, and whether the
    /// player can see it yet. Empty cells are not expressed here — a column that
    /// is not full simply carries fewer cells.
    /// </summary>
    public readonly struct CellData
    {
        public readonly BlockColourId Colour;

        /// <summary>
        /// True for a cell the player cannot read: every cell of a Covered column,
        /// and every cell below the top of a Mystery column.
        /// </summary>
        public readonly bool Hidden;

        public CellData(BlockColourId colour, bool hidden = false)
        {
            if (colour.IsNone)
            {
                throw new ArgumentException(
                    "A cell always holds a block; an empty cell is expressed by the column holding fewer cells.",
                    nameof(colour));
            }

            Colour = colour;
            Hidden = hidden;
        }

        public override string ToString() => Hidden ? Colour + " (hidden)" : Colour.ToString();
    }

    /// <summary>
    /// One authored column: its kind, its capacity and its contents bottom-up.
    /// Validation happens in the constructor, so an illegal column cannot exist —
    /// a broken level fails loudly in the level editor rather than quietly on the
    /// player's device.
    /// </summary>
    public sealed class ColumnData
    {
        public const int MinCapacity = 1;

        /// <summary>Structural bound from fingerprint.md: at most 8 cells per column.</summary>
        public const int MaxCapacity = 8;

        public ColumnKind Kind { get; }

        public int Capacity { get; }

        /// <summary>Contents bottom-up: index 0 is the bottom cell.</summary>
        public IReadOnlyList<CellData> Cells { get; }

        /// <summary>Ice only: the column thaws once this many colours have been completed. 0 otherwise.</summary>
        public int ThawAfterCompletions { get; }

        /// <summary>Covered only: the key colour whose completion opens this cover. None otherwise.</summary>
        public BlockColourId CoverKeyColour { get; }

        public ColumnData(
            ColumnKind kind,
            int capacity,
            IReadOnlyList<CellData> cells,
            int thawAfterCompletions = 0,
            BlockColourId coverKeyColour = default)
        {
            if (capacity < MinCapacity || capacity > MaxCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity), capacity, "A column capacity is " + MinCapacity + ".." + MaxCapacity + ".");
            }

            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count > capacity)
            {
                throw new ArgumentException(
                    "A column holds " + cells.Count + " cells but its capacity is " + capacity + ".", nameof(cells));
            }

            Kind = kind;
            Capacity = capacity;
            Cells = cells;
            ThawAfterCompletions = thawAfterCompletions;
            CoverKeyColour = coverKeyColour;

            ValidateForKind();
        }

        public int BlockCount => Cells.Count;

        private void ValidateForKind()
        {
            if (Kind != ColumnKind.Ice && ThawAfterCompletions != 0)
            {
                throw new ArgumentException("Only an Ice column thaws, so only an Ice column carries thawAfterCompletions.");
            }

            if (Kind != ColumnKind.Covered && !CoverKeyColour.IsNone)
            {
                throw new ArgumentException("Only a Covered column carries a cover key colour.");
            }

            switch (Kind)
            {
                case ColumnKind.Normal:
                    RequireNoHiddenCells();
                    break;

                case ColumnKind.Ice:
                    // The reference draws an ice column empty, with icicles below
                    // (reference §2). Blocks under ice are therefore not an
                    // authored state; if the reference ever shows one, this is the
                    // single line that changes.
                    if (Cells.Count != 0)
                    {
                        throw new ArgumentException("An Ice column starts empty; it holds no authored blocks.");
                    }

                    if (ThawAfterCompletions < 1)
                    {
                        throw new ArgumentException("An Ice column thaws after at least one completed colour.");
                    }

                    break;

                case ColumnKind.Covered:
                    if (Cells.Count == 0)
                    {
                        throw new ArgumentException("A Covered column covers blocks, so it holds at least one.");
                    }

                    if (CoverKeyColour.IsNone)
                    {
                        throw new ArgumentException("A Covered column needs the key colour that opens it.");
                    }

                    for (int cell = 0; cell < Cells.Count; cell++)
                    {
                        if (!Cells[cell].Hidden)
                        {
                            throw new ArgumentException("Every cell of a Covered column is hidden until the cover opens.");
                        }
                    }

                    break;

                case ColumnKind.Mystery:
                    // The invariant the rules rely on: a playable column's top cell
                    // is always readable. A Mystery column is playable from the
                    // start, so its top block is visible and the ones below it are
                    // the hidden '?' bricks.
                    if (Cells.Count > 0 && Cells[Cells.Count - 1].Hidden)
                    {
                        throw new ArgumentException("The top block of a Mystery column is visible; only the cells below it are hidden.");
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown column kind.");
            }
        }

        private void RequireNoHiddenCells()
        {
            for (int cell = 0; cell < Cells.Count; cell++)
            {
                if (Cells[cell].Hidden)
                {
                    throw new ArgumentException("Only a Covered or Mystery column hides cells; " + Kind + " does not.");
                }
            }
        }
    }

    /// <summary>
    /// Everything the rules need to build a board, in plain C#. This is the input
    /// contract Content converts its authored assets into
    /// (<c>LevelDefinition.ToLevelData()</c>) — it carries no presentation data:
    /// the grid layout and the difficulty label stay on the Content side, because
    /// no rule reads them.
    /// </summary>
    public sealed class LevelData
    {
        public const int MinColumns = 2;

        /// <summary>Structural bounds from fingerprint.md.</summary>
        public const int MaxColumns = 16;

        public const int MaxBlocks = 128;

        public int Index { get; }

        public IReadOnlyList<ColumnData> Columns { get; }

        public LevelData(int index, IReadOnlyList<ColumnData> columns)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A level index is not negative.");
            }

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            if (columns.Count < MinColumns || columns.Count > MaxColumns)
            {
                throw new ArgumentException(
                    "A level has " + MinColumns + ".." + MaxColumns + " columns but this one has " + columns.Count + ".",
                    nameof(columns));
            }

            int blocks = 0;
            int colourMask = 0;
            int playableBlocks = 0;

            for (int column = 0; column < columns.Count; column++)
            {
                ColumnData data = columns[column];
                if (data == null)
                {
                    throw new ArgumentException("Column " + column + " is null.", nameof(columns));
                }

                blocks += data.BlockCount;

                if (data.Kind != ColumnKind.Ice && data.Kind != ColumnKind.Covered)
                {
                    playableBlocks += data.BlockCount;
                }

                for (int cell = 0; cell < data.Cells.Count; cell++)
                {
                    colourMask |= 1 << data.Cells[cell].Colour.Value;
                }
            }

            if (blocks == 0)
            {
                throw new ArgumentException("A level holds at least one block.", nameof(columns));
            }

            if (blocks > MaxBlocks)
            {
                throw new ArgumentException(
                    "A level holds at most " + MaxBlocks + " blocks but this one holds " + blocks + ".", nameof(columns));
            }

            if (playableBlocks == 0)
            {
                throw new ArgumentException(
                    "Every block on this level starts locked away, so the first move is impossible.", nameof(columns));
            }

            for (int column = 0; column < columns.Count; column++)
            {
                BlockColourId key = columns[column].CoverKeyColour;
                if (!key.IsNone && (colourMask & (1 << key.Value)) == 0)
                {
                    throw new ArgumentException(
                        "Column " + column + " is covered by a " + key + " key, but that colour is nowhere on the board, so the cover can never open.",
                        nameof(columns));
                }
            }

            Index = index;
            Columns = columns;
            BlockCount = blocks;
        }

        /// <summary>Total authored blocks, hidden ones included.</summary>
        public int BlockCount { get; }
    }
}
