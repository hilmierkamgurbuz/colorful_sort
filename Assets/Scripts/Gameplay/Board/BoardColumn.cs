using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// One column of a live board: its authored shape, plus the little state a
    /// modifier changes (locked or not, which cells are still hidden).
    /// <para>
    /// Every mutator is <c>internal</c>, so the only way state changes is through a
    /// recorded move inside this assembly. <c>BoardView</c> and <c>UI</c> get the
    /// readers and nothing else, which is the "the view never writes to Board"
    /// invariant expressed in the type system rather than in a review comment.
    /// </para>
    /// </summary>
    public sealed class BoardColumn
    {
        private readonly BlockColourId[] _colours;
        private readonly bool[] _hidden;
        private int _count;

        internal BoardColumn(ColumnData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Allocated once when the level loads (load frequency, ×0.01) and never
            // again: no move, and no frame, allocates on the board.
            _colours = new BlockColourId[data.Capacity];
            _hidden = new bool[data.Capacity];
            _count = data.Cells.Count;

            for (int cell = 0; cell < _count; cell++)
            {
                _colours[cell] = data.Cells[cell].Colour;
                _hidden[cell] = data.Cells[cell].Hidden;
            }

            Kind = data.Kind;
            ThawAfterCompletions = data.ThawAfterCompletions;
            CoverKeyColour = data.CoverKeyColour;
            IsLocked = ColumnModifiers.StartsLocked(data.Kind);
        }

        public ColumnKind Kind { get; }

        public int Capacity => _colours.Length;

        /// <summary>Ice only: how many completed colours it takes to thaw this column.</summary>
        public int ThawAfterCompletions { get; }

        /// <summary>Covered only: the colour whose completion opens this cover.</summary>
        public BlockColourId CoverKeyColour { get; }

        /// <summary>Ice before it thaws, Covered before it opens: no block moves in or out.</summary>
        public bool IsLocked { get; private set; }

        public int Count => _count;

        public bool IsEmpty => _count == 0;

        public bool IsFull => _count == Capacity;

        public int FreeCells => Capacity - _count;

        /// <summary>The colour of the top block, or None when the column is empty.</summary>
        public BlockColourId TopColour => _count == 0 ? BlockColourId.None : _colours[_count - 1];

        /// <summary>
        /// True while the top block is still unreadable. This can only be the case
        /// on a locked column: a playable column's top cell is always revealed.
        /// </summary>
        public bool IsTopHidden => _count != 0 && _hidden[_count - 1];

        /// <summary>Cells are indexed bottom-up: 0 is the bottom cell.</summary>
        public BlockColourId ColourAt(int cellIndex)
        {
            RequireOccupied(cellIndex);
            return _colours[cellIndex];
        }

        public bool IsHiddenAt(int cellIndex)
        {
            RequireOccupied(cellIndex);
            return _hidden[cellIndex];
        }

        internal void Push(BlockColourId colour)
        {
            if (colour.IsNone)
            {
                throw new ArgumentException("A pushed block has a colour.", nameof(colour));
            }

            if (IsFull)
            {
                throw new InvalidOperationException("The column is full; the move rule should have rejected this.");
            }

            _colours[_count] = colour;
            _hidden[_count] = false;
            _count++;
        }

        internal BlockColourId Pop()
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("The column is empty; the move rule should have rejected this.");
            }

            _count--;
            BlockColourId colour = _colours[_count];
            _colours[_count] = BlockColourId.None;
            _hidden[_count] = false;
            return colour;
        }

        /// <summary>Reveals one cell. Returns false when it was already readable.</summary>
        internal bool Reveal(int cellIndex)
        {
            RequireOccupied(cellIndex);
            if (!_hidden[cellIndex])
            {
                return false;
            }

            _hidden[cellIndex] = false;
            return true;
        }

        /// <summary>Hides a cell again — the undo direction of <see cref="Reveal"/>.</summary>
        internal void Hide(int cellIndex)
        {
            RequireOccupied(cellIndex);
            _hidden[cellIndex] = true;
        }

        internal void SetLocked(bool locked)
        {
            IsLocked = locked;
        }

        private void RequireOccupied(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= _count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cellIndex), cellIndex, "The column holds " + _count + " block(s).");
            }
        }
    }
}
