using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// A logical block colour, exactly as level data stores it: an id and nothing
    /// else. What the id looks like — the material and the embossed symbol mesh —
    /// is owned by Content's single <c>BlockSkinSet</c> asset (D-003), so a
    /// re-skin never reaches this assembly.
    /// </summary>
    public readonly struct BlockColourId : IEquatable<BlockColourId>
    {
        /// <summary>Lowest valid id; 0 is reserved for <see cref="None"/>.</summary>
        public const int MinId = 1;

        /// <summary>
        /// Highest valid id. Structural bound from fingerprint.md — at most 12
        /// distinct colours per level, one per available symbol mesh. This is a
        /// validation limit on the data contract, not a tuning number, which is
        /// why it lives here and not in <c>Data/Config/</c>.
        /// </summary>
        public const int MaxId = 12;

        /// <summary>
        /// The absence of a block. Empty cells are never stored — a column simply
        /// holds fewer cells — so this is what a query answers with when there is
        /// no block to report.
        /// </summary>
        public static readonly BlockColourId None = default;

        /// <summary>0 for <see cref="None"/>, otherwise <see cref="MinId"/>..<see cref="MaxId"/>.</summary>
        public readonly int Value;

        public BlockColourId(int value)
        {
            if (value != 0 && (value < MinId || value > MaxId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "A colour id is 0 (none) or " + MinId + ".." + MaxId + ".");
            }

            Value = value;
        }

        public bool IsNone => Value == 0;

        public bool Equals(BlockColourId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is BlockColourId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => IsNone ? "colour:none" : "colour:" + Value;

        public static bool operator ==(BlockColourId left, BlockColourId right) => left.Value == right.Value;

        public static bool operator !=(BlockColourId left, BlockColourId right) => left.Value != right.Value;
    }
}
