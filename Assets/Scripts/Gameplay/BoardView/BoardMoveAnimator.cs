using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// The two motions a whole run makes together: it rises out of its column, or it drops back into
    /// it. Travelling to another column is not one of them — that is <see cref="BoardMoveAnimator.PlayEntry"/>,
    /// because the bricks stop being a run there and go one at a time (D-072).
    /// </summary>
    public enum BrickMotion
    {
        Lift,
        Drop,
    }

    /// <summary>
    /// Moves bricks between the places the board has already put them. It is deliberately dumb
    /// about the game: it is handed the bricks and the positions they must end at, and its only
    /// promise is that they get there — the move itself was committed in <c>Board</c> the instant
    /// it was legal (`.claude/rules/gameplay.md`).
    /// <para>
    /// That promise is what makes an interruption safe. Starting a new motion while one is running
    /// settles the old one at its end positions first, so bricks are never left mid-air and the
    /// screen never disagrees with the board — the failure mode a tween usually introduces.
    /// </para>
    /// <para>
    /// The buffers are fields rather than locals: this is the project's first per-frame code, and
    /// a move must not allocate. At most eight bricks fly at once (fingerprint.md → Scale), so the
    /// per-frame work is eight transform writes.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardMoveAnimator : MonoBehaviour
    {
        [SerializeField]
        private BoardAnimationConfig config;

        private readonly List<Transform> moving = new List<Transform>();
        private readonly List<Vector3> origins = new List<Vector3>();
        private readonly List<Vector3> destinations = new List<Vector3>();

        // A rock tips the run, so the brick's own rotation has to be remembered as well as its
        // place: the Block prefab is turned 180° to face the camera (D-047) and writing a bare
        // Z angle would throw that away.
        private readonly List<Quaternion> uprights = new List<Quaternion>();

        // The rock keeps its own three lists on purpose. Sharing the motion's would mean one stop
        // path could clear the other's anchors, and an anchor that goes missing is a brick left
        // hanging a fraction off its cell — or worse, left tipped (D-059).
        private readonly List<Transform> rocking = new List<Transform>();
        private readonly List<Vector3> anchors = new List<Vector3>();
        private readonly List<Quaternion> anchorRotations = new List<Quaternion>();

        private Coroutine playing;
        private Coroutine idling;

        /// <summary>
        /// What the running motion still owes its caller. Held as a field rather than left to the
        /// coroutine's own parameter because <see cref="FinishNow"/> has to be able to run it from
        /// outside: settling a flight puts the bricks on their cells, but only the callback hands
        /// them back to their column, and a flight that is stopped without it leaves the view
        /// disagreeing with `Board` (D-098).
        /// <para>
        /// Cleared before it is invoked, by <see cref="Complete"/> alone, so it fires exactly once
        /// no matter which of the two paths gets there first.
        /// </para>
        /// </summary>
        private Action pending;

        /// <summary>
        /// True while bricks are in the air. A tap during that window is no longer refused — it
        /// finishes the flight first (D-098) — so this is now read to decide *that*, not to ignore
        /// the player.
        /// </summary>
        public bool IsBusy => playing != null;

        /// <summary>How far a selected run rises, or 0 when the timings have not been authored yet.</summary>
        public float LiftHeight => config == null ? 0f : config.LiftHeight;

        /// <summary>
        /// Plays a motion. With no config — or an unauthored one — the bricks are placed
        /// immediately: the board stays correct and the console says what to fix, which beats
        /// either a silent freeze or a made-up default feel.
        /// </summary>
        public void Play(IReadOnlyList<BlockView> bricks, IReadOnlyList<Vector3> targets, BrickMotion motion, Action onFinished)
        {
            if (bricks == null)
            {
                throw new ArgumentNullException(nameof(bricks));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (bricks.Count != targets.Count)
            {
                throw new ArgumentException(
                    "There are " + bricks.Count + " brick(s) and " + targets.Count + " target(s); a brick needs exactly one.", nameof(targets));
            }

            if (!Load(bricks, targets))
            {
                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            float duration;

            if (!TryReadTimings(motion, out duration))
            {
                Settle();

                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            // Only the rise rocks. A drop that tipped would read as a brick fumbled rather than
            // placed.
            float tilt = motion == BrickMotion.Lift && config != null ? config.LiftTiltDegrees : 0f;
            float cycles = config == null ? 0f : config.LiftTiltCycles;

            pending = onFinished;
            playing = StartCoroutine(Tween(duration, tilt, cycles));
        }

        /// <summary>
        /// Sends a run into another column: each brick rises, crosses <em>above</em> that column's
        /// mouth and drops in from over it, and they go one after another rather than together
        /// (D-072).
        /// <para>
        /// The order is the caller's. <c>BoardView</c> hands the run over topmost-brick-first, so the
        /// brick that was on top leaves first and lands lowest — which is only a look, because a
        /// moved run is one colour and the stack it makes is the same either way.
        /// </para>
        /// <param name="apexY">
        /// The world height the bricks cross at, which the caller reads off the target column so a
        /// taller column is cleared without a number stored here.
        /// </param>
        /// </summary>
        public void PlayEntry(IReadOnlyList<BlockView> bricks, IReadOnlyList<Vector3> targets, float apexY, Action onFinished)
        {
            if (!Load(bricks, targets))
            {
                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            if (!ConfigUsable())
            {
                Settle();

                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            pending = onFinished;
            playing = StartCoroutine(Entering(config.TravelDuration, config.EntryStagger, apexY));
        }

        /// <summary>
        /// Where a brick is, part-way through entering its slot. **Three straight legs**: up out of
        /// its own column to <paramref name="apexY"/>, across at that height to over the target's
        /// mouth, then down into the cell (D-081).
        /// <para>
        /// The legs are straight because the alternative was the fault this replaced: the old path
        /// drove x and y from one eased value, so a brick left its column on a diagonal and arrived at
        /// the slot from the side rather than over its mouth. A column has a mouth and a brick goes in
        /// through it — the corners are the point, not a cost.
        /// </para>
        /// <para>
        /// Time is split **in proportion to each leg's own length**, so the brick holds one speed all
        /// the way round and a short hop across does not dawdle while a long one hurries. That is why
        /// this takes no share to tune: the shares are the geometry. The whole flight is eased once,
        /// end to end, so it accelerates out of the source column and settles into the target and the
        /// corners are turns rather than jolts.
        /// </para>
        /// <para>
        /// Pure and static so it can be tested without a scene, which is the point: a path that ends
        /// anywhere but on the cell leaves a brick off the board, and that is not a thing to find by
        /// eye. The apex is raised past both ends, so a move into a column a row lower never dips
        /// through the board on the way.
        /// </para>
        /// </summary>
        public static Vector3 EntryPoint(Vector3 origin, Vector3 target, float apexY, float progress)
        {
            progress = Mathf.Clamp01(progress);

            float apex = Mathf.Max(apexY, Mathf.Max(origin.y, target.y));

            float up = apex - origin.y;
            float across = new Vector2(target.x - origin.x, target.z - origin.z).magnitude;
            float down = apex - target.y;
            float total = up + across + down;

            // Nowhere to go — the same cell, already at the apex — so there is no path to be part of
            // the way along and the answer is simply the destination.
            if (total <= 0.0001f)
            {
                return target;
            }

            // One easing over the whole journey rather than one per leg: eased per leg, the brick
            // would stop dead at each corner.
            float eased = progress * progress * (3f - 2f * progress);
            float travelled = eased * total;

            if (travelled <= up)
            {
                // Straight up, out of its own column. x and z are untouched, which is the whole
                // difference from the path this replaced.
                return new Vector3(origin.x, origin.y + travelled, origin.z);
            }

            travelled -= up;

            if (travelled <= across)
            {
                // Straight across, at the apex. Height is untouched here, so nothing is descending
                // while it is still moving sideways — that is what "no diagonal" means in code.
                float t = across > 0.0001f ? travelled / across : 1f;
                return new Vector3(Mathf.Lerp(origin.x, target.x, t), apex, Mathf.Lerp(origin.z, target.z, t));
            }

            travelled -= across;

            // Straight down, over the mouth. x and z are the target's for the entire descent, so the
            // brick enters the column from above and never through its side.
            float fall = down > 0.0001f ? Mathf.Clamp01(travelled / down) : 1f;
            return new Vector3(target.x, Mathf.Lerp(apex, target.y, fall), target.z);
        }

        /// <summary>
        /// Puts every brick in flight at its destination now and <em>abandons</em> what the motion
        /// still owed its caller. Used when a board is torn down: seating bricks into a column that
        /// is about to be destroyed, or raising "the move is shown" for a board nobody is looking
        /// at, would be worse than dropping the callback.
        /// <para>
        /// This is the reason <see cref="FinishNow"/> exists beside it rather than instead of it:
        /// stopping a flight and *completing* a flight are different intentions, and one method
        /// cannot mean both (D-098).
        /// </para>
        /// </summary>
        public void SettleAndStop()
        {
            StopIdle();

            if (playing == null)
            {
                pending = null;
                return;
            }

            StopCoroutine(playing);
            playing = null;
            pending = null;
            Settle();
        }

        /// <summary>
        /// Ends the running flight the way it would have ended on its own: the bricks go to their
        /// cells and the callback runs. The one thing a caller must do before interrupting a motion
        /// it wants finished rather than dropped.
        /// <para>
        /// It exists because taps are no longer refused while bricks fly (D-098, superseding D-031).
        /// Settling alone is not enough: the flight's callback is what hands the run back to its
        /// column, shows what the move reported and raises <c>BoardShown</c>. A flight stopped
        /// without it leaves bricks standing on the right cells that no column owns — the view
        /// disagreeing with `Board`, which is the one thing animation is not allowed to do
        /// (rules/gameplay.md).
        /// </para>
        /// <para>
        /// Nothing in flight means nothing to finish, so this is a no-op then rather than a second
        /// way to fire a callback that already ran.
        /// </para>
        /// </summary>
        public void FinishNow()
        {
            if (playing == null)
            {
                return;
            }

            StopCoroutine(playing);
            playing = null;
            Settle();
            Complete();
        }

        /// <summary>
        /// Hands the motion's callback back, exactly once. Cleared *before* it is invoked, so the
        /// coroutine finishing and <see cref="FinishNow"/> racing to the same motion cannot both
        /// run it — and so a callback that starts the next motion re-entrantly finds nothing left
        /// pending from the one it just closed.
        /// </summary>
        private void Complete()
        {
            Action finished = pending;
            pending = null;

            if (finished != null)
            {
                finished();
            }
        }

        /// <summary>
        /// Keeps a lifted run alive: the whole stack rocks about its own centre — top-left corner
        /// out one way, top-right the other — rather than sliding sideways, which is what makes it
        /// read as weight in the player's hand. Started when a lift lands, stopped by whatever
        /// happens next.
        /// <para>
        /// Every frame is computed from the anchors, never from a brick's current transform, so the
        /// rock cannot accumulate; <see cref="StopIdle"/> puts both the places and the rotations back,
        /// which is what lets a cancel or a move begin from the run's true position. It deliberately
        /// does **not** make the animator busy: tapping the next column while a run rocks is how the
        /// game is played, and what must not be interrupted is a committed motion, not a decoration.
        /// </para>
        /// </summary>
        public void PlayIdle(IReadOnlyList<BlockView> bricks, IReadOnlyList<Vector3> at)
        {
            if (bricks == null || at == null || bricks.Count != at.Count)
            {
                return;
            }

            StopIdle();

            if (config == null || config.IdleTiltDegrees <= 0f || config.IdleTiltPeriod <= 0f)
            {
                return;
            }

            for (int index = 0; index < bricks.Count; index++)
            {
                if (bricks[index] == null)
                {
                    continue;
                }

                rocking.Add(bricks[index].transform);
                anchors.Add(at[index]);
                anchorRotations.Add(bricks[index].transform.rotation);
            }

            if (rocking.Count == 0)
            {
                return;
            }

            idling = StartCoroutine(Rocking(config.IdleTiltDegrees, config.IdleTiltPeriod));
        }

        /// <summary>Ends the rock and puts the run back upright on its anchors. Safe to call when nothing rocks.</summary>
        public void StopIdle()
        {
            if (idling != null)
            {
                StopCoroutine(idling);
                idling = null;
            }

            for (int index = 0; index < rocking.Count; index++)
            {
                if (rocking[index] != null)
                {
                    rocking[index].position = anchors[index];
                    rocking[index].rotation = anchorRotations[index];
                }
            }

            rocking.Clear();
            anchors.Clear();
            anchorRotations.Clear();
        }

        private IEnumerator Rocking(float degrees, float period)
        {
            Vector3 pivot = Centre(anchors);
            float elapsed = 0f;

            while (true)
            {
                elapsed += Time.deltaTime;

                // One period takes the run out along one diagonal and back along the other, which is
                // the shape the reference rocks in: top-left leading, then top-right.
                float angle = degrees * Mathf.Sin(elapsed / period * 2f * Mathf.PI);
                Quaternion roll = Quaternion.AngleAxis(angle, Vector3.forward);

                for (int index = 0; index < rocking.Count; index++)
                {
                    if (rocking[index] == null)
                    {
                        continue;
                    }

                    rocking[index].position = pivot + roll * (anchors[index] - pivot);
                    rocking[index].rotation = roll * anchorRotations[index];
                }

                yield return null;
            }
        }

        /// <summary>
        /// Tips a set of bricks about a shared pivot: a rigid rock of the whole stack rather than each
        /// brick turning on its own, which would read as a staircase coming loose. The roll is applied
        /// *in front of* the brick's own rotation, so the 180° that faces its symbol at the camera
        /// (D-047) survives.
        /// </summary>
        private static void Rock(List<Transform> bricks, List<Quaternion> uprightRotations, Vector3 pivot, float angle)
        {
            Quaternion roll = Quaternion.AngleAxis(angle, Vector3.forward);

            for (int index = 0; index < bricks.Count; index++)
            {
                if (bricks[index] == null)
                {
                    continue;
                }

                bricks[index].position = pivot + roll * (bricks[index].position - pivot);
                bricks[index].rotation = roll * uprightRotations[index];
            }
        }

        private static Vector3 Centre(List<Vector3> points)
        {
            if (points.Count == 0)
            {
                return Vector3.zero;
            }

            var total = Vector3.zero;

            for (int index = 0; index < points.Count; index++)
            {
                total += points[index];
            }

            return total / points.Count;
        }

        /// <summary>
        /// Takes the bricks and where they must end, and stops whatever was running first — the drift
        /// before the motion, so this one starts from where the run really is rather than from
        /// wherever the drift had carried it (D-059). False when there is nothing to move.
        /// <para>
        /// Starting a motion over a flight that still owes a callback is a caller's mistake, not a
        /// state to handle: the caller is supposed to <see cref="FinishNow"/> first, *before* it
        /// rearranges the buffers the old callback would have read (D-098). Dropping it silently is
        /// what would leave the view disagreeing with `Board`, so it is said out loud instead — the
        /// bricks still land correctly, and the console names the seam that needs fixing.
        /// </para>
        /// </summary>
        private bool Load(IReadOnlyList<BlockView> bricks, IReadOnlyList<Vector3> targets)
        {
            if (pending != null)
            {
                Debug.LogWarning(
                    "[BoardView] A motion started while the previous flight still owed its completion; " +
                    "call FinishNow() before starting one. The bricks are placed, but that flight's landing was dropped.",
                    this);
            }

            StopIdle();
            SettleAndStop();

            moving.Clear();
            origins.Clear();
            destinations.Clear();
            uprights.Clear();

            for (int index = 0; index < bricks.Count; index++)
            {
                if (bricks[index] == null)
                {
                    continue;
                }

                Transform brick = bricks[index].transform;
                moving.Add(brick);
                origins.Add(brick.position);
                destinations.Add(targets[index]);
                uprights.Add(brick.rotation);
            }

            return moving.Count > 0;
        }

        /// <summary>
        /// The entry: one flight per brick, each starting <paramref name="stagger"/> seconds after
        /// the one before it, so a run of three arrives as three bricks and not as a block.
        /// <para>
        /// A brick whose turn has not come sits at its origin — still in the air where the lift left
        /// it, which is what makes the queue read as waiting rather than as a gap. The coroutine runs
        /// for the last brick's flight, not the first's, and <see cref="Settle"/> stays the single end
        /// state, so an interruption still puts every brick on its cell.
        /// </para>
        /// </summary>
        private IEnumerator Entering(float duration, float stagger, float apexY)
        {
            float total = duration + stagger * Mathf.Max(0, moving.Count - 1);
            float elapsed = 0f;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                for (int index = 0; index < moving.Count; index++)
                {
                    if (moving[index] == null)
                    {
                        continue;
                    }

                    float progress = Mathf.Clamp01((elapsed - index * stagger) / duration);
                    moving[index].position = EntryPoint(origins[index], destinations[index], apexY, progress);
                }

                yield return null;
            }

            playing = null;
            Settle();
            Complete();
        }

        private IEnumerator Tween(float duration, float tiltDegrees, float tiltCycles)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Smoothstep: eases both ends without an AnimationCurve field nobody has asked
                // for yet.
                float eased = progress * progress * (3f - 2f * progress);

                // The tilt is a sine over the rise, which is zero at both ends for a whole number of
                // half-rocks — the config refuses anything else, so a rocking run still lands level
                // and exactly where the board says it is (D-059).
                float angle = tiltDegrees * Mathf.Sin(progress * Mathf.PI * tiltCycles);
                Vector3 centre = Vector3.zero;

                for (int index = 0; index < moving.Count; index++)
                {
                    Vector3 point = Vector3.Lerp(origins[index], destinations[index], eased);
                    moving[index].position = point;
                    centre += point;
                }

                if (!Mathf.Approximately(angle, 0f) && moving.Count > 0)
                {
                    Rock(moving, uprights, centre / moving.Count, angle);
                }

                yield return null;
            }

            playing = null;
            Settle();
            Complete();
        }

        private void Settle()
        {
            for (int index = 0; index < moving.Count; index++)
            {
                if (moving[index] != null)
                {
                    moving[index].position = destinations[index];

                    // Upright as well as in place: a rocked brick that kept its tilt would keep it
                    // for the rest of the level, since nothing else in the view writes a rotation
                    // (D-049).
                    moving[index].rotation = uprights[index];
                }
            }

            moving.Clear();
            origins.Clear();
            destinations.Clear();
            uprights.Clear();
        }

        /// <summary>
        /// Whether the timings can be read at all. Both failures are the project half set up rather
        /// than a state to design for, so both are said out loud and the caller snaps the bricks
        /// into place: a correct board with a console line beats a silent freeze.
        /// </summary>
        private bool ConfigUsable()
        {
            string error;

            if (config == null)
            {
                Debug.LogError("[BoardView] " + name + " has no board animation config, so moves will snap. Create one in Data/Config/ and wire it with Tools > Colorful Sort > Wire Game Scene.", this);
                return false;
            }

            if (!config.Validate(out error))
            {
                Debug.LogError("[BoardView] " + config.name + " is not usable, so moves will snap: " + error, config);
                return false;
            }

            return true;
        }

        private bool TryReadTimings(BrickMotion motion, out float duration)
        {
            duration = 0f;

            if (!ConfigUsable())
            {
                return false;
            }

            duration = motion == BrickMotion.Lift ? config.LiftDuration : config.DropDuration;
            return true;
        }

        private void OnDisable()
        {
            SettleAndStop();
        }
    }
}
