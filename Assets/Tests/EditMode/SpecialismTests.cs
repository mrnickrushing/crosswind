using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>Specialisations: the gate, the cost and the cooldown, and that each moves the right numbers the right way.</summary>
    public class SpecialismTests
    {
        double _clock;

        Sim Airport(int stars)
        {
            _clock = 1_800_000_000;
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => _clock;
            s.State.money = 1e9;
            s.State.ratingXp = Content.StarXp[stars];
            return s;
        }

        /// <summary>A six-star airport with the cargo terminal and a runway for anything, specialised as asked ("": not at all).</summary>
        Sim Big(string spec)
        {
            var s = Airport(6);
            for (int i = 0; i < 6; i++) s.Upgrade("runway");
            s.Upgrade("cargo");
            Assert.Greater(s.Level("cargo"), 0);
            if (spec != "") Assert.IsTrue(s.ChooseSpecialism(spec), spec);
            return s;
        }

        static double[] Parts(Sim s, string route) { var p = new double[5]; s.FlightValue(Content.Routes[route], true, p); return p; }
        static double Load(Sim s, string route)
        {
            s.State.contracts.Clear();
            s.State.contracts.Add(new ContractState { route = route });
            return s.HourlyLoad().Sum();
        }

        [Test]
        public void FromFourStarsAndTheCargoHubNeedsTheCargoTerminal()
        {
            var s = Airport(3);
            foreach (var d in Sim.Specialisms) StringAssert.Contains("4-star", s.SpecialismBlocked(d.Id));
            s.State.ratingXp = Content.StarXp[4];
            Assert.AreEqual("Needs the Cargo Terminal", s.SpecialismBlocked(Sim.SpecCargo));
            Assert.IsNull(s.SpecialismBlocked(Sim.SpecPremium));
            Assert.IsNull(s.SpecialismBlocked(Sim.SpecLowCost));
            Assert.IsNull(s.SpecialismBlocked(Sim.SpecRegional));
            Assert.IsNotNull(s.SpecialismBlocked("nonsense"));
            s.State.ratingXp = Content.StarXp[6];
            s.Upgrade("cargo");
            Assert.IsNull(s.SpecialismBlocked(Sim.SpecCargo));
        }

        [Test]
        public void TheFirstChoiceIsFreeThenChangesCostMoneyOnceADay()
        {
            var s = Airport(4);
            s.State.money = 10;
            Assert.AreEqual(0, s.SpecialismCost);
            Assert.IsTrue(s.ChooseSpecialism(Sim.SpecRegional));
            Assert.AreEqual(10, s.State.money, "the first is free");
            Assert.AreEqual(Sim.SpecRegional, s.Specialism);
            Assert.AreEqual("Your specialisation", s.SpecialismBlocked(Sim.SpecRegional));
            StringAssert.StartsWith("Can change in", s.SpecialismBlocked(Sim.SpecPremium));
            Assert.IsFalse(s.ChooseSpecialism(Sim.SpecPremium));

            _clock += Sim.SpecCooldownHours * 3600 + 1;
            double cost = s.SpecialismCost;
            Assert.GreaterOrEqual(cost, Sim.SpecSwitchBase * s.Map.CostScale);
            StringAssert.StartsWith("Need ", s.SpecialismBlocked(Sim.SpecPremium));
            s.State.money = cost + 5;
            Assert.IsTrue(s.ChooseSpecialism(Sim.SpecPremium));
            Assert.AreEqual(5, s.State.money, 0.001);
            Assert.AreEqual(Sim.SpecPremium, s.Specialism);
            StringAssert.StartsWith("Can change in", s.SpecialismBlocked(Sim.SpecLowCost), "and the clock starts again");
        }

        [Test]
        public void UnsetChangesNothing()
        {
            var plain = Big("");
            var reset = Big(Sim.SpecRegional);
            reset.State.specialism = "";
            foreach (var r in Content.Routes.Values.Where(x => !Content.IsContractRoute(x.Id)))
            {
                Assert.AreEqual(plain.FlightValue(r), reset.FlightValue(r), r.Id);
                Assert.AreEqual(plain.OnTimeTarget(r), reset.OnTimeTarget(r), r.Id);
                Assert.AreEqual(1.0, plain.SpecFlights(r), r.Id);
                Assert.AreEqual(1.0, plain.SpecClassPay(r.Class), r.Id);
                Assert.AreEqual(0f, plain.SpecMood(r.Airline), r.Id);
            }
            plain.RollMarket(); reset.RollMarket();
            CollectionAssert.AreEqual(plain.State.market.Select(b => b.kind + ":" + b.reward), reset.State.market.Select(b => b.kind + ":" + b.reward));
        }

        /// <summary>Many markets on the same dice: how many offers of a kind, and what they pay against a series flight.</summary>
        static (int count, double pay) Market(Sim s, int kind)
        {
            int n = 0; double pay = 0;
            for (int i = 0; i < 300; i++)
            {
                s.RollMarket();
                foreach (var b in s.State.market.Where(x => x.kind == kind)) { n++; pay += b.reward; }
            }
            return (n, n > 0 ? pay / n : 0);
        }

        [Test]
        public void ACargoHubGetsMoreFreightThatPaysMoreAndShopsEarnLess()
        {
            var plain = Big(""); var hub = Big(Sim.SpecCargo);
            var a = Market(plain, Sim.Freight); var b = Market(hub, Sim.Freight);
            Assert.Greater(a.count, 0);
            Assert.Greater(b.count, a.count * 1.5, "more freight on offer");
            Assert.Greater(b.pay, a.pay * 1.15, "and it pays more");

            for (int i = 0; i < 3; i++) { plain.PlaceAnywhere("cafe"); hub.PlaceAnywhere("cafe"); }
            var p = Parts(plain, "zip_capital"); var h = Parts(hub, "zip_capital");
            Assert.Greater(p[3], 0); Assert.Greater(p[4], 0);
            Assert.AreEqual(1 + Sim.CargoShare, h[4] / p[4], 1e-9, "cargo");
            Assert.AreEqual(1 - Sim.CargoRetail, h[3] / p[3], 1e-9, "shops");
            Assert.AreEqual(1 - Sim.CargoPax, h[1] / p[1], 1e-9, "passenger fees");
            Assert.AreEqual(p[0], h[0], "landing fees as they were");
        }

        [Test]
        public void APremiumTerminalGetsMoreVipFlightsAndLowFareAirlinesCool()
        {
            var plain = Big(""); var prem = Big(Sim.SpecPremium);
            var a = Market(plain, Sim.Vip); var b = Market(prem, Sim.Vip);
            Assert.Greater(a.count, 0);
            Assert.Greater(b.count, a.count * 1.5, "more VIP flights on offer");
            Assert.Greater(b.pay, a.pay * 1.15, "and they pay more");

            Assert.AreEqual(1 + Sim.PremiumPax, Parts(prem, "meridian_east")[1] / Parts(plain, "meridian_east")[1], 1e-9, "passenger fee, no lounge");
            plain.PlaceAnywhere("lounge"); prem.PlaceAnywhere("lounge");
            Assert.Greater(plain.LoungeLevel, 0);
            Assert.Greater(Parts(prem, "meridian_east")[1] / Parts(plain, "meridian_east")[1], 1 + Sim.PremiumPax + 1e-6, "the lounge counts more");

            Assert.AreEqual(1 - Sim.PremiumLowFareFlights, Load(prem, "sunhop_reef") / Load(plain, "sunhop_reef"), 1e-6, "holiday airlines fly less");
            Assert.AreEqual(1 - Sim.PremiumLowFareFlights, Load(prem, "zip_capital") / Load(plain, "zip_capital"), 1e-6, "low-fare airlines fly less");
            Assert.AreEqual(1.0, Load(prem, "meridian_east") / Load(plain, "meridian_east"), 1e-6, "the others as they were");
            Assert.Less(prem.SpecMood("sunhop"), 0); Assert.AreEqual(0f, prem.SpecMood("meridian"));
        }

        [Test]
        public void ALowCostBaseFliesTheLowFareAirlinesMoreOnTighterTurns()
        {
            var plain = Big(""); var lc = Big(Sim.SpecLowCost);
            Assert.AreEqual(1 + Sim.LowCostFlights, Load(lc, "zip_capital") / Load(plain, "zip_capital"), 1e-6);
            Assert.AreEqual(1 + Sim.LowCostFlights, Load(lc, "solstice_keys") / Load(plain, "solstice_keys"), 1e-6);
            Assert.AreEqual(1.0, Load(lc, "puffin_coast") / Load(plain, "puffin_coast"), 1e-6, "the others as they were");
            Assert.Greater(lc.SpecMood("zip"), 0); Assert.AreEqual(0f, lc.SpecMood("crown"));
            var r = Content.Routes["zip_capital"];
            Assert.AreEqual(1 - Sim.LowCostOnTime, lc.OnTimeTarget(r) / plain.OnTimeTarget(r), 1e-5, "a shorter on-time target");
            Assert.AreEqual(1 - Sim.LowCostLanding, Parts(lc, "zip_capital")[0] / Parts(plain, "zip_capital")[0], 1e-9, "lower landing fees");
            Assert.AreEqual(Parts(plain, "zip_capital")[1], Parts(lc, "zip_capital")[1], "passenger fees as they were");
        }

        [Test]
        public void ARegionalConnectorFavoursSmallAircraftOverWideBodies()
        {
            var plain = Big(""); var reg = Big(Sim.SpecRegional);
            foreach (var r in new[] { "puffin_coast", "fjord_north" })
            {
                Assert.AreEqual(1 + Sim.RegionalPay, reg.FlightValue(Content.Routes[r]) / plain.FlightValue(Content.Routes[r]), 1e-9, r);
                Assert.AreEqual(1 + Sim.RegionalFlights, Load(reg, r) / Load(plain, r), 1e-6, r);
            }
            Assert.AreEqual(1.0, reg.FlightValue(Content.Routes["zip_capital"]) / plain.FlightValue(Content.Routes["zip_capital"]), 1e-9, "class C as it was");
            Assert.AreEqual(1.0, Load(reg, "zip_capital") / Load(plain, "zip_capital"), 1e-6);
            Assert.AreEqual(1 - Sim.RegionalHeavyPay, reg.FlightValue(Content.Routes["solstice_south"]) / plain.FlightValue(Content.Routes["solstice_south"]), 1e-9, "wide-bodies pay less");
            Assert.AreEqual(1.0, Load(reg, "solstice_south") / Load(plain, "solstice_south"), 1e-6);
        }

        [Test]
        public void AnOldSaveLoadsWithoutOne()
        {
            var st = GameState.New();
            st.specialism = null;
            st.Repair();
            Assert.AreEqual("", st.specialism);
            st.specialism = "gone";
            st.Repair();
            Assert.AreEqual("", st.specialism);
        }
    }
}
