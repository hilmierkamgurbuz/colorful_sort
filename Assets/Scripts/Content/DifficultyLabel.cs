namespace ColorfulSort.Content
{
    /// <summary>
    /// The label the level plaque shows. Authored per level, never derived from the
    /// board (reference §4) — two levels with the same column count can carry
    /// different labels, so there is nothing to compute.
    /// <para>
    /// The displayed strings are UI's business; this is the id the level stores.
    /// </para>
    /// </summary>
    public enum DifficultyLabel
    {
        Normal = 0,
        Hard = 1,
        SuperHard = 2,
    }
}
