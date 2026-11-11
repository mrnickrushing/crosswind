using System;
using System.Collections.Generic;

namespace Crosswind.Core
{
    public sealed class EventDef
    {
        public string Id, Name, Blurb, Counts;      // Counts: the task kind it adds up ("flights", "ontime", "earnMinutes", "upgrade")
        public double[] Targets;                    // three milestones (earnMinutes: minutes of the airport's income)
        public int[] Gems;
    }

    /// <summary>
    /// Weekly events: one runs at a time, a new one every Monday in a fixed rotation. Each counts one thing you do
    /// anyway and pays gems at three milestones, so there is always a medium-term target on the board.
    /// </summary>
    public sealed partial class Sim
    {
        public static readonly List<EventDef> Events = new List<EventDef>
        {
            new EventDef { Id = "rush", Name = "Holiday Rush", Blurb = "The whole country is flying. Handle as many flights as you can.", Counts = "flights", Targets = new double[] { 150, 600, 2000 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "clockwork", Name = "Clockwork Week", Blurb = "The airlines are watching the clock. Send flights off on time.", Counts = "ontime", Targets = new double[] { 120, 500, 1600 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "boom", Name = "Building Boom", Blurb = "Cranes on the skyline. Build and upgrade.", Counts = "upgrade", Targets = new double[] { 15, 40, 100 }, Gems = new[] { 10, 25, 60 } },
            new EventDef { Id = "gold", Name = "Golden Week", Blurb = "Fill the tills. Earn as much as you can.", Counts = "earnMinutes", Targets = new double[] { 60, 240, 900 }, Gems = new[] { 10, 25, 60 } },
        };

        /// <summary>Weeks since the epoch, turning over on Monday at local midnight.</summary>
        public int Week { get { return (int)Math.Floor((Today + 3) / 7.0); } }
        public EventDef CurrentEvent { get { return Events[((Week % Events.Count) + Events.Count) % Events.Count]; } }
        /// <summary>Seconds until this week's event ends.</summary>
        public double EventLeft { get { return (Week + 1) * 7 * 86400.0 - 3 * 86400.0 - (RealNow() + UtcOffsetSeconds); } }

        void RollEvent()
        {
            if (State.eventWeek == Week) return;
            State.eventWeek = Week; State.eventProgress = 0; State.eventClaimed = 0;
        }

        void CountEvent(string kind, double n)
        {
            RollEvent();
            var e = CurrentEvent;
            if (e.Counts == kind) State.eventProgress += n;
            else if (e.Counts == "earnMinutes" && kind == "earn")
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
