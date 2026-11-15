using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Real-money purchases: paid out once per transaction, delivered after a crash, restored on a new phone.</summary>
    public class StoreTests
    {
        double _clock;

        Sim Airport()
        {
            _clock = 1_800_000_000;
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => _clock;
            return s;
        }

        [Test]
        public void TheTimeWarpPreviewIsWhatTheWarpDoes()
        {
            var s = Airport();
            s.State.incomeRate = 50;                    // 50 a second
            s.State.money = 1e6;
            int level = s.Level("runway");
            s.State.builds.Add(new BuildJob { facility = "runway", doneAt = _clock + 3600, total = 3600 });     // an hour to go: inside a two-hour warp
            s.State.loanOwed = 1000; s.State.loanTotal = 1000;
            double before = s.State.money;
            var p = s.PreviewWarp(2 * 3600);
            Assert.AreEqual(before, s.State.money, "a preview changes nothing");
            Assert.AreEqual(1000, s.State.loanOwed);
            Assert.That(p.Finishes, Has.Some.StartsWith(Content.Facilities["runway"].Name));
            Assert.AreEqual(1000, p.ToBank, 0.01, "the bank's share comes out first");
            s.Warp(2 * 3600);
            Assert.AreEqual(before + p.Money, s.State.money, 1, "it pays what it said");
            s.Tick(0.1f);
            Assert.IsNull(s.Building("runway"), "and the runway is finished");
            Assert.AreEqual(level + 1, s.Level("runway"));
        }

        [Test]
        public void EveryProductSaysWhatIsSpentAndWhatIsKept()
        {
            var s = Airport();
            foreach (var prod in Sim.Products)
            {
                var once = new System.Collections.Generic.List<string>(); var keep = new System.Collections.Generic.List<string>();
                s.ProductContents(prod, once, keep);
                Assert.IsTrue(once.Count + keep.Count > 0, prod.Id + " says what it gives");
                if (prod.Once) Assert.IsNotEmpty(keep, prod.Id + " is bought once and kept: it must say what is kept");
            }
        }

        [Test]
        public void TheExpressLaneGivesAdvertRewardsWithinTheSameDailyLimit()
        {
            var s = Airport();
            Assert.IsFalse(s.AdFree);
            Assert.IsTrue(s.Grant(Sim.AdPass, "tx-pass"));
            Assert.IsTrue(s.AdFree);
            int boosts = 0;
            while (s.AdOffered(AdReward.Boost) && s.BoostByAd()) { boosts++; s.State.boostUntil = 0; }
            Assert.AreEqual(Sim.AdsPerDay, boosts, "no more than the adverts would have given");
            Assert.AreEqual(0, s.AdsLeft);
            // Kept for good: a restore brings it back.
            var again = Airport();
            Assert.AreEqual(1, again.RestoreOwned(new[] { Sim.AdPass }));
            Assert.IsTrue(again.AdFree);
        }

        [Test]
        public void ATransactionPaysOnce()
        {
            var s = Airport(); int gems = s.State.gems;
            Assert.IsTrue(s.Grant("crosswind.gems.small", "tx1"));
            Assert.IsFalse(s.Grant("crosswind.gems.small", "tx1"));
            Assert.AreEqual(gems + 80, s.State.gems);
            Assert.IsTrue(s.Grant("crosswind.gems.small", "tx2"), "a second purchase is a new transaction");
            Assert.AreEqual(gems + 160, s.State.gems);
        }

        [Test]
        public void APurchaseCutShortArrivesLater()
        {
            var s = Airport(); int gems = s.State.gems;
            s.BeginPurchase("crosswind.gems.medium");
            Assert.IsTrue(s.HasPendingPurchases);
            _clock += 30;
            var got = s.ReconcilePurchases(new[] { new StoreReceipt("crosswind.gems.medium", "tx9", _clock - 20) });
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(gems + 500, s.State.gems);
            Assert.IsFalse(s.HasPendingPurchases);
            Assert.AreEqual(0, s.ReconcilePurchases(new[] { new StoreReceipt("crosswind.gems.medium", "tx9", _clock - 20) }).Count);
            Assert.AreEqual(gems + 500, s.State.gems);
        }

        [Test]
        public void AnOldPurchaseFromAnotherPhoneIsNotPaidAgain()
        {
            var s = Airport(); int gems = s.State.gems;
            s.BeginPurchase("crosswind.gems.small");
            var got = s.ReconcilePurchases(new[] { new StoreReceipt("crosswind.gems.small", "old", _clock - 86400) });
            Assert.AreEqual(0, got.Count);
            Assert.AreEqual(gems, s.State.gems);
            Assert.IsTrue(s.HasPendingPurchases, "still waiting for the real one");
            _clock += Sim.PendingFor + 1;
            s.ReconcilePurchases(new StoreReceipt[0]);
            Assert.IsFalse(s.HasPendingPurchases, "given up after a week");
        }

        [Test]
        public void CancellingStopsTheWatch()
        {
            var s = Airport();
            s.BeginPurchase("crosswind.double");
            s.EndPurchase("crosswind.double");
            Assert.IsFalse(s.HasPendingPurchases);
            s.BeginPurchase("no.such.product");
            Assert.IsFalse(s.HasPendingPurchases);
        }

        [Test]
        public void OneTimePurchasesComeBackOnANewPhone()
        {
            var s = Airport(); int gems = s.State.gems;
            Assert.AreEqual(2, s.RestoreOwned(new[] { "crosswind.starter", "crosswind.double", "crosswind.gems.large", "something.else" }));
            Assert.IsTrue(s.Owns("crosswind.starter")); Assert.IsTrue(s.Owns("crosswind.double"));
            Assert.AreEqual(gems, s.State.gems, "a restore brings the crew back, never the pack's gems again");
            Assert.AreEqual(0, s.RestoreOwned(new[] { "crosswind.starter", "crosswind.double" }));
            Assert.AreEqual(gems, s.State.gems);
        }

        [Test]
        public void ErasingAndRestoringMintsNoGems()
        {
            var s = Airport(); int gems = s.State.gems;
            Assert.IsTrue(s.Grant("crosswind.starter", "tx-pack"));
            Assert.AreEqual(gems + 300, s.State.gems, "the purchase itself pays the gems");
            var fresh = Airport(); int start = fresh.State.gems;      // Erase everything, then RESTORE
            Assert.AreEqual(1, fresh.RestoreOwned(new[] { "crosswind.starter" }));
            Assert.IsTrue(fresh.Owns("crosswind.starter"));
            Assert.AreEqual(start, fresh.State.gems);
        }

        [Test]
        public void AnOlderSaveHasNoPendingList()
        {
            var s = Airport();
            s.State.pending = null;
            s.State.Repair();
            Assert.IsFalse(s.HasPendingPurchases);
        }
    }
}
