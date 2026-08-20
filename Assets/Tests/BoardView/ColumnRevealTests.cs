using ColorfulSort.View;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.View.Tests
{
    /// <summary>
    /// The colours a `?` brick travels through as it becomes what it really was.
    /// <para>
    /// There is one property here that the whole effect rests on, and it is the reason this maths is a
    /// pure static function rather than something inside the coroutine: **at the midpoint the symbol is
    /// exactly the body's colour**. The symbol is embossed geometry, so the shape changes with the skin
    /// no matter what the colours do; painting it its body's colour is the only thing making that shape
    /// change unreadable, and the skin is swapped at precisely that moment. Break the property and the
    /// swap becomes visible — a fault nobody catches by eye in a 0.45-second blend (D-101).
    /// </para>
    /// <para>
    /// The second one worth pinning is continuity across that midpoint. The function is written in two
    /// branches, and two branches are exactly where a seam appears: if they disagree at 0.5 the brick
    /// flickers on the frame the skin changes, which reads as the pop this change existed to remove.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ColumnRevealTests
    {
        // Deliberately far apart on every channel, so an assertion cannot pass by two colours happening
        // to be close. The `?` is a near-black brick with a white mark (D-099); the real one is a bright
        // colour with its own darker symbol (D-052).
        private static readonly Color HiddenBody = new Color(0.16f, 0.16f, 0.21f, 1f);
        private static readonly Color HiddenSymbol = Color.white;
        private static readonly Color Body = new Color(0f, 0.5f, 1f, 1f);
        private static readonly Color Symbol = new Color(0f, 0.33f, 0.65f, 1f);

        private BoardAnimationConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<BoardAnimationConfig>();
            JsonUtility.FromJsonOverwrite("{\"revealFadeDuration\":0.45}", config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
            config = null;
        }

        [Test]
        public void AtTheStart_TheBrickIsExactlyTheQuestionMark()
        {
            Color body;
            Color symbol;
            ColumnView.RevealColours(Look(), 0f, out body, out symbol);

            AssertColour(body, HiddenBody, "the body starts on the ? brick's own colour");
            AssertColour(symbol, HiddenSymbol, "the symbol starts on the ? mark's own colour");
        }

        [Test]
        public void AtTheEnd_TheBrickIsExactlyTheRealSkin()
        {
            Color body;
            Color symbol;
            ColumnView.RevealColours(Look(), 1f, out body, out symbol);

            // Exact, because the end is where the painted override is CLEARED and the material's own
            // colours take over. A blend that ended a shade off would show as a one-frame step.
            AssertColour(body, Body, "the body has to land on the material's own colour");
            AssertColour(symbol, Symbol, "the symbol has to land on the material's own colour");
        }

        [Test]
        public void AtTheMidpoint_TheSymbolIsTheBodyColour()
        {
            Color body;
            Color symbol;
            ColumnView.RevealColours(Look(), 0.5f, out body, out symbol);

            AssertColour(
                symbol,
                body,
                "the skin is swapped here, and only a symbol with no contrast hides the mesh change");

            // And the body has already arrived, so the swap — which brings the real material's own
            // colour with it — does not step either.
            AssertColour(body, Body, "the body finishes its journey by the midpoint");
        }

        [Test]
        public void AcrossTheMidpoint_ThereIsNoSeam()
        {
            const float Hair = 0.001f;

            Color bodyBefore;
            Color symbolBefore;
            ColumnView.RevealColours(Look(), 0.5f - Hair, out bodyBefore, out symbolBefore);

            Color bodyAfter;
            Color symbolAfter;
            ColumnView.RevealColours(Look(), 0.5f + Hair, out bodyAfter, out symbolAfter);

            AssertColour(bodyAfter, bodyBefore, "the body jumped where the two branches meet", 0.01f);
            AssertColour(symbolAfter, symbolBefore, "the symbol jumped where the two branches meet", 0.01f);
        }

        [Test]
        public void BeyondTheEnds_IsClampedToThem()
        {
            Color body;
            Color symbol;

            ColumnView.RevealColours(Look(), -3f, out body, out symbol);
            AssertColour(body, HiddenBody, "a negative progress is the start, not a colour off the scale");

            ColumnView.RevealColours(Look(), 4f, out body, out symbol);
            AssertColour(body, Body, "a progress past 1 is the end");
            AssertColour(symbol, Symbol, "a progress past 1 is the end");
        }

        private RevealLook Look()
        {
            return RevealLook.From(config, HiddenBody, HiddenSymbol, Body, Symbol);
        }

        private static void AssertColour(Color actual, Color expected, string because, float tolerance = 0.0001f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), because + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), because + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), because + " (b)");
        }
    }
}
