using System;
using System.Collections.Generic;
using ColorfulSort.Core;

namespace ColorfulSort.Meta
{
    /// <summary>
    /// What a fresh player is given the first time the economy meets their save: coins in the
    /// purse and charges on each booster. A struct of two numbers rather than the config asset
    /// itself, for the reason <see cref="Progression"/> takes a level <em>count</em> and not
    /// the database — it keeps <see cref="PlayerEconomy"/> plain C# and testable with no
    /// ScriptableObject fixture, which matters most for the code that writes to a player's disk.
    /// </summary>
    public readonly struct EconomySeed
    {
        public EconomySeed(int coins, int chargesPerBooster)
        {
            Coins = coins;
            ChargesPerBooster = chargesPerBooster;
        }

        /// <summary>Coins a save that has never met the economy starts with.</summary>
        public int Coins { get; }

        /// <summary>Charges each booster starts with — the user's three.</summary>
        public int ChargesPerBooster { get; }
    }

    /// <summary>
    /// The player's purse and their booster charges: the single writer of <c>save.coins</c> and
    /// <c>save.boosters</c>, the way <see cref="Progression"/> is the single writer of the
    /// level fields. It changes the save in memory and says that it did; <c>Core</c> still owns
    /// the bytes on disk and is the only thing that writes a file.
    /// <para>
    /// One class for both, not a Wallet plus an Inventory. Coins and charges are one authority
    /// over one save — a purchase moves both in the same breath, and the seeding rule below is
    /// a single fact about a save file. Splitting them would put that fact in two places and
    /// give a buy two writers to keep in step (abstraction-level.md).
    /// </para>
    /// <para>
    /// <b>Seeding, without a save-shape change.</b> `SaveData` already carries <c>coins</c> and
    /// <c>boosters</c> — they were left in place when the economy was cancelled (D-042, D-043),
    /// so nothing here needs a <c>saveVersion</c> bump. A save that has never met the economy
    /// is recognised by its <em>empty</em> booster list: a fresh file and a file written before
    /// this feature existed both look like that and both get seeded once. A player who has
    /// spent every charge has three records reading zero, which is not empty, so they are never
    /// re-seeded — that distinction is why the marker is the records' existence and not their
    /// value.
    /// </para>
    /// </summary>
    public sealed class PlayerEconomy
    {
        private readonly SaveData save;

        private readonly Action changed;

        /// <param name="save">The live save. Its coins and booster charges are this object's to write.</param>
        /// <param name="seed">What to give a save that has never met the economy.</param>
        /// <param name="changed">
        /// Called after every write so `Core` can mark the file dirty. A delegate rather than a
        /// `SaveService` so a test can watch it without touching the filesystem.
        /// </param>
        public PlayerEconomy(SaveData save, EconomySeed seed, Action changed)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (seed.Coins < 0 || seed.ChargesPerBooster < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "A starting balance is never negative.");
            }

            this.save = save;
            this.changed = changed;

