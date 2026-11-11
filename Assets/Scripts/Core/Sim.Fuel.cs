using System;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// The fuel market. The tank farm holds fuel you buy by the tanker at the day's price; every flight that takes fuel
    /// draws from it and pays the pump price back, plus your margin. Buy below par and the difference is yours; let
    /// the tanks run dry and each aircraft is fuelled by an outside supplier, at a fraction of the margin.
    /// Stock is measured in money at par (a tank "worth 12K" refuels 12K of flights).
    /// </summary>
    public sealed partial class Sim
    {
        public event Action FuelLow;

        /// <summary>Share of the fuel margin left when an outside supplier has to fuel the aircraft.</summary>
        public const float DryMargin = 0.4f;
        /// <summary>Below this share of the tanks, the farm is "low": a warning, and the fuel manager orders.</summary>
        public const float LowFuel = 0.25f;

        /// <summary>The price of fuel on a day (1 = par), the same for everyone on that day: a slow swell with a daily jolt.</summary>
        public static float FuelPriceOn(int day)
        {
            double swell = 0.2 * Math.Sin(day * 0.83 + 1.3) + 0.08 * Math.Sin(day * 2.71 + 0.4);
            uint x = (uint)(day * 2654435761u) ^ 0x9E3779B9u; x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            double jolt = ((x & 0xFFFF) / 65535.0 - 0.5) * 0.16;
            return (float)Math.Max(0.65, Math.Min(1.4, 1 + swell + jolt));
        }

        /// <summary>Game days since this airport opened.</summary>
        public int FuelDay { get { return (int)Math.Floor(State.time / DayLength); } }
        /// <summary>Today's price (storms push it up: the tankers can't get through).</summary>
        public float FuelPrice { get { return FuelPriceOn(FuelDay) * (Weather == Weather.Storm ? 1.1f : 1f) * (1 - Math.Min(0.3f, Fx(Effect.FuelMargin) * 0.2f)); } }
        /// <summary>Tomorrow's, as the traders see it.</summary>
        public float FuelForecast { get { return FuelPriceOn(FuelDay + 1); } }

        /// <summary>What the tanks hold, at par: room for a few dozen of the biggest aircraft the runway takes, more with every depot level.</summary>
        public double FuelCapacity { get { return 2 * Content.Aircraft[MaxClass].FuelMargin * (20 + 10 * Math.Max(1, Level("fuel"))); } }
        public double FuelStock { get { return Math.Min(State.fuelStock, FuelCapacity); } }
        public float FuelFill { get { return (float)(FuelStock / Math.Max(1, FuelCapacity)); } }

        /// <summary>What a flight on this route takes from the tanks, at par.</summary>
        public double FuelDraw(RouteDef r) { return r.Needs.Contains(Service.Fuel) ? 2 * Content.Aircraft[r.Class].FuelMargin : 0; }

        /// <summary>What filling the tanks to this share of full costs today.</summary>
        public double FuelCost(float to = 1f) { return Math.Ceiling(Math.Max(0, FuelCapacity * Math.Min(1f, to) - FuelStock) * FuelPrice); }

        public string BuyFuelBlocked(float to = 1f)
        {
            if (Level("fuel") <= 0) return "Build the Fuel Depot first";
            if (FuelCapacity * Math.Min(1f, to) - FuelStock < 1) return "Tanks full";
            if (State.money < 1) return "No money for fuel";
            return null;
        }

        /// <summary>Buys fuel up to a share of the tanks at today's price, or as much of that as the money covers.</summary>
        public bool BuyFuel(float to = 1f)
        {
            if (BuyFuelBlocked(to) != null) return false;
            double want = FuelCapacity * Math.Min(1f, to) - FuelStock, price = FuelPrice;
            double units = Math.Min(want, State.money / price);
            if (units < 1) return false;
            State.money -= units * price;
            State.fuelStock = FuelStock + units;
            State.fuelSaved += units * (1 - price);
            if (price <= 0.9f && units >= FuelCapacity * 0.25) State.cheapFills++;
            State.fuelWarned = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a departing flight's fuel from the tanks. False: they were dry and a supplier filled it.</summary>
        bool DrawFuel(RouteDef r)
        {
            double draw = FuelDraw(r);
            if (draw <= 0) return true;
            // A fuel manager keeps an eye on the gauge: orders half a farm when it runs low and the price is fair.
            if (State.managers.Contains("fuel") && FuelStock < Math.Max(draw, FuelCapacity * LowFuel) && FuelPrice <= 1.08f && FuelCost(0.6f) <= State.money * 0.5) BuyFuel(0.6f);
            bool had = FuelStock >= draw;
            if (had) { State.fuelStock = FuelStock - draw; State.money += draw; }      // the pump price comes back; the margin is in the flight's pay
            if (!State.fuelWarned && FuelFill < LowFuel) { State.fuelWarned = true; FuelLow?.Invoke(); }
            return had;
        }
    }
}
