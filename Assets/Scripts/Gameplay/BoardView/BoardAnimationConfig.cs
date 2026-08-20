using UnityEngine;
using UnityEngine.Serialization;

namespace ColorfulSort.View
{
    /// <summary>
    /// How a run of bricks moves. Every number here is feel, and feel is iterated by playing the
    /// game rather than by reading code — so it lives in an asset under <c>Data/Config/</c>
    /// (`.claude/rules/gameplay.md`) and a designer can retune the whole move without a compile.
    /// <para>
    /// Distances are in cells, because one cell is one unit; durations are in seconds.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "BoardAnimationConfig", menuName = "Colorful Sort/Board Animation Config")]
    public sealed class BoardAnimationConfig : ScriptableObject
    {
        // No field defaults on purpose: a number a designer tunes belongs in the asset, not in a
        // serialized default (`.claude/rules/data.md`). A fresh asset is therefore *invalid*, and
        // `Validate` says so loudly instead of shipping someone's guess as the game's feel.

        [Tooltip("How far a selected run rises above its column, in cells.")]
        [SerializeField]
        private float liftHeight;

        [Tooltip("Seconds for the run to rise when a column is tapped.")]
        [SerializeField]
        private float liftDuration;

        [Tooltip("Seconds for the run to travel to the target column.")]
        [SerializeField]
        private float travelDuration;

        [Tooltip("Seconds for a cancelled run to drop back into its column.")]
        [SerializeField]
        private float dropDuration;

        [Header("How a brick enters its slot")]

        // Renamed rather than replaced: this was arcHeight, the height a run arced over the board on
        // its way across. Bricks no longer cross the board in an arc — they cross *above the target
        // column* and drop in — so the number kept its job description and lost its old name, and
        // FormerlySerializedAs is what carries the tuned value across (D-072).
        [Tooltip("How far above the target column's top a brick crosses before dropping in, in cells.")]
        [FormerlySerializedAs("arcHeight")]
        [SerializeField]
        private float entryClearance;

        [Tooltip("Seconds between one brick leaving and the next. 0 sends a whole run at once.")]
        [SerializeField]
        private float entryStagger;

        [Tooltip("Seconds one puff of a flying brick's plume lives. Longer than the flight and it lingers over the board.")]
        [SerializeField]
        private float trailTime;

        [Tooltip("How big one puff is, in cells. 0 leaves a flight with no plume.")]
        [SerializeField]
        private float trailWidth;

        [Tooltip("Puffs per cell of flight. Too few reads as beads, too many as a solid bar.")]
        [SerializeField]
        private float trailDensity;

        [Header("How a lifted run rocks")]
        [Tooltip("Degrees the run tips as it rises — positive leans its top-left corner left. 0 rises dead level.")]
        [SerializeField]
        private float liftTiltDegrees;

        [Tooltip("Half-rocks during the rise. A whole number, or the run would land tipped.")]
        [SerializeField]
        private float liftTiltCycles;

        [Tooltip("Degrees a waiting run rocks, one corner then the other. Smaller than the lift's.")]
        [SerializeField]
        private float idleTiltDegrees;

        [Tooltip("Seconds for one full rock: top-left out, then top-right out, and back.")]
        [SerializeField]
        private float idleTiltPeriod;

        [Tooltip("How much wider than a cell the lifted run's glow is — the rim of light beside the bricks.")]
        [SerializeField]
        private float glowPadding;

        [Tooltip("How far the glow reaches below the run, in cells.")]
        [SerializeField]
        private float glowBelow;

        [Tooltip("How far the glow reaches above the run, in cells.")]
        [SerializeField]
        private float glowAbove;

        [Tooltip("Sideways nudge, in cells: the drawing is a little off-centre and this recentres it.")]
        [SerializeField]
        private float glowNudgeX;

