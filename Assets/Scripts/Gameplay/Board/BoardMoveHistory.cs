using System;
using System.Collections.Generic;

namespace ColorfulSort.Board
{
    /// <summary>
    /// Every mutation the board has seen this attempt, oldest first. Undo pops from
    /// the end; nothing changes the board off this history, or undo would not be
    /// exact.
    /// <para>
    /// The list is capped at <see cref="MaxEntries"/> (fingerprint.md). When it
    /// overflows the oldest entry is dropped — undo reaches back 256 moves and no
    /// further, and <see cref="DroppedEntryCount"/> says so out loud rather than
    /// letting the Undo booster look broken.
    /// </para>
    /// </summary>
    public sealed class BoardMoveHistory
    {
        /// <summary>Hard bound from fingerprint.md: 256 history entries per attempt.</summary>
        public const int MaxEntries = 256;

        private readonly List<BoardMove> _entries = new List<BoardMove>(MaxEntries);

        public int Count => _entries.Count;

        public bool CanUndo => _entries.Count > 0;

        /// <summary>Moves that fell off the front of the cap and can never be undone.</summary>
        public int DroppedEntryCount { get; private set; }

        public IReadOnlyList<BoardMove> Entries => _entries;

        public BoardMove Last
        {
            get
            {
                if (_entries.Count == 0)
                {
                    throw new InvalidOperationException("The history is empty.");
                }

                return _entries[_entries.Count - 1];
            }
        }

        internal void Record(BoardMove move)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            if (_entries.Count == MaxEntries)
            {
                // 256 references copied down one slot, at most once per move: about
                // a microsecond at event frequency, against a ring buffer's extra
                // index arithmetic in every reader. The cost model picks readable.
                _entries.RemoveAt(0);
                DroppedEntryCount++;
            }

            _entries.Add(move);
        }

        internal BoardMove TakeLast()
        {
            BoardMove move = Last;
            _entries.RemoveAt(_entries.Count - 1);
            return move;
        }
    }
}
