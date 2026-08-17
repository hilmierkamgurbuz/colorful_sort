using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// One of a level's looks: which authored colour is wearing which skin, and which
    /// slot each authored column is standing in. A player replaying a level they have
    /// already solved gets a different variant, so a memorised tap sequence buys them
    /// nothing.
    /// <para>
    /// A level has a small, fixed set of variants rather than one per seed, and a
    /// variant is a pure function of (level index, variant index) — so variant 3 of
    /// level 79 is always the same board, and every board a player can ever meet can
    /// be looked at, and validated, in the level editor. How many variants a level
    /// offers is a tuning number and lives in <c>Data/Config/</c>; it is deliberately
    /// absent from this assembly, which only knows how to build the variant it is
    /// asked for. Which variant an attempt plays is <c>Meta</c>'s question, for the
    /// same reason.
    /// </para>
    /// <para>
    /// Both permutations are isomorphisms of a sort puzzle — relabelling the colours
    /// and reordering the slots cannot make a solvable board unsolvable, or an
    /// unsolvable one solvable. That is what makes this safe to do to a transcribed
    /// level: the editor validates the authored board once (D-010) and every variant
    /// inherits the verdict. It is not taken on trust either — the scrambled level
    /// goes back through the <see cref="LevelData"/> constructor, so every invariant
    /// is re-checked per attempt.
    /// </para>
    /// </summary>
    public sealed class AttemptScramble
    {
        /// <summary>
        /// The authored board, unscrambled — what the level editor previews and what
        /// <c>LevelDefinition.Validate()</c> checks. It is not variant 0: the layout a
        /// level was drawn in deserves its own name rather than hiding inside the
        /// variant set.
        /// </summary>
        public static readonly AttemptScramble None = new AttemptScramble(null, null, NoVariant);

        /// <summary><see cref="VariantIndex"/> of the authored board, which is not a variant.</summary>
        public const int NoVariant = -1;

        // Index = authored value, entry = attempt value. Null means identity, which is
        // also why None needs no level to exist.
        private readonly int[] _colourMap;
        private readonly int[] _columnOrder;

        private AttemptScramble(int[] colourMap, int[] columnOrder, int variantIndex)
        {
            _colourMap = colourMap;
            _columnOrder = columnOrder;
            VariantIndex = variantIndex;
        }

        /// <summary>Which of the level's looks this is, or <see cref="NoVariant"/> for the authored board.</summary>
        public int VariantIndex { get; }

        public bool IsIdentity => _colourMap == null && _columnOrder == null;

        /// <summary>
        /// Builds a level's variant. Pure and repeatable: the same pair always produces
        /// the same board, and no state outside the arguments is read or written.
        /// <para>
        /// The draws come from a stream private to this level — seeded with the level
        /// index, offset so that each variant reads its own non-overlapping slice. The
        /// attempt's own RNG is not touched, which keeps it entirely available to the
        /// Shuffle booster (D-002): choosing how a board looks must not move the cursor
        /// a later shuffle will draw from.
        /// </para>
        /// </summary>
        public static AttemptScramble ForVariant(LevelData level, int variantIndex)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (variantIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(variantIndex), variantIndex, "A variant index is not negative; the authored board is AttemptScramble.None.");
            }

            int[] used = UsedColourIds(level);
            int columnCount = level.Columns.Count;

            // A Fisher-Yates pass over k items spends k-1 draws, so a level's variant
            // costs a fixed number of them and variant n can start where variant n-1
            // finished.
            int drawsPerVariant = (used.Length > 0 ? used.Length - 1 : 0) + (columnCount > 0 ? columnCount - 1 : 0);
            var random = new DeterministicRandom(level.Index, checked(variantIndex * drawsPerVariant));

            return new AttemptScramble(DrawColourMap(used, random), DrawColumnOrder(columnCount, random), variantIndex);
        }

        /// <summary>The skin an authored colour is wearing this attempt. None maps to None.</summary>
        public BlockColourId ColourOf(BlockColourId authoredColour)
        {
            return _colourMap == null ? authoredColour : new BlockColourId(_colourMap[authoredColour.Value]);
        }

        /// <summary>Which authored column is standing in a slot this attempt.</summary>
        public int AuthoredColumnAt(int attemptPosition)
        {
            if (_columnOrder == null)
            {
                return attemptPosition;
            }

            if (attemptPosition < 0 || attemptPosition >= _columnOrder.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptPosition), attemptPosition, "This attempt has " + _columnOrder.Length + " column(s).");
            }

            return _columnOrder[attemptPosition];
        }

        /// <summary>
        /// The level this attempt actually plays. A fresh immutable
        /// <see cref="LevelData"/> — the authored one is Content's read-only output and
        /// is never touched, so there is still exactly one writer of level data.
        /// </summary>
        public LevelData Apply(LevelData authored)
        {
            if (authored == null)
            {
                throw new ArgumentNullException(nameof(authored));
            }

            if (IsIdentity)
            {
                return authored;
            }

            var columns = new ColumnData[authored.Columns.Count];

            for (int position = 0; position < columns.Length; position++)
            {
                ColumnData source = authored.Columns[AuthoredColumnAt(position)];
                var cells = new CellData[source.Cells.Count];

                for (int cell = 0; cell < cells.Length; cell++)
                {
                    cells[cell] = new CellData(ColourOf(source.Cells[cell].Colour), source.Cells[cell].Hidden);
                }

                // The whole column travels: contents, capacity, kind, thaw count and —
                // the one that would be silently lethal if it were forgotten — the cover
                // key, mapped through the same colour permutation as the blocks.
                columns[position] = new ColumnData(
                    source.Kind,
                    source.Capacity,
                    cells,
                    source.ThawAfterCompletions,
                    ColourOf(source.CoverKeyColour));
            }

            return new LevelData(authored.Index, columns);
        }

        /// <summary>
        /// A bijection of the ids the level actually uses onto themselves. Only those:
        /// mapping onto an unused id would demand a <c>BlockSkinSet</c> entry the level
        /// never asked for, and a re-skin has to stay one data edit (D-003).
        /// </summary>
        private static int[] DrawColourMap(int[] used, DeterministicRandom random)
        {
            var map = new int[BlockColourId.MaxId + 1];
            for (int id = 0; id < map.Length; id++)
            {
                map[id] = id;
            }

            var shuffled = new int[used.Length];
            Array.Copy(used, shuffled, used.Length);
            Shuffle(shuffled, random);

            for (int index = 0; index < used.Length; index++)
            {
                map[used[index]] = shuffled[index];
            }

            return map;
        }

        private static int[] DrawColumnOrder(int columnCount, DeterministicRandom random)
        {
            var order = new int[columnCount];
            for (int position = 0; position < columnCount; position++)
            {
                order[position] = position;
            }

            Shuffle(order, random);
            return order;
        }

        private static int[] UsedColourIds(LevelData level)
        {
            int mask = 0;
            for (int column = 0; column < level.Columns.Count; column++)
            {
                ColumnData data = level.Columns[column];
                for (int cell = 0; cell < data.Cells.Count; cell++)
                {
                    mask |= 1 << data.Cells[cell].Colour.Value;
                }
            }

            int count = 0;
            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                if ((mask & (1 << id)) != 0)
                {
                    count++;
                }
            }

            var used = new int[count];
            int next = 0;
            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                if ((mask & (1 << id)) != 0)
                {
                    used[next++] = id;
                }
            }

            return used;
        }

        /// <summary>Fisher–Yates, spending exactly one draw per item after the first.</summary>
        private static void Shuffle(int[] values, DeterministicRandom random)
        {
            for (int index = values.Length - 1; index > 0; index--)
            {
                int pick = random.NextInt(index + 1);
                int swapped = values[index];
                values[index] = values[pick];
                values[pick] = swapped;
            }
        }
    }
}
