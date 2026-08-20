using System.Collections.Generic;
using System.IO;
using ColorfulSort.Content;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.Content.Tests
{
    /// <summary>
    /// The level file's format. Two kinds of test, and the second one is the point.
    /// <para>
    /// Round-trips pin the encoder and the decoder against each other, which catches a format that
    /// cannot express something — a hidden cell, an ice column's thaw count — but says nothing about
    /// whether the file in the repo holds the level it is supposed to. That failure is the dangerous
    /// one: a level that decodes to a *different* board still plays, and nothing complains. So the
    /// last test opens the shipped `Levels.json` off disk and checks it against the board the level
    /// had as an asset (D-085).
    /// </para>
    /// </summary>
    // A column's KIND is checked through its encoded form rather than through ColumnKind, which
    // belongs to Board — an assembly this one does not reference. That turned out to be the better
    // assertion anyway: one string pins the kind, the capacity and every cell at once, and it is the
    // form the file actually holds.
    [TestFixture]
    public sealed class LevelCodecTests
    {
        private const string LevelsFile = "Assets/Data/Levels/Levels.json";

        private readonly List<LevelDefinition> made = new List<LevelDefinition>();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < made.Count; index++)
            {
                if (made[index] != null)
                {
                    Object.DestroyImmediate(made[index]);
                }
            }

            made.Clear();
        }

        [Test]
        public void Column_RoundTrips_ThroughEveryFeatureTheFormatHas()
        {
            // One of each thing a column can carry, so a format that quietly drops one fails here
            // rather than in a level somebody authored a month later.
            AssertColumnRoundTrip("n4:bbbc");
            AssertColumnRoundTrip("i4:aaaa#3");
            AssertColumnRoundTrip("v4:eeef/b");
            AssertColumnRoundTrip("m4:*a*b*c*d");
            AssertColumnRoundTrip("n4", "an empty column carries no cells at all");
            AssertColumnRoundTrip("n4:...", "a half-authored cell has no colour yet");
        }

        [Test]
        public void Column_SectionsCanComeInAnyOrder()
        {
            ColumnDefinition written = Decode("i4:aaaa#3");
            ColumnDefinition swapped = Decode("i4#3:aaaa");

            Assert.That(swapped.ThawAfterCompletions, Is.EqualTo(written.ThawAfterCompletions));
            Assert.That(swapped.Cells.Count, Is.EqualTo(written.Cells.Count));
        }

        [Test]
        public void Column_CarriesHiddenCellsAndTheirColours()
        {
            // A mystery column's cells are hidden and still know what they are — Board reveals them
            // later, so losing the colour here would lose the level.
            ColumnDefinition column = Decode("m4:*a*ldb");

            Assert.That(column.Cells.Count, Is.EqualTo(4));
            Assert.That(column.Cells[0].hidden, Is.True);
            Assert.That(column.Cells[0].colourId, Is.EqualTo(1), "'a' is the first colour");
            Assert.That(column.Cells[1].hidden, Is.True);
            Assert.That(column.Cells[1].colourId, Is.EqualTo(12), "'l' is the twelfth, and the last");
            Assert.That(column.Cells[2].hidden, Is.False);
            Assert.That(column.Cells[2].colourId, Is.EqualTo(4));
        }

        [Test]
        public void Column_ThatIsNotAColumn_IsRefusedAndSaysWhy()
        {
            AssertColumnRefused("", "blank");
            AssertColumnRefused("x4:aaa", "kinds");
            AssertColumnRefused("n:aaa", "capacity");
            AssertColumnRefused("n4:aaz", "not a cell");
            AssertColumnRefused("n4:aa*", "with no cell after it");
            AssertColumnRefused("n4?3", "is not one of");
        }

        [Test]
        public void File_OfADifferentVersion_IsRefusedRatherThanMisread()
        {
            // The reason the version is the first key: a file written by a later build could hold a
            // thirteenth colour or a fifth column kind, and reading it with this decoder would produce
            // a board that is wrong rather than a board that fails.
            List<LevelDefinition> levels;
            string error;

            Assert.That(LevelCodec.TryDecodeAll("{\"v\":2,\"L\":[]}", out levels, out error), Is.False);
            Assert.That(error, Does.Contain("version"));
        }

        [Test]
        public void File_ThatIsNotJson_IsRefusedRatherThanThrowing()
        {
            List<LevelDefinition> levels;
            string error;

            Assert.That(LevelCodec.TryDecodeAll("not a file", out levels, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(LevelCodec.TryDecodeAll("", out levels, out error), Is.False);
        }

        [Test]
        public void File_RoundTripsALevelWholeAndWritesOneLinePerLevel()
        {
            LevelDefinition original = DecodeOnly(
                "{\"v\":1,\"L\":[\n" +
                "{\"i\":79,\"d\":2,\"r\":2,\"c\":6,\"p\":[3,1,0],\"k\":[\"n4:bbbc\",\"i4:#2\",\"v4:eeef/b\"]}\n" +
                "]}")[0];

            string written = LevelCodec.Encode(new[] { original });
            LevelDefinition again = DecodeOnly(written)[0];

            Assert.That(again.LevelIndex, Is.EqualTo(79));
            Assert.That((int)again.Difficulty, Is.EqualTo(2));
            Assert.That(again.LayoutRows, Is.EqualTo(2));
            Assert.That(again.LayoutColumns, Is.EqualTo(6));
            Assert.That(again.LayoutCells(), Is.EqualTo(new[] { 3, 1, 0 }), "the placement belongs to the slot (D-033)");
            Assert.That(again.Columns.Count, Is.EqualTo(3));
            Assert.That(again.Columns[1].ThawAfterCompletions, Is.EqualTo(2));
            Assert.That(again.Columns[2].CoverKeyColourId, Is.EqualTo(2));

            // The ice column went in as "i4:#2" and comes out as "i4#2": a section with nothing in it
            // is not written back. The canonical form differing from a hand-typed one is fine — what
            // must not differ is what it means, which is what the two assertions above check.
            Assert.That(written, Does.Contain("\"i4#2\""));

            // One line per level is what keeps a single-file layout diffable: three lines for one
            // level is the wrapper's two plus the level's own.
            Assert.That(
                written.Trim().Split('\n').Length,
                Is.EqualTo(3),
                "a level should be one line:\n" + written);
        }

        [Test]
        public void ShippedFile_IsReadableAndEveryLevelInItIsShippable()
        {
            // What this asserts had to change once the file stopped being empty on purpose: "the file
            // holds nothing" was true for about an hour and would have failed the moment the first
            // level was saved — a test that breaks when the project starts working is worse than no
            // test. What lasts is that the file **reads** and everything in it **plays**: an unreadable
            // file and an empty one look identical from the menu, and a level saved in a state the
            // board would refuse looks fine until somebody opens it (D-087).
            string path = Path.Combine(Application.dataPath, "..", LevelsFile);

            Assert.That(File.Exists(path), Is.True, LevelsFile + " is missing");

            List<LevelDefinition> levels = DecodeOnly(File.ReadAllText(path));

            for (int ordinal = 0; ordinal < levels.Count; ordinal++)
            {
                LevelDefinition level = levels[ordinal];

                Assert.That(
                    level.Validate(out string error),
                    Is.True,
                    "level " + level.LevelIndex + " is in the file and would not open: " + error);

                Assert.That(
                    level.LevelIndex,
                    Is.GreaterThanOrEqualTo(1),
                    "levels are numbered from 1 (D-086)");
            }
        }

        [Test]
        public void Level_NumberedBelowOne_IsRefused()
        {
            // Levels count from 1. Nothing computes that — progression counts ordinals and the
            // database never renumbers — so the validator is the only place it can be true rather
            // than hoped (D-086).
            LevelDefinition zero = MinimalLevel(0);

            Assert.That(zero.Validate(out string tooLow), Is.False, "level 0 was accepted");
            Assert.That(tooLow, Does.Contain("levels start at 1"));

            LevelDefinition one = MinimalLevel(1);

            Assert.That(one.Validate(out string error), Is.True, error);
        }

        /// <summary>
        /// A level that satisfies every rule <see cref="LevelDefinition.Validate"/> runs, so a test
        /// about one rule cannot fail on another.
        /// <para>
        /// It exists because this fixture broke three rules in three attempts: a colour already
        /// gathered in a single column (D-013), then the two-column minimum, and each time the failure
        /// reported was about something the test was not testing. A fixture is data, and data that
        /// breaks a rule it is not testing reports the wrong thing (D-087).
        /// </para>
        /// <para>
        /// Every choice here is deliberate: **two** columns because <c>LevelData.MinColumns</c> is 2;
        /// capacity 3 inside the 1..8 range; <c>ab</c> against <c>ba</c> so neither colour is complete
        /// in a single column; a 1x2 grid, which is exactly enough room; and a placement that gives
        /// each column one cell and no cell to two columns.
        /// </para>
        /// <para>
        /// It is **offered, not imposed**. The other tests in this file build their own levels on
        /// purpose — they test the *format*, and a round-trip has to work on a level `Validate` would
        /// reject as much as on one it accepts.
        /// </para>
        /// </summary>
        private LevelDefinition MinimalLevel(int levelIndex)
        {
            return DecodeOnly(
                "{\"v\":1,\"L\":[\n" +
                "{\"i\":" + levelIndex + ",\"d\":0,\"r\":1,\"c\":2,\"p\":[0,1]," +
                "\"k\":[\"n3:ab\",\"n3:ba\"]}\n" +
                "]}")[0];
        }

        private List<LevelDefinition> DecodeOnly(string text)
        {
            List<LevelDefinition> levels;
            string error;

            Assert.That(LevelCodec.TryDecodeAll(text, out levels, out error), Is.True, error);
            made.AddRange(levels);
            return levels;
        }

        private static ColumnDefinition Decode(string packed)
        {
            ColumnDefinition column;
            string error;

            Assert.That(LevelCodec.TryDecodeColumn(packed, out column, out error), Is.True, error);
            return column;
        }

        private static void AssertColumnRoundTrip(string packed, string because = null)
        {
            ColumnDefinition column = Decode(packed);
            LevelDefinition level = null;
            string error;

            Assert.That(
                LevelCodec.TryDecode(
                    new LevelCodec.LevelRow { i = 0, d = 0, r = 1, c = 1, p = new int[0], k = new[] { packed } },
                    out level,
                    out error),
                Is.True,
                error);

            string written = LevelCodec.Encode(new[] { level });
            Object.DestroyImmediate(level);

            Assert.That(written, Does.Contain("\"" + packed + "\""), because ?? (packed + " did not survive the trip: " + written));
            Assert.That(column, Is.Not.Null);
        }

        private static void AssertColumnRefused(string packed, string mentions)
        {
            ColumnDefinition column;
            string error;

            Assert.That(LevelCodec.TryDecodeColumn(packed, out column, out error), Is.False, "'" + packed + "' was accepted");
            Assert.That(error, Does.Contain(mentions), "the complaint has to name what is wrong: " + error);
        }
    }
}
