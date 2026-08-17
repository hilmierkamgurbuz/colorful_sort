using System;
using ColorfulSort.Board;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// The one asset that maps a logical colour id to its look (D-003). Level data
    /// stores ids and nothing else, so a re-skin — the reference's cat becoming a
    /// moon — is repointing a slot here and touching nothing else in the project.
    /// <para>
    /// If a second place in the project also decided what a colour looks like, the
    /// single-writer invariant would be broken. This is that single place.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "BlockSkinSet", menuName = "Colorful Sort/Block Skin Set")]
    public sealed class BlockSkinSet : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            [Range(BlockColourId.MinId, BlockColourId.MaxId)]
            public int colourId;

            public BlockSkin skin;
        }

        private static readonly Entry[] NoEntries = new Entry[0];

        [Tooltip("One row per logical colour used by the levels. The id is what level data stores.")]
        [SerializeField]
        private Entry[] entries;

        public int Count => entries == null ? 0 : entries.Length;

        /// <summary>
        /// Looks a colour's skin up. A linear scan over at most 12 rows, run once per
        /// block at spawn (≤128 blocks, once per level load): 12 integer comparisons
        /// against a dictionary's hashing and its own allocation — the cost model picks
        /// the readable one without hesitation.
        /// </summary>
        public bool TryGetSkin(BlockColourId colour, out BlockSkin skin)
        {
            Entry[] rows = entries ?? NoEntries;

            for (int row = 0; row < rows.Length; row++)
            {
                if (rows[row].colourId == colour.Value && rows[row].skin != null)
                {
                    skin = rows[row].skin;
                    return true;
                }
            }

            skin = null;
            return false;
        }

        /// <summary>
        /// The skin for a colour the level uses. A missing entry is a content defect,
        /// not a runtime condition to survive: a board would spawn invisible bricks.
        /// </summary>
        public BlockSkin GetSkin(BlockColourId colour)
        {
            BlockSkin skin;
            if (!TryGetSkin(colour, out skin))
            {
                throw new InvalidOperationException(name + " has no skin for " + colour + ".");
            }

            return skin;
        }

        public bool Validate(out string error)
        {
            Entry[] rows = entries ?? NoEntries;
            int seenIds = 0;

            for (int row = 0; row < rows.Length; row++)
            {
                int id = rows[row].colourId;

                if (id < BlockColourId.MinId || id > BlockColourId.MaxId)
                {
                    error = "Row " + row + " carries colour id " + id + ", which is outside " +
                            BlockColourId.MinId + ".." + BlockColourId.MaxId + ".";
                    return false;
                }

                int bit = 1 << id;
                if ((seenIds & bit) != 0)
                {
                    error = "Colour id " + id + " appears twice, so two rows would decide what it looks like.";
                    return false;
                }

                seenIds |= bit;

                if (rows[row].skin == null)
                {
                    error = "Colour id " + id + " has no skin assigned.";
                    return false;
                }

                if (!rows[row].skin.IsAssigned)
                {
                    error = "The skin for colour id " + id + " (" + rows[row].skin.name + ") is missing its mesh or material.";
                    return false;
                }
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
