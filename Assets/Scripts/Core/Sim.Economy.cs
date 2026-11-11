using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    public sealed partial class Sim
    {
        /// <summary>One thing passengers want, how well the airport answers it (0..1), and what would help.</summary>

        /// <summary>Passengers a minute the signed routes bring through the terminal (both ways).</summary>
        public double PassengerDemand
        {
            get { return State.contracts.Sum(c => { var r = Content.Routes[c.route]; return Content.Aircraft[r.Class].Seats * 2 * r.FlightsPerHour * TermsFlights(c.terms) / 60.0; }) / 10.0; }
        }

        /// <summary>The longest an on-time visit takes for this route at this airport, parked to gone.</summary>
        public float OnTimeTarget(RouteDef r)
        {
            var plane = Content.Aircraft[r.Class];
            float ideal = plane.BoardSeconds * 2 + plane.ServiceSeconds * (1 + r.Needs.Where(Content.OnDeparture).Sum(Content.ServiceShare)) + plane.TaxiSeconds + plane.RunwaySeconds;
            return ideal * Content.OnTimeSlack * Content.Airlines[r.Airline].Patience * (1 + Fx(Effect.OnTimeSlack));
        }

        /// <summary>What a flight on this route pays when it leaves on time, at today's satisfaction.</summary>
        public double FlightValue(RouteDef r, bool onTime = true, double[] parts = null)
        {
            var plane = Content.Aircraft[r.Class];
            var line = Content.Airlines[r.Airline];
            float sat = Satisfaction;
            double landing = plane.LandingFee * line.FeeScale * (onTime ? 1 + Content.OnTimeBonus : Content.LateShare) * (1 + Fx(Effect.LandingFee)) * (1 + 0.05 * Level("hangar"));
            double pax = plane.Seats * 2 * Content.PassengerFee * sat * (1 + Fx(Effect.PassengerFee)) * (1 + 0.06 * LoungeLevel);
            double fuel = r.Needs.Contains(Service.Fuel) ? plane.FuelMargin * (1 + 0.05 * (Level("fuel") - 1)) * (1 + Fx(Effect.FuelMargin)) : 0;
            double retail = plane.Seats * 2 * Content.RetailSpend * RetailPull * sat * (1 + Fx(Effect.Retail));
            double cargo = plane.Seats * 0.8 * Level("cargo") * line.FeeScale;
            if (parts != null) { parts[0] = landing; parts[1] = pax; parts[2] = fuel; parts[3] = retail; parts[4] = cargo; }
            return (landing + pax + fuel + retail + cargo) * IncomeScale;
        }

        /// <summary>The least a flight on this route spends at its stand with every crew to hand: off, serviced, on, pushed back, taxiing.</summary>
        public double GroundSeconds(RouteDef r)
        {
            var plane = Content.Aircraft[r.Class];
            var parked = r.Needs.Where(n => !Content.OnDeparture(n)).ToList();
            double service = parked.Count == 0 ? 0 : plane.ServiceSeconds / Math.Max(0.1f, parked.Min(n => CrewSpeed(n)));
            foreach (var n in r.Needs.Where(Content.OnDeparture)) service += plane.ServiceSeconds * Content.ServiceShare(n) / Math.Max(0.1f, CrewSpeed(n));
            return BoardTime(plane) * 2 + service + plane.TaxiSeconds * 2 / TaxiSpeed;
        }

        void Pay(FlightState f)
        {
            var r = Content.Routes[f.route];
            var contract = State.contracts.Find(c => c.route == f.route);
            int terms = contract != null ? contract.terms : 0;
            bool onTime = State.time - f.parkedAt - f.held <= OnTimeTarget(r) * TermsPatience(terms);
            var parts = new double[5];
            double value = FlightValue(r, onTime, parts);
            // Dry tanks: an outside supplier fuels the aircraft and keeps most of the margin.
            bool dry = !DrawFuel(r);
            if (dry) { value -= parts[2] * (1 - DryMargin) * IncomeScale; parts[2] *= DryMargin; }
            double paid = value * (State.time < State.rushUntil ? 2 : 1) * TermsPay(terms) * SlotPay(r, f.slot) * (r.Airline == "own" ? OwnPay * MaintenancePay(r.Class) : RelationPay(r.Airline) * FeePay * PartnerPay(r.Airline) * RepPay(r.Airline));
            if (r.Airline == "own") FleetFlew(r.Class);
            if (r.Airline != "own") Relate(r.Airline, (onTime ? 0.4f : -2.5f) + FeeMoodPerFlight);
            if (r.Airline != "own") CountDeal(r, onTime, paid);
            double sum = parts[0] + parts[1] + parts[2] + parts[3] + parts[4];
            if (sum > 0) for (int i = 0; i < 5; i++) State.incomeBy[i] += paid * parts[i] / sum;
            if (sum > 0) { var t = State.today; t.landing += paid * parts[0] / sum; t.pax += paid * parts[1] / sum; t.fuel += paid * parts[2] / sum; t.retail += paid * parts[3] / sum; t.cargo += paid * parts[4] / sum; }
            Earn(AfterLoan(paid), true);
            CountDay(onTime, dry, paid);
            if (f.contract != 0) ContractFlew(f, onTime, dry);
            State.flightsHandled++;
            Count("flights", 1);
            if (onTime) Count("ontime", 1);
            if (onTime) State.onTime++; else State.late++;
            AddRating((onTime ? 2 : 0.5) * Math.Pow(3.2, (int)r.Class));       // bigger aircraft make the airport's name
            FlightPaid?.Invoke(f, paid, onTime);
            Changed?.Invoke();
        }

        void AddRating(double xp)
        {
            int before = Stars;
            State.ratingXp += xp * (1 + Fx(Effect.RatingGain));
            int after = Stars;
            for (int s = before + 1; s <= after; s++) { State.crates++; StarGained?.Invoke(s); }     // every star brings a crate
        }

        // ------------------------------------------------------------------ managers

        /// <summary>What a manager for this department costs (services only for now).</summary>
        public double ManagerCost(string facility) { return Math.Floor(Content.Facilities[facility].BaseCost * 12); }

        public string HireBlocked(string facility)
        {
            if (Array.IndexOf(Content.Departments, facility) < 0) return "No manager for this";
            if (State.managers.Contains(facility)) return "Already hired";
            if (Level(facility) <= 0) return "Build it first";
            if (State.money < ManagerCost(facility)) return "Need " + Format.Money(ManagerCost(facility) - State.money) + " more";
            return null;
        }

        /// <summary>Hires a manager: the department dispatches its own crews from now on, with or without you.</summary>
        public bool Hire(string facility)
        {
            if (HireBlocked(facility) != null) return false;
            State.money -= ManagerCost(facility);
            State.managers.Add(facility);
            AddRating(15);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ away

        /// <summary>
        /// Income per game second with nobody tapping: only routes whose every service has a manager turn around by
        /// themselves, and the runway, the stands and the crews cap how many flights an hour get through.
        /// </summary>
        public double IdleRate()
        {
            double perSecond = 0;
            double runwayLoad = 0, standLoad = 0, heavyLoad = 0;
            var crewLoad = new double[Content.Departments.Length];
            foreach (var c in State.contracts)
            {
                var r = Content.Routes[c.route];
                if (!r.Needs.All(Automated) || !Active(r.Airline)) continue;
                var plane = Content.Aircraft[r.Class];
                // A slotted route flies three quarters of the day's flights.
                double perSec = r.FlightsPerHour * (1 + Fx(Effect.Demand)) * FleetFlights(r) / 3600.0 * (c.slot > 0 ? SlotRate * 6 / 24 : 1) * (r.Airline == "own" ? 1 : FeeFlights);
                double ground = GroundSeconds(r);
                runwayLoad += perSec * 2 * plane.RunwaySeconds / RunwaySpeed;
                standLoad += perSec * ground;
                if (Heavy(r.Class)) heavyLoad += perSec * ground;
                foreach (var n in r.Needs) crewLoad[(int)n] += perSec * plane.ServiceSeconds * Content.ServiceShare(n) / Math.Max(0.1f, CrewSpeed(n));
                perSecond += perSec * FlightValue(r, true) * SlotPay(r, c.slot) * (r.Airline == "own" ? 1 : FeePay);
            }
            if (perSecond <= 0) return ParkingRate;
            // The tightest bottleneck scales everything down.
            double cap = 1;
            if (runwayLoad > 0) cap = Math.Min(cap, RunwayLanes / runwayLoad);
            if (standLoad > 0) cap = Math.Min(cap, StandCount / standLoad);
            if (heavyLoad > 0) cap = Math.Min(cap, HeavyStands / heavyLoad);
            for (int i = 0; i < crewLoad.Length; i++) if (crewLoad[i] > 0) cap = Math.Min(cap, Crews((Service)i) / crewLoad[i]);
            return perSecond * Math.Min(1, cap) + ParkingRate;
        }

        /// <summary>What the airport earned while the app was closed for this many real seconds (0 without managers).</summary>
        public double OfflineEarnings(double secondsAway)
        {
            double counted = Math.Max(0, Math.Min(secondsAway, OfflineCapSeconds));
            return Math.Floor(IdleRate() * counted * Math.Min(1.0, Content.OfflineEfficiency + Fx(Effect.OfflineEfficiency)));
        }

        /// <summary>Pays the offline earnings (the welcome-back card's COLLECT) and returns them.</summary>
        public double CollectOffline(double secondsAway, double multiplier = 1)
        {
            double got = Math.Floor(OfflineEarnings(secondsAway) * Math.Max(1, multiplier));
            if (got <= 0) return 0;
            Earn(AfterLoan(got), false);
            Changed?.Invoke();
            return got;
        }
    }
}
