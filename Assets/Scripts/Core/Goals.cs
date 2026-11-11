using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>One step of the guide: what to do, how far along it is, and what finishing it pays.</summary>
    public sealed class GoalDef
    {
        public string Id, Text;
        public Func<Sim, double> Progress;
        public double Target;
        public double Money;
        public int Gems;
    }

    /// <summary>The guided chain: it always says what to do next, and each step pays when claimed.</summary>
    public static class Goals
    {
        public static readonly List<GoalDef> All = new List<GoalDef>();

        static Goals()
        {
            void G(string id, string text, double target, Func<Sim, double> progress, double money, int gems = 0)
            {
                All.Add(new GoalDef { Id = id, Text = text, Target = target, Progress = progress, Money = money, Gems = gems });
            }
            G("first_flight", "Turn round your first flight: tap FUEL when it parks", 1, s => s.State.flightsHandled, 100);
            G("three_flights", "Handle 3 flights", 3, s => s.State.flightsHandled, 150);
            G("second_stand", "Build a second aircraft stand", 2, s => s.Level("stands"), 200);
            G("second_route", "Sign a second route (AIRLINES)", 2, s => s.State.contracts.Count, 300);
            G("fuel_2", "Upgrade the fuel depot to level 2", 2, s => s.Level("fuel"), 300);
            G("fuel_manager", "Hire a fuel manager: the depot runs itself", 1, s => s.State.managers.Contains("fuel") ? 1 : 0, 500, 5);
            G("two_stars", "Reach a 2-star airport", 2, s => s.Stars, 800, 5);
            G("baggage", "Build baggage handling", 1, s => s.Level("baggage"), 600);
            G("runway_3", "Lengthen the runway to level 3 for the Heron 50", 3, s => s.Level("runway"), 1500);
            G("regional", "Sign a Heron 50 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class == AircraftClass.B) ? 1 : 0, 2500, 10);
            G("fifty_flights", "Handle 50 flights", 50, s => s.State.flightsHandled, 4000);
            G("shops", "Open the shops and cafes", 1, s => s.Level("shops"), 3000);
            G("three_stars", "Reach a 3-star airport", 3, s => s.Stars, 10000, 10);
            G("baggage_manager", "Hire a baggage manager", 1, s => s.State.managers.Contains("baggage") ? 1 : 0, 8000, 5);
            G("runway_6", "Build a level 6 runway for the Albatross 160", 6, s => s.Level("runway"), 40000);
            G("jet", "Sign an Albatross 160 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class == AircraftClass.C) ? 1 : 0, 80000, 15);
            G("four_stars", "Reach a 4-star airport", 4, s => s.Stars, 250000, 20);
            G("own_plane", "Buy your first aircraft (AIRLINES, Your Airline)", 1, s => s.FleetSize, 100000, 15);
            G("research_3", "Finish 3 research projects (RESEARCH)", 3, s => s.State.researched.Count, 60000, 10);
            G("catering", "Build the flight kitchen", 1, s => s.Level("catering"), 80000);
            G("ils", "Install landing aids for bad weather", 1, s => s.Level("ils"), 60000);
            G("routes_8", "Fly 8 routes", 8, s => s.State.contracts.Count, 200000, 10);
            G("managers_3", "Have 3 managers", 3, s => s.State.managers.Count, 150000, 10);
            G("flights_500", "Handle 500 flights", 500, s => s.State.flightsHandled, 400000);
            G("parking", "Open the car park and transit", 1, s => s.Level("parking"), 100000);
            G("stands_8", "Have 8 aircraft stands", 8, s => s.Level("stands"), 600000, 10);
            G("five_stars", "Reach a 5-star airport", 5, s => s.Stars, 1500000, 25);
            G("runway_10", "Build a level 10 runway for the Condor 300", 10, s => s.Level("runway"), 3000000);
            G("widebody", "Sign a Condor 300 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class == AircraftClass.D) ? 1 : 0, 6000000, 20);
            G("train_3", "Train a manager to level 3 (STAFF)", 3, s => s.State.managers.Count == 0 ? 0 : s.State.managers.Max(m => s.ManagerLevel(m)), 4000000, 10);
            G("hangar", "Build the maintenance hangar", 1, s => s.Level("hangar"), 5000000);
            G("research_10", "Finish 10 research projects", 10, s => s.State.researched.Count, 8000000, 20);
            G("six_stars", "Reach a 6-star airport", 6, s => s.Stars, 2.5e7, 30);
            G("cargo", "Open the cargo terminal", 1, s => s.Level("cargo"), 3e7);
            G("flights_5000", "Handle 5,000 flights", 5000, s => s.State.flightsHandled, 8e7, 20);
            G("seven_stars", "Reach a 7-star airport", 7, s => s.Stars, 2e8, 40);
            G("jumbo", "Sign a Leviathan 520 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class == AircraftClass.E) ? 1 : 0, 1e9, 50);
            G("sell", "Sell your stake and open a second airport (EMPIRE)", 1, s => s.State.mapsRun.Count, 0, 100);
        }
    }

    public sealed partial class Sim
    {
        /// <summary>The goal on the card, or null when the chain is finished.</summary>
        public GoalDef CurrentGoal { get { return State.goalIndex < Goals.All.Count ? Goals.All[State.goalIndex] : null; } }

        public double GoalProgress { get { var g = CurrentGoal; return g == null ? 0 : Math.Min(g.Target, g.Progress(this)); } }
        public bool GoalDone { get { var g = CurrentGoal; return g != null && g.Progress(this) >= g.Target; } }

        /// <summary>Pays the finished goal and moves to the next.</summary>
        public bool ClaimGoal()
        {
            if (!GoalDone) return false;
            var g = CurrentGoal;
            Earn(g.Money * Map.IncomeScale, false);
            State.gems += g.Gems;
            State.goalIndex++;
            AddRating(8);
            Changed?.Invoke();
            return true;
        }
    }
}
