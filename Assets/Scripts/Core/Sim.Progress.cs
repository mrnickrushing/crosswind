using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>What lasts: research, manager levels, the income boost, and selling up to open the next airport.</summary>
    public sealed partial class Sim
    {
        public MapDef Map { get { return Content.Maps.Find(m => m.Id == State.map) ?? Content.Maps[0]; } }

        /// <summary>The total of one effect from finished research, this map's twist and the team on duty.</summary>
        public float Fx(Effect e)
        {
            float v = 0;
            foreach (var id in State.researched) { var r = Content.ResearchById(id); if (r != null && r.Effect == e) v += r.Value; }
            switch (State.map)
            {
                case "coral": if (e == Effect.Retail) v += 1f; break;
                case "dune": if (e == Effect.FuelMargin) v += 0.5f; break;
                case "polar": if (e == Effect.LandingFee) v += 1f; break;
                case "metro": if (e == Effect.Demand) v += 1f; break;
            }
            return v + CrewFx(e);
        }

        public bool Boosted { get { return RealNow() < State.boostUntil; } }
        public double BoostLeft { get { return Math.Max(0, State.boostUntil - RealNow()); } }

        /// <summary>Everything that multiplies what the airport earns: the map, research, Wings, and a running boost.</summary>
        public double IncomeScale
        {
            get { return Map.IncomeScale * (1 + Fx(Effect.Income)) * (1 + State.wings * Content.WingBonus) * (Boosted ? Content.BoostScale : 1) * (Owns("crosswind.double") ? 2 : 1); }
        }

        /// <summary>The car park and transit: money every second, flights or not.</summary>
        public double ParkingRate { get { return Level("parking") * 0.4 * Stars * IncomeScale; } }

        public double OfflineCapSeconds { get { return (Content.OfflineHours + Fx(Effect.OfflineHours)) * 3600; } }

        /// <summary>Adds money and keeps the books (the smoothed rate only follows what the airport itself earns).</summary>
        void Earn(double amount, bool operating)
        {
            if (amount <= 0) return;
            State.money += amount;
            State.today.earned += amount;
            State.lifetimeEarned += amount;
            State.sessionEarned += amount;
            Count("earn", amount);
            if (operating) State.incomeRate += amount * Math.Log(2) / 60.0;
        }

        // ------------------------------------------------------------------ manager levels

        public int ManagerLevel(string facility)
        {
            if (!State.managers.Contains(facility)) return 0;
            var e = State.managerLevels.Find(l => l.id == facility);
            return e != null ? e.level : 1;
        }

        public const int MaxManagerLevel = 10;
        public double TrainCost(string facility) { return Math.Floor(ManagerCost(facility) * Math.Pow(3, ManagerLevel(facility))); }

        public string TrainBlocked(string facility)
        {
            if (!State.managers.Contains(facility)) return "Hire them first";
            if (ManagerLevel(facility) >= MaxManagerLevel) return "Fully trained";
            if (State.money < TrainCost(facility)) return "Need " + Format.Money(TrainCost(facility) - State.money) + " more";
            return null;
        }

        /// <summary>Trains a manager: their department's crews work 10% faster per level.</summary>
        public bool Train(string facility)
        {
            if (TrainBlocked(facility) != null) return false;
            State.money -= TrainCost(facility);
            int next = ManagerLevel(facility) + 1;
            var e = State.managerLevels.Find(l => l.id == facility);
            if (e == null) State.managerLevels.Add(new LevelEntry { id = facility, level = next }); else e.level = next;
            AddRating(10);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ research

        public bool Researched(string id) { return State.researched.Contains(id); }
        public double ResearchLeft { get { return State.research == null ? 0 : Math.Max(0, State.research.doneAt - RealNow()); } }

        public string ResearchBlocked(string id)
        {
            var r = Content.ResearchById(id);
            if (r == null) return "Unknown";
            if (Researched(id)) return "Done";
            if (State.research != null) return State.research.id == id ? "Under way" : "The lab is busy";
            if (r.Requires != null && !Researched(r.Requires)) return "Needs " + Content.ResearchById(r.Requires).Name;
            if (Stars < r.Stars) return "Needs a " + r.Stars + "-star airport";
            if (State.money < ResearchCost(r)) return "Need " + Format.Money(ResearchCost(r) - State.money) + " more";
            return null;
        }

        public double ResearchCost(ResearchDef r) { return Math.Floor(r.Cost * Map.CostScale); }

        public bool StartResearch(string id)
        {
            if (ResearchBlocked(id) != null) return false;
            var r = Content.ResearchById(id);
            State.money -= ResearchCost(r);
            State.research = new ResearchJob { id = id, doneAt = RealNow() + r.Seconds };
            Changed?.Invoke();
            return true;
        }

        /// <summary>Gems to finish the running project now: one a minute left, at least one.</summary>
        public int ResearchSkipGems { get { return State.research == null ? 0 : Math.Max(1, (int)Math.Ceiling(ResearchLeft / 60)); } }

        public bool SkipResearch()
        {
            if (State.research == null || State.gems < ResearchSkipGems) return false;
            State.gems -= ResearchSkipGems;
            State.research.doneAt = RealNow();
            FinishResearch();
            return true;
        }

        void FinishResearch()
        {
            if (State.research == null || RealNow() < State.research.doneAt) return;
            string id = State.research.id;
            State.research = null;
            if (!State.researched.Contains(id)) State.researched.Add(id);
            Count("research", 1);
            AddRating(20);
            Toast?.Invoke("Research finished: " + Content.ResearchById(id).Name);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ the boost

        /// <summary>Adds half an hour of double income (a rewarded ad, or bought with gems). Stacks up to four hours.</summary>
        public void AddBoost()
        {
            double from = Math.Max(RealNow(), State.boostUntil);
            State.boostUntil = Math.Min(RealNow() + 4 * 3600, from + Content.BoostMinutes * 60);
            Changed?.Invoke();
        }

        public const int FreeBoostHours = 4;
        public bool FreeBoostReady { get { return RealNow() >= State.freeBoostAt; } }
        public double FreeBoostIn { get { return Math.Max(0, State.freeBoostAt - RealNow()); } }

        /// <summary>A boost on the house every four hours: a reason to look in.</summary>
        public bool TakeFreeBoost()
        {
            if (!FreeBoostReady) return false;
            State.freeBoostAt = RealNow() + FreeBoostHours * 3600;
            AddBoost();
            return true;
        }

        public bool BuyBoost()
        {
            if (State.gems < Content.BoostGems) return false;
            State.gems -= Content.BoostGems;
            AddBoost();
            return true;
        }

        // ------------------------------------------------------------------ selling up

        /// <summary>Wings this airport is worth now: they grow with the square root of everything it earned.</summary>
        public int WingsOnSale
        {
            get { return Stars < Content.SellStars ? 0 : (int)Math.Floor(Math.Sqrt(State.lifetimeEarned / (2e6 * Map.IncomeScale)) + (Stars - Content.SellStars) * 2); }
        }

        public IEnumerable<MapDef> MapsOpen { get { return Content.Maps.Where(m => State.wings + WingsOnSale >= m.WingsToOpen); } }

        public string SellBlocked(string toMap)
        {
            var m = Content.Maps.Find(x => x.Id == toMap);
            if (m == null) return "Unknown";
            if (Stars < Content.SellStars) return "Reach " + Content.SellStars + " stars to sell your stake";
            if (State.wings + WingsOnSale < m.WingsToOpen) return "Needs " + m.WingsToOpen + " Wings";
            return null;
        }

        /// <summary>
        /// Sells the stake in this airport for Wings and opens another: buildings, routes, money and rating start
        /// again there; Wings, gems, research and achievements stay.
        /// </summary>
        public bool SellAndMove(string toMap)
        {
            if (SellBlocked(toMap) != null) return false;
            int gained = WingsOnSale;
            var old = State;
            if (!old.mapsRun.Contains(old.map)) old.mapsRun.Add(old.map);
            var fresh = GameState.New();
            old.wings += gained;
            old.map = toMap;
            old.time = fresh.time; old.money = Content.StartMoney * Content.Maps.Find(x => x.Id == toMap).CostScale;
            old.ratingXp = 0; old.lifetimeEarned = 0; old.flightsHandled = 0; old.onTime = 0; old.late = 0;
            old.levels = fresh.levels; old.contracts = fresh.contracts; old.flights = fresh.flights;
            old.managers = new List<string>(); old.managerLevels = new List<LevelEntry>(); old.terminal = new List<PlacedAmenity>(); old.bays = new List<int>(); old.baysBought = 0; old.baysSet = false; old.baysV2 = true; old.feeLevel = 2; old.incomeBy = new double[5];
            old.fleet = new List<LevelEntry>(); old.relations = new List<LevelEntry>(); old.builds = new List<BuildJob>();
            old.goalIndex = Math.Min(Goals.All.Count, 7);    // the guide runs again at each airport, without the first-minute steps
            old.incomeRate = 0; old.weather = 0; old.weatherUntil = 0; old.opportunity = null; old.nextOpportunityAt = 210; old.rushUntil = 0; old.incident = null; old.nextIncidentAt = 600; old.moodShift = 0; old.moodUntil = 0;
            EnsureBays();
            Count("sell", 1);
            Changed?.Invoke();
            return true;
        }
    }
}
