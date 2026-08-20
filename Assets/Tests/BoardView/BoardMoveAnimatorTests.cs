using ColorfulSort.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorfulSort.View.Tests
{
    /// <summary>
    /// The entry path. It is tested for two reasons. A path that ends anywhere but on the cell leaves
    /// a brick off the board, and a brick a fraction out of its slot is exactly the fault that looks
    /// fine in motion and wrong in a screenshot. And the path's *shape* is the feature: three straight
    /// legs — up out of its own column, across, down through the mouth (D-081).
    /// <para>
    /// The shape assertions are the interesting ones, because the fault they replace was invisible to
    /// every test the old path had. It went from origin to apex by driving x and y from one eased
    /// value, which satisfied "starts here, ends there" perfectly while the brick left its column on a
    /// diagonal and came at the slot from the side. So these tests assert what must *not* happen in
    /// the middle: x may not move while the brick is still climbing, y may not move while it is
    /// crossing, and x must already be the target's for the whole descent.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class BoardMoveAnimatorTests
    {
        private static readonly Vector3 Origin = new Vector3(-2f, 3f, 0f);
        private static readonly Vector3 Target = new Vector3(1.5f, 1f, 0f);
        private const float Apex = 5f;

        /// <summary>Fine enough to catch a leg bleeding into its neighbour, coarse enough to run fast.</summary>
        private const int Samples = 400;

        [Test]
        public void EntryPoint_StartsAtTheBrickAndEndsOnItsCell()
        {
            Assert.That(Point(0f), Is.EqualTo(Origin));

            Vector3 landed = Point(1f);

            Assert.That(landed.x, Is.EqualTo(Target.x).Within(0.0001f));
            Assert.That(landed.y, Is.EqualTo(Target.y).Within(0.0001f), "a brick that does not land on its cell is off the board");
            Assert.That(landed.z, Is.EqualTo(Target.z).Within(0.0001f));
        }

        [Test]
        public void EntryPoint_BeyondTheEnds_IsClampedToThem()
        {
            // The staggered flight feeds it progress computed from elapsed time, which arrives negative
            // for a brick still waiting its turn and above 1 on the frame that overshoots.
            Assert.That(Point(-0.4f), Is.EqualTo(Origin));
            Assert.That(Point(1.6f).y, Is.EqualTo(Target.y).Within(0.0001f));
        }

        [Test]
        public void EntryPoint_HasExactlyTwoCorners()
        {
            // The whole complaint, counted. A step that moves sideways *and* vertically is either a
            // corner or a diagonal, and a three-leg path has exactly two corners — so counting them is
            // the same assertion as "no diagonals", without pretending a sampled corner is sharp.
            Vector3 previous = Point(0f);
            int corners = 0;

            for (int i = 1; i <= Samples; i++)
            {
                Vector3 next = Point(i / (float)Samples);
                float sideways = Mathf.Abs(next.x - previous.x) + Mathf.Abs(next.z - previous.z);
                float vertical = Mathf.Abs(next.y - previous.y);

                if (sideways > 0.0005f && vertical > 0.0005f)
                {
                    corners++;
                }

                previous = next;
            }

            Assert.That(
                corners,
                Is.LessThanOrEqualTo(2),
                corners + " steps moved sideways and vertically at once; a path of three straight legs turns twice");
        }

        [Test]
        public void EntryPoint_RisesClearOfItsOwnColumnBeforeItMovesAcross()
        {
            bool cleared = false;

            for (int i = 0; i <= Samples; i++)
            {
                Vector3 point = Point(i / (float)Samples);

                if (point.y >= Apex - 0.0005f)
                {
                    cleared = true;
                    continue;
                }

                // Until it has reached the crossing height it is still inside the column it is leaving.
                // A brick that has drifted sideways by here is passing through whatever stands above it
                // — the fault a straight rise makes visible and a diagonal one hid.
                if (!cleared)
                {
                    Assert.That(
                        point.x,
                        Is.EqualTo(Origin.x).Within(0.001f),
                        "at x " + point.x + " and height " + point.y + ", still below the apex " + Apex);
                }
            }

            Assert.That(cleared, Is.True, "the brick never reached the crossing height");
        }

        [Test]
        public void EntryPoint_ComesDownOverTheMouthAndNeverThroughTheSide()
        {
            bool descending = false;
            float last = Point(0f).y;

            for (int i = 1; i <= Samples; i++)
            {
                Vector3 point = Point(i / (float)Samples);
                descending |= point.y < last - 0.0005f;
                last = point.y;

                if (descending)
                {
                    Assert.That(
                        point.x,
                        Is.EqualTo(Target.x).Within(0.001f),
                        "descending at x " + point.x + " rather than over the slot's mouth");
                }
            }

            Assert.That(descending, Is.True, "the brick never came down at all");
        }

        [Test]
        public void EntryPoint_ReachesTheApexAndNeverPassesIt()
        {
            float highest = float.MinValue;

            for (int i = 0; i <= Samples; i++)
            {
                highest = Mathf.Max(highest, Point(i / (float)Samples).y);
            }

            Assert.That(highest, Is.EqualTo(Apex).Within(0.001f), "the crossing height is the apex, not a suggestion");
        }

        [Test]
        public void EntryPoint_IntoALowerRow_NeverDipsBelowEitherEnd()
        {
            // A move down a row: the apex handed in is lower than where the brick starts, so the path
            // has to raise it itself or the brick would sink through the board on its way across.
            for (int i = 0; i <= Samples; i++)
            {
                Vector3 point = BoardMoveAnimator.EntryPoint(Origin, Target, 1f, i / (float)Samples);

                Assert.That(point.y, Is.GreaterThanOrEqualTo(Mathf.Min(Origin.y, Target.y) - 0.0005f));
                Assert.That(point.y, Is.LessThanOrEqualTo(Mathf.Max(Origin.y, Target.y) + 0.0005f));
            }
        }

        [Test]
        public void EntryPoint_WithNowhereToGo_IsTheDestination()
        {
            // Guarded because the leg lengths are what the timing is divided by: a path of no length
            // would divide by zero rather than simply be over.
            Assert.That(BoardMoveAnimator.EntryPoint(Target, Target, Target.y, 0.5f), Is.EqualTo(Target));
        }

        /// <summary>
        /// A motion's completion runs **exactly once**, and <see cref="BoardMoveAnimator.FinishNow"/>
        /// cannot fire one that has already run.
        /// <para>
        /// This is the failure mode worth a test rather than an eye: the completion is what seats a
        /// run in its column, so running it twice seats the same bricks twice and running it never
        /// leaves them owned by nobody (D-098). Both are silent on screen for one move and wrong on
        /// the next.
        /// </para>
        /// <para>
        /// What it does **not** cover, stated rather than implied: the coroutine path. Reaching it
        /// needs an authored <c>BoardAnimationConfig</c> on the animator's serialised field, and
        /// that field takes an object reference — settable only by reflection or by a seam in
        /// production code that no feature has asked for. So this exercises the unauthored-config
        /// path, where the animator places the bricks and completes immediately, and asserts the
        /// once-only contract there. Interrupting a real flight is verified in play.
        /// </para>
        /// </summary>
        [Test]
        public void Completion_RunsExactlyOnce_AndFinishNowCannotRunItAgain()
        {
            // The animator says out loud that its config is missing and places the bricks anyway —
            // deliberate ("a correct board with a console line beats a silent freeze"), and not what
            // this test is about.
            LogAssert.ignoreFailingMessages = true;

            var host = new GameObject("Animator");

            try
            {
                BoardMoveAnimator animator = host.AddComponent<BoardMoveAnimator>();

                Assert.That(animator.IsBusy, Is.False, "a fresh animator has nothing in the air");

                // Nothing in flight means nothing to finish. It must be a no-op rather than a second
                // way to fire a callback.
                Assert.DoesNotThrow(animator.FinishNow, "FinishNow on an idle animator");

                var brick = new GameObject("Brick").AddComponent<BlockView>();
                int completions = 0;

                try
                {
                    animator.Play(
                        new[] { brick },
                        new[] { Target },
                        BrickMotion.Drop,
                        () => completions++);

                    Assert.That(completions, Is.EqualTo(1), "the completion did not run");
                    Assert.That(
                        brick.transform.position,
                        Is.EqualTo(Target),
                        "with no timings the brick is placed on its cell immediately");

                    animator.FinishNow();

                    Assert.That(
                        completions,
                        Is.EqualTo(1),
                        "FinishNow ran a completion that had already run — the run would be seated twice");
                }
                finally
                {
                    Object.DestroyImmediate(brick.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static Vector3 Point(float progress)
        {
            return BoardMoveAnimator.EntryPoint(Origin, Target, Apex, progress);
        }
    }
}
