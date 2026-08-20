using System;
using System.Collections.Generic;
using System.Text;
using ColorfulSort.Board;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// The one place that knows what a level looks like on disk.
    /// <para>
    /// Levels are not assets. They are lines in a single compact JSON file, one level per line, and
    /// this type is both ends of that: the level editor encodes through it and the database decodes
    /// through it, so the two cannot drift apart the way a writer and a reader in different files
    /// would (D-085).
    /// </para>
    /// <para>
    /// **The format.** Short keys, and each column packed into one string rather than an object per
    /// cell — which is where nearly all of the size went, since a four-cell column was six lines of
    /// YAML and is now six characters:
    /// </para>
    /// <code>
    /// {"v":1,"L":[
    /// {"i":0,"d":0,"r":3,"c":3,"p":[1,2,4,5,3,0],"k":["n4:bbbc","n4:cccb","n4"]}
    /// ]}
    /// </code>
    /// <para>
    /// A column is <c>&lt;kind&gt;&lt;capacity&gt;</c> followed by any of three optional sections, in
    /// any order: <c>:</c> its cells bottom-up, <c>#</c> an ice column's thaw count, <c>/</c> a
    /// covered column's key colour. Kinds are <c>n i v m</c>. A cell is a letter — <c>a</c>..<c>l</c>
    /// for colour ids 1..12, <c>.</c> for none — with <c>*</c> in front of it when the cell is hidden.
    /// </para>
    /// <para>
    /// It reads as text on purpose. A binary or gzipped file would have been slightly smaller and
    /// completely opaque: a level that decodes to the *wrong* board still plays, so being able to read
    /// a diff is not a convenience here, it is how that mistake gets caught.
    /// </para>
    /// </summary>
    public static class LevelCodec
    {
        /// <summary>
        /// The format's own version, and the first key in the file. A decoder that met a version it
        /// did not know would otherwise read a thirteenth colour, or a new column kind, as something
        /// it recognises — so it refuses instead.
        /// </summary>
        public const int Version = 1;

        private const char NormalKind = 'n';
        private const char IceKind = 'i';
        private const char CoveredKind = 'v';
        private const char MysteryKind = 'm';

        private const char CellsMark = ':';
        private const char ThawMark = '#';
        private const char CoverMark = '/';
        private const char HiddenMark = '*';
        private const char NoColour = '.';

        /// <summary>
        /// One level exactly as it sits in the file. The field names are one letter because they *are*
        /// the format — this type is the wire shape and nothing else reads it.
        /// </summary>
        [Serializable]
        public sealed class LevelRow
        {
            /// <summary>Level index — the number on the plaque.</summary>
            public int i;

            /// <summary>Difficulty label, as its enum value.</summary>
            public int d;

            /// <summary>Layout rows.</summary>
            public int r;

            /// <summary>Layout columns.</summary>
            public int c;

            /// <summary>Placement: the grid cell each column stands in, in slot order.</summary>
            public int[] p;

            /// <summary>The packed columns, in slot order.</summary>
            public string[] k;
        }

        [Serializable]
        private sealed class LevelFile
        {
            public int v;
            public LevelRow[] L;
        }

        /// <summary>
        /// Reads the file into its rows without building a single level. That split is the whole of
        /// the load story: two thousand rows are two thousand small structs, while two thousand
        /// levels would be two thousand <c>ScriptableObject</c>s and their columns — so the rows are
        /// read once and a level is built only when it is played (D-085).
        /// </summary>
        public static bool TryReadFile(string text, out LevelRow[] rows, out string error)
        {
            rows = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "the level file is empty";
                return false;
            }

            LevelFile file;

            try
            {
                file = JsonUtility.FromJson<LevelFile>(text);
            }
            catch (Exception malformed)
            {
                error = "the level file is not readable JSON: " + malformed.Message;
                return false;
            }

            if (file == null)
            {
                error = "the level file holds no object";
                return false;
            }

            if (file.v != Version)
            {
                error = "the level file is version " + file.v + " and this build reads version " + Version;
                return false;
            }

            rows = file.L ?? new LevelRow[0];
            error = null;
            return true;
        }

        /// <summary>
        /// Builds one level from its row. The instance is transient — created, never saved — which is
        /// what lets a level stay a <see cref="ScriptableObject"/> while no level asset exists on disk.
        /// </summary>
        public static bool TryDecode(LevelRow row, out LevelDefinition level, out string error)
        {
            level = null;

            if (row == null)
            {
                error = "there is no row to read";
                return false;
            }

            string[] packed = row.k ?? new string[0];
            var columns = new ColumnDefinition[packed.Length];

            for (int slot = 0; slot < packed.Length; slot++)
            {
                string columnError;

                if (!TryDecodeColumn(packed[slot], out columns[slot], out columnError))
                {
                    error = "level " + row.i + ", column " + slot + ": " + columnError;
                    return false;
                }
            }

            if (row.p != null && row.p.Length != 0 && row.p.Length != packed.Length)
            {
                error = "level " + row.i + " places " + row.p.Length + " columns but has " + packed.Length;
                return false;
            }

            level = LevelDefinition.Create(row.i, (DifficultyLabel)row.d, row.r, row.c, columns, row.p);
            error = null;
            return true;
        }

        /// <summary>Every level in the file, for the editor and for tests. The game does not use this.</summary>
        public static bool TryDecodeAll(string text, out List<LevelDefinition> levels, out string error)
        {
            levels = null;
            LevelRow[] rows;

            if (!TryReadFile(text, out rows, out error))
            {
                return false;
            }

            var decoded = new List<LevelDefinition>(rows.Length);

            for (int index = 0; index < rows.Length; index++)
            {
                LevelDefinition level;

                if (!TryDecode(rows[index], out level, out error))
                {
                    return false;
                }

                decoded.Add(level);
            }

            levels = decoded;
            error = null;
            return true;
        }

        /// <summary>
        /// The whole file, one level per line.
        /// <para>
        /// The newlines are deliberate and they are the answer to the one real cost of keeping every
        /// level in one file: without them a single edit rewrites one enormous line and a diff can say
        /// nothing except "it changed". A line each costs about a byte per level and makes an edit show
        /// up as the level it was.
        /// </para>
        /// </summary>
        public static string Encode(IReadOnlyList<LevelDefinition> levels)
        {
            var text = new StringBuilder();
            text.Append("{\"v\":").Append(Version).Append(",\"L\":[");

            int count = levels == null ? 0 : levels.Count;

            for (int index = 0; index < count; index++)
            {
                text.Append(index == 0 ? "\n" : ",\n");
                EncodeLevel(levels[index], text);
            }

            if (count > 0)
            {
                text.Append('\n');
            }

            return text.Append("]}\n").ToString();
        }

        private static void EncodeLevel(LevelDefinition level, StringBuilder text)
        {
            if (level == null)
            {
                throw new ArgumentException("A level list with a hole in it cannot be written.", nameof(level));
            }

            text.Append("{\"i\":").Append(level.LevelIndex);
            text.Append(",\"d\":").Append((int)level.Difficulty);
            text.Append(",\"r\":").Append(level.LayoutRows);
            text.Append(",\"c\":").Append(level.LayoutColumns);

            int[] placement = level.LayoutCells();
            text.Append(",\"p\":[");

            for (int slot = 0; slot < placement.Length; slot++)
            {
                if (slot > 0)
                {
                    text.Append(',');
                }

                text.Append(placement[slot]);
            }

            text.Append("],\"k\":[");
            IReadOnlyList<ColumnDefinition> columns = level.Columns;

            for (int slot = 0; slot < columns.Count; slot++)
            {
                if (slot > 0)
                {
                    text.Append(',');
                }

                text.Append('"');
                EncodeColumn(columns[slot], text);
                text.Append('"');
            }

            text.Append("]}");
        }

        private static void EncodeColumn(ColumnDefinition column, StringBuilder text)
        {
            if (column == null)
            {
                throw new ArgumentException("A column list with a hole in it cannot be written.", nameof(column));
            }

            text.Append(KindToChar(column.Kind)).Append(column.Capacity);

            IReadOnlyList<CellDefinition> cells = column.Cells;

            if (cells.Count > 0)
            {
                text.Append(CellsMark);

                for (int cell = 0; cell < cells.Count; cell++)
                {
                    if (cells[cell].hidden)
                    {
                        text.Append(HiddenMark);
                    }

                    text.Append(ColourToChar(cells[cell].colourId));
                }
            }

            // Written only where they mean something, so a plain column stays four characters and the
            // file does not carry a zero for every rule a level does not use.
            if (column.ThawAfterCompletions > 0)
            {
                text.Append(ThawMark).Append(column.ThawAfterCompletions);
            }

            if (column.CoverKeyColourId > 0)
            {
                text.Append(CoverMark).Append(ColourToChar(column.CoverKeyColourId));
            }
        }

        /// <summary>
        /// Reads one packed column. The three optional sections are read wherever they appear rather
        /// than in a fixed order — the encoder writes one order, and a decoder that insisted on it
        /// would turn a hand-tidied file into an unreadable one for no gain.
        /// </summary>
        public static bool TryDecodeColumn(string packed, out ColumnDefinition column, out string error)
        {
            column = null;

            if (string.IsNullOrEmpty(packed))
            {
                error = "the column is blank";
                return false;
            }

            ColumnKind kind;

            if (!TryReadKind(packed[0], out kind))
            {
                error = "'" + packed[0] + "' is not one of the column kinds " +
                        NormalKind + " " + IceKind + " " + CoveredKind + " " + MysteryKind;
                return false;
            }

            int at = 1;
            int capacity;

            if (!TryReadNumber(packed, ref at, out capacity))
            {
                error = "no capacity after the kind";
                return false;
            }

            var cells = new List<CellDefinition>();
            int thaw = 0;
            int coverKey = 0;

            while (at < packed.Length)
            {
                char mark = packed[at++];

                switch (mark)
                {
                    case CellsMark:
                        if (!TryReadCells(packed, ref at, cells, out error))
                        {
                            return false;
                        }

                        break;

                    case ThawMark:
                        if (!TryReadNumber(packed, ref at, out thaw))
                        {
                            error = "no number after '" + ThawMark + "'";
                            return false;
                        }

                        break;

                    case CoverMark:
                        if (at >= packed.Length || !TryReadColour(packed[at++], out coverKey))
                        {
                            error = "no colour after '" + CoverMark + "'";
                            return false;
                        }

                        break;

                    default:
                        error = "'" + mark + "' is not one of '" + CellsMark + "' '" + ThawMark + "' '" + CoverMark + "'";
                        return false;
                }
            }

            column = ColumnDefinition.Create(kind, capacity, cells.ToArray(), thaw, coverKey);
            error = null;
            return true;
        }

        private static bool TryReadCells(string packed, ref int at, List<CellDefinition> cells, out string error)
        {
            while (at < packed.Length && packed[at] != ThawMark && packed[at] != CoverMark)
            {
                bool hidden = packed[at] == HiddenMark;

                if (hidden)
                {
                    at++;

                    if (at >= packed.Length)
                    {
                        error = "'" + HiddenMark + "' with no cell after it";
                        return false;
                    }
                }

                int colour;

                if (!TryReadColour(packed[at], out colour))
                {
                    error = "'" + packed[at] + "' is not a cell";
                    return false;
                }

                at++;
                cells.Add(new CellDefinition { colourId = colour, hidden = hidden });
            }

            error = null;
            return true;
        }

        private static bool TryReadNumber(string packed, ref int at, out int value)
        {
            int start = at;
            value = 0;

            while (at < packed.Length && packed[at] >= '0' && packed[at] <= '9')
            {
                value = value * 10 + (packed[at] - '0');
                at++;
            }

            return at > start;
        }

        private static bool TryReadKind(char mark, out ColumnKind kind)
        {
            switch (mark)
            {
                case NormalKind: kind = ColumnKind.Normal; return true;
                case IceKind: kind = ColumnKind.Ice; return true;
                case CoveredKind: kind = ColumnKind.Covered; return true;
                case MysteryKind: kind = ColumnKind.Mystery; return true;
                default: kind = ColumnKind.Normal; return false;
            }
        }

        private static char KindToChar(ColumnKind kind)
        {
            switch (kind)
            {
                case ColumnKind.Ice: return IceKind;
                case ColumnKind.Covered: return CoveredKind;
                case ColumnKind.Mystery: return MysteryKind;
                default: return NormalKind;
            }
        }

        /// <summary>A colour id as one character: '.' for none, then 'a' upward for 1..12.</summary>
        private static char ColourToChar(int colourId)
        {
            if (colourId == 0)
            {
                return NoColour;
            }

            if (colourId < BlockColourId.MinId || colourId > BlockColourId.MaxId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colourId), colourId,
                    "A colour id is 0 or " + BlockColourId.MinId + ".." + BlockColourId.MaxId + ".");
            }

            return (char)('a' + colourId - BlockColourId.MinId);
        }

        private static bool TryReadColour(char mark, out int colourId)
        {
            if (mark == NoColour)
            {
                colourId = 0;
                return true;
            }

            colourId = mark - 'a' + BlockColourId.MinId;
            return colourId >= BlockColourId.MinId && colourId <= BlockColourId.MaxId;
        }
    }
}
