using ColorfulSort.View;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.View.Tests
{
    /// <summary>
    /// The formula that turns a brick's colour into light. It is pinned because it was arrived at the
    /// hard way — three attempts drew every glow white and a fourth drew it pale — and because it now
    /// lights two things, the lifted run's glow and a flying brick's trail (D-063, D-073). A change
    /// here changes both, which is the point of there being one copy of it.
    /// </summary>
    [TestFixture]
    public sealed class BlockNeonTests
    {
        [Test]
        public void NeonOf_KeepsTheHueAndRaisesOnlyItsBrightness()
        {
            // A muted pink: the ratios between the channels are the hue, and they must survive.
            var muted = new Color(0.5f, 0.2f, 0.3f, 1f);

            Color neon = BlockView.NeonOf(muted, 0f);

            Assert.That(neon.r, Is.EqualTo(1f).Within(0.0001f), "the strongest channel is taken to full");
            Assert.That(neon.g / neon.r, Is.EqualTo(muted.g / muted.r).Within(0.0001f));
            Assert.That(neon.b / neon.r, Is.EqualTo(muted.b / muted.r).Within(0.0001f));
        }

        [Test]
        public void NeonOf_IsBrighterThanTheBrick()
        {
            var muted = new Color(0.4f, 0.1f, 0.25f, 1f);

            Color neon = BlockView.NeonOf(muted, 0f);

            Assert.That(neon.r, Is.GreaterThan(muted.r));
            Assert.That(neon.g, Is.GreaterThan(muted.g));
            Assert.That(neon.b, Is.GreaterThan(muted.b));
        }

        [Test]
        public void NeonOf_WithALift_AddsAWhiteCoreWithoutWashingTheHueOut()
        {
            var blue = new Color(0.1f, 0.2f, 0.6f, 1f);

            Color plain = BlockView.NeonOf(blue, 0f);
            Color lifted = BlockView.NeonOf(blue, 0.15f);

            Assert.That(lifted.r, Is.GreaterThan(plain.r), "the weak channels come up, which is the hot core");

            // Still recognisably blue: the channel that was strongest stays the strongest, and the
            // gap between it and the weakest has not closed. A lift that closed it would be the
            // desaturation this formula exists to avoid.
            Assert.That(lifted.b, Is.GreaterThanOrEqualTo(lifted.r));
            Assert.That(lifted.b - lifted.r, Is.GreaterThan(0.5f));
        }

        [Test]
        public void NeonOf_AtFullLift_IsWhite()
        {
            Color white = BlockView.NeonOf(new Color(0.1f, 0.2f, 0.6f, 1f), 1f);

            Assert.That(white.r, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(white.g, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(white.b, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void NeonOf_WithNoHueToRaise_LeavesTheColourAlone()
        {
            // The `?` brick is near black and the placeholder is white: neither has a hue to take to
            // full value, and dividing by nothing is how this used to produce a colour of infinities.
            var black = new Color(0f, 0f, 0f, 1f);

            Assert.That(BlockView.NeonOf(black, 0f), Is.EqualTo(black));

            Color white = BlockView.NeonOf(Color.white, 0f);

            Assert.That(white.r, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(white.g, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(white.b, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void NeonOf_KeepsTheAlphaItWasGiven()
        {
            Assert.That(BlockView.NeonOf(new Color(0.4f, 0.1f, 0.25f, 0.5f), 0f).a, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
