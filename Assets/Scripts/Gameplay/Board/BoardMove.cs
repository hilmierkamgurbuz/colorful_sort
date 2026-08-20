using System;
using System.Collections.Generic;

namespace ColorfulSort.Board
{
    /// <summary>A cell address on the board. Cells are indexed bottom-up.</summary>
    public readonly struct CellRef : IEquatable<CellRef>
    {
        public readonly int Column;
        public readonly int Cell;

        public CellRef(int column, int cell)
        {
            Column = column;
            Cell = cell;
        }

        public bool Equals(CellRef other) => Column == other.Column && Cell == other.Cell;

        public override bool Equals(object obj) => obj is CellRef other && Equals(other);

        public override int GetHashCode() => (Column * 397) ^ Cell;

        public override string ToString() => "(" + Column + "," + Cell + ")";
    }

    /// <summary>What kind of mutation a recorded move is. A plain move is a <see cref="Run"/>.</summary>
    public enum MoveKind
    {
        /// <summary>The move rule: a run of one colour lifted from a column and dropped on another.</summary>
        Run,

        /// <summary>The add-column booster: one empty column appended to the board.</summary>
        AddColumn,

        /// <summary>The shuffle booster: the visible cells rearranged among themselves.</summary>
        Shuffle,
    }

    /// <summary>One cell a shuffle touched, and what it held before.</summary>
    public readonly struct ShuffledCell
    {
        public readonly CellRef Cell;

        public readonly BlockColourId PreviousColour;

        public ShuffledCell(CellRef cell, BlockColourId previousColour)
        {
            Cell = cell;
            PreviousColour = previousColour;
        }
    }

    /// <summary>
    /// One recorded board mutation: the run that moved, and every side effect it
    /// caused. This is the whole undo contract — replaying these fields backwards
    /// has to reproduce the previous board exactly, so anything a move changes is
    /// written down here or the invariant is broken.
    /// <para>
    /// One object per move, and a move happens at most about once a second, so the
    /// allocation is invisible; the lists are created only when a side effect
    /// actually occurs, which for a plain move is never.
    /// </para>
    /// </summary>
    public sealed class BoardMove
    {
        private static readonly CellRef[] NoCells = new CellRef[0];
        private static readonly int[] NoColumns = new int[0];
        private static readonly BlockColourId[] NoColours = new BlockColourId[0];
        private static readonly ShuffledCell[] NoShuffledCells = new ShuffledCell[0];

        private List<CellRef> _revealedCells;
        private List<int> _thawedColumns;
        private List<int> _uncoveredColumns;
        private List<BlockColourId> _completedColours;
        private List<ShuffledCell> _shuffledCells;

        internal BoardMove(int fromColumn, int toColumn, int count, BlockColourId colour, int rngCursorBefore)
        {
            Kind = MoveKind.Run;
            FromColumn = fromColumn;
            ToColumn = toColumn;
            Count = count;
            Colour = colour;
            RngCursorBefore = rngCursorBefore;
        }

        /// <summary>
        /// A booster's move. Neither kind moves a run, so the run fields stay at their
        /// no-column sentinel rather than carrying a number that means nothing.
        /// </summary>
        internal BoardMove(MoveKind kind, int rngCursorBefore)
        {
            Kind = kind;
            FromColumn = NoColumn;
            ToColumn = NoColumn;
            Count = 0;
            Colour = BlockColourId.None;
            RngCursorBefore = rngCursorBefore;
        }

        /// <summary>What the run fields read when a move is not a run.</summary>
        public const int NoColumn = -1;

        /// <summary>Which of the three mutations this is. Undo branches on it.</summary>
        public MoveKind Kind { get; }

        public int FromColumn { get; }

        public int ToColumn { get; }

        /// <summary>How many blocks travelled.</summary>
        public int Count { get; }

        /// <summary>The colour of the moved run; all of it is one colour by the move rule.</summary>
        public BlockColourId Colour { get; }

        /// <summary>
        /// The attempt RNG's cursor before this move ran. Undo rewinds to it, so a
        /// re-do draws the same values (D-002).
        /// </summary>
        public int RngCursorBefore { get; }

        /// <summary>Cells this move made readable: a Mystery reveal, or every cell of a cover that opened.</summary>
        public IReadOnlyList<CellRef> RevealedCells => _revealedCells ?? (IReadOnlyList<CellRef>)NoCells;

        /// <summary>Ice columns this move unlocked.</summary>
        public IReadOnlyList<int> ThawedColumns => _thawedColumns ?? (IReadOnlyList<int>)NoColumns;

        /// <summary>Covered columns this move opened.</summary>
        public IReadOnlyList<int> UncoveredColumns => _uncoveredColumns ?? (IReadOnlyList<int>)NoColumns;

        /// <summary>Colours completed for the first time by this move — usually none or one, occasionally two.</summary>
        public IReadOnlyList<BlockColourId> CompletedColours => _completedColours ?? (IReadOnlyList<BlockColourId>)NoColours;

        internal void AddRevealedCell(CellRef cell)
        {
            (_revealedCells ?? (_revealedCells = new List<CellRef>(4))).Add(cell);
        }

        internal void AddThawedColumn(int columnIndex)
        {
            (_thawedColumns ?? (_thawedColumns = new List<int>(2))).Add(columnIndex);
        }

        internal void AddUncoveredColumn(int columnIndex)
        {
            (_uncoveredColumns ?? (_uncoveredColumns = new List<int>(2))).Add(columnIndex);
        }

        internal void AddCompletedColour(BlockColourId colour)
        {
            (_completedColours ?? (_completedColours = new List<BlockColourId>(2))).Add(colour);
        }

        /// <summary>
        /// Every cell a shuffle rearranged, with the colour it held first.
        /// <para>
        /// The preflight for this task assumed the opposite — that a shuffle would store
        /// nothing and undo would rewind the RNG and recompute the permutation, since the
        /// seed determines it. Writing it made the contradiction plain: that design is
        /// exactly the one the same preflight named as its worst risk, an undo that is off
        /// by one draw and restores a board which looks right and is not. A recorded
        /// before-state cannot be off by anything. It costs one array of at most 128
        /// entries per shuffle, at booster frequency (D-041).
        /// </para>
        /// </summary>
        public IReadOnlyList<ShuffledCell> ShuffledCells => _shuffledCells ?? (IReadOnlyList<ShuffledCell>)NoShuffledCells;

        internal void AddShuffledCell(CellRef cell, BlockColourId previousColour)
        {
            (_shuffledCells ?? (_shuffledCells = new List<ShuffledCell>(32))).Add(new ShuffledCell(cell, previousColour));
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case MoveKind.AddColumn:
                    return "add column";

                case MoveKind.Shuffle:
                    return "shuffle " + ShuffledCells.Count + " cell(s)";

                default:
                    return Count + "x" + Colour + " " + FromColumn + "->" + ToColumn;
            }
        }
    }
}
