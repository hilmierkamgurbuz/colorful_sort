using System.Collections.Generic;
using NUnit.Framework;

namespace ColorfulSort.Core.Tests
{
    /// <summary>
    /// Migration is where a save file's version stops being a number and starts being a
    /// promise. These tests pin both halves of it: what is accepted and quietly repaired,
    /// and what is refused outright — because the alternative to refusing is a build
    /// reading fields whose meaning it is guessing at.
    /// </summary>
    [TestFixture]
    public sealed class SaveMigrationTests
    {
        [Test]
        public void Migrate_AVersionOneSave_TurnsMusicOnAndRaisesTheVersion()
        {
            // What a version 1 file parses to: the key is simply absent, so the field is
            // false. Left alone it would read as a player who muted the music.
            SaveData save = SaveData.NewGame();
            save.saveVersion = 1;
            save.musicOn = false;
            save.currentLevelOrdinal = 7;

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(migrated.saveVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.musicOn, Is.True);
            Assert.That(changed, Is.True, "an upgraded save is ahead of the file it came from");

            // The step upgrades the shape and touches nothing else.
            Assert.That(migrated.currentLevelOrdinal, Is.EqualTo(7));
        }

        [Test]
        public void Migrate_ACurrentSave_PassesItThroughUnchanged()
        {
            SaveData save = SaveData.NewGame();
            save.coins = 250;

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(changed, Is.False, "nothing was upgraded, so nothing is owed to the disk");
            Assert.That(migrated, Is.SameAs(save));
            Assert.That(migrated.coins, Is.EqualTo(250));
        }

        [Test]
        public void Migrate_FillsInWhatJsonUtilityLeftNull()
        {
            SaveData save = new SaveData
            {
                saveVersion = SaveData.CurrentVersion,
                playerId = null,
                levels = null,
                boosters = null,
            };

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.True);
            Assert.That(changed, Is.True, "a repaired save is ahead of the file it came from");
            Assert.That(migrated.levels, Is.Not.Null.And.Empty);
            Assert.That(migrated.boosters, Is.Not.Null.And.Empty);
            Assert.That(migrated.playerId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Migrate_LeavesProgressionValuesExactlyAsTheyWere()
        {
            // Repair fixes structure, never content: coins and hearts belong to Meta, and
            // a migration that corrected them would be a second writer of Meta's data.
            SaveData save = SaveData.NewGame();
            save.coins = -5;
            save.hearts = 99;
            save.currentLevelOrdinal = 41;
            save.levels = null;

            SaveData migrated;
            bool changed;
            string problem;

            SaveMigration.TryMigrate(save, out migrated, out changed, out problem);

            Assert.That(changed, Is.True);
            Assert.That(migrated.coins, Is.EqualTo(-5));
            Assert.That(migrated.hearts, Is.EqualTo(99));
            Assert.That(migrated.currentLevelOrdinal, Is.EqualTo(41));
        }

        [Test]
        public void Migrate_KeepsEveryLevelRecordItWasGiven()
        {
            SaveData save = SaveData.NewGame();
            save.levels = new List<LevelProgressRecord>
            {
                new LevelProgressRecord { levelOrdinal = 0, cleared = true, plays = 2 },
                new LevelProgressRecord { levelOrdinal = 1, cleared = false, plays = 5 },
            };

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.True);
            Assert.That(migrated.levels, Has.Count.EqualTo(2));
            Assert.That(migrated.levels[1].plays, Is.EqualTo(5));
        }

        [Test]
        public void Migrate_AnUnversionedSave_IsRefused()
        {
            SaveData save = SaveData.NewGame();
            save.saveVersion = 0;

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.False);
            Assert.That(migrated, Is.Null);
            Assert.That(problem, Does.Contain("no save version"));
        }

        [Test]
        public void Migrate_ASaveFromANewerBuild_IsRefused()
        {
            SaveData save = SaveData.NewGame();
            save.saveVersion = SaveData.CurrentVersion + 1;

            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(save, out migrated, out changed, out problem), Is.False);
            Assert.That(migrated, Is.Null);
            Assert.That(problem, Does.Contain("newer build"));
        }

        [Test]
        public void Migrate_Nothing_IsRefused()
        {
            SaveData migrated;
            bool changed;
            string problem;

            Assert.That(SaveMigration.TryMigrate(null, out migrated, out changed, out problem), Is.False);
            Assert.That(migrated, Is.Null);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }
    }
}
