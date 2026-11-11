using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    public sealed partial class Sim
    {
        /// <summary>One thing passengers want, how well the airport answers it (0..1), and what would help.</summary>
        public struct Need { public string Name, Advice; public float Value; }

        /// <summary>Passengers a minute the signed routes bring through the terminal (both ways).</summary>
        public double PassengerDemand
        {
            get { return State.contracts.Sum(c => { var r = Content.Routes[c.route]; return Content.Aircraft[r.Class].Seats * 2 * r.FlightsPerHour * TermsFlights(c.terms) / 60.0; }) / 10.0; }
        }

        /// <summary>What passengers think of the place, need by need: queues, comfort, food and shopping, finding the way.</summary>
        public List<Need> Needs
        {
            get
            {
                double demand = Math.Max(1, PassengerDemand);
                float Fit(double supply) { return (float)Math.Max(0, Math.Min(1, supply / demand)); }
                return new List<Need>
                {
                    new Need { Name = "Short queues", Value = Fit(TerminalFlow), Advice = "Upgrade the slowest of check-in, security and gates" },
                    new Need { Name = "Comfort", Value = Fit(4 + Level("seating") * 7 + Level("lounge") * 5), Advice = "Add seating and toilets" },
                    new Need { Name = "Food and shops", Value = Fit(2 + Level("shops") * 6), Advice = "Open more shops and cafes" },
                    new Need { Name = "Finding the way", Value = Fit(6 + Level("info") * 9), Advice = "Improve information and wayfinding" },
                };
            }
        }

        /// <summary>How pleased passengers are, 0.4..1.5: queues count most, then comfort, food and wayfinding; lounges add a little on top.</summary>
        public float Satisfaction
        {
            get
            {
                var n = Needs;
                float v = 0.4f + 0.3f * n[0].Value + 0.12f * n[1].Value + 0.1f * n[2].Value + 0.08f * n[3].Value + 0.02f * Level("shops") + 0.01f * Level("lounge");
                return Math.Max(0.4f, Math.Min(1.5f, v));
            }
        }

        /// <summary>The longest an on-time visit takes for this route at this airport, parked to gone.</summary>
        public float OnTimeTarget(RouteDef r)
        {
            var plane = Content.Aircraft[r.Class];
            float ideal = plane.BoardSeconds * 2 + plane.ServiceSeconds + plane.TaxiSeconds + plane.RunwaySeconds;
            return ideal * Content.OnTimeSlack * Content.Airlines[r.Airline].Patience * (1 + Fx(Effect.OnTimeSlack));
        }

        /// <summary>What a flight on this route pays when it leaves on time, at today's satisfaction.</summary>
        public double FlightValue(RouteDef r, bool onTime = true)
        {
            var plane = Content.Aircraft[r.Class];
            var line = Content.Airlines[r.Airline];
            float sat = Satisfaction;
            double landing = plane.LandingFee * line.FeeScale * (onTime ? 1 + Content.OnTimeBonus : Content.LateShare) * (1 + Fx(Effect.LandingFee)) * (1 + 0.05 * Level("hangar"));
            double pax = plane.Seats * 2 * Content.PassengerFee * sat * (1 + Fx(Effect.PassengerFee)) * (1 + 0.06 * Level("lounge"));
            double fuel = r.Needs.Contains(Service.Fuel) ? plane.FuelMargin * (1 + 0.05 * (Level("fuel") - 1)) * (1 + Fx(Effect.FuelMargin)) : 0;
            double retail = plane.Seats * 2 * Content.RetailSpend * Level("shops") * sat * (1 + Fx(Effect.Retail));
            double cargo = plane.Seats * 0.8 * Level("cargo") * line.FeeScale;
            return (landing + pax + fuel + retail + cargo) * IncomeScale;
        }

        void Pay(FlightState f)
        {
            var r = Content.Routes[f.route];
            var contract = State.contracts.Find(c => c.route == f.route);
            int terms = contract != null ? contract.terms : 0;
            bool onTime = State.time - f.parkedAt <= OnTimeTarget(r) * TermsPatience(terms);
            double paid = FlightValue(r, onTime) * (State.time < State.rushUntil ? 2 : 1) * TermsPay(terms) * (r.Airline == "own" ? OwnPay : RelationPay(r.Airline));
            if (r.Airline != "own") Relate(r.Airline, onTime ? 0.4f : -2.5f);
            Earn(paid, true);
            State.flightsHandled++;
            Count("flights", 1);
            if (onTime) Count("ontime", 1);
            if (onTime) State.onTime++; else State.late++;
            AddRating((onTime ? 2 : 0.5) * (1 + (int)r.Class * 2));
            FlightPaid?.Invoke(f, paid, onTime);
            Changed?.Invoke();
        }

        void AddRating(double xp)
        {
            int before = Stars;
            State.ratingXp += xp * (1 + Fx(Effect.RatingGain));
            int after = Stars;
            for (int s = before + 1; s <= after; s++) StarGained?.Invoke(s);
        }

        // ------------------------------------------------------------------ managers

        /// <summary>What a manager for this department costs (services only for now).</summary>
        public double ManagerCost(string facility) { return Math.Floor(Content.Facilities[facility].BaseCost * 12); }

        public string HireBlocked(string facility)
        {
            if (facility != "fuel" && facility != "baggage" && facility != "catering" && facility != "cleaning") return "No manager for this";
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
            double runwayLoad = 0, standLoad = 0;
            var crewLoad = new double[4];
            foreach (var c in State.contracts)
            {
                var r = Content.Routes[c.route];
                if (!r.Needs.All(Automated)) continue;
                var plane = Content.Aircraft[r.Class];
                double perSec = r.FlightsPerHour * (1 + Fx(Effect.Demand)) * FleetFlights(r) / 3600.0;
                double ground = BoardTime(plane) * 2 + plane.ServiceSeconds / Math.Max(0.1f, r.Needs.Min(n => CrewSpeed(n))) + plane.TaxiSeconds * 2 / TaxiSpeed;
                runwayLoad += perSec * 2 * plane.RunwaySeconds / RunwaySpeed;
                standLoad += perSec * ground;
                foreach (var n in r.Needs) crewLoad[(int)n] += perSec * plane.ServiceSeconds / Math.Max(0.1f, CrewSpeed(n));
                perSecond += perSec * FlightValue(r, true);
            }
            if (perSecond <= 0) return ParkingRate;
            // The tightest bottleneck scales everything down.
            double cap = 1;
            if (runwayLoad > 0) cap = Math.Min(cap, RunwayLanes / runwayLoad);
            if (standLoad > 0) cap = Math.Min(cap, StandCount / standLoad);
            for (int i = 0; i < 4; i++) if (crewLoad[i] > 0) cap = Math.Min(cap, Crews((Service)i) / crewLoad[i]);
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
            Earn(got, false);
            Changed?.Invoke();
            return got;
        }
    }
}