            Repair();
            Seed(seed);
        }

        /// <summary>What the player can spend right now.</summary>
        public int Coins => save.coins;

        /// <summary>How many times this booster can still be used before it has to be bought.</summary>
        public int ChargesOf(BoosterId booster)
        {
            BoosterChargeRecord record = Find(booster);
            return record == null ? 0 : record.charges;
        }

        /// <summary>Whether the purse covers a price. A price of 0 is always affordable.</summary>
        public bool CanAfford(int price)
        {
            return price >= 0 && save.coins >= price;
        }

        /// <summary>
        /// Takes one charge off a booster, or refuses. Called <em>after</em> the board has
        /// accepted the mutation, never before: a booster the board refused has not been used,
        /// and charging for it would take a charge for nothing the player can see.
        /// </summary>
        /// <returns>Whether a charge was actually spent.</returns>
        public bool TrySpendCharge(BoosterId booster)
        {
            BoosterChargeRecord record = Find(booster);

            if (record == null || record.charges <= 0)
            {
                return false;
            }

            record.charges--;
            Changed();
            return true;
        }

        /// <summary>
        /// Pays the player. The amount comes from config and the caller decides <em>when</em> —
        /// this only knows how to move the number and say so.
        /// </summary>
        public void Award(int coins)
        {
            if (coins <= 0)
            {
                return;
            }

            save.coins += coins;
            Changed();
        }

        /// <summary>
        /// Buys a pack of charges. Both halves happen or neither does: an unaffordable purchase
        /// leaves the purse and the booster exactly as they were, which is the whole reason the
        /// price check and the two writes live in one method rather than in the caller.
        /// </summary>
        /// <returns>Whether the purchase went through.</returns>
        public bool TryBuy(BoosterId booster, int charges, int price)
        {
            if (charges <= 0 || price < 0 || !CanAfford(price))
            {
                return false;
            }

            BoosterChargeRecord record = RecordFor(booster);

            if (record == null)
            {
                // No save id for this booster: the enum grew and BoosterIds did not. Buying
                // would file the charges under a null key, where nothing would ever find them.
                return false;
            }

            save.coins -= price;
            record.charges += charges;
            Changed();
            return true;
        }

        /// <summary>
        /// Puts an impossible save back in range before anything reads it: a list that arrived
        /// null, a record with no id, a negative balance. It repairs <em>structure</em>, never
        /// policy — a legitimately empty purse stays empty.
        /// </summary>
        private void Repair()
        {
            bool repaired = false;

            if (save.boosters == null)
            {
                save.boosters = new List<BoosterChargeRecord>();
                repaired = true;
            }

            for (int index = save.boosters.Count - 1; index >= 0; index--)
            {
                BoosterChargeRecord record = save.boosters[index];

                if (record == null || string.IsNullOrEmpty(record.boosterId))
                {
                    save.boosters.RemoveAt(index);
                    repaired = true;
                    continue;
                }

                if (record.charges < 0)
                {
                    record.charges = 0;
                    repaired = true;
                }
            }

            if (save.coins < 0)
            {
                save.coins = 0;
                repaired = true;
            }

            if (repaired)
            {
                Changed();
            }
        }

        /// <summary>
        /// The one-off gift, guarded by the emptiness of the booster list. See the class note
        /// for why that is the marker and not a new save field: it costs no <c>saveVersion</c>
        /// bump and it tells "never seeded" apart from "spent everything", which a charge count
        /// of zero cannot.
        /// </summary>
        private void Seed(EconomySeed seed)
        {
            if (save.boosters.Count > 0)
            {
                return;
            }

            for (int index = 0; index < BoosterIds.All.Length; index++)
            {
                BoosterChargeRecord record = RecordFor(BoosterIds.All[index]);

                if (record != null)
                {
                    record.charges = seed.ChargesPerBooster;
                }
            }

            // Added rather than assigned: a save that somehow carries coins already keeps them.
            // The two paths agree on a fresh file, where the balance is zero either way.
            save.coins += seed.Coins;
            Changed();
        }

        private BoosterChargeRecord Find(BoosterId booster)
        {
            string id = BoosterIds.SaveIdOf(booster);

            if (id == null)
            {
                return null;
            }

            for (int index = 0; index < save.boosters.Count; index++)
            {
                BoosterChargeRecord record = save.boosters[index];

                if (record != null && record.boosterId == id)
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>
        /// The record for a booster, created on first use — the same shape as a level's record,
        /// and for the same reason: the save carries what the player has actually met.
        /// </summary>
        private BoosterChargeRecord RecordFor(BoosterId booster)
        {
            BoosterChargeRecord existing = Find(booster);

            if (existing != null)
            {
                return existing;
            }

            string id = BoosterIds.SaveIdOf(booster);

            if (id == null)
            {
                return null;
            }

            var created = new BoosterChargeRecord { boosterId = id };
            save.boosters.Add(created);
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
