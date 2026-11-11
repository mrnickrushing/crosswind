using System;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// The day planner. A route flies all day by default; from three stars it can be given a six-hour slot instead.
    /// A slotted route sends its flights three times as fast inside its slot and none outside it (three quarters of
    /// the day's flights), and each pays more: airlines want the morning and evening peaks most, freight wants the
    /// night. Put too much in one slot and the runway and stands choke, so the day has to be spread.
    /// </summary>
    public sealed partial class Sim
    {
        public const int PlannerStars = 3;
        public static readonly string[] SlotName = { "ALL DAY", "MORNING", "AFTERNOON", "EVENING", "NIGHT" };
        public static readonly string[] SlotHours = { "00-24", "06-12", "12-18", "18-24", "00-06" };
        static readonly int[] SlotStart = { 0, 6, 12, 18, 0 };
        /// <summary>How much faster a slotted route's flights come inside its slot.</summary>
        public const float SlotRate = 3f;

        public bool PlannerOpen { get { return Stars >= PlannerStars; } }
        public bool NightOps { get { return Level("runway") >= 5 || Researched("ops_night"); } }

        public static bool InSlot(int slot, float hour) { return slot <= 0 || (hour >= SlotStart[slot] && hour < SlotStart[slot] + 6); }

        /// <summary>Freight and mail want the night.</summary>
        public static bool NightRoute(RouteDef r) { return r.Airline == "atlas" || r.Id.EndsWith("_mail") || r.Id.EndsWith("_night"); }

        /// <summary>What a flight in this slot pays against the same flight on an all-day schedule.</summary>
        public static float SlotPay(RouteDef r, int slot)
        {
            bool night = NightRoute(r);
            switch (slot)
            {
                case 1: return night ? 1.3f : 1.6f;
                case 2: return night ? 1.3f : 1.45f;
                case 3: return night ? 1.45f : 1.6f;
                case 4: return night ? 2.0f : 1.35f;
                default: return 1f;
            }
        }

        /// <summary>Why this route can't take this slot (null: it can).</summary>
        public string SlotBlocked(string route, int slot)
        {
            if (slot < 0 || slot > 4 || !Signed(route)) return "Unknown";
            if (slot > 0 && !PlannerOpen) return "Needs a " + PlannerStars + "-star airport";
            if (slot == 4 && !NightOps) return "Night flights need runway lights (runway level 5)";
            return null;
        }

        public bool SetSlot(string route, int slot)
        {
            if (SlotBlocked(route, slot) != null) return false;
            var c = State.contracts.Find(x => x.route == route);
            if (c.slot == slot) return false;
            c.slot = slot;
            c.nextArrival = Math.Min(c.nextArrival, State.time + 3);     // the new schedule starts now
            Changed?.Invoke();
            return true;
        }

        /// <summary>How fast a contract's flights come at this hour against its listed rate (0: none now).</summary>
        float ScheduleRate(ContractState c)
        {
            if (c.slot <= 0) return DemandFactor;
            return InSlot(c.slot, Hour) ? SlotRate : 0;
        }

        /// <summary>Game seconds until this slot next opens.</summary>
        double UntilSlot(int slot)
        {
            float hours = ((SlotStart[slot] - Hour) % 24 + 24) % 24;
            return hours * DayLength / 24;
        }

        /// <summary>Flights an hour a contract brings at its listed pace (before the hour of day).</summary>
        double ListedRate(ContractState c)
        {
            var r = Content.Routes[c.route];
            return r.FlightsPerHour * (1 + Fx(Effect.Demand)) * TermsFlights(c.terms) * FleetFlights(r);
        }

        /// <summary>Flights an hour planned for each hour of the day, 00 to 23.</summary>
        public float[] HourlyLoad()
        {
            var load = new float[24];
            foreach (var c in State.contracts)
            {
                double rate = ListedRate(c);
                for (int h = 0; h < 24; h++) load[h] += (float)(rate * (c.slot <= 0 ? 1 : InSlot(c.slot, h) ? SlotRate : 0));
            }
            return load;
        }

        /// <summary>Flights an hour the runway and the stands can turn around with the routes signed now.</summary>
        public float HourlyCapacity()
        {
            if (State.contracts.Count == 0) return 0;
            double weight = 0, runway = 0, ground = 0;
            foreach (var c in State.contracts)
            {
                var r = Content.Routes[c.route]; var plane = Content.Aircraft[r.Class];
                double w = ListedRate(c);
                weight += w;
                runway += w * 2 * plane.RunwaySeconds / RunwaySpeed;
                ground += w * (BoardTime(plane) * 2 + plane.ServiceSeconds / Math.Max(0.1f, r.Needs.Length == 0 ? 1 : r.Needs.Min(n => CrewSpeed(n))) + plane.TaxiSeconds * 2 / TaxiSpeed);
            }
            if (weight <= 0) return 0;
            double byRunway = RunwayLanes * 3600.0 / (runway / weight), byStands = StandCount * 3600.0 / (ground / weight);
            return (float)Math.Min(byRunway, byStands);
        }

        /// <summary>The hour that is most over capacity (-1: the day fits).</summary>
        public int WorstHour()
        {
            var load = HourlyLoad(); float cap = HourlyCapacity(); int worst = -1; float over = 1.0001f;
            for (int h = 0; h < 24; h++) if (cap > 0 && load[h] / cap > over) { over = load[h] / cap; worst = h; }
            return worst;
        }

        /// <summary>
        /// Plans the day for you: the best-paying routes take the slots that pay them most, as long as the slot still
        /// fits under what the airport can handle; whatever doesn't fit stays all day. Returns how many routes moved.
        /// </summary>
        public int AutoPlan()
        {
            if (!PlannerOpen) return 0;
            var before = State.contracts.Select(c => c.slot).ToList();
            foreach (var c in State.contracts) c.slot = 0;
            float cap = HourlyCapacity();
            foreach (var c in State.contracts.OrderByDescending(x => FlightValue(Content.Routes[x.route]) * ListedRate(x)))
            {
                var r = Content.Routes[c.route];
                int best = 0; float bestPay = 1f / 0.75f;       // a slot must beat flying all day (it flies 3/4 of the flights)
                for (int slot = 1; slot <= 4; slot++)
                {
                    if (slot == 4 && !NightOps) continue;
                    if (SlotPay(r, slot) <= bestPay) continue;
                    c.slot = slot;
                    var load = HourlyLoad(); bool fits = true;
                    for (int h = SlotStart[slot]; h < SlotStart[slot] + 6; h++) if (load[h] > cap) fits = false;
                    if (fits) { best = slot; bestPay = SlotPay(r, slot); }
                }
                c.slot = best;
            }
            int moved = 0;
            for (int i = 0; i < before.Count; i++) if (before[i] != State.contracts[i].slot) moved++;
            if (moved > 0) Changed?.Invoke();
            return moved;
        }
    }
}
