using System;

namespace ColorfulSort.Core
{
    /// <summary>
    /// The one legal source of an attempt seed. Gameplay randomness comes only from the
    /// attempt's seeded RNG (CLAUDE.md invariant), which means something has to decide
    /// the seed — and if that decision reads the clock, a board can never be reproduced
    /// from a bug report and two attempts can never be compared.
    /// <para>
    /// So the seed is a pure function of <em>which level</em> and <em>which attempt of
    /// it</em>: attempt 3 of level 12 is the same seed on every device, forever. The
    /// attempt ordinal is the <c>plays</c> counter in the save file, so it advances once
    /// per opened level and nothing else has to be stored.
    /// </para>
    /// <para>
    /// <c>Board</c> has its own mixer inside <c>DeterministicRandom</c>. This one is not
    /// shared with it on purpose: <c>Core</c> depends on nothing (blueprint), and its
    /// assembly cannot see <c>Board</c> — a six-line finaliser is a cheaper price than a
    /// dependency arrow that the plan does not have.
    /// </para>
    /// </summary>
    public static class AttemptSeedSource
    {
        /// <summary>
        /// The seed for one attempt. Both arguments are ordinals, so both are
        /// non-negative; a negative one is a caller bug, not a value to mix.
        /// </summary>
        public static int For(int levelOrdinal, int attemptOrdinal)
        {
            if (levelOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelOrdinal), levelOrdinal, "A level ordinal is never negative.");
            }

            if (attemptOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptOrdinal), attemptOrdinal, "An attempt ordinal is never negative.");
            }

            ulong packed = ((ulong)(uint)levelOrdinal << 32) | (uint)attemptOrdinal;

            // The high half of the mixed value: splitmix64's finaliser avalanches into
            // all 64 bits, and taking the top 32 keeps neighbouring attempts of one level
            // as far apart as attempts of different levels. The truncation to a signed
            // int is the intended reinterpretation, so it is explicitly unchecked — a seed
            // is a bit pattern, not a quantity.
            unchecked
            {
                return (int)(uint)(Mix(packed) >> 32);
            }
        }

        /// <summary>splitmix64 finaliser — a full-avalanche hash of (level, attempt).</summary>
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
