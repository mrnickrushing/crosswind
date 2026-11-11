using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class FinanceTests
    {
        static Sim Fresh(int stars = 3) { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0; s.State.ratingXp = Content.StarXp[stars]; return s; }

        [Test]
        public void FeePolicyTradesPayAgainstFlightsAndGoodwill()
        {
            var early = Fresh(2);
            Assert.IsFalse(early.SetFees(4), "opens at three stars");
            var s = Fresh();
            Assert.AreEqual(1f, s.FeePay); Assert.AreEqual(1f, s.FeeFlights);
            Assert.IsTrue(s.SetFees(4));
            Assert.Greater(s.FeePay, 1f); Assert.Less(s.FeeFlights, 1f); Assert.Less(s.FeeMoodPerFlight, 0f);
            Assert.IsTrue(s.SetFees(0));
            Assert.Less(s.FeePay, 1f); Assert.Greater(s.FeeFlights, 1f); Assert.Greater(s.FeeMoodPerFlight, 0f);
            for (int i = 0; i < 5; i++) Assert.IsNotEmpty(Sim.FeeBlurb(i));
        }

        [Test]
        public void ALoanIsRepaidFromAFifthOfEveryPayment()
        {
            var s = Fresh();
            s.State.money = 1e5; s.Hire("fuel");
            double before = s.State.money;
            Assert.IsTrue(s.TakeLoan("bridge"));
            double lent = s.State.money - before;
            Assert.Greater(lent, 0);
            Assert.AreEqual(System.Math.Floor(lent * 1.08), s.State.loanOwed, 1e-6);
            Assert.IsFalse(s.TakeLoan("growth"), "one at a time");
            double owed = s.State.loanOwed;
            for (int i = 0; i < 4000; i++) s.Tick(0.25f);
            Assert.Less(s.State.loanOwed, owed, "flights pay it down");
            s.State.money = 1e9;
            Assert.IsTrue(s.RepayLoan());
            Assert.IsFalse(s.InDebt);
            Assert.IsNull(s.LoanBlocked(Sim.Loans[1]));
        }

        [Test]
        public void IncomeIsBrokenDownBySource()
        {
            var s = Fresh();
            s.State.money = 1e5; s.Hire("fuel");
            for (int i = 0; i < 4000; i++) s.Tick(0.25f);
            double sum = 0; foreach (var v in s.State.incomeBy) { Assert.GreaterOrEqual(v, 0); sum += v; }
            Assert.Greater(sum, 0);
            Assert.Greater(s.State.incomeBy[0], 0, "landing fees");
        }
    }
}
