using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Advice that names the constraint: the part of the terminal holding passengers up, and why a day lost marks.</summary>
    public class AdviceTests
    {
        static Sim Airport()
        {
            var st = GameState.New(); st.money = 1e7;
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            return s;
        }

        static void SetLevel(Sim s, string id, int level)
        {
            var e = s.State.levels.Find(l => l.id == id);
            if (e == null) s.State.levels.Add(new LevelEntry { id = id, level = level }); else e.level = level;
        }

        [Test]
        public void TheSlowestPartIsNamedWithWhatOneMoreLevelDoes()
        {
            var s = Airport();
            SetLevel(s, "checkin", 4); SetLevel(s, "security", 2); SetLevel(s, "gates", 3);
            CollectionAssert.AreEqual(new[] { "security" }, s.FlowLimiters);
            Assert.AreEqual("security", s.FlowFix);
            Assert.Greater(s.FlowWith(new[] { "security" }), s.TerminalFlow, "one more level of the limiter moves more passengers");
            Assert.AreEqual(s.TerminalFlow, s.FlowWith(new[] { "checkin" }), "upgrading anything else does nothing for the flow");
            string say = s.LimiterSentence(s.FlowLimiters);
            StringAssert.StartsWith("Security is limiting passenger flow (level 2", say);
            StringAssert.Contains("check-in 4", say);
        }

        [Test]
        public void PartsLevelWithEachOtherMustGoUpTogether()
        {
            var s = Airport();
            SetLevel(s, "checkin", 3); SetLevel(s, "security", 3); SetLevel(s, "gates", 5);
            CollectionAssert.AreEquivalent(new[] { "checkin", "security" }, s.FlowLimiters);
            Assert.AreEqual(s.TerminalFlow, s.FlowWith(new[] { "checkin" }), "one of the pair alone moves nothing");
            Assert.Greater(s.FlowWith(s.FlowLimiters), s.TerminalFlow);
            StringAssert.Contains("both go up", s.LimiterSentence(s.FlowLimiters));
            Assert.AreEqual(s.Cost("checkin") + s.Cost("security"), s.FlowFixCost);
        }

        [Test]
        public void TheQueueNeedCarriesTheSpecificAdviceAndItsFix()
        {
            var s = Airport();
            SetLevel(s, "checkin", 1); SetLevel(s, "security", 1); SetLevel(s, "gates", 1);
            // Enough passengers that one level of everything can't keep up.
            for (int i = 0; i < 6; i++) s.State.contracts.Add(new ContractState { route = Content.Routes.Values.Where(r => r.Class == AircraftClass.B).ElementAt(i % 2).Id });
            var q = s.Needs[0];
            Assert.IsTrue(q.Queue);
            Assert.Less(q.Value, 0.999f, "six routes outgrow a level-1 terminal");
            StringAssert.DoesNotContain("slowest", q.Advice);
            StringAssert.Contains("all three", q.Advice);
            Assert.IsNotNull(q.Fix);
        }

        [Test]
        public void CoachingPutsTheBiggestLossFirstAndNamesTheCrew()
        {
            var s = Airport();
            var r = new DayReport { day = 5, flights = 40, onTime = 28, dry = 2, satisfaction = 0.95f, runway = 1f, grade = "C", lateCrew = 10, lateQueue = 1 };
            r.Repair();
            r.wait[(int)Service.Fuel] = 400; r.wait[(int)Service.Baggage] = 60;
            r.score = Sim.ScoreOf(r.flights, r.onTime, r.dry, r.satisfaction, r.runway);
            var lines = s.Coaching(r);
            Assert.AreEqual("ontime", lines[0].Part, "twelve late flights cost the most");
            StringAssert.Contains("12 of 40 flights left late", lines[0].Text);
            StringAssert.Contains("fuel", lines[0].Text);
            Assert.AreEqual("fuel", lines[0].Fix);
            Assert.IsTrue(lines.Any(l => l.Part == "fuel"), "the dry tanks are mentioned");
            Assert.IsFalse(lines.Any(l => l.Part == "runway"), "a sound runway cost nothing");
            for (int i = 1; i < lines.Count; i++) Assert.GreaterOrEqual(lines[i - 1].Lost, lines[i].Lost);
        }

        [Test]
        public void CoachingComparesWithTheDayBeforeAndSaysWhatWasBuilt()
        {
            var s = Airport();
            var r = new DayReport { day = 6, flights = 40, onTime = 38, satisfaction = 1f, runway = 1f, grade = "A", built = new System.Collections.Generic.List<string> { "fuel", "terminal" } };
            r.score = Sim.ScoreOf(40, 38, 0, 1f, 1f);
            r.prevDay = 5; r.prevPunctual = 0.7f; r.prevSatisfaction = 1f; r.prevRunway = 1f; r.prevScore = Sim.ScoreOf(40, 28, 0, 1f, 1f);
            var change = s.Coaching(r).Last();
            Assert.AreEqual("change", change.Part);
            StringAssert.Contains("on time 70% to 95%", change.Text);
            StringAssert.Contains(Content.Facilities["fuel"].Name, change.Text);
            StringAssert.Contains("terminal furniture", change.Text);
            Assert.AreEqual("Better than the day before", change.Title);
        }

        [Test]
        public void AFlawlessDayHasNothingToCoach()
        {
            var s = Airport();
            var r = new DayReport { day = 2, flights = 30, onTime = 30, satisfaction = 1.2f, runway = 0.9f, grade = "A", score = 1 };
            Assert.IsEmpty(s.Coaching(r));
            Assert.AreEqual(0, Sim.PointsToNextGrade(1));
            Assert.AreEqual(3, Sim.PointsToNextGrade(0.77f), "77 is three short of a B (80)");
        }

        [Test]
        public void ADayOfSlowCrewsIsPutDownToTheCrews()
        {
            var s = Airport();
            DayReport got = null; s.DayEnded += r => got = r;
            // Crews go out only once a minute: aircraft wait for them, and leave late.
            float tapIn = 0;
            for (float t = 0; t < Sim.DayLength + 5 && got == null; t += 0.25f)
            {
                if ((tapIn -= 0.25f) <= 0) { tapIn = 60; foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service); }
                s.Tick(0.25f);
            }
            Assert.IsNotNull(got);
            Assert.Greater(got.wait.Sum(), 0, "the waits were counted");
            Assert.Less(got.onTime, got.flights, "a minute's wait for every crew makes flights late");
            Assert.Greater(got.lateCrew, 0, "late flights that had waited are put down to the crews");
            var first = s.Coaching(got)[0];
            Assert.AreEqual("ontime", first.Part);
            StringAssert.Contains("waited for a crew", first.Text);
            Assert.AreEqual(0, s.State.dayWait.Sum(), "a new day starts its own count");
        }
    }
}
