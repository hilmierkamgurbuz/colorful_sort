namespace ColorfulSort.Board
{
    /// <summary>
    /// The four column kinds the reference shows (reference §2). This is a closed
    /// enum on purpose: the reference game is finished and shows exactly three
    /// modifiers, so <c>ColumnModifiers</c> is a set of pure functions switching
    /// on this value rather than an <c>IColumnModifier</c> hierarchy. A real
    /// fourth kind is what would justify the interface, not the anticipation of
    /// one (abstraction-level.md).
    /// </summary>
    public enum ColumnKind
    {
        /// <summary>Plain slot, no modifier.</summary>
        Normal = 0,

        /// <summary>
        /// Encased in ice: starts empty and locked, and thaws once
        /// <c>ThawAfterCompletions</c> colours have been completed this attempt.
        /// </summary>
        Ice = 1,

        /// <summary>
        /// Hidden under a cover: starts locked with every cell hidden. The cover's
        /// symbol is a key — completing <c>CoverKeyColour</c> opens every cover
        /// carrying it at once (D-009).
        /// </summary>
        Covered = 2,

        /// <summary>
        /// Playable, but the cells below the top are hidden <c>?</c> bricks; each
        /// reveals itself when it becomes the column's top block.
        /// </summary>
        Mystery = 3,
    }
}
