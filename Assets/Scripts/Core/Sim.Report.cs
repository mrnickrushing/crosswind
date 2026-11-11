using System;

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
                var r = new DayReport
                {
                    day = State.dayIndex + 1, flights = State.dayFlights, onTime = State.dayOnTime, dry = State.dayDry, income = State.dayIncome,
                    satisfaction = Satisfaction, runway = RunwayCondition, score = score, grade = Grades[g],
                    bonus = Math.Floor(State.dayIncome * GradeBonus[g]) + carried, streak = streak,
                    costs = State.ledger.Count > 0 ? State.ledger[0].Costs : 0,
                };
                State.gradeStreak = streak;
                State.report = r;
                if (g == 0) State.daysAtA++;
                DayEnded?.Invoke(r);
                Changed?.Invoke();
            }
            State.dayIndex = today; State.dayFlights = State.dayOnTime = State.dayDry = 0; State.dayIncome = 0;
            OpenLedger();
        }

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
