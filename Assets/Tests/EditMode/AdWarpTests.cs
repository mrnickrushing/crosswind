using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class AdWarpTests
    {
        static double _now;
        static Sim Fresh()
        {
            _now = 1_800_000_000;
            var s = new Sim(GameState.New()); s.RealNow = () => _now; s.BuildTimeScale = 0; return s;
        }

        [Test]
        public void TheWarpIsInTheShopForNinetyNineCents()
        {
            var p = Sim.Product("crosswind.warp");
            Assert.IsNotNull(p);
            Assert.AreEqual(2, p.WarpHours);
            Assert.IsFalse(p.Once, "it can be bought again");
            Assert.AreEqual("$0.99", p.FallbackPrice);
        }

        [Test]
        public void AWarpPaysTwoHoursAndBringsEveryTimerNearer()
        {
            var s = Fresh();
            s.State.incomeRate = 10;
            s.State.builds.Add(new BuildJob { facility = "parking", doneAt = _now + 3 * 3600, total = 3 * 3600 });
            s.State.freeBoostAt = _now + 3 * 3600; s.State.freeCrateAt = _now + 3600;
            double pays = s.WarpEarnings(7200);
            Assert.GreaterOrEqual(pays, 10 * 7200);
            Assert.IsTrue(s.Grant("crosswind.warp", "t1"));
            Assert.AreEqual(_now + 3600, s.State.builds[0].doneAt, 1e-6);
            Assert.AreEqual(_now + 3600, s.State.freeBoostAt, 1e-6);
            Assert.IsTrue(s.FreeCrateReady);
            Assert.IsFalse(s.Grant("crosswind.warp", "t1"), "the same transaction never pays twice");
            Assert.IsTrue(s.Grant("crosswind.warp", "t2"), "a second purchase does");
        }

        [Test]
        public void AWarpNeverShortensARunningBoost()
        {
            var s = Fresh();
            s.AddBoost();
            double left = s.BoostLeft;
            s.Warp(7200);
            Assert.AreEqual(left, s.BoostLeft, 1e-6);
        }

        [Test]
        public void ABoostByAdvertAndSixADay()
        {
            var s = Fresh();
            Assert.AreEqual(Sim.AdsPerDay, s.AdsLeft);
            Assert.IsTrue(s.BoostByAd());
            Assert.IsTrue(s.Boosted);
            Assert.AreEqual(Sim.AdsPerDay - 1, s.AdsLeft);
            for (int i = 0; i < 20; i++) s.BoostByAd();
            Assert.LessOrEqual(s.BoostLeft, 4 * 3600 + 1, "the boost never holds more than four hours");
            s.State.adCount = Sim.AdsPerDay; s.State.boostUntil = 0;
            Assert.IsFalse(s.BoostByAd(), "six a day");
            _now += 86400;
            Assert.IsTrue(s.BoostByAd(), "and again tomorrow");
        }

        [Test]
        public void NothingAwayNothingDoubledAndNoAdvertCounted()
        {
            var s = Fresh();
            Assert.AreEqual(0, s.CollectOfflineByAd(30));
            Assert.AreEqual(Sim.AdsPerDay, s.AdsLeft);
        }
    }
}
