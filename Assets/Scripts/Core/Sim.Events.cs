using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    public sealed class EventDef
    {
        public string Id, Name, Blurb, Counts;      // Counts: what it adds up ("flights", "ontime", "earnMinutes", "upgrade", "rough", "cargoMinutes")
        public double[] Targets;                    // three milestones (earnMinutes, cargoMinutes: minutes of the airport's income)
        public int[] Gems;
        // Weeks that change the airport, not just count it (0: no change).
        public float Storm, Fog, SnowScale = 1;     // added to the map's chance of storms and fog; snow where it snows is scaled
        public float CargoPay = 1;                  // the cargo part of every flight's pay
        public int CargoLevel;                      // cargo every flight carries this week, as if the cargo terminal were this many levels higher
        public float FreightOdds;                   // added to the freight airlines' share of freight contracts on the market
        public string Effect;                       // what it changes, in a line
    }

    /// <summary>
    /// Weekly events: one runs at a time, a new one every Monday in a fixed rotation, and pays gems at three
    /// milestones. Most count something you do anyway; two change the week itself and ask for preparation: Storm Season
    /// (rough weather all week; flights sent off on time in it count) and Cargo Surge (cargo pays half again and the
    /// freight airlines put more on the market; cargo income counts). The event a week started with is kept in the save
    /// (<see cref="GameState.eventId"/>), so a new version with a longer rotation never swaps it mid-week.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly List<EventDef> Events = new List<EventDef>
        {
            new EventDef { Id = "rush", Name = "Holiday Rush", Blurb = "The whole country is flying. Handle as many flights as you can.", Counts = "flights", Targets = new double[] { 150, 600, 2000 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "clockwork", Name = "Clockwork Week", Blurb = "The airlines are watching the clock. Send flights off on time.", Counts = "ontime", Targets = new double[] { 120, 500, 1600 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "storm", Name = "Storm Season", Blurb = "A week of rough weather is on its way. Keep flights leaving on time through it.", Counts = "rough", Targets = new double[] { 40, 180, 600 }, Gems = new[] { 12, 30, 70 },
                Storm = 0.25f, Fog = 0.08f, SnowScale = 1.5f, Effect = "Storms and fog all week (snow too where it snows): the runway slows. Flights sent off on time in bad weather count." },
            new EventDef { Id = "boom", Name = "Building Boom", Blurb = "Cranes on the skyline. Build and upgrade.", Counts = "upgrade", Targets = new double[] { 15, 40, 100 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "gold", Name = "Golden Week", Blurb = "Fill the tills. Earn as much as you can.", Counts = "earnMinutes", Targets = new double[] { 60, 240, 900 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "cargo", Name = "Cargo Surge", Blurb = "Holds full and pallets stacked high. Every flight carries freight this week.", Counts = "cargoMinutes", Targets = new double[] { 4, 20, 80 }, Gems = new[] { 12, 30, 70 },
                CargoPay = 1.5f, CargoLevel = 1, FreightOdds = 0.25f, Effect = "Every flight carries cargo (as if your Cargo Terminal were a level higher) and it pays half again: big aircraft and busy routes earn most. Freight airlines offer more contracts. Cargo income counts." },
        };

        /// <summary>The rotation before Storm Season and Cargo Surge (a week already under way in an older save keeps its event).</summary>
        static readonly string[] LegacyRotation = { "rush", "clockwork", "boom", "gold" };

        public static EventDef EventById(string id) { return Events.Find(e => e.Id == id); }

        /// <summary>Weeks since the epoch, turning over on Monday at local midnight.</summary>
        public int Week { get { return (int)Math.Floor((Today + 3) / 7.0); } }
        static EventDef EventOfWeek(int week) { return Events[((week % Events.Count) + Events.Count) % Events.Count]; }
        public EventDef CurrentEvent
        {
            get
            {
                if (State.eventWeek == Week && !string.IsNullOrEmpty(State.eventId)) { var e = EventById(State.eventId); if (e != null) return e; }
                return EventOfWeek(Week);
            }
        }
        /// <summary>Next week's event (for the reminder and the card's "next week").</summary>
        public EventDef NextEvent { get { return EventOfWeek(Week + 1); } }
        /// <summary>Seconds until this week's event ends.</summary>
        public double EventLeft { get { return (Week + 1) * 7 * 86400.0 - 3 * 86400.0 - (RealNow() + UtcOffsetSeconds); } }

        /// <summary>An older save in the middle of a week keeps the event it had (from the rotation of four).</summary>
        void EnsureEventId()
        {
            if (string.IsNullOrEmpty(State.eventId) && State.eventWeek >= 0)
                State.eventId = LegacyRotation[((State.eventWeek % 4) + 4) % 4];
        }

        /// <summary>The week's change to the weather, the cargo pay and the market (nothing on an ordinary week).</summary>
        EventDef Week_ { get { RollEvent(); return CurrentEvent; } }
        public float EventCargoPay { get { return Week_.CargoPay; } }
        public int EventCargoLevel { get { return Week_.CargoLevel; } }
        float EventFreightOdds { get { return Week_.FreightOdds; } }

        /// <summary>What to have ready for this week's event: each thing and whether it is ready (empty: nothing in particular).</summary>
        public List<(string what, bool ready)> EventPrep
        {
            get
            {
                var list = new List<(string, bool)>();
                var e = CurrentEvent;
                if (e.Id == "storm")
                {
                    list.Add(("Runway surface sound (resurface before it wears)", RunwayCondition >= 0.7f));
                    if (Map.Snow > 0) list.Add(("De-icing pad for the snow", Level("deice") > 0));
                    if (Level("fuel") > 0) list.Add(("Tanks at least half full", FuelFill >= 0.5f));
                    list.Add(("A pushback manager (late tugs make late flights)", State.managers.Contains("pushback") || Level("pushback") <= 0));
                }
                else if (e.Id == "cargo")
                {
                    // Cargo goes by the seat: the more seats leave, the more it pays.
                    list.Add(("Jets on your routes (cargo goes by the seat)", State.contracts.Exists(c => Content.Routes[c.route].Class >= AircraftClass.C)));
                    list.Add(("A manager for every service (more flights, more cargo)", Content.Departments.All(d => Level(d) <= 0 || State.managers.Contains(d))));
                    if (Stars >= Content.Facilities["cargo"].Stars) list.Add(("Cargo Terminal built (its levels add to the surge)", Level("cargo") > 0));
                    if (ContractsOpen && Level("cargo") > 0) list.Add(("Room in the contract book for freight", State.bookings.Count < BookingCap));
                }
                return list;
            }
        }

        void RollEvent()
        {
            if (State.eventWeek == Week) { if (string.IsNullOrEmpty(State.eventId)) State.eventId = EventOfWeek(Week).Id; return; }
            State.eventWeek = Week; State.eventProgress = 0; State.eventClaimed = 0;
            State.eventId = EventOfWeek(Week).Id;
        }

        void CountEvent(string kind, double n)
        {
            RollEvent();
            var e = CurrentEvent;
            if (e.Counts == kind) State.eventProgress += n;
            else if ((e.Counts == "earnMinutes" && kind == "earn") || (e.Counts == "cargoMinutes" && kind == "cargo"))
                State.eventProgress += n / Math.Max(60, Math.Max(State.incomeRate, IdleRate()) * 60);   // money, in minutes of this airport's income
        }

        /// <summary>Milestones reached but not yet claimed.</summary>
        public int EventWaiting
        {
            get { RollEvent(); int reached = 0; foreach (var t in CurrentEvent.Targets) if (State.eventProgress >= t) reached++; return Math.Max(0, reached - State.eventClaimed); }
        }

        public int ClaimEvent()
        {
            if (EventWaiting <= 0) return 0;
            int gems = CurrentEvent.Gems[State.eventClaimed];
            State.eventClaimed++;
            State.gems += gems;
            Changed?.Invoke();
            return gems;
        }
    }
}
