using System;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// A solver is the one tool whose wrong answer is invisible: a solvable level called
    /// impossible sends someone rewriting good content, and the reverse ships a level nobody
    /// can finish. So the three verdicts are pinned separately, the returned solution is
    /// replayed to prove it actually wins, and a board that is only impossible *because* a
    /// column is locked is checked against the same board with the lock removed.
    /// </summary>
    [TestFixture]
    public sealed class BoardSolverTests
    {
        [Test]
        public void Solve_OnAOneMoveWin_ReturnsThatMove()
        {
            // Two lonely blocks of the same colour: put them together and the board is done.
            LevelData level = TestBoards.Level(TestBoards.Normal(2, 1), TestBoards.Normal(2, 1));

            SolverResult result = BoardSolver.Solve(level);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.Solvable));
            Assert.That(result.Solution.Count, Is.EqualTo(1));
        }

        [Test]
        public void Solve_ReturnsASolutionThatActuallyWins()
        {
            // The claim is only worth as much as the moves behind it, so they are replayed on
            // a fresh board through the same rules the game uses.
            LevelData level = TestBoards.Level(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1),
                TestBoards.Normal(2));

            SolverResult result = BoardSolver.Solve(level);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.Solvable));

            var replay = new BoardSession(level, TestBoards.Seed, AttemptScramble.None);

            for (int move = 0; move < result.Solution.Count; move++)
            {
                Assert.That(
                    replay.TryMove(result.Solution[move].From, result.Solution[move].To),
                    Is.True,
                    "move " + move + " of the solution was refused by the rules");
            }

            Assert.That(replay.IsWon, Is.True, "the solution has to end on a won board");
        }

        [Test]
        public void Solve_WhenNoLegalMoveExists_IsUnsolvable()
        {
            // Two full columns with different tops: nothing can move anywhere, ever.
            LevelData level = TestBoards.Level(TestBoards.Normal(2, 1, 2), TestBoards.Normal(2, 2, 1));

            SolverResult result = BoardSolver.Solve(level);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.Unsolvable));
            Assert.That(result.StatesExplored, Is.EqualTo(0));
            Assert.That(result.Solution, Is.Empty);
        }

        [Test]
        public void Solve_WhenAColourCannotFitInOneColumn_IsUnsolvableAfterSearching()
        {
            // Three blocks of colour 1 and no column taller than two: there are plenty of legal
            // moves and not one of them can ever gather the colour, which only a search knows.
            LevelData level = TestBoards.Level(
                TestBoards.Normal(2, 1, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2));

            SolverResult result = BoardSolver.Solve(level);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.Unsolvable));
            Assert.That(result.StatesExplored, Is.GreaterThan(0), "this board has moves; the verdict has to come from exploring them");
        }

        [Test]
        public void Solve_TreatsALockedIceColumnAsUnusable()
        {
            // The same board twice. With a free column it solves in three moves; with that
            // column encased in ice — which nothing here can ever thaw, since no colour can be
            // completed first — there is no move at all.
            ColumnData first = TestBoards.Normal(2, 1, 2);
            ColumnData second = TestBoards.Normal(2, 2, 1);

            SolverResult free = BoardSolver.Solve(TestBoards.Level(first, second, TestBoards.Normal(2)));
            SolverResult iced = BoardSolver.Solve(TestBoards.Level(first, second, TestBoards.Ice(2, 1)));

            Assert.That(free.Verdict, Is.EqualTo(SolverVerdict.Solvable));
            Assert.That(iced.Verdict, Is.EqualTo(SolverVerdict.Unsolvable), "a locked column is not a buffer");
        }

        [Test]
        public void Solve_WhenTheBudgetRunsOut_IsNotProvenRatherThanUnsolvable()
        {
            // The same impossible board as above, searched with almost no budget: the honest
            // answer is "I do not know", never "impossible".
            LevelData level = TestBoards.Level(
                TestBoards.Normal(2, 1, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2));

            SolverResult result = BoardSolver.Solve(level, 1);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.NotProven));
            Assert.That(result.Solution, Is.Empty);
        }

        [Test]
        public void Solve_WithTwoInterchangeableEmptyColumns_StillFindsTheSolution()
        {
            // The search skips all but the first of a set of identical empty targets, because
            // the boards that follow differ only by a relabelling. This is the board that would
            // expose that reduction if it ever pruned something real.
            LevelData level = TestBoards.Level(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 1),
                TestBoards.Normal(2),
                TestBoards.Normal(2));

            SolverResult result = BoardSolver.Solve(level);

            Assert.That(result.Verdict, Is.EqualTo(SolverVerdict.Solvable));

            var replay = new BoardSession(level, TestBoards.Seed, AttemptScramble.None);

            for (int move = 0; move < result.Solution.Count; move++)
            {
                Assert.That(replay.TryMove(result.Solution[move].From, result.Solution[move].To), Is.True);
            }

            Assert.That(replay.IsWon, Is.True);
        }

        [Test]
        public void Solve_RefusesArgumentsThatCannotBeSearched()
        {
            Assert.That(() => BoardSolver.Solve(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => BoardSolver.Solve(TestBoards.Level(TestBoards.Normal(2, 1), TestBoards.Normal(2, 1)), 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void MaxDepth_IsTheDepthUndoCanWalkBack()
        {
            // Not a round number someone liked: one move deeper and the history would drop the
            // entry the search needs to undo, and it would explore from a board it cannot restore.
            Assert.That(BoardSolver.MaxDepth, Is.EqualTo(BoardMoveHistory.MaxEntries));
        }
    }
}
