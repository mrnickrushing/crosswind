using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>Aircraft size classes, smallest first. A new class is a milestone: it needs a longer runway and bigger stands.</summary>
    public enum AircraftClass { A, B, C, D, E }

    /// <summary>What a turnaround can need. Each is a department with crews that have to reach the aircraft.</summary>
    public enum Service { Fuel, Baggage, Catering, Cleaning }

    public sealed class AircraftDef
    {
        public AircraftClass Class;
        public string Name;
        public int Seats;
        public double LandingFee;        // paid per landing before multipliers
        public float RunwaySeconds;      // how long a landing or a take-off holds the runway
        public float TaxiSeconds;        // each way, before taxiway upgrades
        public float ServiceSeconds;     // one crew doing one service on this aircraft
        public float BoardSeconds;       // deplaning or boarding at a terminal that keeps up
        public float FuelMargin;         // profit on the fuel it takes
        public int RunwayLevel;          // the runway level it needs
    }

    /// <summary>Something you build and level up. Its level drives one number in the simulation.</summary>
    public sealed class FacilityDef
    {
        public string Id, Name, Blurb, Group;
        public double BaseCost;
        public float Growth = 1.6f;      // each level costs this many times the last
        public int MaxLevel = 50;
        public int StartLevel;           // what a new airport has
        public int Stars;                // airport rating needed to build the first level
    }

    public sealed class AirlineDef
    {
        public string Id, Name, Motto;
        public float FeeScale = 1f;      // how well it pays
        public float Patience = 1f;      // how much lateness it puts up with (scales the on-time target)
    }

    /// <summary>A route an airline offers: so many flights an hour of one aircraft class, needing these services.</summary>
    public sealed class RouteDef
    {
        public string Id, Airline, Destination;
        public AircraftClass Class;
        public float FlightsPerHour;
        public Service[] Needs;
        public int Stars;                // airport rating needed before it's offered
        public double SigningBonus;
    }

    public static partial class Content
    {
        public static readonly Dictionary<AircraftClass, AircraftDef> Aircraft = new Dictionary<AircraftClass, AircraftDef>();
        public static readonly Dictionary<string, FacilityDef> Facilities = new Dictionary<string, FacilityDef>();
        public static readonly List<string> FacilityOrder = new List<string>();
        public static readonly Dictionary<string, AirlineDef> Airlines = new Dictionary<string, AirlineDef>();
        public static readonly Dictionary<string, RouteDef> Routes = new Dictionary<string, RouteDef>();

        // Tuning.
        public const double StartMoney = 400;
        public const double PassengerFee = 2.5;          // per passenger, each way, at full satisfaction
        public const double RetailSpend = 1.2;           // per passenger per shop level, at full satisfaction
        public const float OnTimeBonus = 0.5f;           // extra share of the landing fee for an on-time departure
        public const float LateShare = 0.6f;             // share of the landing fee when late
        public const float OfflineHours = 2f;            // how long the airport earns while the app is closed
        public const float OfflineEfficiency = 0.5f;     // of what it would have earned with you watching
        public const float OnTimeSlack = 1.6f;           // an on-time turnaround takes at most this many times the ideal

        /// <summary>Rating XP needed to reach each star level (index 1 = one star, held from the start).</summary>
        public static readonly double[] StarXp = { 0, 0, 60, 450, 2200, 10000, 50000, 250000, 1.5e6, 8e6, 5e7 };
        public static int MaxStars { get { return StarXp.Length - 1; } }

        static Content()
        {
            void Plane(AircraftClass c, string name, int seats, double fee, float runway, float taxi, float service, float board, float fuel, int runwayLevel)
            {
                Aircraft[c] = new AircraftDef { Class = c, Name = name, Seats = seats, LandingFee = fee, RunwaySeconds = runway, TaxiSeconds = taxi, ServiceSeconds = service, BoardSeconds = board, FuelMargin = fuel, RunwayLevel = runwayLevel };
            }
            Plane(AircraftClass.A, "Kestrel 9", 9, 30, 6, 6, 5, 4, 6, 1);
            Plane(AircraftClass.B, "Heron 50", 50, 160, 8, 8, 9, 8, 40, 3);
            Plane(AircraftClass.C, "Albatross 160", 160, 900, 10, 10, 14, 14, 260, 6);
            Plane(AircraftClass.D, "Condor 300", 300, 5200, 12, 12, 20, 20, 1500, 10);
            Plane(AircraftClass.E, "Leviathan 520", 520, 32000, 14, 14, 28, 28, 9000, 15);

            FacilityDef Fac(string id, string name, string group, double cost, string blurb, int start = 0, int stars = 1, float growth = 1.6f, int max = 50)
            {
                var f = new FacilityDef { Id = id, Name = name, Group = group, BaseCost = cost, Blurb = blurb, StartLevel = start, Stars = stars, Growth = growth, MaxLevel = max };
                Facilities[id] = f; FacilityOrder.Add(id);
                return f;
            }
            // Airside.
            Fac("runway", "Runway", "Airside", 500, "Longer and harder surfaces take bigger aircraft, and each level clears the runway faster.", start: 1, growth: 2.1f, max: 20);
            Fac("stands", "Aircraft Stands", "Airside", 150, "One more aircraft on the ground at a time.", start: 1, growth: 1.75f, max: 40);
            Fac("taxiways", "Taxiways", "Airside", 220, "Shorter, wider routes between the runway and the stands.", growth: 1.7f, max: 25);
            Fac("tower", "Control Tower", "Airside", 800, "More aircraft on approach at once, and a second runway movement at higher levels.", start: 1, stars: 2, growth: 2.2f, max: 15);
            // Services: each level adds speed, every third level a crew.
            Fac("fuel", "Fuel Depot", "Services", 120, "Fuel trucks. Faster crews, and another truck every three levels.", start: 1);
            Fac("baggage", "Baggage Handling", "Services", 180, "Tugs and belts. Faster crews, and another tug every three levels.", stars: 2);
            Fac("catering", "Flight Kitchen", "Services", 900, "Meals loaded on time. Needed by the bigger airlines.", stars: 3);
            Fac("cleaning", "Cabin Cleaning", "Services", 1400, "Crews that turn a cabin round between flights.", stars: 4);
            // Terminal.
            Fac("checkin", "Check-in Desks", "Terminal", 200, "More passengers processed a minute.", start: 1);
            Fac("security", "Security Lanes", "Terminal", 260, "Shorter queues: happier passengers who spend more.", start: 1);
            Fac("gates", "Gate Lounges", "Terminal", 320, "Faster boarding and deplaning.", start: 1);
            Fac("shops", "Shops and Cafes", "Terminal", 400, "Every passenger spends more.", stars: 2);
            Fac("seating", "Seating and Toilets", "Terminal", 300, "Somewhere to sit, and clean toilets: comfort for everyone waiting.", stars: 2);
            Fac("info", "Information and Wayfinding", "Terminal", 900, "Screens, signs and a help desk: nobody lost, nobody late to the gate.", stars: 3);
            Fac("lounge", "Lounges", "Terminal", 6000, "Comfort for the passengers who pay most: the passenger fee rises.", stars: 4);
            Fac("parking", "Car Park and Transit", "Terminal", 2500, "Parking, buses, and later a rail link: income every minute, flights or not.", stars: 3);
            // Operations.
            Fac("ils", "Landing Aids", "Airside", 3000, "Lights, radar and ILS: bad weather slows the runway less with every level.", stars: 3, growth: 1.9f, max: 10);
            Fac("hangar", "Maintenance Hangar", "Services", 20000, "Aircraft stay for checks and pay rent: a share on top of every landing fee.", stars: 5, growth: 1.8f, max: 20);
            Fac("cargo", "Cargo Terminal", "Services", 60000, "Freight in the belly of every flight: more for each seat-mile.", stars: 6, growth: 1.8f, max: 20);

            void Line(string id, string name, string motto, float fee, float patience) { Airlines[id] = new AirlineDef { Id = id, Name = name, Motto = motto, FeeScale = fee, Patience = patience }; }
            Line("puffin", "Puffin Air Mail", "If it fits in the hold, it flies.", 1.0f, 1.5f);
            Line("sunhop", "Sunhop Holidays", "Two weeks of somewhere else.", 1.1f, 1.2f);
            Line("zip", "Zip", "Cheap seats, tight turns.", 0.85f, 0.8f);
            Line("meridian", "Meridian Airways", "The long way, properly.", 1.4f, 1.0f);
            Line("atlas", "Atlas Freight", "Night and day, by the tonne.", 1.25f, 1.3f);
            Line("fjord", "Fjordlink", "Small fields, big weather.", 1.15f, 1.1f);
            Line("solstice", "Solstice", "Somewhere it is always summer.", 1.3f, 0.9f);
            Line("crown", "Crown Executive", "Your time, our business.", 2.2f, 0.7f);

            void Route(string id, string airline, string dest, AircraftClass c, float perHour, int stars, double bonus, params Service[] needs)
            {
                Routes[id] = new RouteDef { Id = id, Airline = airline, Destination = dest, Class = c, FlightsPerHour = perHour, Stars = stars, SigningBonus = bonus, Needs = needs };
            }
            Route("puffin_isles", "puffin", "The Out Isles", AircraftClass.A, 40, 1, 0, Service.Fuel);
            Route("puffin_coast", "puffin", "Harbour Coast", AircraftClass.A, 60, 1, 150, Service.Fuel);
            Route("sunhop_bay", "sunhop", "Saffron Bay", AircraftClass.B, 30, 2, 800, Service.Fuel, Service.Baggage);
            Route("zip_north", "zip", "Northgate", AircraftClass.B, 50, 2, 1200, Service.Fuel, Service.Baggage);
            Route("sunhop_reef", "sunhop", "Coral Reach", AircraftClass.C, 24, 3, 6000, Service.Fuel, Service.Baggage, Service.Catering);
            Route("zip_capital", "zip", "The Capital", AircraftClass.C, 40, 3, 9000, Service.Fuel, Service.Baggage);
            Route("meridian_east", "meridian", "Port Meridian", AircraftClass.C, 20, 4, 30000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("atlas_hub", "atlas", "Ironbridge Freight Hub", AircraftClass.D, 16, 5, 150000, Service.Fuel, Service.Baggage);
            Route("meridian_ocean", "meridian", "Across the Ocean", AircraftClass.D, 14, 5, 220000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("meridian_world", "meridian", "Round the World", AircraftClass.E, 10, 7, 2500000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("atlas_heavy", "atlas", "Heavy Lift Line", AircraftClass.E, 12, 8, 4000000, Service.Fuel, Service.Baggage);
            Route("fjord_sound", "fjord", "Kelda Sound", AircraftClass.A, 70, 2, 500, Service.Fuel);
            Route("fjord_north", "fjord", "Northlight", AircraftClass.B, 36, 3, 4000, Service.Fuel, Service.Baggage);
            Route("puffin_mail", "puffin", "The Night Mail", AircraftClass.B, 44, 3, 3000, Service.Fuel);
            Route("zip_west", "zip", "Westmark", AircraftClass.C, 46, 4, 40000, Service.Fuel, Service.Baggage);
            Route("solstice_keys", "solstice", "The Amber Keys", AircraftClass.C, 28, 4, 60000, Service.Fuel, Service.Baggage, Service.Catering);
            Route("crown_shuttle", "crown", "Capital Shuttle", AircraftClass.B, 24, 4, 50000, Service.Fuel, Service.Catering, Service.Cleaning);
            Route("atlas_night", "atlas", "Night Freight", AircraftClass.C, 34, 5, 90000, Service.Fuel, Service.Baggage);
            Route("solstice_south", "solstice", "The Far South", AircraftClass.D, 18, 6, 600000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("zip_long", "zip", "Zip Long Haul", AircraftClass.D, 26, 6, 500000, Service.Fuel, Service.Baggage, Service.Cleaning);
            Route("crown_global", "crown", "Crown Global", AircraftClass.D, 12, 7, 2000000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("fjord_polar", "fjord", "Over the Pole", AircraftClass.D, 16, 7, 1500000, Service.Fuel, Service.Baggage, Service.Catering);
            Route("solstice_world", "solstice", "Endless Summer", AircraftClass.E, 14, 8, 6000000, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);
            Route("zip_jumbo", "zip", "Zip Max", AircraftClass.E, 20, 9, 2.0e7, Service.Fuel, Service.Baggage, Service.Cleaning);
            Route("crown_flag", "crown", "The Crown Jewel", AircraftClass.E, 10, 10, 1.0e8, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);

            // Your own airline: one route per aircraft class, flown by the aircraft you buy (never offered as a contract).
            Line("own", "Your Airline", "Your colours, your routes.", 1.0f, 1.2f);
            Route("own_a", "own", "Island Hopper", AircraftClass.A, 14, 99, 0, Service.Fuel);
            Route("own_b", "own", "Regional Link", AircraftClass.B, 11, 99, 0, Service.Fuel, Service.Baggage);
            Route("own_c", "own", "City Express", AircraftClass.C, 9, 99, 0, Service.Fuel, Service.Baggage);
            Route("own_d", "own", "Long Haul", AircraftClass.D, 7, 99, 0, Service.Fuel, Service.Baggage, Service.Catering);
            Route("own_e", "own", "Flagship", AircraftClass.E, 5, 99, 0, Service.Fuel, Service.Baggage, Service.Catering, Service.Cleaning);

            ExtendProgress();
        }

        public static string ServiceFacility(Service s)
        {
            switch (s) { case Service.Fuel: return "fuel"; case Service.Baggage: return "baggage"; case Service.Catering: return "catering"; default: return "cleaning"; }
        }

        public static IEnumerable<RouteDef> RoutesOf(string airline) { return Routes.Values.Where(r => r.Airline == airline); }
    }
}