        [Tooltip("A touch of white on top of the neon hue, 0-1. Small: mixing towards white desaturates, which is the opposite of neon.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float glowLift;

        // The two bursts are grouped so the three things anyone comes here to change — HOW MANY, HOW
        // BIG AN AREA, HOW FAST — sit together at the top of each, in that order. Everything else in
        // a group is a detail of the same effect. Finding a dial should not need the person who
        // wrote it.

        [Header("A finished column — the hop and the glow")]
        [Tooltip("How high a brick hops when its colour is finished, in cells. 0 skips the hop.")]
        [SerializeField]
        private float celebrationHop;

        [Tooltip("Seconds for one brick's hop, up and back down.")]
        [SerializeField]
        private float celebrationHopDuration;

        [Tooltip("Seconds between one brick hopping and the next. 0 sends the whole column up together.")]
        [SerializeField]
        private float celebrationHopStagger;

        [Tooltip("What the engraved symbol's colour is multiplied by while the brick is up. Over 1, so bloom catches it.")]
        [SerializeField]
        private float celebrationSymbolGlow;

        [Tooltip("Seconds the lit symbols take to come back down to their own colour. 0 snaps them off.")]
        [SerializeField]
        private float celebrationGlowFade;

        [Tooltip("How long that fade has already been running when the burst comes out, so the two end together. 0 starts them on the same frame.")]
        [SerializeField]
        private float celebrationGlowHold;

        [Header("A finished column — its burst: how many, how big an area, how fast")]
        [Tooltip("HOW MANY sparks the finish throws. 0 leaves a silent finish.")]
        [SerializeField]
        private int celebrationSparks;

        [Tooltip("HOW BIG AN AREA they come out of, in cells across. It is as tall as the middle two cells.")]
        [SerializeField]
        private float celebrationBurstSpread;

        [Tooltip("HOW FAST they leave, in cells per second. The fall back down is worked out from this and the lifetime.")]
        [SerializeField]
        private float celebrationBurstRise;

        [Tooltip("Seconds one of those sparks lives — long enough to go up and come back down.")]
        [SerializeField]
        private float celebrationBurstSeconds;

        [Tooltip("How big one of those sparks is, in cells. 1 would draw a symbol the size of a brick's face.")]
        [SerializeField]
        private float celebrationSparkSize;

        [Tooltip("How far apart two sparks of the same burst drift, in cells per second. 0 sends them all the same way.")]
        [SerializeField]
        private float celebrationBurstScatter;

        [Header("A brick landing on its own — its burst: how many, how big an area, how fast")]
        [Tooltip("HOW MANY sparks rise from under each brick of a landing that finished nothing. 0 leaves a plain landing.")]
        [SerializeField]
        private int landingSparks;

        [Tooltip("HOW BIG AN AREA of the brick's underside they rise off, in cells. 0.8 is most of it; 0 is one point.")]
        [SerializeField]
        private float landingSparkSpread;

        [Tooltip("HOW FAR they climb, in cells — on average, since they scatter. Divided by the seconds below, this is their speed.")]
        [SerializeField]
        private float landingRiseHeight;

        [Tooltip("Seconds that climb takes. Raise it to slow them down without moving them less far.")]
        [SerializeField]
        private float landingRiseSeconds;

        [Tooltip("How big one of those sparks is, in cells. 1 would draw a symbol the size of a brick's face.")]
        [SerializeField]
        private float landingSparkSize;

        [Tooltip("How far apart two sparks of the same burst drift, in cells per second. 0 sends them all the same way.")]
        [SerializeField]
        private float landingSparkScatter;

        [Tooltip("How far a spark wanders off its climb. 0 rises dead straight, which reads as mechanical.")]
        [SerializeField]
        private float landingSparkWander;

        [Tooltip("How hard a spark drops before it climbs, as a share of its climbing speed. 0 starts it going up.")]
        [Range(0f, 0.9f)]
        [SerializeField]
        private float landingSparkDip;

        [Tooltip("How far under the brick's middle the burst starts, in cells — they rise from under it, not out of it.")]
        [SerializeField]
        private float landingSparkDrop;

        [Header("A finished column settling into its slot")]
        [Tooltip("Seconds the shadow takes to fade in and the bricks to darken.")]
        [SerializeField]
        private float settleDuration;

        [Tooltip("What a settled brick's colour is multiplied by. 0.75 darkens; 1 would change nothing.")]
        [SerializeField]
        private float settleShade;

        [Tooltip("How opaque the shadow over a settled column ends up.")]
        [SerializeField]
        private float settleShadowAlpha;

        [Tooltip("Seconds a revealed ? brick takes to change. The whole change, both halves: the ? dissolves into the brick's colour and the real symbol emerges from it.")]
        [SerializeField]
        private float revealFadeDuration;

        public float LiftHeight => liftHeight;

        public float LiftDuration => liftDuration;

        public float TravelDuration => travelDuration;

        public float DropDuration => dropDuration;

        /// <summary>
        /// How far above the target column's own top a brick crosses before it drops in. Measured
        /// from the column rather than from the board, so a taller column pushes the crossing higher
        /// without a second number to keep in step (D-072).
        /// </summary>
        public float EntryClearance => entryClearance;

        /// <summary>Seconds between two bricks of the same run leaving; 0 sends them together.</summary>
        public float EntryStagger => entryStagger;

        /// <summary>
        /// How many seconds one puff of the plume lives. It belongs beside the travel duration rather
        /// than in the prefab because the two have to agree: a plume that outlives the flight hangs
        /// over the board after the brick has been placed (D-073).
        /// </summary>
        public float TrailTime => trailTime;

        /// <summary>How big one puff is, in cells; 0 leaves a flight with no plume.</summary>
        public float TrailWidth => trailWidth;

        /// <summary>
        /// How many puffs go into one cell of travel. Emission is by distance, so this and not a rate
        /// per second is what makes the plume the same density however fast the brick is moving
        /// (D-074).
        /// </summary>
        public float TrailDensity => trailDensity;

        /// <summary>Degrees a rising run tips; positive leans its top-left corner left.</summary>
        public float LiftTiltDegrees => liftTiltDegrees;

        /// <summary>Half-rocks during the rise; whole numbers only, so the run lands level.</summary>
        public float LiftTiltCycles => liftTiltCycles;

        /// <summary>Degrees a waiting run rocks, one diagonal then the other.</summary>
        public float IdleTiltDegrees => idleTiltDegrees;

        /// <summary>Seconds for one full rock, out one way and back the other.</summary>
        public float IdleTiltPeriod => idleTiltPeriod;

        /// <summary>How much wider than a cell the glow is, in cells.</summary>
        public float GlowPadding => glowPadding;

        /// <summary>How far the glow reaches below the run, in cells.</summary>
        public float GlowBelow => glowBelow;

        /// <summary>How far the glow reaches above the run, in cells.</summary>
        public float GlowAbove => glowAbove;

        /// <summary>Sideways nudge in cells, recentring a drawing that is not quite centred.</summary>
        public float GlowNudgeX => glowNudgeX;

        /// <summary>
        /// How much white sits on top of the neon hue. Kept small on purpose: mixing towards white
        /// desaturates, and a desaturated glow is a pale patch rather than neon (D-063).
        /// </summary>
        public float GlowLift => glowLift;

        /// <summary>How high a brick hops when its colour is finished, in cells; 0 skips the hop.</summary>
        public float CelebrationHop => celebrationHop;

        /// <summary>Seconds for one brick's hop, up and back down.</summary>
        public float CelebrationHopDuration => celebrationHopDuration;

        /// <summary>
        /// Seconds between one brick hopping and the next, so a finished column reads as four bricks
        /// pleased in turn rather than one block of them (D-076). Zero sends them up together.
        /// </summary>
        public float CelebrationHopStagger => celebrationHopStagger;

        /// <summary>
        /// How long the fade has already been running when the burst comes out — the head start the
        /// glow gets, so the light and the sparks die together rather than one following the other.
        /// <para>
        /// It used to mean the opposite end of the same pause: how long the symbols stayed lit
        /// *before* the fade began. The order changed with it (D-083) — the fade now starts as the
        /// bricks come back down and the burst fires inside it — so the number kept its place and its
        /// name and changed what it measures. 0 is legal: the burst and the fade start together.
        /// </para>
        /// </summary>
        public float CelebrationGlowHold => celebrationGlowHold;

        /// <summary>
        /// Seconds the lit symbols take to come back down to their own colour. The burst at the middle
        /// of the slot waits for this to finish, because "when the glow ends" is the moment the player
        /// asked for and a fade moves that moment (D-079). Zero snaps them off, which is what this did
        /// before it was a number.
        /// </summary>
        public float CelebrationGlowFade => celebrationGlowFade;

        /// <summary>
        /// What the engraved symbol's colour is multiplied by while the brick is up. Over 1 on
        /// purpose: the overshoot is what bloom turns into light (D-066, D-075).
        /// </summary>
        public float CelebrationSymbolGlow => celebrationSymbolGlow;

        /// <summary>
        /// Sparks thrown at the middle of the slot when the glow ends. One burst for the column, not
        /// one per brick: what finished is the colour, and the column is what says so (D-078).
        /// </summary>
        public int CelebrationSparks => celebrationSparks;

        /// <summary>
        /// The upward push that burst leaves with, in cells per second. It is a speed and not a height
        /// because what brings these sparks back down is the prefab's own gravity — the apex is the
        /// two of them together, and only one of them belongs in this asset (D-078).
        /// </summary>
        public float CelebrationBurstRise => celebrationBurstRise;

        /// <summary>Seconds one of those sparks lives; it has to outlast the way up and the way down.</summary>
        public float CelebrationBurstSeconds => celebrationBurstSeconds;

        /// <summary>
        /// How big one finish spark is, in cells — a cell is a brick, so this is a fraction of a
        /// brick's face. It sits here rather than on the prefab because it is the number the look is
        /// judged on, and judging it must not need a rebuild (D-081).
        /// </summary>
        public float CelebrationSparkSize => celebrationSparkSize;

        /// <summary>
        /// How wide the area the finish burst comes out of is, in cells. Its height is not authored:
        /// it is the middle two cells, which is a fact about the column rather than a taste (D-082).
        /// </summary>
        public float CelebrationBurstSpread => celebrationBurstSpread;

        /// <summary>
        /// How far apart two sparks of the same finish burst drift, in cells per second. Zero sends
        /// every one of them along the same arc, which is what made a burst read as a single clump
        /// rather than as sparks (D-083).
        /// </summary>
        public float CelebrationBurstScatter => celebrationBurstScatter;

        /// <summary>
        /// Sparks that rise from under each brick of a landing that finished no colour. A landing that
        /// *does* finish one is the celebration's, and it throws its own burst when the glow ends — so
        /// these two counts are separate numbers on purpose (D-077).
        /// </summary>
        public int LandingSparks => landingSparks;

        /// <summary>
        /// How far a landing spark climbs before it is gone, in cells — **on average**.
        /// <para>
        /// It was exact for three tasks: D-078 made it so with no gravity at one flat speed, D-081 kept
        /// it through a deceleration and D-082 kept it again through a velocity curve whose area is
        /// divided out. <see cref="LandingSparkScatter"/> is what ends it, and deliberately — sparks
        /// that all travel exactly the same distance are sparks in formation, which is the thing that
        /// was wrong with them. Above zero, this is the middle of the spread rather than the whole
        /// story (D-083).
        /// </para>
        /// </summary>
        public float LandingRiseHeight => landingRiseHeight;

        /// <summary>
        /// Seconds that climb takes. The speed handed to the burst is the height divided by this, so
        /// the distance and the duration cannot drift apart the way two authored numbers would.
        /// </summary>
        public float LandingRiseSeconds => landingRiseSeconds;

        /// <summary>
        /// How far under the brick's middle the burst starts, in cells. It is what makes the sparks
        /// rise from *under* a brick that has just been set down rather than out of the middle of it.
        /// </summary>
        public float LandingSparkDrop => landingSparkDrop;

        /// <summary>How big one placement spark is, in cells; a cell is a brick.</summary>
        public float LandingSparkSize => landingSparkSize;

        /// <summary>
        /// How much of the brick's base the sparks rise off, in cells. A cell is a brick, so 0.8 is
        /// most of its underside and 0 is the single point they used to come from.
        /// </summary>
        public float LandingSparkSpread => landingSparkSpread;

        /// <summary>
        /// How far a spark wanders off a straight climb. Its waviness is the plume's own noise
        /// frequency, because they are the same drifting light and one of those numbers is enough.
        /// </summary>
        public float LandingSparkWander => landingSparkWander;

        /// <summary>
        /// How far apart two sparks of the same placement burst drift, in cells per second.
        /// <para>
        /// This is the number that ends the exactness of <see cref="LandingRiseHeight"/>, and the
        /// trade is the point: sparks that all travel exactly the same distance are sparks moving in
        /// formation. Above zero, the climb is a *pair* of curves Unity picks between per particle, so
        /// the authored height becomes the average and this is the spread either side (D-083).
        /// </para>
        /// </summary>
        public float LandingSparkScatter => landingSparkScatter;

        /// <summary>
        /// How hard a spark drops before it climbs, as a share of its climbing speed — a brick landing
        /// pushes what is under it down before it comes back up.
        /// <para>
        /// It changes the *shape* of the climb and not its length: the motion is one velocity curve
        /// whose area is measured and divided out, so the spark still ends exactly
        /// <see cref="LandingRiseHeight"/> up. A deeper dip does not make them finish lower (D-082).
        /// </para>
        /// </summary>
        public float LandingSparkDip => landingSparkDip;

        /// <summary>How long a finished column takes to sink into its slot.</summary>
        public float SettleDuration => settleDuration;

        /// <summary>What a settled column's bricks are multiplied by — under 1, so they darken.</summary>
        public float SettleShade => settleShade;

        /// <summary>How opaque the shadow over a settled column becomes.</summary>
        public float SettleShadowAlpha => settleShadowAlpha;

        /// <summary>
        /// How long a revealed `?` brick takes to change — the whole change, not one half of it. The
        /// `?` dissolves into the brick's own colour as that colour travels to the real one, the skin
        /// changes where the symbol has no contrast left, and the real symbol emerges out of the body
        /// (D-101, replacing D-099's turn).
        /// <para>
        /// Zero is legal and means "no fade": the brick simply shows its colour, which is what the
        /// reveal did before any of this existed and what an asset written before this field carries.
        /// </para>
        /// </summary>
        public float RevealFadeDuration => revealFadeDuration;

        /// <summary>
        /// A zero duration would divide by zero in the tween and a negative one would run it
        /// backwards; heights are free to be zero, which simply means "no lift" or "no arc".
        /// </summary>
        public bool Validate(out string error)
        {
            if (liftDuration <= 0f || travelDuration <= 0f || dropDuration <= 0f)
            {
                error = "Every duration is a positive number of seconds; this asset has " +
                        liftDuration + " / " + travelDuration + " / " + dropDuration + ".";
                return false;
            }

            if (liftHeight < 0f || entryClearance < 0f)
            {
                error = "A lift height or an entry clearance is never negative.";
                return false;
            }

            if (entryStagger < 0f)
            {
                error = "The entry stagger is " + entryStagger + "; it is a wait between bricks, never negative.";
                return false;
            }

            // A tilt of 90° would lay the run on its side; anything near it stops reading as a rock,
            // so the range is refused rather than clamped.
            if (liftTiltDegrees < 0f || liftTiltDegrees >= 90f || idleTiltDegrees < 0f || idleTiltDegrees >= 90f)
            {
                error = "A tilt is between 0 and 90 degrees; this asset has " + liftTiltDegrees + " and " + idleTiltDegrees + ".";
                return false;
            }

            // The tilt is a sine over the rise, and only a whole number of half-rocks comes back to
            // level at the top. Half a rock more and every brick lands tipped.
            if (liftTiltCycles < 0f || !Mathf.Approximately(liftTiltCycles, Mathf.Round(liftTiltCycles)))
            {
                error = "The lift tilt cycles are " + liftTiltCycles + "; a whole number is what lands the run level.";
                return false;
            }

            if (idleTiltDegrees > 0f && idleTiltPeriod <= 0f)
            {
                error = "The idle tilt period is " + idleTiltPeriod + "; a rock needs a positive number of seconds.";
                return false;
            }

            // Zero is a legal plume — it is "no plume", which is a look — but a negative lifetime,
            // size or density is one Unity cannot emit.
            if (trailTime < 0f || trailWidth < 0f || trailDensity < 0f)
            {
                error = "The plume is " + trailTime + "s, " + trailWidth + " across and " + trailDensity +
                        " per cell; none of the three is ever negative.";
                return false;
            }

            if (glowPadding < 0f || glowBelow < 0f || glowAbove < 0f)
            {
                error = "A glow reach is never negative; zero means a glow exactly the run's size.";
                return false;
            }

            if (glowLift < 0f || glowLift > 1f)
            {
                error = "The glow lift is " + glowLift + "; it mixes towards white, so it sits between 0 and 1.";
                return false;
            }

            // The hop may be nothing — a column that only settles is a look — but a symbol glow under
            // 1 would *darken* the shape it is meant to light, and negative sparks are not a number.
            if (celebrationHop < 0f || celebrationHopDuration < 0f || celebrationSparks < 0
                || celebrationHopStagger < 0f || celebrationGlowHold < 0f || celebrationGlowFade < 0f)
            {
                error = "The celebration is " + celebrationHop + " high over " + celebrationHopDuration +
                        "s, " + celebrationHopStagger + "s apart, held " + celebrationGlowHold +
                        "s, fading over " + celebrationGlowFade + "s, with " + celebrationSparks +
                        " sparks; none of those is ever negative.";
                return false;
            }

            if (celebrationSymbolGlow < 1f)
            {
                error = "The celebration symbol glow is " + celebrationSymbolGlow +
                        "; it multiplies the symbol's colour, so under 1 would darken what is supposed to light up.";
                return false;
            }

            // Zero is a legal burst — it is the effect turned off, which is a look — but a negative
            // count is not a number of particles, a negative climb would drive them into the board,
            // and a negative drop would start them above the brick instead of under it.
            if (landingSparks < 0 || landingRiseHeight < 0f || landingRiseSeconds < 0f
                || landingSparkDrop < 0f || landingSparkSize < 0f
                || landingSparkSpread < 0f || landingSparkWander < 0f || landingSparkScatter < 0f)
            {
                error = "A landing throws " + landingSparks + " sparks " + landingRiseHeight + " cells up over " +
                        landingRiseSeconds + "s from " + landingSparkDrop +
                        " under the brick; none of those is ever negative.";
                return false;
            }

            // Zero seconds is only a fault once there is something to live that long: the speed handed
            // to the burst is height ÷ seconds, so a count above zero with no duration is a division by
            // zero, and every other zero here is simply "no burst".
            if (landingSparks > 0 && landingRiseSeconds <= 0f)
            {
                error = "A landing throws " + landingSparks + " sparks over " + landingRiseSeconds +
                        "s; their speed is the climb divided by that, so it is a positive number of seconds.";
                return false;
            }

            if (celebrationBurstRise < 0f || celebrationBurstSeconds < 0f
                || celebrationSparkSize < 0f || celebrationBurstSpread < 0f || celebrationBurstScatter < 0f)
            {
                error = "The finish burst rises at " + celebrationBurstRise + " per second for " +
                        celebrationBurstSeconds + "s at " + celebrationSparkSize +
                        " cells across, out of " + celebrationBurstSpread +
                        " cells; none of those is ever negative.";
                return false;
            }

            // Same rule as the landing's, for the same reason at the other end: a spark with no
            // lifetime is emitted and gone in the frame it was made, which reads as no burst at all.
            if (celebrationSparks > 0 && celebrationBurstSeconds <= 0f)
            {
                error = "The finish throws " + celebrationSparks + " sparks that live " + celebrationBurstSeconds +
                        "s; a spark with no lifetime is one nobody sees.";
                return false;
            }

            // A dip at or past 1 drops as fast as it climbs, and the curve's area — which is what the
            // authored height is divided by — collapses towards nothing and then flips sign, so the
            // sparks would sink instead of rise (D-082).
            if (landingSparkDip < 0f || landingSparkDip >= 1f)
            {
                error = "The landing dip is " + landingSparkDip +
                        "; it is a share of the climb, so it sits at or above 0 and below 1.";
                return false;
            }

            if (settleDuration <= 0f)
            {
                error = "The settle duration is " + settleDuration + "; it is a positive number of seconds.";
                return false;
            }

            // At 0 a settled brick would be black and the shadow invisible; at 1 the shade would
            // change nothing. Both ends are a look nobody asked for, so neither is a legal value.
            if (settleShade <= 0f || settleShade >= 1f)
            {
                error = "The settle shade is " + settleShade + "; it darkens, so it sits between 0 and 1.";
                return false;
            }

            if (settleShadowAlpha <= 0f || settleShadowAlpha > 1f)
            {
                error = "The settle shadow alpha is " + settleShadowAlpha + "; it sits above 0 and at most 1.";
                return false;
            }

            // `revealFadeDuration` is deliberately NOT refused at 0. This asset's rule is that zero is
            // refused only where it fails silently — a burst with no lifetime, a speed divided by no
            // seconds — and zero here is neither: it is "no fade, change the brick", which is both a
            // legal look and exactly what an asset written before the field existed carries (D-077's
            // reasoning, applied through D-099 to D-101).

            error = null;
            return true;
        }

        private void OnValidate()
        {
            string error;
            if (!Validate(out error))
            {
                Debug.LogWarning("[" + name + "] " + error, this);
            }
        }
    }
}
