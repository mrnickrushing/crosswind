using System;
using System.Collections.Generic;

namespace Crosswind.Core
{
    /// <summary>
    /// Where things stand matters. Passengers are spread over the open floor and walk only so far for what they want:
    /// each placed item serves the cells within its reach (a square, counted from its footprint's edge: shops and food
    /// draw people from further than a bench does; every level adds a cell). A want's coverage is the share of the open
    /// floor within reach of something that gives it, and only covered floor gets the item's full worth: what is placed
    /// for a want counts at <see cref="CoverFloor"/> plus the rest in proportion to its coverage. A terminal with its
    /// toilets in one corner is rated lower than the same toilets spread along the concourse.
    /// The premium lounge is a destination (people go to it), so it has no reach.
    /// Saves from before this rule have a grace period: their layouts count as fully covered at first, fading to the
    /// real coverage over <see cref="ReachGraceHours"/> of real time, while the floor map shows where things are missing.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>What placed items are worth with none of the floor in reach (a share of their full worth).</summary>
        public const float CoverFloor = 0.5f;
        public const double ReachGraceHours = 48;

        /// <summary>How many cells beyond its footprint an item of this want serves, at level 1.</summary>
        public static int Reach(Want w)
        {
            switch (w)
            {
                case Want.Food: case Want.Shopping: return 7;
                case Want.Fun: case Want.Info: return 6;
                case Want.Toilets: return 5;
                default: return 4;          // seating, cleanliness
            }
        }

        /// <summary>Wants that coverage applies to (all but the lounge).</summary>
        public static bool HasReach(Want w) { return w != Want.Lounge; }

        /// <summary>The cells an item serves: its footprint grown by its reach (and a cell per level above the first), clipped to the floor.</summary>
        public void ReachRect(PlacedAmenity p, Want w, out int x0, out int z0, out int x1, out int z1)
        {
            var d = AmenityById(p.id);
            int fw, fd; Footprint(d, p.rot, out fw, out fd);
            int r = Reach(w) + Math.Max(0, p.level - 1);
            x0 = Math.Max(0, p.x - r); z0 = Math.Max(0, p.z - r);
            x1 = Math.Min(FloorCols - 1, p.x + fw - 1 + r); z1 = Math.Min(FloorRows - 1, p.z + fd - 1 + r);
        }

        // ------------------------------------------------------------------ the coverage maps, cached

        int _covKey = int.MinValue, _covCols, _covRows, _covOpen;
        bool[] _covOpenCell;
        readonly Dictionary<Want, bool[]> _covered = new Dictionary<Want, bool[]>();
        readonly Dictionary<Want, float> _coverage = new Dictionary<Want, float>();

        /// <summary>A cheap fingerprint of everything coverage depends on (the floor's size, its open bays, what stands where).</summary>
        int CoverageKey()
        {
            int h = FloorCols * 7919 + FloorRows * 104729 + State.bays.Count * 31337;
            foreach (int b in State.bays) h = h * 17 + b;
            foreach (var p in State.terminal) h = h * 31 + p.id.GetHashCode() + p.x * 7 + p.z * 131 + p.rot * 3 + p.level * 17;
            return h;
        }

