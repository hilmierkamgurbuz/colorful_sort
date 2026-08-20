using System;
using System.IO;
using UnityEngine;

namespace ColorfulSort.Core
{
    /// <summary>What <see cref="SaveService.Load"/> found on disk.</summary>
    public enum SaveLoadOutcome
    {
        /// <summary>No file existed; a new game was started.</summary>
        Created,

        /// <summary>A readable save was loaded (possibly after an upgrade step).</summary>
        Loaded,

        /// <summary>A file existed but could not be trusted; it was kept aside and a new game started.</summary>
        Recovered,
    }

    /// <summary>
    /// The save file's single writer (fingerprint.md → Data authorities). Other systems
    /// read and mutate <see cref="Data"/> — <c>Meta</c> owns the progression fields —
    /// and then say <see cref="MarkDirty"/>; only this class turns that into bytes.
    /// <para>
    /// The directory is a constructor argument rather than a call to
    /// <c>Application.persistentDataPath</c> inside, which is what lets EditMode tests
    /// drive the real thing against a temp folder instead of a fake.
    /// </para>
    /// <para>
    /// Writes happen at flush points (level end, pause, focus loss, quit), never per
    /// field change: with the fingerprint's 2000-level ceiling the file is ~80 KB worst
    /// case, and one whole-file write per level is ~0.005 % of the frame budget
    /// amortised — while a write per changed field would be ~100× that for nothing.
    /// </para>
    /// </summary>
    public sealed class SaveService
    {
        public const string FileName = "colorful_sort_save.json";

        private const string TempSuffix = ".tmp";

        /// <summary>How many unreadable saves are kept before the oldest slot is reused.</summary>
        private const int MaxKeptUnreadableFiles = 9;

        private readonly string directory;

        public SaveService(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("A save service needs a directory to write into.", nameof(directory));
            }

            this.directory = directory;
            FilePath = Path.Combine(directory, FileName);
        }

        public string FilePath { get; }

        /// <summary>
        /// The live save. Null until <see cref="Load"/> has run. Mutating a field here
        /// is how a system asks for a change; it must be followed by
        /// <see cref="MarkDirty"/>, or the change lives only until the app closes.
        /// </summary>
        public SaveData Data { get; private set; }

        /// <summary>True when <see cref="Data"/> is ahead of the file on disk.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Reads the file, migrates it, or starts a new game. Never throws on a bad
        /// file: an unreadable save is a recoverable event, not a crash.
        /// </summary>
        public SaveLoadOutcome Load()
        {
            if (!File.Exists(FilePath))
            {
                return StartFresh(SaveLoadOutcome.Created);
            }

            string json;
            try
            {
                json = File.ReadAllText(FilePath);
            }
            catch (Exception readFailure) when (readFailure is IOException || readFailure is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Core] The save file could not be read (" + readFailure.Message +
                                 "); a fresh save was started and the file was left untouched.");
                return StartFresh(SaveLoadOutcome.Recovered);
            }

            SaveData parsed = null;
            string problem = null;

            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception parseFailure)
            {
                // JsonUtility throws ArgumentException on malformed JSON, but the exact
                // type is not part of its contract, so every failure is a bad file here.
                problem = "the file is not valid save JSON (" + parseFailure.Message + ")";
            }

            SaveData migrated = null;
            bool changed = false;

            if (problem == null && !SaveMigration.TryMigrate(parsed, out migrated, out changed, out problem))
            {
                migrated = null;
            }

            if (migrated == null)
            {
                string kept = KeepUnreadableFile();
                Debug.LogWarning("[Core] Save refused: " + problem + ". It was kept as " +
                                 (kept ?? "(it could not be renamed)") + " and a fresh save was started.");
                return StartFresh(SaveLoadOutcome.Recovered);
            }

            Data = migrated;
            IsDirty = changed;
            return SaveLoadOutcome.Loaded;
        }

        /// <summary>Records that <see cref="Data"/> changed and owes the disk a write.</summary>
        public void MarkDirty()
        {
            RequireLoaded();
            IsDirty = true;
        }

        /// <summary>
        /// Writes the save if anything changed. Returns false when there was nothing to
        /// write, and also when the write failed — in which case the save stays dirty
        /// and the next flush point tries again.
        /// </summary>
        public bool Flush()
        {
            RequireLoaded();

            if (!IsDirty)
            {
                return false;
            }

            // An unversioned save is never written (CLAUDE.md invariant). The version is
            // stamped here, at the one place bytes leave the process, rather than trusted
            // to whoever last touched Data.
            Data.saveVersion = SaveData.CurrentVersion;

            if (string.IsNullOrEmpty(Data.playerId))
            {
                Data.playerId = SaveData.NewPlayerId();
            }

            string json = JsonUtility.ToJson(Data);
            string temp = FilePath + TempSuffix;

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temp, json);
                ReplaceFile(temp, FilePath);
                IsDirty = false;
                return true;
            }
            catch (Exception writeFailure) when (writeFailure is IOException ||
                                                 writeFailure is UnauthorizedAccessException ||
                                                 writeFailure is NotSupportedException)
            {
                Debug.LogError("[Core] The save could not be written (" + writeFailure.Message +
                               "); it stays dirty and the next flush point will retry.");
                TryDelete(temp);
                return false;
            }
        }

        private SaveLoadOutcome StartFresh(SaveLoadOutcome outcome)
        {
            Data = SaveData.NewGame();

            // A new game is dirty on purpose: nothing is on disk yet, so the first flush
            // point is what creates the file.
            IsDirty = true;
            return outcome;
        }

        private void RequireLoaded()
        {
            if (Data == null)
            {
                throw new InvalidOperationException("Load() has not run yet, so there is no save to work with.");
            }
        }

        /// <summary>
        /// Puts the finished temp file in place of the real one. <c>File.Replace</c> is
        /// the atomic path; where a platform lacks it, delete-then-move is the best
        /// available, and the temp file is what survives a failure between the two.
        /// </summary>
        private static void ReplaceFile(string temp, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(temp, destination);
                return;
            }

            try
            {
                File.Replace(temp, destination, null);
            }
            catch (Exception replaceFailure) when (replaceFailure is IOException ||
                                                  replaceFailure is NotSupportedException)
            {
                File.Delete(destination);
                File.Move(temp, destination);
            }
        }

        /// <summary>
        /// Moves a save that could not be trusted out of the way instead of deleting it —
        /// it is the only evidence of what went wrong. Slots are bounded, so a device that
        /// keeps producing bad files does not fill up its storage with copies.
        /// </summary>
        private string KeepUnreadableFile()
        {
            string stem = Path.GetFileNameWithoutExtension(FileName);

            try
            {
                for (int slot = 1; slot <= MaxKeptUnreadableFiles; slot++)
                {
                    string candidate = Path.Combine(directory, stem + ".corrupt-" + slot + ".json");
                    if (File.Exists(candidate))
                    {
                        continue;
                    }

                    File.Move(FilePath, candidate);
                    return candidate;
                }

                string lastSlot = Path.Combine(directory, stem + ".corrupt-" + MaxKeptUnreadableFiles + ".json");
                File.Delete(lastSlot);
                File.Move(FilePath, lastSlot);
                return lastSlot;
            }
            catch (Exception moveFailure) when (moveFailure is IOException || moveFailure is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception deleteFailure) when (deleteFailure is IOException || deleteFailure is UnauthorizedAccessException)
            {
                // A leftover temp file is harmless: the next write overwrites it.
            }
        }
    }
}
