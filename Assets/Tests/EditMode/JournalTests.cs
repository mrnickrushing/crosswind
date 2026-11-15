using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>The journal of firsts, its pages and their gems, and the hall of fame of airports sold.</summary>
    public class JournalTests
    {
        static Sim Airport(GameState st = null)
        {
            st = st ?? GameState.New(); st.money = 1e6;
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

        static void RunUntil(Sim s, System.Func<bool> done, float most = 3000)
        {
            for (float t = 0; t < most && !done(); t += 5) Run(s, 5);
            Assert.IsTrue(done(), "it happened in time");
        }

        [Test]
        public void ANewAirportStartsWithAnEmptyJournal()
        {
            var s = Airport();
            Assert.IsEmpty(s.State.journal, "a signed route is not a visit: nothing has flown yet");
            Assert.IsEmpty(s.State.legacy);
            Assert.AreEqual(0, s.State.trophies);
            Assert.IsTrue(s.State.journalSet);
        }

        [Test]
        public void FirstsAreRecordedOnceWithTheDayTheyHappened()
        {
            var s = Airport();
            RunUntil(s, () => s.State.flightsHandled >= 1);
            var puffin = s.Noted(Sim.PageAirlines, "puffin");
            Assert.IsNotNull(puffin, "the first visitor is in the journal");
            Assert.AreEqual(1, puffin.day, "on the first day");
            Assert.AreEqual("meadow", puffin.map);
            Assert.IsNotNull(s.Noted(Sim.PageClasses, "a"));
            Assert.IsNull(s.Noted(Sim.PageAirlines, "fjord"));
            Assert.IsNull(s.Noted(Sim.PageContracts, "charter"), "no contract flown yet");

            // Into the second day, then a new airline's first flight.
            RunUntil(s, () => s.FuelDay >= 1);
            s.State.contracts.Add(new ContractState { route = "fjord_sound", nextArrival = s.State.time });
            RunUntil(s, () => s.Noted(Sim.PageAirlines, "fjord") != null);
            Assert.AreEqual(s.FuelDay + 1, s.Noted(Sim.PageAirlines, "fjord").day);
            Assert.GreaterOrEqual(s.Noted(Sim.PageAirlines, "fjord").day, 2);

            Run(s, 300);
            Assert.AreEqual(1, s.State.journal.Count(e => e.kind == Sim.PageAirlines && e.id == "puffin"), "a first is written once");
            Assert.AreEqual(1, s.Noted(Sim.PageAirlines, "puffin").day, "and keeps its day");
            Assert.AreEqual(s.State.journal.Count, s.State.journal.Select(e => e.kind + e.id).Distinct().Count());
        }

        [Test]
        public void TheFlightMilestonesFollowTheCount()
        {
            var s = Airport();
            s.State.flightsHandled = 98;
            RunUntil(s, () => s.State.flightsHandled >= 99);
            Assert.IsNull(s.Noted(Sim.JournalMilestone, "flights_100"));
            RunUntil(s, () => s.State.flightsHandled >= 100);
            Assert.IsNotNull(s.Noted(Sim.JournalMilestone, "flights_100"), "the hundredth flight is a milestone");
            Assert.IsNull(s.Noted(Sim.JournalMilestone, "flights_1000"));
        }

        [Test]
        public void AFullPagePaysItsGemsOnce()
        {
            var s = Airport();
            // Every class seen but the smallest: the first flight finishes the page.
            foreach (var id in new[] { "b", "c", "d", "e" }) s.State.journal.Add(new JournalEntry { kind = Sim.PageClasses, id = id, map = "meadow", day = 1 });
            Assert.IsFalse(s.PageDone(Sim.PageClasses));
            Assert.AreEqual(4, s.PageSeen(Sim.PageClasses));
            Assert.IsNotNull(s.ClaimPageBlocked(Sim.PageClasses));
            Assert.AreEqual(0, s.ClaimPage(Sim.PageClasses), "not before it is full");
            string toast = null; s.Toast += t => toast = t;
            RunUntil(s, () => s.State.flightsHandled >= 1);
            Assert.IsTrue(s.PageDone(Sim.PageClasses));
            Assert.AreEqual(1, s.PagesWaiting);
            StringAssert.Contains("Journal page complete", toast);

            int gems = s.State.gems, pay = Sim.PageGems(Sim.PageClasses);
            Assert.That(pay, Is.InRange(10, 25), "small");
            Assert.AreEqual(pay, s.ClaimPage(Sim.PageClasses));
            Assert.AreEqual(gems + pay, s.State.gems);
            Assert.AreEqual(0, s.ClaimPage(Sim.PageClasses), "once");
            Assert.AreEqual(gems + pay, s.State.gems);
            Assert.IsTrue(s.PagePaid(Sim.PageClasses));
            Assert.AreEqual(0, s.PagesWaiting);
            foreach (var p in Sim.JournalPages) Assert.That(Sim.PageGems(p), Is.InRange(10, 25));
        }

        [Test]
        public void EveryPageCanBeFinished()
        {
            CollectionAssert.DoesNotContain(Sim.PageItems(Sim.PageAirlines), "own", "your own airline is not a visitor");
            foreach (var a in Sim.PageItems(Sim.PageAirlines))
                Assert.IsTrue(Content.Routes.Values.Any(r => r.Airline == a && !Content.IsContractRoute(r.Id)), a + " flies a route, so it can come");
            Assert.AreEqual(5, Sim.PageItems(Sim.PageClasses).Count);
            Assert.AreEqual(4, Sim.PageItems(Sim.PageContracts).Count);
            Assert.AreEqual("charter", Sim.KindIds[Sim.Charter]);
            Assert.AreEqual("vip", Sim.KindIds[Sim.Vip]);
            foreach (var p in Sim.JournalPages.Concat(new[] { Sim.JournalMilestone }))
                foreach (var i in Sim.PageItems(p)) Assert.IsNotEmpty(Sim.JournalName(p, i));
        }

        [Test]
        public void AContractFlightWritesItsKind()
        {
            var s = Airport();
            s.State.ratingXp = Content.StarXp[3];
            var b = new Booking { id = s.State.nextBookingId++, airline = "sunhop", plane = 1, kind = Sim.Series, flights = 3, part = 1, needs = Content.NeedsMask(new[] { Service.Fuel }), grade = 4, number = 1234, stand = 0, hour = 0 };
            s.State.bookings.Add(b);
            var r = Content.ContractRoute(b.airline, AircraftClass.B, b.needs, 0);
            s.State.flights.Add(new FlightState { id = s.State.nextFlightId++, route = r.Id, phase = Phase.Approach, arrivedAt = s.State.time, contract = b.id, number = b.number });
            b.airborne = true;
            RunUntil(s, () => s.Noted(Sim.PageContracts, "series") != null);
            Assert.AreEqual(s.FuelDay + 1, s.Noted(Sim.PageContracts, "series").day);
            Assert.IsNotNull(s.Noted(Sim.PageAirlines, "sunhop"));
            Assert.IsNotNull(s.Noted(Sim.PageClasses, "b"));
        }

        [Test]
        public void AnOldSaveIsBackFilledWithWhatItCertainlySawAndNoDates()
        {
            var st = GameState.New();
            st.flightsHandled = 1500; st.daysAtA = 2;
            st.contracts.Add(new ContractState { route = "sunhop_bay" });
            st.record.Add(new BookingRecord { airline = "crown", plane = 3, kind = Sim.Vip, rep = 40 });
            st.record.Add(new BookingRecord { airline = "atlas", plane = 2, kind = Sim.Freight, dropped = true });
            st.rep.Add(new LevelEntry { id = "solstice", level = 12 });
            st.mapsRun.Add("meadow"); st.map = "coral";
            var s = Airport(st);
            foreach (var a in new[] { "puffin", "sunhop", "crown", "atlas", "solstice" }) Assert.IsNotNull(s.Noted(Sim.PageAirlines, a), a);
            Assert.IsNull(s.Noted(Sim.PageAirlines, "zip"), "nothing invented");
            foreach (var c in new[] { "a", "b", "c", "d" }) Assert.IsNotNull(s.Noted(Sim.PageClasses, c), c);
            Assert.IsNull(s.Noted(Sim.PageClasses, "e"));
            Assert.IsNotNull(s.Noted(Sim.PageContracts, "vip"));
            Assert.IsNull(s.Noted(Sim.PageContracts, "freight"), "a dropped contract was not flown to the end");
            Assert.IsNotNull(s.Noted(Sim.JournalMilestone, "flights_100"));
            Assert.IsNotNull(s.Noted(Sim.JournalMilestone, "flights_1000"));
            Assert.IsNull(s.Noted(Sim.JournalMilestone, "flights_10000"));
            Assert.IsNotNull(s.Noted(Sim.JournalMilestone, "widebody"));
            Assert.IsNotNull(s.Noted(Sim.JournalMilestone, "grade_a"));
            Assert.IsTrue(s.State.journal.All(e => e.day == 0), "no dates made up: 0 is before the journal");

            Assert.AreEqual(1, s.State.legacy.Count, "the airport it sold has its line");
            Assert.AreEqual("meadow", s.State.legacy[0].map);
            Assert.AreEqual("Meadowfield", s.State.legacy[0].name);
            Assert.IsTrue(s.State.legacy[0].before, "with nothing but its name");
            Assert.AreEqual(1, s.State.trophies);

            int n = s.State.journal.Count;
            s.State.Repair(); s.State.Repair();
            Assert.AreEqual(n, s.State.journal.Count, "back-filled once");
            Assert.AreEqual(1, s.State.legacy.Count);
            Assert.AreEqual(1, s.State.trophies);
        }

        static void ReadyToSell(Sim s, int daysAtA)
        {
            s.State.ratingXp = Content.StarXp[Content.SellStars];
            s.State.lifetimeEarned = 5e8 * s.Map.IncomeScale;
            s.State.flightsHandled = 4321;
            s.State.daysAtA = daysAtA;
        }

        [Test]
        public void SellingWritesALegacyRecordThatSurvivesTheMove()
        {
            var s = Airport();
            ReadyToSell(s, 3);
            s.State.record.Add(new BookingRecord { airline = "zip", plane = 2, kind = Sim.Series, number = 4411, rep = 30 });
            s.State.record.Add(new BookingRecord { airline = "crown", plane = 1, kind = Sim.Vip, number = 7001, rep = 55 });
            s.State.record.Add(new BookingRecord { airline = "meridian", plane = 3, kind = Sim.Series, rep = 90, dropped = true });
            Assert.IsTrue(s.PlaceAnywhere("vending")); Assert.IsTrue(s.PlaceAnywhere("vending"));
            int items = s.State.terminal.Count;
            int stars = s.Stars;
            Assert.IsTrue(s.SellAndMove("coral", "/photos/meadow.png"));
            Assert.AreEqual("coral", s.State.map);
            Assert.AreEqual(1, s.State.trophies);
            Assert.AreEqual(1, s.State.legacy.Count);
            var l = s.State.legacy[0];
            Assert.AreEqual("meadow", l.map);
            Assert.AreEqual("Meadowfield", l.name);
            Assert.AreEqual(stars, l.stars);
            Assert.AreEqual(4321, l.flights);
            Assert.AreEqual(5e8, l.earned, 1);
            Assert.AreEqual(3, l.daysAtA);
            Assert.AreEqual(items, l.items);
            Assert.AreEqual("crown", l.bestAirline, "the most reputation of the contracts flown to the end");
            Assert.AreEqual(Sim.Vip, l.bestKind);
            Assert.AreEqual(55, l.bestRep);
            Assert.AreEqual(7001, l.bestNumber);
            Assert.AreEqual("/photos/meadow.png", l.photo);
            Assert.IsFalse(l.before);
            // The new airport starts again; the record does not.
            Assert.AreEqual(0, s.State.flightsHandled);
            Assert.AreEqual(0, s.State.lifetimeEarned);
            s.State.Repair();
            var again = new Sim(s.State);
            Assert.AreEqual(1, again.State.legacy.Count);
            Assert.AreEqual(4321, again.State.legacy[0].flights);
            Assert.AreEqual(1, again.State.trophies);
        }

        [Test]
        public void TheHallOfFameKeepsEveryAirportSold()
        {
            var s = Airport();
            ReadyToSell(s, 3);
            Assert.IsTrue(s.SellAndMove("coral"));
            ReadyToSell(s, 5);      // two more days at grade A at the second airport (the count is kept for good)
            s.State.wings = 100;
            Assert.IsTrue(s.SellAndMove("dune"));
            ReadyToSell(s, 5);
            Assert.IsTrue(s.SellAndMove("coral"), "an airport run before can be run again");
            ReadyToSell(s, 6);
            Assert.IsTrue(s.SellAndMove("dune"));
            CollectionAssert.AreEqual(new[] { "meadow", "coral", "dune", "coral" }, s.State.legacy.Select(l => l.map).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 2, 0, 1 }, s.State.legacy.Select(l => l.daysAtA).ToArray(), "each airport's own days at grade A");
            Assert.AreEqual(4, s.State.trophies);
            Assert.AreEqual("", s.State.legacy[0].photo, "no photo taken");
            Assert.AreEqual(3, s.State.mapsRun.Count, "maps run counts places; the hall of fame counts every sale");
        }
    }
}
