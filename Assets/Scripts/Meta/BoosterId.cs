namespace ColorfulSort.Meta
{
    /// <summary>
    /// Which of the three boosters a charge, a price or a button is about.
    /// <para>
    /// It lives in <c>Meta</c> and not in <c>UI</c> because what a booster <em>costs</em> is
    /// progression policy: the save carries a charge count per booster, the config carries a
    /// price per booster, and a button only says which one it means. `UI` reads this enum;
    /// the arrow already runs that way.
    /// </para>
    /// <para>
    /// The values are numbered explicitly and are only ever <em>appended</em> to. Three
    /// `BoosterButton` instances carry these ints in the Game scene, and a value inserted in
    /// the middle would silently turn the Undo button into the Shuffle one — the same trap
    /// `Setting` was numbered against (D-045). The order is the reference bar's, left to right.
    /// </para>
    /// </summary>
    public enum BoosterId
    {
        AddColumn = 0,
        Undo = 1,
        Shuffle = 2,
    }

    /// <summary>
    /// The enum's two companions: every booster there is, and the string each one is filed
    /// under in the save.
    /// <para>
    /// <see cref="Core.BoosterChargeRecord"/> keys charges by a <em>string</em> on purpose — a
    /// save file outlives the enum a build happened to declare — so this is the one place the
    /// two representations meet. A renamed enum member keeps its save id; a new booster brings
    /// a new one.
    /// </para>
    /// </summary>
    public static class BoosterIds
    {
        /// <summary>Every booster, in bar order. Allocated once: config validation walks it.</summary>
        public static readonly BoosterId[] All =
        {
            BoosterId.AddColumn,
            BoosterId.Undo,
            BoosterId.Shuffle,
        };

        /// <summary>
        /// What this booster is called on disk. Deliberately not <c>ToString()</c>: that would
        /// tie every save file ever written to the C# spelling of an enum member, and a rename
        /// would quietly reset three charge counts to zero.
        /// </summary>
        public static string SaveIdOf(BoosterId booster)
        {
            switch (booster)
            {
                case BoosterId.AddColumn:
                    return "add_column";

                case BoosterId.Undo:
                    return "undo";

                case BoosterId.Shuffle:
                    return "shuffle";

                default:
                    // A fourth booster was added to the enum and nobody gave it a save id. That
                    // is a code mismatch, not a value to fall back from — falling back would
                    // file two boosters under one key and let them spend each other's charges.
                    return null;
            }
        }
    }
}
