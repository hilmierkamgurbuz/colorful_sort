using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace ColorfulSort.Board.Tests
{
    /// <summary>
    /// A level's variants: a replay must look different without becoming a different
    /// puzzle. The tests here are the argument that it is safe — the set of colours,
    /// the way they are grouped, and every column's shape all survive, across a long
    /// run of variant indices — plus the argument that it is worth doing at all: the
    /// variants really are different boards, and each one is the same board every time.
    /// </summary>
    [TestFixture]
    public sealed class AttemptScrambleTests
    {
        /// <summary>All four column kinds, so both permutations have something to move.</summary>
        private static ColumnData[] AuthoredColumns()
        {
            return new[]
            {
                TestBoards.Mystery(3, 4, 4, 1),
                TestBoards.Normal(3, 1),
                TestBoards.Covered(2, 1, 2, 3),
                TestBoards.Ice(2, 1),
                TestBoards.Normal(2, 5, 1),
            };
        }

        /// <summary>Two covers keyed to the same colour, and that colour split in two.</summary>
        private static ColumnData[] CoverColumns()
        {
            return new[]
            {
                TestBoards.Normal(2, 1),
                TestBoards.Normal(2, 1),
                TestBoards.Covered(2, 1, 2, 3),
                TestBoards.Covered(2, 1, 3, 2),
            };
        }

        [Test]
        public void None_PlaysTheAuthoredBoardAndCopiesNothing()
        {
            BoardSession session = TestBoards.Session(AuthoredColumns());

            Assert.That(session.Scramble.IsIdentity, Is.True);
            Assert.That(session.Scramble.VariantIndex, Is.EqualTo(AttemptScramble.NoVariant),
                "the authored board is not one of the variants");
            Assert.That(session.Level, Is.SameAs(session.AuthoredLevel));
            Assert.That(session.State[3].Kind, Is.EqualTo(ColumnKind.Ice), "column 3 is where it was authored");
        }

        [Test]
        public void ForVariant_LeavesTheAuthoredLevelAloneAndTheAttemptRngUntouched()
        {
            BoardSession session = TestBoards.VariantSession(2, AuthoredColumns());

            Assert.That(session.Scramble.VariantIndex, Is.EqualTo(2));
            Assert.That(session.Level, Is.Not.SameAs(session.AuthoredLevel));
            Assert.That(session.AuthoredLevel.Columns[3].Kind, Is.EqualTo(ColumnKind.Ice),
                "what Content authored is never edited");
            Assert.That(session.AuthoredLevel.Columns[2].CoverKeyColour, Is.EqualTo(new BlockColourId(1)));

            // Choosing a look must not spend a draw the Shuffle booster is going to want.
            Assert.That(session.Random.Cursor, Is.EqualTo(0));

            int from;
            int to;
            Assert.That(FindLegalMove(session, out from, out to), Is.True, "the variant is playable");
            Assert.That(session.TryMove(from, to), Is.True);
            Assert.That(session.History.Last.RngCursorBefore, Is.EqualTo(0));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Random.Cursor, Is.EqualTo(0), "the whole stream still belongs to Shuffle");
        }

        [Test]
        public void ForVariant_IsTheSameBoardEveryTime()
        {
            string first = BoardShape(TestBoards.VariantSession(2, AuthoredColumns()));
            string second = BoardShape(TestBoards.VariantSession(2, AuthoredColumns()));

            Assert.That(second, Is.EqualTo(first),
                "a variant is a pure function of (level, index), so the designer can look at exactly what a player meets");
        }

        [Test]
        public void ForVariant_GivesTheLevelFiveDistinctLooks()
        {
            var shapes = new List<string>();

            for (int variant = 0; variant < 5; variant++)
            {
                shapes.Add(BoardShape(TestBoards.VariantSession(variant, AuthoredColumns())));
            }

            for (int left = 0; left < shapes.Count; left++)
            {
                for (int right = left + 1; right < shapes.Count; right++)
                {
                    Assert.That(shapes[right], Is.Not.EqualTo(shapes[left]),
                        "variants " + left + " and " + right + " are the same board, so one of them is wasted");
                }
            }
        }

        [Test]
        public void ForVariant_KeepsTheSameColoursGroupedTheSameWay()
        {
            BoardSession authored = TestBoards.Session(AuthoredColumns());
            string colours = UsedColourIds(authored);
            string grouping = SortedColourCounts(authored);

            for (int variant = 0; variant < 32; variant++)
            {
                BoardSession scrambled = TestBoards.VariantSession(variant, AuthoredColumns());

                Assert.That(UsedColourIds(scrambled), Is.EqualTo(colours),
                    "variant " + variant + ": the map is a bijection of the ids the level uses, so the set cannot change");
                Assert.That(SortedColourCounts(scrambled), Is.EqualTo(grouping),
                    "variant " + variant + ": relabelling moves counts between ids but never changes the multiset");
            }
        }

        [Test]
        public void ForVariant_MovesEveryColumnWhole()
        {
            BoardSession authored = TestBoards.Session(AuthoredColumns());
            string structure = SortedColumnStructure(authored);

            for (int variant = 0; variant < 32; variant++)
            {
                Assert.That(SortedColumnStructure(TestBoards.VariantSession(variant, AuthoredColumns())),
                    Is.EqualTo(structure),
                    "variant " + variant + ": kind, capacity, thaw count and contents travel together");
            }
        }

        [Test]
        public void ForVariant_MapsACoverKeyWithTheBlocksThatOpenIt()
        {
            // The one place a silent bug would be lethal: if the key were left on the
            // authored colour, the cover would wait for a colour that no longer exists.
            for (int variant = 0; variant < 16; variant++)
            {
                BoardSession session = TestBoards.VariantSession(variant, CoverColumns());
                BlockColourId key = session.Scramble.ColourOf(new BlockColourId(1));

                var singles = new List<int>();
                for (int column = 0; column < session.State.ColumnCount; column++)
                {
                    if (session.State[column].Kind == ColumnKind.Normal && session.State[column].Count == 1)
                    {
                        singles.Add(column);
                    }
                }

                Assert.That(singles.Count, Is.EqualTo(2), "variant " + variant);
                Assert.That(session.State[singles[0]].TopColour, Is.EqualTo(key), "variant " + variant);
                Assert.That(session.TryMove(singles[0], singles[1]), Is.True, "variant " + variant);

                for (int column = 0; column < session.State.ColumnCount; column++)
                {
                    if (session.State[column].Kind == ColumnKind.Covered)
                    {
                        Assert.That(session.State[column].IsLocked, Is.False,
                            "variant " + variant + ": completing the mapped key opens the cover");
                    }
                }
            }
        }

        [Test]
        public void ColourOf_LeavesNoneAlone()
        {
            // A Normal column's cover key is None; if the map turned it into a real
            // colour, the scrambled level would be refused outright.
            BoardSession session = TestBoards.VariantSession(3, AuthoredColumns());

            Assert.That(session.Scramble.ColourOf(BlockColourId.None).IsNone, Is.True);
        }

        [Test]
        public void AuthoredColumnAt_IsAPermutationOfTheColumns()
        {
            BoardSession session = TestBoards.VariantSession(4, AuthoredColumns());
            var seen = new bool[session.State.ColumnCount];

            for (int position = 0; position < seen.Length; position++)
            {
                int authored = session.Scramble.AuthoredColumnAt(position);

                Assert.That(authored, Is.InRange(0, seen.Length - 1));
                Assert.That(seen[authored], Is.False, "column " + authored + " appears twice");
                seen[authored] = true;
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => session.Scramble.AuthoredColumnAt(seen.Length));
        }

        [Test]
        public void ForVariant_RefusesAMissingLevelOrANegativeIndex()
        {
            LevelData level = TestBoards.Level(AuthoredColumns());

            Assert.Throws<ArgumentNullException>(() => AttemptScramble.ForVariant(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AttemptScramble.ForVariant(level, -1));
            Assert.Throws<ArgumentNullException>(() => AttemptScramble.None.Apply(null));
            Assert.Throws<ArgumentNullException>(() => new BoardSession(level, 1, null));
        }

        private static bool FindLegalMove(BoardSession session, out int from, out int to)
        {
            for (from = 0; from < session.State.ColumnCount; from++)
            {
                for (to = 0; to < session.State.ColumnCount; to++)
                {
                    if (session.CanMove(from, to))
                    {
                        return true;
                    }
                }
            }

            from = -1;
            to = -1;
            return false;
        }

        /// <summary>The columns only — no RNG cursor, no history — so two boards can be compared.</summary>
        private static string BoardShape(BoardSession session)
        {
            var text = new StringBuilder();

            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];
                text.Append(candidate.Kind).Append(candidate.IsLocked ? "*" : "-").Append(candidate.Capacity).Append(':');

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    text.Append(candidate.ColourAt(cell).Value).Append(candidate.IsHiddenAt(cell) ? "?" : ".");
                }

                text.Append(" | ");
            }

            return text.ToString();
        }

        private static string UsedColourIds(BoardSession session)
        {
            var used = new List<string>();

            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                if (ColourCount(session, new BlockColourId(id)) > 0)
                {
                    used.Add(id.ToString());
                }
            }

            return string.Join(",", used);
        }

        private static string SortedColourCounts(BoardSession session)
        {
            var counts = new List<int>();

            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                int count = ColourCount(session, new BlockColourId(id));
                if (count > 0)
                {
                    counts.Add(count);
                }
            }

            counts.Sort();
            return string.Join(",", counts);
        }

        private static string SortedColumnStructure(BoardSession session)
        {
            var rows = new List<string>();

            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];
                int hidden = 0;

                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    if (candidate.IsHiddenAt(cell))
                    {
                        hidden++;
                    }
                }

                rows.Add(candidate.Kind + "|" + candidate.Capacity + "|" + candidate.ThawAfterCompletions +
                         "|" + candidate.Count + "|" + hidden + "|" + (candidate.IsLocked ? "locked" : "open"));
            }

            rows.Sort();
            return string.Join(" / ", rows);
        }

        private static int ColourCount(BoardSession session, BlockColourId colour)
        {
            int count = 0;

            for (int column = 0; column < session.State.ColumnCount; column++)
            {
                BoardColumn candidate = session.State[column];
                for (int cell = 0; cell < candidate.Count; cell++)
                {
                    if (candidate.ColourAt(cell) == colour)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
