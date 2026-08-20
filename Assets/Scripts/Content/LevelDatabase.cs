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

        private static readonly LevelCodec.LevelRow[] NoRows = new LevelCodec.LevelRow[0];

        [Tooltip("Every level, in play order, as one compact JSON file. Written by the level editor; there are no level assets (D-085).")]
        [SerializeField]
        private TextAsset levels;

        /// <summary>
        /// The file's rows, read once. Small structs rather than levels: two thousand of these is a
        /// few hundred kilobytes, while two thousand levels would be two thousand ScriptableObjects
        /// and every column under them.
        /// </summary>
        private LevelCodec.LevelRow[] rows;

        /// <summary>Levels already asked for. A level is built the first time it is played, then kept.</summary>
        private LevelDefinition[] built;

        private bool read;

        public int Count => Rows.Length;

        /// <summary>
        /// Every level, built. It is what the level editor and validation want and what the game never
        /// asks for — reaching for it turns the whole file into objects, which is the exact cost
        /// <see cref="ByOrdinal"/> exists to avoid.
        /// </summary>
        public IReadOnlyList<LevelDefinition> Levels
        {
            get
            {
                LevelCodec.LevelRow[] all = Rows;

                for (int ordinal = 0; ordinal < all.Length; ordinal++)
                {
                    ByOrdinal(ordinal);
                }

                return built;
            }
        }

        /// <summary>The level at a position in play order, or null when the ordinal is past the end.</summary>
        public LevelDefinition ByOrdinal(int ordinal)
        {
            LevelCodec.LevelRow[] all = Rows;

            if (ordinal < 0 || ordinal >= all.Length)
            {
                return null;
            }

            if (built[ordinal] != null)
            {
                return built[ordinal];
            }

            LevelDefinition level;
            string error;

            if (!LevelCodec.TryDecode(all[ordinal], out level, out error))
            {
                Debug.LogError("[" + name + "] level " + ordinal + " cannot be read: " + error, this);
                return null;
            }

            built[ordinal] = level;
            return level;
        }

        /// <summary>
        /// Finds a level by the number on its plaque. It reads the *rows* rather than the levels, so
        /// a search does not build every level on the way past — the row already carries the index,
        /// which is the whole reason the file leads with it.
        /// </summary>
        public bool TryFindByLevelIndex(int levelIndex, out LevelDefinition level)
        {
            LevelCodec.LevelRow[] all = Rows;

            for (int ordinal = 0; ordinal < all.Length; ordinal++)
            {
                if (all[ordinal] != null && all[ordinal].i == levelIndex)
                {
                    level = ByOrdinal(ordinal);
                    return level != null;
                }
            }

            level = null;
            return false;
        }

        /// <summary>
        /// The file, read the first time anything asks. Once, and remembered even when it fails — a
        /// broken file would otherwise report itself on every call for the rest of the session.
        /// </summary>
        private LevelCodec.LevelRow[] Rows
        {
            get
            {
                if (read)
                {
                    return rows;
                }

                read = true;
                rows = NoRows;

                if (levels == null)
                {
                    Debug.LogError("[" + name + "] has no level file, so there is nothing to play.", this);
                }
                else
                {
                    LevelCodec.LevelRow[] parsed;
                    string error;

                    if (LevelCodec.TryReadFile(levels.text, out parsed, out error))
                    {
                        rows = parsed;
                    }
                    else
                    {
                        Debug.LogError("[" + name + "] " + levels.name + ": " + error, this);
                    }
                }

                built = new LevelDefinition[rows.Length];
                return rows;
            }
        }

        /// <summary>Drops what has been read, so the editor sees a file it has just rewritten.</summary>
        public void Reload()
        {
            read = false;
            rows = null;
            built = null;
        }

        /// <summary>
        /// Checks the list itself: no gaps, no duplicates, ascending. Whether each level
        /// is playable is that level's own <c>Validate</c> — this one would otherwise
        /// convert two thousand boards on every Inspector keystroke.
        /// </summary>
        public bool Validate(out string error)
        {
            LevelCodec.LevelRow[] ordered = Rows;

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

                int index = ordered[ordinal].i;
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
            Reload();

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
