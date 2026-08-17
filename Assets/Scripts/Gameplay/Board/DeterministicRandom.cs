using System;

namespace ColorfulSort.Board
{
    /// <summary>
    /// The attempt's only source of randomness (fingerprint.md → Determinism).
    /// It is <em>counter-based</em>: the entire state is <c>(Seed, Cursor)</c>, so
    /// undo restores it by setting an integer back rather than by snapshotting an
    /// internal buffer — which is what makes "undo is exact" provable.
    /// <para>
    /// <c>UnityEngine.Random</c> is unavailable here (this assembly has no engine
    /// reference) and <c>System.Random</c> is deliberately not used: its sequence
    /// is not guaranteed stable across runtimes, and it cannot be rewound.
    /// </para>
    /// </summary>
    public sealed class DeterministicRandom
    {
        /// <summary>The attempt seed. Stored with the attempt so a board can be replayed.</summary>
        public int Seed { get; }

        /// <summary>How many values have been drawn. This is the whole mutable state.</summary>
        public int Cursor { get; private set; }

        public DeterministicRandom(int seed, int cursor = 0)
        {
            if (cursor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cursor), cursor, "A cursor is never negative.");
            }

            Seed = seed;
            Cursor = cursor;
        }

        /// <summary>
        /// Draws a value in <c>[0, exclusiveMax)</c> and advances the cursor by one.
        /// Every call must be recorded in the move history by the caller, or undo
        /// cannot reproduce what it undid (D-002).
        /// </summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), exclusiveMax, "The exclusive upper bound is at least 1.");
            }

            ulong bits = Mix(((ulong)(uint)Seed << 32) | (uint)Cursor);
            Cursor = checked(Cursor + 1);

            // Modulo bias over a 64-bit draw with exclusiveMax <= 128 (fingerprint's
            // largest gameplay range is the block count) is below 2^-56 — far under
            // anything observable, and the alternative costs a rejection loop whose
            // draw count would itself have to be recorded for undo.
            return (int)(bits % (ulong)exclusiveMax);
        }

        /// <summary>
        /// Restores the cursor to an earlier position. Rewinding forward is
        /// rejected: undo may only give draws back, never invent them.
        /// </summary>
        public void Rewind(int cursor)
        {
            if (cursor < 0 || cursor > Cursor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cursor), cursor, "A rewind only moves the cursor back; it is currently " + Cursor + ".");
            }

            Cursor = cursor;
        }

        /// <summary>splitmix64 finaliser — a full-avalanche hash of (seed, cursor).</summary>
        private static ulong Mix(ulong value)
        {
            unchecked
            {
                ulong z = value + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
