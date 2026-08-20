using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.Core.Tests
{
    /// <summary>
    /// The save file is the one piece of this game that survives the process, so the
    /// tests here are about the promises a player never sees kept: a version is always
    /// written, a file that cannot be trusted is never silently rewritten, and a write
    /// that fails leaves the previous save intact.
    /// <para>
    /// They drive the real <see cref="SaveService"/> against a real folder — the
    /// directory is a constructor argument for exactly this reason — so what is proven
    /// is the file behaviour and not a mock of it.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private string directory;

        private static string CorruptSlot(string directory, int slot)
        {
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(SaveService.FileName) + ".corrupt-" + slot + ".json");
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "colorful-sort-save-tests");

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Constructor_WithoutADirectory_Refuses()
        {
            Assert.That(() => new SaveService(""), Throws.ArgumentException);
        }

        [Test]
        public void MarkDirty_BeforeLoad_Refuses()
        {
            SaveService service = new SaveService(directory);

            Assert.That(() => service.MarkDirty(), Throws.InvalidOperationException);
        }

        [Test]
        public void Load_WithNoFile_StartsAVersionedGameAndWritesNothing()
        {
            SaveService service = new SaveService(directory);

            Assert.That(service.Load(), Is.EqualTo(SaveLoadOutcome.Created));
            Assert.That(service.Data.saveVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(service.Data.playerId, Is.Not.Null.And.Not.Empty);
            Assert.That(service.IsDirty, Is.True, "a new game owes the disk a file");
            Assert.That(File.Exists(service.FilePath), Is.False, "loading never writes; the first flush point creates the file");
        }

        [Test]
        public void Load_WithNoFile_CarriesNoEconomyDefaults()
        {
            SaveService service = new SaveService(directory);
            service.Load();

            // Starting hearts, coins and booster charges are tuning numbers: they belong
            // in Data/Config/ and are Meta's to seed. A default typed in here would be
            // content living in code (.claude/rules/data.md).
            Assert.That(service.Data.coins, Is.Zero);
            Assert.That(service.Data.hearts, Is.Zero);
            Assert.That(service.Data.boosters, Is.Empty);
            Assert.That(service.Data.levels, Is.Empty);
            Assert.That(service.Data.currentLevelOrdinal, Is.Zero);

            Assert.That(service.Data.soundOn, Is.True);
            Assert.That(service.Data.musicOn, Is.True);
            Assert.That(service.Data.vibrationOn, Is.True);
        }

        [Test]
        public void Flush_WritesOnceAndLeavesNoTemporaryFile()
        {
            SaveService service = new SaveService(directory);
            service.Load();

            Assert.That(service.Flush(), Is.True);
            Assert.That(File.Exists(service.FilePath), Is.True);
            Assert.That(service.IsDirty, Is.False);

            Assert.That(service.Flush(), Is.False, "a clean save is not rewritten");
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty, "the temp file is moved into place, not left behind");
        }

        [Test]
        public void Flush_RoundTripsProgressionAndSettings()
        {
            SaveService writer = new SaveService(directory);
            writer.Load();

            writer.Data.currentLevelOrdinal = 7;
            writer.Data.coins = 1200;
            writer.Data.hearts = 3;
            writer.Data.heartRefillUnixMs = 1755400000000L;
            writer.Data.soundOn = false;
            writer.Data.levels.Add(new LevelProgressRecord { levelOrdinal = 7, cleared = true, plays = 4 });
            writer.Data.boosters.Add(new BoosterChargeRecord { boosterId = "shuffle", charges = 2 });
            writer.MarkDirty();

            Assert.That(writer.Flush(), Is.True);

            SaveService reader = new SaveService(directory);

            Assert.That(reader.Load(), Is.EqualTo(SaveLoadOutcome.Loaded));
            Assert.That(reader.IsDirty, Is.False, "a file that needed no upgrade owes the disk nothing");
            Assert.That(reader.Data.playerId, Is.EqualTo(writer.Data.playerId));
            Assert.That(reader.Data.currentLevelOrdinal, Is.EqualTo(7));
            Assert.That(reader.Data.coins, Is.EqualTo(1200));
            Assert.That(reader.Data.hearts, Is.EqualTo(3));
            Assert.That(reader.Data.heartRefillUnixMs, Is.EqualTo(1755400000000L));
            Assert.That(reader.Data.soundOn, Is.False);
            Assert.That(reader.Data.vibrationOn, Is.True);
            Assert.That(reader.Data.levels, Has.Count.EqualTo(1));
            Assert.That(reader.Data.levels[0].plays, Is.EqualTo(4), "the attempt ordinal the seed is derived from survives");
            Assert.That(reader.Data.levels[0].cleared, Is.True);
            Assert.That(reader.Data.boosters, Has.Count.EqualTo(1));
            Assert.That(reader.Data.boosters[0].boosterId, Is.EqualTo("shuffle"));
            Assert.That(reader.Data.boosters[0].charges, Is.EqualTo(2));
        }

        [Test]
        public void Flush_NeverWritesAnUnversionedSave()
        {
            SaveService service = new SaveService(directory);
            service.Load();

            // Whatever else happened to the save in memory, the bytes carry a version.
            service.Data.saveVersion = 0;
            service.MarkDirty();

            Assert.That(service.Flush(), Is.True);
            Assert.That(File.ReadAllText(service.FilePath), Does.Contain("\"saveVersion\":" + SaveData.CurrentVersion));
            Assert.That(new SaveService(directory).Load(), Is.EqualTo(SaveLoadOutcome.Loaded));
        }

        [Test]
        public void Load_WithAFileThatIsNotJson_KeepsItAsideAndStartsFresh()
        {
            SaveService service = new SaveService(directory);
            File.WriteAllText(service.FilePath, "{ this was never a save file");

            Assert.That(service.Load(), Is.EqualTo(SaveLoadOutcome.Recovered));
            Assert.That(File.Exists(CorruptSlot(directory, 1)), Is.True, "the unreadable file is evidence, not rubbish");
            Assert.That(File.Exists(service.FilePath), Is.False);
            Assert.That(service.Data.saveVersion, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(service.IsDirty, Is.True);
        }

        [Test]
        public void Load_WithAnUnversionedFile_RefusesIt()
        {
            SaveService service = new SaveService(directory);
            File.WriteAllText(service.FilePath, "{\"coins\":9999}");

            Assert.That(service.Load(), Is.EqualTo(SaveLoadOutcome.Recovered));
            Assert.That(service.Data.coins, Is.Zero, "an unversioned file's fields are never trusted");
            Assert.That(File.Exists(CorruptSlot(directory, 1)), Is.True);
        }

        [Test]
        public void Load_WithAFileFromANewerBuild_RefusesItRatherThanDowngradeIt()
        {
            SaveService service = new SaveService(directory);
            File.WriteAllText(service.FilePath, "{\"saveVersion\":" + (SaveData.CurrentVersion + 1) + ",\"coins\":9999}");

            Assert.That(service.Load(), Is.EqualTo(SaveLoadOutcome.Recovered));
            Assert.That(File.Exists(CorruptSlot(directory, 1)), Is.True);
            Assert.That(service.Data.coins, Is.Zero);
        }

        [Test]
        public void Load_WithASecondUnreadableFile_KeepsBothCopies()
        {
            SaveService service = new SaveService(directory);

            File.WriteAllText(service.FilePath, "first bad file");
            service.Load();

            File.WriteAllText(service.FilePath, "second bad file");
            service.Load();

            Assert.That(File.ReadAllText(CorruptSlot(directory, 1)), Is.EqualTo("first bad file"));
            Assert.That(File.ReadAllText(CorruptSlot(directory, 2)), Is.EqualTo("second bad file"));
        }

        [Test]
        public void Flush_OverAnExistingFile_ReplacesItWholesale()
        {
            SaveService first = new SaveService(directory);
            first.Load();
            first.Data.coins = 10;
            first.MarkDirty();
            first.Flush();

            SaveService second = new SaveService(directory);
            second.Load();
            second.Data.coins = 20;
            second.MarkDirty();

            Assert.That(second.Flush(), Is.True);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1), "one save file, no temp and no leftovers");

            SaveService reader = new SaveService(directory);
            reader.Load();

            Assert.That(reader.Data.coins, Is.EqualTo(20));
        }
    }
}
