using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class CrewTests
    {
        static Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; s.BuildTimeScale = 0; return s; }

        [Test]
        public void TheOddsAddUpAndEveryRarityHasSomeone()
        {
            Assert.AreEqual(100, Sim.CrateOdds.Sum());
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity))) Assert.IsTrue(Sim.Crew.Any(c => c.Rarity == r), r.ToString());
            Assert.AreEqual(Sim.Crew.Count, Sim.Crew.Select(c => c.Id).Distinct().Count());
        }

        [Test]
        public void ANewGameStartsWithOneCrateAndTheFirstPersonGoesOnDuty()
        {
            var s = Fresh();
            Assert.AreEqual(1, s.State.crates);
            var got = s.OpenCrate();
            Assert.IsNotNull(got);
            Assert.IsTrue(got.New);
            Assert.AreEqual(1, got.Level);
            Assert.IsTrue(s.OnDuty(got.Who.Id));
            Assert.IsNull(s.OpenCrate(), "no crates left");
        }

        [Test]
        public void CopiesLevelSomeoneUp()
        {
            Assert.AreEqual(0, Sim.LevelFor(0));
            Assert.AreEqual(1, Sim.LevelFor(1));
            Assert.AreEqual(1, Sim.LevelFor(2));
            Assert.AreEqual(2, Sim.LevelFor(3));
            Assert.AreEqual(3, Sim.LevelFor(7));
            Assert.AreEqual(5, Sim.LevelFor(31));
            Assert.AreEqual(5, Sim.LevelFor(500));
        }

        [Test]
        public void OnlyThoseOnDutyCountAndPlacesAreLimited()
        {
            var s = Fresh();
            foreach (var id in new[] { "flint", "dot", "gus", "marnie" }) s.State.crew.Add(new LevelEntry { id = id, level = 3 });
            double before = s.IncomeScale;
            Assert.IsTrue(s.ToggleDuty("flint"));
            Assert.AreEqual(before * (1 + 0.06 * 2), s.IncomeScale, 1e-6, "level 2 chairman");
            Assert.IsTrue(s.ToggleDuty("dot")); Assert.IsTrue(s.ToggleDuty("gus"));
            Assert.IsFalse(s.ToggleDuty("marnie"), "three places");
            Assert.IsNotNull(s.DutyBlocked("marnie"));
            Assert.IsTrue(s.ToggleDuty("flint"), "resting");
            Assert.AreEqual(before, s.IncomeScale, 1e-6);
            Assert.IsTrue(s.ToggleDuty("marnie"));
            Assert.IsNotNull(s.DutyBlocked("quill"), "not on the team");
        }

        [Test]
        public void AnEpicIsCertainByTheTenthCrateAndTheOddsHold()
        {
            var s = Fresh();
            int[] seen = new int[4]; int sinceEpic = 0, worst = 0;
            for (int i = 0; i < 4000; i++)
            {
                s.State.crates = 1;
                var got = s.OpenCrate();
                seen[(int)got.Who.Rarity]++;
                if (got.Who.Rarity >= Rarity.Epic) sinceEpic = 0; else { sinceEpic++; if (sinceEpic > worst) worst = sinceEpic; }
            }
            Assert.Less(worst, Sim.CratePity);
            Assert.Greater(seen[0], seen[1]); Assert.Greater(seen[1], seen[2]); Assert.Greater(seen[2], seen[3]);
            Assert.Greater(seen[3], 0);
            Assert.Greater(s.State.gems, 0, "spares of someone at the top level come back as gems");
        }

        [Test]
        public void CratesComeFreeEveryEightHoursWithStarsAndForGems()
        {
            var s = Fresh(); double now = 1_800_000_000; s.RealNow = () => now;
            Assert.IsTrue(s.TakeFreeCrate());
            Assert.IsFalse(s.TakeFreeCrate());
            now += Sim.FreeCrateHours * 3600 + 1;
            Assert.IsTrue(s.TakeFreeCrate());
            Assert.AreEqual(3, s.State.crates);
            Assert.IsFalse(s.BuyCrate());
            s.State.gems = Sim.CrateGems;
            Assert.IsTrue(s.BuyCrate());
            Assert.AreEqual(0, s.State.gems);
            int had = s.State.crates;
            s.State.ratingXp = Content.StarXp[2] - 1; s.State.money = 1e6; s.Hire("fuel");
            for (int i = 0; i < 4000 && s.Stars < 2; i++) s.Tick(0.25f);
            Assert.AreEqual(2, s.Stars);
            Assert.AreEqual(had + 1, s.State.crates);
        }

        [Test]
        public void TheTeamMovesWithYou()
        {
            var s = Fresh();
            s.OpenCrate();
            string who = s.State.crew[0].id;
            s.State.ratingXp = Content.StarXp[Content.MaxStars]; s.State.lifetimeEarned = 1e12;
            var next = Content.Maps.First(m => m.Id != s.State.map && s.SellBlocked(m.Id) == null);
            Assert.IsTrue(s.SellAndMove(next.Id));
            Assert.AreEqual(1, s.Copies(who));
            Assert.IsTrue(s.OnDuty(who));
            Assert.AreEqual(4, s.DutyPlaces);
        }
    }
}
