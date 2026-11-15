using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>The co-op week with friends: what counts, the tiers by group size, claims once per tier per week.</summary>
    public class CoopTests
    {
        static Sim Airport(System.Func<double> clock)
        {
            var st = GameState.New(); st.money = 1e6;
            return new Sim(st) { BuildTimeScale = 0, RealNow = clock };
        }

        static void Run(Sim s, float seconds)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
        }

        [Test]
        public void TheGoalGrowsWithTheGroup()
        {
            CollectionAssert.AreEqual(new long[] { 2500, 10000, 30000, 80000 }, Sim.CoopTargets(1), "alone");
            CollectionAssert.AreEqual(new long[] { 4500, 18000, 54000, 144000 }, Sim.CoopTargets(2), "each friend adds four fifths");
            CollectionAssert.AreEqual(new long[] { 10500, 42000, 126000, 336000 }, Sim.CoopTargets(5));
            CollectionAssert.AreEqual(Sim.CoopTargets(1), Sim.CoopTargets(0), "never fewer than you");
            CollectionAssert.AreEqual(Sim.CoopTargets(Sim.CoopMaxMembers), Sim.CoopTargets(500), "never more than you and a full friend list");
            Assert.AreEqual(3_280_000, Sim.CoopTarget(3, 51));
            for (int m = 1; m < Sim.CoopMaxMembers; m++)
                for (int i = 0; i < Sim.CoopBase.Length; i++)
                {
                    Assert.Greater(Sim.CoopTarget(i, m + 1), Sim.CoopTarget(i, m), "a bigger group needs more");
                    Assert.LessOrEqual(Sim.CoopTarget(i, m + 1) / (double)(m + 1), Sim.CoopTarget(i, m) / (double)m, "but no more per head");
                    if (i > 0) Assert.Greater(Sim.CoopTarget(i, m), Sim.CoopTarget(i - 1, m));
                }
            Assert.AreEqual(Sim.CoopBase.Length, Sim.CoopGems.Length);
            Assert.LessOrEqual(Sim.CoopGems.Sum(), 40, "small rewards: the weekly event pays more");
        }

        [Test]
        public void EveryFlightThatPaysCountsItsPassengersBothWays()
        {
            double clock = 1_800_000_000;
            var s = Airport(() => clock);
            long expected = 0;
            s.FlightPaid += (f, paid, onTime) => expected += Content.Aircraft[Content.Routes[f.route].Class].Seats * 2;
            Run(s, 300);
            Assert.Greater(s.State.flightsHandled, 0, "flights have flown");
            Assert.AreEqual(expected, s.CoopMine);
            Assert.AreEqual(expected, s.CoopGroup, "alone, the group is you");
            Assert.AreEqual(1.0, s.CoopShareOfGroup);
        }

        [Test]
        public void TheWeekStartsAgainOnMonday()
        {
            double clock = 1_800_000_000;
            var s = Airport(() => clock);
            int week = s.Week;
            Assert.AreEqual(0, s.CoopMine, "opens the week");
            s.State.coopPassengers = 12000;
            Assert.IsTrue(s.SetCoopGroup(week, 40000, 11000, 3));
            Assert.AreEqual(12000 + 29000, s.CoopGroup, "the friends' part, plus your own count as it is now");
            Assert.AreEqual(3, s.CoopMembers);
            s.ClaimCoop();
            Assert.AreEqual(1, s.CoopClaimed);

            // Monday at midnight (UTC, as the server counts it): a fresh week.
            int today = s.Today;
            while (s.Week == week) clock += 3600;
            Assert.AreEqual(week + 1, s.Week);
            Assert.LessOrEqual(s.Today - today, 7);
            Assert.AreEqual(0, s.CoopMine);
            Assert.AreEqual(0, s.CoopGroup);
            Assert.AreEqual(1, s.CoopMembers);
            Assert.AreEqual(0, s.CoopClaimed);
            Assert.IsFalse(s.SetCoopGroup(week, 99999, 0, 4), "a reply about last week changes nothing");
            Assert.AreEqual(0, s.CoopGroup);
            Assert.AreEqual(1, s.CoopMembers);
        }

        [Test]
        public void EachTierPaysOncePerWeek()
        {
            double clock = 1_800_000_000;
            var s = Airport(() => clock);
            int week = s.Week, gems = s.State.gems;
            Assert.AreEqual(0, s.ClaimCoop(), "nothing reached");
            s.State.coopPassengers = 2500;      // as if flown (the week was opened by the claim above)
            Assert.AreEqual(1, s.CoopWaiting);
            Assert.AreEqual(Sim.CoopGems[0], s.ClaimCoop());
            Assert.AreEqual(0, s.ClaimCoop(), "the same tier doesn't pay twice");
            Assert.AreEqual(gems + Sim.CoopGems[0], s.State.gems);

            // Two friends join in: the goal grows, and the group's total carries it past two more tiers.
            s.SetCoopGroup(week, 60000, 2500, 3);
            CollectionAssert.AreEqual(new long[] { 6500, 26000, 78000, 208000 }, s.CoopGoal);
            Assert.AreEqual(2, s.CoopReached);
            Assert.AreEqual(1, s.CoopWaiting);
            Assert.AreEqual(Sim.CoopGems[1], s.ClaimCoop());
            Assert.AreEqual(0, s.ClaimCoop());

            // More friends: the goal outgrows the total. What was claimed stays claimed, and doesn't pay again.
            s.SetCoopGroup(week, 60000, 2500, 40);
            Assert.AreEqual(0, s.CoopReached);
            Assert.AreEqual(0, s.CoopWaiting);
            Assert.AreEqual(0, s.ClaimCoop());
            s.SetCoopGroup(week, 60000, 2500, 3);
            Assert.AreEqual(0, s.CoopWaiting, "back to two tiers reached, both claimed");

            // Everything at once: the rest pay one at a time, in order, then nothing.
            s.SetCoopGroup(week, 1_000_000, 2500, 3);
            Assert.AreEqual(2, s.CoopWaiting);
            Assert.AreEqual(Sim.CoopGems[2], s.ClaimCoop());
            Assert.AreEqual(Sim.CoopGems[3], s.ClaimCoop());
            Assert.AreEqual(0, s.ClaimCoop());
            Assert.AreEqual(gems + Sim.CoopGems.Sum(), s.State.gems);

            // Next week, the tiers pay again.
            while (s.Week == week) clock += 3600;
            Assert.AreEqual(0, s.CoopMine);
            s.State.coopPassengers = 2500;
            Assert.AreEqual(Sim.CoopGems[0], s.ClaimCoop());
        }

        [Test]
        public void AReplyIsReadCarefully()
        {
            double clock = 1_800_000_000;
            var s = Airport(() => clock);
            Assert.AreEqual(0, s.CoopMine);
            s.State.coopPassengers = 500;
            s.SetCoopGroup(s.Week, 100, 400, 0);
            Assert.AreEqual(500, s.CoopGroup, "a total under your own count adds nothing");
            Assert.AreEqual(1, s.CoopMembers, "never fewer than you");
            s.SetCoopGroup(s.Week, 5000, -10, 9999);
            Assert.AreEqual(5500, s.CoopGroup);
            Assert.AreEqual(Sim.CoopMaxMembers, s.CoopMembers);
            Assert.AreEqual(500 / 5500.0, s.CoopShareOfGroup, 1e-9);
        }

        [Test]
        public void AnOlderSaveStartsTheWeekClean()
        {
            var st = GameState.New();
            Assert.AreEqual(-1, st.coopWeek);
            Assert.AreEqual(1, st.coopMembers);
            st.coopMembers = 0; st.coopClaimed = -2;
            st.Repair();
            Assert.AreEqual(1, st.coopMembers);
            Assert.AreEqual(0, st.coopClaimed);
            var s = new Sim(st) { RealNow = () => 1_800_000_000 };
            Assert.AreEqual(0, s.CoopMine);
            Assert.AreEqual(s.Week, s.State.coopWeek);
        }
    }
}
