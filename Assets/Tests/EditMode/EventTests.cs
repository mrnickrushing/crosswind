using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Weeks that change the airport: Storm Season and Cargo Surge, and a week's event kept through an update.</summary>
    public class EventTests
    {
        double _clock;

        /// <summary>An airport whose real clock sits in the week of the given event.</summary>
        Sim InWeekOf(string id, GameState st = null)
        {
            var clock = new double[] { 1_800_000_000 };       // each airport its own clock
            var s = new Sim(st ?? GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => clock[0];
            for (int i = 0; i < 20 && s.CurrentEvent.Id != id; i++) clock[0] += 7 * 86400;
            Assert.AreEqual(id, s.CurrentEvent.Id);
            _clock = clock[0];
            return s;
        }

        static void Run(Sim s, float seconds)
        {
            for (float t = 0; t < seconds; t += 0.5f)
            {
                foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
                s.Tick(0.5f);
            }
        }

        [Test]
        public void TheRotationHasWeeksThatChangeTheAirport()
        {
            Assert.IsNotNull(Sim.EventById("storm"));
            Assert.IsNotNull(Sim.EventById("cargo"));
            Assert.Greater(Sim.EventById("storm").Storm, 0);
            Assert.Greater(Sim.EventById("cargo").CargoPay, 1);
            foreach (var e in Sim.Events.Where(e => e.Id != "storm" && e.Id != "cargo"))
            {
                Assert.AreEqual(0, e.Storm); Assert.AreEqual(1, e.CargoPay); Assert.AreEqual(0, e.FreightOdds);
            }
        }

        [Test]
        public void AWeekUnderWayInAnOlderSaveKeepsItsEvent()
        {
            var probe = InWeekOf("storm");
            int week = probe.Week;
            // A save from the four-event rotation, mid-week: its event was the legacy one for that week.
            var st = GameState.New(); st.eventWeek = week; st.eventId = ""; st.eventProgress = 77;
            var s = new Sim(st); s.RealNow = () => _clock;
            string legacy = new[] { "rush", "clockwork", "boom", "gold" }[((week % 4) + 4) % 4];
            Assert.AreEqual(legacy, s.CurrentEvent.Id, "the week finishes as it began");
            Assert.AreEqual(77, s.State.eventProgress);
            _clock += 7 * 86400;
            s.Tick(0.1f);
            Assert.AreNotEqual(week, s.State.eventWeek, "next week the new rotation takes over");
        }

        [Test]
        public void StormSeasonBringsRoughWeatherAndCountsFlightsThroughIt()
        {
            float RoughShare(Sim s)
            {
                s.State.money = 1e6; int rough = 0, n = 0;
                for (float t = 0; t < 4 * 3600; t += 5) { s.Tick(5); n++; if (s.Weather == Weather.Storm || s.Weather == Weather.Fog) rough++; }
                return rough / (float)n;
            }
            var storm = InWeekOf("storm"); var plain = InWeekOf("rush");
            Assert.Greater(RoughShare(storm), RoughShare(plain) + 0.1f, "storms and fog more of the time");

            var s = InWeekOf("storm");
            Run(s, 900);
            s.State.weather = (int)Weather.Rain; s.State.weatherUntil = s.State.time + 3600;
            double before = s.State.eventProgress;
            Run(s, 600);
            Assert.Greater(s.State.eventProgress, before, "flights away on time in the rain count");
            Assert.IsNotEmpty(s.EventPrep, "and the card says what to have ready");
        }

        [Test]
        public void CargoSurgePaysCargoHalfAgainAndCountsIt()
        {
            var s = InWeekOf("cargo"); var plain = InWeekOf("rush");
            var r = Content.Routes.Values.First(x => x.Airline != "own");
            var a = new double[5]; var b = new double[5];
            s.FlightValue(r, true, a); plain.FlightValue(r, true, b);
            Assert.AreEqual(0, b[4], "no cargo terminal, no cargo, on an ordinary week");
            Assert.Greater(a[4], 0, "but every flight carries some in a surge");
            Assert.AreEqual(b[0], a[0], 1e-9, "and nothing else changes");
            // With a terminal, its levels and the surge's add up, all paying half again.
            foreach (var x in new[] { s, plain }) { x.State.money = 1e12; x.State.ratingXp = Content.StarXp[Content.MaxStars]; Assert.IsTrue(x.Upgrade("cargo"), x.UpgradeBlocked("cargo")); }
            s.FlightValue(r, true, a); plain.FlightValue(r, true, b);
            Assert.AreEqual(b[4] * 2 * 1.5, a[4], 1e-6);
            Assert.AreEqual(1, plain.EventCargoPay); Assert.AreEqual(0, plain.EventCargoLevel);
            Run(s, 900);
            Assert.Greater(s.State.eventProgress, 0, "cargo income counts toward it");
            Assert.IsNotEmpty(s.EventPrep);
        }
    }
}
