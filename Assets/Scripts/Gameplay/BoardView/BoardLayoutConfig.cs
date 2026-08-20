using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// The few numbers about the board's look that a designer tunes. Everything else on
    /// screen is derived: a column's size comes from its sprite, the grid's shape comes
    /// from the level, and the camera's framing comes from the board it is looking at.
    /// <para>
    /// They live in an asset under <c>Data/Config/</c> rather than as field defaults,
    /// because a number a designer changes belongs in data (<c>.claude/rules/data.md</c>).
    /// Zero is a legal value for all of them — columns touching is a look, not a bug, and a
    /// board with no reserved bands is what a screen with no HUD wants — so a freshly created
    /// asset renders a valid, tight board rather than refusing to start.
    /// </para>
    /// <para>
    /// The two reserves are how the board learns that it does not own the whole screen. They
    /// are stated here rather than measured off the HUD's rects, because gameplay holds no
    /// reference to <c>UI</c> (<c>.claude/rules/gameplay.md</c>): reading a Canvas rect from
    /// the view would reverse the blueprint's one-way arrow, and the number it returned could
    /// not be unit-tested. The cost is that a HUD which grows needs these edited to match.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "BoardLayoutConfig", menuName = "Colorful Sort/Board Layout Config")]
    public sealed class BoardLayoutConfig : ScriptableObject
    {
        [Tooltip("Space between two columns side by side, in cells (1 cell = 1 unit).")]
        [SerializeField]
        private float columnGap;

        [Tooltip("Space between two rows of columns, in cells. Level 79 is two rows of six.")]
        [SerializeField]
        private float rowGap;

        [Tooltip("How much empty space the camera keeps around the board, in cells.")]
        [SerializeField]
        private float cameraPadding;

        [Tooltip("Share of the screen's height the HUD owns at the top (0-1). The board is framed below it.")]
        [Range(0f, 0.9f)]
        [SerializeField]
        private float topReserve;

        [Tooltip("Share of the screen's height the booster bar owns at the bottom (0-1).")]
        [Range(0f, 0.9f)]
        [SerializeField]
        private float bottomReserve;

        [Tooltip("How many columns an added-column booster may put in one row before it starts a new one.")]
        [Range(2, 16)]
        [SerializeField]
        private int maxColumnsPerRow;

        public float ColumnGap => columnGap;

        public float RowGap => rowGap;

        public float CameraPadding => cameraPadding;

        /// <summary>The top band the board keeps clear, as a share of viewport height.</summary>
        public float TopReserve => topReserve;

        /// <summary>The bottom band the board keeps clear, as a share of viewport height.</summary>
        public float BottomReserve => bottomReserve;

        /// <summary>
        /// How wide a row an added column may join. It decides when the booster starts a new row, and
        /// nothing else: a level authored wider keeps its own shape, because authored placements are
        /// never re-placed (D-046).
        /// </summary>
        public int MaxColumnsPerRow => maxColumnsPerRow;

        /// <summary>
        /// A negative gap would overlap columns in a way no board layout means; that is the
        /// only thing this can get wrong, so it is the only thing checked.
        /// </summary>
        public bool Validate(out string error)
        {
            if (columnGap < 0f)
            {
                error = "The column gap is " + columnGap + "; a gap is never negative.";
                return false;
            }

            if (rowGap < 0f)
            {
                error = "The row gap is " + rowGap + "; a gap is never negative.";
                return false;
            }

            if (cameraPadding < 0f)
            {
                error = "The camera padding is " + cameraPadding + "; padding is never negative.";
                return false;
            }

            if (topReserve < 0f || bottomReserve < 0f)
            {
                error = "The reserves are " + topReserve + " and " + bottomReserve + "; a reserved band is never negative.";
                return false;
            }

            // The two bands are what is left for the board. At 1 there is nothing left, and the
            // framing would divide by zero and hand the camera an infinite size.
            if (topReserve + bottomReserve >= 1f)
            {
                error = "The reserves add up to " + (topReserve + bottomReserve) +
                        " of the screen, which leaves the board none of it.";
                return false;
            }

            if (maxColumnsPerRow < 2)
            {
                error = "The row limit is " + maxColumnsPerRow + "; a row holds at least two columns before a new one starts.";
                return false;
            }

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
