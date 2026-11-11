using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>A local notification to schedule: seconds from now, and what it says.</summary>
    public sealed class Reminder { public double In; public string Title, Body; }

    public sealed partial class Sim
    {
        /// <summary>
        /// What is worth a notification once the app is closed: the tills are full, research or the builders are
        /// done, a free crate, the event's last hours, tomorrow's gift. At most four, never two within ten minutes.
        /// </summary>
        public List<Reminder> ReminderPlan()
        {
            var all = new List<Reminder>();
            if (IdleRate() > ParkingRate && OfflineCapSeconds > 600)
                all.Add(new Reminder { In = OfflineCapSeconds, Title = Map.Name + " is waiting for you", Body = "The airport has earned all it can while you were away. Come and collect." });
            if (State.research != null && ResearchLeft > 60)
                all.Add(new Reminder { In = ResearchLeft, Title = "Research finished", Body = Content.ResearchById(State.research.id).Name + " is ready. Pick the next project." });
            if (State.builds.Count > 0)
            {
                double last = State.builds.Max(b => b.doneAt) - RealNow();
                if (last > 60) all.Add(new Reminder { In = last, Title = "The builders are done", Body = State.builds.Count == 1 ? Content.Facilities[State.builds[0].facility].Name + " is finished." : "Everything you ordered is finished." });
            }
            if (FreeCrateIn > 60)
                all.Add(new Reminder { In = FreeCrateIn, Title = "A team crate is waiting", Body = "Your free crate is ready. Who's inside?" });
            if (ContractsOpen && MarketIn > 60 && State.bookings.Count < BookingCap)
                all.Add(new Reminder { In = MarketIn, Title = "New contracts on the market", Body = "The airlines have posted new charters and series at " + Map.Name + "." });
            double ev = EventLeft - 3 * 3600;
            if (ev > 60 && State.eventProgress > 0)
                all.Add(new Reminder { In = ev, Title = CurrentEvent.Name + " ends in 3 hours", Body = "There's still time for the next reward." });
            all.Add(new Reminder { In = 24 * 3600, Title = "A new day at " + Map.Name, Body = "Today's gift and tasks are ready." });

            var plan = new List<Reminder>();
            foreach (var r in all.OrderBy(r => r.In))
            {
                if (plan.Count >= 4) break;
                if (plan.Count > 0 && r.In - plan[plan.Count - 1].In < 600) continue;
                plan.Add(r);
            }
            return plan;
        }
    }
}
