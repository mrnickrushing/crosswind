using System;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// Your own airline. From four stars you can buy aircraft; each flies its class's route from your airport, in the
    /// colours you pick, and because the tickets are yours each flight pays well above an ordinary airline's. More
    /// aircraft of a class fly that route more often. They need the same runway, stands and services as anyone's.
    /// The fleet stays with the airport when you sell it; the colours stay with you.
    /// </summary>
    public sealed partial class Sim
    {
        public const int FleetStars = 4, MaxPerClass = 6;
        public const double OwnPay = 1.8;
        public static readonly string[] Liveries = { "Skyline Blue", "Signal Red", "Forest Green", "Sunrise Orange", "Royal Purple", "Graphite" };
        public static readonly uint[] LiveryHex = { 0x1F6FD0, 0xD43A2F, 0x2F8F5B, 0xF08A1E, 0x7A4FD0, 0x3A4048 };

        public int Owned(AircraftClass c)
        {
            var e = State.fleet.Find(f => f.id == c.ToString().ToLower());
            return e != null ? e.level : 0;
        }

        public int FleetSize { get { return State.fleet.Sum(f => f.level); } }

        public double AircraftCost(AircraftClass c)
        {
            return Math.Floor(Content.Aircraft[c].LandingFee * 600 * Math.Pow(1.7, Owned(c)) * Map.CostScale);
        }

        public string BuyAircraftBlocked(AircraftClass c)
        {
            var plane = Content.Aircraft[c];
            if (Stars < FleetStars) return "Needs a " + FleetStars + "-star airport";
            if (Owned(c) >= MaxPerClass) return "Fleet full for this type";
            if (Level("runway") < plane.RunwayLevel) return "Runway too short (level " + plane.RunwayLevel + ")";
            if (Heavy(c) && HeavyStands == 0) return "Needs a Wide-body Stand";
            foreach (var n in Content.Routes["own_" + c.ToString().ToLower()].Needs) if (Crews(n) == 0) return "Needs " + Content.Facilities[Content.ServiceFacility(n)].Name;
            if (State.money < AircraftCost(c)) return "Need " + Format.Money(AircraftCost(c) - State.money) + " more";
            return null;
        }

        public bool BuyAircraft(AircraftClass c)
        {
            if (BuyAircraftBlocked(c) != null) return false;
            State.money -= AircraftCost(c);
            string id = c.ToString().ToLower();
            var e = State.fleet.Find(f => f.id == id);
            if (e == null) State.fleet.Add(new LevelEntry { id = id, level = 1 }); else e.level++;
            if (!Signed("own_" + id)) State.contracts.Add(new ContractState { route = "own_" + id, nextArrival = State.time + 4 });
            AddRating(25 * (1 + (int)c));
            Changed?.Invoke();
            return true;
        }

        /// <summary>How much more often a route flies for the aircraft you own on it (1 for everyone else's).</summary>
        double FleetFlights(RouteDef r) { return r.Airline == "own" ? Math.Max(1, Owned(r.Class)) : 1; }

        public void SetLivery(int index)
        {
            State.livery = ((index % Liveries.Length) + Liveries.Length) % Liveries.Length;
            Changed?.Invoke();
        }
    }
}
