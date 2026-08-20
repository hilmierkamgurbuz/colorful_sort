using System;
using System.Collections.Generic;
using ColorfulSort.Board;
using ColorfulSort.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace ColorfulSort.View
{
    /// <summary>
    /// Turns an attempt into the thing the player looks at and taps: columns on a grid, bricks in
    /// their cells, a camera that frames whatever board it was handed, and the hand that moves a
    /// run from one column to another.
    /// <para>
    /// Everything on screen is read from <see cref="BoardSession"/> — column order, capacity,
    /// colour, hidden flag — and never from the authored level. That is not tidiness: the attempt's
    /// columns are a permutation of the authored ones, so a view that read the asset would quietly
    /// undo the variant that stops a replay from replaying memorised taps (D-014, D-015).
    /// </para>
    /// <para>
    /// A tap is a <em>command</em>. The move is committed in <c>Board</c> the instant it is legal
    /// and the animation is what the player watches afterwards, so the screen can lag the board but
    /// can never contradict it (`.claude/rules/gameplay.md`). The view writes transforms and
    /// renderers; it writes no board state, which <c>Board</c>'s <c>internal</c> mutators already
    /// make impossible.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        private const int NoColumn = -1;

        [Header("Scene")]
        [SerializeField]
        private Camera boardCamera;

        [Tooltip("Columns are parented here; the board is centred on this transform.")]
        [SerializeField]
        private Transform columnRoot;

        [Tooltip("Where pooled bricks wait between levels.")]
        [SerializeField]
        private Transform idleBlockRoot;

        [SerializeField]
        private BoardInput input;

        [SerializeField]
        private BoardMoveAnimator animator;

        [Header("Data")]
        [SerializeField]
        private BoardLayoutConfig layout;

        [Tooltip("Read for the settle, the rock and the glow; the brick tweens are the animator's own copy of it.")]
        [FormerlySerializedAs("animation")]
        [SerializeField]
        private BoardAnimationConfig animationConfig;

        [Tooltip("The light behind a lifted run. One for the whole board: only one run is ever up.")]
        [SerializeField]
        private SpriteRenderer glow;

        [SerializeField]
        private BlockSkinSet skins;

        [Header("Prefabs")]
        [SerializeField]
        private ColumnView normalColumn;

        [SerializeField]
        private ColumnView iceColumn;

        [SerializeField]
        private ColumnView coveredColumn;

        [SerializeField]
        private BlockView blockPrefab;

        private readonly List<ColumnView> columnViews = new List<ColumnView>();
        private readonly List<BlockView> liveBlocks = new List<BlockView>();
        private readonly List<BlockView> run = new List<BlockView>();
        private readonly List<Vector3> targets = new List<Vector3>();

        private BlockPool pool;

        /// <summary>How wide the layout grid is — what turns a cell index into a row and a column.</summary>
        private int layoutColumns;

        /// <summary>
        /// How tall the layout grid is *right now* — the authored row count, or more when an
        /// added column had to start a row. Derived, never accumulated: see <see cref="authoredRows"/>.
        /// </summary>
        private int layoutRows;

        /// <summary>The grid cell each slot stands in, in slot order (D-033). Derived every open and resync.</summary>
        private int[] layoutCells;

        /// <summary>
        /// The level's own placement, kept unedited for the lifetime of the attempt. It is the
        /// only honest answer to "where did this board start", and every derived placement is
        /// computed from it — which is what lets an undone column give its cell back (D-046).
        /// </summary>
        private int[] authoredCells;

        /// <summary>How many rows the level authored, before any added column widened the grid.</summary>
        private int authoredRows;
        private float columnWidth;
        private float rowHeight;

        private int selectedColumn = NoColumn;

        /// <summary>So a missing animation config is reported once, not once per completed colour.</summary>
        private bool reportedMissingAnimation;

        /// <summary>URP has no per-renderer colour this shader can trust, so the glow's tint is ours (D-065).</summary>
        private static readonly int GlowTintProperty = Shader.PropertyToID("_Tint");

        /// <summary>One block, reused: a lift happens on a tap, but there is no reason to allocate on one.</summary>
        private MaterialPropertyBlock glowTint;

        /// <summary>
        /// The view has finished showing whatever the board last did: the bricks have landed, and any
        /// column that started celebrating has finished and settled into its slot.
        /// <para>
        /// It exists because `Board` is right *immediately* and the screen is not. `BoardSession.Won`
        /// fires the instant a move is legal — with the winning bricks still in the air — so anything
        /// that wants to talk to the player about it has to wait for this instead (D-088, D-076's
        /// split applied to the win). A rebuild through <see cref="Resync"/> raises it at once, since
        /// a resync is instant by design (D-031); that is also what keeps a win by shuffle from never
        /// being announced.
        /// </para>
        /// </summary>
        public event Action BoardShown;

        /// <summary>The attempt on screen, or null before the first one opens.</summary>
        public BoardSession Session { get; private set; }

        public int ColumnCount => columnViews.Count;

        /// <summary>The column whose run is lifted, or -1 when nothing is selected.</summary>
        public int SelectedColumn => selectedColumn;

        public ColumnView ColumnAt(int index)
        {
            if (index < 0 || index >= columnViews.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "There are " + columnViews.Count + " columns on screen.");
            }

            return columnViews[index];
        }

        /// <summary>
        /// Builds the board for an attempt. The grid shape and the cell each column stands in
        /// come from the level's authored layout, and are passed as plain numbers rather than
        /// as the level asset, so the view has no way to read authored columns by accident.
        /// <para>
        /// The placement is per <em>slot</em>: entry k is where the attempt's column k stands
        /// (D-033). The attempt permutes which authored column that is, so the same placement
        /// serves every variant of a level.
        /// </para>
        /// </summary>
        public void Open(BoardSession session, int layoutRows, int layoutColumns, IReadOnlyList<int> placement)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            if (layoutRows < 1 || layoutColumns < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layoutRows), layoutRows + "×" + layoutColumns, "A board's layout has at least one row and one column.");
            }

            if (!ReferencesReady())
            {
                return;
            }

            BoardState state = session.State;

            if (layoutRows * layoutColumns < state.ColumnCount)
            {
                throw new ArgumentException(
                    "The level's layout is " + layoutRows + "×" + layoutColumns + " = " + (layoutRows * layoutColumns) +
                    " slots but the board has " + state.ColumnCount + " columns, so one would have nowhere to stand.", nameof(layoutColumns));
            }

            if (placement.Count != state.ColumnCount)
            {
                throw new ArgumentException(
                    "The layout places " + placement.Count + " column(s) but the board has " + state.ColumnCount +
                    "; the level's placement and its columns disagree.", nameof(placement));
            }

            Clear();

            // No subscription to ColourCompleted. It fires the instant the move is legal, which is
            // before the bricks have flown, so a celebration hung off it started while the run was
            // still in the air. The move itself reports what it completed and the view reads that
            // once the bricks are seated (D-076).
            Session = session;
            authoredRows = layoutRows;

            // Named, because the field of the same name is about to hold a different number and the
            // renumbering below needs both: this is the width the level's cells were counted in.
            int authoredColumns = layoutColumns;

            // The grid the board is *placed* in is as wide as the wider of the level and the row
            // limit. A cell is numbered `row × width + column`, so the stride is what caps how wide a
            // row can get — and an added column is allowed to make a row up to MaxColumnsPerRow wide
            // (D-070), which a level authored three-wide could not otherwise reach.
            //
            // Widening moves nothing: each row is centred on its own occupied span (D-034), so the
            // empty cells on the right of a wider grid are not there to be seen. What it does change
            // is the numbering, so the level's own cells are renumbered into the new stride below.
            this.layoutColumns = Mathf.Max(authoredColumns, layout.MaxColumnsPerRow);

            // Copied rather than held: the caller's array is Content's, and a board that
            // re-reads it every tap would follow an edit made mid-attempt.
            authoredCells = new int[placement.Count];

            for (int slot = 0; slot < placement.Count; slot++)
            {
                int cell = placement[slot];
                authoredCells[slot] = (cell / authoredColumns) * this.layoutColumns + (cell % authoredColumns);
            }

            ApplyPlacement(state.ColumnCount);
            Rebuild();
        }

        /// <summary>
        /// Redraws the whole board from <c>Board</c>'s current state — what a booster leaves
        /// behind.
        /// <para>
        /// It rebuilds rather than reversing what it thinks changed, and that is the point. An
        /// undo can put back a cover, a lock and a hidden cell at once; a shuffle rewrites every
        /// visible cell. Three bespoke reverse-animations would be three more ways for the screen
        /// to disagree with the board, which is the failure D-031 exists to prevent — and the one
        /// that is invisible, because a plausible wrong board only misbehaves on the *next* tap.
        /// Re-reading the authority cannot drift.
        /// </para>
        /// <para>
        /// There is no animation here on purpose: a booster's result appears instantly. What the
        /// undo of a shuffle should look like is a decision with no reference behaviour recorded,
        /// and the move animator is untouched, so the tap path keeps its lift-arc-drop.
        /// </para>
        /// </summary>
        public void Resync()
        {
            if (Session == null || !ReferencesReady())
            {
                return;
            }

            ApplyPlacement(Session.State.ColumnCount);
            Rebuild();

            // A resync is instant by design, so the view is already caught up. Saying so here is what
            // keeps a win by *booster* — a shuffle that finishes the last colour — from waiting for a
            // landing that never happens.
            //
            // This is also why the resync path keeps `SettleAndStop` rather than moving to
            // `FinishNow` with the tap path (D-098): a rebuild reads every brick back off `Board`
            // and raises "shown" right here, so finishing the old flight's landing first would seat
            // a run into a column that is about to be torn down and rebuilt anyway.
            celebrating = 0;
            RaiseShown();
        }

        /// <summary>
        /// Works out where the columns stand for however many the board has now, from the level's
        /// authored placement. Every column the level authored keeps its cell; one it did not —
        /// which is exactly the added column — joins the topmost row that is not yet
        /// <c>MaxColumnsPerRow</c> wide, and when every row is full the board gains one below
        /// (D-070). Each row is centred on its own occupied span and the camera is framed from the
        /// board it is given (D-026), so a taller or wider board re-centres and re-frames itself
        /// without a single new authored number.
        /// <para>
        /// Derived every time rather than adjusted in place, which is the fix behind D-046: the
        /// grid used to only grow, so undoing an added column left its cell — and the row it may
        /// have started — reserved for a column that was no longer there.
        /// </para>
        /// </summary>
        private void ApplyPlacement(int columnCount)
        {
            layoutCells = BoardLayout.PlaceColumns(
                authoredCells, columnCount, layoutColumns, authoredRows, layout.MaxColumnsPerRow, out layoutRows);
        }

        /// <summary>
        /// Builds every column and brick from the session's state. Shared by <see cref="Open"/>
        /// and <see cref="Resync"/> so there is one description of what the board looks like.
        /// </summary>
        private void Rebuild()
        {
            ClearVisuals();

            BoardState state = Session.State;
            pool = pool ?? new BlockPool(blockPrefab, idleBlockRoot);

            // Pass one: build every column, and let the tallest one set the row pitch. A level
            // may mix capacities; a straight grid is what keeps that readable.
            columnWidth = 0f;
            rowHeight = 0f;

            for (int index = 0; index < state.ColumnCount; index++)
            {
                BoardColumn column = state[index];
                ColumnView view = Instantiate(PrefabFor(column.Kind), columnRoot);
                view.name = "Column_" + index.ToString("00");
                view.Build(column);

                columnViews.Add(view);
                columnWidth = Mathf.Max(columnWidth, view.Metrics.Width);
                rowHeight = Mathf.Max(rowHeight, view.Metrics.Height(column.Capacity));
            }

            // Pass two: place each column on the grid and fill it.
            for (int index = 0; index < columnViews.Count; index++)
            {
                Vector2 bottomCentre = BoardLayout.SlotBottomCentre(
                    index, layoutCells, layoutColumns, columnWidth, rowHeight, layout.ColumnGap, layout.RowGap);

                columnViews[index].transform.localPosition = new Vector3(bottomCentre.x, bottomCentre.y, 0f);
                SpawnBlocks(state[index], columnViews[index]);

                // Settled is read off the board, never remembered: a column whose colour is already
                // finished is born settled, with no fade. That is what makes an undo and every
                // booster resync correct for free — both rebuild, and the state answers (D-057).
                ShowSettled(index, BoardRules.HoldsCompletedColour(state, index), instant: true);
            }

            FrameCamera();
        }

        /// <summary>
        /// The live path for the colour a move has just finished: the column holding it hops, sparks
        /// and *then* settles (D-075).
        /// <para>
        /// It celebrates that colour's column and no other. Asking every column whether it holds a
        /// finished colour — which is what this did first — sends every column finished earlier back
        /// up into the air, because the answer is still yes (D-076). A rebuilt board does not come
        /// through here at all: it lands on <see cref="ShowSettled"/> with no duration, since a column
        /// already finished when the level opened has nothing to celebrate.
        /// </para>
        /// </summary>
        private void Celebrate(BlockColourId colour)
        {
            BoardState state = Session.State;

            for (int index = 0; index < columnViews.Count && index < state.ColumnCount; index++)
            {
                if (!BoardRules.HoldsCompletedColour(state, index) || state[index].TopColour != colour)
                {
                    continue;
                }

                if (animationConfig == null)
                {
                    // The same one-time complaint ShowSettled makes, and for the same reason: a
                    // finished colour that quietly does nothing is a bug with nothing in the console.
                    ShowSettled(index, true, instant: true);
                    continue;
                }

                celebrating++;
                columnViews[index].Celebrate(CelebrationLook.From(animationConfig), CelebrationFinished);
            }
        }

        private void ShowSettled(int index, bool settled, bool instant)
        {
            if (index < 0 || index >= columnViews.Count)
            {
                return;
            }

            if (animationConfig == null)
            {
                // Said out loud, once: returning quietly here is what makes a finished colour look
                // like a bug with no console to read — the settle simply never happens.
                if (!reportedMissingAnimation)
                {
                    reportedMissingAnimation = true;
                    Debug.LogError("[BoardView] " + name + " has no animation config, so a finished column cannot settle. " +
                                   "Run Tools > Colorful Sort > Wire Game Scene.", this);
                }

                return;
            }

            columnViews[index].SetSettled(
                settled,
                instant ? 0f : animationConfig.SettleDuration,
                animationConfig.SettleShade,
                animationConfig.SettleShadowAlpha);
        }

        /// <summary>Empties the board: bricks back to the pool, columns gone, no attempt on screen.</summary>
        public void Clear()
        {
            ClearVisuals();
            Session = null;
        }

        /// <summary>
        /// Takes down everything that is drawn while leaving the attempt in place — what a
        /// rebuild needs, and the half of <see cref="Clear"/> that a resync reuses.
        /// </summary>
        private void ClearVisuals()
        {
            if (animator != null)
            {
                animator.SettleAndStop();
            }

            selectedColumn = NoColumn;
            HideGlow();
            run.Clear();

            if (pool != null)
            {
                for (int index = liveBlocks.Count - 1; index >= 0; index--)
                {
                    pool.Return(liveBlocks[index]);
                }
            }

            liveBlocks.Clear();

            for (int index = columnViews.Count - 1; index >= 0; index--)
            {
                ColumnView view = columnViews[index];

                if (view == null)
                {
                    continue;
                }

                view.ForgetBricks();

                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            columnViews.Clear();
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.Pressed += OnPressed;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Pressed -= OnPressed;
            }
        }

        /// <summary>
        /// Where a screen point lands on the board, in the column root's own space. The board is a
        /// plane through the column root facing the camera's way, and the press is a ray: that
        /// holds for any camera angle, including none.
        /// </summary>
        /// <returns>False when the ray runs parallel to the board — a camera looking along it.</returns>
        private bool TryBoardPoint(Vector2 screenPosition, out Vector3 localPoint)
        {
            var board = new Plane(columnRoot.forward, columnRoot.position);
            Ray ray = boardCamera.ScreenPointToRay(screenPosition);

            float distance;

            if (!board.Raycast(ray, out distance))
            {
                localPoint = Vector3.zero;
                return false;
            }

            localPoint = columnRoot.InverseTransformPoint(ray.GetPoint(distance));
            return true;
        }

        /// <summary>
        /// A press lands on at most one column. A tap while bricks are in the air no longer waits:
        /// the flight is finished on the spot and the tap is served (D-098, superseding D-031).
        /// <para>
        /// That is safe for the reason D-031 already relied on — the move was committed in `Board`
        /// the instant it was legal, so the player's tap always acts on the true board — and it is
        /// *complete* because the flight is finished rather than merely stopped: `FinishNow` runs
        /// the landing, so the run is seated in its column, what the move reported is shown, and
        /// `BoardShown` is raised, all before this tap changes anything.
        /// </para>
        /// <para>
        /// The order is the whole point and is not an accident of layout: finishing happens once the
        /// press is known to land on a column and before <see cref="Select"/> or <see cref="Move"/>
        /// touch the shared <c>run</c>/<c>targets</c> buffers. Finished later, the old landing would
        /// seat the *new* run into the *old* destination; finished earlier — on every press — a tap
        /// on the background would cut a flight short for nothing.
        /// </para>
        /// </summary>
        private void OnPressed(Vector2 screenPosition)
        {
            if (Session == null || animator == null)
            {
                return;
            }

            // A ray against the board's own plane, not `ScreenToWorldPoint`. That call returns a
            // point on the camera's near plane, whose x and y match the board's only while the
            // camera looks straight down the board's normal; the moment it is tilted they drift,
            // and a tap starts selecting the row above or below — which reads as a rule bug and is
            // not one (D-050).
            Vector3 local;

            if (!TryBoardPoint(screenPosition, out local))
            {
                return;
            }

            int slot = BoardLayout.SlotAt(
                new Vector2(local.x, local.y), layoutCells, layoutColumns, columnWidth, rowHeight, layout.ColumnGap, layout.RowGap);

            if (slot == BoardLayout.NoSlot || slot >= columnViews.Count)
            {
                return;
            }

            // The press is going to be served, so any flight still running ends properly first: its
            // run is seated, its reveals are shown, its BoardShown is raised. Only then does the tap
            // get to touch `run`/`targets` (D-098).
            animator.FinishNow();

            Tap(slot);
        }

        private void Tap(int index)
        {
            if (selectedColumn == NoColumn)
            {
                Select(index);
                return;
            }

            if (selectedColumn == index)
            {
                Cancel();
                return;
            }

            Move(index);
        }

        /// <summary>
        /// Lifts the top run of a column. Whether it can be lifted at all is <c>Board</c>'s answer,
        /// never a guess here — a locked column, an empty one and a hidden top all say no.
        /// </summary>
        private void Select(int index)
        {
            if (!Session.CanLift(index))
            {
                return;
            }

            ColumnView column = columnViews[index];
            int length = BoardRules.TopRunLength(Session.State[index]);

            if (length <= 0 || length > column.BrickCount)
            {
                return;
            }

            run.Clear();
            targets.Clear();

            for (int offset = 0; offset < length; offset++)
            {
                BlockView brick = column.BrickAt(column.BrickCount - length + offset);
                run.Add(brick);
                targets.Add(brick.transform.position + Vector3.up * animator.LiftHeight);
            }

            selectedColumn = index;
            ShowGlow(Session.State[index].TopColour, length);

            // The run keeps rocking once it is up, anchored on the very targets it rose to, so a
            // waiting stack looks held rather than parked (D-059). It does not make the animator
            // busy: the tap that hands the selection over has to land mid-rock.
            animator.Play(run, targets, BrickMotion.Lift, () => animator.PlayIdle(run, targets));
        }

        /// <summary>
        /// Lights the run that has just been lifted: the tray shadow's shape again, in the colour of
        /// the bricks that are up, sized to the whole run and a little larger so the light shows past
        /// them (D-060).
        /// <para>
        /// It is parented to the run's <em>bottom</em> brick, which is what makes this the only code
        /// the glow needs: the rock, the rise and the drop all move that brick, and a child follows
        /// for free. The brick is turned 180° to face its symbol at the camera (D-047), so a local
        /// −Z lands behind it in world space — where an opaque brick hides everything but the rim.
        /// </para>
        /// </summary>
        private void ShowGlow(BlockColourId colour, int length)
        {
            if (glow == null || animationConfig == null || run.Count == 0 || skins == null)
            {
                return;
            }

            BlockSkin skin;

            if (!skins.TryGetSkin(colour, out skin))
            {
                return;
            }

            if (glow.sprite == null)
            {
                return;
            }

            // Measured against the *run*, not against the tray the drawing came from: the tray's own
            // skirt and crown made a glow wider and lower than the eye wanted, and these three numbers
            // are what the user tuned instead (D-063). Anchored on the run, they hold for any length.
            float below = animationConfig.GlowBelow;
            float above = animationConfig.GlowAbove;
            Transform bottom = run[0].transform;

            glow.transform.SetParent(bottom, false);

            // The pivot is the sprite's bottom edge, and the bottom brick's body starts half a cell
            // below its centre. Half a cell behind in Z is that brick's back face — a cell is one unit
            // and a brick is one cell deep — and the brick's own 180° (D-047) is what turns a local
            // -0.5 into "behind" in world space. That same flip is why the sideways nudge is applied
            // negative here: it recentres a drawing that is off-centre by that much.
            glow.transform.localPosition = new Vector3(-animationConfig.GlowNudgeX, -0.5f - below, -0.5f);
            glow.transform.localRotation = Quaternion.identity;
            glow.transform.localScale = Vector3.one;
            glow.size = new Vector2(1f + animationConfig.GlowPadding, length + below + above);
            // The colour goes in as *our own* shader property, not as the renderer's colour: the
            // diagnostic that closed this showed the renderer holding the right light pink while the
            // screen stayed white, because neither the vertex colour nor `_RendererColor` reaches the
            // shader on this path (D-065). A property block writes it per renderer and clones nothing.
            glow.color = Color.white;

            glowTint = glowTint ?? new MaterialPropertyBlock();
            glowTint.Clear();
            glowTint.SetColor(GlowTintProperty, BlockView.NeonOf(skin.UiColour, animationConfig.GlowLift));
            glow.SetPropertyBlock(glowTint);

            glow.enabled = true;
        }

        /// <summary>
        /// Puts the glow away — off the brick it was riding, back on the board root, and dark. Called
        /// from every path that ends a selection, because the pool re-parents a brick without taking
        /// its children with it: a glow left attached would ride into the next level (D-060).
        /// </summary>
        private void HideGlow()
        {
            if (glow == null)
            {
                return;
            }

            glow.enabled = false;
            glow.transform.SetParent(columnRoot, false);
        }

        /// <summary>Drops the lifted run back where it still is as far as the board is concerned.</summary>
        private void Cancel()
        {
            ColumnView column = columnViews[selectedColumn];
            int length = run.Count;

            targets.Clear();

            for (int offset = 0; offset < length; offset++)
            {
                targets.Add(column.CellPosition(column.BrickCount - length + offset));
            }

            selectedColumn = NoColumn;
            HideGlow();
            animator.Play(run, targets, BrickMotion.Drop, null);
            run.Clear();
        }

        /// <summary>
        /// Commits the move and then flies the bricks.
        /// <para>
        /// A target the move is not legal into hands the selection over instead: the lifted run goes
        /// back down and the tapped column's run comes up (D-055). Doing nothing — which is what
        /// this did — reads as a dead tap, because the player who taps cat and then moon has already
        /// decided they want the moon. Legality is still `Board`'s answer; what changed is only what
        /// the view does with a no.
        /// </para>
        /// </summary>
        private void Move(int target)
        {
            int source = selectedColumn;
            BoardMove move;

            if (!Session.TryMove(source, target, out move))
            {
                // Cancel first, so the run is on its way down and the selection is clear before
                // anything else is lifted; Select then refuses on its own if the tapped column has
                // nothing to give, which leaves the board at "nothing selected" — an ordinary
                // cancel, and never a run stranded in the air.
                Cancel();
                Select(target);
                return;
            }

            ColumnView from = columnViews[source];
            ColumnView to = columnViews[target];

            // End the rock BEFORE touching anything it holds. `StopIdle` puts every rocking brick back
            // on its *lifted* anchor, so calling it later — which `PlayEntry` does, through `Load` —
            // would undo the re-seating below and hang the leftover brick back in the air (D-090).
            //
            // The rule this keeps: the view never rearranges a brick the animator is still holding.
            animator.StopIdle();

            // The mirror follows the move that already happened. `Board` caps a move by the free
            // space in the target ("only as many blocks as still fit"), so a lifted run of three
            // can leave with two — and the brick that stays behind is still hanging in the air.
            // Re-seating the source column puts it back down, and now nothing lifts it again.
            run.Clear();
            from.ReleaseTop(move.Count, run);
            from.ReseatAll();

            // Topmost brick first, which is the order it flies in *and* the order it is seated in:
            // the brick that was on top leaves first and lands lowest (D-072). Nothing about the
            // board changes — a moved run is a single colour, so the stack is the same either way —
            // and reversing here rather than in two places is what keeps the flight and the seating
            // from ever disagreeing about which brick belongs in which cell.
            run.Reverse();

            targets.Clear();
            int landing = to.BrickCount;

            for (int offset = 0; offset < run.Count; offset++)
            {
                targets.Add(to.CellPosition(landing + offset));

                // A brick in flight hangs off the board root, not off a column, and is re-seated
                // once it lands (blueprint → hierarchy conventions).
                run[offset].transform.SetParent(columnRoot, true);
            }

            selectedColumn = NoColumn;
            HideGlow();

            // The height the bricks cross at clears **both** columns' mouths plus the authored
            // clearance — the one they are leaving as well as the one they are entering (D-081).
            //
            // It used to measure only the target, which was invisible while the first leg was a
            // diagonal: a brick drifted upward as it crossed, so it was never level with the column it
            // was still inside. Now it rises straight out first, and a run leaving the bottom of a
            // tall column would turn sideways *inside* it and pass through the bricks above.
            //
            // With no config the clearance is simply the taller mouth: the animator reports the
            // missing asset itself and snaps the bricks home, so this path stays arithmetic rather
            // than a second place that has to know the config can be absent.
            float clearance = animationConfig == null ? 0f : animationConfig.EntryClearance;
            float leaving = from.transform.position.y + from.Metrics.Height(from.Capacity);
            float entering = to.transform.position.y + to.Metrics.Height(to.Capacity);
            float apexY = Mathf.Max(leaving, entering) + clearance;

            ShowPlumes(true);

            animator.PlayEntry(run, targets, apexY, () => Land(to, move));
        }

        private void Land(ColumnView destination, BoardMove move)
        {
            // Before the bricks are handed back to their column: the plume stops being fed here, and
            // the puffs it has already dropped fade out where they were (D-074).
            ShowPlumes(false);

            // Read before the seating, because it is what the seating changes: whatever ends up above
            // this height is the run that has just arrived.
            int landing = destination.BrickCount;

            for (int index = 0; index < run.Count; index++)
            {
                destination.Seat(run[index]);
            }

            run.Clear();

            // A landing that finished a colour belongs to the celebration, which throws its own sparks
            // at the end of the hop (D-075) — firing this one as well would put two bursts on the same
            // brick a second apart. So this is the *other* landing: a brick set down on its own
            // (D-077). `CompletedColours` is the move's own report, and a move only ever completes a
            // colour in the column it moved into, so an empty list is exactly "nothing to celebrate".
            if (animationConfig != null && move.CompletedColours.Count == 0)
            {
                destination.SparkLanding(landing, LandingLook.From(animationConfig));
            }

            ShowWhatTheMoveReported(move);

            // Nothing celebrated, so the move is fully shown the moment the bricks are seated.
            if (celebrating == 0)
            {
                RaiseShown();
            }
        }

        /// <summary>How many columns this move set celebrating and have not finished yet.</summary>
        private int celebrating;

        private void RaiseShown()
        {
            Action shown = BoardShown;

            if (shown != null)
            {
                shown();
            }
        }

        /// <summary>
        /// One column has finished its celebration. The last one to finish is what "the move is
        /// shown" means, which is why they are counted rather than assumed to be one: a single move
        /// can finish two colours.
        /// </summary>
        private void CelebrationFinished()
        {
            celebrating = Mathf.Max(0, celebrating - 1);

            if (celebrating == 0)
            {
                RaiseShown();
            }
        }

        /// <summary>
        /// Starts or stops the plume on every brick of the run in flight. The white lift is the
        /// glow's, because the plume and the glow are the same light — one number for one look
        /// (D-073).
        /// </summary>
        private void ShowPlumes(bool flying)
        {
            for (int index = 0; index < run.Count; index++)
            {
                BlockView brick = run[index];

                if (brick == null)
                {
                    continue;
                }

                if (!flying)
                {
                    brick.StopPlume();
                    continue;
                }

                if (animationConfig != null)
                {
                    brick.StartPlume(
                        animationConfig.GlowLift,
                        animationConfig.TrailTime,
                        animationConfig.TrailWidth,
                        animationConfig.TrailDensity);
                }
            }
        }

        /// <summary>
        /// The view does not work out what changed — the move says so. A reveal is a turn on a brick
        /// that never leaves its cell, a thaw is a sprite swap, and an opened cover is one object off.
        /// </summary>
        private void ShowWhatTheMoveReported(BoardMove move)
        {
            IReadOnlyList<CellRef> revealed = move.RevealedCells;

            for (int index = 0; index < revealed.Count; index++)
            {
                Reveal(revealed[index]);
            }

            IReadOnlyList<int> thawed = move.ThawedColumns;

            for (int index = 0; index < thawed.Count; index++)
            {
                columnViews[thawed[index]].Thaw();
            }

            IReadOnlyList<int> uncovered = move.UncoveredColumns;

            for (int index = 0; index < uncovered.Count; index++)
            {
                columnViews[uncovered[index]].SetCoverVisible(false);
            }

            // Last, because it is the biggest thing the move did. Reported by the move rather than
            // heard as an event, which is what puts it *here* — after the bricks are seated — instead
            // of at the tap, with the run still in the air (D-076).
            IReadOnlyList<BlockColourId> completed = move.CompletedColours;

            for (int index = 0; index < completed.Count; index++)
            {
                Celebrate(completed[index]);
            }
        }

        /// <summary>
        /// Shows what a hidden cell really was. The brick turns rather than changing between frames
        /// (D-099); `Board` had already decided the colour, so this is a look and nothing else, and the
        /// turn is free to be interrupted because it ends revealed either way.
        /// </summary>
        private void Reveal(CellRef cell)
        {
            ColumnView column = columnViews[cell.Column];

            if (cell.Cell >= column.BrickCount)
            {
                return;
            }

            BlockColourId colour = Session.State[cell.Column].ColourAt(cell.Cell);
            BlockSkin revealed = skins.GetSkin(colour);
            BlockSkin hidden = skins.HiddenSkin;

            // The four colours are asked for HERE because the skin set lives here and a column has
            // never held one (D-101). With no config, or no `?` skin to travel from, the brick simply
            // changes — the same trade the animator makes when its timings are missing: a correct board
            // and a console line beat a made-up feel (D-053).
            if (animationConfig == null || hidden == null)
            {
                column.RevealAt(cell.Cell, revealed, default);
                return;
            }

            RevealLook look = RevealLook.From(
                animationConfig,
                hidden.UiColour,
                skins.SymbolColour(hidden),
                revealed.UiColour,
                skins.SymbolColour(revealed));

            column.RevealAt(cell.Cell, revealed, look);
        }

        private void SpawnBlocks(BoardColumn column, ColumnView view)
        {
            for (int cell = 0; cell < column.Count; cell++)
            {
                BlockSkin skin;

                if (column.IsHiddenAt(cell))
                {
                    skin = skins.HiddenSkin;

                    if (skin == null)
                    {
                        Debug.LogError("[BoardView] " + skins.name + " has no ? brick, so a hidden cell cannot be drawn. Assign its hidden skin.", skins);
                        return;
                    }
                }
                else
                {
                    skin = skins.GetSkin(column.ColourAt(cell));
                }

                BlockView brick = pool.Take();
                brick.Apply(skin);
                view.Seat(brick);
                liveBlocks.Add(brick);
            }
        }

        /// <summary>
        /// A mystery column's art is an ordinary column: what is hidden is the bricks inside
        /// it, and they wear the skin set's `?` brick until they are revealed (D-021).
        /// </summary>
        private ColumnView PrefabFor(ColumnKind kind)
        {
            switch (kind)
            {
                case ColumnKind.Ice:
                    return iceColumn;
                case ColumnKind.Covered:
                    return coveredColumn;
                default:
                    return normalColumn;
            }
        }

        /// <summary>
        /// Frames whatever board was built: a 12-column level and a 4-column one need
        /// different sizes, so the size is computed rather than authored. Only the padding and
        /// the two reserved bands are tuning numbers, and they live in the config asset.
        /// <para>
        /// The bands are why the camera is not simply centred on the board any more. The HUD owns
        /// the top of the screen and the booster bar the bottom; the board is fitted into what is
        /// left and then pushed into the middle of it, because a board centred on a screen it
        /// shares is a board sliding under the plaque.
        /// </para>
        /// </summary>
        private void FrameCamera()
        {
            if (!boardCamera.orthographic)
            {
                Debug.LogError("[BoardView] " + boardCamera.name + " is a perspective camera; the board is drawn flat under an orthographic one (fingerprint.md → Space model).", boardCamera);
                return;
            }

            Vector2 boardSize = BoardLayout.BoardSize(
                layoutCells, layoutColumns, columnWidth, rowHeight, layout.ColumnGap, layout.RowGap);

            Transform cameraTransform = boardCamera.transform;
            Vector3 centre = columnRoot.position;

            // The angle is the camera's own: the scene decides how the board is looked at, and this
            // reads it rather than keeping a second copy of it in the config (D-050). Euler angles
            // come back in 0..360, so a downward 25° reads as 25 and an upward one as 335.
            float tilt = Mathf.DeltaAngle(0f, cameraTransform.eulerAngles.x);

            // How far the camera stands along its *own* view ray, which is the one measurement this
            // method does not disturb: the position it writes below is `centre - forward · ray`, and
            // `up` is perpendicular to `forward`, so re-reading it gives the same number back. Taken
            // as a depth in z instead, the band's own z component would be added to it every
            // resync and the camera would creep away from the board a fraction at a time.
            float viewRay = Vector3.Dot(centre - cameraTransform.position, cameraTransform.forward);

            float size = BoardLayout.OrthographicSize(
                boardSize, boardCamera.aspect, layout.CameraPadding, layout.TopReserve, layout.BottomReserve, tilt);

            boardCamera.orthographicSize = size;

            float bandOffset = BoardLayout.CameraCentreOffset(size, layout.TopReserve, layout.BottomReserve);

            // Built from the camera's own basis, not from world axes: `up` is what the reserved
            // bands are measured along and `forward` is what the distance is measured along, and
            // both already carry the tilt. Adding the band to world Y instead would mis-centre it
            // by the cosine of the angle, which is the kind of wrong that still looks fine.
            cameraTransform.position = centre
                                       + cameraTransform.up * bandOffset
                                       - cameraTransform.forward * viewRay;
        }

        private bool ReferencesReady()
        {
            string missing = null;

            if (boardCamera == null)
            {
                missing = "the board camera";
            }
            else if (columnRoot == null)
            {
                missing = "the column root";
            }
            else if (idleBlockRoot == null)
            {
                missing = "the idle block root";
            }
            else if (input == null)
            {
                missing = "the board input";
            }
            else if (animator == null)
            {
                missing = "the move animator";
            }
            else if (layout == null)
            {
                missing = "the board layout config";
            }
            else if (skins == null)
            {
                missing = "the block skin set";
            }
            else if (normalColumn == null || iceColumn == null || coveredColumn == null)
            {
                missing = "one of the column prefabs";
            }
            else if (blockPrefab == null)
            {
                missing = "the block prefab";
            }

            if (missing == null)
            {
                return true;
            }

            Debug.LogError("[BoardView] " + name + " cannot build a board: " + missing +
                           " is not assigned. Run Tools > Colorful Sort > Wire Game Scene.", this);
            return false;
        }
    }
}
