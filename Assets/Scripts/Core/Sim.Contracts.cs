using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// A contract: so many flights of one aircraft for one airline, one a day in the part of the day it asks for.
    /// On offer it sits in the market; signed, it is planned onto a stand and an hour and flown until it is done.
    /// </summary>
    [Serializable]
    public sealed class Booking
    {
        public int id;
        public string airline;
        public int plane;               // AircraftClass
        public int kind;                // 0 charter (one flight), 1 series
        public int flights;             // how many it runs to
        public int part;                // the day-part it flies in (planner slots: 1 morning, 2 afternoon, 3 evening, 4 night)
        public int needs;               // services, as bits of Service
        public int grade;               // the lowest passenger grade the airline accepts (an index into Sim.Grades; 4: any)
        public int place;               // where it flies to (an index into Content.Places)
        public int number;              // its flight number
        public double reward;           // paid when it is done, at full marks
        public int rep;                 // reputation at full marks
        // Once signed.
        public int stand = -1, hour = -1;       // where and when it is planned (-1: not yet)
        public int flown, onTime, served, pleased;
        public int lastDay = -1;        // the airport day its last flight came in
        public bool airborne;           // one of its flights is here now
    }

    /// <summary>A finished contract, as the track record keeps it.</summary>
    [Serializable]
    public sealed class BookingRecord
    {
        public string airline;
        public int plane, kind, flights, number, day;
        public int onTime, served, pleased;
        public bool punctual, serviced, standards;      // the three marks
        public int rep;                 // reputation it earned
        public double paid;
        public bool dropped;            // given up before it was done
    }

    /// <summary>
    /// Contracts. Beside the routes that fly all day, airlines put single charters and series of flights on the
    /// market. Each is signed into a book of limited size, planned onto a stand and an hour of the day-part it asks
    /// for, and flown one flight a day. When the last has left it is marked three ways: punctual (four in five on
    /// time), serviced (never fuelled by an outside supplier) and standards (passengers at the airline's grade or
    /// better four times in five). The marks set how much of its reward and reputation is paid. Reputation with an
    /// airline is kept: it raises what all that airline's flights pay and brings longer, better contracts.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>A contract finished (or was dropped): its line in the track record.</summary>
        public event Action<BookingRecord> ContractDone;

        public const int ContractStars = 2;
        /// <summary>Real seconds between one set of offers and the next.</summary>
        public const double MarketSeconds = 2 * 3600;
        public const int RecordKept = 40;

        public bool ContractsOpen { get { return Stars >= ContractStars; } }
        /// <summary>How many contracts can be under way at once: the tower keeps the book.</summary>
        public int BookingCap { get { return 2 + Level("tower"); } }

        public Booking Booking(int id) { return State.bookings.Find(b => b.id == id); }
        public Booking OfferById(int id) { return State.market.Find(b => b.id == id); }
        public RouteDef RouteOf(Booking b) { return Content.ContractRoute(b.airline, (AircraftClass)b.plane, b.needs, b.place); }
        public static string KindName(Booking b) { return b.kind == 0 ? "CHARTER" : "SERIES"; }

        // ------------------------------------------------------------------ flight numbers and weekdays

        static int Hash(string s) { int h = 17; foreach (char c in s) h = unchecked(h * 31 + c); return h & 0x7FFFFFFF; }

        /// <summary>A flight's number as the board shows it: the airline's two letters and its digits.</summary>
        public string FlightNo(FlightState f)
        {
            var r = Content.Routes[f.route];
            int n = f.number > 0 ? f.number : 100 + (Hash(r.Id) % 80 * 10 + f.id % 10) % 900;
            return Content.Airlines[r.Airline].Code + " " + n;
        }

        public string FlightNo(Booking b) { return Content.Airlines[b.airline].Code + " " + b.number; }

        public static readonly string[] Weekdays = { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" };
        /// <summary>The day of the week at this airport (0 Monday .. 6 Sunday): it opened on a Monday.</summary>
        public int Weekday { get { return ((FuelDay % 7) + 7) % 7; } }
        public static bool Weekend(int weekday) { return weekday >= 5; }

        /// <summary>
        /// How an airline's routes fly on a day of the week against their listed pace: holiday airlines are busy at
        /// the weekend, business airlines in the week. Over a week each comes to its listed number of flights.
        /// </summary>
        public static float WeekFactor(string airline, int weekday)
        {
            switch (Content.Airlines[airline].Kind)
            {
                case 1: return Weekend(weekday) ? 1.3f : 0.88f;
                case 2: return Weekend(weekday) ? 0.7f : 1.12f;
                default: return 1f;
            }
        }

        // ------------------------------------------------------------------ reputation

        public static readonly int[] RepAt = { 0, 60, 200, 500, 1200, 3000 };
        public static readonly string[] RepName = { "New", "Known", "Trusted", "Preferred", "Favoured", "Flagship" };
        /// <summary>Each reputation tier adds this much to what every flight of that airline pays.</summary>
        public const double RepBonus = 0.03;

        public int Rep(string airline) { var e = State.rep.Find(l => l.id == airline); return e != null ? Math.Max(0, e.level) : 0; }
        public int RepTotal { get { return State.rep.Sum(l => Math.Max(0, l.level)); } }
        public int RepTier(string airline) { int v = Rep(airline), t = 0; while (t + 1 < RepAt.Length && v >= RepAt[t + 1]) t++; return t; }
        /// <summary>Points to the next tier (0 at the top).</summary>
        public int RepToNext(string airline) { int t = RepTier(airline); return t + 1 < RepAt.Length ? RepAt[t + 1] - Rep(airline) : 0; }
        public double RepPay(string airline) { return 1 + RepBonus * RepTier(airline); }

        void AddRep(string airline, int points)
        {
            int before = RepTier(airline);
            var e = State.rep.Find(l => l.id == airline);
            if (e == null) State.rep.Add(e = new LevelEntry { id = airline });
            e.level = Math.Max(0, e.level + points);
            int after = RepTier(airline);
            if (after > before) Toast?.Invoke(Content.Airlines[airline].Name + " now rates you " + RepName[after].ToLower() + ": their flights pay " + (int)Math.Round(RepBonus * 100 * after) + "% more");
        }

        // ------------------------------------------------------------------ airlines told to stay away

        public bool Active(string airline) { return !State.paused.Contains(airline); }

        /// <summary>Why an airline can't be paused (null: it can). One with flights here always has to stay.</summary>
        public string PauseBlocked(string airline)
        {
            if (airline == "own") return "Your own airline flies when you say";
            if (!State.contracts.Exists(c => Content.Routes[c.route].Airline != airline && Active(Content.Routes[c.route].Airline))) return "Keep at least one airline flying";
            return null;
        }

        /// <summary>Pauses an airline (its routes send nothing and it offers no contracts) or lets it back.</summary>
        public bool SetActive(string airline, bool on)
        {
            if (!Content.Airlines.ContainsKey(airline) || Active(airline) == on) return false;
            if (!on && PauseBlocked(airline) != null) return false;
            if (on) State.paused.Remove(airline); else State.paused.Add(airline);
            foreach (var c in State.contracts) if (Content.Routes[c.route].Airline == airline) c.nextArrival = State.time + 3;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ the market

        double MarketRand()
        {
            uint x = State.marketSeed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State.marketSeed = x == 0 ? 8675309u : x;
            return (State.marketSeed & 0xFFFFFF) / (double)0x1000000;
        }

        /// <summary>Airlines that know this airport: any with a route it could be offered by now.</summary>
        public IEnumerable<string> KnownAirlines
        {
            get { return Content.Airlines.Keys.Where(a => a != "own" && Content.RoutesOf(a).Any(r => !Content.IsContractRoute(r.Id) && r.Stars <= Stars)); }
        }

        /// <summary>Real seconds until the offers change.</summary>
        public double MarketIn { get { return Math.Max(0, State.marketAt - RealNow()); } }
        public int MarketGems { get { return Math.Max(1, (int)Math.Ceiling(MarketIn / 600)); } }

        /// <summary>New offers now, for gems.</summary>
        public bool RushMarket()
        {
            if (!ContractsOpen || State.gems < MarketGems) return false;
            State.gems -= MarketGems;
            RollMarket();
            return true;
        }

        void Market()
        {
            if (ContractsOpen && RealNow() >= State.marketAt) RollMarket();
        }

        /// <summary>Puts a new set of offers on the market: more of them, and longer ones, as the airport and its name grow.</summary>
        public void RollMarket()
        {
            State.market.Clear();
            State.marketAt = RealNow() + MarketSeconds;
            var lines = KnownAirlines.Where(Active).ToList();
            if (lines.Count == 0) { Changed?.Invoke(); return; }
            int want = Math.Min(8, 3 + Stars / 2);
            var top = MaxClass;
            for (int tries = 0; tries < 40 && State.market.Count < want; tries++)
            {
                string airline = lines[(int)(MarketRand() * lines.Count) % lines.Count];
                // An aircraft the airline flies and this runway takes.
                var flown = Content.RoutesOf(airline).Where(r => !Content.IsContractRoute(r.Id) && r.Stars <= Stars + 1 && r.Class <= top).ToList();
                if (flown.Count == 0) continue;
                var like = flown[(int)(MarketRand() * flown.Count) % flown.Count];
                int tier = RepTier(airline);
                var line = Content.Airlines[airline];
                var b = new Booking
                {
                    id = State.nextBookingId++, airline = airline, plane = (int)like.Class, needs = Content.NeedsMask(like.Needs), grade = line.Grade,
                    place = (int)(MarketRand() * Content.Places.Length), number = 1000 + (int)(MarketRand() * 9000),
                };
                b.kind = MarketRand() < 0.35 ? 0 : 1;
                b.flights = b.kind == 0 ? 1 : 3 + (int)(MarketRand() * 4) + tier;
                b.part = line.Likes[(int)(MarketRand() * line.Likes.Length) % line.Likes.Length];
                if (b.part == 4 && !NightOps) b.part = line.Likes.FirstOrDefault(p => p != 4);
                if (b.part <= 0) b.part = 1;
                b.reward = Math.Floor(ContractMinute * (b.kind == 0 ? CharterMinutes : SeriesMinutes * b.flights) * (1 + 0.1 * tier) * (0.7 + 0.3 * (b.plane + 1) / ((int)top + 1)));
                b.rep = b.kind == 0 ? 8 + 4 * b.plane : b.flights * (3 + b.plane);
                State.market.Add(b);
            }
            Changed?.Invoke();
        }

        /// <summary>A contract's reward, in minutes of what the airport earns: a charter's one flight, and each flight of a series.</summary>
        public const double CharterMinutes = 0.9, SeriesMinutes = 0.3;

        /// <summary>What a minute of this airport is worth now (a running boost is not in the price): rewards keep pace with the game.</summary>
        double ContractMinute { get { return Math.Max(40, Math.Max(State.incomeRate, IdleRate()) * 60 / (Boosted ? Content.BoostScale : 1)); } }

        /// <summary>Why an offer can't be signed (null: it can).</summary>
        public string SignContractBlocked(int id)
        {
            var b = OfferById(id);
            if (b == null) return "Gone";
            if (!ContractsOpen) return "Needs a " + ContractStars + "-star airport";
            if (State.bookings.Count >= BookingCap) return "Contract book full (" + State.bookings.Count + " of " + BookingCap + ")";
            if (!Active(b.airline)) return Content.Airlines[b.airline].Name + " is paused";
            var plane = Content.Aircraft[(AircraftClass)b.plane];
            if (Level("runway") < plane.RunwayLevel) return "Runway too short for the " + plane.Name;
            if (Heavy(plane.Class) && HeavyStands == 0) return "Needs a Wide-body Stand";
            foreach (var n in Content.NeedsOf(b.needs)) if (Crews(n) == 0) return "Needs " + Content.Facilities[Content.ServiceFacility(n)].Name;
            if (b.part == 4 && !NightOps) return "Night flights need runway lights";
            return null;
        }

        /// <summary>Signs an offer into the book and plans it into the quietest hour that is free.</summary>
        public bool SignContract(int id)
        {
            if (SignContractBlocked(id) != null) return false;
            var b = OfferById(id);
            State.market.Remove(b);
            State.bookings.Add(b);
            if (!AutoPlace(b)) Toast?.Invoke(FlightNo(b) + " has no free stand in its hours: plan it in OPS");
            AddRating(4 * (b.plane + 1));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Gives a contract up. The airline remembers: a quarter of what it would have earned comes off your reputation.</summary>
        public bool DropContract(int id)
        {
            var b = Booking(id);
            if (b == null) return false;
            State.bookings.Remove(b);
            foreach (var f in State.flights) if (f.contract == id) f.contract = 0;
            int lost = Math.Max(1, b.rep / 4);
            AddRep(b.airline, -lost);
            Relate(b.airline, -3);
            var rec = new BookingRecord { airline = b.airline, plane = b.plane, kind = b.kind, flights = b.flights, number = b.number, day = FuelDay + 1, onTime = b.onTime, served = b.served, pleased = b.pleased, rep = -lost, dropped = true };
            Keep(rec);
            ContractDone?.Invoke(rec);
            Changed?.Invoke();
            return true;
        }

        void Keep(BookingRecord rec)
        {
            State.record.Insert(0, rec);
            if (State.record.Count > RecordKept) State.record.RemoveRange(RecordKept, State.record.Count - RecordKept);
        }

        // ------------------------------------------------------------------ the plan

        /// <summary>How many hours of the day a stand is kept for one visit of this aircraft.</summary>
        public static int BlockHours(AircraftClass c) { return c <= AircraftClass.B ? 1 : c <= AircraftClass.D ? 2 : 3; }
        public static int PartStart(int part) { return SlotStart[Math.Max(0, Math.Min(4, part))]; }
        /// <summary>The last hour a contract in this day-part can be planned to start.</summary>
        public static int LastHour(Booking b) { return PartStart(b.part) + 6 - BlockHours((AircraftClass)b.plane); }

        /// <summary>The contract planned on this stand over this hour (null: nothing).</summary>
        public Booking PlannedAt(int stand, int hour, Booking ignore = null)
        {
            foreach (var o in State.bookings)
            {
                if (o == ignore || o.stand != stand || o.hour < 0) continue;
                if (hour >= o.hour && hour < o.hour + BlockHours((AircraftClass)o.plane)) return o;
            }
            return null;
        }

        /// <summary>Why this contract can't be planned on this stand at this hour (null: it can).</summary>
        public string PlanBlocked(int id, int stand, int hour)
        {
            var b = Booking(id);
            if (b == null) return "Unknown";
            if (stand < 0 || stand >= StandCount) return "No such stand";
            var c = (AircraftClass)b.plane;
            if (Heavy(c) && !HeavyStand(stand)) return "The " + Content.Aircraft[c].Name + " needs a wide-body stand";
            if (hour < PartStart(b.part) || hour > LastHour(b)) return "It flies in the " + SlotName[b.part].ToLower() + " (" + SlotHours[b.part] + ")";
            for (int h = hour; h < hour + BlockHours(c); h++)
            {
                var o = PlannedAt(stand, h, b);
                if (o != null) return "Stand " + (stand + 1) + " is kept for " + FlightNo(o) + " then";
            }
            return null;
        }

        public bool Plan(int id, int stand, int hour)
        {
            if (PlanBlocked(id, stand, hour) != null) return false;
            var b = Booking(id);
            if (b.stand == stand && b.hour == hour) return false;
            b.stand = stand; b.hour = hour;
            State.plansMade++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Plans one contract into the quietest hour of its day-part with a stand free (false: no room).</summary>
        public bool AutoPlace(Booking b)
        {
            var load = HourlyTraffic();
            int bestStand = -1, bestHour = -1; float best = float.MaxValue;
            var c = (AircraftClass)b.plane;
            for (int hour = PartStart(b.part); hour <= LastHour(b); hour++)
            {
                float busy = load[hour] + 3 * State.bookings.Count(o => o != b && o.hour == hour);
                if (busy >= best) continue;
                // Small aircraft leave the wide-body stands for those that need them.
                for (int k = 0; k < StandCount; k++)
                {
                    int stand = Heavy(c) ? k : (k + HeavyStands) % StandCount;
                    if (PlanBlocked(b.id, stand, hour) != null) continue;
                    best = busy; bestStand = stand; bestHour = hour;
                    break;
                }
            }
            if (bestStand < 0) return false;
            b.stand = bestStand; b.hour = bestHour;
            return true;
        }

        /// <summary>Plans every contract again, the longest first. Returns how many found a place.</summary>
        public int AutoPlanContracts()
        {
            foreach (var b in State.bookings) { b.stand = -1; b.hour = -1; }
            int placed = 0;
            foreach (var b in State.bookings.OrderByDescending(x => BlockHours((AircraftClass)x.plane)).ThenByDescending(x => x.reward))
                if (AutoPlace(b)) placed++;
            State.plansMade++;
            Changed?.Invoke();
            return placed;
        }

        // ------------------------------------------------------------------ flying them

        /// <summary>Game seconds into the airport day (which opens at 06:00) that an hour of the clock starts.</summary>
        public static float SecondsInto(int hour) { return ((hour - 6 + 24) % 24) * DayLength / 24; }

        /// <summary>A stand a contract flight is about to need: other aircraft leave it be while another is free.</summary>
        bool Reserved(int stand)
        {
            float into = (float)(State.time % DayLength);
            int today = FuelDay;
            foreach (var b in State.bookings)
            {
                if (b.stand != stand || b.hour < 0 || b.flown >= b.flights) continue;
                if (b.airborne) { if (State.flights.Exists(f => f.contract == b.id && f.stand < 0 && f.phase == Phase.Approach)) return true; continue; }
                if (b.lastDay == today || !Active(b.airline)) continue;
                float at = SecondsInto(b.hour);
                if (into >= at - DayLength / 24 && into < at + DayLength / 24) return true;
            }
            return false;
        }

        /// <summary>The stand a contract flight takes: the one it was planned on when that is free, else any that fits.</summary>
        int BookedStand(FlightState f, AircraftClass c)
        {
            var b = Booking(f.contract);
            if (b != null && b.stand >= 0 && b.stand < StandCount && (!Heavy(c) || HeavyStand(b.stand)) && !State.flights.Exists(x => x.stand == b.stand)) return b.stand;
            return FreeStand(c);
        }

        /// <summary>Sends in each contract's flight of the day when the clock reaches its hour.</summary>
        void ContractArrivals()
        {
            if (State.bookings.Count == 0) return;
            float into = (float)(State.time % DayLength);
            int today = FuelDay;
            foreach (var b in State.bookings)
            {
                if (b.hour < 0 || b.airborne || b.lastDay == today || b.flown >= b.flights || !Active(b.airline)) continue;
                float at = SecondsInto(b.hour);
                if (into < at || into >= at + DayLength / 24) continue;
                if (State.flights.Count >= TowerSlots + 1) continue;       // the tower squeezes a booked flight in, but only one
                var r = RouteOf(b);
                var f = new FlightState { id = State.nextFlightId++, route = r.Id, phase = Phase.Approach, arrivedAt = State.time, slot = b.part, contract = b.id, number = b.number };
                b.airborne = true; b.lastDay = today;
                State.flights.Add(f);
                FlightChanged?.Invoke(f);
            }
        }

        /// <summary>Counts a contract's flight as it leaves, and closes the contract after its last one.</summary>
        void ContractFlew(FlightState f, bool onTime, bool dry)
        {
            var b = Booking(f.contract);
            if (b == null) return;
            b.airborne = false;
            b.flown++;
            if (onTime) b.onTime++;
            if (!dry) b.served++;
            if (GradeIndex(Satisfaction) <= b.grade) b.pleased++;
            if (b.flown >= b.flights) Finish(b);
        }

        /// <summary>How many of a contract's flights must meet a mark for it to count (four in five; every one for the service mark).</summary>
        public static int MarkNeeds(int flights) { return (int)Math.Ceiling(flights * 0.8); }

        /// <summary>The three marks of a contract as they stand now: punctual, serviced, standards.</summary>
        public void Marks(Booking b, out bool punctual, out bool serviced, out bool standards)
        {
            int left = b.flights - b.flown;
            // While it runs, a mark stands as long as it can still be made.
            punctual = b.onTime + left >= MarkNeeds(b.flights);
            serviced = b.served + left >= b.flights;
            standards = b.pleased + left >= MarkNeeds(b.flights);
        }

        /// <summary>What a contract pays at this many marks: two fifths for flying it at all, a fifth for each mark.</summary>
        public static double RewardShare(int marks) { return 0.4 + 0.2 * marks; }
        public static double RepShare(int marks) { return 0.25 + 0.25 * marks; }

        void Finish(Booking b)
        {
            bool punctual, serviced, standards;
            Marks(b, out punctual, out serviced, out standards);
            int marks = (punctual ? 1 : 0) + (serviced ? 1 : 0) + (standards ? 1 : 0);
            double paid = Math.Floor(b.reward * RewardShare(marks));
            int rep = (int)Math.Round(b.rep * RepShare(marks));
            State.bookings.Remove(b);
            Earn(AfterLoan(paid), false);
            AddRep(b.airline, rep);
            Relate(b.airline, marks * 2 - 2);
            AddRating(3 * b.flights * Math.Pow(2.2, b.plane));
            var rec = new BookingRecord
            {
                airline = b.airline, plane = b.plane, kind = b.kind, flights = b.flights, number = b.number, day = FuelDay + 1,
                onTime = b.onTime, served = b.served, pleased = b.pleased, punctual = punctual, serviced = serviced, standards = standards, rep = rep, paid = paid,
            };
            Keep(rec);
            Toast?.Invoke(FlightNo(b) + " contract done, " + marks + " of 3 marks: +" + Format.Money(paid) + ", +" + rep + " reputation with " + Content.Airlines[b.airline].Name);
            ContractDone?.Invoke(rec);
            Changed?.Invoke();
        }
    }
}
