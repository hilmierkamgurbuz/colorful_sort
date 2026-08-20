using System;
using UnityEngine;

namespace ColorfulSort.Meta
{
    /// <summary>
    /// One booster's shop offer: how many charges a pack holds and what it costs. A
    /// serializable row rather than three pairs of loose fields, because the three boosters
    /// are one table with one shape and the Inspector should say so.
    /// </summary>
    [Serializable]
    public sealed class BoosterOffer
    {
        [Tooltip("Which booster this row prices.")]
        [SerializeField]
        private BoosterId booster;

        [Tooltip("How many charges one purchase adds. The reference's pack is 3.")]
        [SerializeField]
        private int charges;

        [Tooltip("What that pack costs in coins. 0 makes it free, which is a design, not a bug.")]
        [SerializeField]
        private int price;

        public BoosterId Booster => booster;

        public int Charges => charges;

        public int Price => price;
    }

    /// <summary>
    /// Progression's tuning numbers: how many looks a level offers, and the whole economy —
    /// what a player starts with, what a cleared level pays, and what a pack of charges costs.
    /// The type lives with the system that reads it and its asset lives in <c>Data/Config/</c>
    /// (D-023).
    /// <para>
    /// This is the "somewhere that is not a C# field default" the save's own documentation
    /// points at: `SaveData` deliberately carries no economy defaults, so the starting coins
    /// and the starting charges have to arrive from here and are handed to
    /// <see cref="PlayerEconomy"/> as an <see cref="EconomySeed"/>.
    /// </para>
    /// <para>
    /// `Board` knows how to build a variant but deliberately knows neither the count nor which
    /// one to play, because both need config and config is `Meta`'s (D-015). The same line
    /// holds for the economy: `Board` runs the three mutations and never learns that one of
    /// them was paid for.
    /// </para>
    /// <para>
    /// No field carries a default, so a fresh asset is invalid and says so rather than
    /// shipping a guessed number (rules/data.md).
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressionConfig", menuName = "Colorful Sort/Progression Config")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Tooltip("How many looks one level offers. The designer must be able to look at every board a player can meet, so this stays small (the user's answer was around five).")]
        [SerializeField]
        private int variantCount;

        [Header("What a new player is given, once")]
        [Tooltip("Coins a save that has never met the economy starts with. It is granted once, not per session.")]
        [SerializeField]
        private int startingCoins;

        [Tooltip("Charges each booster starts with. The user's number is 3.")]
        [SerializeField]
        private int startingBoosterCharges;

        [Header("What the game pays")]
        [Tooltip("Coins for clearing a level for the FIRST time. Replaying a level already cleared pays nothing, or the last level would be a coin tap.")]
        [SerializeField]
        private int coinsPerLevelCleared;

        [Header("What a pack of charges costs")]
        [Tooltip("One row per booster. Every booster needs exactly one, or the shop has nothing to sell for it.")]
        [SerializeField]
        private BoosterOffer[] boosterOffers;

        public int VariantCount => variantCount;

        /// <summary>Coins a first-time player is given. See <see cref="Seed"/>.</summary>
        public int StartingCoins => startingCoins;

        /// <summary>Charges each booster is given to a first-time player.</summary>
        public int StartingBoosterCharges => startingBoosterCharges;

        /// <summary>
        /// What clearing a level for the first time pays. A repeat clear pays nothing: the
        /// ordinal only moves forward once, so a paid replay would make the last level in the
        /// database an infinite coin source (`AttemptStarter.RaiseWon`).
        /// </summary>
        public int CoinsPerLevelCleared => coinsPerLevelCleared;

        /// <summary>
        /// The two starting numbers as the plain-C# struct <see cref="PlayerEconomy"/> takes.
        /// Handing over a struct rather than this asset is what keeps the economy testable
        /// without a ScriptableObject fixture — the same split `Progression` makes with the
        /// level count.
        /// </summary>
        public EconomySeed Seed => new EconomySeed(startingCoins, startingBoosterCharges);

        /// <summary>
        /// What one booster costs and what it gives, or null if this asset prices no such
        /// booster. Null is not a fallback to guess around: <see cref="Validate"/> refuses an
        /// asset with a missing row, so a null here means the config was never authored.
        /// </summary>
        public BoosterOffer OfferFor(BoosterId booster)
        {
            if (boosterOffers == null)
            {
                return null;
            }

            for (int index = 0; index < boosterOffers.Length; index++)
            {
                BoosterOffer offer = boosterOffers[index];

                if (offer != null && offer.Booster == booster)
                {
                    return offer;
                }
            }

            return null;
        }

        public bool Validate(out string error)
        {
            if (variantCount < 1)
            {
                error = "The variant count must be at least 1; a level always has at least its authored look.";
                return false;
            }

            // Zero is legal on all three: a game that starts you with nothing and pays nothing
            // is a design decision. Negative is not a number of coins.
            if (startingCoins < 0 || startingBoosterCharges < 0 || coinsPerLevelCleared < 0)
            {
                error = "The economy starts at " + startingCoins + " coins and " + startingBoosterCharges +
                        " charges and pays " + coinsPerLevelCleared + " per level; none of those is ever negative.";
                return false;
            }

            for (int index = 0; index < BoosterIds.All.Length; index++)
            {
                BoosterId booster = BoosterIds.All[index];
                BoosterOffer offer = OfferFor(booster);

                if (offer == null)
                {
                    error = "There is no shop offer for " + booster + "; a booster the player can run out of " +
                            "must have something to buy, or its button opens a popup with nothing in it.";
                    return false;
                }

                if (offer.Charges < 1)
                {
                    error = "The offer for " + booster + " gives " + offer.Charges +
                            " charges; a pack that adds nothing is a button that does nothing.";
                    return false;
                }

                if (offer.Price < 0)
                {
                    error = "The offer for " + booster + " costs " + offer.Price + " coins; a price is never negative.";
                    return false;
                }
            }

            // A duplicate row is worse than a missing one: OfferFor returns the first, so the
            // second is a price somebody authored and nobody will ever be charged.
            if (boosterOffers != null && boosterOffers.Length > BoosterIds.All.Length)
            {
                error = "There are " + boosterOffers.Length + " shop offers for " + BoosterIds.All.Length +
                        " boosters; a duplicate row is a price that is silently never used.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
