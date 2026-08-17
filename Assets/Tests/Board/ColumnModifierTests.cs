using System;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// The three column modifiers (reference §2) and the data validation that keeps a
    /// broken level from ever reaching a board: ice thaws per completed colour, a
    /// cover's symbol is a key, and a mystery cell reveals itself when it surfaces.
    /// </summary>
    [TestFixture]
    public sealed class ColumnModifierTests
    {
        [Test]
        public void StartsLocked_CoversIceAndCoversOnly()
        {
            Assert.That(ColumnModifiers.StartsLocked(ColumnKind.Normal), Is.False);
            Assert.That(ColumnModifiers.StartsLocked(ColumnKind.Mystery), Is.False);
            Assert.That(ColumnModifiers.StartsLocked(ColumnKind.Ice), Is.True);
            Assert.That(ColumnModifiers.StartsLocked(ColumnKind.Covered), Is.True);
        }

        [Test]
        public void Ice_ThawsOnTheFirstCompletedColour()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Ice(2, 1));

            Assert.That(session.State[2].IsLocked, Is.True);
            Assert.That(ColumnModifiers.IsPlayable(session.State[2]), Is.False);

            BoardMove move;
            Assert.That(session.TryMove(0, 1, out move), Is.True);

            Assert.That(move.CompletedColours, Contains.Item(new BlockColourId(1)));
            Assert.That(move.ThawedColumns, Is.EqualTo(new[] { 2 }));
            Assert.That(session.State[2].IsLocked, Is.False);
            Assert.That(session.State[2].Kind, Is.EqualTo(ColumnKind.Ice), "a thawed column keeps its kind for the view");
        }

        [Test]
        public void Ice_WaitsForItsOwnCompletionCount()
        {
            // Level 79 shows three ice columns thawing at the 1st, 2nd and 3rd
            // completion; this is the N = 2 column.
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 2),
                TestBoards.Normal(2, 2),
                TestBoards.Ice(2, 2));

            Assert.That(session.TryMove(0, 1), Is.True);
            Assert.That(session.State.CompletedColourCount, Is.EqualTo(1));
            Assert.That(session.State[4].IsLocked, Is.True, "one completion is not enough for an N=2 column");

            Assert.That(session.TryMove(2, 3), Is.True);
            Assert.That(session.State.CompletedColourCount, Is.EqualTo(2));
            Assert.That(session.State[4].IsLocked, Is.False);
        }

        [Test]
        public void Ice_CreditsBothColoursWhenOneMoveFinishesTwo()
        {
            // The run lands and finishes its colour, while the block left behind in
            // the source column turns out to be a finished colour too.
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(3, 2, 1, 1),
                TestBoards.Normal(3, 1),
                TestBoards.Ice(2, 2));

            BoardMove move;
            Assert.That(session.TryMove(0, 1, out move), Is.True);

            Assert.That(move.CompletedColours.Count, Is.EqualTo(2));
            Assert.That(session.State.CompletedColourCount, Is.EqualTo(2));
            Assert.That(session.State[2].IsLocked, Is.False, "two completions in one move thaw an N=2 column");
        }

        [Test]
        public void Cover_OpensEveryCoverCarryingTheCompletedKey()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Covered(2, 1, 2, 3),
                TestBoards.Covered(2, 1, 3, 2));

            int unlocked = 0;
            session.ColumnUnlocked += _ => unlocked++;

            BoardMove move;
            Assert.That(session.TryMove(0, 1, out move), Is.True);

            Assert.That(move.UncoveredColumns, Is.EqualTo(new[] { 2, 3 }), "one key opens every cover carrying it");
            Assert.That(move.RevealedCells.Count, Is.EqualTo(4));
            Assert.That(unlocked, Is.EqualTo(2));
            Assert.That(session.State[2].IsHiddenAt(0), Is.False);
            Assert.That(session.State[3].IsHiddenAt(1), Is.False);
            Assert.That(session.CanMove(2, 3), Is.False, "the opened columns are full, so still nothing fits");
            Assert.That(session.CanLift(2), Is.True, "but an opened column can now be lifted from");
        }

        [Test]
        public void Cover_IgnoresACompletionThatIsNotItsKey()
        {
            BoardSession session = TestBoards.Session(
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Covered(2, 2, 3, 4),
                TestBoards.Normal(2, 2, 3));

            Assert.That(session.TryMove(0, 1), Is.True, "colour 1 is finished");

            Assert.That(session.State[2].IsLocked, Is.True, "the cover is keyed to colour 2, not colour 1");
            Assert.That(session.State[2].IsHiddenAt(0), Is.True);
        }

        [Test]
        public void Mystery_RevealsTheCellThatBecomesTheTop()
        {
            Assert.That(ColumnModifiers.RevealsTopWhenExposed(ColumnKind.Mystery), Is.True);
            Assert.That(ColumnModifiers.RevealsTopWhenExposed(ColumnKind.Normal), Is.False);

            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(3, 4, 4, 1),
                TestBoards.Normal(3, 1),
                TestBoards.Normal(2, 5, 1));

            CellRef revealed = default;
            int reveals = 0;
            session.CellRevealed += cell =>
            {
                revealed = cell;
                reveals++;
            };

            Assert.That(session.State[0].IsTopHidden, Is.False, "the top of a mystery column starts readable");
            Assert.That(session.TryMove(0, 1), Is.True);

            Assert.That(reveals, Is.EqualTo(1));
            Assert.That(revealed, Is.EqualTo(new CellRef(0, 1)));
            Assert.That(session.State[0].IsHiddenAt(1), Is.False);
            Assert.That(session.State[0].IsHiddenAt(0), Is.True, "only the new top reveals, not the whole column");
        }

        [Test]
        public void HiddenBlock_KeepsItsColourIncompleteAndTheBoardUnwon()
        {
            // [4? 4] — every 4 on the board is in this column and nothing else is,
            // but the player cannot see that yet, so the colour is not finished.
            BoardSession session = TestBoards.Session(
                TestBoards.Mystery(2, 4, 4),
                TestBoards.Normal(2, 1, 2));

            Assert.That(BoardRules.IsColourComplete(session.State, new BlockColourId(4)), Is.False);
            Assert.That(session.IsWon, Is.False);
        }

        [Test]
        public void ColumnData_RefusesShapesTheModifiersCannotRender()
        {
            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Ice, 2, new[] { TestBoards.Cell(1) }, 1),
                "an ice column is drawn empty");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Ice, 2, new CellData[0]),
                "an ice column that never thaws is a dead column");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Covered, 2, new[] { TestBoards.Cell(1, true), TestBoards.Cell(2) }, 0, new BlockColourId(1)),
                "a cover hides every cell under it");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Covered, 2, new[] { TestBoards.Cell(1, true) }),
                "a cover without a key colour can never open");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Mystery, 2, new[] { TestBoards.Cell(1, true), TestBoards.Cell(2, true) }),
                "the top of a mystery column is visible");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Normal, 2, new[] { TestBoards.Cell(1, true) }),
                "only covered and mystery columns hide cells");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Normal, 2, new[] { TestBoards.Cell(1), TestBoards.Cell(2), TestBoards.Cell(3) }),
                "more blocks than the column can hold");

            Assert.Catch<ArgumentException>(
                () => new ColumnData(ColumnKind.Normal, ColumnData.MaxCapacity + 1, new CellData[0]),
                "capacity is bounded by the fingerprint");
        }

        [Test]
        public void LevelData_RefusesALevelThatCannotBePlayed()
        {
            Assert.Catch<ArgumentException>(
                () => new LevelData(1, new[] { TestBoards.Normal(2, 1, 2) }),
                "a single column is not a puzzle");

            Assert.Catch<ArgumentException>(
                () => new LevelData(1, new[] { TestBoards.Normal(2, 1, 2), TestBoards.Covered(2, 7, 3, 4) }),
                "a cover keyed to a colour that is nowhere on the board can never open");

            Assert.Catch<ArgumentException>(
                () => new LevelData(1, new[] { TestBoards.Ice(2, 1), TestBoards.Covered(2, 1, 1, 2) }),
                "every block starts locked away, so there is no first move");
        }

        [Test]
        public void BoardState_RefusesALevelThatStartsPartlySolved()
        {
            LevelData level = TestBoards.Level(
                TestBoards.Normal(2, 1, 1),
                TestBoards.Normal(2, 2, 3));

            Assert.Catch<ArgumentException>(() => BoardState.FromLevel(level),
                "colour 1 is already gathered, so the level would begin partly solved");
        }

        [Test]
        public void BlockColourId_RefusesAnIdOutsideTheSkinRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockColourId(BlockColourId.MaxId + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockColourId(-1));
            Assert.That(new BlockColourId(0).IsNone, Is.True);
        }
    }
}
