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

        [Tooltip("Columns in layout order: row by row, left to right.")]
        [SerializeField]
        private ColumnDefinition[] columns;

        public int LevelIndex => levelIndex;

        public DifficultyLabel Difficulty => difficulty;

        public int LayoutRows => layoutRows;

        public int LayoutColumns => layoutColumns;

        public IReadOnlyList<ColumnDefinition> Columns => columns ?? NoColumns;

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
