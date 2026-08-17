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

        private List<CellRef> _revealedCells;
        private List<int> _thawedColumns;
        private List<int> _uncoveredColumns;
        private List<BlockColourId> _completedColours;

        internal BoardMove(int fromColumn, int toColumn, int count, BlockColourId colour, int rngCursorBefore)
        {
            FromColumn = fromColumn;
            ToColumn = toColumn;
            Count = count;
            Colour = colour;
            RngCursorBefore = rngCursorBefore;
        }

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

        public override string ToString()
        {
            return Count + "x" + Colour + " " + FromColumn + "->" + ToColumn;
        }
    }
}
