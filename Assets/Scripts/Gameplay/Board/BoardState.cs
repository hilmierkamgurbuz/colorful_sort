using System;
using System.Collections.Generic;

namespace ColorfulSort.Board
{
    /// <summary>
    /// The board as it stands right now: the columns, and which colours have been
    /// completed during this attempt.
    /// <para>
    /// Win and deadlock are <em>not</em> here — they are computed from this state by
    /// <c>BoardRules</c> so that no code can forget to set a flag. The completed
    /// colour set is different: it is a history fact, not a snapshot of the board.
    /// Ice thaws per completed colour and a thawed column stays thawed even if the
    /// player later breaks that colour apart again, so the count cannot be derived
    /// from the columns and is recorded here instead — and undone by the move that
    /// caused it.
    /// </para>
    /// </summary>
    public sealed class BoardState
    {
        // Not readonly since the add-column booster: a board can grow by one column and
        // shrink again when that is undone. Nothing else ever replaces this array.
        private BoardColumn[] _columns;

        // One flag per colour id (1..MaxId). A fixed 13-entry array beats a hash set
        // here: no hashing, no allocation per query, and the whole thing is smaller
        // than the set's header would be.
        private readonly bool[] _completedColours = new bool[BlockColourId.MaxId + 1];

        private BoardState(int levelIndex, BoardColumn[] columns)
        {
            LevelIndex = levelIndex;
            _columns = columns;
        }

        public int LevelIndex { get; }

        public IReadOnlyList<BoardColumn> Columns => _columns;

        public int ColumnCount => _columns.Length;

        /// <summary>How many distinct colours have been completed this attempt. Drives the ice thaw.</summary>
        public int CompletedColourCount { get; private set; }

        public BoardColumn this[int columnIndex]
        {
            get
            {
                if (columnIndex < 0 || columnIndex >= _columns.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(columnIndex), columnIndex, "The board has " + _columns.Length + " column(s).");
                }

                return _columns[columnIndex];
            }
        }

        /// <summary>Builds the starting board for a level. The only way a board comes into existence.</summary>
        public static BoardState FromLevel(LevelData level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var columns = new BoardColumn[level.Columns.Count];
            for (int column = 0; column < columns.Length; column++)
            {
                columns[column] = new BoardColumn(level.Columns[column]);
            }

            var state = new BoardState(level.Index, columns);

            // A board never starts with a colour already gathered. It would hand the
            // player a free colour, and it would make the ice/cover trigger
            // ambiguous — is a colour that was finished before the first move
            // "completed"? Refusing the data is what keeps that question from
            // existing, and the check reuses the one definition of completeness
            // rather than restating it.
            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                var colour = new BlockColourId(id);
                if (BoardRules.IsColourComplete(state, colour))
                {
                    throw new ArgumentException(
                        "Level " + level.Index + " starts with " + colour +
                        " already gathered in a single column, so it would begin partly solved.",
                        nameof(level));
                }
            }

            return state;
        }

        /// <summary>
        /// True once this colour has been completed at least once this attempt —
        /// which is what ice and covers react to, not "is it complete right now".
        /// </summary>
        public bool HasEverCompleted(BlockColourId colour)
        {
            return !colour.IsNone && _completedColours[colour.Value];
        }

        /// <summary>Records a completion. Returns false when it was already recorded.</summary>
        internal bool MarkCompleted(BlockColourId colour)
        {
            if (colour.IsNone || _completedColours[colour.Value])
            {
                return false;
            }

            _completedColours[colour.Value] = true;
            CompletedColourCount++;
            return true;
        }

        /// <summary>The undo direction of <see cref="MarkCompleted"/>.</summary>
        internal void UnmarkCompleted(BlockColourId colour)
        {
            if (colour.IsNone || !_completedColours[colour.Value])
            {
                return;
            }

            _completedColours[colour.Value] = false;
            CompletedColourCount--;
        }

        /// <summary>Mutable access for the move machinery inside this assembly.</summary>
        internal BoardColumn ColumnFor(int columnIndex) => this[columnIndex];

        /// <summary>
        /// Adds a column to the end of the board — the add-column booster, and the only way
        /// a column appears after <see cref="FromLevel"/> has run. Copying the array rather
        /// than carrying a <c>List</c> keeps the common path (indexing a column, dozens of
        /// times per rule query) on a plain array; this copy happens once per booster press.
        /// </summary>
        internal void AppendColumn(BoardColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            // The same ceiling an authored level is held to (fingerprint.md: ≤ 16 columns),
            // read from the one place that already states it rather than restated here.
            if (_columns.Length >= LevelData.MaxColumns)
            {
                throw new InvalidOperationException(
                    "The board already holds " + LevelData.MaxColumns + " columns; the booster should have refused this.");
            }

            var grown = new BoardColumn[_columns.Length + 1];
            Array.Copy(_columns, grown, _columns.Length);
            grown[_columns.Length] = column;
            _columns = grown;
        }

        /// <summary>
        /// The undo direction of <see cref="AppendColumn"/>. It refuses to drop a column
        /// holding blocks: undo is last-in-first-out, so by the time an add-column is
        /// reverted every move that filled it has been reverted too — a column with
        /// anything in it means the history and the board have come apart, which is worth
        /// stopping on rather than quietly deleting the player's blocks.
        /// </summary>
        internal void RemoveLastColumn()
        {
            if (_columns.Length == 0)
            {
                throw new InvalidOperationException("There is no column to remove.");
            }

            BoardColumn last = _columns[_columns.Length - 1];

            if (!last.IsEmpty)
            {
                throw new InvalidOperationException(
                    "The column being removed still holds " + last.Count + " block(s), so the history and the board disagree.");
            }

            var shrunk = new BoardColumn[_columns.Length - 1];
            Array.Copy(_columns, shrunk, shrunk.Length);
            _columns = shrunk;
        }
    }
}
