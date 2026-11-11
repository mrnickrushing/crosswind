using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class AirlineTests
    {
        static Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; return s; }

        [Test]
        public void OnTimeFlightsPleaseAnAirlineAndItPaysMore()
        {
            var s = Fresh();
            s.State.money = 1e6; s.Hire("fuel");
            Assert.AreEqual(50f, s.Relation("puffin"));
            Assert.AreEqual(1.0, s.RelationPay("puffin"), 1e-6);
            for (int i = 0; i < 4800; i++) s.Tick(0.25f);
            Assert.Greater(s.Relation("puffin"), 50f);
            Assert.Greater(s.RelationPay("puffin"), 1.0);
            Assert.LessOrEqual(s.Relation("puffin"), 100f);
        }

        [Test]
        public void NeglectMakesAnAirlineCancelARoute()
        {
            var s = Fresh();
            s.State.money = 1e6;
            s.Sign("puffin_coast");
            RouteDef lost = null;
            s.RouteLost += r => lost = r;
            // Serve every flight, but only after leaving it standing for two minutes.
            for (int i = 0; i < 40000 && lost == null; i++)
            {
                foreach (var (f, j) in s.Waiting.ToList()) if (s.State.time - f.parkedAt > 120) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
            Assert.IsNotNull(lost, "they walked");
            Assert.AreEqual("puffin", lost.Airline);
            Assert.AreEqual(1, s.State.contracts.Count, "but the last route is never lost");
            Assert.Less(s.RelationPay("puffin"), 1.0);
        }

        [Test]
        public void TermsTradeFlightsForPay()
        {
            Assert.Greater(Sim.TermsFlights(1), 1f); Assert.Less(Sim.TermsPatience(1), 1f);
            Assert.Less(Sim.TermsFlights(2), 1f); Assert.Greater(Sim.TermsPay(2), 1f);
            int Flights(int terms)
            {
                var s = Fresh();
                s.State.money = 1e9; s.Hire("fuel");
                for (int i = 0; i < 4; i++) { s.Upgrade("stands"); s.Upgrade("fuel"); s.Upgrade("fuel"); }
                Assert.IsTrue(s.SetTerms("puffin_isles", terms));
                for (int i = 0; i < 7200; i++) s.Tick(0.25f);
                return s.State.flightsHandled;
            }
            Assert.Greater(Flights(1), Flights(0));
            Assert.Greater(Flights(0), Flights(2));
        }
    }
}
