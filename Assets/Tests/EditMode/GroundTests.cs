using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>What happens between the last passenger boarding and the wheels leaving the stand.</summary>
    public class GroundTests
    {
        static Sim Airport(int stands, params string[] routes)
        {
            var st = GameState.New();
            st.contracts.Clear();
            st.money = 1e12;
            st.ratingXp = Content.StarXp[6];
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            while (s.Level("stands") < stands) s.Upgrade("stands");
            while (s.Level("runway") < 10) s.Upgrade("runway");
            foreach (var f in Content.Departments) if (f != "deice") { while (s.Level(f) < 4) s.Upgrade(f); }
            while (s.Level("tower") < 4) s.Upgrade("tower");
            foreach (var f in new[] { "checkin", "security", "gates" }) while (s.Level(f) < 12) s.Upgrade(f);
            foreach (var r in routes) Assert.IsTrue(s.Sign(r), s.SignBlocked(r));
            return s;
        }

        static void Run(Sim s, float seconds, bool tap = true)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                if (tap) foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
        }

        [Test]
        public void JetsNeedATugAndPropsDoNot()
        {
            Assert.Contains(Service.Pushback, Content.Routes["zip_capital"].Needs);
            Assert.IsFalse(Content.Routes["puffin_isles"].Needs.Contains(Service.Pushback));
            var st = GameState.New(); st.ratingXp = Content.StarXp[4]; st.money = 1e9;
            var s = new Sim(st) { BuildTimeScale = 0 };
            while (s.Level("runway") < 6) s.Upgrade("runway");
            s.Upgrade("baggage");
            StringAssert.Contains("Pushback", s.SignBlocked("zip_capital"));
            s.Upgrade("pushback");
            Assert.IsNull(s.SignBlocked("zip_capital"));
        }

        [Test]
        public void AJetWaitsBoardedUntilItsTugIsSent()
        {
            var s = Airport(1, "zip_capital");
            // Everything but the tug is tapped.
            for (float t = 0; t < 200; t += 0.25f)
            {
                foreach (var (f, j) in s.Waiting.ToList()) if (j.service != Service.Pushback) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
            Assert.AreEqual(0, s.State.flightsHandled, "nothing leaves without a tug");
            var parked = s.State.flights.Single(f => f.stand >= 0);
            Assert.AreEqual(Phase.Board, parked.phase);
            Assert.IsTrue(parked.boarded);
            Assert.AreEqual(Service.Pushback, s.Waiting.Single().job.service);
            Run(s, 120);
            Assert.Greater(s.State.flightsHandled, 0);
        }

        [Test]
        public void ATugManagerSendsItByItself()
        {
            var s = Airport(2, "zip_capital");
            foreach (var f in new[] { "fuel", "baggage", "pushback" }) Assert.IsTrue(s.Hire(f), s.HireBlocked(f));
            Run(s, 300, tap: false);
            Assert.Greater(s.State.flightsHandled, 2);
            Assert.Greater(s.IdleRate(), 0);
        }

        [Test]
        public void FinishedFlightsKeepTheirGateWhileStandsAreSpare()
        {
            var s = Airport(6, "puffin_isles");
            Run(s, 400);
            // One quiet route on six stands: without the hold the apron would be all but empty.
            int samples = 0; double parked = 0;
            for (int i = 0; i < 400; i++) { Run(s, 1); samples++; parked += s.State.flights.Count(f => f.stand >= 0 && f.phase != Phase.TaxiOut && f.phase != Phase.Landing && f.phase != Phase.TaxiIn); }
            Assert.Greater(parked / samples, 0.45, "an aircraft at a gate most of the time");
            Assert.Greater(s.State.onTime, s.State.late * 5, "waiting at the gate by choice is not lateness");
        }

        [Test]
        public void HoldingNeverCostsFlights()
        {
            // A busy apron: the same traffic with and without the hold handles as many flights.
            int Handled(bool hold)
            {
                var s = Airport(3, "puffin_isles", "puffin_coast", "fjord_sound", "zip_north", "sunhop_bay");
                foreach (var f in new[] { "fuel", "baggage" }) s.Hire(f);
                for (float t = 0; t < 1800; t += 0.25f)
                {
                    if (!hold) foreach (var f in s.State.flights) f.held = 9999;
                    s.Tick(0.25f);
                }
                return s.State.flightsHandled;
            }
            int with = Handled(true), without = Handled(false);
            TestContext.WriteLine(with + " with the hold, " + without + " without");
            Assert.GreaterOrEqual(with, without * 0.97);
        }

        [Test]
        public void SnowBringsTheSprayOnlyWhereThereIsAPad()
        {
            var s = Airport(1, "puffin_isles");
            StringAssert.Contains("never snows", s.UpgradeBlocked("deice") ?? "");
            s.State.map = Content.Maps.First(m => m.Snow > 0).Id;
            s.State.weather = (int)Weather.Snow; s.State.weatherUntil = s.State.time + 1e6;
            float bare = s.WeatherFactor;
            Run(s, 60);
            Assert.IsFalse(s.State.flights.Any(f => f.jobs.Any(j => j.service == Service.Deice)));
            Assert.IsNull(s.UpgradeBlocked("deice"));
            s.Upgrade("deice");
            Assert.Greater(s.WeatherFactor, bare, "a pad keeps the runway moving in snow");
            bool sprayed = false;
            for (int i = 0; i < 400 && !sprayed; i++) { Run(s, 1); sprayed = s.State.flights.Any(f => f.jobs.Any(j => j.service == Service.Deice)); }
            Assert.IsTrue(sprayed);
            int before = s.State.flightsHandled;
            Run(s, 200);
            Assert.Greater(s.State.flightsHandled, before);
            Assert.AreEqual(0, s.State.late, "the weather's delay is not counted against the airport");
        }

        [Test]
        public void AnOlderSaveKeepsFlyingItsJets()
        {
            var st = GameState.New();
            st.contracts.Add(new ContractState { route = "zip_capital", nextArrival = 70 });
            var s = new Sim(st);
            Assert.AreEqual(1, s.Level("pushback"));
        }
    }
}
