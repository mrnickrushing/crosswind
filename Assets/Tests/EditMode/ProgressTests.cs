using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class ProgressTests
    {
        double _now = 1_800_000_000;
        Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => _now; Sim.UtcOffsetSeconds = 0; return s; }

        [Test]
        public void ResearchTakesRealTimeAndChangesTheRules()
        {
            var s = Fresh();
            s.State.money = 1e6;
            float before = s.TaxiSpeed;
            Assert.IsTrue(s.StartResearch("ops_marshal"));
            StringAssert.Contains("busy", s.ResearchBlocked("pax_signs"));
            s.Tick(1);
            Assert.IsFalse(s.Researched("ops_marshal"), "not yet");
            _now += 61;
            s.Tick(1);
            Assert.IsTrue(s.Researched("ops_marshal"));
            Assert.Greater(s.TaxiSpeed, before);
            StringAssert.Contains("Needs", s.ResearchBlocked("ops_crews"));
            Assert.IsNull(s.ResearchBlocked("pax_signs"));
        }

        [Test]
        public void GemsFinishResearchNow()
        {
            var s = Fresh();
            s.State.money = 1e6; s.State.gems = 3;
            s.StartResearch("pax_signs");                       // two minutes
            Assert.AreEqual(2, s.ResearchSkipGems);
            Assert.IsTrue(s.SkipResearch());
            Assert.AreEqual(1, s.State.gems);
            Assert.IsTrue(s.Researched("pax_signs"));
        }

        [Test]
        public void ABoostDoublesIncomeForAWhileAndStacks()
        {
            var s = Fresh();
            var r = Content.Routes["puffin_isles"];
            double plain = s.FlightValue(r);
            s.AddBoost();
            Assert.AreEqual(plain * Content.BoostScale, s.FlightValue(r), 1e-6);
            s.AddBoost();
            Assert.AreEqual(Content.BoostMinutes * 120, s.BoostLeft, 1);
            _now += Content.BoostMinutes * 120 + 1;
            Assert.AreEqual(plain, s.FlightValue(r), 1e-6);
            for (int i = 0; i < 20; i++) s.AddBoost();
            Assert.LessOrEqual(s.BoostLeft, 4 * 3600);
        }

        [Test]
        public void BadWeatherSlowsTheRunwayAndLandingAidsHelp()
        {
            var s = Fresh();
            float clear = s.RunwaySpeed;
            s.State.weather = (int)Weather.Storm; s.State.weatherUntil = 1e9;
            float storm = s.RunwaySpeed;
            Assert.Less(storm, clear);
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[4];
            for (int i = 0; i < 5; i++) Assert.IsTrue(s.Upgrade("ils"));
            Assert.Greater(s.RunwaySpeed, storm);
            Assert.LessOrEqual(s.RunwaySpeed, clear + 1e-4f);
        }

        [Test]
        public void OpportunitiesComeAndGoAndPay()
        {
            var s = Fresh();
            s.State.money = 1e6; s.Hire("fuel");
            int seen = 0, taken = 0; double money = 0;
            for (int i = 0; i < 4 * 3600; i++)
            {
                s.Tick(0.25f);
                if (s.State.opportunity == null) continue;
                seen++;
                var kind = s.State.opportunity.kind;
                money = s.State.money; int gems = s.State.gems;
                Assert.IsTrue(s.TakeOpportunity());
                taken++;
                if (kind == "charter") Assert.Greater(s.State.money, money);
                if (kind == "rush") Assert.IsTrue(s.Rush);
                if (kind == "gems") Assert.Greater(s.State.gems, gems);
                Assert.IsFalse(s.TakeOpportunity(), "once");
            }
            Assert.Greater(seen, 2, "several in an hour");
            Assert.AreEqual(seen, taken);
        }

        [Test]
        public void TodaysTasksCountAndPayGemsOnce()
        {
            var s = Fresh();
            s.Tick(0.1f);
            Assert.AreEqual(3, s.State.tasks.Count);
            Assert.AreEqual(3, s.State.tasks.Select(t => t.kind).Distinct().Count());
            var task = s.State.tasks[0];
            Assert.IsFalse(s.ClaimTask(0));
            task.progress = task.target;
            int gems = s.State.gems;
            Assert.IsTrue(s.ClaimTask(0));
            Assert.AreEqual(gems + task.gems, s.State.gems);
            Assert.IsFalse(s.ClaimTask(0));
            _now += 86400;
            s.Tick(0.1f);
            Assert.IsFalse(s.State.tasks[0].claimed, "new tasks the next day");
            foreach (var t in s.State.tasks) Assert.IsNotEmpty(Sim.TaskText(t));
        }

        [Test]
        public void ManagersTrainAndTheirCrewsSpeedUp()
        {
            var s = Fresh();
            s.State.money = 1e9;
            StringAssert.Contains("Hire", s.TrainBlocked("fuel"));
            s.Hire("fuel");
            float before = s.CrewSpeed(Service.Fuel);
            double cost = s.TrainCost("fuel");
            Assert.IsTrue(s.Train("fuel"));
            Assert.AreEqual(2, s.ManagerLevel("fuel"));
            Assert.Greater(s.CrewSpeed(Service.Fuel), before);
            Assert.Greater(s.TrainCost("fuel"), cost);
        }

        [Test]
        public void SellingUpKeepsWingsAndResearchAndStartsAgainSomewhereHarder()
        {
            var s = Fresh();
            Assert.IsNotNull(s.SellBlocked("coral"));
            s.State.ratingXp = Content.StarXp[Content.SellStars];
            s.State.lifetimeEarned = 5e8;
            s.State.researched.Add("com_fees"); s.State.gems = 12;
            int wings = s.WingsOnSale;
            Assert.GreaterOrEqual(wings, Content.Maps.Find(m => m.Id == "coral").WingsToOpen);
            Assert.IsNotNull(s.SellBlocked("metro"), "not enough Wings for the capital");
            double meadowValue = s.FlightValue(Content.Routes["puffin_isles"]);
            Assert.IsTrue(s.SellAndMove("coral"));
            Assert.AreEqual("coral", s.State.map);
            Assert.AreEqual(wings, s.State.wings);
            Assert.AreEqual(1, s.Stars);
            Assert.AreEqual(1, s.State.contracts.Count);
            Assert.AreEqual(12, s.State.gems);
            Assert.IsTrue(s.Researched("com_fees"));
            CollectionAssert.Contains(s.State.mapsRun, "meadow");
            Assert.Greater(s.FlightValue(Content.Routes["puffin_isles"]), meadowValue * 3, "the island pays more, and Wings add to it");
            Assert.Greater(s.Cost("stands"), 150, "and costs more");
            s.Tick(30);
            // Coral Cay has no room for a long runway.
            s.State.money = 1e30;
            while (s.UpgradeBlocked("runway") == null) s.Upgrade("runway");
            Assert.AreEqual(12, s.Level("runway"));
            StringAssert.Contains("No room", s.UpgradeBlocked("runway"));
        }

        [Test]
        public void AchievementsPayGemsOnce()
        {
            var s = Fresh();
            var a = Sim.Achievements.Find(x => x.Id == "flights_100");
            Assert.IsFalse(s.ClaimAchievement(a.Id));
            s.State.flightsHandled = 100;
            Assert.AreEqual(1, s.AchievementsWaiting);
            Assert.IsTrue(s.ClaimAchievement(a.Id));
            Assert.AreEqual(a.Gems, s.State.gems);
            Assert.IsFalse(s.ClaimAchievement(a.Id));
            Assert.AreEqual(Sim.Achievements.Count, Sim.Achievements.Select(x => x.Id).Distinct().Count());
        }

        [Test]
        public void EveryRouteCanBeFlownAndEveryResearchReached()
        {
            foreach (var r in Content.Routes.Values)
            {
                Assert.IsTrue(Content.Airlines.ContainsKey(r.Airline), r.Id);
                Assert.LessOrEqual(r.Stars, Content.MaxStars, r.Id);
                Assert.IsNotEmpty(r.Needs, r.Id);
            }
            foreach (var r in Content.Research)
            {
                if (r.Requires != null) Assert.IsNotNull(Content.ResearchById(r.Requires), r.Id);
                Assert.LessOrEqual(r.Stars, Content.MaxStars);
            }
            Assert.AreEqual(Content.Research.Count, Content.Research.Select(r => r.Id).Distinct().Count());
            Assert.AreEqual(Goals.All.Count, Goals.All.Select(g => g.Id).Distinct().Count());
        }

        [Test]
        public void ASaveWithTheNewFieldsSurvivesBeingEmptied()
        {
            var st = GameState.New();
            st.researched = null; st.tasks = null; st.managerLevels = null; st.achievements = null; st.mapsRun = null;
            st.research = new ResearchJob(); st.opportunity = new Opportunity(); st.map = "nowhere";
            var s = new Sim(st); s.RealNow = () => _now;
            Assert.IsNull(s.State.research); Assert.IsNull(s.State.opportunity);
            Assert.AreEqual("meadow", s.State.map);
            s.Tick(10);
        }
    }
}
