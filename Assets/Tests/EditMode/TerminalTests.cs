using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class TerminalTests
    {
        /// <summary>A five-star airport with money and its whole floor open.</summary>
        static Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0; s.State.money = 1e9; s.State.ratingXp = Content.StarXp[5]; foreach (int b in s.Bays) if (!s.BayOpen(b)) s.State.bays.Add(b); return s; }

        [Test]
        public void ThingsArePlacedOnFreeFloorOnly()
        {
            var s = Fresh();
            Assert.IsTrue(s.Place("cafe", 0, 0, 0));
            Assert.IsFalse(s.Place("seats", 1, 1, 0), "on the cafe");
            Assert.IsNotNull(s.PlaceBlocked("seats", s.FloorCols - 1, 0, 0), "hangs off the edge");
            Assert.IsTrue(s.Place("seats", s.FloorCols - 1, 0, 1), "turned, it fits");
            Assert.IsFalse(s.Place("cafe", 0, s.FloorRows - 1, 0), "off the end");
            Assert.AreEqual(2, s.State.terminal.Count);
        }

        [Test]
        public void TheHallIsACompactRectangleThatGrowsBothWays()
        {
            var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0; s.State.money = 1e15;
            float w0 = s.TerminalLength, d0 = s.TerminalDepth;
            Assert.AreEqual(Sim.MinFloorCols, s.FloorCols);
            Assert.LessOrEqual(w0 / d0, 2f, "never a long strip");
            Assert.GreaterOrEqual(w0 / d0, 0.75f, "nor a deep one");
            // Wider with what stands across it.
            for (int i = 0; i < 12; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            Assert.Greater(s.TerminalLength, w0);
            Assert.GreaterOrEqual(s.TerminalLength, s.SecurityLanes * 4.6f + Sim.ArrivalsGap - 2, "room for the lanes beside the arrivals passage");
            Assert.GreaterOrEqual(s.TerminalLength, (s.CheckinDesks + 5) / 6 * 13.4f + Sim.ReclaimWidth, "room for check-in beside baggage reclaim");
            // Deeper with the rating.
            s.State.ratingXp = Content.StarXp[6];
            Assert.AreEqual(Sim.MinFloorCols + 2 * Sim.BayCols, s.FloorCols, "a row of zones deeper at three stars and again at five");
            Assert.AreEqual(0, s.FloorCols % Sim.BayCols, "always whole zones deep");
            Assert.Greater(s.TerminalDepth, d0);
            // At its largest it is still under two to one.
            s.State.ratingXp = Content.StarXp[Content.MaxStars];
            for (int i = 0; i < 40; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            Assert.AreEqual(Sim.MaxFloorCols, s.FloorCols);
            Assert.LessOrEqual(s.TerminalLength, Sim.MaxTerminalWidth);
            Assert.GreaterOrEqual(s.TerminalLength, s.SecurityLanes * 4.6f + Sim.ArrivalsGap - 2, "even with every lane built");
            Assert.GreaterOrEqual(s.TerminalLength, (s.CheckinDesks + 5) / 6 * 13.4f + Sim.ReclaimWidth, "and every desk");
            Assert.AreEqual(3, s.Carousels);
            Assert.LessOrEqual(s.TerminalLength / s.TerminalDepth, 2f);
            Assert.AreEqual(s.FloorRows * Sim.Cell, s.TerminalLength - 4, 0.01f, "the floor runs the width of the hall, in whole cells");
        }

        [Test]
        public void ANewAirportIsExactlySatisfiedAndSaysWhatNobodyAsksForYet()
        {
            var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000;
            Assert.AreEqual(1f, s.Satisfaction, 0.001f, "every need met reads 100%, not more");
            var needs = s.Needs;
            Assert.IsFalse(needs[0].Unasked, "queues are always asked about");
            Assert.IsTrue(needs.Skip(1).All(n => n.Unasked && n.Value >= 1f), "nothing is placed and nothing is missed yet");
            // Placing something makes it a need answered, and shops take satisfaction past 100%.
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[3];
            Assert.IsTrue(s.PlaceAnywhere("cafe"));
            Assert.IsFalse(s.Needs.First(n => n.Want == Want.Food && !n.Queue).Unasked);
            Assert.Greater(s.Satisfaction, 1f);
        }

        [Test]
        public void ABusyAirportAsksForWhatASmallOneDidNot()
        {
            var s = Fresh();
            for (int i = 0; i < 14; i++) { s.Upgrade("stands"); s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            // Its biggest routes, on the books whatever they would need to be signed.
            foreach (var r in Content.Routes.Values.Where(r => r.Airline != "own" && r.FlightsPerHour > 0).OrderByDescending(r => Content.Aircraft[r.Class].Seats * r.FlightsPerHour).Take(10))
                if (!s.State.contracts.Exists(c => c.route == r.Id)) s.State.contracts.Add(new ContractState { route = r.Id });
            Assert.Greater(s.PassengerDemand, 6, "busier than anything the free allowance covers");
            var food = s.Needs.First(n => n.Want == Want.Food && !n.Queue);
            Assert.IsFalse(food.Unasked);
            Assert.Less(food.Value, 1f, "with nothing to eat, a busy terminal says so");
        }

        [Test]
        public void ANewAirportHasAnOpenCoreAndTheRestForSale()
        {
            var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.State.money = 1e9; s.State.ratingXp = Content.StarXp[3];
            Assert.AreEqual(Sim.FreeBayRows * Sim.FreeBayCols, s.State.bays.Count);
            Assert.IsTrue(s.CellOpen(0, s.FloorRows / 2), "the middle of the front is open");
            Assert.IsFalse(s.CellOpen(0, 0), "the end of the hall is fenced off");
            Assert.IsFalse(s.CellOpen(s.FloorCols - 1, s.FloorRows / 2), "and so is the back, toward the gates");
            Assert.Greater(s.BayColBlocks, Sim.FreeBayCols);
            // Nothing goes on closed floor, and the game says why.
            Assert.AreEqual("That floor is not open yet", s.PlaceBlocked("cafe", 0, 0, 0));
            Assert.IsFalse(s.Place("cafe", 0, 0, 0));
            // Nor half on it: across the hall, or back toward the gates.
            int edge = s.State.bays.Min() / Sim.BayStride * Sim.BayRows;
            Assert.IsNotNull(s.PlaceBlocked("cafe", 0, edge - 1, 0));
            Assert.IsTrue(s.Place("cafe", 0, edge, 0));
            int back = Sim.FreeBayCols * Sim.BayCols;
            Assert.IsNotNull(s.PlaceBlocked("seats", back - 1, edge + 3, 0), "seats two cells deep, one of them in the closed zone");
            // Whatever is placed for you lands on open floor.
            for (int i = 0; i < 12; i++) s.PlaceAnywhere("seats");
            foreach (var p in s.State.terminal) Assert.IsTrue(s.CellOpen(p.x, p.z));
        }

        [Test]
        public void BaysAreBoughtBesideOpenFloorEachDearer()
        {
            var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.State.money = 1e9;
            for (int i = 0; i < 12; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }      // a wider hall: more bays
            Assert.Greater(s.BayCount, Sim.FreeBayRows * Sim.FreeBayCols + 2);
            int open = s.State.bays.Min();
            int far = s.Bays.First(b => !s.BayOpen(b) && !s.BayNeighbours(b).Any(n => s.BayOpen(n)));
            Assert.AreEqual("Open the bay beside it first", s.BuyBayBlocked(far));
            Assert.AreEqual("Already open", s.BuyBayBlocked(open));
            double first = s.BayCost, money = s.State.money;
            int next = s.NextBay;
            Assert.IsTrue(s.BayNeighbours(next).Any(n => s.BayOpen(n)), "the next one is beside open floor");
            Assert.IsTrue(s.BuyBay(next));
            Assert.AreEqual(money - first, s.State.money, 0.5);
            Assert.IsTrue(s.BayOpen(next));
            Assert.Greater(s.BayCost, first * 1.5);
            int x, z, w, d; s.BayRect(next, out x, out z, out w, out d);
            Assert.IsTrue(s.Place("bins", x, z, 0), "its floor can be built on");
            s.State.money = 0;
            StringAssert.StartsWith("Need", s.BuyBayBlocked(s.NextBay));
            // Every bay can be had in the end.
            s.State.money = 1e15;
            while (s.NextBay >= 0) Assert.IsTrue(s.BuyBay(s.NextBay));
            foreach (int b in s.Bays) Assert.IsTrue(s.BayOpen(b));
        }

        [Test]
        public void ASaveFromBeforeBaysKeepsTheFloorItBuiltOn()
        {
            var st = GameState.New();
            st.terminal.Add(new PlacedAmenity { id = "cafe", x = 0, z = 0, rot = 0 });       // at the very end of the hall
            var s = new Sim(st); s.RealNow = () => 1_800_000_000;
            Assert.IsTrue(s.CellOpen(0, 0), "what was built on stays open");
            Assert.AreEqual(0, s.State.terminal[0].z, "and nothing moved");
            Assert.AreEqual(0, s.State.baysBought, "none of it was charged for");
        }

        [Test]
        public void ASaveWithStripBaysKeepsEachStripOpen()
        {
            var st = GameState.New();
            st.baysSet = true; st.bays.Add(1); st.bays.Add(2);      // as strips across the whole floor
            var s = new Sim(st); s.RealNow = () => 1_800_000_000;
            Assert.IsTrue(s.State.baysV2);
            for (int x = 0; x < s.FloorCols; x++) { Assert.IsTrue(s.CellOpen(x, Sim.BayRows)); Assert.IsTrue(s.CellOpen(x, 3 * Sim.BayRows - 1)); }
            Assert.IsFalse(s.CellOpen(0, 0)); Assert.IsFalse(s.CellOpen(0, 3 * Sim.BayRows));
        }

        [Test]
        public void AnOlderSavesFurnitureIsMovedOntoTheFloor()
        {
            var st = GameState.New();
            st.money = 1000;
            // As the long strip had it: far down a floor that no longer reaches there, and two things on one spot.
            st.terminal.Add(new PlacedAmenity { id = "cafe", x = 0, z = 80, rot = 0 });
            st.terminal.Add(new PlacedAmenity { id = "seats", x = 2, z = 3, rot = 0 });
            st.terminal.Add(new PlacedAmenity { id = "bins", x = 2, z = 3, rot = 0 });
            var s = new Sim(st); s.RealNow = () => 1_800_000_000;
            Assert.AreEqual(3, s.State.terminal.Count);
            foreach (var p in s.State.terminal)
                Assert.IsTrue(s.FloorFree(Sim.AmenityById(p.id), p.x, p.z, p.rot, p), p.id + " lies on free floor");
            Assert.AreEqual(1000, s.State.money, "nothing had to be sold");
            Assert.AreEqual(0, s.RefitTerminal(), "and it stays put from then on");
        }

        [Test]
        public void EachOneCostsMoreAndSellsForHalf()
        {
            var s = Fresh();
            double first = s.AmenityCost("cafe");
            s.PlaceAnywhere("cafe");
            Assert.Greater(s.AmenityCost("cafe"), first);
            double before = s.State.money;
            var p = s.State.terminal[0];
            Assert.AreEqual(System.Math.Floor(first * 0.5), s.SellValue(p));
            Assert.IsTrue(s.SellAmenity(p));
            Assert.AreEqual(before + System.Math.Floor(first * 0.5), s.State.money, 1e-6);
            Assert.AreEqual(first, s.AmenityCost("cafe"));
        }

        [Test]
        public void MovingIsFreeAndUpgradesGiveMore()
        {
            var s = Fresh();
            s.Place("toilets", 0, 0, 0);
            var p = s.State.terminal[0];
            double money = s.State.money;
            Assert.IsTrue(s.MoveAmenity(p, 4, 6, 1));
            Assert.AreEqual(money, s.State.money);
            float one = s.Supply(Want.Toilets);
            Assert.IsTrue(s.UpgradeAmenity(p));
            Assert.AreEqual(one * 2, s.Supply(Want.Toilets), 1e-4);
            Assert.IsTrue(s.UpgradeAmenity(p));
            Assert.IsFalse(s.UpgradeAmenity(p), "three levels");
        }

        [Test]
        public void EveryWantHasSomethingThatAnswersItAndFoodEarns()
        {
            foreach (Want w in System.Enum.GetValues(typeof(Want))) Assert.IsTrue(Sim.Amenities.Any(a => a.Gives == w), w.ToString());
            Assert.AreEqual(Sim.Amenities.Count, Sim.Amenities.Select(a => a.Id).Distinct().Count());
            var s = Fresh();
            var route = Content.Routes["puffin_isles"];
            double before = s.FlightValue(route);
            s.PlaceAnywhere("cafe"); s.PlaceAnywhere("gifts");
            Assert.Greater(s.FlightValue(route), before, "passengers spend in the shops");
            Assert.Greater(s.RetailLevel, 0);
        }

        [Test]
        public void TheFloorFillsUpAndTheSaveKeepsIt()
        {
            var s = Fresh();
            int n = 0;
            while (n < 500) { s.State.money = 1e300; if (!s.PlaceAnywhere("dutyfree")) break; n++; }
            Assert.Greater(n, 3);
            Assert.IsNull(s.FindSpot("dutyfree"));
            s.State.terminal.Add(new PlacedAmenity { id = "gone" });
            s.State.Repair();
            Assert.AreEqual(n, s.State.terminal.Count);
        }
    }
}
