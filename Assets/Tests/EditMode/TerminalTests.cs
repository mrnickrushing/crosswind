using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class TerminalTests
    {
        static Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0; s.State.money = 1e9; s.State.ratingXp = Content.StarXp[5]; return s; }

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
            Assert.GreaterOrEqual(w0 / d0, 0.9f);
            // Wider with what stands across it.
            for (int i = 0; i < 12; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            Assert.Greater(s.TerminalLength, w0);
            Assert.GreaterOrEqual(s.TerminalLength, s.SecurityLanes * 4.6f - 2);
            Assert.GreaterOrEqual(s.TerminalLength, (s.CheckinDesks + 5) / 6 * 13.4f);
            // Deeper with the rating.
            s.State.ratingXp = Content.StarXp[6];
            Assert.AreEqual(Sim.MinFloorCols + 5, s.FloorCols);
            Assert.Greater(s.TerminalDepth, d0);
            // At its largest it is still under two to one.
            s.State.ratingXp = Content.StarXp[Content.MaxStars];
            for (int i = 0; i < 40; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            Assert.AreEqual(Sim.MaxFloorCols, s.FloorCols);
            Assert.LessOrEqual(s.TerminalLength, Sim.MaxTerminalWidth);
            Assert.LessOrEqual(s.TerminalLength / s.TerminalDepth, 2f);
            Assert.AreEqual(s.FloorRows * Sim.Cell, s.TerminalLength - 4, 0.01f, "the floor runs the width of the hall, in whole cells");
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
            s.State.money = 1e15;
            int n = 0;
            while (s.PlaceAnywhere("dutyfree") && n < 500) n++;
            Assert.Greater(n, 3);
            Assert.IsNull(s.FindSpot("dutyfree"));
            s.State.terminal.Add(new PlacedAmenity { id = "gone" });
            s.State.Repair();
            Assert.AreEqual(n, s.State.terminal.Count);
        }
    }
}
