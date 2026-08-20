using System;
using System.Collections.Generic;
using ColorfulSort.Board;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// One authored cell. Public fields on purpose: this is a serialized row the
    /// level editor writes, and Unity draws it in the Inspector without ceremony.
    /// </summary>
    [Serializable]
    public struct CellDefinition
    {
        [Tooltip("Logical colour id. What it looks like is decided by the BlockSkinSet, never here.")]
        [Range(BlockColourId.MinId, BlockColourId.MaxId)]
        public int colourId;

        [Tooltip("Hidden cells: every cell of a covered column, and the '?' cells below a mystery column's top.")]
        public bool hidden;
    }

    /// <summary>
    /// One authored column, as the level editor stores it. It converts itself into
    /// <see cref="ColumnData"/> — the rules never read a serialized field, and the
    /// rules are the ones that decide whether a shape is legal, so this type carries
    /// no validation of its own.
    /// </summary>
    [Serializable]
    public sealed class ColumnDefinition
    {
        private static readonly CellDefinition[] NoCells = new CellDefinition[0];

        [SerializeField]
        private ColumnKind kind;

        [Tooltip("How many cells this column holds. Per-level data: Level 79's columns hold 4.")]
        [SerializeField]
        private int capacity;

        [Tooltip("Contents bottom-up: the first entry is the bottom cell.")]
        [SerializeField]
        private CellDefinition[] cells;

        [Tooltip("Ice only: the column thaws once this many colours have been completed.")]
        [SerializeField]
        private int thawAfterCompletions;

        [Tooltip("Covered only: the key colour whose completion opens this cover. 0 for none.")]
        [Range(0, BlockColourId.MaxId)]
        [SerializeField]
        private int coverKeyColourId;

        /// <summary>
        /// Builds a column from decoded data. <c>internal</c>, so only <c>Content</c> can make one —
        /// the file is the authority for level content, and a setter anybody could reach would be a
        /// second way to write it.
        /// </summary>
        internal static ColumnDefinition Create(
            ColumnKind kind,
            int capacity,
            CellDefinition[] cells,
            int thawAfterCompletions,
            int coverKeyColourId)
        {
            return new ColumnDefinition
            {
                kind = kind,
                capacity = capacity,
                cells = cells ?? NoCells,
                thawAfterCompletions = thawAfterCompletions,
                coverKeyColourId = coverKeyColourId,
            };
        }

        public ColumnKind Kind => kind;

        public int Capacity => capacity;

        public int ThawAfterCompletions => thawAfterCompletions;

        public int CoverKeyColourId => coverKeyColourId;

        public IReadOnlyList<CellDefinition> Cells => cells ?? NoCells;

        /// <summary>
        /// Converts to the rules' plain-C# shape. Throws when the authored column is
        /// illegal — the message comes from <see cref="ColumnData"/>, which is the one
        /// place those rules live.
        /// </summary>
        public ColumnData ToColumnData()
        {
            CellDefinition[] authored = cells ?? NoCells;
            var converted = new CellData[authored.Length];

            for (int cell = 0; cell < authored.Length; cell++)
            {
                converted[cell] = new CellData(new BlockColourId(authored[cell].colourId), authored[cell].hidden);
            }

            BlockColourId key = coverKeyColourId == 0 ? BlockColourId.None : new BlockColourId(coverKeyColourId);
            return new ColumnData(kind, capacity, converted, thawAfterCompletions, key);
        }
    }
}
