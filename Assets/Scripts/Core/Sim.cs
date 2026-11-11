using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// The airport, simulated. Flights arrive on each signed route, wait for a runway and a stand, land, taxi in,
    /// let their passengers off, take the services their airline contracted (each needs a free crew: dispatched by a
    /// tap, or by itself once the department has a manager), board, taxi out and take off. Leaving pays: a landing fee
    /// (more when on time), a charge per passenger, a margin on fuel and what the passengers spent in the shops.
    /// No Unity types: the same code runs in the game, in the tests and for offline earnings.
    /// </summary>
    public sealed partial class Sim
    {
        public readonly GameState State;

        /// <summary>A flight changed phase (the world moves its aircraft).</summary>
        public event Action<FlightState> FlightChanged;
        /// <summary>A flight left and paid: the flight, what it paid, and whether it was on time.</summary>
        public event Action<FlightState, double, bool> FlightPaid;
        /// <summary>The airport gained a star.</summary>
        public event Action<int> StarGained;
        public event Action Changed;
        public event Action<string> Toast;

        /// <summary>Real time in unix seconds (research, boosts and the day's tasks run on it; tests replace it).</summary>
        public Func<double> RealNow = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        public Sim(GameState state)
        {
            State = state;
            State.Repair();
        }

        // ------------------------------------------------------------------ levels and what they mean

        public int Level(string facility)
        {
            var e = State.levels.Find(l => l.id == facility);
            return e != null ? e.level : 0;
        }

        void SetLevel(string facility, int level)
        {
            var e = State.levels.Find(l => l.id == facility);
            if (e == null) State.levels.Add(new LevelEntry { id = facility, level = level }); else e.level = level;
        }

        public int Stars
        {
            get
            {
                int s = 1;
                while (s < Content.MaxStars && State.ratingXp >= Content.StarXp[s + 1]) s++;
                return s;
            }
        }

        /// <summary>Progress to the next star, 0..1 (1 at the top).</summary>
        public float StarProgress
        {
            get
            {
                int s = Stars;
                if (s >= Content.MaxStars) return 1;
                double lo = Content.StarXp[s], hi = Content.StarXp[s + 1];
                return (float)Math.Max(0, Math.Min(1, (State.ratingXp - lo) / (hi - lo)));
            }
        }

        public int StandCount { get { return Level("stands"); } }
        /// <summary>Aircraft allowed on approach and on the ground together.</summary>
        public int TowerSlots { get { return 2 + Level("tower") * 2; } }
        /// <summary>Movements the runway handles at once (a second one from tower level 8: a parallel runway).</summary>
        public int RunwayLanes { get { return Level("tower") >= 8 ? 2 : 1; } }
        public float RunwaySpeed { get { return (1 + (Level("runway") - 1) * 0.06f) * (1 + Fx(Effect.RunwaySpeed)) * WeatherFactor; } }
        public float TaxiSpeed { get { return (1 + Level("taxiways") * 0.08f) * (1 + Fx(Effect.TaxiSpeed)) * (0.5f + 0.5f * WeatherFactor); } }
        public int Crews(Service s) { int l = Level(Content.ServiceFacility(s)); return l <= 0 ? 0 : 1 + (l - 1) / 3; }
        public float CrewSpeed(Service s)
        {
            int l = Level(Content.ServiceFacility(s));
            return l <= 0 ? 0 : (1 + (l - 1) * 0.1f) * (1 + Fx(Effect.CrewSpeed)) * (1 + 0.1f * ManagerLevel(Content.ServiceFacility(s)));
        }
        public bool Automated(Service s) { return State.managers.Contains(Content.ServiceFacility(s)); }

        /// <summary>Passengers a minute the terminal can move, set by its slowest part.</summary>
        public float TerminalFlow { get { return 6f * Math.Min(Level("checkin"), Math.Min(Level("security"), Level("gates"))) + 4; } }

        /// <summary>The biggest aircraft the runway takes.</summary>
        public AircraftClass MaxClass
        {
            get
            {
                var best = AircraftClass.A;
                foreach (var a in Content.Aircraft.Values) if (Level("runway") >= a.RunwayLevel && a.Class > best) best = a.Class;
                return best;
            }
        }

        // ------------------------------------------------------------------ building

        public double Cost(string facility)
        {
            var d = Content.Facilities[facility];
            return Math.Floor(d.BaseCost * Math.Pow(d.Growth, Level(facility) - d.StartLevel) * Map.CostScale * (1 - Math.Min(0.6, Fx(Effect.BuildDiscount))));
        }

        /// <summary>Why this can't be built or upgraded right now (null: it can).</summary>
        public string UpgradeBlocked(string facility)
        {
            FacilityDef d;
            if (!Content.Facilities.TryGetValue(facility, out d)) return "Unknown";
            if (Level(facility) >= d.MaxLevel) return "Fully upgraded";
            if (facility == "runway" && Level(facility) >= Map.MaxRunway) return "No room for a longer runway at " + Map.Name;
            if (Stars < d.Stars) return "Needs a " + d.Stars + "-star airport";
            if (Building(facility) != null) return "Being built";
            if (State.builds.Count >= Builders && BuildSeconds(facility) > 0) return "Every building crew is busy";
            if (State.money < Cost(facility)) return "Need " + Format.Money(Cost(facility) - State.money) + " more";
            return null;
        }

        /// <summary>How many builds can run at once.</summary>
        public int Builders { get { return Owns("crosswind.starter") ? 3 : 2; } }
        /// <summary>Tests set this to 0 to build at once.</summary>
        public float BuildTimeScale = 1;

        public BuildJob Building(string facility) { return State.builds.Find(b => b.facility == facility); }

        /// <summary>How long this build takes: nothing for small jobs, then longer with every tenfold of its cost.</summary>
        public float BuildSeconds(string facility)
        {
            double baseCost = Cost(facility) / Map.CostScale;
            if (baseCost < 4000) return 0;
            return (float)Math.Min(1800, (Math.Log10(baseCost) - 3.3) * 40) * BuildTimeScale;
        }

        /// <summary>Pays for a build or an upgrade. Small ones are done at once; big ones take a crew and some time.</summary>
        public bool Upgrade(string facility)
        {
            if (UpgradeBlocked(facility) != null) return false;
            float seconds = BuildSeconds(facility);
            State.money -= Cost(facility);
            State.upgradesBought++;
            Count("upgrade", 1);
            if (seconds <= 0) FinishBuild(facility);
            else State.builds.Add(new BuildJob { facility = facility, doneAt = RealNow() + seconds, total = seconds });
            Changed?.Invoke();
            return true;
        }

        void FinishBuild(string facility)
        {
            SetLevel(facility, Level(facility) + 1);
            AddRating(2 + Level(facility));
            BuildDone?.Invoke(facility);
        }

        /// <summary>A build finished (the world puts up dust, the HUD says so).</summary>
        public event Action<string> BuildDone;

        void Builds()
        {
            for (int i = State.builds.Count - 1; i >= 0; i--)
            {
                if (RealNow() < State.builds[i].doneAt) continue;
                string f = State.builds[i].facility;
                State.builds.RemoveAt(i);
                FinishBuild(f);
                Toast?.Invoke(Content.Facilities[f].Name + " is ready: level " + Level(f));
                Changed?.Invoke();
            }
        }

        public int BuildSkipGems(BuildJob b) { return Math.Max(1, (int)Math.Ceiling((b.doneAt - RealNow()) / 60)); }

        public bool SkipBuild(string facility)
        {
            var b = Building(facility);
            if (b == null || State.gems < BuildSkipGems(b)) return false;
            State.gems -= BuildSkipGems(b);
            b.doneAt = RealNow();
            Builds();
            return true;
        }

        // ------------------------------------------------------------------ contracts

        public bool Signed(string route) { return State.contracts.Exists(c => c.route == route); }

        /// <summary>Why a route can't be signed (null: it can).</summary>
        public string SignBlocked(string route)
        {
            RouteDef r;
            if (!Content.Routes.TryGetValue(route, out r)) return "Unknown";
            if (Signed(route)) return "Already signed";
            if (Stars < r.Stars) return "Needs a " + r.Stars + "-star airport";
            var plane = Content.Aircraft[r.Class];
            if (Level("runway") < plane.RunwayLevel) return "Runway too short for the " + plane.Name + " (level " + plane.RunwayLevel + ")";
            foreach (var n in r.Needs) if (Crews(n) == 0) return "Needs " + Content.Facilities[Content.ServiceFacility(n)].Name;
            return null;
        }

        public bool Sign(string route, int terms = 0)
        {
            if (SignBlocked(route) != null) return false;
            var r = Content.Routes[route];
            State.contracts.Add(new ContractState { route = route, nextArrival = State.time + 3, terms = Math.Max(0, Math.Min(2, terms)) });
            Earn(r.SigningBonus * Map.IncomeScale, false);
            AddRating(10 * r.Stars);
            Changed?.Invoke();
            return true;
        }

        public IEnumerable<RouteDef> Offers { get { return Content.Routes.Values.Where(r => !Signed(r.Id) && Stars >= r.Stars); } }

        // ------------------------------------------------------------------ the tick

        /// <summary>Advances the airport by <paramref name="dt"/> game seconds.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            // Big steps (a hitch, a fast-forward) are taken in slices so nothing skips a phase.
            while (dt > 0.5f) { Step(0.5f); dt -= 0.5f; }
            Step(dt);
        }

        void Step(float dt)
        {
            State.time += dt;
            Live(dt);
            Arrivals();
            int runwayBusy = State.flights.Count(f => f.phase == Phase.Landing || f.phase == Phase.Takeoff);
            // Departures go before arrivals: clearing the ground is what frees a stand.
            foreach (var f in State.flights.OrderBy(f => f.phase == Phase.TaxiOut ? 0 : 1).ThenBy(f => f.id).ToList())
                Advance(f, dt, ref runwayBusy);
            State.flights.RemoveAll(f => f.phase == Phase.Gone);
            State.incomeRate *= Math.Pow(0.5, dt / 60.0);       // the rate forgets with a one-minute half-life
        }

        void Arrivals()
        {
            foreach (var c in State.contracts)
            {
                if (State.time < c.nextArrival) continue;
                var r = Content.Routes[c.route];
                double gap = 3600.0 / (r.FlightsPerHour * (1 + Fx(Effect.Demand)) * TermsFlights(c.terms));
                // Never more waiting than the tower can watch: a full pattern sends the next one round again.
                if (State.flights.Count >= TowerSlots) { c.nextArrival = State.time + Math.Min(gap, 5); continue; }
                c.nextArrival = State.time + gap * (0.85 + Rand() * 0.3);
                var f = new FlightState { id = State.nextFlightId++, route = r.Id, phase = Phase.Approach, arrivedAt = State.time };
                State.flights.Add(f);
                FlightChanged?.Invoke(f);
            }
        }

        void Advance(FlightState f, float dt, ref int runwayBusy)
        {
            var r = Content.Routes[f.route];
            var plane = Content.Aircraft[r.Class];
            switch (f.phase)
            {
                case Phase.Approach:
                    if (runwayBusy >= RunwayLanes) return;
                    int stand = FreeStand();
                    if (stand < 0) return;
                    f.stand = stand;
                    runwayBusy++;
                    Enter(f, Phase.Landing, plane.RunwaySeconds / RunwaySpeed);
                    return;
                case Phase.Landing:
                    if (!Count(f, dt)) return;
                    Enter(f, Phase.TaxiIn, plane.TaxiSeconds / TaxiSpeed);
                    return;
                case Phase.TaxiIn:
                    if (!Count(f, dt)) return;
                    f.parkedAt = State.time;
                    Enter(f, Phase.Deplane, BoardTime(plane));
                    return;
                case Phase.Deplane:
                    if (!Count(f, dt)) return;
                    f.jobs.Clear();
                    foreach (var n in r.Needs) f.jobs.Add(new ServiceJob { service = n, left = plane.ServiceSeconds });
                    Enter(f, Phase.Service, 0);
                    return;
                case Phase.Service:
                    foreach (var j in f.jobs)
                    {
                        if (j.done) continue;
                        if (!j.dispatched) { if (Automated(j.service) && CrewFree(j.service)) j.dispatched = true; else continue; }
                        j.left -= dt * CrewSpeed(j.service);
                        if (j.left <= 0) { j.done = true; Changed?.Invoke(); }
                    }
                    if (f.jobs.All(j => j.done)) Enter(f, Phase.Board, BoardTime(plane));
                    return;
                case Phase.Board:
                    if (!Count(f, dt)) return;
                    Enter(f, Phase.TaxiOut, plane.TaxiSeconds / TaxiSpeed);
                    return;
                case Phase.TaxiOut:
                    if (f.timer > 0) { f.timer -= dt; return; }
                    if (runwayBusy >= RunwayLanes) return;         // holding short
                    runwayBusy++;
                    f.stand = -1;
                    Enter(f, Phase.Takeoff, plane.RunwaySeconds / RunwaySpeed);
                    return;
                case Phase.Takeoff:
                    if (!Count(f, dt)) return;
                    Pay(f);
                    Enter(f, Phase.Gone, 0);
                    return;
            }
        }

        bool Count(FlightState f, float dt) { f.timer -= dt; return f.timer <= 0; }

        void Enter(FlightState f, Phase p, float seconds)
        {
            f.phase = p;
            f.timer = seconds;
            FlightChanged?.Invoke(f);
        }

        int FreeStand()
        {
            for (int i = 0; i < StandCount; i++)
                if (!State.flights.Exists(f => f.stand == i)) return i;
            return -1;
        }

        /// <summary>Deplaning or boarding: quick when the terminal keeps up, slow when the aircraft outgrows it.</summary>
        float BoardTime(AircraftDef plane)
        {
            float need = plane.Seats / Math.Max(1f, TerminalFlow) * 60f / 10f;   // seconds for the terminal to move them
            return Math.Max(plane.BoardSeconds, need) / (1 + Fx(Effect.BoardSpeed));
        }

        // ------------------------------------------------------------------ services

        int Working(Service s) { return State.flights.Sum(f => f.jobs.Count(j => j.service == s && j.dispatched && !j.done)); }
        public bool CrewFree(Service s) { return Working(s) < Crews(s); }

        /// <summary>The flights with a service waiting for the player's tap (departments without a manager).</summary>
        public IEnumerable<(FlightState flight, ServiceJob job)> Waiting
        {
            get
            {
                foreach (var f in State.flights)
                    if (f.phase == Phase.Service)
                        foreach (var j in f.jobs) if (!j.dispatched && !j.done) yield return (f, j);
            }
        }

        /// <summary>The player sends a crew: false when every crew of that service is busy.</summary>
        public bool Dispatch(int flightId, Service s)
        {
            var f = State.flights.Find(x => x.id == flightId);
            if (f == null || f.phase != Phase.Service) return false;
            var j = f.jobs.Find(x => x.service == s && !x.dispatched && !x.done);
            if (j == null) return false;
            if (!CrewFree(s)) { Toast?.Invoke("Every " + Content.Facilities[Content.ServiceFacility(s)].Name.ToLower() + " crew is busy"); return false; }
            j.dispatched = true;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ randomness (in the save, so a run replays)

        double Rand()
        {
            uint x = State.seed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State.seed = x == 0 ? 12345u : x;
            return (State.seed & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