        void EnsureCoverage()
        {
            int key = CoverageKey();
            if (key == _covKey) return;
            _covKey = key;
            int cols = _covCols = FloorCols, rows = _covRows = FloorRows;
            _covOpenCell = new bool[cols * rows];
            _covOpen = 0;
            for (int x = 0; x < cols; x++) for (int z = 0; z < rows; z++) if (CellOpen(x, z)) { _covOpenCell[x * rows + z] = true; _covOpen++; }
            _covered.Clear(); _coverage.Clear();
            foreach (Want w in Enum.GetValues(typeof(Want)))
            {
                if (!HasReach(w)) continue;
                var map = new bool[cols * rows];
                foreach (var p in State.terminal)
                {
                    var d = AmenityById(p.id);
                    if (d == null || (d.Gives != w && !(d.AlsoPoints > 0 && d.Also == w))) continue;
                    int x0, z0, x1, z1; ReachRect(p, w, out x0, out z0, out x1, out z1);
                    for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) map[x * rows + z] = true;
                }
                int n = 0;
                for (int i = 0; i < map.Length; i++) if (map[i] && _covOpenCell[i]) n++;
                _covered[w] = map;
                _coverage[w] = _covOpen > 0 ? n / (float)_covOpen : 1f;
            }
        }

        /// <summary>The share of the open floor within reach of something that gives this want (1 for the lounge, or nothing open).</summary>
        public float Coverage(Want w)
        {
            if (!HasReach(w)) return 1;
            EnsureCoverage();
            float c; return _coverage.TryGetValue(w, out c) ? c : 1;
        }

        /// <summary>Whether a floor cell is within reach of something giving this want (the floor map).</summary>
        public bool Covered(Want w, int x, int z)
        {
            if (!HasReach(w)) return true;
            EnsureCoverage();
            if (x < 0 || z < 0 || x >= _covCols || z >= _covRows) return false;
            bool[] map; return _covered.TryGetValue(w, out map) && map[x * _covRows + z];
        }

        /// <summary>
        /// How much of their full worth the items placed for a want count for: <see cref="CoverFloor"/> with none of the
        /// floor in reach, all of it with all of it; during an old save's grace period, nearer all of it.
        /// </summary>
        public float CoverFactor(Want w)
        {
            if (!HasReach(w)) return 1;
            float c = Coverage(w);
            float grace = ReachGrace;
            if (grace > 0) c = c + (1 - c) * grace;
            return CoverFloor + (1 - CoverFloor) * c;
        }

        /// <summary>What the floor gives toward a want once reach is counted: <see cref="Supply"/> times <see cref="CoverFactor"/>.</summary>
        public float ReachedSupply(Want w) { return Supply(w) * CoverFactor(w); }

        // ------------------------------------------------------------------ an old save's grace

        /// <summary>
        /// Called when a save is opened: one from before reach counted, with things already on its floor, gets the grace
        /// period (started at the first tick, when the real clock is known); a new airport, none.
        /// </summary>
        void EnsureReach()
        {
            if (State.reachSet) return;
            State.reachSet = true;
            State.reachGraceUntil = State.terminal.Count > 0 ? -1 : 0;
        }

        /// <summary>How much of the grace is left, 1 (all) .. 0 (none).</summary>
        public float ReachGrace
        {
            get
            {
                if (State.reachGraceUntil == 0) return 0;
                if (State.reachGraceUntil < 0) return 1;
                double left = State.reachGraceUntil - RealNow();
                return left <= 0 ? 0 : (float)Math.Min(1, left / (ReachGraceHours * 3600));
            }
        }

        void TickReach()
        {
            if (State.reachGraceUntil < 0) State.reachGraceUntil = RealNow() + ReachGraceHours * 3600;
            else if (State.reachGraceUntil > 0 && RealNow() >= State.reachGraceUntil) State.reachGraceUntil = 0;
        }

        // ------------------------------------------------------------------ placing where it reaches most

        /// <summary>
        /// The free spot where this item would bring the most uncovered floor within reach of its want (its second want
        /// counts a third as much); ties go to the spot <see cref="FindSpot"/> would pick first. Null: no room.
        /// </summary>
        public PlacedAmenity BestSpot(string id, PlacedAmenity ignore = null)
        {
            var d = AmenityById(id);
            if (d == null) return null;
            var first = FindSpot(id, ignore);
            if (first == null || !HasReach(d.Gives)) return first;
            int cols = FloorCols, rows = FloorRows;
            // Uncovered open floor as a summed-area table: the gain of any rectangle in four lookups.
            var main = Uncovered(d.Gives, ignore); var also = d.AlsoPoints > 0 && HasReach(d.Also) ? Uncovered(d.Also, ignore) : null;
            PlacedAmenity best = first;
            float bestGain = Gain(first, d, main, also);
            int mid = rows / 2;
            for (int k = 0; k < rows; k++)
            {
                int z = mid + ((k & 1) == 0 ? k / 2 : -(k / 2) - 1);
                for (int rot = 0; rot < 2; rot++)
                    for (int x = 0; x < cols; x++)
                    {
                        if (!FloorFree(d, x, z, rot, ignore)) continue;
                        var spot = new PlacedAmenity { id = id, x = x, z = z, rot = rot, level = ignore != null ? ignore.level : 1 };
                        float g = Gain(spot, d, main, also);
                        if (g > bestGain + 0.01f) { bestGain = g; best = spot; }
                    }
            }
            return best;
        }

        /// <summary>A summed-area table of the open cells no item (but the ignored one) reaches for this want.</summary>
        int[] Uncovered(Want w, PlacedAmenity ignore)
        {
            int cols = FloorCols, rows = FloorRows;
            var map = new bool[cols * rows];
            foreach (var p in State.terminal)
            {
                if (p == ignore) continue;
                var pd = AmenityById(p.id);
                if (pd == null || (pd.Gives != w && !(pd.AlsoPoints > 0 && pd.Also == w))) continue;
                int x0, z0, x1, z1; ReachRect(p, w, out x0, out z0, out x1, out z1);
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) map[x * rows + z] = true;
            }
            var sat = new int[(cols + 1) * (rows + 1)];
            for (int x = 0; x < cols; x++)
                for (int z = 0; z < rows; z++)
                {
                    int v = !map[x * rows + z] && CellOpen(x, z) ? 1 : 0;
                    sat[(x + 1) * (rows + 1) + z + 1] = v + sat[x * (rows + 1) + z + 1] + sat[(x + 1) * (rows + 1) + z] - sat[x * (rows + 1) + z];
                }
            return sat;
        }

        float Gain(PlacedAmenity spot, AmenityDef d, int[] main, int[] also)
        {
            int x0, z0, x1, z1; ReachRect(spot, d.Gives, out x0, out z0, out x1, out z1);
            float g = Area(main, x0, z0, x1, z1);
            if (also != null) { ReachRect(spot, d.Also, out x0, out z0, out x1, out z1); g += Area(also, x0, z0, x1, z1) / 3f; }
            return g;
        }

        int Area(int[] sat, int x0, int z0, int x1, int z1)
        {
            int r = FloorRows + 1;
            return sat[(x1 + 1) * r + z1 + 1] - sat[x0 * r + z1 + 1] - sat[(x1 + 1) * r + z0] + sat[x0 * r + z0];
        }

        /// <summary>How much more of the open floor a want would cover with this item at this spot (the ghost's hint), 0..1.</summary>
        public float CoverageGain(string id, int x, int z, int rot, PlacedAmenity moving = null)
        {
            var d = AmenityById(id);
            if (d == null || !HasReach(d.Gives)) return 0;
            EnsureCoverage();
            if (_covOpen == 0) return 0;
            var main = Uncovered(d.Gives, moving);
            int x0, z0, x1, z1; ReachRect(new PlacedAmenity { id = id, x = x, z = z, rot = rot, level = moving != null ? moving.level : 1 }, d.Gives, out x0, out z0, out x1, out z1);
            float after = (_covOpen - Area(main, 0, 0, FloorCols - 1, FloorRows - 1) + Area(main, x0, z0, x1, z1)) / (float)_covOpen;
            return Math.Max(0, after - Coverage(d.Gives));
        }
    }
}
