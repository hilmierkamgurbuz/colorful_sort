using System;
using System.Collections.Generic;

namespace ColorfulSort.Core
{
    /// <summary>
    /// What one level remembers between sessions. <c>plays</c> is not a statistic:
    /// it is the attempt ordinal <see cref="AttemptSeedSource"/> turns into a seed,
    /// and the number <c>Meta</c> rotates a level's variant with (D-015).
    /// </summary>
    [Serializable]
    public sealed class LevelProgressRecord
    {
        public int levelOrdinal;
        public bool cleared;
        public int plays;
    }

    /// <summary>
    /// One booster's remaining charges, keyed by a <em>string</em> id on purpose: a
    /// save file outlives the enum that a build happened to declare, so adding a
    /// fourth booster must not renumber the three already on disk.
    /// </summary>
    [Serializable]
    public sealed class BoosterChargeRecord
    {
        public string boosterId;
        public int charges;
    }

    /// <summary>
    /// The whole save file, as plain public fields because <c>JsonUtility</c> reads
    /// nothing else — no properties, no dictionaries, no nullables (fingerprint.md →
    /// Persistence).
    /// <para>
    /// Writers, one per field group and no overlap (fingerprint.md → Data authorities):
    /// <c>saveVersion</c> and <c>playerId</c> belong to <see cref="SaveService"/>,
    /// <c>soundOn</c>/<c>musicOn</c>/<c>vibrationOn</c> to <c>Core</c>
    /// (<see cref="GameRoot"/>), and every progression field to <c>Meta</c>. <c>Core</c>
    /// owns the bytes on disk; <c>Meta</c> asks for the write, it never performs one.
    /// </para>
    /// <para>
    /// A fresh save deliberately carries <em>no economy defaults</em> — 0 coins,
    /// 0 hearts, no charges. Starting hearts and booster charges are tuning numbers
    /// that live in <c>Data/Config/</c> and are <c>Meta</c>'s to seed
    /// (<c>.claude/rules/data.md</c>); typing them into a C# field here would put
    /// content in code for a value nothing reads yet.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// The shape this build writes. Bump it together with an upgrade step in
        /// <see cref="SaveMigration"/> — never on its own.
        /// <para>
        /// 2 added <see cref="musicOn"/>. A file written by version 1 has no such key, and
        /// <c>JsonUtility</c> would leave the field <c>false</c> — an existing player's music
        /// silently off — so the step that raises the version is also the step that turns it
        /// on (D-045).
        /// </para>
        /// </summary>
        public const int CurrentVersion = 2;

        public int saveVersion;
        public string playerId;

        public int currentLevelOrdinal;
        public int coins;
        public int hearts;

        /// <summary>
        /// When the next heart arrives, in Unix milliseconds UTC. 0 means "hearts are
        /// not refilling"; <c>Meta</c> owns what that implies.
        /// </summary>
        public long heartRefillUnixMs;

        public bool soundOn;
        public bool musicOn;
        public bool vibrationOn;

        /// <summary>Only levels the player has actually opened get a record.</summary>
        public List<LevelProgressRecord> levels = new List<LevelProgressRecord>();

        public List<BoosterChargeRecord> boosters = new List<BoosterChargeRecord>();

        /// <summary>
        /// The state a player who has never played is in. Sound, music and vibration
        /// start on because that is the platform-conventional default a Settings toggle
        /// then changes — none of them is a tuning number.
        /// </summary>
        public static SaveData NewGame()
        {
            return new SaveData
            {
                saveVersion = CurrentVersion,
                playerId = NewPlayerId(),
                currentLevelOrdinal = 0,
                soundOn = true,
                musicOn = true,
                vibrationOn = true,
            };
        }

        /// <summary>
        /// A device-local identity, used for nothing but telling two save files apart.
        /// A <c>Guid</c> is not gameplay randomness, so the seeded-RNG invariant does
        /// not reach it — no board ever derives from this value.
        /// </summary>
        public static string NewPlayerId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
