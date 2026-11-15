using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Your airline's paint: patterns and emblems earned by stars and fleet size, the trim, and the name.</summary>
    public class LiveryTests
    {
        static Sim Airport(int stars)
        {
            var st = GameState.New(); st.money = 1e12; st.ratingXp = Content.StarXp[stars];
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            return s;
        }

        static int Index(LiveryItem[] items, string id) { return System.Array.FindIndex(items, x => x.Id == id); }

        [Test]
        public void ThereIsEnoughToChooseFrom()
        {
            Assert.GreaterOrEqual(Sim.LiveryPatterns.Length, 5);
            Assert.GreaterOrEqual(Sim.Emblems.Length - 1, 5, "five shapes besides none");
            Assert.AreEqual(Sim.Liveries.Length + 1, Sim.Trims.Length, "the palette plus the pattern's own match");
            foreach (var items in new[] { Sim.LiveryPatterns, Sim.Emblems })
                for (int i = 0; i < items.Length; i++)
                    for (int j = i + 1; j < items.Length; j++) Assert.AreNotEqual(items[i].Id, items[j].Id);
        }

        [Test]
        public void ANewOrOldSaveHasTheClassicLook()
        {
            var st = GameState.New();
            Assert.AreEqual(0, st.liveryPattern); Assert.AreEqual(0, st.liveryTrim); Assert.AreEqual(0, st.emblem);
            Assert.AreEqual("", st.airlineName);
            // A save from before liveries had patterns: none of the fields, and the name missing altogether.
            st.airlineName = null; st.liveryPattern = 99; st.liveryTrim = -3; st.emblem = 42;
            var s = new Sim(st);
            Assert.AreEqual("classic", s.Pattern.Id);
            Assert.AreEqual("none", s.Emblem.Id);
            Assert.AreEqual(-1, Sim.TrimColour(s.State.liveryTrim), "matched trim");
            Assert.AreEqual(Sim.DefaultAirlineName, s.AirlineName);
        }

        [Test]
        public void TheFirstTwoAreFreeAndTheRestOpenWithStars()
        {
            var s = Airport(1);
            Assert.AreEqual(2, s.OpenCount(false)); Assert.AreEqual(2, s.OpenCount(true));
            Assert.IsNull(s.PatternBlocked(0)); Assert.IsNull(s.PatternBlocked(1));
            Assert.IsNull(s.EmblemBlocked(0)); Assert.IsNull(s.EmblemBlocked(1));
            int cheat = Index(Sim.LiveryPatterns, "cheatline"), two = Index(Sim.LiveryPatterns, "twotone"), ring = Index(Sim.Emblems, "ring");
            Assert.AreEqual("Opens at 2 stars", s.PatternBlocked(cheat));
            s.State.ratingXp = Content.StarXp[2];
            Assert.IsNull(s.PatternBlocked(cheat));
            Assert.IsNotNull(s.PatternBlocked(two));
            Assert.IsNotNull(s.EmblemBlocked(ring));
            s.State.ratingXp = Content.StarXp[5];
            for (int i = 0; i < Sim.LiveryPatterns.Length; i++) if (Sim.LiveryPatterns[i].Fleet == 0) Assert.IsNull(s.PatternBlocked(i), Sim.LiveryPatterns[i].Id);
            for (int i = 0; i < Sim.Emblems.Length; i++) if (Sim.Emblems[i].Fleet == 0) Assert.IsNull(s.EmblemBlocked(i), Sim.Emblems[i].Id);
            Assert.AreEqual("Unknown", s.PatternBlocked(-1)); Assert.AreEqual("Unknown", s.EmblemBlocked(Sim.Emblems.Length));
        }

        [Test]
        public void FleetItemsOpenWithAircraftAndStayOpen()
        {
            var s = Airport(5);
            int full = Index(Sim.LiveryPatterns, "full"), wing = Index(Sim.Emblems, "wing");
            Assert.AreEqual("Opens with 5 aircraft", s.PatternBlocked(full));
            Assert.IsNotNull(s.EmblemBlocked(wing));
            for (int i = 0; i < 2; i++) Assert.IsTrue(s.BuyAircraft(AircraftClass.A), s.BuyAircraftBlocked(AircraftClass.A));
            Assert.IsNull(s.EmblemBlocked(wing));
            Assert.IsNotNull(s.PatternBlocked(full));
            for (int i = 0; i < 3; i++) Assert.IsTrue(s.BuyAircraft(AircraftClass.A), s.BuyAircraftBlocked(AircraftClass.A));
            Assert.AreEqual(5, s.FleetSize);
            Assert.IsNull(s.PatternBlocked(full));
            Assert.IsTrue(s.SetPattern(full));

            // Selling up leaves the fleet behind and the rating at one star; what was earned stays earned.
            s.State.lifetimeEarned = 1e12;
            var next = Content.Maps.Find(m => m.Id != s.State.map && s.SellBlocked(m.Id) == null);
            Assert.IsNotNull(next);
            Assert.IsTrue(s.SellAndMove(next.Id));
            Assert.AreEqual(0, s.FleetSize); Assert.AreEqual(1, s.Stars);
            Assert.IsNull(s.PatternBlocked(full)); Assert.IsNull(s.EmblemBlocked(wing));
            Assert.IsNull(s.PatternBlocked(Index(Sim.LiveryPatterns, "swoosh")), "the top rating was reached to sell");
            Assert.AreEqual(full, s.State.liveryPattern, "the paint stays with you");
        }

        [Test]
        public void AnOldSaveWithAFleetRemembersItsSize()
        {
            var st = GameState.New();
            st.fleet.Add(new LevelEntry { id = "a", level = 3 }); st.fleet.Add(new LevelEntry { id = "c", level = 2 });
            var s = new Sim(st);
            Assert.AreEqual(5, s.State.fleetBest);
        }

        [Test]
        public void SettersRefuseWhatIsLocked()
        {
            var s = Airport(1);
            int swoosh = Index(Sim.LiveryPatterns, "swoosh"), leaf = Index(Sim.Emblems, "leaf");
            Assert.IsFalse(s.SetPattern(swoosh)); Assert.AreEqual(0, s.State.liveryPattern);
            Assert.IsFalse(s.SetEmblem(leaf)); Assert.AreEqual(0, s.State.emblem);
            Assert.IsFalse(s.SetPattern(-1)); Assert.IsFalse(s.SetPattern(Sim.LiveryPatterns.Length));
            Assert.IsFalse(s.SetEmblem(-1));
            Assert.IsTrue(s.SetPattern(1)); Assert.AreEqual(1, s.State.liveryPattern);
            Assert.IsTrue(s.SetEmblem(1)); Assert.AreEqual(1, s.State.emblem);
            s.State.ratingXp = Content.StarXp[5];
            Assert.IsTrue(s.SetPattern(swoosh)); Assert.IsTrue(s.SetEmblem(leaf));
        }

        [Test]
        public void CyclingSkipsWhatIsLocked()
        {
            var s = Airport(1);
            Assert.AreEqual(1, s.NextPattern());
            s.SetPattern(1);
            Assert.AreEqual(0, s.NextPattern(), "wraps past the shut ones");
            s.State.ratingXp = Content.StarXp[2];
            Assert.AreEqual(Index(Sim.LiveryPatterns, "cheatline"), s.NextPattern());
            Assert.AreEqual(1, s.NextEmblem());
            s.SetEmblem(1);
            Assert.AreEqual(Index(Sim.Emblems, "chevron"), s.NextEmblem());
        }

        [Test]
        public void TrimIsOpenAndWraps()
        {
            var s = Airport(1);
            s.SetTrim(3); Assert.AreEqual(3, s.State.liveryTrim); Assert.AreEqual(2, Sim.TrimColour(3));
            Assert.AreEqual(Sim.Liveries[2], s.TrimName);
            s.SetTrim(Sim.Trims.Length); Assert.AreEqual(0, s.State.liveryTrim); Assert.AreEqual("Matched", s.TrimName);
            s.SetTrim(-1); Assert.AreEqual(Sim.Trims.Length - 1, s.State.liveryTrim);
        }

        [Test]
        public void TheAirlineCanBeNamedButNotRudely()
        {
            var s = Airport(1);
            Assert.AreEqual("Your airline", s.AirlineName);
            Assert.IsTrue(s.RenameAirline("  Gull   Air  "));
            Assert.AreEqual("Gull Air", s.AirlineName);
            Assert.IsFalse(s.RenameAirline("Sh1t Airways")); Assert.AreEqual("Gull Air", s.AirlineName, "a refused name changes nothing");
            Assert.IsFalse(s.RenameAirline("<<>>")); Assert.AreEqual("Gull Air", s.AirlineName);
            Assert.IsTrue(s.RenameAirline("Northern Lights Airways International"));
            Assert.LessOrEqual(s.AirlineName.Length, Sim.AirlineNameMax);
            Assert.IsTrue(s.RenameAirline("")); Assert.AreEqual(Sim.DefaultAirlineName, s.AirlineName);
            Assert.AreEqual("", s.State.airlineName);
        }
    }
}
