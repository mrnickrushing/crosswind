using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>A greedy player, to see how long the first hour's milestones take and that nothing ever dead-ends.</summary>
    public class PacingTests
    {
        /// <summary>Plays to a time limit: taps services, claims goals, signs what it can, hires managers, buys the cheapest upgrade.</summary>
        public static List<string> Greedy(Sim s, float seconds, float tapEvery = 1f)
        {
            var log = new List<string>();
            double clock = 1_800_000_000;
            s.RealNow = () => clock;
            int stars = s.Stars, goal = s.State.goalIndex;
            float sinceTap = 0;
            for (float t = 0; t < seconds; t += 0.25f)
            {
                sinceTap += 0.25f;
                if (sinceTap >= tapEvery) { sinceTap = 0; foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service); }
                s.Tick(0.25f);
                clock += 0.25;
                if ((int)(t * 4) % 4 != 0) continue;
                while (s.ClaimGoal()) { }
                s.TakeOpportunity();
                for (int k = 0; k < s.State.tasks.Count; k++) s.ClaimTask(k);
                foreach (var a in Sim.Achievements) s.ClaimAchievement(a.Id);
                if (s.State.research == null)
                {
                    var next = Content.Research.Where(r => s.ResearchBlocked(r.Id) == null).OrderBy(r => r.Cost).FirstOrDefault();
                    if (next != null && s.State.money > s.ResearchCost(next) * 1.5) s.StartResearch(next.Id);
                }
                foreach (var m in s.State.managers.ToList()) if (s.TrainBlocked(m) == null && s.State.money > s.TrainCost(m) * 4) s.Train(m);
                foreach (var r in s.Offers.ToList()) s.Sign(r.Id);
                foreach (var id in Content.FacilityOrder) if (s.HireBlocked(id) == null) s.Hire(id);
                while (s.State.crates > 0) s.OpenCrate();
                {
                    var worst = s.WorstNeed;
                    if (worst.Value < 0.9f)
                    {
                        var item = Sim.Amenities.Where(a => a.Gives == worst.Want && s.Stars >= a.Stars && s.AmenityCost(a.Id) <= s.State.money * 0.5).OrderByDescending(a => a.Points).FirstOrDefault();
                        if (item != null && !s.PlaceAnywhere(item.Id)) { var up = s.State.terminal.Where(t => Sim.AmenityById(t.id).Gives == worst.Want && s.AmenityUpgradeBlocked(t) == null && s.AmenityUpgradeCost(t) <= s.State.money * 0.5).FirstOrDefault(); if (up != null) s.UpgradeAmenity(up); }
                    }
                    if (s.LoungeLevel < s.Stars - 3 && s.Stars >= 4 && s.AmenityCost("lounge") <= s.State.money * 0.3) s.PlaceAnywhere("lounge");
                }

                if (s.PlannerOpen && (s.State.plansMade == 0 || (int)(t / 60) % 10 == 0)) s.AutoPlan();
                // What a contract on offer is waiting for comes first, then the cheapest thing.
                var wanted = Content.Routes.Values.Where(r => !s.Signed(r.Id) && s.Stars >= r.Stars)
                    .SelectMany(r => r.Needs.Select(Content.ServiceFacility).Where(f => s.Level(f) == 0).Concat(s.Level("runway") < Content.Aircraft[r.Class].RunwayLevel ? new[] { "runway" } : new string[0])).Distinct().ToList();
                var pick = wanted.Where(f => s.UpgradeBlocked(f) == null).OrderBy(s.Cost).FirstOrDefault()
                    ?? (wanted.Count == 0 ? Content.FacilityOrder.Where(f => s.UpgradeBlocked(f) == null).OrderBy(s.Cost).FirstOrDefault() : null);
                if (pick != null) s.Upgrade(pick);
                foreach (AircraftClass ac in System.Enum.GetValues(typeof(AircraftClass)))
                    if (s.BuyAircraftBlocked(ac) == null && s.State.money > s.AircraftCost(ac) * 3) s.BuyAircraft(ac);
                if (s.Stars != stars) { stars = s.Stars; log.Add(Format.Time(t) + ": " + stars + " stars, " + Format.Money(s.State.incomeRate * 60) + "/min, " + s.State.contracts.Count + " routes"); }
                if (s.State.goalIndex != goal) { goal = s.State.goalIndex; log.Add(Format.Time(t) + ": goal " + goal + " done (" + Goals.All[goal - 1].Id + ")"); }
            }
            return log;
        }

        [Test, Explicit("over a minute: run after tuning (--filter Name~ThreeDays)")]
        public void ThreeDaysOfPlayReachTheBigJets()
        {
            var s = new Sim(GameState.New());
            var log = Greedy(s, 72 * 3600, 4f);
            foreach (var l in log.Where(x => x.Contains("stars"))) TestContext.WriteLine(l);
            TestContext.WriteLine("after 72h: " + s.Stars + " stars, rate/min " + Format.Money(s.State.incomeRate * 60) + ", " + s.State.flightsHandled + " flights, goal " + s.State.goalIndex + "/" + Goals.All.Count
                + ", research " + s.State.researched.Count + ", runway " + s.Level("runway") + ", fleet " + s.FleetSize + ", wings on sale " + s.WingsOnSale + ", biggest " + s.MaxClass);
            Assert.GreaterOrEqual(s.Stars, 7);
        }

        [Test]
        public void ALongSessionReachesTheSecondAirport()
        {
            var s = new Sim(GameState.New());
            var log = Greedy(s, 12 * 3600, 2f);
            foreach (var l in log.Where(x => x.Contains("stars"))) TestContext.WriteLine(l);
            TestContext.WriteLine("after 12h: " + s.Stars + " stars, money " + Format.Money(s.State.money) + ", rate/min " + Format.Money(s.State.incomeRate * 60) + ", " + s.State.flightsHandled + " flights, goal " + s.State.goalIndex + "/" + Goals.All.Count
                + ", research " + s.State.researched.Count + "/" + Content.Research.Count + ", gems " + s.State.gems + ", wings on sale " + s.WingsOnSale + ", routes " + s.State.contracts.Count);
            Assert.GreaterOrEqual(s.Stars, Content.SellStars, "a day's play can sell the first airport");
            Assert.Greater(s.WingsOnSale, 0);
        }

        [Test]
        public void TheFirstHourKeepsMoving()
        {
            var s = new Sim(GameState.New());
            var log = Greedy(s, 3600);
            foreach (var l in log) TestContext.WriteLine(l);
            TestContext.WriteLine("after 1h: " + s.Stars + " stars, money " + Format.Money(s.State.money) + ", " + s.State.flightsHandled + " flights (" + s.State.onTime + " on time), goal " + s.State.goalIndex + "/" + Goals.All.Count + ", idle/min " + Format.Money(s.IdleRate() * 60));
            Assert.GreaterOrEqual(s.Stars, 3, "three stars inside the first hour");
            Assert.GreaterOrEqual(s.State.goalIndex, 8, "the guide keeps up");
            Assert.Greater(s.IdleRate(), 0, "the airport earns by itself by then");
            Assert.Greater(s.State.onTime, s.State.late, "mostly on time");
        }
    }
}
