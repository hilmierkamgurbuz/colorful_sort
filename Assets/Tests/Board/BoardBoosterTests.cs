using System.Collections.Generic;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// The two boosters that change the rules. Both are new shapes for
    /// <see cref="BoardMove"/> — one adds a column that was not there, the other rewrites
    /// every visible cell at once — and undo is the invariant this whole project is built
    /// around, so every case here ends by comparing the attempt's complete fingerprint
    /// against the one it started with. A shuffle whose undo is off would restore a board
    /// that looks right and is not, and nothing on screen would ever say so.
    /// </summary>
    public sealed class BoardBoosterTests
    {
        // A board that is genuinely stuck: three full columns, no two tops alike, nowhere
        // to put anything. Adding a column is the only thing that can rescue it.
        private static BoardSession Deadlocked()
        {
            return TestBoards.Session(
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2, 2, 3),
                TestBoards.Normal(2, 3, 1));
        }

        private static List<BlockColourId> AllColours(BoardSession session)
        {
            var colours = new List<BlockColourId>();

            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    colours.Add(candidate.ColourAt(cell));
                }
            }

            colours.Sort((left, right) => left.Value.CompareTo(right.Value));
            return colours;
        }

        // ------------------------------------------------------------ add column

        [Test]
        public void AddedColumnIsEmptyNormalAndAsTallAsTheBoard()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 1, 2),
                TestBoards.Normal(3, 2));

            Assert.IsTrue(session.TryAddColumn());

            Assert.AreEqual(3, session.State.ColumnCount);

            BoardColumn added = session.State[2];
            Assert.AreEqual(ColumnKind.Normal, added.Kind);
            Assert.AreEqual(4, added.Capacity, "The booster takes the largest capacity on the board, never a number of its own.");
            Assert.IsTrue(added.IsEmpty);
            Assert.IsFalse(added.IsLocked);
        }

        [Test]
        public void AddingAColumnRescuesADeadlockedBoard()
        {
            BoardSession session = Deadlocked();
            Assert.IsTrue(session.IsDeadlocked, "The fixture is meant to be stuck.");

            Assert.IsTrue(session.TryAddColumn());

            Assert.IsFalse(session.IsDeadlocked, "That is what the booster is for.");
        }

        [Test]
        public void TheBoardStopsAtTheColumnCeiling()
        {
            var columns = new List<ColumnData>();
            for (int column = 0; column < LevelData.MaxColumns; column++)
            {
                columns.Add(TestBoards.Normal(2, 1));
            }

            BoardSession session = TestBoards.Session(columns.ToArray());

            Assert.IsFalse(session.CanAddColumn);
            Assert.IsFalse(session.TryAddColumn(), "Refused rather than clamped, so a button can be greyed out.");
            Assert.AreEqual(LevelData.MaxColumns, session.State.ColumnCount);
        }

        [Test]
        public void UndoingAnAddedColumnRestoresTheBoardExactly()
        {
            BoardSession session = Deadlocked();
            string before = TestBoards.Fingerprint(session);

            session.TryAddColumn();
            Assert.IsTrue(session.Undo());

            Assert.AreEqual(before, TestBoards.Fingerprint(session));
        }

        [Test]
        public void AColumnAddedThenFilledIsStillUndoneInOrder()
        {
            BoardSession session = Deadlocked();
            string before = TestBoards.Fingerprint(session);

            session.TryAddColumn();
            Assert.IsTrue(session.TryMove(0, 3), "The rescued board has a legal move again.");

            Assert.IsTrue(session.Undo());
            Assert.IsTrue(session.Undo());

            Assert.AreEqual(before, TestBoards.Fingerprint(session),
                "Undo is last-in-first-out, so the column is empty again by the time it is removed.");
        }

        // ------------------------------------------------------------ shuffle

        [Test]
        public void ShuffleKeepsEveryColourAndEveryColumnsFillCount()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 2, 3, 1),
                TestBoards.Normal(4, 2, 3),
                TestBoards.Normal(4));

            List<BlockColourId> before = AllColours(session);
            int[] counts = { session.State[0].Count, session.State[1].Count, session.State[2].Count };

            Assert.IsTrue(session.TryShuffle());

            CollectionAssert.AreEqual(before, AllColours(session),
                "A shuffle rearranges what is on the board; it does not invent or destroy a block.");
            Assert.AreEqual(counts[0], session.State[0].Count);
            Assert.AreEqual(counts[1], session.State[1].Count);
            Assert.AreEqual(counts[2], session.State[2].Count, "An empty column stays empty: this rearranges, it does not redistribute.");
        }

        [Test]
        public void ShuffleConsumesTheAttemptRngAndUndoRewindsIt()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 2, 3, 1),
                TestBoards.Normal(4, 2, 3));

            int cursorBefore = session.Random.Cursor;

            Assert.IsTrue(session.TryShuffle());
            Assert.Greater(session.Random.Cursor, cursorBefore, "Shuffle is the RNG's only consumer (D-002).");

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(cursorBefore, session.Random.Cursor);
        }

        [Test]
        public void UndoingAShuffleRestoresTheBoardExactly()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 2, 3, 1),
                TestBoards.Normal(4, 2, 3, 2),
                TestBoards.Normal(4, 3, 1));

            string before = TestBoards.Fingerprint(session);

            Assert.IsTrue(session.TryShuffle());
            Assert.IsTrue(session.Undo());

            Assert.AreEqual(before, TestBoards.Fingerprint(session));
        }

        [Test]
        public void RepeatedShufflesUndoBackToTheStart()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 1, 2, 3, 1),
                TestBoards.Normal(4, 2, 3, 2));

            string before = TestBoards.Fingerprint(session);

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(session.TryShuffle());
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(session.Undo());
            }

            Assert.AreEqual(before, TestBoards.Fingerprint(session),
                "Five shuffles deep, the RNG cursor and every cell still come back.");
        }

        [Test]
        public void ShuffleLeavesHiddenCellsAlone()
        {
            // A mystery column: its lower cells stay unreadable, so what sits under the '?'
            // must be exactly what the level authored (D-011).
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(4, 1, 2, 3),
                TestBoards.Normal(4, 2, 3, 1));

            BoardColumn mystery = session.State[0];
            var hiddenBefore = new List<BlockColourId>();

            for (int cell = 0; cell < mystery.Count; cell++)
            {
                if (mystery.IsHiddenAt(cell))
                {
                    hiddenBefore.Add(mystery.ColourAt(cell));
                }
            }

            Assert.Greater(hiddenBefore.Count, 0, "The fixture is meant to have hidden cells.");

            Assert.IsTrue(session.TryShuffle());

            var hiddenAfter = new List<BlockColourId>();
            for (int cell = 0; cell < mystery.Count; cell++)
            {
                if (mystery.IsHiddenAt(cell))
                {
                    hiddenAfter.Add(mystery.ColourAt(cell));
                }
            }

            CollectionAssert.AreEqual(hiddenBefore, hiddenAfter,
                "The solvability verdict was computed with these contents; a shuffle may not change them.");
        }

        [Test]
        public void ShuffleLeavesLockedColumnsAlone()
        {
            // A covered column is locked until its key colour completes; nothing enters or
            // leaves it, and that includes a rearrangement.
            BoardSession session = TestBoards.Session(
                TestBoards.Covered(4, 1, 2, 3),
                TestBoards.Normal(4, 1, 2, 3, 1),
                TestBoards.Normal(4, 2));

            BoardColumn covered = session.State[0];
            Assert.IsTrue(covered.IsLocked, "The fixture is meant to start locked.");

            var before = new List<BlockColourId>();
            for (int cell = 0; cell < covered.Count; cell++)
            {
                before.Add(covered.ColourAt(cell));
            }

            Assert.IsTrue(session.TryShuffle());

            var after = new List<BlockColourId>();
            for (int cell = 0; cell < covered.Count; cell++)
            {
                after.Add(covered.ColourAt(cell));
            }

            CollectionAssert.AreEqual(before, after);
        }

        [Test]
        public void ShuffleIsRefusedWhenThereIsNothingToRearrange()
        {
            // One readable cell and one hidden one. A lone block in a column of its own
            // would be a gathered colour, which BoardState.FromLevel refuses outright
            // (D-013), so the fixture hides the second block instead.
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(4, 1, 2),
                TestBoards.Normal(4));

            Assert.IsFalse(session.CanShuffle);
            Assert.IsFalse(session.TryShuffle(), "One block cannot be rearranged, and an empty history entry still costs one of the 256.");
            Assert.AreEqual(0, session.History.Count);
        }

        [Test]
        public void ShuffleRecordsTheCellsItTouchedAndNothingElse()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(4, 1, 2, 3),
                TestBoards.Normal(4, 2, 3, 1));

            Assert.IsTrue(session.TryShuffle(out BoardMove move));

            Assert.AreEqual(MoveKind.Shuffle, move.Kind);
            Assert.AreEqual(BoardMove.NoColumn, move.FromColumn, "A shuffle moves no run, so the run fields stay at their sentinel.");

            int movable = 0;
            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];

                if (candidate.IsLocked)
                {
                    continue;
                }

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    if (!candidate.IsHiddenAt(cell))
                    {
                        movable++;
                    }
                }
            }

            Assert.AreEqual(movable, move.ShuffledCells.Count);
        }
    }
}
