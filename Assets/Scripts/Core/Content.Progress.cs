using System.Collections.Generic;

namespace Crosswind.Core
{
    /// <summary>What a research project or a map changes: a named number the rules multiply or add in.</summary>
    public enum Effect
    {
        Income,             // everything a flight pays
        LandingFee, PassengerFee, Retail, FuelMargin,
        RunwaySpeed, TaxiSpeed, CrewSpeed, BoardSpeed,
        OfflineHours, OfflineEfficiency,
        BuildDiscount,      // share off every build and upgrade
        OnTimeSlack,        // more time before a flight counts as late
        WeatherShield,      // share of the weather penalty removed
        RatingGain, Demand, // rating XP, and how often flights come
    }

    public sealed class ResearchDef
    {
        public string Id, Name, Blurb, Branch, Requires;
        public double Cost;
        public float Seconds;           // real time
        public int Stars;
        public Effect Effect;
        public float Value;             // added to the effect's total (0.1 = +10%)
    }

    /// <summary>An airport you can run. Each new one is harder and pays more, with its own weather and a twist.</summary>
    public sealed class MapDef
    {
        public string Id, Name, Blurb, Twist;
        public double IncomeScale = 1, CostScale = 1;
        public float Storm, Fog, Snow;  // how likely each kind of weather is (0..1 of the time)
        public int MaxRunway = 20;      // some fields have no room for a long runway
        public int WingsToOpen;
    }

    public enum Weather { Clear, Rain, Fog, Storm, Snow }

    public static partial class Content
    {
        public static readonly List<ResearchDef> Research = new List<ResearchDef>();
        public static readonly List<MapDef> Maps = new List<MapDef>();

        public const int BoostMinutes = 30;             // one income boost (a rewarded ad, or gems)
        public const float BoostScale = 2f;
        public const int BoostGems = 10;
        public const float WingBonus = 0.08f;           // income per Wing, for good
        public const int SellStars = 5;                 // the rating at which a stake can be sold

        public static ResearchDef ResearchById(string id) { return Research.Find(r => r.Id == id); }

