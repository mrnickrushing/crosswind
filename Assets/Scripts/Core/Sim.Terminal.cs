using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>What passengers want from the terminal, beyond short queues.</summary>
    public enum Want { Food, Shopping, Seating, Toilets, Fun, Clean, Info, Lounge }

    /// <summary>Something the player places on the concourse floor: what it is, the room it takes, and what it gives.</summary>
    public sealed class AmenityDef
    {
        public string Id, Name, Group, Blurb;
        public int W, D;                // footprint in floor cells (2 m each), before turning
        public double Cost;
        public int Stars;
        public Want Gives; public int Points;
        public Want Also; public int AlsoPoints;        // a second, smaller benefit (0: none)
        public bool Earns;              // food and shops take money from every passenger
    }

    public sealed class PlacedAmenity { public string id; public int x, z, rot, level = 1; }

    /// <summary>One line of the terminal's report: a need, how well it is met (0..1), and what to do about it.</summary>
    public sealed class Need { public string Name, Advice; public float Value; public bool Queue; public Want Want; }

    /// <summary>
    /// The terminal you furnish. Check-in, security and the gates are built by level; everything between them is
    /// yours to place on the concourse floor: cafes and restaurants, shops, seats, toilets, play areas, bins and
    /// cleaners, information desks, lounges. Each raises one of the passengers' ratings; the ratings together are
    /// their satisfaction, which multiplies what they pay and spend. Anything placed can be upgraded (three levels),
    /// moved for nothing or sold back for half.
    /// </summary>
    public sealed partial class Sim
    {
        public const int MaxAmenityLevel = 3;
        public const float Cell = 2f;

        public static readonly List<AmenityDef> Amenities = new List<AmenityDef>
        {
            A("vending", "Vending Machines", "Food", "A snack for the hurried.", 1, 1, 150, 1, Want.Food, 3),
            A("cafe", "Coffee Bar", "Food", "Coffee and pastries. Everyone stops.", 2, 2, 400, 2, Want.Food, 10, earns: true),
            A("noodle", "Noodle Kitchen", "Food", "A hot meal in ten minutes.", 3, 2, 1400, 3, Want.Food, 18, earns: true),
            A("restaurant", "Runway Grill", "Food", "Table service with a view of the apron.", 3, 3, 4200, 4, Want.Food, 30, Want.Seating, 6, earns: true),
            A("newsstand", "Newsstand", "Shops", "Papers, sweets, a paperback for the flight.", 2, 1, 350, 2, Want.Shopping, 6, earns: true),
            A("gifts", "Gift Shop", "Shops", "Something for the people at home.", 2, 2, 800, 2, Want.Shopping, 10, earns: true),
            A("fashion", "Fashion Boutique", "Shops", "For passengers with an hour and a card.", 3, 2, 2600, 4, Want.Shopping, 18, earns: true),
            A("dutyfree", "Duty Free", "Shops", "The big one. Everyone walks through it.", 4, 3, 9000, 5, Want.Shopping, 36, earns: true),
            A("seats", "Bench Seating", "Comfort", "Somewhere to sit near the gate.", 2, 1, 200, 2, Want.Seating, 6),
            A("recliners", "Rest Zone", "Comfort", "Recliners and quiet. Long layovers love it.", 3, 2, 1200, 3, Want.Seating, 16),
            A("charging", "Charging Bar", "Comfort", "Stools, sockets, a place for the laptop.", 2, 1, 500, 3, Want.Seating, 4, Want.Fun, 4),
            A("lounge", "Premium Lounge", "Comfort", "Paid entry: the passenger fee rises.", 4, 3, 6000, 4, Want.Lounge, 10, Want.Seating, 8),
            A("toilets", "Toilets", "Hygiene", "Clean, close and enough of them.", 2, 2, 500, 2, Want.Toilets, 10),
            A("fountain", "Water Station", "Hygiene", "Bottle refills.", 1, 1, 120, 2, Want.Toilets, 3),
            A("bins", "Recycling Bins", "Hygiene", "Keeps the floor clear.", 1, 1, 80, 2, Want.Clean, 4),
            A("janitor", "Cleaning Station", "Hygiene", "A crew with machines, on the floor all day.", 2, 1, 900, 3, Want.Clean, 14),
            A("plants", "Planter", "Hygiene", "Green, and somehow people litter less.", 1, 1, 60, 2, Want.Clean, 2, Want.Seating, 1),
            A("tv", "Screen Wall", "Entertainment", "News, sport and the weather at your destination.", 2, 1, 300, 2, Want.Fun, 5),
            A("arcade", "Arcade Corner", "Entertainment", "Twenty minutes gone.", 2, 2, 700, 3, Want.Fun, 9, Want.Shopping, 2, earns: true),
            A("play", "Play Area", "Entertainment", "Families will thank you.", 3, 3, 900, 3, Want.Fun, 14),
            A("gallery", "Aviation Gallery", "Entertainment", "Model aircraft and the airport's story.", 3, 2, 2400, 4, Want.Fun, 20, Want.Info, 4),
            A("screens", "Flight Screens", "Information", "Departures at a glance.", 1, 1, 350, 2, Want.Info, 5),
            A("infodesk", "Information Desk", "Information", "A person who knows where gate 4 is.", 2, 1, 900, 3, Want.Info, 10),
            // Decor: nobody needs it, everybody remembers it.
            A("sculpture", "Flight Sculpture", "Decor", "A polished jet climbing off a stone plinth. People meet under it.", 1, 1, 450, 2, Want.Fun, 3, Want.Info, 2),
            A("clock", "Meeting Point Clock", "Decor", "Four faces, one time. \"See you under the clock.\"", 1, 1, 520, 2, Want.Info, 5),
            A("flags", "Flag Row", "Decor", "The flags of everywhere you fly.", 1, 2, 380, 2, Want.Info, 3, Want.Fun, 2),
            A("piano", "Concourse Piano", "Decor", "Anyone may play. Some of them can.", 1, 1, 700, 3, Want.Fun, 5),
            A("palms", "Palm Court", "Decor", "Tall palms in a bed of ferns. The air feels cleaner.", 2, 2, 1100, 3, Want.Clean, 6, Want.Seating, 2),
            A("pool", "Fountain Court", "Decor", "Water, light and somewhere to sit on the rim.", 2, 2, 1600, 3, Want.Fun, 6, Want.Seating, 3),
            A("aquarium", "Aquarium Wall", "Decor", "A reef behind glass. Children press their noses to it.", 1, 2, 2200, 4, Want.Fun, 10),
        };

        static AmenityDef A(string id, string name, string group, string blurb, int w, int d, double cost, int stars, Want gives, int points, Want also = Want.Food, int alsoPoints = 0, bool earns = false)
        {
            return new AmenityDef { Id = id, Name = name, Group = group, Blurb = blurb, W = w, D = d, Cost = cost, Stars = stars, Gives = gives, Points = points, Also = also, AlsoPoints = alsoPoints, Earns = earns };
        }

        public static AmenityDef AmenityById(string id) { return Amenities.Find(a => a.Id == id); }

        // The hall is a compact rectangle seen from the landside: doors, check-in and security across the front, the
        // concourse floor behind them, the gate lounges along the glass at the back. It grows both ways: wider with
        // check-in, security and the gates (each needs room across the hall), deeper with the airport's rating.
        public const float TerminalFront = 25f, TerminalBack = 16f, MinTerminalWidth = 60f, MaxTerminalWidth = 120f;
        public const int MinFloorCols = 8, MaxFloorCols = 16;

        /// <summary>Check-in desks, security lanes and gates as the hall shows them (their levels, capped).</summary>
        public int CheckinDesks { get { return Math.Max(2, Math.Min(40, Level("checkin") * 2)); } }
        public int SecurityLanes { get { return Math.Max(1, Math.Min(24, Level("security"))); } }
        public int GateLounges { get { return Math.Max(1, Math.Min(24, Level("gates"))); } }

        /// <summary>The hall's width in metres (across, as you look in from the doors): what its desks, lanes and gates need, in whole floor cells.</summary>
        public float TerminalLength
        {
            get
            {
                int islands = (CheckinDesks + 5) / 6;
                float want = MinTerminalWidth + (Level("checkin") + Level("security") + Level("gates")) * 0.9f;
                want = Math.Max(want, Math.Max(islands * 13.4f + 4, Math.Max(SecurityLanes * 4.6f, GateLounges * 4.4f)));
                want = Math.Max(MinTerminalWidth, Math.Min(MaxTerminalWidth, want));
                return (float)Math.Ceiling(want / Cell) * Cell;
            }
        }
        /// <summary>Floor cells from security back toward the gates: one more with every star.</summary>
        public int FloorCols { get { return Math.Max(MinFloorCols, Math.Min(MaxFloorCols, MinFloorCols - 1 + Stars)); } }
        /// <summary>Floor cells across the hall.</summary>
        public int FloorRows { get { return (int)((TerminalLength - 4) / Cell); } }
        /// <summary>The hall's depth in metres, doors to glass.</summary>
        public float TerminalDepth { get { return TerminalFront + FloorCols * Cell + TerminalBack; } }

        public int Placed(string id) { return State.terminal.Count(p => p.id == id); }

        /// <summary>What the next one of these costs: each one you already have makes the next dearer.</summary>
        public double AmenityCost(string id)
        {
            var d = AmenityById(id);
            return Math.Floor(d.Cost * Math.Pow(1.45, Placed(id)) * Map.CostScale * (1 - Math.Min(0.6, Fx(Effect.BuildDiscount))));
        }

        public double AmenityUpgradeCost(PlacedAmenity p) { return Math.Floor(AmenityById(p.id).Cost * Math.Pow(4, p.level) * Map.CostScale); }

        public static void Footprint(AmenityDef d, int rot, out int w, out int depth) { bool turned = (rot & 1) == 1; w = turned ? d.D : d.W; depth = turned ? d.W : d.D; }

        /// <summary>Whether this footprint lies on the floor and on nothing else (ignoring one item: the one being moved).</summary>
        public bool FloorFree(AmenityDef d, int x, int z, int rot, PlacedAmenity ignore = null)
        {
            int w, depth; Footprint(d, rot, out w, out depth);
            if (x < 0 || z < 0 || x + w > FloorCols || z + depth > FloorRows) return false;
            foreach (var p in State.terminal)
            {
                if (p == ignore) continue;
                int pw, pd; Footprint(AmenityById(p.id), p.rot, out pw, out pd);
                if (x < p.x + pw && p.x < x + w && z < p.z + pd && p.z < z + depth) return false;
            }
            return true;
        }

        public string PlaceBlocked(string id, int x, int z, int rot)
        {
            var d = AmenityById(id);
            if (d == null) return "Unknown";
            if (Stars < d.Stars) return "Needs a " + d.Stars + "-star airport";
            if (!FloorFree(d, x, z, rot)) return "No room there";
            if (State.money < AmenityCost(id)) return "Need " + Format.Money(AmenityCost(id) - State.money) + " more";
            return null;
        }

        public bool Place(string id, int x, int z, int rot)
        {
            if (PlaceBlocked(id, x, z, rot) != null) return false;
            State.money -= AmenityCost(id);
            State.terminal.Add(new PlacedAmenity { id = id, x = x, z = z, rot = rot & 3 });
            AddRating(6);
            Count("upgrade", 1);
            Changed?.Invoke();
            return true;
        }

        /// <summary>The first free spot for this, working out from the middle of the hall (null: the floor is full).</summary>
        public PlacedAmenity FindSpot(string id, PlacedAmenity ignore = null)
        {
            var d = AmenityById(id);
            if (d == null) return null;
            int rows = FloorRows, cols = FloorCols, mid = rows / 2;
            for (int k = 0; k < rows; k++)
            {
                int z = mid + ((k & 1) == 0 ? k / 2 : -(k / 2) - 1);
                for (int rot = 0; rot < 2; rot++)
                    for (int x = 0; x < cols; x++)
                        if (FloorFree(d, x, z, rot, ignore)) return new PlacedAmenity { id = id, x = x, z = z, rot = rot };
            }
            return null;
        }

        /// <summary>
        /// Puts back on the floor anything that no longer lies on it (a save from before the hall changed shape): each
        /// such item moves to the nearest free spot; with no room left it is sold back for all it cost.
        /// </summary>
        public int RefitTerminal()
        {
            int moved = 0;
            foreach (var p in State.terminal.ToList())
            {
                var d = AmenityById(p.id);
                if (d == null) { State.terminal.Remove(p); continue; }
                if (FloorFree(d, p.x, p.z, p.rot, p)) continue;
                var spot = FindSpot(p.id, p);
                if (spot != null) { p.x = spot.x; p.z = spot.z; p.rot = spot.rot; }
                else { State.money += SellValue(p) * 2; State.terminal.Remove(p); }
                moved++;
            }
            return moved;
        }

        /// <summary>Places one wherever it fits (the play bot, and "place for me").</summary>
        public bool PlaceAnywhere(string id)
        {
            var spot = FindSpot(id);
            return spot != null && Place(id, spot.x, spot.z, spot.rot);
        }

        public bool MoveAmenity(PlacedAmenity p, int x, int z, int rot)
        {
            if (!State.terminal.Contains(p) || !FloorFree(AmenityById(p.id), x, z, rot, p)) return false;
            p.x = x; p.z = z; p.rot = rot & 3;
            Changed?.Invoke();
            return true;
        }

        public string AmenityUpgradeBlocked(PlacedAmenity p)
        {
            if (p.level >= MaxAmenityLevel) return "Fully upgraded";
            if (State.money < AmenityUpgradeCost(p)) return "Need " + Format.Money(AmenityUpgradeCost(p) - State.money) + " more";
            return null;
        }

        public bool UpgradeAmenity(PlacedAmenity p)
        {
            if (!State.terminal.Contains(p) || AmenityUpgradeBlocked(p) != null) return false;
            State.money -= AmenityUpgradeCost(p);
            p.level++;
            AddRating(6);
            Count("upgrade", 1);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sells one back for half of what the last one cost.</summary>
        public double SellValue(PlacedAmenity p) { return Math.Floor(AmenityById(p.id).Cost * Math.Pow(1.45, Math.Max(0, Placed(p.id) - 1)) * Map.CostScale * 0.5); }

        public bool SellAmenity(PlacedAmenity p)
        {
            if (!State.terminal.Contains(p)) return false;
            double back = SellValue(p);
            State.terminal.Remove(p);
            State.money += back;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Everything the floor gives toward one want (a level-2 item gives twice, level 3 three and a half times).</summary>
        public float Supply(Want w)
        {
            float v = 0;
            foreach (var p in State.terminal)
            {
                var d = AmenityById(p.id);
                float scale = p.level == 1 ? 1 : p.level == 2 ? 2 : 3.5f;
                if (d.Gives == w) v += d.Points * scale;
                if (d.AlsoPoints > 0 && d.Also == w) v += d.AlsoPoints * scale;
            }
            return v;
        }

        /// <summary>The shops' pull on every passenger's wallet (what "shop level" used to be).</summary>
        public float RetailLevel { get { return (Supply(Want.Food) + Supply(Want.Shopping)) / 10f; } }
        public float LoungeLevel { get { return Supply(Want.Lounge) / 10f; } }
        /// <summary>How much the shops actually pull from a wallet: the first few count in full, more of the same less and less.</summary>
        public float RetailPull { get { float l = RetailLevel; return l <= 6 ? l : 6 + (l - 6) * 0.25f; } }

        static readonly (Want want, string name, string advice, float free, float per, float weight)[] WantRules =
        {
            (Want.Food, "Food and drink", "Place a coffee bar or a kitchen", 2, 0.8f, 0.05f),
            (Want.Shopping, "Shopping", "Place a newsstand or a shop", 2, 0.8f, 0.04f),
            (Want.Seating, "Seating", "Place benches or a rest zone", 4, 0.9f, 0.06f),
            (Want.Toilets, "Toilets", "Place toilets and water stations", 4, 0.9f, 0.05f),
            (Want.Fun, "Entertainment", "Place a screen wall, an arcade or a play area", 2, 0.9f, 0.04f),
            (Want.Clean, "Cleanliness", "Place bins and a cleaning station", 4, 0.9f, 0.04f),
            (Want.Info, "Finding the way", "Place flight screens and an information desk", 6, 0.9f, 0.04f),
        };

        /// <summary>The passengers' report: queues first, then each thing they look for in the terminal.</summary>
        public List<Need> Needs
        {
            get
            {
                double demand = Math.Max(1, PassengerDemand);
                float Fit(double supply) { return (float)Math.Max(0, Math.Min(1, supply / demand)); }
                var list = new List<Need> { new Need { Name = "Short queues", Queue = true, Value = Fit(TerminalFlow), Advice = "Upgrade the slowest of check-in, security and gates" } };
                foreach (var r in WantRules) list.Add(new Need { Name = r.name, Want = r.want, Value = Fit(r.free + Supply(r.want) * r.per), Advice = r.advice });
                return list;
            }
        }

        /// <summary>How pleased passengers are, 0.4..1.5: queues count most, then everything the concourse offers.</summary>
        public float Satisfaction
        {
            get
            {
                var n = Needs;
                float v = 0.4f + 0.3f * n[0].Value;
                for (int i = 0; i < WantRules.Length; i++) v += WantRules[i].weight * n[i + 1].Value;
                v += 0.02f * RetailPull + 0.01f * LoungeLevel;
                v += MoodShift;
                return Math.Max(0.4f, Math.Min(1.5f, v));
            }
        }

        /// <summary>The need that is worst met right now (for the guide and the bot).</summary>
        public Need WorstNeed { get { return Needs.Where(x => !x.Queue).OrderBy(x => x.Value).First(); } }
    }
}
