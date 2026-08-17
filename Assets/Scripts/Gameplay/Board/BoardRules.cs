using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// The move rule, the win condition and the deadlock test — pure functions of
    /// board state: same input, same answer, no side effects, no logging.
    /// <para>
    /// Nothing here is cached or tracked as a flag. With fingerprint.md's scale
    /// (≤16 columns, ≤8 cells, ≤12 colours) the whole deadlock test is 256 pair
    /// checks and the win test 128 cell reads, both at <em>event</em> frequency —
    /// thousandths of a 16.6 ms frame, so the cost model picks the readable
    /// recompute over incremental bookkeeping every time.
    /// </para>
    /// </summary>
    public static class BoardRules
    {
        /// <summary>
        /// Length of the contiguous same-colour run on top of a column — the blocks
        /// a tap lifts. Hidden cells end the run: an unreadable block is not part of
        /// a run the player can see.
        /// </summary>
        public static int TopRunLength(BoardColumn column)
        {
            if (column == null || column.IsEmpty || column.IsTopHidden)
            {
                return 0;
            }

            BlockColourId top = column.TopColour;
            int run = 0;

            for (int cell = column.Count - 1; cell >= 0; cell--)
            {
                if (column.IsHiddenAt(cell) || column.ColourAt(cell) != top)
                {
                    break;
                }

                run++;
            }

            return run;
        }

        /// <summary>True when tapping this column would lift something.</summary>
        public static bool CanLift(BoardState state, int columnIndex)
        {
            RequireState(state);
            BoardColumn column = state[columnIndex];
            return ColumnModifiers.IsPlayable(column) && TopRunLength(column) > 0;
        }

        /// <summary>
        /// How many blocks a move from one column to another would actually carry:
        /// the top run, capped by the free space in the destination ("only as many
        /// blocks as still fit", reference §1). 0 means the move is illegal.
        /// </summary>
        public static int MovableCount(BoardState state, int fromColumn, int toColumn)
        {
            RequireState(state);

            if (fromColumn == toColumn)
            {
                return 0;
            }

            BoardColumn source = state[fromColumn];
            BoardColumn destination = state[toColumn];

            if (!ColumnModifiers.IsPlayable(source) || !ColumnModifiers.IsPlayable(destination))
            {
                return 0;
            }

            if (destination.IsFull || destination.IsTopHidden)
            {
                return 0;
            }

            int run = TopRunLength(source);
            if (run == 0)
            {
                return 0;
            }

            if (!destination.IsEmpty && destination.TopColour != source.TopColour)
            {
                return 0;
            }

            int free = destination.FreeCells;
            return run < free ? run : free;
        }

        public static bool CanMove(BoardState state, int fromColumn, int toColumn)
        {
            return MovableCount(state, fromColumn, toColumn) > 0;
        }

        /// <summary>A column that holds one colour and nothing else — "solved" in the reference's words.</summary>
        public static bool IsColumnSolved(BoardState state, int columnIndex)
        {
            RequireState(state);
            BoardColumn column = state[columnIndex];

            if (column.IsEmpty)
            {
                return false;
            }

            BlockColourId first = column.ColourAt(0);
            for (int cell = 1; cell < column.Count; cell++)
            {
                if (column.ColourAt(cell) != first)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when every block of this colour sits in one column, that column holds
        /// nothing else, and none of it is still hidden. This is what ice and covers
        /// react to.
        /// <para>
        /// Hidden blocks count as present — a block still under a cover keeps its
        /// colour incomplete — and they also block the completion of the column they
        /// sit in. Without that second half a colour could finish while the player is
        /// still looking at a '?', so ice would thaw for no visible reason and a board
        /// could be declared won with an unrevealed brick on it.
        /// </para>
        /// </summary>
        public static bool IsColourComplete(BoardState state, BlockColourId colour)
        {
            RequireState(state);

            if (colour.IsNone)
            {
                return false;
            }

            int holder = -1;

            for (int column = 0; column < state.ColumnCount; column++)
            {
                BoardColumn candidate = state[column];
                bool holdsColour = false;
                bool holdsSomethingElse = false;
                bool holdsHidden = false;

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    if (candidate.ColourAt(cell) == colour)
                    {
                        holdsColour = true;
                    }
                    else
                    {
                        holdsSomethingElse = true;
                    }

                    if (candidate.IsHiddenAt(cell))
                    {
                        holdsHidden = true;
                    }
                }

                if (!holdsColour)
                {
                    continue;
                }

                if (holder != -1 || holdsSomethingElse || holdsHidden)
                {
                    return false;
                }

                holder = column;
            }

            return holder != -1;
        }

        /// <summary>
        /// Win: every colour is gathered in a single column and no column holds a
        /// mixture (reference §1). A board with a hidden cell left on it is never won —
        /// an unrevealed '?' means the player has not actually finished reading the
        /// board, and a win popup over a '?' brick would be a lie.
        /// </summary>
        public static bool IsWin(BoardState state)
        {
            RequireState(state);

            // One bit per colour id; ids are 1..12, so an int holds the whole board.
            int seenColours = 0;

            for (int column = 0; column < state.ColumnCount; column++)
            {
                BoardColumn candidate = state[column];
                if (candidate.IsEmpty)
                {
                    continue;
                }

                BlockColourId colour = candidate.ColourAt(0);
                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    if (candidate.IsHiddenAt(cell) || candidate.ColourAt(cell) != colour)
                    {
                        return false;
                    }
                }

                int bit = 1 << colour.Value;
                if ((seenColours & bit) != 0)
                {
                    return false;
                }

                seenColours |= bit;
            }

            return seenColours != 0;
        }

        /// <summary>
        /// True while at least one legal move exists. A locked column cannot rescue
        /// the board: ice thaws and covers open only on a completed colour, and
        /// completing a colour needs a move.
        /// </summary>
        public static bool HasAnyLegalMove(BoardState state)
        {
            RequireState(state);

            for (int from = 0; from < state.ColumnCount; from++)
            {
                if (TopRunLength(state[from]) == 0 || !ColumnModifiers.IsPlayable(state[from]))
                {
                    continue;
                }

                for (int to = 0; to < state.ColumnCount; to++)
                {
                    if (MovableCount(state, from, to) > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Lose: not won, and no legal move left (reference §1 — no move counter, no timer).</summary>
        public static bool IsDeadlock(BoardState state)
        {
            return !IsWin(state) && !HasAnyLegalMove(state);
        }

        private static void RequireState(BoardState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
        }
    }
}
