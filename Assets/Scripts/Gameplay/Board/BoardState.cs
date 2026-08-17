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
        private readonly BoardColumn[] _columns;

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
    }
}
