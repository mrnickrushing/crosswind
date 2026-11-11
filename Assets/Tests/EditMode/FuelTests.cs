using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>The fuel market: a price that moves by the day, tanks to fill, and what running dry costs.</summary>
    public class FuelTests
    {
        static Sim Airport(bool manager = false)
        {
            var st = GameState.New();
            st.money = 1e9;
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            if (manager) Assert.IsTrue(s.Hire("fuel"), s.HireBlocked("fuel"));
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
        public void ThePriceMovesByTheDayAndStaysInBounds()
        {
            float lo = 9, hi = 0; int below = 0;
            for (int d = 0; d < 400; d++) { float p = Sim.FuelPriceOn(d); lo = System.Math.Min(lo, p); hi = System.Math.Max(hi, p); if (p < 1) below++; }
            Assert.GreaterOrEqual(lo, 0.65f); Assert.LessOrEqual(hi, 1.4f);
            Assert.Less(lo, 0.85f, "there are cheap days"); Assert.Greater(hi, 1.15f, "and dear ones");
            Assert.That(below, Is.InRange(140, 260), "about half the days are below par");
            Assert.AreEqual(Sim.FuelPriceOn(7), Sim.FuelPriceOn(7));
        }

        [Test]
        public void ANewAirportOpensWithFullTanks()
        {
            var s = Airport();
            Assert.AreEqual(s.FuelCapacity, s.FuelStock, 1e-6);
            StringAssert.Contains("full", s.BuyFuelBlocked());
        }

        [Test]
        public void FlightsDrawFuelAndPayThePumpPriceBack()
        {
            var s = Airport();
            double draw = s.FuelDraw(Content.Routes["puffin_isles"]);
            Assert.Greater(draw, 0);
            double stock = s.FuelStock;
            Run(s, 200);
            Assert.Greater(s.State.flightsHandled, 0);
            Assert.AreEqual(stock - s.State.flightsHandled * draw, s.FuelStock, 1e-6);
        }

        [Test]
        public void BuyingCostsTodaysPriceAndCheapDaysAreRemembered()
        {
            var s = Airport();
            s.State.fuelStock = 0;
            double money = s.State.money, cost = s.FuelCost();
            Assert.AreEqual(System.Math.Ceiling(s.FuelCapacity * s.FuelPrice), cost);
            Assert.IsTrue(s.BuyFuel());
            Assert.AreEqual(money - s.FuelCapacity * s.FuelPrice, s.State.money, 1e-3);
            Assert.AreEqual(s.FuelCapacity, s.FuelStock, 1e-6);
            Assert.AreEqual(s.FuelCapacity * (1 - s.FuelPrice), s.State.fuelSaved, 1e-3);
        }

        [Test]
        public void ShortOfMoneyBuysWhatItCan()
        {
            var s = Airport();
            s.State.fuelStock = 0; s.State.money = 30;
            Assert.IsTrue(s.BuyFuel());
            Assert.AreEqual(30 / s.FuelPrice, s.FuelStock, 1e-6);
            Assert.AreEqual(0, s.State.money, 1e-6);
        }

        [Test]
        public void DryTanksCostMostOfTheFuelMargin()
        {
            var full = Airport(); var dry = Airport();
            dry.State.fuelStock = 0;
            bool warned = false; dry.FuelLow += () => warned = true;
            double a = 0, b = 0;
            full.FlightPaid += (f, paid, onTime) => a += paid; dry.FlightPaid += (f, paid, onTime) => b += paid;
            Run(full, 300); Run(dry, 300);
            Assert.AreEqual(full.State.flightsHandled, dry.State.flightsHandled);
            Assert.Less(b, a, "a supplier's fuel leaves less");
            Assert.IsTrue(warned, "and you are told");
            Assert.AreEqual(0, dry.FuelStock, 1e-6);
        }

        [Test]
        public void AFuelManagerOrdersWhenTheGaugeRunsLowAndThePriceIsFair()
        {
            var s = Airport(manager: true);
            s.State.fuelStock = s.FuelCapacity * 0.1;
            // Find a fair day, then let a flight go.
            while (s.FuelPrice > 1.08f) s.State.time += Sim.DayLength;
            foreach (var c in s.State.contracts) c.nextArrival = s.State.time + 1;
            Run(s, 200);
            Assert.Greater(s.State.flightsHandled, 0);
            Assert.Greater(s.FuelFill, 0.4f, "ordered to 60%");
        }

        [Test]
        public void BiggerDepotsAndBiggerAircraftMeanBiggerTanks()
        {
            var s = Airport();
            double small = s.FuelCapacity;
            s.Upgrade("fuel");
            Assert.Greater(s.FuelCapacity, small);
            double mid = s.FuelCapacity;
            while (s.Level("runway") < 3) s.Upgrade("runway");
            Assert.Greater(s.FuelCapacity, mid);
        }
    }
}
