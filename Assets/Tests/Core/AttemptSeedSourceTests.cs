using System.Collections.Generic;
using NUnit.Framework;

namespace ColorfulSort.Core.Tests
{
    /// <summary>
    /// The seed is the one number that decides what a Shuffle booster will do, so
    /// "attempt 3 of level 12" has to mean the same board on every device and in every
    /// build. The golden values below are that promise written down: they are the
    /// contract, not an implementation detail, and changing the mixer is supposed to
    /// break this fixture loudly.
    /// </summary>
    [TestFixture]
    public sealed class AttemptSeedSourceTests
    {
        [Test]
        public void For_IsStableAcrossBuilds()
        {
            Assert.That(AttemptSeedSource.For(0, 0), Is.EqualTo(-501176263));
            Assert.That(AttemptSeedSource.For(0, 1), Is.EqualTo(-1861603860));
            Assert.That(AttemptSeedSource.For(1, 0), Is.EqualTo(-1003726310));
            Assert.That(AttemptSeedSource.For(12, 3), Is.EqualTo(1547524612));
        }

        [Test]
        public void For_TheSameAttempt_IsTheSameSeedEveryTime()
        {
            Assert.That(AttemptSeedSource.For(9, 4), Is.EqualTo(AttemptSeedSource.For(9, 4)));
        }

        [Test]
        public void For_EveryAttemptOfEveryLevel_GetsItsOwnSeed()
        {
            // 8 levels x 8 attempts, all distinct. The seed is 32 bits wide, so somewhere
            // past a few tens of thousands of pairs a collision is a birthday certainty —
            // and harmless, since two attempts sharing a shuffle stream is invisible. What
            // must not happen is a pattern: neighbouring attempts of one level, or the same
            // attempt of neighbouring levels, landing on the same seed.
            List<int> seeds = new List<int>();

            for (int levelOrdinal = 0; levelOrdinal < 8; levelOrdinal++)
            {
                for (int attemptOrdinal = 0; attemptOrdinal < 8; attemptOrdinal++)
                {
                    seeds.Add(AttemptSeedSource.For(levelOrdinal, attemptOrdinal));
                }
            }

            Assert.That(seeds, Is.Unique);
        }

        [Test]
        public void For_ANegativeOrdinal_IsACallerBug()
        {
            Assert.That(() => AttemptSeedSource.For(-1, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => AttemptSeedSource.For(0, -1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
