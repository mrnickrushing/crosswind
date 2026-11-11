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
        public int terms;               // 0 standard, 1 busy (more flights, less patience), 2 premium (fewer flights, pays more)
        public double nextArrival;      // game time of the next flight on this route
        public int slot;                // the planner: 0 all day, 1 morning, 2 afternoon, 3 evening, 4 night
    }

    [Serializable]
    public sealed class FlightState
    {
        public int id;
        public string route;
        public Phase phase;
        public float timer;             // seconds left in the current timed phase
        public int stand = -1;
        public int slot;                // the planner slot it was scheduled in (its pay follows it)
        public double arrivedAt;        // game time it joined the approach
        public double parkedAt;         // game time it reached the stand
        public bool boarded;            // everyone is aboard: what is left is de-icing, the tug, and the wait for its slot
        public float held;              // seconds not counted against it: waiting at the gate by choice, and de-icing
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

    /// <summary>A research project under way (real time, so it finishes while the app is closed).</summary>
    [Serializable]
    public sealed class ResearchJob { public string id; public double doneAt; }

    /// <summary>A build or an upgrade under way (real time).</summary>
    [Serializable]
    public sealed class BuildJob { public string facility; public double doneAt, total; }

    /// <summary>Something that turns up for a short while and pays if you take it: a rush, a charter, a lucky find.</summary>
    [Serializable]
    public sealed class Opportunity { public string kind; public double expiresAt; public double value; }

    /// <summary>One of today's tasks: count something, then claim the gems.</summary>
    [Serializable]
    public sealed class TaskState { public string kind; public double target, progress; public int gems; public bool claimed; }

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

        // Progress beyond one airport.
        public string map = "meadow";
        public int wings;                                           // kept for good: each adds to all income
        public List<string> mapsRun = new List<string>();           // airports already sold on
        public List<string> researched = new List<string>();
        public ResearchJob research;                                // JsonUtility writes an empty one for null: see Repair
        public List<LevelEntry> managerLevels = new List<LevelEntry>();
        public double boostUntil;
        public double freeBoostAt;                                  // real time the next free boost is ready                                   // real time the 2x income boost ends
        public List<string> achievements = new List<string>();     // claimed
        public List<string> owned = new List<string>();            // one-time purchases
        public List<string> transactions = new List<string>();     // store transactions already granted
        public bool tutorialDone;

        public List<LevelEntry> relations = new List<LevelEntry>();   // each airline's satisfaction with you, 0..100 (starts at 50)

        public List<LevelEntry> fleet = new List<LevelEntry>();      // your own aircraft: id = class letter ("a".."e"), level = how many
        public int livery;                                           // your airline's colour (an index into Sim.Liveries)

        // The live layer.
        public int weather;                                         // Weather, as a number
        public double weatherUntil;                                 // game time it changes
        public Opportunity opportunity;
        public double nextOpportunityAt = 210;                      // game time
        public double rushUntil;                                    // game time a rush (double pay) ends
        public int taskDay;                                         // the day today's tasks belong to
        public List<TaskState> tasks = new List<TaskState>();
        public int upgradesBought;
        public List<BuildJob> builds = new List<BuildJob>();
        public int signInDay = -1, signInStreak;
        public int eventWeek = -1, eventClaimed;                    // the week the progress belongs to, and milestones claimed
        public double eventProgress;                    // the last day claimed, and days in a row
        public double sessionEarned;
        public List<LevelEntry> crew = new List<LevelEntry>();       // the team: id and copies held
        public List<string> duty = new List<string>();               // who is on duty, in order
        public int crates = 1, cratesOpened, sinceEpic;               // unopened crates (one to start), and the count toward a sure Epic
        public List<PlacedAmenity> terminal = new List<PlacedAmenity>();   // what stands on the concourse floor
        public string playerName = "";                             // what friends see (empty: "Airport Manager")
        public List<ResearchJob> gifts = new List<ResearchJob>();    // friend code and when their gift was last collected (real time)
        public int feeLevel = 2;                                     // fee policy, 0 bargain .. 4 premium
        public double loanOwed, loanTotal;                           // what the bank is still owed, and what was owed at the start
        public double[] incomeBy = new double[5];                    // lifetime income here by source: landing, passengers, fuel, shops, cargo
        public int plansMade;                                        // times the day was planned (by AUTO PLAN or by hand)
        public double freeCrateAt;                                   // real time the next free crate is ready

        public static GameState New()
        {
            var s = new GameState { money = Content.StartMoney, time = 60 };      // a new airport opens at eight in the morning
            foreach (var id in Content.FacilityOrder)
                if (Content.Facilities[id].StartLevel > 0) s.levels.Add(new LevelEntry { id = id, level = Content.Facilities[id].StartLevel });
            s.contracts.Add(new ContractState { route = "puffin_isles", nextArrival = 62 });
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
            if (string.IsNullOrEmpty(map) || !Content.Maps.Exists(m => m.Id == map)) map = "meadow";
            if (mapsRun == null) mapsRun = new List<string>();
            if (researched == null) researched = new List<string>();
            researched.RemoveAll(r => Content.ResearchById(r) == null);
            if (research != null && (string.IsNullOrEmpty(research.id) || Content.ResearchById(research.id) == null)) research = null;
            if (managerLevels == null) managerLevels = new List<LevelEntry>();
            if (achievements == null) achievements = new List<string>();
            if (owned == null) owned = new List<string>();
            if (transactions == null) transactions = new List<string>();
            if (relations == null) relations = new List<LevelEntry>();
            if (fleet == null) fleet = new List<LevelEntry>();
            if (opportunity != null && string.IsNullOrEmpty(opportunity.kind)) opportunity = null;
            if (tasks == null) tasks = new List<TaskState>();
            if (builds == null) builds = new List<BuildJob>();
            builds.RemoveAll(b => b == null || b.facility == null || !Content.Facilities.ContainsKey(b.facility));
            tasks.RemoveAll(x => x == null || string.IsNullOrEmpty(x.kind));
            if (terminal == null) terminal = new List<PlacedAmenity>();
            terminal.RemoveAll(t => t == null || Sim.AmenityById(t.id) == null);
            foreach (var t in terminal) if (t.level < 1) t.level = 1;
            if (playerName == null) playerName = "";
            if (gifts == null) gifts = new List<ResearchJob>();
            gifts.RemoveAll(g => g == null || string.IsNullOrEmpty(g.id));
            if (incomeBy == null || incomeBy.Length != 5) incomeBy = new double[5];
            if (crew == null) crew = new List<LevelEntry>();
            crew.RemoveAll(c => c == null || Sim.CrewById(c.id) == null);
            if (duty == null) duty = new List<string>();
            duty.RemoveAll(d => !crew.Exists(c => c.id == d));
            if (seed == 0) seed = 12345;
        }
    }
}
