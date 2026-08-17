namespace ColorfulSort.Board
{
    /// <summary>
    /// The three column modifiers, as pure functions over <see cref="ColumnKind"/>.
    /// <para>
    /// There is deliberately no <c>IColumnModifier</c> hierarchy: the reference game
    /// is finished and shows exactly three modifiers, so the narrowest form that
    /// covers them is a switch over a closed enum (abstraction-level.md). The
    /// interface becomes justified the day a real fourth kind exists.
    /// </para>
    /// <para>
    /// Ice and Covered read the same trigger — "a colour was just completed" — and
    /// read it differently: ice counts completions, a cover matches its key colour
    /// (D-009). <c>BoardSession</c> is the single place that trigger fires.
    /// </para>
    /// </summary>
    public static class ColumnModifiers
    {
        /// <summary>Ice and Covered columns start locked; the player cannot move into or out of them.</summary>
        public static bool StartsLocked(ColumnKind kind)
        {
            return kind == ColumnKind.Ice || kind == ColumnKind.Covered;
        }

        /// <summary>A column takes part in the move rule only while it is unlocked.</summary>
        public static bool IsPlayable(BoardColumn column)
        {
            return column != null && !column.IsLocked;
        }

        /// <summary>
        /// Ice thaws once enough colours have been completed. The comparison is
        /// against the running total, so a move that completes two colours at once
        /// correctly thaws two ice columns.
        /// </summary>
        public static bool ShouldThaw(BoardColumn column, int completedColourCount)
        {
            return column != null
                   && column.Kind == ColumnKind.Ice
                   && column.IsLocked
                   && completedColourCount >= column.ThawAfterCompletions;
        }

        /// <summary>
        /// A cover opens when its key colour is completed — every cover carrying
        /// that key, at once (reference §2).
        /// </summary>
        public static bool ShouldUncover(BoardColumn column, BlockColourId completedColour)
        {
            return column != null
                   && column.Kind == ColumnKind.Covered
                   && column.IsLocked
                   && column.CoverKeyColour == completedColour;
        }

        /// <summary>A Mystery cell reveals itself the moment it becomes the column's top block.</summary>
        public static bool RevealsTopWhenExposed(ColumnKind kind)
        {
            return kind == ColumnKind.Mystery;
        }
    }
}
