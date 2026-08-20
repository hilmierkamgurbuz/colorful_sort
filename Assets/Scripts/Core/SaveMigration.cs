using System.Collections.Generic;

namespace ColorfulSort.Core
{
    /// <summary>
    /// The load-time half of the versioning invariant: an unversioned save is never
    /// written, and one that is read must announce which shape it is in before a
    /// single field is trusted (CLAUDE.md invariant, fingerprint.md → Persistence).
    /// <para>
    /// Nothing here guesses. A file that carries no version, or a version this build
    /// has no upgrade step for, is <em>refused</em> — <see cref="SaveService"/> then
    /// keeps it aside and starts fresh, which loses a save but never silently rewrites
    /// one whose meaning is unknown.
    /// </para>
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>
        /// Brings a just-parsed save up to <see cref="SaveData.CurrentVersion"/>.
        /// <paramref name="changed"/> reports whether anything was upgraded or
        /// repaired, so the caller knows the in-memory save is now ahead of the file
        /// on disk and owes it a flush.
        /// </summary>
        public static bool TryMigrate(SaveData loaded, out SaveData migrated, out bool changed, out string problem)
        {
            migrated = null;
            changed = false;
            problem = null;

            if (loaded == null)
            {
                problem = "the file parsed to no save object at all";
                return false;
            }

            if (loaded.saveVersion <= 0)
            {
                problem = "the file carries no save version, so nothing can be assumed about its fields";
                return false;
            }

            if (loaded.saveVersion > SaveData.CurrentVersion)
            {
                problem = "the file was written by a newer build (version " + loaded.saveVersion +
                          "; this build reads " + SaveData.CurrentVersion + ")";
                return false;
            }

            while (loaded.saveVersion < SaveData.CurrentVersion)
            {
                // One upgrade step per version, each ending in `loaded.saveVersion = n + 1;`
                // and `changed = true;`. The loop — rather than a step per version *pair* —
                // is what lets a save two versions behind arrive by walking 1 -> 2 -> 3.
                if (loaded.saveVersion == 1)
                {
                    // 1 -> 2 added `musicOn`. The key is absent from a version 1 file, so the
                    // parsed field is `false`, which would read as "this player muted the
                    // music" — a default cannot be told apart from a choice once it is on
                    // disk. Version 2 says it explicitly, matching `NewGame`.
                    loaded.musicOn = true;
                    loaded.saveVersion = 2;
                    changed = true;
                    continue;
                }

                problem = "no upgrade step from save version " + loaded.saveVersion +
                          " to " + SaveData.CurrentVersion;
                return false;
            }

            // `|=`, not `=`: an upgraded save is owed a flush even when there was nothing left
            // to repair, and plain assignment here would throw that away and leave the file on
            // disk at the old version with the new one only in memory.
            changed |= Repair(loaded);
            migrated = loaded;
            return true;
        }

        /// <summary>
        /// Fills in what <c>JsonUtility</c> leaves null when a field is absent from the
        /// file, and nothing else. Progression values are not clamped or corrected here:
        /// they are <c>Meta</c>'s data, and a migration that quietly rewrote them would
        /// be a second writer.
        /// </summary>
        private static bool Repair(SaveData save)
        {
            bool repaired = false;

            if (save.levels == null)
            {
                save.levels = new List<LevelProgressRecord>();
                repaired = true;
            }

            if (save.boosters == null)
            {
                save.boosters = new List<BoosterChargeRecord>();
                repaired = true;
            }

            if (string.IsNullOrEmpty(save.playerId))
            {
                save.playerId = SaveData.NewPlayerId();
                repaired = true;
            }

            return repaired;
        }
    }
}
