using System;
using ColorfulSort.Core;

namespace ColorfulSort.Meta
{
    /// <summary>
    /// Where the player is in the game: which level is current, which have been cleared, and
    /// how many times each has been played. It is the single writer of those save fields —
    /// `SaveData`'s own documentation already assigns them to `Meta` — while `Core` keeps
    /// owning the bytes on disk. This class never writes a file; it changes the save in memory
    /// and says that it did.
    /// <para>
    /// Plain C#, and it takes a level <em>count</em> rather than the `LevelDatabase`, because
    /// progression is about ordinals and the database is about which asset an ordinal names.
    /// That split is what lets every rule below be tested without a ScriptableObject fixture,
    /// which matters more here than anywhere else in the project: this is the first code whose
    /// mistakes are written to a player's disk, and a board can be undone where a save cannot.
    /// </para>
    /// <para>
    /// Reading and writing are deliberately separate. <see cref="AttemptOrdinal"/> is what the
    /// seed is built from and costs nothing; <see cref="RecordAttemptStarted"/> is called only
    /// once a board is actually on screen. An attempt that fails to open must not consume a
    /// play — that would silently change the board the next honest attempt deals.
    /// </para>
    /// </summary>
    public sealed class Progression
    {
        private readonly SaveData save;

        private readonly int levelCount;

        private readonly Action changed;

        /// <summary>
        /// </summary>
        /// <param name="save">The live save. Its progression fields are this object's to write.</param>
        /// <param name="levelCount">How many levels the database holds.</param>
        /// <param name="changed">
        /// Called after every write, so `Core` can mark the file dirty. A delegate rather than a
        /// `SaveService` so a test can watch it without touching the filesystem.
        /// </param>
        public Progression(SaveData save, int levelCount, Action changed)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (levelCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "A database holds no negative number of levels.");
            }

            this.save = save;
            this.levelCount = levelCount;
            this.changed = changed;

            Repair();
        }

        /// <summary>How many levels there are to play through.</summary>
        public int LevelCount => levelCount;

        /// <summary>Where the player is, as a position in the database's play order.</summary>
        public int CurrentOrdinal => save.currentLevelOrdinal;

        /// <summary>Whether a level exists after the current one.</summary>
        public bool HasNext => CurrentOrdinal + 1 < levelCount;

        /// <summary>
        /// Which attempt of the current level the next one is. It is the play count as it
        /// stands right now, so a player's first attempt is attempt 0 — which is what makes
        /// `(level, attempt)` reproduce a board exactly (D-017).
        /// </summary>
        public int AttemptOrdinal => PlaysOf(CurrentOrdinal);

        /// <summary>How many times a level has been played. Zero for one never opened.</summary>
        public int PlaysOf(int ordinal)
        {
            LevelProgressRecord record = Find(ordinal);
            return record == null ? 0 : record.plays;
        }

        /// <summary>Whether a level has ever been solved.</summary>
        public bool IsCleared(int ordinal)
        {
            LevelProgressRecord record = Find(ordinal);
            return record != null && record.cleared;
        }

        /// <summary>
        /// Records that an attempt actually began. Called after the board is up, never before:
        /// a level that refuses to open has not been played.
        /// </summary>
        public void RecordAttemptStarted()
        {
            RecordFor(CurrentOrdinal).plays++;
            Changed();
        }

        /// <summary>
        /// The current level was solved: mark it cleared and move on. Idempotent by design —
        /// solving a level that is already cleared advances nothing, so a win raised twice, or
        /// a cleared level replayed, cannot walk the player forward twice.
        /// </summary>
        /// <returns>Whether the current ordinal moved to a next level.</returns>
        public bool CompleteCurrentLevel()
        {
            LevelProgressRecord record = RecordFor(CurrentOrdinal);

            // Clearing a level that was already cleared is not an error and it is not a reason to stay
            // put: replaying a finished level and winning it again should still move the player on.
            // The early return that used to live here conflated "I have finished this before" with
            // "there is nowhere to go", and the two are answered by different things — the record and
            // HasNext (D-088).
            record.cleared = true;

            // The last level clears without advancing: there is nowhere to go, and an ordinal
            // pointing past the end is exactly the corrupt state Repair has to undo on load.
            bool advanced = HasNext;

            if (advanced)
            {
                save.currentLevelOrdinal = CurrentOrdinal + 1;
            }

            Changed();
            return advanced;
        }

        /// <summary>
        /// Drags a nonsense ordinal back into range once, at construction. A save can outlive
        /// the database that produced it — levels get reordered, a build ships fewer — and an
        /// ordinal past the end would otherwise open nothing at all, forever.
        /// </summary>
        private void Repair()
        {
            if (save.levels == null)
            {
                save.levels = new System.Collections.Generic.List<LevelProgressRecord>();
            }

            int highest = levelCount < 1 ? 0 : levelCount - 1;
            int repaired = save.currentLevelOrdinal < 0 ? 0 : save.currentLevelOrdinal;

            if (repaired > highest)
            {
                repaired = highest;
            }

            if (repaired == save.currentLevelOrdinal)
            {
                return;
            }

            save.currentLevelOrdinal = repaired;
            Changed();
        }

        private LevelProgressRecord Find(int ordinal)
        {
            for (int index = 0; index < save.levels.Count; index++)
            {
                if (save.levels[index] != null && save.levels[index].levelOrdinal == ordinal)
                {
                    return save.levels[index];
                }
            }

            return null;
        }

        /// <summary>
        /// The record for a level, created on first use — the save only carries levels the
        /// player has actually opened, which is what keeps a 2000-level file small.
        /// </summary>
        private LevelProgressRecord RecordFor(int ordinal)
        {
            LevelProgressRecord existing = Find(ordinal);

            if (existing != null)
            {
                return existing;
            }

            var created = new LevelProgressRecord { levelOrdinal = ordinal };
            save.levels.Add(created);
            return created;
        }

        private void Changed()
        {
            if (changed != null)
            {
                changed();
            }
        }
    }
}
