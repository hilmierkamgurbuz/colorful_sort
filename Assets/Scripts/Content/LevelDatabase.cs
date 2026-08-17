using System.Collections.Generic;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// The level order the game plays through. <c>Meta</c> tracks progression by
    /// <em>ordinal</em> — position in this list — while the plaque shows the level's
    /// own <see cref="LevelDefinition.LevelIndex"/>, so a level can be inserted later
    /// without renumbering the reference's transcription.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Colorful Sort/Level Database")]
    public sealed class LevelDatabase : ScriptableObject
    {
        /// <summary>Scale bound from fingerprint.md: at most 2000 levels.</summary>
        public const int MaxLevels = 2000;

        private static readonly LevelDefinition[] NoLevels = new LevelDefinition[0];

        [Tooltip("Levels in play order. The database does not renumber them; it orders them.")]
        [SerializeField]
        private LevelDefinition[] levels;

        public int Count => levels == null ? 0 : levels.Length;

        public IReadOnlyList<LevelDefinition> Levels => levels ?? NoLevels;

        /// <summary>The level at a position in play order, or null when the ordinal is past the end.</summary>
        public LevelDefinition ByOrdinal(int ordinal)
        {
            LevelDefinition[] ordered = levels ?? NoLevels;
            return ordinal < 0 || ordinal >= ordered.Length ? null : ordered[ordinal];
        }

        /// <summary>Finds a level by the number on its plaque.</summary>
        public bool TryFindByLevelIndex(int levelIndex, out LevelDefinition level)
        {
            LevelDefinition[] ordered = levels ?? NoLevels;

            for (int ordinal = 0; ordinal < ordered.Length; ordinal++)
            {
                if (ordered[ordinal] != null && ordered[ordinal].LevelIndex == levelIndex)
                {
                    level = ordered[ordinal];
                    return true;
                }
            }

            level = null;
            return false;
        }

        /// <summary>
        /// Checks the list itself: no gaps, no duplicates, ascending. Whether each level
        /// is playable is that level's own <c>Validate</c> — this one would otherwise
        /// convert two thousand boards on every Inspector keystroke.
        /// </summary>
        public bool Validate(out string error)
        {
            LevelDefinition[] ordered = levels ?? NoLevels;

            if (ordered.Length > MaxLevels)
            {
                error = "The database holds " + ordered.Length + " levels; the ceiling is " + MaxLevels + ".";
                return false;
            }

            int previousIndex = int.MinValue;

            for (int ordinal = 0; ordinal < ordered.Length; ordinal++)
            {
                if (ordered[ordinal] == null)
                {
                    error = "Slot " + ordinal + " is empty, so progression would stop there.";
                    return false;
                }

                int index = ordered[ordinal].LevelIndex;
                if (index <= previousIndex)
                {
                    error = "Level " + index + " at slot " + ordinal + " does not come after level " + previousIndex +
                            "; the list is out of order or holds it twice.";
                    return false;
                }

                previousIndex = index;
            }

            error = null;
            return true;
        }

        private void OnValidate()
        {
            if (Count == 0)
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
