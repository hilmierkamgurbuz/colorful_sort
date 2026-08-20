using System;
using System.Collections;
using System.Collections.Generic;
using ColorfulSort.Board;
using ColorfulSort.Content;
using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// One column on the screen. Its height is not authored anywhere: the slot sprite is
    /// drawn <c>Tiled</c> in exact one-cell steps (D-007), so a four-cell column is the same
    /// sprite as a two-cell one with a taller size — which is the only way per-level capacity
    /// can reach the screen at all.
    /// <para>
    /// Ice needs nothing extra: the frost shelf and the icicles are part of the ice sprite
    /// family. A cover is assembled per cell from the pack's recipe, because the cover art
    /// ships as caps, a repeat and a separator rather than as one finished piece.
    /// </para>
    /// </summary>
    /// <summary>
    /// Every number a celebration needs, read off the config once and passed as one thing (D-076).
    /// <para>
    /// It exists because the alternative is a nine-argument call, where a mistuned board is one
    /// transposed pair of floats away and nothing in the compiler would notice. A copy rather than a
    /// reference to the asset, so the sequence cannot be retuned halfway through by an edit in the
    /// Inspector — and it is read-only, so it is no second writer of anything.
    /// </para>
    /// </summary>
    public readonly struct CelebrationLook
    {
        private CelebrationLook(
            float hop,
            float hopDuration,
            float hopStagger,
            float symbolGlow,
            float glowHold,
            float glowFade,
            int sparks,
            float burstRise,
            float burstSeconds,
            float burstSize,
            float burstSpread,
            float burstScatter,
            float whiteLift,
            float settleDuration,
            float shade,
            float shadowAlpha)
        {
            Hop = hop;
            HopDuration = hopDuration;
            HopStagger = hopStagger;
            SymbolGlow = symbolGlow;
            GlowHold = glowHold;
            GlowFade = glowFade;
            Sparks = sparks;
            BurstRise = burstRise;
            BurstSeconds = burstSeconds;
            BurstSize = burstSize;
            BurstSpread = burstSpread;
            BurstScatter = burstScatter;
            WhiteLift = whiteLift;
            SettleDuration = settleDuration;
            Shade = shade;
            ShadowAlpha = shadowAlpha;
        }

        /// <summary>How high one brick lifts out of its cell, in cells.</summary>
        public float Hop { get; }

        /// <summary>Seconds for one brick's hop, up and back down.</summary>
        public float HopDuration { get; }

        /// <summary>Seconds between one brick hopping and the next.</summary>
        public float HopStagger { get; }

        /// <summary>What a lit symbol's colour is multiplied by.</summary>
        public float SymbolGlow { get; }

        /// <summary>Seconds the symbols stay lit after the last brick has come down.</summary>
        public float GlowHold { get; }

        /// <summary>Seconds those symbols then take to come back down to their own colour.</summary>
        public float GlowFade { get; }

        /// <summary>Sparks thrown at the middle of the slot when the glow ends.</summary>
        public int Sparks { get; }

        /// <summary>The upward push that burst leaves with; the fall back down is the prefab's gravity.</summary>
        public float BurstRise { get; }

        /// <summary>Seconds one of those sparks lives — the way up and the way down together.</summary>
        public float BurstSeconds { get; }

        /// <summary>How big one of those sparks is, in cells.</summary>
        public float BurstSize { get; }

        /// <summary>How wide the area they come out of is; its height is the middle two cells.</summary>
        public float BurstSpread { get; }

        /// <summary>How far apart two sparks of the same burst drift, in cells per second.</summary>
        public float BurstScatter { get; }

        /// <summary>How much white sits on the spark's hue — the glow's own dial, so they are one light.</summary>
        public float WhiteLift { get; }

        /// <summary>Seconds for the shadow to arrive and the bricks to darken — one number, so they agree.</summary>
        public float SettleDuration { get; }

        /// <summary>What a settled brick's colour is multiplied by.</summary>
        public float Shade { get; }

        /// <summary>How opaque the shadow over a settled column ends up.</summary>
        public float ShadowAlpha { get; }

        /// <summary>Reads the whole look out of the tuning asset.</summary>
        public static CelebrationLook From(BoardAnimationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new CelebrationLook(
                config.CelebrationHop,
                config.CelebrationHopDuration,
                config.CelebrationHopStagger,
                config.CelebrationSymbolGlow,
                config.CelebrationGlowHold,
                config.CelebrationGlowFade,
                config.CelebrationSparks,
                config.CelebrationBurstRise,
                config.CelebrationBurstSeconds,
                config.CelebrationSparkSize,
                config.CelebrationBurstSpread,
                config.CelebrationBurstScatter,
                config.GlowLift,
                config.SettleDuration,
                config.SettleShade,
                config.SettleShadowAlpha);
        }
    }

    /// <summary>
    /// Every number the placement burst needs, read off the config once and passed as one thing.
    /// <para>
    /// V-8d passed these as loose arguments and said so in its preflight: at three, a struct was
    /// ceremony. At five — with a count, a height, a duration, a drop and a white lift, three of them
    /// floats in a row — it is exactly what D-076 wanted the celebration's struct for. A transposed
    /// pair mistunes the board and the compiler notices nothing.
    /// </para>
    /// </summary>
    public readonly struct LandingLook
    {
        private LandingLook(
            int sparks,
            float height,
            float seconds,
            float drop,
            float size,
            float spread,
            float wander,
            float dip,
            float scatter,
            float whiteLift)
        {
            Sparks = sparks;
            Height = height;
            Seconds = seconds;
            Drop = drop;
            Size = size;
            Spread = spread;
            Wander = wander;
            Dip = dip;
            Scatter = scatter;
            WhiteLift = whiteLift;
        }

        /// <summary>Sparks thrown up from under each brick that has just been set down.</summary>
        public int Sparks { get; }

        /// <summary>How far they climb before they are gone, in cells. With no gravity this is exact.</summary>
        public float Height { get; }

        /// <summary>Seconds that climb takes; the speed is the height over it.</summary>
        public float Seconds { get; }

        /// <summary>How far under the brick's middle the burst starts, in cells.</summary>
        public float Drop { get; }

        /// <summary>How big one of those sparks is, in cells.</summary>
        public float Size { get; }

        /// <summary>How much of the brick's base they rise off, in cells.</summary>
        public float Spread { get; }

        /// <summary>How far a spark wanders off a straight climb.</summary>
        public float Wander { get; }

        /// <summary>How hard it drops before it climbs, as a share of the climbing speed.</summary>
        public float Dip { get; }

        /// <summary>How far apart two sparks of the same burst drift, in cells per second.</summary>
        public float Scatter { get; }

        /// <summary>How much white sits on the spark's hue — the glow's own dial (D-063).</summary>
        public float WhiteLift { get; }

        /// <summary>Reads the whole look out of the tuning asset.</summary>
        public static LandingLook From(BoardAnimationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new LandingLook(
                config.LandingSparks,
                config.LandingRiseHeight,
                config.LandingRiseSeconds,
                config.LandingSparkDrop,
                config.LandingSparkSize,
                config.LandingSparkSpread,
                config.LandingSparkWander,
                config.LandingSparkDip,
                config.LandingSparkScatter,
                config.GlowLift);
        }
    }

    /// <summary>
    /// A reveal, as the four colours it travels between and the seconds it takes. The `?` brick's two
    /// and the real brick's two, because a reveal walks from one *skin* to another and there is
    /// nothing to scale a colour from — the ends belong to different materials (D-101).
    /// <para>
    /// The colours arrive from the caller rather than being looked up here, because the skin set lives
    /// on <c>BoardView</c> and a column has never held one. That is also why this carries them at all:
    /// yesterday the reveal took a bare float and the note said it would earn a struct the day it
    /// gained a second field (D-099). It has.
    /// </para>
    /// </summary>
    public readonly struct RevealLook
    {
        private RevealLook(float seconds, Color hiddenBody, Color hiddenSymbol, Color body, Color symbol)
        {
            Seconds = seconds;
            HiddenBody = hiddenBody;
            HiddenSymbol = hiddenSymbol;
            Body = body;
            Symbol = symbol;
        }

        /// <summary>Seconds the whole change takes. Zero means "no fade": the brick simply changes.</summary>
        public float Seconds { get; }

        /// <summary>The `?` brick's own colour, where the body starts.</summary>
        public Color HiddenBody { get; }

        /// <summary>The `?` mark's colour, where the symbol starts (D-099).</summary>
        public Color HiddenSymbol { get; }

        /// <summary>The real colour's brick, where the body ends.</summary>
        public Color Body { get; }

        /// <summary>The real colour's engraved symbol, where the symbol ends.</summary>
        public Color Symbol { get; }

        public static RevealLook From(
            BoardAnimationConfig config, Color hiddenBody, Color hiddenSymbol, Color body, Color symbol)
        {
            return new RevealLook(
                config == null ? 0f : config.RevealFadeDuration, hiddenBody, hiddenSymbol, body, symbol);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ColumnView : MonoBehaviour
    {
        /// <summary>
        /// The plate the lowest brick stands on. The prefab tool makes the object and holds the same
        /// name in a constant of its own; this is the runtime half of that agreement, and the tool
        /// should read it from here the next time it is edited.
        /// </summary>
        public const string BasePlateName = "Base";

        /// <summary>
        /// How wavy a drifting light's wander is. One constant, shared with the plume behind a flying
        /// brick, because they are the same effect at two sizes — and the prefab tool reads it from
        /// here rather than keeping its own copy, the same way it takes the base plate's name (D-071).
        /// The *strength* is authored per effect; only the frequency is common.
        /// </summary>
        public const float WanderFrequency = 0.6f;

        [Tooltip("The column sprite. Draw Mode must be Tiled — that is what makes capacity visible (D-007).")]
        [SerializeField]
        private SpriteRenderer slot;

        [Tooltip("Bricks are parented here. Its local Z is what puts them in front of the slot art (D-005).")]
        [SerializeField]
        private Transform blockRoot;

        [Tooltip("Cover pieces are built here, in front of the bricks.")]
        [SerializeField]
        private Transform coverRoot;

        [Tooltip("What an ice column wears once it has thawed. The ice art is integrated — frost shelf and icicles are part of the sprite — so thawing is a sprite swap (D-030).")]
        [SerializeField]
        private Sprite thawedSlot;

        [Tooltip("The line drawn on each boundary between two cells. Optional: art that draws its own separators leaves it empty.")]
        [SerializeField]
        private Sprite cellDivider;

        [Tooltip("The shadow drawn over a finished column, in front of its bricks. Optional: without it a settled column only darkens.")]
        [SerializeField]
        private SpriteRenderer settledShadow;

        [Tooltip("The finish burst: sparks thrown at the middle of the slot when a colour is gathered. Carries gravity, so they come back down.")]
        [SerializeField]
        private ParticleSystem finish;

        [Tooltip("The placement burst: sparks that rise from under a brick just set down. Carries NO gravity, so they climb and fade (D-078).")]
        [SerializeField]
        private ParticleSystem rise;

        [Header("Covered columns only")]
        [Tooltip("Sorting order for cover pieces. The art pack's overlay order is 30.")]
        [SerializeField]
        private int coverSortingOrder;

        [SerializeField]
        private Sprite coverTopCap;

        [SerializeField]
        private Sprite coverCell;

        [SerializeField]
        private Sprite coverSeparator;

        [SerializeField]
        private Sprite coverBottomCap;

        private readonly List<BlockView> bricks = new List<BlockView>();

        /// <summary>The running settle fade, or null. Kept so a second call cannot leave two tweens fighting.</summary>
        private Coroutine settleFade;

        /// <summary>The running celebration, for the same reason: one hop at a time on one column.</summary>
        private Coroutine celebration;

        /// <summary>The running `?` change, for the same reason: one reveal at a time on one column.</summary>
        private Coroutine revealFade;

        // What the running change still owes. The brick so its painted colours can be handed back, and
        // the skin so an interrupted change still ENDS revealed: a reveal is a fact `Board` already
        // recorded, so the one outcome the view may not produce is a brick left wearing the `?`
        // (D-099). The skin is nulled the moment it is applied, which is what stops a settle from
        // applying it twice.
        private BlockView fadingBrick;
        private BlockSkin fadingSkin;
        private RevealLook fadingLook;

        /// <summary>What this column's bricks are currently multiplied by; 1 is untouched.</summary>
        private float currentShade = 1f;

        /// <summary>The size this column's own sprite reports. Valid after <see cref="Build"/>.</summary>
        public ColumnMetrics Metrics { get; private set; }

        /// <summary>How many cells this column holds, from the attempt's data.</summary>
        public int Capacity { get; private set; }

        /// <summary>Where bricks live. Cell positions are local to this transform.</summary>
        public Transform BlockRoot => blockRoot;

        /// <summary>
        /// How many bricks this column is showing. It mirrors <c>Board</c> and is only ever
        /// changed from what a move reports, so it cannot drift into its own opinion.
        /// </summary>
        public int BrickCount => bricks.Count;

        public BlockView BrickAt(int cell)
        {
            if (cell < 0 || cell >= bricks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "This column is showing " + bricks.Count + " brick(s).");
            }

            return bricks[cell];
        }

        /// <summary>Puts a brick in the next free cell, parented and positioned.</summary>
        public void Seat(BlockView brick)
        {
            if (brick == null)
            {
                throw new ArgumentNullException(nameof(brick));
            }

            if (bricks.Count >= Capacity)
            {
                Debug.LogError("[BoardView] " + name + " already holds " + Capacity + " brick(s); Board would not have allowed this move.", this);
                return;
            }

            bricks.Add(brick);
            Place(brick, bricks.Count - 1);
        }

        /// <summary>
        /// Hands the top <paramref name="count"/> bricks to the caller, bottom of the run first,
        /// and forgets them. Transforms are left alone — the caller is about to fly them.
        /// </summary>
        public void ReleaseTop(int count, List<BlockView> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (count < 0 || count > bricks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "This column is showing " + bricks.Count + " brick(s).");
            }

            // A brick about to fly must not still be mid-reveal: it would arrive wearing painted colours
            // that no longer match its skin, and `Apply` on the next spawn is the only thing that would
            // ever clear them. Settling first hands it over already revealed and back on its own
            // materials (D-101). Reachable since taps stopped waiting for flights to land (D-098).
            SettleReveal();

            int first = bricks.Count - count;

            for (int index = first; index < bricks.Count; index++)
            {
                into.Add(bricks[index]);
            }

            bricks.RemoveRange(first, count);
        }

        /// <summary>Forgets every brick without touching it. The board is being torn down.</summary>
        public void ForgetBricks()
        {
            // Before the list goes, or the turn would keep writing the rotation of a brick this column
            // no longer owns — and the pool hands that same brick to another cell.
            SettleReveal();

            bricks.Clear();
        }

        /// <summary>
        /// Puts every brick back in its own cell. Used when part of a lifted run flew away and the
        /// rest has to come down, and after a thaw moves the cells.
        /// </summary>
        public void ReseatAll()
        {
            for (int cell = 0; cell < bricks.Count; cell++)
            {
                Place(bricks[cell], cell);
            }
        }

        /// <summary>
        /// The column has thawed: it wears the normal slot art from now on. Its cells move down by
        /// the difference in skirt — an ice column is empty while it is locked, so normally there
        /// is nothing to move, but any brick present is re-seated rather than trusted to be absent.
        /// </summary>
        public void Thaw()
        {
            if (thawedSlot == null)
            {
                Debug.LogError("[BoardView] " + name + " has no thawed sprite, so it cannot stop looking frozen. Rebuild the column prefabs.", this);
                return;
            }

            slot.sprite = thawedSlot;
            Metrics = ColumnMetrics.FromSprite(thawedSlot);
            slot.size = new Vector2(Metrics.Width, Metrics.Height(Capacity));

            // The thawed tray has its own skirt, so every cell boundary moved with it.
            BuildDividers();
            ReseatAll();
        }

        /// <summary>
        /// Sizes the column for the capacity the attempt gives it and builds its cover if it
        /// has one. Reads the column, never writes it.
        /// </summary>
        public void Build(BoardColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            // The brick list is about to be replaced, so a turn still running belongs to bricks that are
            // on their way back to the pool.
            SettleReveal();

            if (slot == null || blockRoot == null || coverRoot == null)
            {
                Debug.LogError("[BoardView] " + name + " is missing its slot, block root or cover root; rebuild the column prefabs with Tools > Colorful Sort > Build BoardView Prefabs.", this);
                return;
            }

            Metrics = ColumnMetrics.FromSprite(slot.sprite);
            Capacity = column.Capacity;

            // Which of the two size-respecting modes it is comes from the art (see
            // ColumnMetrics.TilesPerCell): the pack's tray repeats a cell, the hand-drawn one
            // stretches. Simple is the one mode that ignores `size` outright, so a column set to
            // it would silently draw one sprite's worth whatever its capacity.
            SpriteDrawMode expected = Metrics.TilesPerCell ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;

            if (slot.drawMode != expected)
            {
                // Not corrected here on purpose: the prefab is the authority for how a column
                // draws (D-007). Fixing it in code would hide a broken prefab forever.
                Debug.LogError("[BoardView] " + name + "'s slot renderer is " + slot.drawMode + " but its sprite '" +
                               slot.sprite.name + "' asks for " + expected + ", so its size is ignored and every column " +
                               "will draw the sprite's own height. Rebuild the column prefabs.", this);
            }

            slot.size = new Vector2(Metrics.Width, Metrics.Height(Capacity));

            BuildDividers();
            SizeSettledShadow();
            BuildCover(column.Kind);
            CheckBasePlate();
        }

        /// <summary>
        /// Says out loud why a column has no floor under its bricks.
        /// <para>
        /// A column added by the booster was reported drawing no plate while the authored ones drew
        /// theirs, and nothing in the rules or the layout can produce that — every column is the same
        /// prefab, instantiated in the same loop. So this reports the three things that *can*: the
        /// object being absent, its renderer drawing nothing, and the plate sitting somewhere other
        /// than under cell 0. Both numbers are read off the objects rather than assumed, which is
        /// what makes the console line an answer instead of another guess.
        /// </para>
        /// <para>
        /// Silent when the plate is where it belongs — one <c>Find</c> per column per rebuild, and
        /// rebuilds happen when a level opens or a booster fires, never per frame. It stays in for
        /// good: a column with no visible floor is obvious on a phone and invisible in code.
        /// </para>
        /// </summary>
        private void CheckBasePlate()
        {
            Transform plate = transform.Find(BasePlateName);

            if (plate == null)
            {
                Debug.LogError("[BoardView] " + name + " has no '" + BasePlateName + "' child, so its bricks stand on nothing. " +
                               "Rebuild the column prefabs with Tools > Colorful Sort > Build BoardView Prefabs.", this);
                return;
            }

            var plateRenderer = plate.GetComponent<MeshRenderer>();

            if (plateRenderer == null || !plateRenderer.enabled || plateRenderer.sharedMaterial == null)
            {
                string fault = plateRenderer == null ? "it has no MeshRenderer"
                    : !plateRenderer.enabled ? "its renderer is disabled"
                    : "its renderer has no material";

                Debug.LogError("[BoardView] " + name + "'s base plate draws nothing: " + fault + ". " +
                               "Rebuild the column prefabs with Tools > Colorful Sort > Build BoardView Prefabs.", this);
                return;
            }

            // The plate's job is to close the bottom of cell 0, so its top belongs within half a cell
            // of that cell's floor. Measured in the same frame and the same space, before the column
            // is placed — the two positions move together, so their difference is the seating.
            float plateTop = plateRenderer.bounds.max.y;
            float cellFloor = CellPosition(0).y - 0.5f;

            if (Mathf.Abs(plateTop - cellFloor) > 0.5f)
            {
                Debug.LogError("[BoardView] " + name + "'s base plate tops out at y " + plateTop.ToString("0.###") +
                               " while cell 0's floor is at y " + cellFloor.ToString("0.###") + ", so it is not under " +
                               "the bricks. Its transform is the prefab's (D-053), so fix it there.", this);
            }
        }

        /// <summary>The middle of a cell in world space. Cells are indexed bottom-up, as the rules index them.</summary>
        public Vector3 CellPosition(int cellIndex)
        {
            return blockRoot.TransformPoint(CellLocalPosition(cellIndex));
        }

        /// <summary>The middle of a cell, relative to <see cref="BlockRoot"/>.</summary>
        public Vector3 CellLocalPosition(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= Capacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cellIndex), cellIndex, "Cell " + cellIndex + " is outside a column of " + Capacity + ".");
            }

            return new Vector3(0f, Metrics.CellCentreY(cellIndex), 0f);
        }

        /// <summary>Hides or shows the whole cover. The opening animation is part 2's.</summary>
        public void SetCoverVisible(bool visible)
        {
            if (coverRoot != null)
            {
                coverRoot.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// The pack's cover recipe, in Unity units and measured from the cell floor: the
        /// bottom cap sits in the first cell, one repeat per cell above it, a separator on
        /// every cell boundary, and the top cap hangs from the column's top.
        /// </summary>
        private void BuildCover(ColumnKind kind)
        {
            ClearCover();

            if (kind != ColumnKind.Covered)
            {
                coverRoot.gameObject.SetActive(false);
                return;
            }

            coverRoot.gameObject.SetActive(true);

            if (coverCell == null || coverTopCap == null || coverBottomCap == null)
            {
                Debug.LogError("[BoardView] " + name + " is a covered column with no cover sprites assigned; rebuild the column prefabs.", this);
                return;
            }

            AddCoverPiece(coverBottomCap, Metrics.CellCentreY(0), "Cover_Bottom");

            for (int cell = 1; cell < Capacity; cell++)
            {
                AddCoverPiece(coverCell, Metrics.CellCentreY(cell), "Cover_Cell_" + cell.ToString("00"));

                if (coverSeparator != null)
                {
                    AddCoverPiece(coverSeparator, Metrics.Skirt + cell, "Cover_Separator_" + cell.ToString("00"));
                }
            }

            // The top cap's pivot is its bottom edge, so it hangs off the top cell's ceiling.
            AddCoverPiece(coverTopCap, Metrics.Skirt + Capacity, "Cover_Top");
        }

        private void Place(BlockView brick, int cell)
        {
            Transform brickTransform = brick.transform;
            brickTransform.SetParent(blockRoot, false);
            brickTransform.localPosition = CellLocalPosition(cell);

            // Rotation is deliberately not touched. It used to be forced to identity here, which
            // quietly overrode the Block prefab's own orientation every time a brick was seated —
            // and that is what kept every symbol facing away from the camera even after the prefab
            // was turned round (D-047, D-049). Which way a brick faces is the prefab's answer;
            // this method only says which cell it sits in, and nothing in the view rotates bricks.
        }

        /// <summary>
        /// Gives the shadow the one number it cannot be authored with: its height, which follows the
        /// column's capacity. Its position and scale are the **prefab's** — they used to be copied
        /// from the tray here, which was an invention rather than a derivation (the shadow art has
        /// its own proportions and its own padding) and it quietly overwrote the values a designer
        /// had tuned on every build (D-058, the same correction D-053 made for the base plate).
        /// </summary>
        private void SizeSettledShadow()
        {
            if (settledShadow == null)
            {
                return;
            }

            settledShadow.size = new Vector2(Metrics.Width, Metrics.Height(Capacity));
        }

        /// <summary>
        /// Sinks this column into its slot, or brings it back: the shadow fades to
        /// <paramref name="shadowAlpha"/> while every brick is multiplied down to
        /// <paramref name="shade"/>. A <paramref name="duration"/> of zero does it at once, which is
        /// what a rebuilt board wants — settled is read off `Board` rather than remembered, so a
        /// resync or an undo lands here with no fade and nothing to reconcile (D-057).
        /// </summary>
        public void SetSettled(bool settled, float duration, float shade, float shadowAlpha)
        {
            float targetAlpha = settled ? shadowAlpha : 0f;
            float targetShade = settled ? shade : 1f;

            // A celebration in flight is over: this is the authority on what a settled column looks
            // like, and a hop still writing brick positions would be fighting it (D-075).
            if (celebration != null)
            {
                StopCoroutine(celebration);
                celebration = null;
                ReseatAll();
                SetSymbolGlow(1f);
            }

            if (settleFade != null)
            {
                StopCoroutine(settleFade);
                settleFade = null;
            }

            // And a `?` still changing is over too, for the same reason and with the same outcome as a
            // cut-short celebration: the brick ends revealed and on its own materials, rather than
            // holding a painted mid-blend colour while this method darkens it (D-101).
            SettleReveal();

            if (duration <= 0f || !isActiveAndEnabled)
            {
                ApplySettle(targetAlpha, targetShade);
                return;
            }

            settleFade = StartCoroutine(Settle(targetAlpha, targetShade, duration));
        }

        /// <summary>
        /// The column has just been finished, and this is it being pleased about it (D-075, D-076):
        /// its bricks hop one after another, each symbol lighting as its brick leaves its cell and
        /// staying lit, they come back together, hold that brightness a moment, let go of it in a
        /// scatter of sparks — and only then does the shadow come over them.
        /// <para>
        /// One coroutine for the whole sequence, because the order *is* the effect — the brightness
        /// has to end with the sparks and the shadow has to arrive after both. Every frame writes the
        /// hop from the bricks' seated positions rather than adding to where they are, so an
        /// interruption can shorten the hop but never leave a brick off its cell (D-059); the run ends
        /// by reseating them all, which is the one guarantee this method makes.
        /// </para>
        /// <para>
        /// A second completion landing on a column mid-celebration replaces the first rather than
        /// stacking on it, exactly as the settle already does.
        /// </para>
        /// </summary>
        /// <param name="onFinished">
        /// Called once the column has hopped, burst, and finished sinking into its slot — which is
        /// what "the player has seen this" means, and what the win popup waits for (D-088). Called on
        /// every path, including the ones that skip the hop, or a caller counting celebrations would
        /// wait forever for one that never started.
        /// </param>
        public void Celebrate(CelebrationLook look, Action onFinished = null)
        {
            if (celebration != null)
            {
                StopCoroutine(celebration);
                celebration = null;
            }

            // No hop to play, or no scene to play it in: the column still has to end up settled, and
            // that is the part the board's state depends on.
            if (look.HopDuration <= 0f || !isActiveAndEnabled)
            {
                SetSettled(true, look.SettleDuration, look.Shade, look.ShadowAlpha);

                if (onFinished != null)
                {
                    onFinished();
                }

                return;
            }

            celebration = StartCoroutine(Celebrating(look, onFinished));
        }

        private IEnumerator Celebrating(CelebrationLook look, Action onFinished)
        {
            // Bottom brick first, each one a stagger behind the last: a finished column reads as four
            // bricks pleased in turn rather than as one block of them going up (D-076). The last hop
            // therefore *starts* after the others have, which is what the total below accounts for.
            float total = look.HopDuration + look.HopStagger * Mathf.Max(0, bricks.Count - 1);
            float elapsed = 0f;
            int lit = 0;

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                for (int cell = 0; cell < bricks.Count; cell++)
                {
                    if (bricks[cell] == null)
                    {
                        continue;
                    }

                    float progress = Mathf.Clamp01((elapsed - cell * look.HopStagger) / look.HopDuration);

                    // A half sine: nothing at both ends, highest in the middle. The brick leaves its
                    // cell and comes back to it, and the curve says so rather than the code hoping so.
                    float rise = look.Hop * Mathf.Sin(progress * Mathf.PI);
                    bricks[cell].transform.localPosition = CellLocalPosition(cell) + new Vector3(0f, rise, 0f);
                }

                // A brick's symbol lights as it leaves its cell and stays lit — the column brightens
                // one brick at a time and lets go all at once. Hops start in order, so the count is
                // all the bookkeeping this needs and there is no per-frame array to allocate.
                while (lit < bricks.Count && elapsed >= lit * look.HopStagger)
                {
                    if (bricks[lit] != null)
                    {
                        bricks[lit].SetSymbolGlow(look.SymbolGlow);
                    }

                    lit++;
                }

                yield return null;
            }

            ReseatAll();

            // The bricks are home, and this is where the finish happens. The fade and the burst
            // **overlap** now rather than queueing: the light starts going out as the column comes
            // together, the sparks come out a moment into that, and the two end together (D-083).
            // Chased through three shapes — hold-then-fade-then-throw, then throw-at-the-end-of-fade —
            // and both read as two events near each other rather than one.
            //
            // Still one coroutine, which is D-075's whole point: the order *is* the effect, and two
            // coroutines would need synchronising rather than simply being written down in order.
            float lead = Mathf.Clamp(look.GlowHold, 0f, look.GlowFade);
            float fading = 0f;
            bool thrown = false;

            while (fading < look.GlowFade)
            {
                fading += Time.deltaTime;

                // NOT smoothstep, unlike the settle and the move animator, and this is the one place in
                // the project where that easing is wrong: its slope is zero at the end, so the last
                // third is invisible — and a symbol stops reading as *lit* even earlier, once its
                // multiplier drops under bloom's threshold (D-066). A curve that holds and then falls
                // away is what makes the end of the glow a moment a player can see (D-082).
                float linear = Mathf.Clamp01(fading / look.GlowFade);
                SetSymbolGlow(Mathf.Lerp(look.SymbolGlow, 1f, linear * linear * linear));

                if (!thrown && fading >= lead)
                {
                    thrown = true;
                    Scatter(look);
                }

                yield return null;
            }

            SetSymbolGlow(1f);

            // A fade of nothing still owes the burst: the loop above never ran.
            if (!thrown)
            {
                Scatter(look);
            }

            celebration = null;
            SetSettled(true, look.SettleDuration, look.Shade, look.ShadowAlpha);

            // The settle is a coroutine of exactly this length, so waiting it out is not a guess about
            // when the column has landed — it is the same number, read from the same look. Only then
            // is there nothing left on screen for this move.
            if (look.SettleDuration > 0f)
            {
                yield return new WaitForSeconds(look.SettleDuration);
            }

            if (onFinished != null)
            {
                onFinished();
            }
        }

        /// <summary>
        /// The finish burst: one throw of sparks at the middle of the slot, the moment the glow lets
        /// go. They are pushed up and the system's own gravity brings them back down (D-078).
        /// <para>
        /// One burst for the column rather than one per brick, which is what it used to be: what the
        /// player just finished is a *colour*, and the column is the thing that says so — four bursts
        /// at four bricks read as four separate small events rather than one finish.
        /// </para>
        /// </summary>
        private void Scatter(CelebrationLook look)
        {
            if (look.Sparks <= 0 || look.BurstSeconds <= 0f)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams();
            emit.applyShapeToPosition = true;
            emit.velocity = Vector3.up * look.BurstRise;
            emit.startLifetime = look.BurstSeconds;
            emit.startSize = look.BurstSize;

            // Any brick will do: a column that has just been finished holds one colour, which is the
            // whole reason it is celebrating.
            emit.startColor = Dress(finish, BrickAt(0), look.WhiteLift);

            // Out of an **area** spanning the middle two cells, in one throw. Two throws at two cell
            // centres read as two events at two spots; a column letting go of a colour is one event
            // over a region of itself (D-082). The height is the column's own two cells rather than an
            // authored number, because that is a fact about the shape rather than a taste.
            int low = (Capacity - 1) / 2;
            int high = Capacity / 2;

            Shape(finish, look.BurstSpread, CellSpan() * (low == high ? 1f : 2f));
            emit.position = transform.TransformPoint((CellLocalPosition(low) + CellLocalPosition(high)) * 0.5f);

            // A clean arc rather than the prefab's randomised pull: with a lifetime of t and a launch
            // speed of v, a pull of 2v/t puts the apex at the halfway mark and brings them back down by
            // the end, which is the "clearly rises, then falls" that was asked for.
            Slow(finish, look.BurstSeconds > 0.0001f ? 2f * look.BurstRise / look.BurstSeconds : 0f);
            Drift(finish, look.BurstScatter);

            Fire(finish, emit, look.Sparks);
        }

        /// <summary>
        /// One cell's height in this column's own space. Measured off two cells rather than assumed,
        /// and only falling back on the project's "one cell is one unit" invariant when there is a
        /// single cell to measure between.
        /// </summary>
        private float CellSpan()
        {
            return Capacity > 1 ? Mathf.Abs(CellLocalPosition(1).y - CellLocalPosition(0).y) : 1f;
        }

        /// <summary>
        /// Changes a `?` brick into what it really was, softly and without moving it. The `?` dissolves
        /// into the brick's own colour while that colour travels from the `?` grey to the real one; the
        /// skin changes at the moment the symbol has no contrast left; then the real symbol emerges out
        /// of the body colour (D-101, replacing D-099's turn).
        /// <para>
        /// Contrast is the only thing hiding the swap here, because the symbol is embossed *geometry* —
        /// slot 1 of the brick mesh — so the shape changes with the skin whatever the colours are doing.
        /// Painting the symbol its body's colour is what makes that shape change unreadable. Not
        /// invisible: an embossed shape in its body's colour still catches the light as "nothing but a
        /// faint shadow" (D-052), so a very slight relief remains at the midpoint. That is the accepted
        /// price of a straight change; the alternative was transparency, which would mean transparent
        /// brick materials and the batching they pay for (D-004).
        /// </para>
        /// <para>
        /// Nothing on the transform is touched — no rotation, no scale — which is what makes the
        /// interruption contract smaller than the turn's was: there is no pose to restore, only painted
        /// colours to hand back.
        /// </para>
        /// </summary>
        public void RevealAt(int cell, BlockSkin revealed, RevealLook look)
        {
            if (revealed == null || cell < 0 || cell >= bricks.Count)
            {
                return;
            }

            BlockView brick = bricks[cell];

            if (brick == null)
            {
                return;
            }

            // A second reveal on this column replaces the first, exactly as the settle and the
            // celebration do — and the first still ends revealed, because settling applies its skin.
            SettleReveal();

            // No time to change in, or no scene to change in: the brick still has to end up showing its
            // real colour, because that is the part `Board` has already decided.
            if (look.Seconds <= 0f || !isActiveAndEnabled)
            {
                brick.Apply(revealed);
                return;
            }

            fadingBrick = brick;
            fadingSkin = revealed;
            fadingLook = look;

            revealFade = StartCoroutine(Fading());
        }

        /// <summary>
        /// Where the two painted colours are at a point in the change. Pure and static so the one
        /// property the whole effect rests on can be tested without a scene: at the midpoint the symbol
        /// is exactly the body's colour, which is what makes the mesh swap unreadable. Break that and
        /// the swap becomes visible — a fault nobody catches by eye at 0.45 seconds (D-101).
        /// <para>
        /// Eased end to end rather than per half, so the change is quickest where the swap hides and
        /// slowest at the two ends the player actually reads.
        /// </para>
        /// </summary>
        public static void RevealColours(RevealLook look, float progress, out Color body, out Color symbol)
        {
            float clamped = Mathf.Clamp01(progress);
            float eased = clamped * clamped * (3f - 2f * clamped);

            // The body finishes its journey by the midpoint, so the skin swap — which brings the real
            // material's own colour with it — lands on a body that is already that colour.
            body = Color.Lerp(look.HiddenBody, look.Body, Mathf.Clamp01(eased * 2f));

            symbol = eased < 0.5f
                ? Color.Lerp(look.HiddenSymbol, body, Mathf.Clamp01(eased * 2f))
                : Color.Lerp(body, look.Symbol, Mathf.Clamp01((eased - 0.5f) * 2f));
        }

        private IEnumerator Fading()
        {
            float elapsed = 0f;

            while (elapsed < fadingLook.Seconds)
            {
                elapsed += Time.deltaTime;

                // Checked first: the pool can destroy a brick under a running change, and Unity reports
                // that as a null this reference would otherwise call Apply on.
                if (fadingBrick == null)
                {
                    break;
                }

                float progress = elapsed / fadingLook.Seconds;

                Color body;
                Color symbol;
                RevealColours(fadingLook, progress, out body, out symbol);

                // Halfway is where the symbol has no contrast, so the shape may change here and nowhere
                // else. The paint below then carries the new skin's slots at the same two colours the old
                // ones were showing, which is what keeps the frame continuous.
                if (progress >= 0.5f && fadingSkin != null)
                {
                    fadingBrick.Apply(fadingSkin);
                    fadingSkin = null;
                }

                fadingBrick.PaintReveal(body, symbol);

                yield return null;
            }

            revealFade = null;
            SettleReveal();
        }

        /// <summary>
        /// Ends a reveal wherever it got to: the real skin on, the painted colours handed back to their
        /// materials. Called by the change itself when it finishes and by every path that cuts one
        /// short, because a `?` still showing after `Board` has revealed the cell is the view
        /// disagreeing with the rules — and a brick left frozen mid-blend is worse.
        /// <para>
        /// Clearing the paint rather than writing the final colours is what makes the end exact: the
        /// material already holds them, so there is no second copy to drift from a re-skin (the same
        /// reason <see cref="BlockView.SetShade"/> at 1 clears instead of restoring).
        /// </para>
        /// </summary>
        private void SettleReveal()
        {
            if (revealFade != null)
            {
                StopCoroutine(revealFade);
                revealFade = null;
            }

            if (fadingBrick != null)
            {
                if (fadingSkin != null)
                {
                    fadingBrick.Apply(fadingSkin);
                }

                fadingBrick.ClearReveal();
            }

            fadingBrick = null;
            fadingSkin = null;
        }

        /// <summary>
        /// The sparks a landing throws when it finished nothing: a small burst under every brick from
        /// <paramref name="firstCell"/> upward, pushed *up* rather than thrown outward, with no hop
        /// and no symbol flash. A brick set down on its own, not a colour gathered (D-077).
        /// <para>
        /// The range is open-ended on purpose. Bricks land on top of a column, so everything above the
        /// height it had before the flight is exactly what has just arrived — the caller remembers one
        /// number instead of keeping a count in step with how many were actually seated.
        /// </para>
        /// <para>
        /// It has its own system, not the finish burst's, and the reason is one thing
        /// <see cref="ParticleSystem.EmitParams"/> cannot override: **gravity**. It can set a
        /// particle's position, velocity, lifetime, size and colour per burst, but the pull on it
        /// belongs to the system — and these sparks must climb and fade while the finish burst's must
        /// come back down. Two systems per column is what that costs (D-078).
        /// </para>
        /// <para>
        /// Because that system carries no gravity, the climb is exactly speed × lifetime, which is why
        /// the caller hands over a height and a duration rather than a speed: the distance asked for is
        /// the distance travelled, and the two numbers cannot drift apart. Their spread and their fade
        /// are the system's own — no randomness is drawn in code here, which is what keeps the
        /// attempt's seeded RNG the only one this project has.
        /// </para>
        /// </summary>
        public void SparkLanding(int firstCell, LandingLook look)
        {
            if (look.Sparks <= 0 || look.Seconds <= 0f)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams();
            emit.applyShapeToPosition = true;
            emit.startLifetime = look.Seconds;
            emit.startSize = look.Size;

            // Down a little, then up, then easing to nothing — three phases, which a launch speed and
            // a constant pull cannot give (that is one). So the whole motion is a velocity curve the
            // view writes, the launch velocity is zero, and the pull is switched off under it (D-082).
            emit.velocity = Vector3.zero;
            Slow(rise, 0f);
            Climb(rise, look.Height, look.Seconds, look.Dip, look.Scatter);

            // Off the brick's base rather than out of a point beneath it, and wandering rather than
            // rising dead straight — the two things that made it read as mechanical.
            Shape(rise, look.Spread, 0f);
            Wander(rise, look.Wander);

            for (int cell = Mathf.Max(0, firstCell); cell < bricks.Count; cell++)
            {
                if (bricks[cell] == null)
                {
                    continue;
                }

                // A run that lands is one colour, so this dresses the same shape every time round —
                // but it is asked per brick rather than hoisted, because that is true of the *rule*
                // and not of this loop, and the day a mixed run can land the loop is already right.
                emit.startColor = Dress(rise, bricks[cell], look.WhiteLift);

                // Under the brick, not out of it: a landing pushes what was beneath it upward, and
                // starting the burst inside the mesh would hide half of it in the brick's own faces.
                emit.position = bricks[cell].transform.position - new Vector3(0f, look.Drop, 0f);
                Fire(rise, emit, look.Sparks);
            }
        }

        /// <summary>
        /// Points a burst at the symbol it is about to throw and answers with the colour to throw it
        /// in — the brick's own hue as light, the same formula the glow behind a lifted run and the
        /// plume behind a flying one are lit with (D-063, D-073).
        /// <para>
        /// The mesh is the renderer's, not the particle's: Unity draws one mesh per system, so a burst
        /// sets the shape and every particle it emits takes it. That is exact for these two, because a
        /// landing run is one colour and a finished column is one colour — the only thing it cannot
        /// express is two live bursts of *different* colours in one column at once, which the flight
        /// lock makes unlikely and which is written down rather than left to be discovered (D-080).
        /// </para>
        /// <para>
        /// A skin with no spark mesh falls back to the billboard it used to be instead of drawing
        /// nothing at all — a mesh renderer with no mesh is invisible, and this series has already
        /// spent three rounds on effects that were silently absent.
        /// </para>
        /// </summary>
        private static Color Dress(ParticleSystem system, BlockView brick, float whiteLift)
        {
            if (system == null || brick == null)
            {
                return Color.white;
            }

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Mesh symbol = brick.Skin == null ? null : brick.Skin.SparkMesh;

            if (renderer != null)
            {
                if (symbol != null)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Mesh;
                    renderer.mesh = symbol;
                }
                else if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    ReportMissingSymbol(brick);
                }
            }

            return brick.Neon(whiteLift) ?? Color.white;
        }

        /// <summary>
        /// Sets the region a burst comes out of: <paramref name="across"/> wide and deep, and
        /// <paramref name="tall"/> high. A flat box is a brick's base; a tall one is a stretch of a
        /// column.
        /// <para>
        /// Written by the view rather than left on the prefab, and that is a deliberate move of the
        /// line D-053 drew. The tool creating a look once and the designer owning it afterwards is
        /// right when the designer can reach it — but every number asked for across three rounds of
        /// this effect lived on a prefab, and each one cost a rebuild and a round-trip to change. A
        /// number nobody can reach without a rebuild is not a number a designer owns (D-082).
        /// </para>
        /// </summary>
        private static void Shape(ParticleSystem system, float across, float tall)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(across, tall, across);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
        }

        /// <summary>
        /// Pushes a burst's particles off their own path as they travel, which is what turns a rising
        /// column of sparks into a drifting one. Its waviness is the plume's own frequency, because
        /// they are the same drifting light and one authored number for it is enough (D-074).
        /// </summary>
        private static void Wander(ParticleSystem system, float strength)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.NoiseModule noise = system.noise;

            if (strength <= 0f)
            {
                noise.enabled = false;
                return;
            }

            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = WanderFrequency;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        /// <summary>
        /// Writes the whole shape of a placement spark's climb as one velocity curve: down a little,
        /// then up, then easing to a stop as it fades.
        /// <para>
        /// It is a curve because the motion has three phases and an acceleration has one. What it does
        /// **not** cost is the guarantee: the curve's area is measured and divided out, so the spark
        /// still travels exactly <paramref name="height"/> however the dip is tuned (D-078, D-081,
        /// D-082). Deepen the dip and the shape changes, not the destination.
        /// </para>
        /// <para>
        /// All three axes are written, in the same curve mode. Unity refuses a velocity module whose
        /// axes disagree, and writing only y is the exact fault that filled the console for two rounds
        /// (D-078) — met here at the source rather than repaired afterwards.
        /// </para>
        /// </summary>
        private static void Climb(ParticleSystem system, float height, float seconds, float dip, float scatter)
        {
            if (system == null || seconds <= 0.0001f)
            {
                return;
            }

            AnimationCurve shape = ClimbShape(dip);
            float area = Area(shape);
            float scale = area > 0.0001f ? height / (seconds * area) : height / seconds;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;

            // A band rather than a line, on every axis: Unity picks each particle's own place between
            // the two curves, so no two sparks travel together. The band is centred on the climb, which
            // is what keeps the authored height as the average of the burst (D-083).
            velocity.x = Band(AnimationCurve.Constant(0f, 1f, 0f), 1f, scatter);
            velocity.y = Band(shape, scale, scatter);
            velocity.z = Band(AnimationCurve.Constant(0f, 1f, 0f), 1f, scatter);
        }

        /// <summary>
        /// One axis as a pair of curves <paramref name="spread"/> either side of
        /// <c>scale × middle</c>, in absolute units — so a particle's own draw between them is a real
        /// difference in where it ends up rather than a difference in how it is scaled.
        /// <para>
        /// Always a pair, even at zero spread, because Unity refuses a velocity module whose three axes
        /// are in different curve modes — the console fault of D-078, avoided here by never letting the
        /// three disagree in the first place.
        /// </para>
        /// </summary>
        private static ParticleSystem.MinMaxCurve Band(AnimationCurve middle, float scale, float spread)
        {
            return new ParticleSystem.MinMaxCurve(1f, Shift(middle, scale, -spread), Shift(middle, scale, spread));
        }

        /// <summary>A copy of a curve with every value scaled and then offset. The keys' shape survives.</summary>
        private static AnimationCurve Shift(AnimationCurve curve, float scale, float offset)
        {
            Keyframe[] keys = curve.keys;

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value = keys[i].value * scale + offset;
                keys[i].inTangent *= scale;
                keys[i].outTangent *= scale;
            }

            return new AnimationCurve(keys);
        }

        /// <summary>
        /// Sends a burst's particles apart sideways as they travel, each at its own rate, without
        /// touching how they rise. It is what turns a clump into sparks: the emission area gives them
        /// different starting points, and this gives them different destinations (D-083).
        /// </summary>
        private static void Drift(ParticleSystem system, float scatter)
        {
            if (system == null)
            {
                return;
            }

            AnimationCurve flat = AnimationCurve.Constant(0f, 1f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = Band(flat, 1f, scatter);
            velocity.y = Band(flat, 1f, 0f);
            velocity.z = Band(flat, 1f, scatter);
        }

        /// <summary>The last climb shape built, kept so a burst per move does not build a curve per move.</summary>
        private static AnimationCurve climbShape;

        private static float climbDip = float.NaN;

        /// <summary>
        /// The climb's profile, in units of its own peak speed: it leaves going *down* at
        /// <paramref name="dip"/>, crosses zero early, peaks past the middle and arrives at nothing.
        /// The peak sits after the crossing rather than on it, so the turn reads as a push rather than
        /// a bounce.
        /// </summary>
        private static AnimationCurve ClimbShape(float dip)
        {
            if (climbShape != null && Mathf.Approximately(climbDip, dip))
            {
                return climbShape;
            }

            climbDip = dip;
            climbShape = new AnimationCurve(
                new Keyframe(0f, -dip),
                new Keyframe(0.22f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f));

            for (int i = 0; i < climbShape.length; i++)
            {
                climbShape.SmoothTangents(i, 0f);
            }

            return climbShape;
        }

        /// <summary>
        /// The area under a curve over 0..1, by trapezoid. Sampled rather than solved because the curve
        /// is an <see cref="AnimationCurve"/> with smoothed tangents and its integral has no closed
        /// form worth writing — and this runs once per tuning change, not once per spark.
        /// </summary>
        private static float Area(AnimationCurve curve)
        {
            const int Steps = 64;
            float total = 0f;
            float previous = curve.Evaluate(0f);

            for (int i = 1; i <= Steps; i++)
            {
                float current = curve.Evaluate(i / (float)Steps);
                total += (previous + current) * 0.5f / Steps;
                previous = current;
            }

            return total;
        }

        /// <summary>
        /// Sets the downward pull on a burst, in cells per second squared, through the only dial Unity
        /// offers for it: a multiplier on the project's own gravity.
        /// <para>
        /// It is a *system* property and not a per-particle one, so — like the mesh the renderer draws
        /// — a burst still alive from a moment ago is re-tuned by the next one. Both bursts here are
        /// one colour and one shape at a time, so it costs nothing in practice; it is written down
        /// because it is the kind of limit that is otherwise found rather than known (D-080, D-081).
        /// </para>
        /// <para>
        /// With gravity switched off in the project there is nothing to scale, so the sparks keep the
        /// flat rise they used to have rather than dividing by zero to get one.
        /// </para>
        /// </summary>
        private static void Slow(ParticleSystem system, float pull)
        {
            if (system == null)
            {
                return;
            }

            float world = Mathf.Abs(Physics.gravity.y);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = world > 0.0001f ? pull / world : 0f;
        }

        /// <summary>Said once, and it names the command that fixes it rather than the state it found.</summary>
        private static void ReportMissingSymbol(BlockView brick)
        {
            if (symbolReported)
            {
                return;
            }

            symbolReported = true;
            Debug.LogWarning(
                "[ColumnView] " + (brick.Skin == null ? "a brick with no skin" : brick.Skin.name) +
                " has no spark mesh, so the sparks fall back to soft puffs. Run Tools > Colorful Sort > Create Block Skins.",
                brick.Skin);
        }

        /// <summary>Set once the missing-symbol warning has been said, so it is one line and not one per brick.</summary>
        private static bool symbolReported;

        /// <summary>
        /// Emits into a system, having first made sure it is running.
        /// <para>
        /// This is the line whose absence meant no spark on this board had ever been seen. Both bursts
        /// are dressed <c>playOnAwake = false</c> and emit nothing on their own — the view says when,
        /// how many and from where — but a system that has never been played is *stopped*, and a
        /// stopped system does not simulate what is emitted into it. `BlockView.StartPlume` had this
        /// right from the beginning (<c>Clear(); Play();</c>) and the column's path was written without
        /// it, which is why V-8c's celebration and V-8d's landing both drew nothing and neither was
        /// noticed: an effect that silently does not run looks exactly like an effect nobody has got to
        /// yet (D-078).
        /// </para>
        /// <para>
        /// It is <c>Play</c> and not <c>Clear(); Play()</c>, unlike the plume: two bursts a moment apart
        /// are two bursts, and clearing would take the first one off the screen to start the second.
        /// </para>
        /// </summary>
        private static void Fire(ParticleSystem system, ParticleSystem.EmitParams emit, int count)
        {
            if (system == null)
            {
                Debug.LogWarning("[ColumnView] A burst was asked for and its particle system is not assigned. Run Tools > Colorful Sort > Build BoardView Prefabs.");
                return;
            }

            if (!system.isPlaying)
            {
                system.Play();
            }

            system.Emit(emit, count);
            Report(system, emit.position, count);
        }

        /// <summary>Set once the first burst has spoken, so this is one line per play session, not per move.</summary>
        private static bool burstReported;

        /// <summary>
        /// One line, the first time a burst fires, naming the two facts that separate the two remaining
        /// explanations for a burst nobody can see.
        /// <para>
        /// This is D-065's method rather than a fourth guess: there, three plausible causes produced one
        /// symptom and a single logged fact ended it faster than another round of reasoning would have.
        /// If the count printed here is <b>0</b>, the particles were never created and the fault is in
        /// the emit path. If it is the number asked for, they exist and something is not drawing them —
        /// the position printed alongside says whether they were even put where they were meant to go.
        /// </para>
        /// <para>
        /// It comes out once and then stays quiet, because a console that repeats itself every move is
        /// one nobody reads. Delete it when the bursts have been seen.
        /// </para>
        /// </summary>
        private static void Report(ParticleSystem system, Vector3 at, int asked)
        {
            if (burstReported)
            {
                return;
            }

            burstReported = true;
            Debug.Log(
                "[ColumnView] " + system.name + " fired " + asked + " at " + at.ToString("F2") +
                "; the system now holds " + system.particleCount + " particle(s), drawing with " +
                (system.GetComponent<ParticleSystemRenderer>()?.sharedMaterial?.name ?? "no material") +
                ". 0 means they were never made; the number asked for means they exist and are not being drawn.",
                system);
        }

        /// <summary>
        /// Brightens or restores the engraved symbol on every brick — the body is left alone, so what
        /// lights up is the shape in the middle and not the whole brick.
        /// </summary>
        private void SetSymbolGlow(float brightness)
        {
            for (int index = 0; index < bricks.Count; index++)
            {
                if (bricks[index] != null)
                {
                    bricks[index].SetSymbolGlow(brightness);
                }
            }
        }

        private IEnumerator Settle(float targetAlpha, float targetShade, float duration)
        {
            float fromAlpha = settledShadow == null ? 0f : settledShadow.color.a;
            float fromShade = currentShade;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // Smoothstep, the same easing BoardMoveAnimator uses, so a settling column and a
                // flying brick do not read as two different games.
                float linear = Mathf.Clamp01(elapsed / duration);
                float eased = linear * linear * (3f - 2f * linear);

                ApplySettle(Mathf.Lerp(fromAlpha, targetAlpha, eased), Mathf.Lerp(fromShade, targetShade, eased));
                yield return null;
            }

            ApplySettle(targetAlpha, targetShade);
            settleFade = null;
        }

        private void ApplySettle(float alpha, float shade)
        {
            currentShade = shade;

            if (settledShadow != null)
            {
                Color colour = settledShadow.color;
                settledShadow.color = new Color(colour.r, colour.g, colour.b, alpha);
            }

            for (int index = 0; index < bricks.Count; index++)
            {
                if (bricks[index] != null)
                {
                    bricks[index].SetShade(shade);
                }
            }
        }

        /// <summary>
        /// Draws the boundary between each pair of cells — one line for a two-cell column, seven
        /// for an eight-cell one. The tray art is flat and stretches, so these are what make a
        /// column's capacity readable, which is the job D-007 used to give tiled art.
        /// <para>
        /// They hang off the slot renderer rather than a root of their own, so they share its
        /// plane and its sorting layer by construction: one order in front of the tray, and behind
        /// the bricks, which are opaque geometry nearer the camera.
        /// </para>
        /// </summary>
        private void BuildDividers()
        {
            ClearDividers();

            if (cellDivider == null)
            {
                return;
            }

            for (int boundary = 1; boundary < Capacity; boundary++)
            {
                var piece = new GameObject("Divider_" + boundary.ToString("00"));
                piece.transform.SetParent(slot.transform, false);
                piece.transform.localPosition = new Vector3(0f, Metrics.Skirt + boundary, 0f);

                var renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = cellDivider;
                renderer.sortingLayerID = slot.sortingLayerID;
                renderer.sortingOrder = slot.sortingOrder + 1;
            }
        }

        private void ClearDividers()
        {
            for (int child = slot.transform.childCount - 1; child >= 0; child--)
            {
                GameObject piece = slot.transform.GetChild(child).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(piece);
                }
                else
                {
                    DestroyImmediate(piece);
                }
            }
        }

        private void AddCoverPiece(Sprite sprite, float localY, string objectName)
        {
            var piece = new GameObject(objectName);
            piece.transform.SetParent(coverRoot, false);
            piece.transform.localPosition = new Vector3(0f, localY, 0f);

            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = slot.sortingLayerID;
            renderer.sortingOrder = coverSortingOrder;
        }

        private void ClearCover()
        {
            for (int child = coverRoot.childCount - 1; child >= 0; child--)
            {
                GameObject piece = coverRoot.GetChild(child).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(piece);
                }
                else
                {
                    DestroyImmediate(piece);
                }
            }
        }
    }
}
