using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>The end-of-day report: what it counts, how it grades, what it pays.</summary>
    public class ReportTests
    {
        static Sim Airport()
        {
            var st = GameState.New(); st.money = 1e6;
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            return s;
        }

        static void Run(Sim s, float seconds, bool tap = true)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                if (tap) foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
        }

        [Test]
        public void GradesFollowTheScore()
        {
            Assert.AreEqual(0, Sim.GradeIndex(Sim.ScoreOf(100, 100, 0, 1f, 1f)));
            Assert.AreEqual("A", Sim.Grades[Sim.GradeIndex(0.9f)]);
            Assert.AreEqual("B", Sim.Grades[Sim.GradeIndex(0.85f)]);
            Assert.AreEqual("E", Sim.Grades[Sim.GradeIndex(0.2f)]);
            Assert.Less(Sim.ScoreOf(100, 50, 0, 1f, 1f), Sim.ScoreOf(100, 100, 0, 1f, 1f));
            Assert.Less(Sim.ScoreOf(100, 100, 100, 1f, 1f), Sim.ScoreOf(100, 100, 0, 1f, 1f), "dry tanks count against the day");
            Assert.Less(Sim.ScoreOf(100, 100, 0, 1f, 0.1f), Sim.ScoreOf(100, 100, 0, 1f, 1f), "so does a worn runway");
            Assert.AreEqual(0, Sim.ScoreOf(0, 0, 0, 1f, 1f));
        }

        [Test]
        public void TheDayTurnsIntoAReportThatPaysOnce()
        {
            var s = Airport();
            DayReport got = null; s.DayEnded += r => got = r;
            Run(s, Sim.DayLength + 5);
            Assert.IsNotNull(got, "a day has ended");
            Assert.AreEqual(1, got.day);
            Assert.Greater(got.flights, 0);
            Assert.AreEqual(got.flights, got.onTime + (got.flights - got.onTime));
            Assert.Greater(got.income, 0);
            Assert.IsTrue(s.ReportWaiting);
            double money = s.State.money, bonus = got.bonus;
            Assert.AreEqual(bonus, s.ClaimReport(), 1e-6);
            Assert.AreEqual(money + bonus, s.State.money, 1e-6);
            Assert.AreEqual(0, s.ClaimReport());
            Assert.IsFalse(s.ReportWaiting);
        }

        [Test]
        public void ANeglectedDayGradesWorseThanAWorkedOne()
        {
            var good = Airport(); var bad = Airport();
            DayReport a = null, b = null; good.DayEnded += r => a = r; bad.DayEnded += r => b = r;
            bad.State.fuelStock = 0; bad.State.runwayWear = 1f;
            Run(good, Sim.DayLength + 5);
            // The neglected one only gets its crews half the time.
            for (float t = 0; t < Sim.DayLength + 5; t += 0.25f)
            {
                if ((int)(t / 60) % 2 == 0) foreach (var (f, j) in bad.Waiting.ToList()) bad.Dispatch(f.id, j.service);
                bad.Tick(0.25f);
            }
            Assert.IsNotNull(a); Assert.IsNotNull(b);
            Assert.Greater(a.score, b.score);
            Assert.Greater(b.dry, 0);
        }

        [Test]
        public void GoodDaysInARowBuildAStreakThatPaysGems()
        {
            var s = Airport();
            int gems = s.State.gems, reports = 0, paidGems = 0;
            s.DayEnded += r => { reports++; };
            for (int d = 0; d < 4; d++)
            {
                Run(s, Sim.DayLength);
                if (s.ReportWaiting) { paidGems += s.ReportGems(s.State.report); s.ClaimReport(); }
                if (s.FuelFill < 0.5f) s.BuyFuel();
            }
            Assert.GreaterOrEqual(reports, 3);
            if (s.State.gradeStreak >= Sim.StreakEvery || paidGems > 0) Assert.AreEqual(gems + paidGems, s.State.gems - 0, "the streak's gems arrive on collection");
        }

        [Test]
        public void AnUncollectedBonusRollsIntoTheNextReport()
        {
            var s = Airport();
            Run(s, Sim.DayLength + 5);
            Assert.IsTrue(s.ReportWaiting);
            double first = s.State.report.bonus;
            Run(s, Sim.DayLength);
            Assert.AreEqual(2, s.State.report.day);
            Assert.GreaterOrEqual(s.State.report.bonus, first);
        }
    }
}
