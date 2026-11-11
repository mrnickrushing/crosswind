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
            Assert.IsNotNull(s.PlaceBlocked("seats", Sim.FloorCols - 1, 0, 0), "hangs off the edge");
            Assert.IsTrue(s.Place("seats", Sim.FloorCols - 1, 0, 1), "turned, it fits");
            Assert.IsFalse(s.Place("cafe", 0, s.FloorRows - 1, 0), "off the end");
            Assert.AreEqual(2, s.State.terminal.Count);
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
