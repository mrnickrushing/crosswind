using System;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>One airport day's books.</summary>
    [Serializable]
    public sealed class DayLedger
    {
        public int day;                         // which day of this airport (1 = the first)
        public double landing, pax, fuel, retail, cargo;     // what flights paid, by source
        public double earned;                   // everything banked that day (flights, parking, bonuses, contract rewards)
        public double loanPaid;                 // the bank's share, taken before it was banked
        public double wages, upkeep;            // the day's running costs
        public double fuelBought, fuelBack;     // tankers bought, and the pump price returned by departures
        public double borrowed, repaid;         // loans drawn and paid down by hand
        public double spent;                    // building, hiring, research and other purchases
        public double closed;                   // cash as the day closed

        public double Flights { get { return landing + pax + fuel + retail + cargo; } }
        /// <summary>Income before the bank's share: flights and everything else.</summary>
        public double Income { get { return earned + loanPaid; } }
        public double Other { get { return Math.Max(0, Income - Flights); } }
        public double Costs { get { return wages + upkeep; } }
        /// <summary>What the day's running made: income less the running costs and the fuel bill.</summary>
        public double Profit { get { return Income - Costs - Math.Max(0, fuelBought - fuelBack); } }
    }

    /// <summary>
    /// The books. Every airport day is closed into a ledger: income by source, the wages of everyone on the payroll,
    /// the upkeep of everything airside, fuel bought and sold, loans, and what went on building. Wages and upkeep are
    /// charged as the day closes, in proportion to how far each department and structure has been built up: a big
    /// airport standing idle still costs money.
    /// </summary>
    public sealed partial class Sim
    {
        public const int LedgerDays = 7;
        /// <summary>A day's wages (and upkeep), as a share of what the last level of each department (structure) cost.</summary>
        public const double WageRate = 0.036, UpkeepRate = 0.024;

        static readonly string[] Staffed = { "fuel", "baggage", "catering", "cleaning", "pushback", "deice", "checkin", "security", "gates", "hangar", "cargo" };
        static readonly string[] Kept = { "runway", "stands", "heavy", "taxiways", "tower", "ils", "parking" };

        /// <summary>What the last level bought of a facility cost (nothing if it isn't built).</summary>
        double LastCost(string facility)
        {
            var d = Content.Facilities[facility];
            int l = Level(facility);
            return l <= 0 ? 0 : d.BaseCost * Math.Pow(d.Growth, l - 1 - d.StartLevel) * Map.CostScale;
        }

        /// <summary>A day's wages: the crews and desks of every department, their managers, and the staff of what stands in the terminal.</summary>
        public double Wages
        {
            get
            {
                double sum = Staffed.Sum(f => LastCost(f));
                sum += State.managers.Sum(m => ManagerCost(m) * 0.25 * ManagerLevel(m));
                sum += State.terminal.Sum(p => { var d = AmenityById(p.id); return d == null ? 0 : d.Cost * Math.Pow(2, p.level - 1) * (d.Earns ? 1.0 : 0.4) * Map.CostScale; });
                return Math.Floor(sum * WageRate);
            }
        }

        /// <summary>A day's upkeep of the runway, the stands, the taxiways, the tower and its aids, and the car park.</summary>
        public double Upkeep { get { return Math.Floor(Kept.Sum(f => LastCost(f)) * UpkeepRate); } }
        public double RunningCosts { get { return Wages + Upkeep; } }

        /// <summary>Closes the day's books (from TurnDay) and charges the running costs: never more than there is.</summary>
        void CloseLedger(int day)
        {
            var t = State.today;
            t.day = day;
            double wages = Math.Min(State.money, Wages);
            State.money -= wages;
            double upkeep = Math.Min(State.money, Upkeep);
            State.money -= upkeep;
            t.wages = wages; t.upkeep = upkeep;
            if (State.dayOpenMoney >= 0)
                t.spent = Math.Max(0, State.dayOpenMoney + t.earned - t.wages - t.upkeep - t.fuelBought + t.fuelBack + t.borrowed - t.repaid - State.money);
            t.closed = State.money;
            State.ledger.Insert(0, t);
            if (State.ledger.Count > LedgerDays) State.ledger.RemoveRange(LedgerDays, State.ledger.Count - LedgerDays);
        }

        void OpenLedger()
        {
            State.today = new DayLedger();
            State.dayOpenMoney = State.money;
        }

        /// <summary>What has gone on building and purchases so far today.</summary>
        public double SpentToday
        {
            get
            {
                var t = State.today;
                return State.dayOpenMoney < 0 ? 0 : Math.Max(0, State.dayOpenMoney + t.earned - t.fuelBought + t.fuelBack + t.borrowed - t.repaid - State.money);
            }
        }

        /// <summary>The share of all flights that have left on time (1 before the first).</summary>
        public float OnTimeShare { get { int n = State.onTime + State.late; return n > 0 ? State.onTime / (float)n : 1f; } }
        /// <summary>The same for today alone.</summary>
        public float OnTimeToday { get { return State.dayFlights > 0 ? State.dayOnTime / (float)State.dayFlights : 1f; } }
    }
}
