using System;

namespace Crosswind.Core
{
    /// <summary>
    /// Maintenance of your own fleet. Every flight puts hours on the aircraft type that flew it; when a type's check
    /// falls due it goes on flying, but under restrictions that cost a quarter of what its flights pay. A check
    /// clears the hours: in your own hangar at cost, or by an outside contractor at twice that. Bigger hangars
    /// stretch the time between checks.
    /// </summary>
    public sealed partial class Sim
    {
        public event Action<AircraftClass> CheckDue;

        /// <summary>Flights between checks, per aircraft of the type.</summary>
        public const int CheckEvery = 40;
        /// <summary>What an overdue type's flights pay.</summary>
        public const double OverduePay = 0.75;

        LevelEntry HoursEntry(AircraftClass c, bool create)
        {
            string id = c.ToString().ToLower();
            var e = State.fleetHours.Find(f => f.id == id);
            if (e == null && create) { e = new LevelEntry { id = id }; State.fleetHours.Add(e); }
            return e;
        }

        /// <summary>Flights a type may make between checks: its aircraft share the flying, and the hangar stretches it.</summary>
        public int CheckInterval(AircraftClass c) { return (int)Math.Round(CheckEvery * Math.Max(1, Owned(c)) * (1 + 0.1 * Level("hangar"))); }
        /// <summary>How far through the interval the type is, 0..1 (1: the check is due).</summary>
        public float CheckProgress(AircraftClass c) { var e = HoursEntry(c, false); return e == null ? 0 : Math.Min(1f, e.level / (float)CheckInterval(c)); }
        public bool CheckOverdue(AircraftClass c) { return Owned(c) > 0 && CheckProgress(c) >= 1f; }
        /// <summary>What your own flights of this type pay for their state of maintenance.</summary>
        public double MaintenancePay(AircraftClass c) { return CheckOverdue(c) ? OverduePay : 1; }

        void FleetFlew(AircraftClass c)
        {
            var e = HoursEntry(c, true);
            bool was = CheckOverdue(c);
            e.level++;
            if (!was && CheckOverdue(c)) CheckDue?.Invoke(c);
        }

        /// <summary>A check on every aircraft of the type: a few flights' worth each, half that in your own hangar.</summary>
        public double CheckCost(AircraftClass c)
        {
            double perAircraft = Content.Aircraft[c].LandingFee * 12 * Map.CostScale;
            return Math.Floor(perAircraft * Math.Max(1, Owned(c)) * (Level("hangar") > 0 ? 1 : 2) * Math.Max(0.25, CheckProgress(c)));
        }

        public string CheckBlocked(AircraftClass c)
        {
            if (Owned(c) <= 0) return "You own none";
            if (CheckProgress(c) < 0.25f) return "Not needed yet";
            if (State.money < CheckCost(c)) return "Need " + Format.Money(CheckCost(c) - State.money) + " more";
            return null;
        }

        public bool DoCheck(AircraftClass c)
        {
            if (CheckBlocked(c) != null) return false;
            State.money -= CheckCost(c);
            HoursEntry(c, true).level = 0;
            State.checksDone++;
            Changed?.Invoke();
            return true;
        }
    }
}