        static void ExtendProgress()
        {
            void R(string id, string branch, string name, string blurb, double cost, float minutes, int stars, Effect e, float v, string requires = null)
            {
                Research.Add(new ResearchDef { Id = id, Branch = branch, Name = name, Blurb = blurb, Cost = cost, Seconds = minutes * 60, Stars = stars, Effect = e, Value = v, Requires = requires });
            }
            // Operations: the airside runs faster.
            R("ops_marshal", "Operations", "Ground Marshals", "Aircraft taxi 15% faster.", 400, 1, 1, Effect.TaxiSpeed, 0.15f);
            R("ops_slots", "Operations", "Slot Planning", "The runway clears 10% faster.", 2500, 5, 2, Effect.RunwaySpeed, 0.10f, "ops_marshal");
            R("ops_crews", "Operations", "Crew Training", "Every service crew works 15% faster.", 12000, 15, 3, Effect.CrewSpeed, 0.15f, "ops_slots");
            R("ops_radar", "Operations", "Surface Radar", "Weather slows you 25% less.", 60000, 30, 4, Effect.WeatherShield, 0.25f, "ops_crews");
            R("ops_rapid", "Operations", "Rapid Exits", "The runway clears another 15% faster.", 400000, 60, 5, Effect.RunwaySpeed, 0.15f, "ops_radar");
            R("ops_ai", "Operations", "Turnaround Planner", "Crews another 20% faster.", 5e6, 180, 7, Effect.CrewSpeed, 0.20f, "ops_rapid");
            R("ops_night", "Operations", "Round the Clock", "30% more flights on every route.", 8e7, 480, 9, Effect.Demand, 0.30f, "ops_ai");
            // Passengers: they pay more and wait better.
            R("pax_signs", "Passengers", "Clear Signs", "Boarding and deplaning 15% faster.", 600, 2, 1, Effect.BoardSpeed, 0.15f);
            R("pax_seats", "Passengers", "Better Seating", "Passenger fees up 10%.", 3500, 6, 2, Effect.PassengerFee, 0.10f, "pax_signs");
            R("pax_wifi", "Passengers", "Free Wi-Fi", "Airlines forgive 15% more delay.", 15000, 15, 3, Effect.OnTimeSlack, 0.15f, "pax_seats");
            R("pax_fast", "Passengers", "Fast Track", "Passenger fees up another 20%.", 90000, 40, 4, Effect.PassengerFee, 0.20f, "pax_wifi");
            R("pax_art", "Passengers", "Art and Gardens", "The airport's rating grows 25% faster.", 600000, 90, 5, Effect.RatingGain, 0.25f, "pax_fast");
            R("pax_bio", "Passengers", "Walk-through Gates", "Boarding another 30% faster.", 7e6, 240, 7, Effect.BoardSpeed, 0.30f, "pax_art");
            // Commerce: more money from what you already do.
            R("com_fees", "Commerce", "Fee Schedule", "Landing fees up 10%.", 800, 2, 1, Effect.LandingFee, 0.10f);
            R("com_brands", "Commerce", "Brand Partners", "Shops earn 25% more.", 5000, 8, 2, Effect.Retail, 0.25f, "com_fees");
            R("com_fuel", "Commerce", "Fuel Hedging", "Fuel margin up 25%.", 20000, 20, 3, Effect.FuelMargin, 0.25f, "com_brands");
            R("com_duty", "Commerce", "Duty Free", "Shops earn another 50%.", 120000, 45, 4, Effect.Retail, 0.50f, "com_fuel");
            R("com_deals", "Commerce", "Volume Deals", "Everything costs 10% less to build.", 900000, 120, 5, Effect.BuildDiscount, 0.10f, "com_duty");
            R("com_group", "Commerce", "Airport Group", "All income up 25%.", 1.2e7, 300, 7, Effect.Income, 0.25f, "com_deals");
            R("com_brand", "Commerce", "A Name People Know", "All income up another 50%.", 2e8, 600, 9, Effect.Income, 0.50f, "com_group");
            // Management: the airport runs better without you.
            R("man_rota", "Management", "Night Rota", "Earn while away for 2 more hours.", 1500, 4, 2, Effect.OfflineHours, 2f);
            R("man_duty", "Management", "Duty Managers", "Earn 20% more of the normal rate while away.", 10000, 12, 3, Effect.OfflineEfficiency, 0.20f, "man_rota");
            R("man_shift", "Management", "Three Shifts", "Earn while away for 4 more hours.", 150000, 60, 4, Effect.OfflineHours, 4f, "man_duty");
            R("man_ops", "Management", "Operations Centre", "Earn another 20% of the normal rate while away.", 2e6, 180, 6, Effect.OfflineEfficiency, 0.20f, "man_shift");
            R("man_auto", "Management", "Self-running Airport", "Earn while away for 12 more hours.", 5e7, 480, 8, Effect.OfflineHours, 12f, "man_ops");

            void Map(string id, string name, string blurb, string twist, double income, double cost, int wings, float storm, float fog, float snow, int maxRunway = 20)
            {
                Maps.Add(new MapDef { Id = id, Name = name, Blurb = blurb, Twist = twist, IncomeScale = income, CostScale = cost, WingsToOpen = wings, Storm = storm, Fog = fog, Snow = snow, MaxRunway = maxRunway });
            }
            Map("meadow", "Meadowfield", "A grass strip, a shed and a debt: where it starts.", "Gentle weather while you learn.", 1, 1, 0, 0.05f, 0.05f, 0);
            Map("coral", "Coral Cay", "A holiday island with no room to spare.", "The runway can't pass level 12: no jumbos, but tourists spend double in the shops.", 4, 5, 3, 0.18f, 0.02f, 0, 12);
            Map("dune", "Dune Station", "A desert crossroads for the long-haul lines.", "Heat and sandstorms: storms come often, and fuel margins are half again as good.", 18, 30, 12, 0.3f, 0, 0);
            Map("alpine", "Hochtal", "A valley airport between two walls of rock.", "Fog most mornings and snow all winter.", 80, 200, 40, 0.1f, 0.3f, 0.25f);
            Map("polar", "Polar Gateway", "The top of the world, and the shortest way round it.", "Snow half the time. Airlines pay very well to land here at all.", 400, 1500, 120, 0.1f, 0.15f, 0.5f);
            Map("metro", "Meridian City", "The capital's airport. Every airline wants in.", "Twice the flights on every route.", 2500, 12000, 400, 0.12f, 0.1f, 0.05f);
        }
    }
}
