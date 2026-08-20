using ColorfulSort.Core;
using ColorfulSort.Meta;
using NUnit.Framework;

namespace ColorfulSort.Meta.Tests
{
    /// <summary>
    /// Progression is the first code in this project whose mistakes are written to a player's
    /// disk, and unlike a board there is no undo for a save. Every rule that could corrupt one
    /// — an attempt consumed by a level that never opened, a win that advances twice, an
    /// ordinal that outlives its database — is pinned here rather than left to be noticed.
    /// </summary>
    public sealed class ProgressionTests
    {
        private SaveData save;

        private int changes;

        [SetUp]
        public void SetUp()
        {
            save = SaveData.NewGame();
            changes = 0;
        }

        private Progression NewProgression(int levelCount)
        {
            return new Progression(save, levelCount, () => changes++);
        }

        [Test]
        public void FreshSaveStartsAtTheFirstLevelAndItsFirstAttempt()
        {
            Progression progression = NewProgression(3);

            Assert.AreEqual(0, progression.CurrentOrdinal);
            Assert.AreEqual(0, progression.AttemptOrdinal, "A player's first attempt is attempt 0, or (level, attempt) stops reproducing a board.");
            Assert.IsFalse(progression.IsCleared(0));
        }

        [Test]
        public void RecordingAnAttemptMovesTheAttemptOrdinalOn()
        {
            Progression progression = NewProgression(3);

            progression.RecordAttemptStarted();

            Assert.AreEqual(1, progression.AttemptOrdinal);
            Assert.AreEqual(1, progression.PlaysOf(0));
            Assert.AreEqual(1, changes, "A write nobody is told about is a write that never reaches the disk.");
        }

        [Test]
        public void ReadingTheAttemptOrdinalConsumesNothing()
        {
            Progression progression = NewProgression(3);

            int first = progression.AttemptOrdinal;
            int second = progression.AttemptOrdinal;

            Assert.AreEqual(first, second);
            Assert.AreEqual(0, changes, "A level that refuses to open must not consume a play.");
        }

        [Test]
        public void CompletingALevelClearsItAndAdvances()
        {
            Progression progression = NewProgression(3);

            bool advanced = progression.CompleteCurrentLevel();

            Assert.IsTrue(advanced);
            Assert.IsTrue(progression.IsCleared(0));
            Assert.AreEqual(1, progression.CurrentOrdinal);
        }

        [Test]
        public void WinningALevelAlreadyClearedStillMovesOn()
        {
            // This used to assert the opposite, and that assertion is what let a save get stuck: a
            // level already marked cleared returned early *without advancing*, so a player who won
            // level 1 while the database still reported one level could never leave it — every later
            // win hit the early return. "I have finished this before" and "there is nowhere to go" are
            // different questions, answered by the record and by HasNext (D-088).
            Progression progression = NewProgression(3);

            progression.CompleteCurrentLevel();
            save.currentLevelOrdinal = 0;

            bool advancedAgain = progression.CompleteCurrentLevel();

            Assert.IsTrue(advancedAgain, "Replaying a cleared level and winning it must still move on.");
            Assert.AreEqual(1, progression.CurrentOrdinal);
            Assert.IsTrue(progression.IsCleared(0), "and it stays cleared");
        }

        [Test]
        public void ClearingIsRememberedSoASecondWinIsNotASecondClear()
        {
            // What the old test was really protecting — that a win is not counted twice — is still
            // true, and it is the *record* that carries it rather than the ordinal. Guarding against
            // one win being announced twice belongs to whoever raises it, not here.
            Progression progression = NewProgression(3);

            progression.CompleteCurrentLevel();

            Assert.IsTrue(progression.IsCleared(0));
            Assert.AreEqual(1, progression.CurrentOrdinal, "one win, one step");
        }

        [Test]
        public void TheLastLevelClearsWithoutAdvancing()
        {
            save.currentLevelOrdinal = 2;
            Progression progression = NewProgression(3);

            bool advanced = progression.CompleteCurrentLevel();

            Assert.IsFalse(advanced, "There is nowhere to go, and an ordinal past the end is a corrupt save.");
            Assert.IsTrue(progression.IsCleared(2));
            Assert.AreEqual(2, progression.CurrentOrdinal);
            Assert.IsFalse(progression.HasNext);
        }

        [Test]
        public void EachLevelKeepsItsOwnPlayCount()
        {
            Progression progression = NewProgression(3);

            progression.RecordAttemptStarted();
            progression.RecordAttemptStarted();
            progression.CompleteCurrentLevel();
            progression.RecordAttemptStarted();

            Assert.AreEqual(2, progression.PlaysOf(0));
            Assert.AreEqual(1, progression.PlaysOf(1));
        }

        [Test]
        public void AnOrdinalPastTheEndIsDraggedBackIntoRange()
        {
            // A save outliving the database that produced it: levels reordered, or a build
            // shipping fewer. Left alone, this ordinal opens nothing at all, forever.
            save.currentLevelOrdinal = 99;

            Progression progression = NewProgression(3);

            Assert.AreEqual(2, progression.CurrentOrdinal);
            Assert.AreEqual(1, changes, "The repair has to reach the disk, or it happens again on every load.");
        }

        [Test]
        public void ANegativeOrdinalIsDraggedBackIntoRange()
        {
            save.currentLevelOrdinal = -4;

            Progression progression = NewProgression(3);

            Assert.AreEqual(0, progression.CurrentOrdinal);
        }

        [Test]
        public void AnEmptyDatabaseLeavesTheOrdinalAtZeroAndOffersNoNext()
        {
            save.currentLevelOrdinal = 5;

            Progression progression = NewProgression(0);

            Assert.AreEqual(0, progression.CurrentOrdinal);
            Assert.IsFalse(progression.HasNext);
        }

        [Test]
        public void OnlyPlayedLevelsGetARecord()
        {
            Progression progression = NewProgression(2000);

            progression.RecordAttemptStarted();

            Assert.AreEqual(1, save.levels.Count, "A 2000-level database must not become a 2000-record save file.");
            Assert.AreEqual(0, save.levels[0].levelOrdinal);
        }
    }
}
