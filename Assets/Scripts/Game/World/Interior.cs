using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Inside the terminal, as a cutaway: the departures hall with its check-in desks, the security lanes, the
    /// concourse of shops and cafes, seating, information desks and the gate lounges, all sized by their levels,
    /// and a crowd walking it: in at the doors, through check-in and security, round the shops, to a gate.
    /// It stands far from the airfield (at <see cref="Origin"/>); the camera moves there for the INSIDE view.
    /// </summary>
    public sealed class Interior : MonoBehaviour
    {
        public static Interior Instance { get; private set; }
        public static readonly Vector3 Origin = new Vector3(3000, 0, 0);
        /// <summary>The hall's depth (x: the doors at -, the glass to the apron at +) and its width across (z).</summary>
        public float Width { get; private set; } = 66f;
        public float Length { get; private set; }

        GameObject _static;
        string _built = "";
        Sim _sim;

        // Where the crowd goes.
        readonly List<Vector3> _doors = new List<Vector3>(), _desks = new List<Vector3>(), _lanes = new List<Vector3>(), _shops = new List<Vector3>(), _seats = new List<Vector3>(), _gates = new List<Vector3>();

        public sealed class Agent { public int Seed; public int Kind; public Vector3 Pos, Target; public int Step; public float Wait, Speed, Phase; public bool Arriving; public Vector3[] Path; public bool[] Stop; }
        readonly List<Agent> _agents = new List<Agent>();
        static Mesh[][] _walk; static Mesh[] _idle;
        readonly List<Matrix4x4>[] _batches = new List<Matrix4x4>[12 * 5];
        float _spawn;

        public static Interior Create(Transform parent)
        {
            var go = new GameObject("Interior");
            go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var v = go.AddComponent<Interior>();
            Instance = v;
            v._sim = GameRoot.Sim;
            if (_walk == null)
            {
                _walk = new Mesh[12][]; _idle = new Mesh[12];
                for (int i = 0; i < 12; i++)
                {
                    _walk[i] = new Mesh[4];
                    for (int f = 0; f < 4; f++) _walk[i][f] = Resources.Load<Mesh>("Models/person_" + i + "_w" + f);
                    _idle[i] = Resources.Load<Mesh>("Models/person_" + i + "_idle");
                }
            }
            for (int i = 0; i < v._batches.Length; i++) v._batches[i] = new List<Matrix4x4>();
            v.Rebuild();
            return v;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Rebuild()
        {
            if (_static != null) Destroy(_static);
            _doors.Clear(); _desks.Clear(); _lanes.Clear(); _shops.Clear(); _seats.Clear(); _gates.Clear();
            float was = Length, wasDeep = Width;
            var s = _sim;
            int checkin = s.Level("checkin"), security = s.Level("security"), gates = s.Level("gates"), shops = s.Level("shops"), seating = s.Level("seating"), info = s.Level("info"), lounge = s.Level("lounge");
            Length = s.TerminalLength; Width = s.TerminalDepth;
            if (Length < was - 0.5f || Mathf.Abs(Width - wasDeep) > 0.5f) _agents.Clear();      // a smaller hall (a new airport): the old crowd would walk through walls. Otherwise people finish their way.
            float L = Length, W = Width, x0 = -W * 0.5f, z0 = -L * 0.5f;
            var mb = new MeshBuilder { Smoothness = 0.25f };
            mb.Box(new Vector3(0, -0.6f, 0), new Vector3(1600, 0.5f, 1600), Palette.Hex(0x1E242C));     // the table the cutaway stands on
            var g = new Ground();
            // Floors: pale stone in the hall, a warmer tone in the concourse, carpet blue at the gates.
            g.Rect("Concrete", 0, 0, W, L, 0.0f, 0, new Color(1.25f, 1.27f, 1.3f));
            float sx = x0 + 21.5f, gateEdge = W * 0.5f - Sim.TerminalBack;      // the security wall, and where the gate lounges begin
            g.Rect("Concrete", gateEdge + Sim.TerminalBack * 0.5f, 0, Sim.TerminalBack, L, 0.01f, 0, new Color(0.62f, 0.72f, 0.86f));      // a grey-blue carpet, not a swimming pool
            g.Rect("Concrete", (sx + gateEdge) * 0.5f, 0, gateEdge - sx, L, 0.012f, 0, new Color(1.35f, 1.2f, 0.98f));
            // Walls: full height along the glass at the back, lower at the two ends, cut low at the doors (toward the camera) so the floor shows.
            Color wall = Palette.Hex(0xE7E3DA), trim = Palette.Hex(0x1F5FA8), deskTint = new Color(0.82f, 0.92f, 1.12f);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0, 1.5f, e * (L * 0.5f + 0.3f)), new Vector3(W + 1.2f, 3.0f, 0.6f), wall, Palette.White);
            mb.Box(new Vector3(W * 0.5f + 0.3f, 2.6f, 0), new Vector3(0.6f, 5.2f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(-W * 0.5f - 0.3f, 0.5f, 0), new Vector3(0.6f, 1.0f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(W * 0.5f - 0.06f, 4.9f, 0), new Vector3(0.1f, 0.5f, L), trim);
            // Glass to the apron along the airside wall.
            mb.Smoothness = 0.92f;
            for (float z = z0 + 3; z < L * 0.5f - 2; z += 6) mb.Box(new Vector3(W * 0.5f - 0.05f, 2.6f, z), new Vector3(0.12f, 3.6f, 4.6f), Palette.Hex(0x6FB0CC));
            mb.Smoothness = 0.25f;

            // ---- landside doors along the west wall
            int doorCount = Mathf.Clamp(2 + checkin / 3, 2, 8);
            for (int i = 0; i < doorCount; i++)
            {
                float z = z0 + L * (i + 0.5f) / doorCount;
                _doors.Add(new Vector3(x0 + 1.2f, 0, z));
                mb.Box(new Vector3(x0 - 0.3f, 1.4f, z), new Vector3(0.7f, 2.8f, 3.2f), Palette.Hex(0x4A8FB5));
                mb.Box(new Vector3(x0 + 1.4f, 0.03f, z), new Vector3(2.2f, 0.02f, 3.4f), Palette.Hex(0x7A2E2E));      // door mat
            }

            // ---- check-in: islands of desks with a baggage belt behind and queue barriers in front
            int desks = s.CheckinDesks, perIsland = 6, islands = (desks + perIsland - 1) / perIsland;
            float cx = x0 + 11.5f;
            for (int i = 0; i < islands; i++)
            {
                float iz = z0 + L * (i + 0.5f) / islands;
                int n = Mathf.Min(perIsland, desks - i * perIsland);
                mb.Box(new Vector3(cx + 1.6f, 0.45f, iz), new Vector3(1.0f, 0.5f, n * 1.9f + 1), Palette.Hex(0x2B3138));                            // belt
                mb.Box(new Vector3(cx + 2.6f, 2.9f, iz), new Vector3(0.25f, 1.1f, n * 1.9f), Palette.Hex(0x12161B));                                // flight board
                mb.Box(new Vector3(cx + 2.45f, 2.9f, iz), new Vector3(0.05f, 0.8f, n * 1.9f - 0.4f), Palette.Hex(0xF2C230));
                foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(cx + 2.6f, 1.2f, iz + e * n * 0.95f), new Vector3(0.18f, 2.4f, 0.18f), Palette.Steel);
                for (int k = 0; k < n; k++)
                {
                    float dz = iz + (k - (n - 1) / 2f) * 1.9f;
                    mb.Fit("desk_white", new Vector3(cx, 0, dz), 0, 1.5f, 1.08f, 0.9f, deskTint);
                    mb.Long("screen", new Vector3(cx + 0.12f, 1.08f, dz + 0.32f), 180, 0.46f);
                    mb.Tall("chair_desk", new Vector3(cx + 0.8f, 0, dz + 0.3f), 0, 0.95f);
                    mb.Box(new Vector3(cx + 0.9f, 0.3f, dz - 0.5f), new Vector3(0.8f, 0.3f, 0.5f), Palette.Hex(0x9AA0A6));                           // scale
                    if ((k + i) % 2 == 0) mb.Tall("luggage", new Vector3(cx + 0.9f, 0.45f, dz - 0.5f), 90 + k * 25, 0.55f, (k + i) % 4 == 0 ? Color.white : new Color(0.6f, 0.9f, 1.3f));
                    _desks.Add(new Vector3(cx - 1.3f, 0, dz));
                }
                // Queue barriers: posts and belts in a zig-zag before the island.
                for (int r = 0; r < 3; r++)
                    for (float bz = iz - n * 0.9f; bz <= iz + n * 0.9f; bz += 2.2f)
                    {
                        mb.Cylinder(new Vector3(cx - 2.6f - r * 1.4f, 0, bz), new Vector3(cx - 2.6f - r * 1.4f, 0.95f, bz), 0.06f, 5, Palette.Steel);
                        mb.Box(new Vector3(cx - 2.6f - r * 1.4f, 0.9f, bz + 1.1f), new Vector3(0.04f, 0.08f, 2.2f), Palette.Hex(0xB0282A));
                    }
            }

            // ---- security: lanes with a belt, a scanner and an arch, across the middle
            int lanes = s.SecurityLanes;
            mb.Box(new Vector3(sx, 1.3f, 0), new Vector3(0.3f, 2.6f, L), Palette.Hex(0xD5D8DC), Palette.White);                                    // the wall security pierces
            for (int i = 0; i < lanes; i++)
            {
                float lz = z0 + L * (i + 0.5f) / lanes;
                mb.Box(new Vector3(sx, 1.3f, lz), new Vector3(0.5f, 2.7f, Mathf.Min(4.2f, L / lanes - 0.6f)), Palette.Hex(0x2B3138));                  // the opening (dark)
                mb.Box(new Vector3(sx, 0.5f, lz - 0.8f), new Vector3(5.2f, 0.9f, 0.8f), Palette.Hex(0x4A5058), Palette.Hex(0x1B2028));              // belt
                mb.Box(new Vector3(sx, 1.25f, lz - 0.8f), new Vector3(1.6f, 1.0f, 1.1f), Palette.Hex(0xDADDE0), Palette.Hex(0x9AA0A6));             // scanner
                foreach (float e in new[] { -0.5f, 0.5f }) mb.Box(new Vector3(sx + 0.3f, 1.1f, lz + 0.8f + e), new Vector3(0.5f, 2.2f, 0.12f), Palette.Hex(0xDADDE0));
                mb.Box(new Vector3(sx + 0.3f, 2.25f, lz + 0.8f), new Vector3(0.5f, 0.14f, 1.12f), Palette.Hex(0x3FB26A));                            // the arch, green
                // Bags riding the belt, in and out of the scanner.
                mb.Tall("luggage", new Vector3(sx - 1.7f, 0.95f, lz - 0.8f), 90 + i * 30, 0.5f, i % 2 == 0 ? Color.white : new Color(0.6f, 0.9f, 1.3f));
                mb.Tall("luggage", new Vector3(sx + 1.6f, 0.95f, lz - 0.8f), 60 + i * 50, 0.46f, i % 3 == 0 ? new Color(1.3f, 0.7f, 0.6f) : Color.white);
                _lanes.Add(new Vector3(sx, 0, lz + 0.8f));
            }

            // ---- the concourse: whatever the player has placed on its floor
            DrawFloor(mb, g, x0, z0);

            // ---- gate lounges along the glass: rows of seats, a desk and a gate number each
            int gateCount = s.GateLounges;
            float gx = W * 0.5f - 6f;
            for (int i = 0; i < gateCount; i++)
            {
                float gz = z0 + L * (i + 0.5f) / gateCount, span = Mathf.Min(7f, L / gateCount - 1.2f);
                mb.Fit("desk_white", new Vector3(W * 0.5f - 1.6f, 0, gz + span * 0.4f), 0, 1.6f, 1.08f, 0.9f, deskTint);                        // gate desk
                mb.Long("screen", new Vector3(W * 0.5f - 1.5f, 1.08f, gz + span * 0.4f + 0.3f), 180, 0.46f);
                mb.Box(new Vector3(W * 0.5f - 0.5f, 3.3f, gz), new Vector3(0.2f, 0.8f, 1.6f), Palette.Hex(0x12161B));
                mb.Box(new Vector3(W * 0.5f - 0.62f, 3.3f, gz), new Vector3(0.05f, 0.5f, 1.0f), Palette.Hex(0xF2C230));
                int rows = 2;
                for (int r = 0; r < rows; r++)
                {
                    float rx = gx - 4.5f + r * 1.7f, z0s = gz - span * 0.4f, z1s = gz + span * 0.25f;
                    mb.Mat(0.7f, 0.6f); mb.Box(new Vector3(rx, 0.2f, (z0s + z1s) * 0.5f), new Vector3(0.08f, 0.06f, z1s - z0s + 0.3f), Palette.Steel); mb.Mat(0.25f);
                    for (float sz = z0s; sz <= z1s; sz += 0.52f)
                    {
                        var sp = new Vector3(rx, 0, sz);
                        mb.Tall("chair", sp, 0, 0.88f);
                        if ((int)(sz * 4) % 3 == 0) _seats.Add(sp + new Vector3(-0.2f, 0, 0));
                    }
                }
                _gates.Add(new Vector3(W * 0.5f - 2.4f, 0, gz));
            }
            _static = mb.ToObject("Static", transform);
            g.Build(_static.transform);
            BuildGrid();
            foreach (var l in _labels) Lettering(l.text, l.at, l.yaw, l.size, l.color);
            _labels.Clear();
            foreach (var it in _items)
            {
                var go = new GameObject(it.mesh.name);
                go.transform.SetParent(_static.transform, false);
                go.transform.localPosition = it.at;
                go.transform.localRotation = Quaternion.Euler(0, it.yaw, 0);
                go.AddComponent<MeshFilter>().sharedMesh = it.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Materials.World;
            }
            _items.Clear();
        }

        void Update()
        {
            var s = _sim;
            string key = s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.FloorCols + "|" + FloorKey(s);
            if (key != _built) { _built = key; Rebuild(); }
            if (!CameraRig.Inside || _walk == null || _walk[0][0] == null) return;

            // The crowd grows with the passengers the airport moves, up to what a phone draws comfortably.
            int want = Mathf.Clamp((int)(s.PassengerDemand * 28) + 50, 50, 340);
            float dt = Time.deltaTime;
            if ((_spawn -= dt) <= 0 && _agents.Count < want) { _spawn = 0.02f; Spawn(_agents.Count % 3 == 0, want); }

            foreach (var b in _batches) b.Clear();
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var a = _agents[i];
                bool walking = false;
                if (a.Wait > 0) a.Wait -= dt;
                else
                {
                    var to = a.Path[a.Step] - a.Pos;
                    float d = to.magnitude;
                    if (d < 0.25f)
                    {
                        a.Step++;
                        a.Wait = a.Step < a.Path.Length && a.Stop[a.Step - 1] ? Random.Range(0.5f, 7f) : 0;      // they pause where they have business, not at every corner
                        if (a.Step >= a.Path.Length) { _agents.RemoveAt(i); continue; }
                    }
                    else { a.Pos += to / d * Mathf.Min(d, a.Speed * dt); a.Target = to / d; walking = true; }
                }
                var rot = a.Target.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-a.Target) : Quaternion.identity;
                int frame = walking ? (int)(a.Phase + Time.time * 7) & 3 : 4;
                _batches[a.Kind * 5 + frame].Add(Matrix4x4.TRS(Origin + a.Pos, rot, Vector3.one * 1.75f));
            }
            for (int k = 0; k < 12; k++)
                for (int f = 0; f < 5; f++)
                {
                    var list = _batches[k * 5 + f];
                    var mesh = f < 4 ? _walk[k][f] : _idle[k];
                    if (list.Count > 0 && mesh != null) Graphics.DrawMeshInstanced(mesh, 0, Materials.Kit, list);
                }
        }

        void Spawn(bool arriving, int want = 100)
        {
            if (_doors.Count == 0 || _gates.Count == 0 || _lanes.Count == 0 || _desks.Count == 0) return;
            Vector3 R(List<Vector3> l) { return l[Random.Range(0, l.Count)] + new Vector3(Random.Range(-0.4f, 0.4f), 0, Random.Range(-0.4f, 0.4f)); }
            // Where they have business, in order; between those they keep to the aisles (see Walk).
            var goals = new List<Vector3>();
            if (!arriving)
            {
                goals.Add(R(_doors)); goals.Add(R(_desks));
                var lane = R(_lanes); goals.Add(lane + Vector3.left * 3.5f); goals.Add(lane + Vector3.right * 3f);
                if (_shops.Count > 0 && Random.value < 0.7f) goals.Add(R(_shops));
                if (_seats.Count > 0 && Random.value < 0.6f) goals.Add(R(_seats));
                goals.Add(R(_gates));
            }
            else
            {
                goals.Add(R(_gates));
                if (_shops.Count > 0 && Random.value < 0.3f) goals.Add(R(_shops));
                var lane = R(_lanes); goals.Add(lane + Vector3.right * 3f + Vector3.forward * 1.6f); goals.Add(lane + Vector3.left * 3.5f + Vector3.forward * 1.6f);
                goals.Add(R(_doors));
            }
            var path = new List<Vector3> { goals[0] };
            var stop = new List<bool> { true };
            for (int i = 1; i < goals.Count; i++)
            {
                Walk(goals[i - 1], goals[i], path);
                while (stop.Count < path.Count - 1) stop.Add(false);
                stop.Add(true);
            }
            var a = new Agent { Seed = Random.Range(1, 1 << 30), Arriving = arriving, Kind = Random.Range(0, 12), Path = path.ToArray(), Stop = stop.ToArray(), Speed = Random.Range(1.6f, 2.6f), Phase = Random.Range(0, 4) };
            // Start part-way along, so the hall is full the moment you look in.
            int start = _agents.Count < want * 0.8f ? Random.Range(0, path.Count - 1) : 0;
            a.Pos = path[start]; a.Step = start + 1 < path.Count ? start + 1 : start; a.Target = Vector3.right;
            _agents.Add(a);
        }

        // ------------------------------------------------------------------ finding a way round the furniture

        // The floor as cells, with one more ring of cells round it (the aisles along security, the gates and the end
        // walls, always clear): true where something stands or a bay is fenced off.
        bool[,] _solid = new bool[0, 0];
        int[,] _from = new int[0, 0];       // the cell each was reached from, in the last search
        readonly Queue<int> _open = new Queue<int>();

        void BuildGrid()
        {
            int cols = _sim.FloorCols + 2, rows = _sim.FloorRows + 2;
            _solid = new bool[cols, rows]; _from = new int[cols, rows];
            for (int z = 0; z < _sim.FloorRows; z++)
                if (!_sim.BayOpen(Sim.BayOf(z))) for (int x = 0; x < _sim.FloorCols; x++) _solid[x + 1, z + 1] = true;
            foreach (var p in _sim.State.terminal)
            {
                var d = Sim.AmenityById(p.id);
                // Planters, bins and the like are walked round without a detour: only what fills its cells counts.
                if (d == null || d.W * d.D < 2) continue;
                int w, dp; Sim.Footprint(d, p.rot, out w, out dp);
                for (int x = p.x; x < p.x + w; x++) for (int z = p.z; z < p.z + dp; z++) if (x >= 0 && z >= 0 && x < _sim.FloorCols && z < _sim.FloorRows) _solid[x + 1, z + 1] = true;
            }
        }

        bool Cell(Vector3 at, out int x, out int z)
        {
            var c = FloorCorner;
            x = Mathf.FloorToInt((at.x - c.x) / Sim.Cell) + 1; z = Mathf.FloorToInt((at.z - c.z) / Sim.Cell) + 1;
            return x >= 0 && z >= 0 && x < _solid.GetLength(0) && z < _solid.GetLength(1);
        }

        Vector3 CellCentre(int x, int z) { var c = FloorCorner; return new Vector3(c.x + (x - 0.5f) * Sim.Cell, 0, c.z + (z - 0.5f) * Sim.Cell); }

        /// <summary>Whether the straight line between two points crosses nothing solid.</summary>
        bool Clear(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.6f));
            for (int i = 0; i <= n; i++)
            {
                int x, z;
                if (Cell(Vector3.Lerp(a, b, i / (float)n), out x, out z) && _solid[x, z]) return false;
            }
            return true;
        }

        /// <summary>
        /// Adds the way from one point to another to a path: straight when nothing is between them, otherwise along
        /// the free cells of the floor (the shortest way, then straightened wherever the view is clear). Where they
        /// are going may itself be inside something (a seat in the lounge): the last step goes in.
        /// </summary>
        void Walk(Vector3 from, Vector3 to, List<Vector3> path)
        {
            int cols = _solid.GetLength(0), rows = _solid.GetLength(1);
            int ax, az, bx, bz;
            bool inA = Cell(from, out ax, out az), inB = Cell(to, out bx, out bz);
            if (cols == 0 || Clear(from, to)) { path.Add(to); return; }
            // Outside the floor (the check-in hall, the gate lounges): step onto the ring first, or off it last.
            ax = Mathf.Clamp(ax, 0, cols - 1); az = Mathf.Clamp(az, 0, rows - 1); bx = Mathf.Clamp(bx, 0, cols - 1); bz = Mathf.Clamp(bz, 0, rows - 1);
            for (int x = 0; x < cols; x++) for (int z = 0; z < rows; z++) _from[x, z] = -1;
            _open.Clear();
            int start = ax + az * cols, goal = bx + bz * cols, best = start;
            float bestD = float.MaxValue;
            _from[ax, az] = start; _open.Enqueue(start);      // they may start inside something (leaving the lounge)
            while (_open.Count > 0)
            {
                int cur = _open.Dequeue(), cx = cur % cols, cz = cur / cols;
                if (cur == goal) { best = goal; break; }
                // Somewhere walled in (a seat deep in a block of shops) is reached from the nearest free cell to it.
                float d = (CellCentre(cx, cz) - to).sqrMagnitude;
                if (d < bestD && cur != start) { bestD = d; best = cur; }
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), nz = cz + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || nz < 0 || nx >= cols || nz >= rows || _from[nx, nz] >= 0) continue;
                    if (_solid[nx, nz] && nx + nz * cols != goal) continue;
                    _from[nx, nz] = cur; _open.Enqueue(nx + nz * cols);
                }
            }
            if (best == start) { path.Add(to); return; }       // nowhere to go but straight there
            var cells = new List<Vector3>();
            for (int cur = best; cur != start; cur = _from[cur % cols, cur / cols]) cells.Add(CellCentre(cur % cols, cur / cols));
            cells.Reverse();
            // Straighten: from where they stand, walk to the furthest cell in clear view.
            var at = from; int i = 0;
            while (i < cells.Count)
            {
                int far = i;
                for (int j = cells.Count - 1; j > i; j--) if (Clear(at, cells[j])) { far = j; break; }
                // A step of jitter, so a crowd does not walk one line.
                var p = cells[far] + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
                bool last = far == cells.Count - 1;
                if (!last || best != goal || !inB || !Clear(at, to)) { path.Add(p); at = p; }
                i = far + 1;
            }
            path.Add(to);
        }

        // ------------------------------------------------------------------ the concourse floor

        /// <summary>The floor's corner (cell 0,0) in the hall's own space: just past security, two metres in from the end wall. Cells run x toward the gates, z across the hall.</summary>
        public Vector3 FloorCorner { get { return new Vector3(-Width * 0.5f + Sim.TerminalFront, 0, -Length * 0.5f + 2f); } }

        /// <summary>The middle of a footprint, in the world.</summary>
        public Vector3 FloorPoint(int x, int z, int w, int d) { return Origin + FloorCorner + new Vector3((x + w * 0.5f) * Sim.Cell, 0, (z + d * 0.5f) * Sim.Cell); }

        /// <summary>The floor cell under a point in the world.</summary>
        public void CellAt(Vector3 world, out int x, out int z)
        {
            var l = world - Origin - FloorCorner;
            x = Mathf.FloorToInt(l.x / Sim.Cell); z = Mathf.FloorToInt(l.z / Sim.Cell);
        }

        /// <summary>The placed item whose footprint covers a floor cell (null: bare floor).</summary>
        public PlacedAmenity ItemAt(int x, int z)
        {
            foreach (var p in _sim.State.terminal)
            {
                int w, d; Sim.Footprint(Sim.AmenityById(p.id), p.rot, out w, out d);
                if (x >= p.x && x < p.x + w && z >= p.z && z < p.z + d) return p;
            }
            return null;
        }

        /// <summary>The passenger nearest a point in the world, within reach of a fingertip.</summary>
        public Agent AgentNear(Vector3 world, float reach = 1.6f)
        {
            Agent best = null; float bd = reach * reach;
            var l = world - Origin;
            foreach (var a in _agents) { float d = (new Vector2(a.Pos.x - l.x, a.Pos.z - l.z)).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
            return best;
        }

        public Vector3 AgentWorld(Agent a) { return Origin + a.Pos; }
        public bool Alive(Agent a) { return _agents.Contains(a); }

        static string FloorKey(Sim s)
        {
            int h = s.State.terminal.Count + s.State.bays.Count * 1009 + s.State.baysBought * 7919 + s.BayCount * 104729;
            foreach (var p in s.State.terminal) h = h * 31 + p.id.GetHashCode() + p.x * 7 + p.z * 131 + p.rot * 3 + p.level * 17;
            return h.ToString();
        }

        readonly List<(string text, Vector3 at, float yaw, float size, Color color)> _labels = new List<(string, Vector3, float, float, Color)>();
        static Material _textMat;

        /// <summary>Lettering standing in the hall (a shop's name on its fascia), leaning back so it reads from above.</summary>
        void Lettering(string text, Vector3 at, float yaw, float size, Color color)
        {
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(_static.transform, false);
            go.transform.localPosition = at;
            // TextMesh reads along +x facing -z; the fascia faces the unit's -x: turn it, then lean it back toward the sky.
            go.transform.localRotation = Quaternion.Euler(0, yaw + 90, 0) * Quaternion.Euler(38, 0, 0);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Ui.Bold; tm.text = text; tm.fontSize = 72; tm.characterSize = size * 10f / 72f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
            if (_textMat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/Text3D");
                _textMat = sh != null ? new Material(sh) : new Material(Ui.Bold.material);
            }
            _textMat.mainTexture = Ui.Bold.material.mainTexture;
            go.GetComponent<MeshRenderer>().sharedMaterial = _textMat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Every placed item is its own object with a mesh made once for its kind, level and look: placing or moving one
        // thing does not rebuild the others, and what is off screen is not drawn.
        const int ItemVariants = 6;
        static readonly Dictionary<string, Mesh> _itemMeshes = new Dictionary<string, Mesh>();
        readonly List<(Mesh mesh, Vector3 at, float yaw)> _items = new List<(Mesh, Vector3, float)>();

        static Mesh ItemMesh(AmenityDef d, int level, int variant)
        {
            string key = d.Id + " " + level + " " + variant;
            Mesh m;
            if (_itemMeshes.TryGetValue(key, out m) && m != null) return m;
            var mb = new MeshBuilder { Smoothness = 0.25f };
            Furnish.Amenity(mb, d, d.W * Sim.Cell, d.D * Sim.Cell, level, variant);
            m = mb.ToMesh(key);
            m.UploadMeshData(true);      // drawn only: the copy kept for reading is not needed
            return _itemMeshes[key] = m;
        }

        void DrawFloor(MeshBuilder mb, Ground g, float x0, float z0)
        {
            var corner = FloorCorner;
            int rows = _sim.FloorRows;
            // The floor you can build on: a warm stone with fine joints at every cell.
            int cols = _sim.FloorCols;
            float fw = cols * Sim.Cell, fl = rows * Sim.Cell;
            g.Rect("Concrete", corner.x + fw * 0.5f, corner.z + fl * 0.5f, fw, fl, 0.014f, 0, new Color(1.32f, 1.22f, 1.04f));
            var joint = new Color(0.62f, 0.58f, 0.5f);
            for (int x = 0; x <= cols; x++) mb.Box(new Vector3(corner.x + x * Sim.Cell, 0.02f, corner.z + fl * 0.5f), new Vector3(0.05f, 0.012f, fl), joint);
            for (int z = 0; z <= rows; z += 1) mb.Box(new Vector3(corner.x + fw * 0.5f, 0.02f, corner.z + z * Sim.Cell), new Vector3(fw, 0.012f, 0.05f), joint);

            // Bays not opened yet: a duller floor behind site fencing, a few cones and boxes, and what it costs to open.
            int next = _sim.NextBay;
            for (int b = 0; b < _sim.BayCount; b++)
            {
                if (_sim.BayOpen(b)) continue;
                float len = _sim.BayRowsOf(b) * Sim.Cell, bz0 = corner.z + b * Sim.BayRows * Sim.Cell, bz1 = bz0 + len, bx0 = corner.x, bx1 = corner.x + fw;
                if (len <= 0) continue;
                g.Rect("Concrete", (bx0 + bx1) * 0.5f, (bz0 + bz1) * 0.5f, fw, len, 0.018f, 0, new Color(0.72f, 0.74f, 0.78f));
                for (float z = bz0 + 1; z < bz1; z += 2) foreach (float x in new[] { bx0 + 0.15f, bx1 - 0.15f }) mb.Long("c_fence", new Vector3(x, 0, z), 90, 2f);
                foreach (bool low in new[] { true, false })
                {
                    // The side toward an open bay (or the end wall) is fenced too; between two closed bays there is nothing.
                    if (!_sim.BayOpen(low ? b - 1 : b + 1) && (low ? b > 0 : b < _sim.BayCount - 1)) continue;
                    for (float x = bx0 + 1; x < bx1; x += 2) mb.Long("c_fence", new Vector3(x, 0, low ? bz0 + 0.15f : bz1 - 0.15f), 0, 2f);
                }
                var rng = new System.Random(b * 97 + 13);
                float R(float a, float c) { return a + (float)rng.NextDouble() * (c - a); }
                for (int k = 0; k < 3 + (int)(len / 4); k++) mb.Tall("c_cone", new Vector3(R(bx0 + 1, bx1 - 1), 0, R(bz0 + 1, bz1 - 1)), R(0, 360), 0.7f);
                for (int k = 0; k < 2; k++)
                {
                    var at = new Vector3(R(bx0 + 2, bx1 - 2), 0, R(bz0 + 1.5f, bz1 - 1.5f));
                    mb.Tall("box", at, R(0, 90), 0.8f); mb.Tall("box", at + new Vector3(0.75f, 0, 0.2f), R(0, 90), 0.7f); mb.Tall("box", at + new Vector3(0.3f, 0.8f, 0.1f), R(0, 90), 0.6f);
                    mb.Long("box_open", at + new Vector3(-0.9f, 0, -0.4f), R(0, 90), 0.9f);
                }
                mb.Long("c_barrier", new Vector3(bx0 + 1.2f, 0, (bz0 + bz1) * 0.5f), 90, 1.8f);
                mb.Glow = true; mb.Tall("c_light", new Vector3(bx0 + 0.6f, 0, bz0 + 0.8f), 0, 1.3f); mb.Tall("c_light", new Vector3(bx0 + 0.6f, 0, bz1 - 0.8f), 0, 1.3f); mb.Glow = false;
                bool can = b == next || _sim.BayOpen(b - 1) || _sim.BayOpen(b + 1);
                _labels.Add((can ? "TAP TO OPEN  " + Format.Money(_sim.BayCost) : "FENCED OFF", new Vector3(bx0 + 1.6f, 1.9f, (bz0 + bz1) * 0.5f), 0, Mathf.Min(0.8f, len / 14f), can ? new Color(1f, 0.85f, 0.3f) : new Color(1, 1, 1, 0.7f)));
            }

            foreach (var p in _sim.State.terminal)
            {
                var d = Sim.AmenityById(p.id);
                int w, dp; Sim.Footprint(d, p.rot, out w, out dp);
                var c = corner + new Vector3((p.x + w * 0.5f) * Sim.Cell, 0, (p.z + dp * 0.5f) * Sim.Cell);
                _items.Add((ItemMesh(d, p.level, (p.x * 7 + p.z * 13) % ItemVariants), c, p.rot * 90f));
                // Its name over the front, for the units that have a fascia.
                string sign = SignText(d.Id);
                if (sign != null)
                {
                    var rot = Quaternion.Euler(0, p.rot * 90, 0);
                    float sw = d.D * Sim.Cell;
                    _labels.Add((sign, c + rot * new Vector3(-d.W * Sim.Cell * 0.5f - 0.05f, 3.25f, 0), p.rot * 90, Mathf.Min(0.62f, sw * 0.95f / Mathf.Max(4, sign.Length) * 1.7f), Color.white));
                }
                // Where passengers stop: in front of it (toward check-in), or on it for seats.
                var stop = c + new Vector3(-w * Sim.Cell * 0.5f - 0.7f, 0, 0);
                if (d.Gives == Want.Seating || d.Gives == Want.Lounge) _seats.Add(c); else _shops.Add(stop);
            }
        }

        /// <summary>What is written over a unit's front (null: it has no fascia).</summary>
        static string SignText(string id)
        {
            switch (id)
            {
                case "cafe": return "COFFEE"; case "noodle": return "NOODLES"; case "restaurant": return "BRASSERIE"; case "newsstand": return "NEWS";
                case "gifts": return "GIFTS"; case "fashion": return "FASHION"; case "dutyfree": return "DUTY FREE"; case "lounge": return "LOUNGE";
            }
            return null;
        }

        /// <summary>
        /// A picture of a terminal item for the catalogue: its own furniture, set out at level 2 and photographed once from
        /// the passengers' side (see <see cref="Studio"/>).
        /// </summary>
        public static Sprite Picture(AmenityDef def)
        {
            return Studio.Shot("amenity_" + def.Id, () => { var mb = new MeshBuilder(); Furnish.Amenity(mb, def, def.W * Sim.Cell, def.D * Sim.Cell, 2, 3); return mb; }, 62, 24, 320);
        }
    }
}
