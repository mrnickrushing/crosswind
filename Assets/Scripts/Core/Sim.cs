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
            // A save from before a service existed keeps flying the routes it signed.
            foreach (var c in State.contracts)
                foreach (var n in Content.Routes[c.route].Needs)
                    if (Level(Content.ServiceFacility(n)) == 0) SetLevel(Content.ServiceFacility(n), 1);
            // A new airport, or one from before the fuel market, opens with full tanks.
            if (State.fuelStock < 0) State.fuelStock = FuelCapacity;
            // And one from before wide-bodies needed their own stand keeps the stands it was already using for them.
            if (HeavyStands == 0 && State.contracts.Exists(c => Heavy(Content.Routes[c.route].Class))) SetLevel("heavy", Math.Min(StandCount, 2));
            // And one from when the terminal was a long strip has its furniture moved onto the hall's floor.
            EnsureBays();
            RefitTerminal();
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

        /// <summary>The apron is one row at the terminal: six stands at most.</summary>
        public const int MaxStands = 6;
        public int StandCount { get { return Math.Min(MaxStands, Level("stands")); } }
        /// <summary>The first aircraft class that needs a wide-body stand.</summary>
        public static bool Heavy(AircraftClass c) { return c >= AircraftClass.D; }
        /// <summary>Stands rebuilt for wide-bodies: the first so many of the row, counted from the south end.</summary>
        public int HeavyStands { get { return Math.Min(StandCount, Level("heavy")); } }
        public bool HeavyStand(int stand) { return stand >= 0 && stand < HeavyStands; }

        /// <summary>What a route is still waiting for before it can be signed, as facilities to build (rating aside).</summary>
        public IEnumerable<string> Missing(RouteDef r)
        {
            if (Level("runway") < Content.Aircraft[r.Class].RunwayLevel) yield return "runway";
            if (Heavy(r.Class) && HeavyStands == 0) yield return "heavy";
            foreach (var n in r.Needs) if (Level(Content.ServiceFacility(n)) == 0) yield return Content.ServiceFacility(n);
        }

        /// <summary>
        /// Past six stands, an upgrade equips the ones there are (more ground power, a second bridge, a tug on call):
        /// everything on the ground goes faster, as much as the extra stand would have added.
        /// </summary>
        public float StandBoost { get { return 1 + Math.Max(0, Level("stands") - MaxStands) / (float)MaxStands; } }
        /// <summary>Aircraft allowed on approach and on the ground together.</summary>
        public int TowerSlots { get { return 2 + Level("tower") * 2; } }
        /// <summary>Movements the runway handles at once (a second one from tower level 8: a parallel runway).</summary>
        public int RunwayLanes { get { return Level("tower") >= 8 ? 2 : 1; } }
        public float RunwaySpeed { get { return (1 + (Level("runway") - 1) * 0.06f) * (1 + Fx(Effect.RunwaySpeed)) * WeatherFactor * SurfaceFactor; } }
        public float TaxiSpeed { get { return (1 + Level("taxiways") * 0.08f) * (1 + Fx(Effect.TaxiSpeed)) * (0.5f + 0.5f * WeatherFactor) * StandBoost; } }
        public int Crews(Service s) { int l = Level(Content.ServiceFacility(s)); return l <= 0 ? 0 : 1 + (l - 1) / 3; }
        public float CrewSpeed(Service s)
        {
            int l = Level(Content.ServiceFacility(s));
            return l <= 0 ? 0 : (1 + (l - 1) * 0.1f) * (1 + Fx(Effect.CrewSpeed)) * (1 + 0.1f * ManagerLevel(Content.ServiceFacility(s))) * StandBoost;
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
            if (facility == "deice" && Map.Snow <= 0) return "It never snows at " + Map.Name;
            if (facility == "heavy" && Level(facility) >= StandCount) return "Build another stand first";
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
            AddRating(2 + Level(facility) + Math.Max(0, Math.Log10(Math.Max(1, Cost(facility) / Map.CostScale)) - 3) * 12);
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
            if (Heavy(r.Class) && HeavyStands == 0) return "Needs a Wide-body Stand for the " + plane.Name;
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
            bool shut = RunwayClosed;
            State.time += dt;
            if (shut && !RunwayClosed) RunwayReopened?.Invoke();
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
            ContractArrivals();
            foreach (var c in State.contracts)
            {
                if (State.time < c.nextArrival) continue;
                var r = Content.Routes[c.route];
                if (!Active(r.Airline)) { c.nextArrival = State.time + 5; continue; }       // told to stay away for now
                float pace = ScheduleRate(c);
                if (pace <= 0) { c.nextArrival = State.time + UntilSlot(c.slot) + Rand() * 4; continue; }     // outside its slot: back when it opens
                double gap = 3600.0 / (ListedRate(c) * pace);
                // Never more waiting than the tower can watch: a full pattern sends the next one round again.
                if (State.flights.Count >= TowerSlots) { c.nextArrival = State.time + Math.Min(gap, 5); continue; }
                c.nextArrival = State.time + gap * (0.85 + Rand() * 0.3);
                var f = new FlightState { id = State.nextFlightId++, route = r.Id, phase = Phase.Approach, arrivedAt = State.time, slot = c.slot };
                f.number = 100 + (Hash(r.Id) % 80 * 10 + f.id % 10) % 900;
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
                    if (runwayBusy >= RunwayLanes || RunwayClosed) return;
                    int stand = f.contract != 0 ? BookedStand(f, r.Class) : FreeStand(r.Class);
                    if (stand < 0) return;
                    f.stand = stand;
                    runwayBusy++;
                    Enter(f, Phase.Landing, plane.RunwaySeconds / RunwaySpeed);
                    WearRunway(r.Class);
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
                    foreach (var n in r.Needs) if (!Content.OnDeparture(n)) f.jobs.Add(new ServiceJob { service = n, left = plane.ServiceSeconds });
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
                    if (!f.boarded)
                    {
                        if (!Count(f, dt)) return;
                        f.boarded = true;
                        // Doors closed. In snow it is sprayed first (with a pad to do it); a jet then needs its tug.
                        if (Weather == Weather.Snow && Crews(Service.Deice) > 0) f.jobs.Add(new ServiceJob { service = Service.Deice, left = plane.ServiceSeconds * Content.ServiceShare(Service.Deice) });
                        if (r.Needs.Contains(Service.Pushback)) f.jobs.Add(new ServiceJob { service = Service.Pushback, left = plane.ServiceSeconds * Content.ServiceShare(Service.Pushback) });
                        FlightChanged?.Invoke(f);
                        Changed?.Invoke();
                    }
                    // One at a time, in order: the spray, then the tug.
                    var next = f.jobs.Find(j => !j.done);
                    if (next != null)
                    {
                        if (next.service == Service.Deice) f.held += dt;      // the weather's delay, not the airport's
                        if (!next.dispatched) { if (Automated(next.service) && CrewFree(next.service)) next.dispatched = true; else return; }
                        next.left -= dt * CrewSpeed(next.service);
                        if (next.left <= 0) { next.done = true; Changed?.Invoke(); }
                        return;
                    }
                    // Ready. With stands to spare it keeps its gate a while (a full apron, not an empty one); the
                    // moment the last free stand is taken, the one that has waited longest goes.
                    if (f.held < HoldLimit && StandsFree > 0 && !(HeavyStand(f.stand) && WideBodyWaiting)) { f.held += dt; return; }
                    Enter(f, Phase.TaxiOut, plane.TaxiSeconds / TaxiSpeed);
                    return;
                case Phase.TaxiOut:
                    if (f.timer > 0) { f.timer -= dt; return; }
                    if (runwayBusy >= RunwayLanes || RunwayClosed) return;         // holding short
                    runwayBusy++;
                    f.stand = -1;
                    Enter(f, Phase.Takeoff, plane.RunwaySeconds / RunwaySpeed);
                    WearRunway(r.Class);
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

        /// <summary>How long a finished flight may keep its gate: nothing with one stand, longer with more.</summary>
        public float HoldLimit { get { return Content.GateHold * (StandCount - 1); } }
        /// <summary>Stands empty or about to be (an aircraft taxiing out still holds its own until it takes off).</summary>
        int StandsFree { get { return StandCount - State.flights.Count(f => f.stand >= 0 && f.phase != Phase.TaxiOut); } }

        /// <summary>A wide-body is circling for a stand only it can use: nobody lingers on one.</summary>
        public bool WideBodyWaiting { get { return State.flights.Exists(f => f.phase == Phase.Approach && Heavy(Content.Routes[f.route].Class)); } }

        /// <summary>Wide-bodies take a wide-body stand or wait; everything else fills the ordinary stands first.</summary>
        int FreeStand(AircraftClass c)
        {
            int heavy = HeavyStands;
            // A stand a contract flight is about to need is left alone while another will do.
            for (int pass = State.bookings.Count > 0 ? 0 : 1; pass < 2; pass++)
            {
                if (!Heavy(c))
                    for (int i = heavy; i < StandCount; i++)
                        if (!State.flights.Exists(f => f.stand == i) && (pass == 1 || !Reserved(i))) return i;
                for (int i = 0; i < heavy; i++)
                    if (!State.flights.Exists(f => f.stand == i) && (pass == 1 || !Reserved(i))) return i;
            }
            return -1;
        }

        /// <summary>Deplaning or boarding: quick when the terminal keeps up, slow when the aircraft outgrows it.</summary>
        float BoardTime(AircraftDef plane)
        {
            float need = plane.Seats / Math.Max(1f, TerminalFlow) * 60f / 10f;   // seconds for the terminal to move them
            return Math.Max(plane.BoardSeconds, need) / (1 + Fx(Effect.BoardSpeed)) / StandBoost;
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
                    if (f.phase == Phase.Service) { foreach (var j in f.jobs) if (!j.dispatched && !j.done) yield return (f, j); }
                    else if (f.phase == Phase.Board && f.boarded) { var j = f.jobs.Find(x => !x.done); if (j != null && !j.dispatched) yield return (f, j); }
            }
        }

        /// <summary>The player sends a crew: false when every crew of that service is busy.</summary>
        public bool Dispatch(int flightId, Service s)
        {
            var f = State.flights.Find(x => x.id == flightId);
            if (f == null || (f.phase != Phase.Service && !(f.phase == Phase.Board && f.boarded))) return false;
            var j = f.jobs.Find(x => x.service == s && !x.dispatched && !x.done);
            if (j == null) return false;
            if (f.phase == Phase.Board && f.jobs.Find(x => !x.done) != j) return false;      // the spray comes before the tug
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
