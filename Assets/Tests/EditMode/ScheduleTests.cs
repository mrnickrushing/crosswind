using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class ScheduleTests
    {
        static Sim Fresh(int stars = 3)
        {
            var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0;
            s.State.ratingXp = Content.StarXp[stars]; s.State.money = 1e9;
            return s;
        }

        [Test]
        public void ThePlannerOpensAtThreeStarsAndNightNeedsLights()
        {
            var early = Fresh(2);
            Assert.IsNotNull(early.SlotBlocked("puffin_isles", 1));
            Assert.IsFalse(early.SetSlot("puffin_isles", 1));
            var s = Fresh();
            Assert.IsNull(s.SlotBlocked("puffin_isles", 1));
            Assert.IsNotNull(s.SlotBlocked("puffin_isles", 4), "no runway lights yet");
            Assert.IsTrue(s.SetSlot("puffin_isles", 1));
            Assert.AreEqual(1, s.State.contracts[0].slot);
        }

        [Test]
        public void ASlottedRouteOnlyFliesInItsSlot()
        {
            var s = Fresh();
            s.Hire("fuel");
            s.SetSlot("puffin_isles", 2);                       // afternoon, 12-18
            int inside = 0, outside = 0;
            s.FlightChanged += f => { if (f.phase != Phase.Approach) return; if (Sim.InSlot(2, s.Hour)) inside++; else outside++; };
            for (int i = 0; i < (int)(Sim.DayLength * 2 / 0.25f); i++) s.Tick(0.25f);
            Assert.Greater(inside, 3);
            Assert.LessOrEqual(outside, 1, "at most the one already due when the plan changed");
        }

        [Test]
        public void SlotFlightsPayMoreAndFreightWantsTheNight()
        {
            var day = Content.Routes["puffin_isles"]; var freight = Content.Routes["atlas_night"];
            Assert.Greater(Sim.SlotPay(day, 1), Sim.SlotPay(day, 4));
            Assert.Greater(Sim.SlotPay(freight, 4), Sim.SlotPay(freight, 1));
            // A slot flies 3/4 of the flights, so it has to pay over 4/3 to be worth taking.
            for (int slot = 1; slot <= 4; slot++) Assert.Greater(Sim.SlotPay(day, slot) * Sim.SlotRate * 6 / 24, 1f);
        }

        [Test]
        public void TheLoadChartShowsWhereTheDayIsCrowded()
        {
            var s = Fresh();
            var flat = s.HourlyLoad();
            Assert.AreEqual(flat[3], flat[9], 1e-4);
            s.SetSlot("puffin_isles", 1);
            var load = s.HourlyLoad();
            Assert.AreEqual(flat[9] * Sim.SlotRate, load[9], 1e-3);
            Assert.AreEqual(0, load[14]);
            Assert.Greater(s.HourlyCapacity(), 0);
        }

        [Test]
        public void AutoPlanNeverOverfillsASlot()
        {
            var s = Fresh(8);
            foreach (var f in Content.Facilities.Keys.ToList()) for (int i = 0; i < 6; i++) s.Upgrade(f);
            foreach (var r in Content.Routes.Values.Where(r => r.Airline != "own")) s.Sign(r.Id);
            Assert.Greater(s.State.contracts.Count, 5);
            s.AutoPlan();
            float cap = s.HourlyCapacity(); var load = s.HourlyLoad();
            foreach (var c in s.State.contracts.Where(c => c.slot > 0))
                for (int h = 0; h < 24; h++) if (Sim.InSlot(c.slot, h)) Assert.LessOrEqual(load[h], cap * 1.0001f, "hour " + h);
            Assert.AreEqual(0, s.AutoPlan(), "planning twice changes nothing");
        }
    }
}
