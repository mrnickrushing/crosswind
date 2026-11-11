using System;

namespace Crosswind.Core
{
    /// <summary>
    /// The runway wears. Every landing and take-off takes a little off the surface, heavy aircraft most; a worn
    /// runway (rubber, cracks, ponding) has to be used more carefully and clears slower. Resurfacing costs money and
    /// closes the runway for a while, so the question is when: the small hours, when little is flying, or now.
    /// </summary>
    public sealed partial class Sim
    {
        public event Action RunwayWorn;
        public event Action RunwayReopened;

        /// <summary>Condition below which the runway slows down.</summary>
        public const float WornAt = 0.5f;
        /// <summary>How slow a runway worn right out is.</summary>
        public const float WornSpeed = 0.8f;
        /// <summary>Game seconds the runway is shut for resurfacing.</summary>
        public const float ResurfaceSeconds = 45f;

        /// <summary>1 fresh .. 0 worn out.</summary>
        public float RunwayCondition { get { return 1 - Math.Max(0, Math.Min(1, State.runwayWear)); } }
        /// <summary>How the surface scales runway speed: full down to half condition, then falling to <see cref="WornSpeed"/>.</summary>
        public float SurfaceFactor { get { float c = RunwayCondition; return c >= WornAt ? 1 : WornSpeed + (1 - WornSpeed) * c / WornAt; } }
        public bool RunwayClosed { get { return State.time < State.runwayClosedUntil; } }
        public double RunwayClosedLeft { get { return Math.Max(0, State.runwayClosedUntil - State.time); } }

        /// <summary>What one movement of this class takes off the surface: better-built runways wear slower.</summary>
        public float WearOf(AircraftClass c)
        {
            float[] w = { 0.0004f, 0.0008f, 0.0015f, 0.003f, 0.005f };
            return w[(int)c] / (1 + 0.15f * Math.Max(0, Level("runway") - 1)) * (1 - Math.Min(0.5f, Level("hangar") * 0.03f));
        }

        void WearRunway(AircraftClass c)
        {
            bool was = RunwayCondition >= WornAt;
            State.runwayWear = Math.Min(1f, State.runwayWear + WearOf(c));
            if (was && RunwayCondition < WornAt) RunwayWorn?.Invoke();
        }

        public double ResurfaceCost { get { return Math.Floor(400 * Math.Pow(1.6, Math.Max(0, Level("runway") - 1)) * Map.CostScale * (0.4 + 0.6 * State.runwayWear)); } }

        public string ResurfaceBlocked()
        {
            if (RunwayClosed) return "Crews are on the runway";
            if (State.runwayWear < 0.1f) return "The surface is good";
            if (State.flights.Exists(f => f.phase == Phase.Landing || f.phase == Phase.Takeoff)) return "An aircraft is on the runway";
            if (State.money < ResurfaceCost) return "Need " + Format.Money(ResurfaceCost - State.money) + " more";
            return null;
        }

        /// <summary>Closes the runway and relays it: nothing lands or leaves until the crews are off.</summary>
        public bool Resurface()
        {
            if (ResurfaceBlocked() != null) return false;
            State.money -= ResurfaceCost;
            State.runwayWear = 0;
            State.runwayClosedUntil = State.time + ResurfaceSeconds;
            State.resurfaced++;
            Changed?.Invoke();
            return true;
        }
    }
}
