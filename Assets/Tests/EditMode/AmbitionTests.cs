using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Ambitions: big targets with their needs in sight, reached the moment they happen, claimed once per airport.</summary>
    public class AmbitionTests
    {
        static Sim Airport()
        {
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => 1_800_000_000;
            return s;
        }

        [Test]
        public void AJumboSaysWhatItStillNeeds()
        {
            var s = Airport();
            var jumbo = Sim.AmbitionById("first_jumbo");
            var steps = s.AmbitionSteps(jumbo);
            Assert.IsNotEmpty(steps);
            Assert.IsTrue(steps.Any(x => x.Facility == "runway" && !x.Ready), "a longer runway first");
            Assert.IsTrue(steps.Any(x => x.Facility == "heavy"), "and a stand for it");
            Assert.IsTrue(steps.Any(x => x.Text.Contains("star")));
            Assert.IsFalse(s.AmbitionDone(jumbo));
        }

        [Test]
        public void TheFirstLandingOfAClassIsAnAmbitionReachedOnce()
        {
            var s = Airport();
            var reached = new System.Collections.Generic.List<string>();
            s.AmbitionReached += a => reached.Add(a.Id);
            // A jet route on the books, whatever it would need to be signed.
            var jet = Content.Routes.Values.First(r => r.Class == AircraftClass.C && r.Airline != "own");
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[Content.MaxStars];
            foreach (var f in s.Missing(jet).ToList()) while (s.Missing(jet).Contains(f) && s.Upgrade(f)) { }
            s.State.contracts.Clear();
            s.State.contracts.Add(new ContractState { route = jet.Id });
            for (float t = 0; t < 600 && reached.Count == 0; t += 0.5f) { foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service); s.Tick(0.5f); }
            CollectionAssert.Contains(reached, "first_jet");
            Assert.AreEqual(1, reached.Count(x => x == "first_jet"), "said once");
            int gems = s.State.gems;
            Assert.AreEqual(10, s.ClaimAmbition("first_jet"));
            Assert.AreEqual(gems + 10, s.State.gems);
            Assert.AreEqual(0, s.ClaimAmbition("first_jet"), "and paid once");
        }

        [Test]
        public void SixStandsFillTheApron()
        {
            var s = Airport();
            AmbitionDef got = null; s.AmbitionReached += a => got = a;
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[Content.MaxStars];
            while (s.StandCount < Sim.MaxStands) Assert.IsTrue(s.Upgrade("stands"), s.UpgradeBlocked("stands"));
            Assert.AreEqual("full_apron", got?.Id);
        }

        [Test]
        public void APinnedAmbitionStaysUntilItIsClaimed()
        {
            var s = Airport();
            s.Pin("first_widebody");
            Assert.AreEqual("first_widebody", s.Pinned.Id);
            s.Pin("first_widebody");
            Assert.IsNull(s.Pinned, "pinning it again unpins it");
            s.Pin("full_apron");
            s.State.ambitionsDone.Add(s.State.map + ":full_apron");
            s.ClaimAmbition("full_apron");
            Assert.IsNull(s.Pinned, "claimed: off the HUD");
        }

        [Test]
        public void AnOlderAirportCountsWhatItHasDoneQuietlyAndANewOneStartsAfresh()
        {
            var st = GameState.New();
            st.flightsHandled = 50;
            for (int i = 0; i < 6; i++) { var e = st.levels.Find(l => l.id == "stands"); if (e == null) st.levels.Add(new LevelEntry { id = "stands", level = 6 }); else e.level = 6; }
            var s = new Sim(st); s.RealNow = () => 1_800_000_000;
            Assert.IsTrue(s.AmbitionDone(Sim.AmbitionById("full_apron")), "already true: counted");
            Assert.IsFalse(s.AmbitionClaimed(Sim.AmbitionById("full_apron")), "its reward still to collect");
            Assert.Greater(s.AmbitionsWaiting, 0);
            // A different airport keeps its own ambitions.
            s.State.map = "alpine"; s.State.classesHere.Clear();
            Assert.IsFalse(s.AmbitionDone(Sim.AmbitionById("full_apron")));
        }
    }
}
