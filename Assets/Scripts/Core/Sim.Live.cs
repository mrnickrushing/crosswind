using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    public sealed class AchievementDef { public string Id, Name, Text; public Func<Sim, double> Progress; public double Target; public int Gems; }

    /// <summary>The layer that keeps a session lively: weather, passing opportunities, today's tasks, achievements.</summary>
    public sealed partial class Sim
    {
        public event Action<Weather> WeatherChanged;
        public event Action<Opportunity> OpportunityAppeared;

        // ------------------------------------------------------------------ the day

        /// <summary>A day at the airport takes twelve minutes: it starts at 06:00, dusk falls about 19:00, and the small hours are quiet.</summary>
        public const float DayLength = 720f;
        /// <summary>0 at 06:00, 0.5 at 18:00, back to 1 at 06:00.</summary>
        public float DayPhase { get { return (float)((State.time % DayLength) / DayLength); } }
        public float Hour { get { return (6 + DayPhase * 24) % 24; } }
        /// <summary>How dark it is, 0 by day to 1 in the middle of the night (dusk and dawn in between).</summary>
        public float Night
        {
            get
            {
                float p = DayPhase;
                if (p < 0.04f) return 1 - p / 0.04f * 0.6f - 0.4f * (p / 0.04f);     // dawn: 1 -> 0
                if (p < 0.52f) return 0;
                if (p < 0.6f) return (p - 0.52f) / 0.08f;                            // dusk
                return 1;
            }
        }

        /// <summary>How busy this time of day is: a morning and an evening peak, a quiet night (less quiet with runway lights).</summary>
        public float DemandFactor
        {
            get
            {
                float h = Hour;
                float peak = Math.Max(0, 1 - Math.Abs(h - 8.5f) / 2.5f) + Math.Max(0, 1 - Math.Abs(h - 18f) / 2.5f);
                bool lit = Level("runway") >= 5 || Researched("ops_night");
                float night = h >= 23 || h < 5 ? (Researched("ops_night") ? 0.9f : lit ? 0.6f : 0.4f) : 1f;
                return (0.9f + 0.5f * peak) * night;
            }
        }

        public Weather Weather { get { return (Weather)State.weather; } }

        /// <summary>How fast the runway works in this weather, 0.5..1, after landing aids and research.</summary>
        public float WeatherFactor
        {
            get
            {
                float f = Weather == Weather.Clear ? 1f : Weather == Weather.Rain ? 0.9f : Weather == Weather.Fog ? 0.6f : Weather == Weather.Storm ? 0.5f : Level("deice") > 0 ? Math.Min(0.92f, 0.72f + 0.02f * Level("deice")) : 0.55f;
                float shield = Math.Min(0.9f, Level("ils") * 0.08f + Fx(Effect.WeatherShield));
                return 1 - (1 - f) * (1 - shield);
            }
        }

        void Live(float dt)
        {
            if (State.research != null) FinishResearch();
            if (State.builds.Count > 0) Builds();
            if (State.time >= State.weatherUntil) NextWeather();
            if (State.opportunity != null && State.time >= State.opportunity.expiresAt) { State.opportunity = null; Changed?.Invoke(); }
            if (State.opportunity == null && State.time >= State.nextOpportunityAt) NextOpportunity();
            double park = ParkingRate * dt;
            if (park > 0) Earn(park, true);
            RollTasks();
            TurnDay();
        }

        void NextWeather()
        {
            var m = Map;
            double roll = Rand();
            Weather w = roll < m.Storm ? Weather.Storm : roll < m.Storm + m.Fog ? Weather.Fog : roll < m.Storm + m.Fog + m.Snow ? Weather.Snow : roll < m.Storm + m.Fog + m.Snow + 0.15 ? Weather.Rain : Weather.Clear;
            // The first few minutes of a new airport are always fair.
            if (State.time < 300) w = Weather.Clear;
            bool changed = (int)w != State.weather;
            State.weather = (int)w;
            State.weatherUntil = State.time + (w == Weather.Clear ? 180 + Rand() * 240 : 60 + Rand() * 90);
            if (changed) { WeatherChanged?.Invoke(w); Changed?.Invoke(); }
        }

        void NextOpportunity()
        {
            State.nextOpportunityAt = State.time + 150 + Rand() * 150;
            if (State.flightsHandled < 3) return;      // not during the first steps
            double roll = Rand();
            // What a minute of this airport is worth, so the offer keeps pace with the game.
            double minute = Math.Max(60, Math.Max(State.incomeRate, IdleRate()) * 60);
            var o = roll < 0.45 ? new Opportunity { kind = "charter", value = Math.Floor(minute * (2 + Rand() * 2)) }
                : roll < 0.9 ? new Opportunity { kind = "rush", value = 45 }
                : new Opportunity { kind = "gems", value = 1 + Math.Floor(Rand() * 3) };
            o.expiresAt = State.time + 25;
            State.opportunity = o;
            OpportunityAppeared?.Invoke(o);
            Changed?.Invoke();
        }

        /// <summary>Takes what is on offer: a charter's fee at once, a rush of double pay, or a few gems.</summary>
        public bool TakeOpportunity()
        {
            var o = State.opportunity;
            if (o == null) return false;
            State.opportunity = null;
            switch (o.kind)
            {
                case "charter": Earn(o.value, false); Toast?.Invoke("Charter flight handled: +" + Format.Money(o.value)); break;
                case "rush": State.rushUntil = State.time + o.value; Toast?.Invoke("Rush hour: every flight pays double for " + (int)o.value + " seconds"); break;
                default: State.gems += (int)o.value; Toast?.Invoke("+" + (int)o.value + " gems"); break;
            }
            Count("opportunity", 1);
            Changed?.Invoke();
            return true;
        }

        public bool Rush { get { return State.time < State.rushUntil; } }

        // ------------------------------------------------------------------ the sign-in calendar

        /// <summary>Seven days of rewards, then round again: gems, with a bigger one on day seven. Missing a day starts over.</summary>
        public static readonly int[] SignInGems = { 2, 3, 4, 5, 6, 8, 20 };

        public bool SignInReady { get { return State.signInDay != Today; } }
        /// <summary>The day of the calendar the next claim is (0..6).</summary>
        public int SignInIndex { get { return (State.signInDay == Today - 1 || State.signInDay == Today ? State.signInStreak : 0) % SignInGems.Length; } }

        public int ClaimSignIn()
        {
            if (!SignInReady) return 0;
            int index = State.signInDay == Today - 1 ? State.signInStreak % SignInGems.Length : 0;
            int gems = SignInGems[index];
            State.signInStreak = index + 1;
            State.signInDay = Today;
            State.gems += gems;
            Changed?.Invoke();
            return gems;
        }

        // ------------------------------------------------------------------ today's tasks

        /// <summary>The local day number (tests set the offset).</summary>
        public static int UtcOffsetSeconds = 0;
        public int Today { get { return (int)Math.Floor((RealNow() + UtcOffsetSeconds) / 86400.0); } }

        void RollTasks()
        {
            if (State.taskDay == Today && State.tasks.Count > 0) return;
            State.taskDay = Today;
            State.tasks.Clear();
            double minute = Math.Max(60, Math.Max(State.incomeRate, IdleRate()) * 60);
            // Three a day, picked by the day so everyone gets the same ones, sized to the airport.
            var pool = new[]
            {
                new TaskState { kind = "flights", target = 30 + Stars * 10, gems = 3 },
                new TaskState { kind = "ontime", target = 20 + Stars * 8, gems = 3 },
                new TaskState { kind = "earn", target = Math.Floor(minute * 20), gems = 4 },
                new TaskState { kind = "upgrade", target = 5, gems = 3 },
                new TaskState { kind = "opportunity", target = 3, gems = 4 },
                new TaskState { kind = "research", target = 1, gems = 5 },
            };
            int start = Math.Abs(Today) % pool.Length;
            for (int i = 0; i < 3; i++) State.tasks.Add(pool[(start + i * 2) % pool.Length]);
            Changed?.Invoke();
        }

        void Count(string kind, double n)
        {
            CountEvent(kind, n);
            foreach (var t in State.tasks) if (t.kind == kind && !t.claimed) t.progress = Math.Min(t.target, t.progress + n);
        }

        public static string TaskText(TaskState t)
        {
            switch (t.kind)
            {
                case "flights": return "Handle " + Format.Money(t.target) + " flights";
                case "ontime": return "Send " + Format.Money(t.target) + " flights off on time";
                case "earn": return "Earn " + Format.Money(t.target);
                case "upgrade": return "Build or upgrade " + Format.Money(t.target) + " times";
                case "opportunity": return "Take " + Format.Money(t.target) + " opportunities";
                default: return "Finish a research project";
            }
        }

        public bool ClaimTask(int index)
        {
            if (index < 0 || index >= State.tasks.Count) return false;
            var t = State.tasks[index];
            if (t.claimed || t.progress < t.target) return false;
            t.claimed = true;
            State.gems += t.gems;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ achievements

        public static readonly List<AchievementDef> Achievements = BuildAchievements();

        static List<AchievementDef> BuildAchievements()
        {
            var list = new List<AchievementDef>();
            void A(string id, string name, string text, double target, Func<Sim, double> p, int gems) { list.Add(new AchievementDef { Id = id, Name = name, Text = text, Target = target, Progress = p, Gems = gems }); }
            foreach (var (n, g) in new[] { (100, 5), (1000, 10), (10000, 20), (100000, 40) })
                A("flights_" + n, "Wheels Down", "Handle " + Format.Money(n) + " flights at one airport", n, s => s.State.flightsHandled, g);
            foreach (var (n, g) in new[] { (3, 5), (5, 10), (7, 20), (10, 50) })
                A("stars_" + n, "Rising Star", "Reach a " + n + "-star airport", n, s => s.Stars, g);
            foreach (var (n, g) in new[] { (2, 5), (4, 15) })
                A("managers_" + n, "Delegation", "Have " + n + " managers", n, s => s.State.managers.Count, g);
            foreach (var (n, g) in new[] { (5, 10), (15, 25), (26, 60) })
                A("research_" + n, "Bright Ideas", "Finish " + n + " research projects", n, s => s.State.researched.Count, g);
            foreach (var (n, g) in new[] { (1, 20), (3, 40), (5, 80) })
                A("airports_" + n, "Portfolio", "Sell " + n + " airport" + (n == 1 ? "" : "s"), n, s => s.State.mapsRun.Count, g);
            foreach (var (n, g) in new[] { (5, 10), (25, 25), (100, 60) })
                A("grade_a_" + n, "Top Marks", "Close " + n + " days with grade A", n, s => s.State.daysAtA, g);
            foreach (var (n, g) in new[] { (3, 5), (20, 20) })
                A("cheap_fuel_" + n, "Fuel Trader", "Fill a quarter of the tanks or more at 10% below par, " + n + " times", n, s => s.State.cheapFills, g);
            foreach (var (n, g) in new[] { (3, 5), (15, 20) })
                A("resurface_" + n, "Fresh Tarmac", "Resurface the runway " + n + " times", n, s => s.State.resurfaced, g);
            A("class_c", "Jet Age", "Sign an Albatross 160 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class >= AircraftClass.C) ? 1 : 0, 10);
            A("class_e", "Heavy Metal", "Sign a Leviathan 520 route", 1, s => s.State.contracts.Exists(c => Content.Routes[c.route].Class == AircraftClass.E) ? 1 : 0, 40);
            return list;
        }

        public bool AchievementDone(AchievementDef a) { return a.Progress(this) >= a.Target; }
        public bool AchievementClaimed(AchievementDef a) { return State.achievements.Contains(a.Id); }

        public bool ClaimAchievement(string id)
        {
            var a = Achievements.Find(x => x.Id == id);
            if (a == null || AchievementClaimed(a) || !AchievementDone(a)) return false;
            State.achievements.Add(id);
            State.gems += a.Gems;
            Changed?.Invoke();
            return true;
        }

        public int AchievementsWaiting { get { return Achievements.Count(a => !AchievementClaimed(a) && AchievementDone(a)); } }
    }
}
