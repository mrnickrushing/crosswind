using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Placement has reach: things count fully only where passengers are within walking distance of them.</summary>
    public class CoverageTests
    {
        static Sim Airport()
        {
            var st = GameState.New();
            var s = new Sim(st) { BuildTimeScale = 0 };
            s.RealNow = () => 1_800_000_000;
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[4];
            for (int i = 0; i < 8; i++) { s.Upgrade("checkin"); s.Upgrade("security"); s.Upgrade("gates"); }
            while (s.NextBay >= 0) s.BuyBay(s.NextBay);       // the whole floor open: room to spread out
            return s;
        }

        [Test]
        public void ANewAirportHasNoGraceAndAnOldLayoutDoes()
        {
            var fresh = new Sim(GameState.New());
            Assert.IsTrue(fresh.State.reachSet);
            Assert.AreEqual(0, fresh.ReachGrace, "nothing placed before the rule: nothing to be gentle with");

            // A save from before reach counted, with a layout on its floor.
            var st = GameState.New(); st.reachSet = false; st.reachGraceUntil = 0;
            st.terminal.Add(new PlacedAmenity { id = "toilets", x = 0, z = st.bays.Count > 0 ? 0 : 0 });
            var old = new Sim(st); double clock = 1_800_000_000; old.RealNow = () => clock;
            Assert.AreEqual(1, old.ReachGrace, "full grace until the clock is known");
            old.Tick(0.1f);
            Assert.AreEqual(clock + Sim.ReachGraceHours * 3600, old.State.reachGraceUntil, 1);
            clock += Sim.ReachGraceHours * 1800;
            Assert.AreEqual(0.5f, old.ReachGrace, 0.01f, "it fades with real time");
            clock += Sim.ReachGraceHours * 1800 + 1;
            old.Tick(0.1f);
            Assert.AreEqual(0, old.ReachGrace);
            Assert.AreEqual(0, old.State.reachGraceUntil, "and then it's over for good");
        }

        [Test]
        public void SpreadOutIsBetterThanHuddledInACorner()
        {
            // The same six toilets: all in one corner, then spread by the game.
            var huddled = Airport(); var spread = Airport();
            int placed = 0;
            for (int x = 0; x < 6 && placed < 6; x += 2)
                for (int z = 0; z < 4 && placed < 6; z += 2)
                    if (huddled.Place("toilets", x, z, 0)) placed++;
            Assert.AreEqual(6, placed, "six fit in the corner");
            for (int i = 0; i < 6; i++) Assert.IsTrue(spread.PlaceAnywhere("toilets"));
            Assert.AreEqual(huddled.Supply(Want.Toilets), spread.Supply(Want.Toilets), "the same things, worth the same in total");
            Assert.Less(huddled.Coverage(Want.Toilets), 0.5f, "a corner serves the corner");
            Assert.Greater(spread.Coverage(Want.Toilets), huddled.Coverage(Want.Toilets) + 0.3f);
            Assert.Greater(spread.ReachedSupply(Want.Toilets), huddled.ReachedSupply(Want.Toilets));
            Assert.Greater(spread.Satisfaction, huddled.Satisfaction - 1e-4f);
            // Even huddled, nothing is worth less than the floor.
            Assert.GreaterOrEqual(huddled.CoverFactor(Want.Toilets), Sim.CoverFloor);
        }

        [Test]
        public void ReachGrowsWithLevelAndBigItemsReachFurther()
        {
            var s = Airport();
            Assert.IsTrue(s.Place("seats", 10, 10, 0));
            var p = s.State.terminal.Last();
            int x0, z0, x1, z1; s.ReachRect(p, Want.Seating, out x0, out z0, out x1, out z1);
            Assert.AreEqual(10 - Sim.Reach(Want.Seating), x0);
            float before = s.Coverage(Want.Seating);
            Assert.IsTrue(s.UpgradeAmenity(p));
            s.ReachRect(p, Want.Seating, out x0, out z0, out x1, out z1);
            Assert.AreEqual(10 - Sim.Reach(Want.Seating) - 1, x0, "a level adds a cell all round");
            Assert.Greater(s.Coverage(Want.Seating), before);
            Assert.Greater(Sim.Reach(Want.Food), Sim.Reach(Want.Seating), "people walk further for food than for a bench");
        }

        [Test]
        public void TheGameSuggestsTheSpotThatReachesMost()
        {
            var s = Airport();
            Assert.IsTrue(s.PlaceAnywhere("toilets"));
            var first = s.State.terminal.Last();
            var next = s.BestSpot("toilets");
            Assert.IsNotNull(next);
            Assert.Greater(System.Math.Max(System.Math.Abs(next.x - first.x), System.Math.Abs(next.z - first.z)), Sim.Reach(Want.Toilets),
                "the second goes where the first doesn't reach");
            Assert.Greater(s.CoverageGain("toilets", next.x, next.z, next.rot), s.CoverageGain("toilets", first.x, first.z + 2, 0));
            // Something with no reach (the lounge) goes where it fits.
            var lounge = s.BestSpot("lounge"); var fits = s.FindSpot("lounge");
            Assert.AreEqual(fits.x, lounge.x); Assert.AreEqual(fits.z, lounge.z);
        }

        [Test]
        public void FencedFloorNeedsNothing()
        {
            // Only open floor is counted: a new airport's core covered from its middle is covered.
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 }; s.RealNow = () => 1_800_000_000; s.State.money = 1e9; s.State.ratingXp = Content.StarXp[3];
            Assert.AreEqual(0, s.Coverage(Want.Food), "nothing placed reaches nobody (and scales nothing: there is nothing to scale)");
            for (int i = 0; i < 3; i++) Assert.IsTrue(s.PlaceAnywhere("cafe"));
            Assert.Greater(s.Coverage(Want.Food), 0.95f, "three coffee bars reach every open cell of the starting core");
        }

        [Test]
        public void CoverageIsCachedButFollowsEveryChange()
        {
            var s = Airport();
            Assert.IsTrue(s.Place("bins", 0, 0, 0));
            float a = s.Coverage(Want.Clean);
            var p = s.State.terminal.Last();
            Assert.IsTrue(s.MoveAmenity(p, 20, 20, 0));
            Assert.AreNotEqual(a, s.Coverage(Want.Clean), "a move changes what is in reach");
            Assert.IsTrue(s.SellAmenity(p));
            Assert.AreEqual(0, s.Coverage(Want.Clean), "nothing left to reach anyone");
        }
    }
}
