using ColorfulSort.Core;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.Core.Tests
{
    /// <summary>
    /// The frame-rate ceiling, and specifically the two values that are easy to get wrong in
    /// opposite directions: zero, which has to stay legal, and a negative, which must not.
    /// <para>
    /// Zero is the interesting one. It reads as "leave the platform's own rate alone", which is both a
    /// legal authoring choice and what an asset created before this field existed carries — so
    /// refusing it would make a config invalid over a number nobody set. But it is *not* a neutral
    /// outcome, and that is the whole reason this asset exists: Unity's own default is the platform
    /// rate, and on mobile that is 30 fps. A test that only checked "does it validate" would miss
    /// that, so these also pin what the accessors report (D-100).
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DisplayConfigTests
    {
        private DisplayConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<DisplayConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
            config = null;
        }

        [Test]
        public void Unauthored_IsLegalAndAsksForNoCap()
        {
            string error;

            Assert.That(config.Validate(out error), Is.True, error);
            Assert.That(config.CapsFrameRate, Is.False, "an unauthored rate asks for nothing");
            Assert.That(
                config.TargetFrameRate,
                Is.EqualTo(DisplayConfig.PlatformDefault),
                "with nothing authored the platform's own rate stands — which is 30 on mobile, not 60");
        }

        [Test]
        public void AuthoredRate_IsReportedAsACap()
        {
            Overwrite("{\"targetFrameRate\":120}");

            string error;

            Assert.That(config.Validate(out error), Is.True, error);
            Assert.That(config.CapsFrameRate, Is.True);
            Assert.That(config.TargetFrameRate, Is.EqualTo(120));
        }

        [Test]
        public void NegativeRate_IsRefused()
        {
            // Neither a ceiling nor a hand-off to the platform: it would reach the engine as an
            // arbitrary number and mean nothing, which is the one case worth refusing outright.
            Overwrite("{\"targetFrameRate\":-30}");

            string error;

            Assert.That(config.Validate(out error), Is.False, "a negative rate was accepted");
            Assert.That(error, Does.Contain("positive ceiling"));
        }

        private void Overwrite(string body)
        {
            JsonUtility.FromJsonOverwrite(body, config);
        }
    }
}
