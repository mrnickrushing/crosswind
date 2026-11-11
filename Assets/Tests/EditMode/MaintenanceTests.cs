using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Checks on your own fleet.</summary>
    public class MaintenanceTests
    {
        static Sim Airline()
        {
            var st = GameState.New(); st.money = 1e9; st.ratingXp = Content.StarXp[5]; st.contracts.Clear();
            var s = new Sim(st) { BuildTimeScale = 0 };
            double clock = 1_800_000_000; s.RealNow = () => clock;
            Assert.IsTrue(s.BuyAircraft(AircraftClass.A), s.BuyAircraftBlocked(AircraftClass.A));
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

        [Test]
        public void FlightsPutHoursOnTheTypeUntilItsCheckIsDue()
        {
            var s = Airline();
            Assert.AreEqual(0, s.CheckProgress(AircraftClass.A));
            StringAssert.Contains("Not needed", s.CheckBlocked(AircraftClass.A));
            Run(s, 600);
            Assert.Greater(s.State.flightsHandled, 0);
            Assert.AreEqual(s.State.flightsHandled / (float)s.CheckInterval(AircraftClass.A), s.CheckProgress(AircraftClass.A), 1e-4);
        }

        [Test]
        public void AnOverdueTypePaysLessUntilChecked()
        {
            var s = Airline();
            bool told = false; s.CheckDue += c => told = true;
            double fresh = 0, overdue = 0;
            s.FlightPaid += (f, paid, onTime) => { if (s.CheckOverdue(AircraftClass.A)) overdue = paid; else fresh = paid; };
            s.State.fleetHours.Add(new LevelEntry { id = "a", level = s.CheckInterval(AircraftClass.A) - 1 });
            Run(s, 800);
            Assert.IsTrue(told);
            Assert.IsTrue(s.CheckOverdue(AircraftClass.A));
            Assert.Greater(overdue, 0);
            double money = s.State.money, cost = s.CheckCost(AircraftClass.A);
            Assert.IsTrue(s.DoCheck(AircraftClass.A));
            Assert.AreEqual(money - cost, s.State.money, 1e-6);
            Assert.IsFalse(s.CheckOverdue(AircraftClass.A));
            Assert.AreEqual(1, s.MaintenancePay(AircraftClass.A));
            Assert.AreEqual(Sim.OverduePay, 0.75);
        }

        [Test]
        public void AHangarHalvesTheBillAndStretchesTheInterval()
        {
            var s = Airline();
            s.State.fleetHours.Add(new LevelEntry { id = "a", level = 30 });
            double contractor = s.CheckCost(AircraftClass.A); int interval = s.CheckInterval(AircraftClass.A);
            s.State.ratingXp = Content.StarXp[6];
            Assert.IsTrue(s.Upgrade("hangar"), s.UpgradeBlocked("hangar"));
            Assert.Greater(s.CheckInterval(AircraftClass.A), interval);
            Assert.Less(s.CheckCost(AircraftClass.A), contractor * 0.6);
        }

        [Test]
        public void MoreAircraftShareTheFlying()
        {
            var s = Airline();
            int one = s.CheckInterval(AircraftClass.A);
            s.BuyAircraft(AircraftClass.A);
            Assert.AreEqual(one * 2, s.CheckInterval(AircraftClass.A));
        }
    }
}
