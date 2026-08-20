using ColorfulSort.Core;
using ColorfulSort.Meta;
using NUnit.Framework;

namespace ColorfulSort.Meta.Tests
{
    /// <summary>
    /// The economy writes to a player's disk, and unlike a board there is no undo for a save.
    /// The rules pinned here are the ones whose failure is silent and permanent: a seeding that
    /// runs twice would hand out free charges forever, a seeding that never runs would leave a
    /// player with nothing, and a purchase that half-happens would take coins for no charges.
    /// </summary>
    public sealed class PlayerEconomyTests
    {
        private SaveData save;

        private int changes;

        [SetUp]
        public void SetUp()
        {
            save = SaveData.NewGame();
            changes = 0;
        }

        private PlayerEconomy NewEconomy(int coins = 100, int charges = 3)
        {
            return new PlayerEconomy(save, new EconomySeed(coins, charges), () => changes++);
        }

        [Test]
        public void AFreshSaveIsSeededWithCoinsAndAChargeOnEveryBooster()
        {
            PlayerEconomy economy = NewEconomy(750, 3);

            Assert.AreEqual(750, economy.Coins);

            for (int index = 0; index < BoosterIds.All.Length; index++)
            {
                Assert.AreEqual(3, economy.ChargesOf(BoosterIds.All[index]),
                    BoosterIds.All[index] + " was not seeded, so a new player starts unable to use it.");
            }

            Assert.Greater(changes, 0, "Seeding writes to the save, so the file has to be marked dirty.");
        }

        [Test]
        public void SeedingHappensOnceEvenThoughTheEconomyIsRebuiltEveryTimeItIsAsked()
        {
            NewEconomy(750, 3);
            PlayerEconomy second = NewEconomy(750, 3);

            Assert.AreEqual(750, second.Coins,
                "A second build re-granted the starting coins; AttemptStarter builds this per call, so that would pay per press.");
            Assert.AreEqual(3, second.ChargesOf(BoosterId.Undo));
        }

        [Test]
        public void APlayerWhoSpentEveryChargeIsNotReSeeded()
        {
            PlayerEconomy economy = NewEconomy(0, 1);
            Assert.IsTrue(economy.TrySpendCharge(BoosterId.Undo));
            Assert.AreEqual(0, economy.ChargesOf(BoosterId.Undo));

            PlayerEconomy reopened = NewEconomy(0, 1);

            Assert.AreEqual(0, reopened.ChargesOf(BoosterId.Undo),
                "Zero charges must not look like a save that has never met the economy, or a booster refills itself.");
        }

        [Test]
        public void ASaveWrittenBeforeTheEconomyExistedIsSeededOnFirstUse()
        {
            // What a file written under D-043 looks like: the fields are in the shape, nothing
            // ever wrote to them. It is indistinguishable from a fresh save on purpose, which is
            // what lets this feature ship without a saveVersion bump.
            save.coins = 0;
            save.boosters.Clear();

            PlayerEconomy economy = NewEconomy(500, 3);

            Assert.AreEqual(500, economy.Coins);
            Assert.AreEqual(3, economy.ChargesOf(BoosterId.Shuffle));
        }

        [Test]
        public void SpendingTakesExactlyOneChargeAndRefusesAtZero()
        {
            PlayerEconomy economy = NewEconomy(0, 2);

            Assert.IsTrue(economy.TrySpendCharge(BoosterId.AddColumn));
            Assert.IsTrue(economy.TrySpendCharge(BoosterId.AddColumn));
            Assert.IsFalse(economy.TrySpendCharge(BoosterId.AddColumn));

            Assert.AreEqual(0, economy.ChargesOf(BoosterId.AddColumn));

            // Two, because that is what this fixture seeded — untouched by the three spends on
            // its neighbour, which is the actual claim: the two are filed under different save
            // ids for exactly this reason.
            Assert.AreEqual(2, economy.ChargesOf(BoosterId.Undo),
                "Spending one booster's charge moved another's; the two are filed under different save ids for exactly this reason.");
        }

        [Test]
        public void BuyingMovesCoinsAndChargesTogether()
        {
            PlayerEconomy economy = NewEconomy(1000, 0);

            Assert.IsTrue(economy.TryBuy(BoosterId.Undo, 3, 750));

            Assert.AreEqual(250, economy.Coins);
            Assert.AreEqual(3, economy.ChargesOf(BoosterId.Undo));
        }

        [Test]
        public void AnUnaffordableBuyChangesNothingAtAll()
        {
            PlayerEconomy economy = NewEconomy(100, 0);
            int before = changes;

            Assert.IsFalse(economy.TryBuy(BoosterId.Undo, 3, 750));

            Assert.AreEqual(100, economy.Coins);
            Assert.AreEqual(0, economy.ChargesOf(BoosterId.Undo));
            Assert.AreEqual(before, changes, "A refused purchase marked the save dirty for a write it did not make.");
        }

        [Test]
        public void AwardingAddsToThePurseAndIgnoresNothing()
        {
            PlayerEconomy economy = NewEconomy(0, 0);

            economy.Award(20);
            economy.Award(20);
            economy.Award(0);
            economy.Award(-5);

            Assert.AreEqual(40, economy.Coins, "A win pays 20; a zero or negative award is not a payment.");
        }

        [Test]
        public void ABrokenSaveIsRepairedIntoSomethingPlayableRatherThanTrusted()
        {
            save.coins = -50;
            save.boosters.Add(null);
            save.boosters.Add(new BoosterChargeRecord { boosterId = BoosterIds.SaveIdOf(BoosterId.Undo), charges = -3 });

            PlayerEconomy economy = NewEconomy(750, 3);

            Assert.AreEqual(0, economy.Coins, "A negative balance is not a debt this game has any way to collect.");
            Assert.AreEqual(0, economy.ChargesOf(BoosterId.Undo));
            Assert.AreEqual(0, economy.ChargesOf(BoosterId.Shuffle),
                "The list was not empty, so this save has met the economy and must not be re-seeded by a repair.");
        }

        [Test]
        public void EverySaveIdIsDistinctSoTwoBoostersCannotShareOneCount()
        {
            for (int a = 0; a < BoosterIds.All.Length; a++)
            {
                string id = BoosterIds.SaveIdOf(BoosterIds.All[a]);
                Assert.IsFalse(string.IsNullOrEmpty(id), BoosterIds.All[a] + " has no save id, so its charges would be filed under null.");

                for (int b = a + 1; b < BoosterIds.All.Length; b++)
                {
                    Assert.AreNotEqual(id, BoosterIds.SaveIdOf(BoosterIds.All[b]),
                        "Two boosters share a save id, so they would spend each other's charges.");
                }
            }
        }
    }
}
