using System.Text;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// Board fixtures for the three test files in this assembly. Columns are written
    /// bottom-up, exactly as the rules index them.
    /// <para>
    /// It lives in this file rather than a fourth one because the task's manifest
    /// declares three test files; if it grows past a handful of helpers it earns its
    /// own file in a later task.
    /// </para>
    /// </summary>
    internal static class TestBoards
    {
        internal const int Seed = 20260817;

        internal static CellData Cell(int colour, bool hidden = false)
        {
            return new CellData(new BlockColourId(colour), hidden);
        }

        /// <summary>A plain column, contents bottom-up.</summary>
        internal static ColumnData Normal(int capacity, params int[] cellsBottomUp)
        {
            return new ColumnData(ColumnKind.Normal, capacity, Cells(cellsBottomUp, -1));
        }

        /// <summary>An ice column: empty and locked until the given number of colours are done.</summary>
        internal static ColumnData Ice(int capacity, int thawAfterCompletions)
        {
            return new ColumnData(ColumnKind.Ice, capacity, new CellData[0], thawAfterCompletions);
        }

        /// <summary>A covered column: every cell hidden, opened by the key colour.</summary>
        internal static ColumnData Covered(int capacity, int keyColour, params int[] cellsBottomUp)
        {
            var cells = new CellData[cellsBottomUp.Length];
            for (int cell = 0; cell < cells.Length; cell++)
            {
                cells[cell] = Cell(cellsBottomUp[cell], true);
            }

            return new ColumnData(ColumnKind.Covered, capacity, cells, 0, new BlockColourId(keyColour));
        }

        /// <summary>A mystery column: the top block is visible, everything below it is a hidden '?'.</summary>
        internal static ColumnData Mystery(int capacity, params int[] cellsBottomUp)
        {
            return new ColumnData(ColumnKind.Mystery, capacity, Cells(cellsBottomUp, cellsBottomUp.Length - 1));
        }

        internal static LevelData Level(params ColumnData[] columns)
        {
            return new LevelData(79, columns);
        }

        /// <summary>
        /// A session on the authored board — <see cref="AttemptScramble.None"/>, so a
        /// fixture written here means on screen exactly what it says on the page.
        /// </summary>
        internal static BoardSession Session(params ColumnData[] columns)
        {
            return new BoardSession(Level(columns), Seed, AttemptScramble.None);
        }

        /// <summary>A session playing one of the level's variants: colours relabelled, columns reordered.</summary>
        internal static BoardSession VariantSession(int variantIndex, params ColumnData[] columns)
        {
            LevelData level = Level(columns);
            return new BoardSession(level, Seed, AttemptScramble.ForVariant(level, variantIndex));
        }

        /// <summary>
        /// The complete mutable state of an attempt as one string: cells, hidden
        /// flags, locks, completed colours, history depth and RNG cursor. Undo is
        /// "exact" precisely when this string comes back unchanged, and a failure
        /// message shows what moved.
        /// </summary>
        internal static string Fingerprint(BoardSession session)
        {
            var text = new StringBuilder();
            text.Append("rng:").Append(session.Random.Cursor);
            text.Append(" history:").Append(session.History.Count);
            text.Append(" dropped:").Append(session.History.DroppedEntryCount);
            text.Append(" completed:").Append(session.State.CompletedColourCount).Append('[');

            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                if (session.State.HasEverCompleted(new BlockColourId(id)))
                {
                    text.Append(id).Append(',');
                }
            }

            text.Append(']');

            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];
                text.Append(" | ").Append(candidate.Kind);
                text.Append(candidate.IsLocked ? "*" : "-");
                text.Append(candidate.Count).Append('/').Append(candidate.Capacity).Append(':');

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    text.Append(candidate.ColourAt(cell).Value);
                    text.Append(candidate.IsHiddenAt(cell) ? "?" : ".");
                }
            }

            return text.ToString();
        }

        private static CellData[] Cells(int[] cellsBottomUp, int visibleIndex)
        {
            var cells = new CellData[cellsBottomUp.Length];
            for (int cell = 0; cell < cells.Length; cell++)
            {
                bool hidden = visibleIndex >= 0 && cell != visibleIndex;
                cells[cell] = Cell(cellsBottomUp[cell], hidden);
            }

            return cells;
        }
    }

    /// <summary>
    /// The move rule, the win condition and the deadlock test — reference §1.
    /// </summary>
    [TestFixture]
    public sealed class BoardRulesTests
    {
        [Test]
        public void TopRunLength_CountsTheContiguousSameColourRun()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 1, 2, 2),
                TestBoards.Normal(4, 2, 1));

            Assert.That(BoardRules.TopRunLength(session.State[0]), Is.EqualTo(2));
            Assert.That(BoardRules.TopRunLength(session.State[1]), Is.EqualTo(1));
        }

        [Test]
        public void TopRunLength_StopsAtAHiddenCell()
        {
            // [4? 4? 1] — the run is the single visible block, because an unreadable
            // block is not part of a run the player can see.
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(3, 4, 4, 1),
                TestBoards.Normal(3, 1));

            Assert.That(BoardRules.TopRunLength(session.State[0]), Is.EqualTo(1));
        }

        [Test]
        public void TopRunLength_IsZeroForAnEmptyColumn()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2));

            Assert.That(BoardRules.TopRunLength(session.State[1]), Is.EqualTo(0));
            Assert.That(session.CanLift(1), Is.False);
        }

        [Test]
        public void MovableCount_TakesTheWholeRunOntoAnEmptyColumn()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 2, 1, 1),
                TestBoards.Normal(4, 1),
                TestBoards.Normal(4));

            Assert.That(session.MovableCount(0, 2), Is.EqualTo(2));
        }

        [Test]
        public void MovableCount_CapsAtTheFreeSpaceInTheDestination()
        {
            // Reference §1: "only as many blocks as still fit".
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 2, 1, 1, 1),
                TestBoards.Normal(3, 1, 1));

            Assert.That(BoardRules.TopRunLength(session.State[0]), Is.EqualTo(3));
            Assert.That(session.MovableCount(0, 1), Is.EqualTo(1));
        }

        [Test]
        public void CanMove_RejectsADifferentTopColour()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(3, 2, 1),
                TestBoards.Normal(3, 1, 2));

            Assert.That(session.CanMove(0, 1), Is.False);
        }

        [Test]
        public void CanMove_RejectsAFullDestinationAndTheSameColumn()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 2, 1),
                TestBoards.Normal(2, 1, 1));

            Assert.That(session.CanMove(0, 1), Is.False, "the destination is full");
            Assert.That(session.CanMove(0, 0), Is.False, "a column is not its own destination");
        }

        [Test]
        public void CanMove_RejectsALockedColumnInEitherDirection()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Ice(2, 1),
                TestBoards.Covered(2, 1, 2, 3));

            Assert.That(session.State[2].IsLocked, Is.True);
            Assert.That(session.State[3].IsLocked, Is.True);
            Assert.That(session.CanMove(0, 2), Is.False, "ice takes no blocks before it thaws");
            Assert.That(session.CanMove(0, 3), Is.False, "a cover takes no blocks before it opens");
            Assert.That(session.CanMove(3, 0), Is.False, "nothing comes out of a closed cover");
        }

        [Test]
        public void TryMove_MovesTheRunAndRecordsExactlyOneHistoryEntry()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 2, 1, 1),
                TestBoards.Normal(4, 1),
                TestBoards.Normal(4));

            BoardMove move;
            Assert.That(session.TryMove(0, 2, out move), Is.True);

            Assert.That(move.Count, Is.EqualTo(2));
            Assert.That(move.Colour, Is.EqualTo(new BlockColourId(1)));
            Assert.That(session.State[0].Count, Is.EqualTo(1));
            Assert.That(session.State[2].Count, Is.EqualTo(2));
            Assert.That(session.History.Count, Is.EqualTo(1));
            Assert.That(session.History.Last, Is.SameAs(move));
        }

        [Test]
        public void TryMove_LeavesTheBoardUntouchedWhenTheMoveIsIllegal()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(3, 2, 1),
                TestBoards.Normal(3, 1, 2));

            string before = TestBoards.Fingerprint(session);

            Assert.That(session.TryMove(0, 1), Is.False);
            Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(before));
            Assert.That(session.History.Count, Is.EqualTo(0));
        }

        [Test]
        public void IsWin_IsTrueOnceEveryColourIsGatheredInOneColumn()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1),
                TestBoards.Normal(2));

            Assert.That(session.IsWon, Is.False);

            Assert.That(session.TryMove(0, 2), Is.True, "the 2 goes to the empty column");
            Assert.That(session.TryMove(1, 0), Is.True, "the 1 joins the 1");
            Assert.That(session.IsWon, Is.False, "the 2s are still split");
            Assert.That(session.TryMove(1, 2), Is.True, "the last 2 joins its own");

            Assert.That(session.IsWon, Is.True);
            Assert.That(session.IsDeadlocked, Is.False, "a won board is never reported as a loss");
        }

        [Test]
        public void IsWin_IsFalseWhileAnyColumnHoldsAMixture()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1));

            Assert.That(BoardRules.IsColumnSolved(session.State, 0), Is.False);
            Assert.That(session.IsWon, Is.False);
        }

        [Test]
        public void IsDeadlock_IsTrueWhenNoLegalMoveRemains()
        {
            // Two full columns, mismatched tops, nowhere to put anything.
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1));

            Assert.That(BoardRules.HasAnyLegalMove(session.State), Is.False);
            Assert.That(session.IsDeadlocked, Is.True);
        }

        [Test]
        public void IsDeadlock_IsFalseWhileAnEmptyPlayableColumnExists()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1),
                TestBoards.Normal(2));

            Assert.That(session.IsDeadlocked, Is.False);
        }

        [Test]
        public void IsDeadlock_IgnoresLockedColumnsBecauseTheyCannotUnlockWithoutAMove()
        {
            // The ice would thaw on a completed colour, but completing a colour needs
            // a move, and there is none — so this really is a loss.
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1),
                TestBoards.Ice(2, 1));

            Assert.That(session.IsDeadlocked, Is.True);
        }

        [Test]
        public void Events_ReportTheCommittedMoveAndTheWin()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(3, 2, 1, 1),
                TestBoards.Normal(3, 1));

            int applied = 0;
            int wins = 0;
            session.MoveApplied += _ => applied++;
            session.Won += () => wins++;

            Assert.That(session.TryMove(0, 1), Is.True);

            Assert.That(applied, Is.EqualTo(1));
            Assert.That(wins, Is.EqualTo(1), "the move gathers both colours at once");
        }
    }
}
