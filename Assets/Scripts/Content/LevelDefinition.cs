using System;
using System.Collections.Generic;
using ColorfulSort.Board;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// One authored level. It is the level editor's output (D-010) and the only place
    /// a level's shape is written down.
    /// <para>
    /// Two kinds of data live here and they are kept apart on purpose: the columns
    /// convert into the rules' <see cref="LevelData"/>, while the difficulty label and
    /// the grid layout stay on this side, because no rule reads them and Board is not
    /// allowed to carry presentation data.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "Level_0000", menuName = "Colorful Sort/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        /// <summary>The number on the first level's plaque. Levels count from one, not from zero.</summary>
        public const int FirstLevelIndex = 1;

        private static readonly ColumnDefinition[] NoColumns = new ColumnDefinition[0];

        [Tooltip("The number on the level plaque. Also how the database finds this level.")]
        [SerializeField]
        private int levelIndex;

        [SerializeField]
        private DifficultyLabel difficulty;

        [Tooltip("How the columns are arranged on screen — Level 79 is 2 rows of 6. Read by BoardView, never by a rule.")]
        [SerializeField]
        private int layoutRows;

        [SerializeField]
        private int layoutColumns;

        [Tooltip("Columns in slot order. Where each one stands is columnCells; the order itself is what the attempt scramble permutes.")]
        [SerializeField]
        private ColumnDefinition[] columns;

        [Tooltip("Where each column stands on the layout grid, as row × layoutColumns + column, one entry per column. Empty means the plain fill: row by row, left to right, no holes.")]
        [SerializeField]
        private int[] columnCells;

        /// <summary>
        /// Builds a level from decoded data, as a **transient** instance: created, never saved, and
        /// marked so Unity neither writes it into a scene nor throws it away on a load.
        /// <para>
        /// This is what lets a level stay a <see cref="ScriptableObject"/> while no level asset exists
        /// on disk — which is what keeps the level editor's whole <c>SerializedProperty</c> surface
        /// working unchanged (D-032, D-085). <c>internal</c>, because the file is the authority for
        /// level content and a public setter would be a second way to write it.
        /// </para>
        /// </summary>
        internal static LevelDefinition Create(
            int levelIndex,
            DifficultyLabel difficulty,
            int layoutRows,
            int layoutColumns,
            ColumnDefinition[] columns,
            int[] columnCells)
        {
            var level = CreateInstance<LevelDefinition>();
            level.name = "Level_" + levelIndex.ToString("0000");
            level.hideFlags = HideFlags.HideAndDontSave;
            level.levelIndex = levelIndex;
            level.difficulty = difficulty;
            level.layoutRows = layoutRows;
            level.layoutColumns = layoutColumns;
            level.columns = columns ?? NoColumns;
            level.columnCells = columnCells;
            return level;
        }

        public int LevelIndex => levelIndex;

        public DifficultyLabel Difficulty => difficulty;

        public int LayoutRows => layoutRows;

        public int LayoutColumns => layoutColumns;

        public IReadOnlyList<ColumnDefinition> Columns => columns ?? NoColumns;

        /// <summary>
        /// The grid cell each column stands in, one entry per column, in slot order.
        /// <para>
        /// The placement belongs to the <em>slot</em> and not to the column standing in it,
        /// which is why it lives here rather than on <see cref="ColumnDefinition"/>: an
        /// attempt permutes the columns among the slots (D-014), so a position that
        /// travelled with its column would make that permutation invisible (D-033).
        /// </para>
        /// <para>
        /// An unauthored — or half-authored — placement falls back to the plain fill, row by
        /// row and left to right, so a level that does not care about placement authors
        /// nothing. A length that disagrees with the column count is a content defect and
        /// <see cref="Validate"/> says so; drawing something is still better than throwing
        /// inside the level editor while a level is half typed in.
        /// </para>
        /// </summary>
        public int[] LayoutCells()
        {
            int count = Columns.Count;
            var placement = new int[count];

            bool authored = columnCells != null && columnCells.Length == count;

            for (int slot = 0; slot < count; slot++)
            {
                placement[slot] = authored ? columnCells[slot] : slot;
            }

            return placement;
        }

        /// <summary>
        /// Converts the authored columns into the rules' input contract. Throws on
        /// illegal data, with the message the rules produced — a broken level fails at
        /// the level editor, not on a player's device.
        /// </summary>
        public LevelData ToLevelData()
        {
            ColumnDefinition[] authored = columns ?? NoColumns;
            var converted = new ColumnData[authored.Length];

            for (int column = 0; column < authored.Length; column++)
            {
                if (authored[column] == null)
                {
                    throw new ArgumentException("Column " + column + " of " + name + " is empty.");
                }

                converted[column] = authored[column].ToColumnData();
            }

            return new LevelData(levelIndex, converted);
        }

        /// <summary>
        /// Runs every check the game would run, without needing a scene: the data
        /// conversion, the rules' own level validation, and the layout grid. The level
        /// editor's "is this level shippable" button is this method (D-010).
        /// </summary>
        public bool Validate(out string error)
        {
            // Levels are numbered from 1. It is an authoring rule rather than anything the game
            // computes — progression counts *ordinals* and the database never renumbers (D-085) — so
            // the only place it can be true rather than hoped is here (D-086).
            if (levelIndex < FirstLevelIndex)
            {
                error = "Level " + levelIndex + " is numbered below " + FirstLevelIndex +
                        "; levels start at " + FirstLevelIndex + ".";
                return false;
            }

            try
            {
                // Building the board is part of the check: some rules — a colour that
                // starts already gathered, for instance — only exist at board level.
                BoardState.FromLevel(ToLevelData());
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }

            int cells = layoutRows * layoutColumns;
            if (layoutRows < 1 || layoutColumns < 1 || cells < Columns.Count)
            {
                error = "The layout grid is " + layoutRows + "x" + layoutColumns +
                        ", which has no room for " + Columns.Count + " columns.";
                return false;
            }

            return ValidatePlacement(cells, out error);
        }

        /// <summary>
        /// The placement, checked the way the board will read it: one cell per column, every
        /// cell inside the grid, and no two columns standing in the same one. A level with no
        /// authored placement takes the plain fill and has nothing to check.
        /// </summary>
        private bool ValidatePlacement(int cells, out string error)
        {
            if (columnCells == null || columnCells.Length == 0)
            {
                error = null;
                return true;
            }

            if (columnCells.Length != Columns.Count)
            {
                error = "The layout places " + columnCells.Length + " column(s) but the level has " +
                        Columns.Count + ". Every column stands somewhere, and only one column stands there.";
                return false;
            }

            for (int slot = 0; slot < columnCells.Length; slot++)
            {
                if (columnCells[slot] < 0 || columnCells[slot] >= cells)
                {
                    error = "Column " + slot + " stands in grid cell " + columnCells[slot] +
                            ", which is outside the " + layoutRows + "x" + layoutColumns + " layout.";
                    return false;
                }

                for (int other = 0; other < slot; other++)
                {
                    if (columnCells[other] == columnCells[slot])
                    {
                        error = "Columns " + other + " and " + slot + " both stand in grid cell " +
                                columnCells[slot] + "; a cell holds one column.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Editor-only feedback while authoring. A level with no columns yet is simply
        /// unfinished, not broken, so it stays quiet until there is something to check.
        /// </summary>
        private void OnValidate()
        {
            if (Columns.Count == 0)
            {
                return;
            }

            string error;
            if (!Validate(out error))
            {
                Debug.LogWarning("[" + name + "] " + error, this);
            }
        }
    }
}
