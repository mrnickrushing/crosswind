using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>What one airport day came to: kept until the next one replaces it.</summary>
    [Serializable]
    public sealed class DayReport
    {
        public int day;                 // which day of this airport (1 = the first)
        public int flights, onTime, dry;        // dry: flights a supplier had to fuel
        public double income;
        public float satisfaction, runway;      // as the day closed
        public float score;             // 0..1
        public string grade;            // A..E
        public double bonus;            // paid when collected
        public int streak;              // days in a row at B or better, this one included
        public bool claimed;
        public double costs;            // wages and upkeep charged as the day closed
        // Why it went as it did (from 2026-11-15; older reports have none of these and get the plain report).
        public float[] wait;            // seconds jobs waited for a crew, by Service
        public int lateCrew, lateQueue; // late flights that had waited for a crew, or whose boarding the terminal slowed
        public List<string> built;      // what was built or upgraded during the day
        public int prevDay;             // the day before it (0: none), and how that one went
        public float prevScore, prevPunctual, prevSatisfaction, prevDry, prevRunway;

        public void Repair()
        {
            if (wait == null || wait.Length != 6) wait = new float[6];
            if (built == null) built = new List<string>();
        }
    }

    /// <summary>One line of the day report's coaching: a part of the grade, the points it cost and what to do about it.</summary>
    public sealed class ReportLine
    {
        public string Part;             // ontime, passengers, fuel, runway, or change
        public string Title, Text;
        public float Lost;              // points of 100 this part cost the day
        public string Fix;              // a facility to build for it (null: none in particular)
    }

    /// <summary>
    /// The day report. A day at the airport is twelve minutes; when it turns, the day is marked on what an airport is
    /// judged by (flights away on time, passengers content, tanks never dry, a sound runway) and a good grade pays
    /// a bonus on the day's takings. Grades in a row build a streak, and a streak pays gems.
    /// </summary>
    public sealed partial class Sim
    {
        public event Action<DayReport> DayEnded;

        public static readonly string[] Grades = { "A", "B", "C", "D", "E" };
        static readonly float[] GradeAt = { 0.9f, 0.8f, 0.65f, 0.5f, 0f };
        static readonly float[] GradeBonus = { 0.05f, 0.03f, 0.01f, 0f, 0f };
        /// <summary>Every so many good days in a row pay gems.</summary>
        public const int StreakEvery = 5, StreakGems = 5;

        public static float ScoreOf(int flights, int onTime, int dry, float satisfaction, float runway)
        {
            if (flights <= 0) return 0;
            float punctual = onTime / (float)flights, fuelled = 1 - Math.Min(1f, dry / (float)flights);
            return 0.5f * punctual + 0.3f * Math.Max(0, Math.Min(1, satisfaction)) + 0.1f * fuelled + 0.1f * (runway >= WornAt ? 1f : runway / WornAt);
        }

        /// <summary>The four parts of the day's score, each 0..1 (its share of the marks it carries).</summary>
        public static void PartsOf(int flights, int onTime, int dry, float satisfaction, float runway, out float punctual, out float content, out float fuelled, out float sound)
        {
            punctual = flights > 0 ? onTime / (float)flights : 1;
            content = Math.Max(0, Math.Min(1, satisfaction));
            fuelled = flights > 0 ? 1 - Math.Min(1f, dry / (float)flights) : 1;
            sound = runway >= WornAt ? 1f : Math.Max(0, runway) / WornAt;
        }
        /// <summary>What each part weighs in the score: on time, passengers, fuel, runway.</summary>
        public static readonly float[] PartWeight = { 0.5f, 0.3f, 0.1f, 0.1f };
        /// <summary>A flight that waited this long for crews and still left late is put down to the crews.</summary>
        public const float LateWait = 8f;

        public static int GradeIndex(float score) { for (int i = 0; i < GradeAt.Length; i++) if (score >= GradeAt[i]) return i; return GradeAt.Length - 1; }

        /// <summary>The score the day would get if it ended now.</summary>
        public float DayScore { get { return ScoreOf(State.dayFlights, State.dayOnTime, State.dayDry, Satisfaction, RunwayCondition); } }

        public bool ReportWaiting { get { return State.report != null && !State.report.claimed; } }

        void CountDay(bool onTime, bool dry, double paid)
        {
            State.dayFlights++;
            if (onTime) State.dayOnTime++;
            if (dry) State.dayDry++;
            State.dayIncome += paid;
        }

        /// <summary>Closes the books when the day turns (called every step; cheap unless it has).</summary>
        void TurnDay()
        {
            int today = FuelDay;
            if (State.dayIndex == today) return;
            if (State.dayIndex >= 0) CloseLedger(State.dayIndex + 1);
            if (State.dayIndex >= 0 && State.dayFlights > 0)
            {
                float score = DayScore; int g = GradeIndex(score);
                int streak = g <= 1 ? State.gradeStreak + 1 : 0;
                // An uncollected bonus is not lost: it rolls into the new report.
                double carried = ReportWaiting ? State.report.bonus : 0;
                var was = State.report;
                var r = new DayReport
                {
                    day = State.dayIndex + 1, flights = State.dayFlights, onTime = State.dayOnTime, dry = State.dayDry, income = State.dayIncome,
                    satisfaction = Satisfaction, runway = RunwayCondition, score = score, grade = Grades[g],
                    bonus = Math.Floor(State.dayIncome * GradeBonus[g]) + carried, streak = streak,
                    costs = State.ledger.Count > 0 ? State.ledger[0].Costs : 0,
                    wait = State.dayWait.Select(x => (float)x).ToArray(), lateCrew = State.dayLateCrew, lateQueue = State.dayLateQueue,
                    built = State.dayBuilt.ToList(),
                };
                if (was != null && was.day > 0)
                {
                    float wp, wc, wf, wr; PartsOf(was.flights, was.onTime, was.dry, was.satisfaction, was.runway, out wp, out wc, out wf, out wr);
                    r.prevDay = was.day; r.prevScore = was.score; r.prevPunctual = wp; r.prevSatisfaction = was.satisfaction; r.prevDry = 1 - wf; r.prevRunway = was.runway;
                }
                State.gradeStreak = streak;
                State.report = r;
                if (g == 0) { State.daysAtA++; JournalGradeA(r.day); }
                DayEnded?.Invoke(r);
                Changed?.Invoke();
            }
            State.dayIndex = today; State.dayFlights = State.dayOnTime = State.dayDry = 0; State.dayIncome = 0;
            State.dayWait = new double[6]; State.dayLateCrew = State.dayLateQueue = 0; State.dayBuilt.Clear();
            OpenLedger();
        }

        /// <summary>Notes something built today (the report links what changed to it).</summary>
        void Built(string what) { if (!State.dayBuilt.Contains(what) && State.dayBuilt.Count < 8) State.dayBuilt.Add(what); }

        static string Pts(float lost) { int n = Math.Max(1, (int)Math.Round(lost * 100)); return "-" + n + (n == 1 ? " point" : " points"); }
        static string Pct(float v) { return (int)Math.Round(v * 100) + "%"; }

        /// <summary>
        /// The day report as coaching: each part of the grade that cost marks, the biggest first, with what caused it as
        /// far as the airport can tell and what to do about it; then how the day compares with the one before and what
        /// was built in between. Empty for a perfect day.
        /// </summary>
        public List<ReportLine> Coaching(DayReport r)
        {
            var lines = new List<ReportLine>();
            if (r == null || r.flights <= 0) return lines;
            r.Repair();
            float p, c, f, w; PartsOf(r.flights, r.onTime, r.dry, r.satisfaction, r.runway, out p, out c, out f, out w);
            int late = r.flights - r.onTime;
            if (late > 0)
            {
                var line = new ReportLine { Part = "ontime", Lost = PartWeight[0] * (1 - p), Title = "Late departures" };
                string why, fix = null;
                int worst = 0; for (int i = 1; i < r.wait.Length; i++) if (r.wait[i] > r.wait[worst]) worst = i;
                var svc = (Service)worst; string dept = Content.ServiceFacility(svc), name = Content.Facilities[dept].Name;
                if (r.lateCrew >= r.lateQueue && r.lateCrew > 0 && r.wait[worst] > 0)
                {
                    why = r.lateCrew + " of them waited for a crew, most of all for " + ServiceNoun(svc) + " (" + Format.Time(r.wait[worst]) + " in all).";
                    fix = dept;
                    why += !State.managers.Contains(dept) ? " Hire a " + name.ToLower() + " manager so crews go out the moment an aircraft parks, or upgrade the " + name + " for more crews."
                        : " Upgrade the " + name + ": every third level adds a crew, every level makes them faster.";
                }
                else if (r.lateQueue > 0)
                {
                    var limit = FlowLimiters;
                    why = r.lateQueue + " of them boarded slowly: the terminal can't move passengers as fast as the aircraft take them. " + LimiterSentence(limit) + ".";
                    fix = limit[0];
                }
                else why = "Turnarounds ran long. Faster crews (upgrades, training their managers) and more stands shorten every one.";
                line.Text = late + " of " + r.flights + " flights left late (" + Pts(line.Lost) + "). " + why;
                line.Fix = fix;
                lines.Add(line);
            }
            if (c < 0.995f)
            {
                var worst = WorstNeed; var queue = Needs[0];
                var pick = queue.Value < worst.Value ? queue : worst;
                var line = new ReportLine { Part = "passengers", Lost = PartWeight[1] * (1 - c), Title = "Passengers" };
                line.Text = "Passengers were " + Pct(c) + " satisfied (" + Pts(line.Lost) + "). Weakest now: " + pick.Name.ToLower() + " at " + Pct(pick.Value) + ". " + pick.Advice + ".";
                if (pick.Queue) line.Fix = FlowLimiters[0];
                lines.Add(line);
            }
            if (r.dry > 0)
            {
                var line = new ReportLine { Part = "fuel", Lost = PartWeight[2] * (1 - f), Title = "Dry tanks", Fix = "fuel" };
                line.Text = r.dry + (r.dry == 1 ? " flight was" : " flights were") + " fuelled by a supplier because the tanks ran dry (" + Pts(line.Lost) + "). "
                    + (State.managers.Contains("fuel") ? "Your fuel manager reorders only when the price is fair: top up yourself on a dear day." : "Keep the tanks above a quarter (BUILD, Tank farm), or hire a fuel manager to reorder.");
                lines.Add(line);
            }
            if (w < 0.995f)
            {
                var line = new ReportLine { Part = "runway", Lost = PartWeight[3] * (1 - w), Title = "Worn runway" };
                line.Text = "The runway was worn (" + Pts(line.Lost) + "): aircraft clear it slower. Resurface it in a quiet hour (BUILD, Runway surface).";
                lines.Add(line);
            }
            lines.Sort((a, b) => b.Lost.CompareTo(a.Lost));
            lines.RemoveAll(l => l.Lost < 0.005f);
            // How it compares with the day before, and what was built in between.
            if (r.prevDay > 0)
            {
                var parts = new List<string>();
                if (Math.Abs(p - r.prevPunctual) >= 0.02f) parts.Add("on time " + Pct(r.prevPunctual) + " to " + Pct(p));
                if (Math.Abs(c - Math.Min(1, r.prevSatisfaction)) >= 0.02f) parts.Add("passengers " + Pct(Math.Min(1, r.prevSatisfaction)) + " to " + Pct(c));
                float dryShare = 1 - f;
                if (Math.Abs(dryShare - r.prevDry) >= 0.02f) parts.Add("flights fuelled by a supplier " + Pct(r.prevDry) + " to " + Pct(dryShare));
                string built = r.built.Count == 0 ? null : string.Join(", ", r.built.Select(BuiltName).Distinct());
                string grade = Grades[GradeIndex(r.prevScore)] + " to " + r.grade;
                string text = parts.Count == 0 ? "Much the same as day " + r.prevDay + " (grade " + grade + ")."
                    : "Since day " + r.prevDay + ": " + string.Join(", ", parts) + " (grade " + grade + ").";
                if (built != null) text += " Built during the day: " + built + ".";
                lines.Add(new ReportLine { Part = "change", Title = r.score >= r.prevScore + 0.005f ? "Better than the day before" : r.score <= r.prevScore - 0.005f ? "Worse than the day before" : "Steady", Text = text });
            }
            return lines;
        }

        /// <summary>How far the day was from the grade above, in points (0: it was an A).</summary>
        public static int PointsToNextGrade(float score)
        {
            int g = GradeIndex(score);
            return g == 0 ? 0 : Math.Max(1, (int)Math.Ceiling((GradeAt[g - 1] - score) * 100 - 1e-4));
        }

        static string BuiltName(string id) { return id == "terminal" ? "terminal furniture" : Content.Facilities.ContainsKey(id) ? Content.Facilities[id].Name : id; }
        static string ServiceNoun(Service s) { return s == Service.Fuel ? "fuel" : s == Service.Baggage ? "baggage" : s == Service.Catering ? "catering" : s == Service.Cleaning ? "cleaning" : s == Service.Pushback ? "the tug" : "de-icing"; }

        /// <summary>Gems this report's streak pays on collection (every fifth good day in a row).</summary>
        public int ReportGems(DayReport r) { return r != null && r.streak > 0 && r.streak % StreakEvery == 0 ? StreakGems : 0; }

        /// <summary>Collects the last day's bonus (and the streak's gems). Returns the money paid.</summary>
        public double ClaimReport()
        {
            if (!ReportWaiting) return 0;
            var r = State.report;
            r.claimed = true;
            if (r.bonus > 0) Earn(r.bonus, false);
            State.gems += ReportGems(r);
            Changed?.Invoke();
            return r.bonus;
        }
    }
}
