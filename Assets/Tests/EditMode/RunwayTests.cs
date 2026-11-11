using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Runway wear and the resurfacing closure.</summary>
    public class RunwayTests
    {
        static Sim Airport()
        {
            var st = GameState.New(); st.money = 1e9;
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            return s;
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
        public void EveryMovementWearsTheSurfaceAndHeavyAircraftMost()
        {
            var s = Airport();
            Assert.AreEqual(1f, s.RunwayCondition);
            Run(s, 300);
            Assert.Greater(s.State.flightsHandled, 0);
            Assert.AreEqual(s.State.flightsHandled * 2 * s.WearOf(AircraftClass.A), s.State.runwayWear, s.WearOf(AircraftClass.A) * 2.5f);
            Assert.Greater(s.WearOf(AircraftClass.E), s.WearOf(AircraftClass.A) * 5);
            float before = s.WearOf(AircraftClass.C);
            s.Upgrade("runway");
            Assert.Less(s.WearOf(AircraftClass.C), before, "a better runway lasts longer");
        }

        [Test]
        public void AWornRunwayIsSlowerAndSaysSo()
        {
            var s = Airport();
            float fresh = s.RunwaySpeed;
            s.State.runwayWear = 0.4f;
            Assert.AreEqual(fresh, s.RunwaySpeed, 1e-5, "no penalty down to half");
            bool told = false; s.RunwayWorn += () => told = true;
            s.State.runwayWear = 0.4999f;
            Run(s, 200);
            Assert.IsTrue(told);
            s.State.runwayWear = 1f;
            Assert.AreEqual(fresh * Sim.WornSpeed, s.RunwaySpeed, 1e-4);
        }

        [Test]
        public void ResurfacingClosesTheRunwayThenItIsAsNew()
        {
            var s = Airport();
            StringAssert.Contains("good", s.ResurfaceBlocked());
            s.State.runwayWear = 0.8f;
            double money = s.State.money, cost = s.ResurfaceCost;
            // Wait for a landing: the crews can't go on with an aircraft there.
            for (int i = 0; i < 4000 && !s.State.flights.Any(f => f.phase == Phase.Landing); i++) s.Tick(0.1f);
            StringAssert.Contains("on the runway", s.ResurfaceBlocked());
            while (s.State.flights.Any(f => f.phase == Phase.Landing || f.phase == Phase.Takeoff)) s.Tick(0.1f);
            cost = s.ResurfaceCost;
            Assert.IsTrue(s.Resurface());
            Assert.AreEqual(money - cost, s.State.money, 1e-6);
            Assert.AreEqual(1f, s.RunwayCondition);
            Assert.IsTrue(s.RunwayClosed);
            StringAssert.Contains("Crews", s.ResurfaceBlocked());
            int handled = s.State.flightsHandled; bool reopened = false; s.RunwayReopened += () => reopened = true;
            Run(s, Sim.ResurfaceSeconds - 2);
            Assert.AreEqual(handled, s.State.flightsHandled, "nothing leaves a closed runway");
            Assert.IsFalse(s.State.flights.Any(f => f.phase == Phase.Landing || f.phase == Phase.Takeoff));
            Run(s, 120);
            Assert.IsTrue(reopened);
            Assert.Greater(s.State.flightsHandled, handled);
        }

        [Test]
        public void ALightlyWornRunwayIsCheaperToRelay()
        {
            var s = Airport();
            s.State.runwayWear = 0.2f; double light = s.ResurfaceCost;
            s.State.runwayWear = 1f;
            Assert.Greater(s.ResurfaceCost, light);
        }
    }
}
