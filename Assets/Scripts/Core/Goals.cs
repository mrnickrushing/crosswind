using System;
using System.Collections.Generic;

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
            State.money += g.Money;
            State.lifetimeEarned += g.Money;
            State.gems += g.Gems;
            State.goalIndex++;
            AddRating(8);
            Changed?.Invoke();
            return true;
        }
    }
}
