using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// One attempt at one level: the board, its move history and its seeded RNG,
    /// behind the only API that is allowed to change any of them.
    /// <para>
    /// This is the whole command surface the rest of the game talks to.
    /// <c>BoardView</c> turns a tap into <see cref="TryMove"/> and then redraws from
    /// what it is told; the Undo booster calls <see cref="Undo"/>. Neither ever
    /// reaches into board state, and there is no other way in: every mutator below
    /// this line is internal to the assembly and goes through a recorded
    /// <see cref="BoardMove"/>.
    /// </para>
    /// <para>
    /// Selection ("which column is lifted") is deliberately absent: it is
    /// interaction state, it is owned by <c>BoardView</c>, and putting it here would
    /// give the same datum two writers.
    /// </para>
    /// </summary>
    public sealed class BoardSession
    {
        private bool _isApplying;

        /// <summary>
        /// Starts an attempt. The scramble is named, never implied:
        /// <see cref="AttemptScramble.ForVariant"/> for one of the level's looks, or
        /// <see cref="AttemptScramble.None"/> for the authored board — the level editor's
        /// preview and validation path. There is no shorter constructor on purpose: a
        /// caller cannot quietly lose the memorisation defence by omitting an argument,
        /// because the compiler asks the question instead.
        /// </summary>
        public BoardSession(LevelData level, int seed, AttemptScramble scramble)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (scramble == null)
            {
                throw new ArgumentNullException(nameof(scramble));
            }

            Random = new DeterministicRandom(seed);
            Build(level, scramble);
        }

        /// <summary>The level this attempt is actually playing: authored, then scrambled.</summary>
        public LevelData Level { get; private set; }

        /// <summary>What Content authored, untouched. The scramble is a view of this, never an edit to it.</summary>
        public LevelData AuthoredLevel { get; private set; }

        /// <summary>
        /// Which skin each authored colour is wearing this attempt and which slot each
        /// authored column stands in. Read it to explain a board; it never changes once
        /// the attempt has begun.
        /// </summary>
        public AttemptScramble Scramble { get; private set; }

        public BoardState State { get; private set; }

        /// <summary>The attempt's only randomness. Its seed is what makes the attempt replayable.</summary>
        public DeterministicRandom Random { get; }

        public BoardMoveHistory History { get; private set; }

        public int Seed => Random.Seed;

        /// <summary>Raised after a move is committed and recorded, before its side-effect events.</summary>
        public event Action<BoardMove> MoveApplied;

        /// <summary>Raised after a move has been reverted; the board already shows the earlier state.</summary>
        public event Action<BoardMove> MoveUndone;

        /// <summary>The one trigger ice and covers hang off (D-009). Also what a "colour solved" flourish listens to.</summary>
        public event Action<BlockColourId> ColourCompleted;

        /// <summary>A hidden cell became readable — a Mystery reveal, or a cell under a cover that just opened.</summary>
        public event Action<CellRef> CellRevealed;

        /// <summary>An ice column thawed or a cover opened; the column is now playable.</summary>
        public event Action<int> ColumnUnlocked;

        /// <summary>
        /// Raised once a committed move leaves the board won. <see cref="IsWon"/> is
        /// the authority — this is a notification, never the place the answer is
        /// stored.
        /// </summary>
        public event Action Won;

        /// <summary>Raised once a committed move leaves the board with no legal move (reference §1).</summary>
        public event Action Deadlocked;

        /// <summary>Computed from state on every read, never tracked as a flag.</summary>
        public bool IsWon => BoardRules.IsWin(State);

        /// <summary>Computed from state on every read, never tracked as a flag.</summary>
        public bool IsDeadlocked => BoardRules.IsDeadlock(State);

        public bool CanUndo => History.CanUndo;

        public bool CanLift(int columnIndex) => BoardRules.CanLift(State, columnIndex);

        public bool CanMove(int fromColumn, int toColumn) => BoardRules.CanMove(State, fromColumn, toColumn);

        public int MovableCount(int fromColumn, int toColumn) => BoardRules.MovableCount(State, fromColumn, toColumn);

        public bool TryMove(int fromColumn, int toColumn) => TryMove(fromColumn, toColumn, out _);

        /// <summary>
        /// Commits a move if the rule allows it: lifts the top run off
        /// <paramref name="fromColumn"/>, drops what fits onto
        /// <paramref name="toColumn"/>, then applies whatever that caused — a Mystery
        /// reveal, completed colours, thawed ice, opened covers — recording all of
        /// it in one history entry.
        /// </summary>
        /// <returns>False when the move is illegal; the board is then untouched.</returns>
        public bool TryMove(int fromColumn, int toColumn, out BoardMove move)
        {
            move = null;
            RequireNotReentrant();

            int count = BoardRules.MovableCount(State, fromColumn, toColumn);
            if (count == 0)
            {
                return false;
            }

            BoardColumn source = State.ColumnFor(fromColumn);
            BoardColumn destination = State.ColumnFor(toColumn);
            BlockColourId colour = source.TopColour;

            var record = new BoardMove(fromColumn, toColumn, count, colour, Random.Cursor);

            _isApplying = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    source.Pop();
                    destination.Push(colour);
                }

                RevealExposedTop(fromColumn, record);
                ApplyCompletions(record);

                History.Record(record);
                move = record;

                // Raised inside the guard on purpose: a move is atomic, so a
                // subscriber that answers by ordering another one gets a loud
                // exception instead of a half-applied board.
                RaiseMoveEvents(record);
            }
            finally
            {
                _isApplying = false;
            }

            return true;
        }

        /// <summary>
        /// Reverts the last recorded move exactly: the side effects come undone in
        /// the reverse of the order they were applied, then the blocks travel back,
        /// then the RNG cursor rewinds. Nothing about the board is left out — cells,
        /// hidden flags, locks, completed colours and the RNG position are the whole
        /// of the state.
        /// </summary>
        /// <returns>False when there is nothing left to undo.</returns>
        public bool Undo()
        {
            RequireNotReentrant();

            if (!History.CanUndo)
            {
                return false;
            }

            BoardMove move = History.TakeLast();

            _isApplying = true;
            try
            {
                var revealed = move.RevealedCells;
                for (int i = revealed.Count - 1; i >= 0; i--)
                {
                    CellRef cell = revealed[i];
                    State.ColumnFor(cell.Column).Hide(cell.Cell);
                }

                var uncovered = move.UncoveredColumns;
                for (int i = uncovered.Count - 1; i >= 0; i--)
                {
                    State.ColumnFor(uncovered[i]).SetLocked(true);
                }

                var thawed = move.ThawedColumns;
                for (int i = thawed.Count - 1; i >= 0; i--)
                {
                    State.ColumnFor(thawed[i]).SetLocked(true);
                }

                var completed = move.CompletedColours;
                for (int i = completed.Count - 1; i >= 0; i--)
                {
                    State.UnmarkCompleted(completed[i]);
                }

                BoardColumn source = State.ColumnFor(move.FromColumn);
                BoardColumn destination = State.ColumnFor(move.ToColumn);
                for (int i = 0; i < move.Count; i++)
                {
                    destination.Pop();
                    source.Push(move.Colour);
                }

                Random.Rewind(move.RngCursorBefore);

                MoveUndone?.Invoke(move);
            }
            finally
            {
                _isApplying = false;
            }

            return true;
        }

        /// <summary>
        /// A Mystery cell reveals itself when it becomes the top block.
        /// <para>
        /// The revealed colour is <em>authored</em>: the level stores what is under
        /// the '?' and this only clears the hidden flag, so the RNG is not consulted.
        /// The task's preflight assumed a random draw here (and D-002 lists Mystery
        /// as an RNG consumer), but a drawn colour cannot be reconciled with D-010 —
        /// the level editor has to validate solvability before a level ships, and a
        /// board whose contents differ per attempt cannot be validated at all, nor
        /// transcribed 1:1 from the reference. The RNG and its recorded cursor stay
        /// in place for the Shuffle booster, which genuinely does inject randomness.
        /// See the postflight: this is an open question for the user, and flipping it
        /// back costs this one method.
        /// </para>
        /// </summary>
        private void RevealExposedTop(int columnIndex, BoardMove record)
        {
            BoardColumn column = State.ColumnFor(columnIndex);

            if (!ColumnModifiers.RevealsTopWhenExposed(column.Kind) || column.IsEmpty || !column.IsTopHidden)
            {
                return;
            }

            int cell = column.Count - 1;
            if (column.Reveal(cell))
            {
                record.AddRevealedCell(new CellRef(columnIndex, cell));
            }
        }

        /// <summary>
        /// Detects the colours this move completed and lets the modifiers react — one
        /// trigger point, two readers (D-009).
        /// <para>
        /// Every colour is re-tested rather than only the moved one, because a single
        /// move can complete two: the run lands on its final column while the blocks
        /// left behind in the source column turn out to be a finished colour too.
        /// 12 colours × 128 cells at event frequency is nothing, and the readable
        /// version cannot miss a case an incremental one would.
        /// </para>
        /// </summary>
        private void ApplyCompletions(BoardMove record)
        {
            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                var colour = new BlockColourId(id);

                if (State.HasEverCompleted(colour) || !BoardRules.IsColourComplete(State, colour))
                {
                    continue;
                }

                State.MarkCompleted(colour);
                record.AddCompletedColour(colour);
            }

            if (record.CompletedColours.Count == 0)
            {
                return;
            }

            for (int column = 0; column < State.ColumnCount; column++)
            {
                BoardColumn candidate = State.ColumnFor(column);

                for (int i = 0; i < record.CompletedColours.Count; i++)
                {
                    if (!ColumnModifiers.ShouldUncover(candidate, record.CompletedColours[i]))
                    {
                        continue;
                    }

                    candidate.SetLocked(false);
                    record.AddUncoveredColumn(column);

                    for (int cell = 0; cell < candidate.Count; cell++)
                    {
                        if (candidate.Reveal(cell))
                        {
                            record.AddRevealedCell(new CellRef(column, cell));
                        }
                    }

                    break;
                }

                if (ColumnModifiers.ShouldThaw(candidate, State.CompletedColourCount))
                {
                    candidate.SetLocked(false);
                    record.AddThawedColumn(column);
                }
            }
        }

        private void RaiseMoveEvents(BoardMove move)
        {
            MoveApplied?.Invoke(move);

            var completed = move.CompletedColours;
            for (int i = 0; i < completed.Count; i++)
            {
                ColourCompleted?.Invoke(completed[i]);
            }

            var revealed = move.RevealedCells;
            for (int i = 0; i < revealed.Count; i++)
            {
                CellRevealed?.Invoke(revealed[i]);
            }

            var uncovered = move.UncoveredColumns;
            for (int i = 0; i < uncovered.Count; i++)
            {
                ColumnUnlocked?.Invoke(uncovered[i]);
            }

            var thawed = move.ThawedColumns;
            for (int i = 0; i < thawed.Count; i++)
            {
                ColumnUnlocked?.Invoke(thawed[i]);
            }

            if (IsWon)
            {
                Won?.Invoke();
            }
            else if (IsDeadlocked)
            {
                Deadlocked?.Invoke();
            }
        }

        /// <summary>
        /// A subscriber that answers an event by ordering another move would
        /// interleave two mutations and scramble the history order. The board has one
        /// writer; this is that rule, enforced at runtime.
        /// </summary>
        /// <summary>
        /// The scramble is applied here, once, before a single move exists — which is why
        /// it needs no history entry: undo walks back through recorded moves, and there is
        /// no state older than this to walk back to. It also leaves the attempt RNG
        /// untouched, because a variant reads a stream private to the level, so the whole
        /// cursor still belongs to the Shuffle booster.
        /// </summary>
        private void Build(LevelData authored, AttemptScramble scramble)
        {
            AuthoredLevel = authored;
            Scramble = scramble;
            Level = scramble.Apply(authored);
            State = BoardState.FromLevel(Level);
            History = new BoardMoveHistory();
        }

        private void RequireNotReentrant()
        {
            if (_isApplying)
            {
                throw new InvalidOperationException(
                    "The board is already applying a move. Commands cannot be issued from inside a board event.");
            }
        }
    }
}
