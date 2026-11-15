using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>A specialisation an airport can be known for: what it is, what it does for you and what it costs you.</summary>
    public sealed class SpecialismDef
    {
        public string Id, Name, Icon, Blurb;
        public string[] Pros, Cons;
    }

    /// <summary>
    /// Specialisations: from four stars the airport may be known for one thing. Each moves a few numbers the rest of
    /// the game already has (the contract market's odds and rewards, the parts of what a flight pays, how often an
    /// airline flies, its mood, the on-time target), up where it plays to the choice and down where it does not, so
    /// it only pays to pick one you then build and sign for. The first choice is free; changing it costs money and
    /// can be done once a day (real time). Unset, every multiplier is exactly one and nothing changes.
    /// </summary>
    public sealed partial class Sim
    {
        public const int SpecStars = 4;
        public const string SpecCargo = "cargo", SpecPremium = "premium", SpecLowCost = "lowcost", SpecRegional = "regional";
        /// <summary>Real hours before the specialisation can be changed again.</summary>
        public const double SpecCooldownHours = 24;
        /// <summary>A change costs at least this (times the map's costs), or this many minutes of the airport's income.</summary>
        public const double SpecSwitchBase = 50000, SpecSwitchMinutes = 30;

        // The magnitudes, one place for the rules and the words.
        public const double CargoShare = 0.35, CargoRetail = 0.15, CargoPax = 0.10, CargoFreightPay = 0.30, CargoFreightOdds = 0.25;
        public const double PremiumPax = 0.15, PremiumLounge = 0.5, PremiumVipPay = 0.30, PremiumVipOdds = 0.25, PremiumLowFareFlights = 0.15;
        public const float PremiumLowFareMood = -0.3f;
        public const double LowCostFlights = 0.25, LowCostLanding = 0.10;
        public const float LowCostMood = 0.3f, LowCostOnTime = 0.15f;
        public const double RegionalPay = 0.20, RegionalFlights = 0.20, RegionalHeavyPay = 0.20;

        static string Pct(double v) { return Math.Round(v * 100) + "%"; }

        public static readonly List<SpecialismDef> Specialisms = new List<SpecialismDef>
        {
            new SpecialismDef { Id = SpecCargo, Name = "Cargo Hub", Icon = "cargo", Blurb = "Freight first: the holds matter more than the shops.",
                Pros = new[] { "More freight contracts on the market, paying " + Pct(CargoFreightPay) + " more", "Cargo income " + Pct(CargoShare) + " higher on every flight" },
                Cons = new[] { "Shops earn " + Pct(CargoRetail) + " less", "Passenger fees " + Pct(CargoPax) + " lower" } },
            new SpecialismDef { Id = SpecPremium, Name = "Premium Terminal", Icon = "crown", Blurb = "For travellers who expect the best and pay for it.",
                Pros = new[] { "More VIP contracts on the market, paying " + Pct(PremiumVipPay) + " more", "Passenger fees " + Pct(PremiumPax) + " higher; the Premium Lounge counts " + Pct(PremiumLounge) + " more" },
                Cons = new[] { "Holiday and low-fare airlines fly " + Pct(PremiumLowFareFlights) + " less", "and like you a little less with every flight" } },
            new SpecialismDef { Id = SpecLowCost, Name = "Low-cost Base", Icon = "tag", Blurb = "Quick turns and cheap seats, all day long.",
                Pros = new[] { "Holiday and low-fare airlines fly " + Pct(LowCostFlights) + " more", "and like you a little more with every flight" },
                Cons = new[] { "Turnarounds must be " + Pct(LowCostOnTime) + " quicker to count as on time", "Landing fees " + Pct(LowCostLanding) + " lower" } },
            new SpecialismDef { Id = SpecRegional, Name = "Regional Connector", Icon = "compass", Blurb = "Small fields, short hops, many of them.",
                Pros = new[] { "Small aircraft (classes A and B) fly " + Pct(RegionalFlights) + " more", "and pay " + Pct(RegionalPay) + " more" },
                Cons = new[] { "Wide-bodies (classes D and E) pay " + Pct(RegionalHeavyPay) + " less" } },
        };

        public static SpecialismDef SpecialismById(string id) { return Specialisms.Find(x => x.Id == id); }

        /// <summary>The airport's specialisation ("": none).</summary>
        public string Specialism { get { return State.specialism ?? ""; } }
        public bool Specialised(string id) { return Specialism == id; }

        /// <summary>Holiday and low-fare airlines: the leisure ones and any that pays under the going rate.</summary>
        public static bool LowFare(string airline) { var a = Content.Airlines[airline]; return airline != "own" && (a.Kind == 1 || a.FeeScale < 1); }
        static bool FreightLine(string airline) { return Content.Airlines[airline].Kind == 3 || airline == "puffin"; }

        /// <summary>What a change costs now (the first choice is free).</summary>
        public double SpecialismCost { get { return State.specialismChanges == 0 ? 0 : Math.Floor(Math.Max(SpecSwitchBase * Map.CostScale, ContractMinute * SpecSwitchMinutes)); } }
        /// <summary>Real seconds until the specialisation can be changed again (0: now).</summary>
        public double SpecialismWait { get { return State.specialismChanges == 0 ? 0 : Math.Max(0, State.specialismAt + SpecCooldownHours * 3600 - RealNow()); } }

        /// <summary>Why this specialisation can't be chosen now (null: it can).</summary>
        public string SpecialismBlocked(string id)
        {
            if (SpecialismById(id) == null) return "Unknown";
            if (Stars < SpecStars) return "Needs a " + SpecStars + "-star airport";
            if (Specialised(id)) return "Your specialisation";
            if (id == SpecCargo && Level("cargo") <= 0) return "Needs the Cargo Terminal";
            if (SpecialismWait > 0) return "Can change in " + Format.Time(SpecialismWait);
            if (State.money < SpecialismCost) return "Need " + Format.Money(SpecialismCost - State.money) + " more";
            return null;
        }

        /// <summary>Makes this the airport's specialisation (pays for a change).</summary>
        public bool ChooseSpecialism(string id)
        {
            if (SpecialismBlocked(id) != null) return false;
            State.money -= SpecialismCost;
            State.specialism = id;
            State.specialismAt = RealNow();
            State.specialismChanges++;
            Toast?.Invoke("Your airport is now a " + SpecialismById(id).Name);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ the hooks (exactly one when unset)

        double SpecLanding { get { return Specialised(SpecLowCost) ? 1 - LowCostLanding : 1; } }
        double SpecPax { get { return Specialised(SpecPremium) ? 1 + PremiumPax : Specialised(SpecCargo) ? 1 - CargoPax : 1; } }
        double SpecLounge { get { return Specialised(SpecPremium) ? 1 + PremiumLounge : 1; } }
        double SpecRetail { get { return Specialised(SpecCargo) ? 1 - CargoRetail : 1; } }
        double SpecCargoPay { get { return Specialised(SpecCargo) ? 1 + CargoShare : 1; } }
        float SpecOnTime { get { return Specialised(SpecLowCost) ? 1 - LowCostOnTime : 1f; } }

        /// <summary>What a flight of this class pays against the usual.</summary>
        public double SpecClassPay(AircraftClass c)
        {
            if (!Specialised(SpecRegional)) return 1;
            return c <= AircraftClass.B ? 1 + RegionalPay : c >= AircraftClass.D ? 1 - RegionalHeavyPay : 1;
        }

        /// <summary>How often a route flies against its listed pace.</summary>
        public double SpecFlights(RouteDef r)
        {
            if (Specialised(SpecRegional)) return r.Class <= AircraftClass.B ? 1 + RegionalFlights : 1;
            if (Specialised(SpecLowCost)) return LowFare(r.Airline) ? 1 + LowCostFlights : 1;
            if (Specialised(SpecPremium)) return LowFare(r.Airline) ? 1 - PremiumLowFareFlights : 1;
            return 1;
        }

        /// <summary>How an airline's mood moves with each of its flights on top of the usual.</summary>
        public float SpecMood(string airline)
        {
            if (!LowFare(airline)) return 0f;
            return Specialised(SpecLowCost) ? LowCostMood : Specialised(SpecPremium) ? PremiumLowFareMood : 0f;
        }

        /// <summary>The share of an airline's market offers moved from series to its special kind (freight or VIP).</summary>
        double SpecialOdds(string airline)
        {
            if (Specialised(SpecCargo)) return FreightLine(airline) ? CargoFreightOdds : 0;
            if (Specialised(SpecPremium)) return !FreightLine(airline) && Content.Airlines[airline].Grade <= 1 ? PremiumVipOdds : 0;
            return 0;
        }

        /// <summary>What a contract of this kind is worth against the usual.</summary>
        double SpecContractPay(int kind)
        {
            if (kind == Freight && Specialised(SpecCargo)) return 1 + CargoFreightPay;
            if (kind == Vip && Specialised(SpecPremium)) return 1 + PremiumVipPay;
            return 1;
        }
    }
}
