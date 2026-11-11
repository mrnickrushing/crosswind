using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Contracts: the market, the book, the plan, the flights, the marks, reputation; the week and the ledger.</summary>
    public class ContractTests
    {
        double _clock;

        /// <summary>A two-star airport with three stands, managers on fuel and baggage, and a market open.</summary>
        Sim Airport()
        {
            _clock = 1_800_000_000;
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => _clock;
            s.State.money = 1e9;
            s.State.ratingXp = Content.StarXp[2];
            for (int i = 0; i < 2; i++) s.Upgrade("stands");
            s.Upgrade("baggage"); s.Upgrade("runway"); s.Upgrade("runway");
            // A terminal that keeps up with fifty seats, so flights are judged on the ground crews.
            for (int i = 0; i < 5; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            s.Hire("fuel"); s.Hire("baggage");
            s.Tick(0.1f);
            return s;
        }

        void Run(Sim s, float seconds) { for (float t = 0; t < seconds; t += 0.25f) { s.Tick(0.25f); _clock += 0.25; } }

        /// <summary>The first offer that can be signed as the airport stands.</summary>
        Booking Offer(Sim s) { return s.State.market.First(b => s.SignContractBlocked(b.id) == null); }

        [Test]
        public void TheMarketOpensAtTwoStarsAndChangesWithTime()
        {
            var fresh = new Sim(GameState.New());
            fresh.RealNow = () => 1_800_000_000;
            fresh.Tick(1);
            Assert.IsFalse(fresh.ContractsOpen);
            Assert.IsEmpty(fresh.State.market);

            var s = Airport();
            Assert.IsTrue(s.ContractsOpen);
            Assert.GreaterOrEqual(s.State.market.Count, 3);
            foreach (var b in s.State.market)
            {
                Assert.AreNotEqual("own", b.airline);
                Assert.LessOrEqual((AircraftClass)b.plane, s.MaxClass, "nothing the runway can't take");
                Assert.Greater(b.reward, 0); Assert.Greater(b.rep, 0);
                Assert.AreEqual(b.kind == 0 ? 1 : b.flights, b.flights);
                Assert.AreNotEqual(4, b.part, "no night flights without runway lights");
            }
            var first = s.State.market.Select(b => b.id).ToList();
            _clock += Sim.MarketSeconds + 1;
            s.Tick(0.1f);
            Assert.IsFalse(s.State.market.Any(b => first.Contains(b.id)), "a new set of offers");
        }

        [Test]
        public void GemsBringTheNextOffersNow()
        {
            var s = Airport();
            var first = s.State.market.Select(b => b.id).ToList();
            s.State.gems = 0;
            Assert.IsFalse(s.RushMarket());
            s.State.gems = 50;
            int price = s.MarketGems;
            Assert.IsTrue(s.RushMarket());
            Assert.AreEqual(50 - price, s.State.gems);
            Assert.IsFalse(s.State.market.Any(b => first.Contains(b.id)));
        }

        [Test]
        public void TheBookHoldsOnlySoMany()
        {
            var s = Airport();
            int cap = s.BookingCap;
            Assert.AreEqual(2 + s.Level("tower"), cap);
            int signed = 0;
            for (int round = 0; round < 10 && signed < cap; round++)
            {
                foreach (var b in s.State.market.ToList()) if (signed < cap && s.SignContract(b.id)) signed++;
                s.RollMarket();
            }
            Assert.AreEqual(cap, s.State.bookings.Count);
            var extra = s.State.market.First();
            StringAssert.Contains("full", s.SignContractBlocked(extra.id));
            Assert.IsFalse(s.SignContract(extra.id));
        }

        [Test]
        public void ASignedContractIsPlannedInsideItsHours()
        {
            var s = Airport();
            var b = Offer(s);
            Assert.IsTrue(s.SignContract(b.id));
            Assert.IsNull(s.OfferById(b.id));
            Assert.AreSame(b, s.Booking(b.id));
            Assert.GreaterOrEqual(b.stand, 0);
            Assert.GreaterOrEqual(b.hour, Sim.PartStart(b.part));
            Assert.LessOrEqual(b.hour, Sim.LastHour(b));
        }

        [Test]
        public void TwoContractsNeverShareAStandAndAnHour()
        {
            var s = Airport();
            var a = Offer(s); s.SignContract(a.id);
            var b = Offer(s); s.SignContract(b.id);
            // Force them into the same day-part to test the clash.
            b.part = a.part; b.stand = -1; b.hour = -1;
            StringAssert.Contains("kept for", s.PlanBlocked(b.id, a.stand, a.hour));
            Assert.IsFalse(s.Plan(b.id, a.stand, a.hour));
            int other = (a.stand + 1) % s.StandCount;
            Assert.IsTrue(s.Plan(b.id, other, a.hour), "another stand at the same hour is fine");
            StringAssert.Contains("flies in", s.PlanBlocked(b.id, other, Sim.PartStart(b.part) + 7));
            Assert.AreEqual(2, s.AutoPlanContracts());
            Assert.IsTrue(a.stand != b.stand || a.hour != b.hour);
        }

        [Test]
        public void AContractFliesOnceADayAtItsHourAndIsMarked()
        {
            var s = Airport();
            var b = s.State.market.First(x => s.SignContractBlocked(x.id) == null);
            b.kind = 1; b.flights = 3; b.grade = 4;
            double reward = b.reward; int rep = b.rep;
            Assert.IsTrue(s.SignContract(b.id));
            double before = s.State.money;
            int days = 0;
            while (s.Booking(b.id) != null && days++ < 8) { s.BuyFuel(); Run(s, Sim.DayLength); }
            Assert.IsNull(s.Booking(b.id), "three flights, one a day: done inside a few days");
            var rec = s.State.record[0];
            Assert.AreEqual(3, rec.flights);
            Assert.AreEqual(b.number, rec.number);
            Assert.IsTrue(rec.punctual, "punctual: " + rec.onTime + " of " + rec.flights);
            Assert.IsTrue(rec.serviced, "serviced: " + rec.served + " of " + rec.flights);
            Assert.IsTrue(rec.standards, "standards: " + rec.pleased + " of " + rec.flights);
            Assert.AreEqual(reward, rec.paid, 1);
            Assert.AreEqual(rep, rec.rep);
            Assert.AreEqual(rep, s.Rep(b.airline));
            Assert.GreaterOrEqual(days, 3, "never more than one flight a day");
        }

        [Test]
        public void MissedMarksPayLess()
        {
            Assert.AreEqual(1.0, Sim.RewardShare(3), 1e-9);
            Assert.AreEqual(0.4, Sim.RewardShare(0), 1e-9);
            Assert.AreEqual(1.0, Sim.RepShare(3), 1e-9);
            Assert.AreEqual(4, Sim.MarkNeeds(5));
            Assert.AreEqual(1, Sim.MarkNeeds(1));
            var s = Airport();
            var b = Offer(s); s.SignContract(b.id);
            b.flights = 5; b.flown = 3; b.onTime = 1; b.served = 3; b.pleased = 3;
            bool punctual, serviced, standards;
            s.Marks(b, out punctual, out serviced, out standards);
            Assert.IsFalse(punctual, "one on time of three, two to go: four of five is out of reach");
            Assert.IsTrue(serviced); Assert.IsTrue(standards);
        }

        [Test]
        public void DroppingAContractCostsReputation()
        {
            var s = Airport();
            var b = Offer(s); s.SignContract(b.id);
            s.State.rep.Add(new LevelEntry { id = b.airline, level = 100 });
            Assert.IsTrue(s.DropContract(b.id));
            Assert.IsNull(s.Booking(b.id));
            Assert.Less(s.Rep(b.airline), 100);
            Assert.IsTrue(s.State.record[0].dropped);
        }

        [Test]
        public void ReputationRaisesWhatAnAirlinePays()
        {
            var s = Airport();
            Assert.AreEqual(0, s.RepTier("puffin"));
            Assert.AreEqual(1.0, s.RepPay("puffin"), 1e-9);
            s.State.rep.Add(new LevelEntry { id = "puffin", level = Sim.RepAt[2] });
            Assert.AreEqual(2, s.RepTier("puffin"));
            Assert.AreEqual(1 + 2 * Sim.RepBonus, s.RepPay("puffin"), 1e-9);
            Assert.AreEqual(Sim.RepAt[3] - Sim.RepAt[2], s.RepToNext("puffin"));
            Assert.AreEqual(Sim.RepAt[2], s.RepTotal);
        }

        [Test]
        public void APausedAirlineSendsNothing()
        {
            var s = Airport();
            s.Sign("sunhop_bay");
            Assert.IsNull(s.PauseBlocked("sunhop"));
            Assert.IsTrue(s.SetActive("sunhop", false));
            Assert.IsFalse(s.Active("sunhop"));
            Run(s, 300);
            Assert.IsFalse(s.State.flights.Any(f => Content.Routes[f.route].Airline == "sunhop"));
            Assert.IsFalse(s.State.market.Any(b => b.airline == "sunhop") && s.SignContractBlocked(s.State.market.First(b => b.airline == "sunhop").id) == null);
            Assert.IsNotNull(s.PauseBlocked("puffin"), "the last airline flying has to stay");
            Assert.IsFalse(s.SetActive("puffin", false));
            Assert.IsTrue(s.SetActive("sunhop", true));
            Run(s, 200);
            Assert.IsTrue(s.State.flightsHandled > 0);
        }

        [Test]
        public void TheWeekEvensOut()
        {
            foreach (var a in Content.Airlines.Keys)
            {
                float week = 0;
                for (int d = 0; d < 7; d++) week += Sim.WeekFactor(a, d);
                Assert.AreEqual(7f, week, 0.01f, a);
            }
            Assert.Greater(Sim.WeekFactor("sunhop", 5), Sim.WeekFactor("sunhop", 1), "holiday airlines are busy at the weekend");
            Assert.Less(Sim.WeekFactor("crown", 6), Sim.WeekFactor("crown", 2), "business airlines in the week");
            var s = Airport();
            Assert.AreEqual(0, s.Weekday);
            s.State.time += Sim.DayLength * 5;
            Assert.AreEqual(5, s.Weekday);
            Assert.IsTrue(Sim.Weekend(s.Weekday));
        }

        [Test]
        public void FlightsCarryNumbers()
        {
            var s = Airport();
            Run(s, 120);
            Assert.IsNotEmpty(s.State.flights);
            foreach (var f in s.State.flights)
            {
                Assert.Greater(f.number, 0);
                StringAssert.StartsWith(Content.Airlines[Content.Routes[f.route].Airline].Code + " ", s.FlightNo(f));
            }
        }

        [Test]
        public void EveryDayIsClosedIntoTheLedgerAndChargedItsRunningCosts()
        {
            var s = Airport();
            s.State.money = 50000;
            double costs = s.RunningCosts;
            Assert.Greater(costs, 0);
            Run(s, Sim.DayLength * 2 + 5);
            Assert.GreaterOrEqual(s.State.ledger.Count, 1);
            var l = s.State.ledger[0];
            Assert.Greater(l.wages, 0); Assert.Greater(l.upkeep, 0);
            Assert.Greater(l.Income, 0);
            Assert.AreEqual(l.landing + l.pax + l.fuel + l.retail + l.cargo, l.Flights, 1e-6);
            Assert.GreaterOrEqual(l.Income + 1e-6, l.Flights, "flights are part of the day's income");
            Assert.LessOrEqual(s.State.ledger.Count, Sim.LedgerDays);
            // A broke airport is charged what it has, never into the red.
            var poor = Airport();
            poor.State.money = 1;
            Run(poor, Sim.DayLength + 5);
            Assert.GreaterOrEqual(poor.State.money, 0);
        }

        [Test]
        public void AContractRouteSurvivesASave()
        {
            var s = Airport();
            var b = Offer(s); s.SignContract(b.id);
            var route = s.RouteOf(b);
            Assert.IsTrue(Content.IsContractRoute(route.Id));
            Assert.IsTrue(Content.EnsureContractRoute(route.Id));
            Assert.IsFalse(Content.EnsureContractRoute("bk_nobody_0_1_0"));
            Assert.IsFalse(Content.EnsureContractRoute("puffin_isles"));
            Assert.AreEqual(99, route.Stars, "never offered as a route");
            Assert.IsFalse(s.Offers.Any(r => Content.IsContractRoute(r.Id)));
        }
    }
}
