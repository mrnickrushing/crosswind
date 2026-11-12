using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>The incident on the manager's desk: which one, until when, and what a minute of this airport was worth when it came up.</summary>
    [Serializable]
    public sealed class IncidentState { public string id; public double expiresAt; public double value; public string airline; }

    /// <summary>One line of the incident log.</summary>
    [Serializable]
    public sealed class IncidentRecord { public string id; public int choice, day; public bool lapsed; public string line; }

    public sealed class IncidentChoice
    {
        public string Label;
        /// <summary>What it will do, said before it is chosen (costs and odds in plain words).</summary>
        public Func<Sim, IncidentState, string> Does;
        /// <summary>Why it can't be chosen (null: it can).</summary>
        public Func<Sim, IncidentState, string> Blocked;
        /// <summary>Does it, and says how it went.</summary>
        public Func<Sim, IncidentState, string> Take;
    }

    public sealed class IncidentDef
    {
        public string Id, Title, Icon;
        public int Stars = 2;
        public Func<Sim, IncidentState, string> Story;
        /// <summary>Whether it can come up at this airport right now (null: always).</summary>
        public Func<Sim, bool> When;
        public IncidentChoice[] Choices;
        /// <summary>What happens when nobody answers in time: always a choice that costs nothing up front.</summary>
        public int Default = 1;
    }

    /// <summary>
    /// Incidents: every few minutes something lands on the manager's desk that the plan did not cover. A medical
    /// diversion, a strike threat, a film crew, a fox on the taxiway. Each is a short decision between two answers
    /// with different costs: money now, the passengers' mood for a while, an airline's goodwill, the runway shut for
    /// a moment, or a risk taken. Left unanswered it settles itself the passive way. Amounts follow what a minute of
    /// the airport is worth, so a decision matters at every size.
    /// </summary>
    public sealed partial class Sim
    {
        public event Action<IncidentState> IncidentAppeared;
        /// <summary>How the last one went, in a sentence.</summary>
        public event Action<string> IncidentSettled;

        public const int IncidentStars = 2, IncidentLogSize = 8;
        /// <summary>Game seconds an incident waits for an answer.</summary>
        public const float IncidentSeconds = 75f;

        public static readonly List<IncidentDef> Incidents = BuildIncidents();

        public IncidentDef Incident { get { var i = State.incident; return i == null ? null : Incidents.Find(d => d.Id == i.id); } }
        public double IncidentLeft { get { return State.incident == null ? 0 : Math.Max(0, State.incident.expiresAt - State.time); } }

        /// <summary>A passing lift or dent in the passengers' mood (an incident's doing), added to their satisfaction.</summary>
        public float MoodShift { get { return State.time < State.moodUntil ? State.moodShift : 0; } }
        public double MoodLeft { get { return Math.Max(0, State.moodUntil - State.time); } }

        // Incidents roll their own dice, so adding or answering one never changes how the rest of a run plays out.
        double IncidentRand()
        {
            uint x = State.incidentSeed == 0 ? 2463534242u : State.incidentSeed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State.incidentSeed = x == 0 ? 2463534242u : x;
            return (State.incidentSeed & 0xFFFFFF) / (double)0x1000000;
        }

        void IncidentTick()
        {
            var i = State.incident;
            if (i != null)
            {
                if (Incident == null) { State.incident = null; return; }
                if (State.time >= i.expiresAt) Settle(Incident.Default, true);
                return;
            }
            if (State.time < State.nextIncidentAt) return;
            State.nextIncidentAt = State.time + 240 + IncidentRand() * 240;
            if (Stars < IncidentStars || State.flightsHandled < 15) return;
            var pool = Incidents.Where(d => Stars >= d.Stars && d.Id != State.lastIncident && (d.When == null || d.When(this))).ToList();
            if (pool.Count == 0) return;
            var def = pool[Math.Min(pool.Count - 1, (int)(IncidentRand() * pool.Count))];
            var lines = State.contracts.Select(c => Content.Routes[c.route].Airline).Where(a => a != "own" && Content.Airlines.ContainsKey(a)).Distinct().ToList();
            State.incident = new IncidentState
            {
                id = def.Id, expiresAt = State.time + IncidentSeconds,
                value = Math.Floor(Math.Max(60, Math.Max(State.incomeRate, IdleRate()) * 60)),
                airline = lines.Count > 0 ? lines[Math.Min(lines.Count - 1, (int)(IncidentRand() * lines.Count))] : "",
            };
            IncidentAppeared?.Invoke(State.incident);
            Changed?.Invoke();
        }

        /// <summary>Why this answer can't be given (null: it can).</summary>
        public string IncidentBlocked(int choice)
        {
            var def = Incident;
            if (def == null) return "Nothing to decide";
            if (choice < 0 || choice >= def.Choices.Length) return "No such answer";
            var c = def.Choices[choice];
            return c.Blocked == null ? null : c.Blocked(this, State.incident);
        }

        /// <summary>What an answer will do, in a line.</summary>
        public string IncidentDoes(int choice)
        {
            var def = Incident;
            return def == null || choice < 0 || choice >= def.Choices.Length ? "" : def.Choices[choice].Does(this, State.incident);
        }

        public string IncidentStory { get { var def = Incident; return def == null ? "" : def.Story(this, State.incident); } }

        /// <summary>Answers the incident on the desk.</summary>
        public bool DecideIncident(int choice)
        {
            if (IncidentBlocked(choice) != null) return false;
            Settle(choice, false);
            return true;
        }

        void Settle(int choice, bool lapsed)
        {
            var def = Incident; var i = State.incident;
            string line = def.Choices[choice].Take(this, i);
            State.incident = null;
            State.lastIncident = def.Id;
            if (!lapsed) { State.incidentsDecided++; Count("incident", 1); }
            State.incidentLog.Insert(0, new IncidentRecord { id = def.Id, choice = choice, day = FuelDay + 1, lapsed = lapsed, line = line });
            if (State.incidentLog.Count > IncidentLogSize) State.incidentLog.RemoveRange(IncidentLogSize, State.incidentLog.Count - IncidentLogSize);
            IncidentSettled?.Invoke((lapsed ? "No answer: " : "") + line);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ what answers are made of

        void Pay(double amount) { State.money = Math.Max(0, State.money - Math.Max(0, amount)); }
        void ShiftMood(float by, double seconds) { State.moodShift = by; State.moodUntil = State.time + seconds; }
        void HoldRunway(double seconds) { State.runwayClosedUntil = Math.Max(State.runwayClosedUntil, State.time + seconds); }
        /// <summary>A step of rating worth having at this size of airport.</summary>
        double RatingStep { get { int s = Math.Min(Stars, Content.MaxStars - 1); return Math.Max(4, (Content.StarXp[s + 1] - Content.StarXp[s]) * 0.003); } }
        static string LineName(string airline) { return !string.IsNullOrEmpty(airline) && Content.Airlines.ContainsKey(airline) ? Content.Airlines[airline].Name : "the airline"; }
        static string Short(string airline) { return LineName(airline); }
        string Need(double cost) { return State.money < cost ? "Need " + Format.Money(cost - State.money) + " more" : null; }
        void Please(string airline, float mood, int rep) { if (string.IsNullOrEmpty(airline) || !Content.Airlines.ContainsKey(airline)) return; Relate(airline, mood); if (rep > 0 && ContractsOpen) AddRep(airline, rep); }

        static List<IncidentDef> BuildIncidents()
        {
            var list = new List<IncidentDef>();
            IncidentChoice C(string label, Func<Sim, IncidentState, string> does, Func<Sim, IncidentState, string> take, Func<Sim, IncidentState, string> blocked = null)
            { return new IncidentChoice { Label = label, Does = does, Take = take, Blocked = blocked }; }
            void I(string id, string title, string icon, int stars, Func<Sim, IncidentState, string> story, Func<Sim, bool> when, IncidentChoice a, IncidentChoice b)
            { list.Add(new IncidentDef { Id = id, Title = title, Icon = icon, Stars = stars, Story = story, When = when, Choices = new[] { a, b } }); }
            string M(double v) { return Format.Money(Math.Floor(v)); }

            I("medical", "Medical diversion", "bell", 2,
                (s, i) => "A flight passing overhead has a passenger taken ill and asks to land now.", null,
                C("CLEAR THE RUNWAY", (s, i) => "Runway held for 20 s. Rating up, and the airline remembers.",
                    (s, i) => { s.HoldRunway(20); s.AddRating(s.RatingStep); s.Please(i.airline, 6, 8); return "The aircraft landed and the passenger is in good hands. " + Short(i.airline) + " sent their thanks."; }),
                C("SEND THEM ON", (s, i) => "Nothing changes here. The airline will note it.",
                    (s, i) => { s.Please(i.airline, -3, 0); return "They flew on to the next field."; }));

            I("vip", "A minister's jet", "crown", 3,
                (s, i) => "A government jet wants to land within the half hour, quietly, and leave the same way.", null,
                C("ROLL OUT THE CARPET", (s, i) => "Costs " + M(i.value * 1.5) + ". +3 gems, rating up" + (s.LoungeLevel > 0 ? ", and your lounge earns " + M(i.value * 5) + "." : "."),
                    (s, i) =>
                    {
                        s.Pay(i.value * 1.5); s.State.gems += 3; s.AddRating(s.RatingStep);
                        if (s.LoungeLevel > 0) { s.Earn(i.value * 5, false); return "The minister used your lounge and the office paid in full: +" + M(i.value * 5) + " and 3 gems."; }
                        return "Handled without a word in the papers: +3 gems.";
                    }, (s, i) => s.Need(i.value * 1.5)),
                C("LIKE ANY OTHER FLIGHT", (s, i) => "A standard handling fee: +" + M(i.value * 2) + ".",
                    (s, i) => { s.Earn(i.value * 2, false); return "A standard handling fee: +" + M(i.value * 2) + "."; }));

            I("strike", "The baggage crews want a rise", "baggage", 2,
                (s, i) => "The baggage crews say the season has been too heavy for the pay. They want an answer today.", s => s.Level("baggage") > 0,
                C("PAY THE RISE", (s, i) => "Costs " + M(i.value * 3) + ". Passengers a little happier for a while.",
                    (s, i) => { s.Pay(i.value * 3); s.ShiftMood(0.03f, 150); return "The crews went back to work with a grin."; }, (s, i) => s.Need(i.value * 3)),
                C("REFUSE", (s, i) => "Even odds: nothing happens, or a walkout that upsets passengers.",
                    (s, i) =>
                    {
                        if (s.IncidentRand() < 0.5) return "They grumbled and went back to work.";
                        s.ShiftMood(-0.15f, 150); return "A walkout. Bags piled up at the belts and passengers noticed.";
                    }));

            I("birds", "Bird strike on approach", "arrival", 2,
                (s, i) => "A crew reports hitting a flock on short final. There may be debris on the runway.", null,
                C("INSPECT THE RUNWAY", (s, i) => "Runway shut for 25 s. Rating up.",
                    (s, i) => { s.HoldRunway(25); s.AddRating(s.RatingStep); return "Two birds and a loose bolt found and cleared."; }),
                C("KEEP LANDING", (s, i) => "One chance in three of damage to the surface.",
                    (s, i) =>
                    {
                        if (s.IncidentRand() >= 0.34) return "Nothing more was found.";
                        s.State.runwayWear = Math.Min(1f, s.State.runwayWear + 0.25f); s.Please(i.airline, -4, 0);
                        return "The next aircraft took debris. The surface is scarred and " + Short(i.airline) + " is not pleased.";
                    }));

            I("press", "A film crew at the door", "tv", 2,
                (s, i) => "A production wants to shoot a scene in your concourse this afternoon.", s => s.State.terminal.Count > 0,
                C("WELCOME THEM", (s, i) => "+" + M(i.value * 3) + " and rating up. The hall is crowded for a while.",
                    (s, i) => { s.Earn(i.value * 3, false); s.AddRating(s.RatingStep); s.ShiftMood(-0.05f, 120); return "Location fee: +" + M(i.value * 3) + ". Cables everywhere for a while."; }),
                C("NOT TODAY", (s, i) => "Nothing changes.", (s, i) => "They went to film somewhere else."));

            I("fuel", "Fuel at a discount", "fuel", 2,
                (s, i) => "A supplier has a tanker more than it can sell today and offers it at 40% off.", s => s.Level("fuel") > 0 && s.FuelFill < 0.7f,
                C("FILL THE TANKS", (s, i) => "Costs " + M(s.DiscountFuelCost) + " for full tanks.",
                    (s, i) =>
                    {
                        double cost = s.DiscountFuelCost, units = s.FuelCapacity - s.FuelStock;
                        s.Pay(cost); s.State.today.fuelBought += cost; s.State.fuelSaved += units * Math.Max(0, 1 - s.FuelPrice * 0.6); s.State.fuelStock = s.FuelCapacity; s.State.fuelWarned = false;
                        return "Tanks full for " + M(cost) + ".";
                    }, (s, i) => s.Need(s.DiscountFuelCost)),
                C("PASS", (s, i) => "Nothing changes.", (s, i) => "The tanker went to the next airport."));

            I("overbooked", "One flight too many", "rail_airlines", 2,
                (s, i) => (Content.Airlines.ContainsKey(i.airline ?? "") ? Content.Airlines[i.airline].Name : "An airline") + " has a full aircraft and nowhere to send it. Can you take it within the hour?", s => s.State.contracts.Exists(c => Content.Routes[c.route].Airline != "own"),
                C("SQUEEZE IT IN", (s, i) => "+" + M(i.value * 3) + " and their goodwill. The gates are tight for a while.",
                    (s, i) => { s.Earn(i.value * 3, false); s.Please(i.airline, 5, 6); s.ShiftMood(-0.03f, 90); return "+" + M(i.value * 3) + ", and " + Short(i.airline) + " owes you one."; }),
                C("DECLINE", (s, i) => "They will remember it, a little.", (s, i) => { s.Please(i.airline, -2, 0); return "They found another airport."; }));

            I("inspection", "Safety inspection", "tasks", 3,
                (s, i) => "The aviation authority has arrived unannounced, with clipboards.", null,
                C("SHOW THEM EVERYTHING", (s, i) => "Runway surface at " + (int)Math.Round(s.RunwayCondition * 100) + "%: " + (s.RunwayCondition >= 0.6f ? "you should pass (+2 gems, rating up)." : "below standard, expect a fine of " + M(i.value * 2) + "."),
                    (s, i) =>
                    {
                        if (s.RunwayCondition >= 0.6f) { s.State.gems += 2; s.AddRating(s.RatingStep * 2); return "A clean report: +2 gems and a line in the trade press."; }
                        double fine = Math.Min(s.State.money, i.value * 2); s.Pay(fine);
                        return "They found the runway surface below standard: a fine of " + M(fine) + ".";
                    }),
                C("THE OFFICE ONLY", (s, i) => "They see the paperwork. Nothing gained, nothing lost.", (s, i) => "They read the paperwork and left."));

            I("sponsor", "A name over the gates", "tag", 2,
                (s, i) => "A drinks company will pay to put its name over every gate for a week.", s => s.Level("gates") > 0,
                C("TAKE THE MONEY", (s, i) => "+" + M(i.value * 5) + ". Passengers like the hall a little less for a while.",
                    (s, i) => { s.Earn(i.value * 5, false); s.ShiftMood(-0.04f, 240); return "+" + M(i.value * 5) + " for a week of banners."; }),
                C("KEEP THE HALL CLEAN", (s, i) => "Passengers a little happier for a while. Rating up.",
                    (s, i) => { s.ShiftMood(0.04f, 240); s.AddRating(s.RatingStep); return "The hall stays yours. People notice."; }));

            I("power", "The lights are flickering", "bolt", 2,
                (s, i) => "The grid is browning out and the terminal lights are dipping.", null,
                C("RUN THE GENERATORS", (s, i) => "Costs " + M(i.value * 1.5) + ". Nobody notices a thing.",
                    (s, i) => { s.Pay(i.value * 1.5); return "The generators carried the hall. Nobody noticed."; }, (s, i) => s.Need(i.value * 1.5)),
                C("RIDE IT OUT", (s, i) => "Passengers unhappy for a while.",
                    (s, i) => { s.ShiftMood(-0.12f, 100); return "Escalators stopped and the boards went dark for a while."; }));

            I("band", "A band flies out tonight", "sound", 3,
                (s, i) => "A touring band leaves this evening and a few hundred fans have come to see them off.", s => s.State.terminal.Count > 0,
                C("OPEN A VIEWING AREA", (s, i) => "Costs " + M(i.value * 0.5) + ". The cafes take about " + M(i.value * 2.5) + " and the hall is lively.",
                    (s, i) => { s.Pay(i.value * 0.5); s.Earn(i.value * 2.5, false); s.ShiftMood(0.06f, 120); return "The cafes sold out: +" + M(i.value * 2.5) + "."; }, (s, i) => s.Need(i.value * 0.5)),
                C("THE SIDE DOOR", (s, i) => "The airline will thank you for the quiet.",
                    (s, i) => { s.Please(i.airline, 4, 4); return "The band left unseen."; }));

            I("fox", "A fox on the taxiway", "compass", 2,
                (s, i) => "Ground control reports a fox trotting along the taxiway as if it owned it.", null,
                C("SEND THE OPS TRUCK", (s, i) => "Costs " + M(i.value * 0.3) + ". Rating up.",
                    (s, i) => { s.Pay(i.value * 0.3); s.AddRating(s.RatingStep); return "Chased off. Someone filmed it and the clip did the rounds."; }, (s, i) => s.Need(i.value * 0.3)),
                C("WAIT FOR IT TO LEAVE", (s, i) => "Traffic holds for 12 s.",
                    (s, i) => { s.HoldRunway(12); return "It took its time. Traffic held for a while."; }));

            return list;
        }

        /// <summary>What the discounted tanker costs: the rest of the tanks at 60% of today's price.</summary>
        public double DiscountFuelCost { get { return Math.Ceiling(Math.Max(0, FuelCapacity - FuelStock) * FuelPrice * 0.6); } }
    }
}
