using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class SimTests
    {
        static Sim Fresh() { return new Sim(GameState.New()) { BuildTimeScale = 0, RealNow = () => 1_800_000_000 }; }

        /// <summary>Plays for a while, tapping every waiting service the way a player would.</summary>
        static void Play(Sim s, float seconds, bool tap = true)
        {
            for (float t = 0; t < seconds; t += 0.25f)
            {
                if (tap) foreach (var (f, j) in s.Waiting.ToList()) s.Dispatch(f.id, j.service);
                s.Tick(0.25f);
            }
        }

        [Test]
        public void AFlightLandsIsServedAndPays()
        {
            var s = Fresh();
            double start = s.State.money;
            int paid = 0; bool onTime = false;
            s.FlightPaid += (f, m, ok) => { paid++; onTime = ok; };
            Play(s, 60);
            Assert.GreaterOrEqual(paid, 1, "the first flight turned round inside a minute");
            Assert.IsTrue(onTime, "with the player tapping at once, it leaves on time");
            Assert.Greater(s.State.money, start);
            Assert.AreEqual(s.State.flightsHandled, s.State.onTime + s.State.late);
        }

        [Test]
        public void WithoutATapOrAManagerAFlightWaitsAtTheStand()
        {
            var s = Fresh();
            Play(s, 120, tap: false);
            Assert.AreEqual(0, s.State.flightsHandled, "nobody sent the fuel truck");
            Assert.IsTrue(s.State.flights.Any(f => f.phase == Phase.Service));
            Assert.LessOrEqual(s.State.flights.Count, s.TowerSlots, "the tower holds the rest off");
            Assert.AreEqual(0, s.IdleRate(), "and nothing is earned while away");
        }

        [Test]
        public void AManagerRunsTheDepartmentAndEarnsOffline()
        {
            var s = Fresh();
            s.State.money = 1e6;
            Assert.IsTrue(s.Hire("fuel"));
            Assert.IsFalse(s.Hire("fuel"), "once");
            Play(s, 120, tap: false);
            Assert.Greater(s.State.flightsHandled, 0, "the depot dispatched its own trucks");
            double rate = s.IdleRate();
            Assert.Greater(rate, 0);
            double hour = s.OfflineEarnings(3600);
            Assert.AreEqual(System.Math.Floor(rate * 3600 * Content.OfflineEfficiency), hour);
            Assert.AreEqual(s.OfflineEarnings(Content.OfflineHours * 3600), s.OfflineEarnings(1e9), "capped");
            double before = s.State.money;
            Assert.AreEqual(hour * 2, s.CollectOffline(3600, 2));
            Assert.AreEqual(before + hour * 2, s.State.money);
        }

        [Test]
        public void OneStandAndOneRunwayAreNeverShared()
        {
            var s = Fresh();
            s.State.money = 1e9;
            s.Hire("fuel");
            s.Sign("puffin_coast");
            for (int i = 0; i < 2400; i++)
            {
                s.Tick(0.25f);
                int onRunway = s.State.flights.Count(f => f.phase == Phase.Landing || f.phase == Phase.Takeoff);
                Assert.LessOrEqual(onRunway, s.RunwayLanes, "one movement at a time");
                var stands = s.State.flights.Where(f => f.stand >= 0).Select(f => f.stand).ToList();
                Assert.AreEqual(stands.Count, stands.Distinct().Count(), "one aircraft a stand");
                Assert.IsTrue(stands.All(x => x < s.StandCount));
            }
            Assert.Greater(s.State.flightsHandled, 5);
        }

        [Test]
        public void UpgradesCostMoreEachLevelAndNeedTheMoneyAndTheStars()
        {
            var s = Fresh();
            StringAssert.StartsWith("Need ", s.UpgradeBlocked("runway"));
            StringAssert.Contains("2-star", s.UpgradeBlocked("baggage"));
            s.State.money = 1e9;
            double c1 = s.Cost("stands");
            Assert.IsTrue(s.Upgrade("stands"));
            Assert.Greater(s.Cost("stands"), c1);
            Assert.AreEqual(2, s.StandCount);
            Assert.AreEqual(1e9 - c1, s.State.money);
        }

        [Test]
        public void BiggerAircraftNeedTheRunwayAndTheServices()
        {
            var s = Fresh();
            s.State.money = 1e12;
            s.State.ratingXp = Content.StarXp[3];
            StringAssert.Contains("Runway too short", s.SignBlocked("sunhop_bay"));
            while (s.Level("runway") < 3) Assert.IsTrue(s.Upgrade("runway"));
            StringAssert.Contains("Baggage", s.SignBlocked("sunhop_bay"));
            Assert.IsTrue(s.Upgrade("baggage"));
            Assert.IsNull(s.SignBlocked("sunhop_bay"));
            double before = s.State.money;
            Assert.IsTrue(s.Sign("sunhop_bay"));
            Assert.AreEqual(before + Content.Routes["sunhop_bay"].SigningBonus, s.State.money);
            Assert.AreEqual(AircraftClass.B, s.MaxClass);
        }

        [Test]
        public void MoreStandsAndCrewsMoveMoreFlights()
        {
            int Run(bool grown)
            {
                var s = Fresh();
                s.State.money = 1e12;
                s.State.ratingXp = Content.StarXp[2];
                s.Hire("fuel");
                s.Sign("puffin_coast"); s.Sign("fjord_sound");       // more traffic than one stand can turn
                if (grown) { for (int i = 0; i < 3; i++) s.Upgrade("stands"); for (int i = 0; i < 6; i++) s.Upgrade("fuel"); }
                Play(s, 1800, tap: false);
                return s.State.flightsHandled;
            }
            Assert.Greater(Run(true), Run(false));
        }

        [Test]
        public void TheGuideAdvancesAndPaysOnce()
        {
            var s = Fresh();
            Assert.AreEqual("first_flight", s.CurrentGoal.Id);
            Assert.IsFalse(s.ClaimGoal());
            Play(s, 60);
            Assert.IsTrue(s.GoalDone);
            double before = s.State.money;
            Assert.IsTrue(s.ClaimGoal());
            Assert.AreEqual(before + 100, s.State.money);
            Assert.AreEqual("three_flights", s.CurrentGoal.Id);
        }

        [Test]
        public void StarsComeWithRatingAndAnnounceThemselves()
        {
            var s = Fresh();
            int gained = 0;
            s.StarGained += n => gained = n;
            Assert.AreEqual(1, s.Stars);
            s.State.money = 1e9;
            while (s.Stars < 2) s.Upgrade("stands");
            Assert.AreEqual(2, gained);
            Assert.That(s.StarProgress, Is.InRange(0f, 1f));
        }

        [Test]
        public void ASaveRepairsItself()
        {
            var st = GameState.New();
            st.levels = null; st.flights = null; st.contracts.Add(new ContractState { route = "gone_route" });
            var s = new Sim(st);
            Assert.AreEqual(1, s.Level("runway"));
            Assert.AreEqual(1, s.State.contracts.Count);
            s.Tick(5);
        }

        [Test]
        public void TheSameSeedPlaysTheSameGame()
        {
            double Run() { var s = Fresh(); s.State.money = 1e6; s.Hire("fuel"); s.Sign("puffin_coast"); Play(s, 900, tap: false); return s.State.money; }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void MoneyReadsShort()
        {
            Assert.AreEqual("0", Format.Money(0));
            Assert.AreEqual("999", Format.Money(999.9));
            Assert.AreEqual("1K", Format.Money(1000));
            Assert.AreEqual("1.23K", Format.Money(1234));
            Assert.AreEqual("15.6M", Format.Money(15_600_000));
            Assert.AreEqual("999K", Format.Money(999_999));
            Assert.AreEqual("2.5T", Format.Money(2.5e12));
            Assert.AreEqual("45s", Format.Time(45));
            Assert.AreEqual("3m 20s", Format.Time(200));
            Assert.AreEqual("2h 05m", Format.Time(7500));
        }
    }
}
