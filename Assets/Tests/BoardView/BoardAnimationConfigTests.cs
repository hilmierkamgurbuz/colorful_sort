using ColorfulSort.View;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.View.Tests
{
    /// <summary>
    /// The two bursts' numbers, and deliberately only those. The rest of this asset's ranges were
    /// written next to the effects they guard and are judged on screen — but these arrived after the
    /// asset did, and an asset authored before a field exists carries **zero** for it. If zero were
    /// refused outright, the whole animation config would report invalid over a number nobody had ever
    /// set, and an invalid config is a board that snaps instead of moving (D-077).
    /// <para>
    /// The one place zero is not free is a duration with particles behind it: the placement burst's
    /// speed is its height divided by its seconds, so a count above zero with no duration is a division
    /// by zero, and a finish spark with no lifetime is emitted and gone in the same frame. Those two
    /// are the rules worth a test — they fail *silently*, as nothing on screen (D-078).
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class BoardAnimationConfigTests
    {
        /// <summary>
        /// The smallest asset the validator accepts, in the shape Unity serialises one. Only the fields
        /// it insists are positive appear; everything else is left at zero, which is what an untuned but
        /// legal config looks like — and what an asset written before either burst existed looks like.
        /// </summary>
        private const string BeforeTheBursts =
            "{\"liftDuration\":0.12,\"travelDuration\":0.22,\"dropDuration\":0.1," +
            "\"celebrationSymbolGlow\":3," +
            "\"settleDuration\":1.2,\"settleShade\":0.75,\"settleShadowAlpha\":1}";

        /// <summary>
        /// What `Data/Config/BoardAnimationConfig.asset` actually ships. Pinned so the tuned feel and
        /// the validator cannot drift apart without a red test.
        /// </summary>
        private const string AsShipped =
            "\"landingSparks\":6,\"landingRiseHeight\":2,\"landingRiseSeconds\":1.6,\"landingSparkDrop\":0.35," +
            "\"landingSparkSize\":0.35,\"landingSparkSpread\":0.8,\"landingSparkWander\":0.25," +
            "\"landingSparkDip\":0.5,\"landingSparkScatter\":0.9," +
            "\"celebrationSparks\":8,\"celebrationBurstRise\":6,\"celebrationBurstSeconds\":1.1," +
            "\"celebrationGlowFade\":0.6,\"celebrationGlowHold\":0.12," +
            "\"celebrationSparkSize\":0.45,\"celebrationBurstSpread\":1,\"celebrationBurstScatter\":1.2," +
            "\"revealFadeDuration\":0.45";

        private BoardAnimationConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<BoardAnimationConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
            config = null;
        }

        [Test]
        public void Bursts_LeftUnauthored_IsStillALegalConfig()
        {
            Overwrite(BeforeTheBursts);

            string error;

            Assert.That(
                config.Validate(out error),
                Is.True,
                "an asset written before these fields existed reads them as 0, and 0 is a burst turned " +
                "off — refusing it would invalidate the whole move over a number nobody set: " + error);
        }

        [Test]
        public void Bursts_AsShipped_IsALegalConfig()
        {
            Overwrite(BeforeTheBursts, AsShipped);

            string error;

            Assert.That(config.Validate(out error), Is.True, error);
        }

        [Test]
        public void RevealFade_LeftUnauthored_IsLegalAndMeansNoFade()
        {
            // The `?` fade is the newest field in this asset, so it is the one most likely to be zero in
            // somebody's copy — and it was renamed once already (D-101 replaced D-099's turn), which is
            // exactly the kind of edit that silently drops a serialised value. Zero has to stay legal: it
            // is the reveal this project shipped before any of this existed, and refusing it would
            // invalidate the whole config over a look rather than a fault. The rule this file keeps is
            // that zero is refused only where it fails *silently*.
            Overwrite(BeforeTheBursts);

            string error;

            Assert.That(config.Validate(out error), Is.True, error);
            Assert.That(config.RevealFadeDuration, Is.EqualTo(0f), "an unauthored fade is no fade");
        }

        [Test]
        public void LandingBurst_WithSparksButNoSeconds_IsRefused()
        {
            // The speed handed to the burst is height ÷ seconds. Zero here is a division by zero, and
            // what a player would see is simply no sparks — a fault with nothing in the console.
            Overwrite(BeforeTheBursts, "\"landingSparks\":12,\"landingRiseHeight\":2,\"landingRiseSeconds\":0");

            AssertRefused("divided by that");
        }

        [Test]
        public void FinishBurst_WithSparksButNoLifetime_IsRefused()
        {
            // Emitted and gone in the frame it was made, which reads exactly like the burst never firing.
            Overwrite(BeforeTheBursts, "\"celebrationSparks\":14,\"celebrationBurstSeconds\":0");

            AssertRefused("nobody sees");
        }

        [Test]
        public void LandingBurst_WithNegativeHeight_IsRefused()
        {
            // Negative would drive the sparks down into the board, where the column art hides them.
            Overwrite(BeforeTheBursts, "\"landingSparks\":12,\"landingRiseHeight\":-2,\"landingRiseSeconds\":0.9");

            AssertRefused("cells up");
        }

        [Test]
        public void LandingBurst_WithNegativeDrop_IsRefused()
        {
            // Negative would start the burst *above* the brick, which is the one place it must not be.
            Overwrite(BeforeTheBursts, "\"landingSparks\":12,\"landingRiseSeconds\":0.9,\"landingSparkDrop\":-0.35");

            AssertRefused("under the brick");
        }

        [Test]
        public void FinishBurst_WithNegativeRise_IsRefused()
        {
            Overwrite(BeforeTheBursts, "\"celebrationBurstRise\":-3,\"celebrationBurstSeconds\":0.9");

            AssertRefused("finish burst rises");
        }

        [Test]
        public void GlowFade_Negative_IsRefused()
        {
            // A negative fade would run the lerp backwards and leave the symbols brighter than they
            // started, on a column that is finished and about to darken.
            Overwrite(BeforeTheBursts, "\"celebrationGlowFade\":-0.6");

            AssertRefused("fading over");
        }

        // A size is what the two bursts are judged on and the reason it lives in the asset at all
        // (D-081) — but a negative one is a mesh turned inside out, not a smaller spark.

        [Test]
        public void LandingBurst_WithNegativeSize_IsRefused()
        {
            Overwrite(BeforeTheBursts, "\"landingSparks\":12,\"landingRiseSeconds\":0.9,\"landingSparkSize\":-0.35");

            AssertRefused("cells up");
        }

        [Test]
        public void FinishBurst_WithNegativeSize_IsRefused()
        {
            Overwrite(BeforeTheBursts, "\"celebrationSparkSize\":-0.45");

            AssertRefused("cells across");
        }

        [Test]
        public void LandingDip_AtOrPastTheWholeClimb_IsRefused()
        {
            // The dip is a share of the climbing speed, and the climb's *length* is the curve's area
            // divided out of the authored height. Dip as hard as you climb and that area collapses
            // towards nothing, then flips: the sparks would sink instead of rise, from a number that
            // reads like a harmless "a bit more" (D-082).
            Overwrite(BeforeTheBursts, "\"landingSparkDip\":1");

            AssertRefused("share of the climb");
        }

        [Test]
        public void LandingBurst_WithNegativeSpreadOrWander_IsRefused()
        {
            Overwrite(BeforeTheBursts, "\"landingSparks\":6,\"landingRiseSeconds\":1.6,\"landingSparkSpread\":-0.8");

            AssertRefused("cells up");
        }

        [Test]
        public void FinishBurst_WithNegativeSpread_IsRefused()
        {
            Overwrite(BeforeTheBursts, "\"celebrationBurstSpread\":-0.7");

            AssertRefused("cells;");
        }

        [Test]
        public void Scatter_Negative_IsRefused()
        {
            // Scatter is a distance either side of the climb, so a negative one is the same band read
            // backwards — legal arithmetic that says nothing anybody meant (D-083).
            Overwrite(BeforeTheBursts, "\"landingSparks\":6,\"landingRiseSeconds\":1.6,\"landingSparkScatter\":-0.9");

            AssertRefused("cells up");
        }

        private void AssertRefused(string mentions)
        {
            string error;

            Assert.That(config.Validate(out error), Is.False, "that burst cannot be drawn, so it is not a look");
            Assert.That(error, Does.Contain(mentions), "the complaint has to name what is wrong: " + error);
        }

        /// <summary>
        /// Writes a JSON body over the asset's serialised fields, optionally with extra members spliced
        /// in — which is how a test states "this asset, but with one number changed" without a second
        /// copy of every field it does not care about.
        /// </summary>
        private void Overwrite(string body, string extra = null)
        {
            if (!string.IsNullOrEmpty(extra))
            {
                body = body.Substring(0, body.Length - 1) + "," + extra + "}";
            }

            JsonUtility.FromJsonOverwrite(body, config);
        }
    }
}
