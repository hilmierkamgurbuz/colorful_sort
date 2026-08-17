using System;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// Undo is the reason this assembly is engine-free: it has to be provably exact
    /// (D-001), which means the board, the hidden flags, the locks, the completed
    /// colours <em>and</em> the attempt RNG all have to come back. The fingerprint
    /// helper covers all five, so a single string comparison is the proof.
    /// </summary>
    [TestFixture]
    public sealed class BoardUndoTests
    {
        /// <summary>
        /// One board carrying all four column kinds, so a single scripted game
        /// exercises a reveal, two completions, a cover opening and an ice thaw.
        /// </summary>
        private static BoardSession RichSession()
        {
            return TestBoards.Session(
                TestBoards.Mystery(3, 4, 4, 1),
                TestBoards.Normal(3, 1),
                TestBoards.Covered(2, 1, 2, 3),
                TestBoards.Ice(2, 1),
                TestBoards.Normal(2, 5, 1));
        }

        [Test]
        public void Undo_ReturnsFalseWhenThereIsNothingToUndo()
        {
            BoardSession session = RichSession();

            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void Undo_RestoresASimpleMoveExactly()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 2, 1, 1),
                TestBoards.Normal(4, 1),
                TestBoards.Normal(4));

            string before = TestBoards.Fingerprint(session);

            Assert.That(session.TryMove(0, 2), Is.True);
            Assert.That(TestBoards.Fingerprint(session), Is.Not.EqualTo(before));

            Assert.That(session.Undo(), Is.True);
            Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(before));
        }

        [Test]
        public void TryMove_RevealingAMysteryCell_IsUndoneBackToHidden()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(3, 4, 4, 1),
                TestBoards.Normal(3, 1),
                TestBoards.Normal(2, 5, 1));

            string before = TestBoards.Fingerprint(session);

            BoardMove move;
            Assert.That(session.TryMove(0, 1, out move), Is.True);

            Assert.That(move.RevealedCells.Count, Is.EqualTo(1));
            Assert.That(move.RevealedCells[0], Is.EqualTo(new CellRef(0, 1)));
            Assert.That(session.State[0].IsTopHidden, Is.False);
            Assert.That(session.State[0].ColourAt(1), Is.EqualTo(new BlockColourId(4)));
            Assert.That(move.CompletedColours.Count, Is.EqualTo(0), "a colour with a hidden block is not finished");

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State[0].IsHiddenAt(1), Is.True);
            Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(before));
        }

        [Test]
        public void Undo_RestoresACompletionThatOpenedACoverAndThawedIce()
        {
            BoardSession session = RichSession();

            Assert.That(session.TryMove(0, 1), Is.True, "the mystery column's top 1 joins column 1");
            string beforeCompletion = TestBoards.Fingerprint(session);

            BoardMove move;
            Assert.That(session.TryMove(4, 1, out move), Is.True, "the last 1 lands and finishes the colour");

            Assert.That(move.CompletedColours, Contains.Item(new BlockColourId(1)));
            Assert.That(move.CompletedColours, Contains.Item(new BlockColourId(5)),
                "the block left alone in column 4 finishes its colour in the same move");
            Assert.That(move.UncoveredColumns, Is.EqualTo(new[] { 2 }));
            Assert.That(move.ThawedColumns, Is.EqualTo(new[] { 3 }));
            Assert.That(move.RevealedCells.Count, Is.EqualTo(2), "both cells under the cover became readable");
            Assert.That(session.State[2].IsLocked, Is.False);
            Assert.That(session.State[3].IsLocked, Is.False);
            Assert.That(session.State.CompletedColourCount, Is.EqualTo(2));

            Assert.That(session.Undo(), Is.True);

            Assert.That(session.State[2].IsLocked, Is.True, "the cover closes again");
            Assert.That(session.State[3].IsLocked, Is.True, "the ice refreezes");
            Assert.That(session.State[2].IsHiddenAt(0), Is.True);
            Assert.That(session.State.CompletedColourCount, Is.EqualTo(0));
            Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(beforeCompletion));
        }

        [Test]
        public void Undo_UnwindsAWholeGameStepByStep()
        {
            BoardSession session = RichSession();

            var fingerprints = new string[4];
            fingerprints[0] = TestBoards.Fingerprint(session);

            Assert.That(session.TryMove(0, 1), Is.True);
            fingerprints[1] = TestBoards.Fingerprint(session);

            Assert.That(session.TryMove(4, 1), Is.True);
            fingerprints[2] = TestBoards.Fingerprint(session);

            Assert.That(session.TryMove(2, 3), Is.True, "the opened cover feeds the thawed ice column");
            fingerprints[3] = TestBoards.Fingerprint(session);

            Assert.That(session.History.Count, Is.EqualTo(3));

            for (int step = 3; step >= 1; step--)
            {
                Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(fingerprints[step]));
                Assert.That(session.Undo(), Is.True);
            }

            Assert.That(TestBoards.Fingerprint(session), Is.EqualTo(fingerprints[0]));
            Assert.That(session.History.Count, Is.EqualTo(0));
            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void History_DropsTheOldestEntryOnceItIsFull()
        {
            // A board that can be played back and forth forever: the two 1s travel
            // between column 0 and the empty column 2, and never finish their colour
            // because a third 1 sits at the bottom of column 1.
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1, 1),
                TestBoards.Normal(2, 1, 2),
                TestBoards.Normal(2));

            for (int move = 0; move < BoardMoveHistory.MaxEntries + 1; move++)
            {
                bool forward = move % 2 == 0;
                Assert.That(session.TryMove(forward ? 0 : 2, forward ? 2 : 0), Is.True, "move " + move);
            }

            Assert.That(session.History.Count, Is.EqualTo(BoardMoveHistory.MaxEntries));
            Assert.That(session.History.DroppedEntryCount, Is.EqualTo(1),
                "one move fell off the front and can never be undone");

            for (int undone = 0; undone < BoardMoveHistory.MaxEntries; undone++)
            {
                Assert.That(session.Undo(), Is.True, "undo " + undone);
            }

            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void DeterministicRandom_ReplaysTheSameSequenceForTheSameSeed()
        {
            var first = new DeterministicRandom(7);
            var second = new DeterministicRandom(7);

            for (int draw = 0; draw < 8; draw++)
            {
                Assert.That(second.NextInt(16), Is.EqualTo(first.NextInt(16)), "draw " + draw);
            }

            Assert.That(first.Cursor, Is.EqualTo(8));
        }

        [Test]
        public void DeterministicRandom_RewindReproducesTheDrawsAfterIt()
        {
            var random = new DeterministicRandom(7);
            random.NextInt(6);

            int cursor = random.Cursor;
            int drawn = random.NextInt(6);

            random.Rewind(cursor);

            Assert.That(random.Cursor, Is.EqualTo(cursor));
            Assert.That(random.NextInt(6), Is.EqualTo(drawn));
        }

        [Test]
        public void DeterministicRandom_RefusesToRewindForward()
        {
            var random = new DeterministicRandom(7);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.Rewind(1));
        }

        [Test]
        public void Session_RecordsTheRngCursorWithEveryMove()
        {
            // Nothing in this task's rules draws from the RNG — the reveal reads an
            // authored colour, and Shuffle is the Boosters task — but every move
            // records the cursor, so undo already rewinds correctly the day it does.
            BoardSession session = RichSession();

            Assert.That(session.TryMove(0, 1), Is.True);

            Assert.That(session.History.Last.RngCursorBefore, Is.EqualTo(0));
            Assert.That(session.Random.Cursor, Is.EqualTo(0));
            Assert.That(session.Seed, Is.EqualTo(TestBoards.Seed));
        }

        [Test]
        public void Session_RefusesACommandIssuedFromInsideABoardEvent()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(4, 2, 1, 1),
                TestBoards.Normal(4, 1),
                TestBoards.Normal(4));

            session.MoveApplied += _ => Assert.Throws<InvalidOperationException>(() => session.Undo());

            Assert.That(session.TryMove(0, 2), Is.True);
        }
    }
}
