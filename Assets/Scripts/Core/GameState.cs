using System;
using System.Collections.Generic;

namespace Crosswind.Core
{
    /// <summary>Where a flight is in its visit. The world shows each phase; the simulation only needs the order.</summary>
    public enum Phase { Approach, Landing, TaxiIn, Deplane, Service, Board, TaxiOut, Takeoff, Gone }

    [Serializable]
    public sealed class LevelEntry { public string id; public int level; }

    [Serializable]
    public sealed class ContractState
    {
        public string route;
        public double nextArrival;      // game time of the next flight on this route
    }

    [Serializable]
    public sealed class FlightState
    {
        public int id;
        public string route;
        public Phase phase;
        public float timer;             // seconds left in the current timed phase
        public int stand = -1;
        public double arrivedAt;        // game time it joined the approach
        public double parkedAt;         // game time it reached the stand
        public List<ServiceJob> jobs = new List<ServiceJob>();
    }

    /// <summary>One service on one flight: waiting to be dispatched, under way, or done.</summary>
    [Serializable]
    public sealed class ServiceJob
    {
        public Service service;
        public bool dispatched;
        public float left;              // seconds of work left once a crew is on it
        public bool done;
    }

    [Serializable]
    public sealed class GoalProgress { public string id; public bool claimed; }

    /// <summary>The save. Plain fields and lists only, so Unity's JsonUtility can write it.</summary>
    [Serializable]
    public sealed class GameState
    {
        public int version = 1;
        public double time;                 // game seconds played at this airport
        public double savedAt;              // real time (unix seconds) of the last save, for offline earnings
        public double money;
        public int gems;
        public double ratingXp;
        public double lifetimeEarned;
        public int flightsHandled, onTime, late;
        public int nextFlightId = 1;
        public List<LevelEntry> levels = new List<LevelEntry>();
        public List<ContractState> contracts = new List<ContractState>();
        public List<FlightState> flights = new List<FlightState>();
        public List<string> managers = new List<string>();      // facility ids whose department runs itself
        public List<GoalProgress> goals = new List<GoalProgress>();
        public int goalIndex;
        public double incomeRate;           // smoothed income per game second, for the HUD and offline earnings
        public uint seed = 12345;

        public static GameState New()
        {
            var s = new GameState { money = Content.StartMoney };
            foreach (var id in Content.FacilityOrder)
                if (Content.Facilities[id].StartLevel > 0) s.levels.Add(new LevelEntry { id = id, level = Content.Facilities[id].StartLevel });
            s.contracts.Add(new ContractState { route = "puffin_isles", nextArrival = 2 });
            return s;
        }

        /// <summary>Fills in what an older or hand-edited save lacks.</summary>
        public void Repair()
        {
            if (levels == null) levels = new List<LevelEntry>();
            if (contracts == null) contracts = new List<ContractState>();
            if (flights == null) flights = new List<FlightState>();
            if (managers == null) managers = new List<string>();
            if (goals == null) goals = new List<GoalProgress>();
            contracts.RemoveAll(c => c == null || !Content.Routes.ContainsKey(c.route));
            flights.RemoveAll(f => f == null || !Content.Routes.ContainsKey(f.route));
            foreach (var f in flights) if (f.jobs == null) f.jobs = new List<ServiceJob>();
            levels.RemoveAll(l => l == null || !Content.Facilities.ContainsKey(l.id));
            foreach (var id in Content.FacilityOrder)
            {
                var d = Content.Facilities[id];
                if (d.StartLevel > 0 && !levels.Exists(l => l.id == id)) levels.Add(new LevelEntry { id = id, level = d.StartLevel });
            }
            if (nextFlightId < 1) nextFlightId = 1;
            if (seed == 0) seed = 12345;
        }
    }
}
