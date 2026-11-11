using System.Collections.Generic;
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
        public const float Width = 64f;     // x: landside (-) to airside (+)

        public float Length { get; private set; }

        GameObject _static;
        string _built = "";
        Sim _sim;

        // Where the crowd goes.
        readonly List<Vector3> _doors = new List<Vector3>(), _desks = new List<Vector3>(), _lanes = new List<Vector3>(), _shops = new List<Vector3>(), _seats = new List<Vector3>(), _gates = new List<Vector3>();

        public sealed class Agent { public int Seed; public int Kind; public Vector3 Pos, Target; public int Step; public float Wait, Speed, Phase; public bool Arriving; public Vector3[] Path; }
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
            float was = Length;
            var s = _sim;
            int checkin = s.Level("checkin"), security = s.Level("security"), gates = s.Level("gates"), shops = s.Level("shops"), seating = s.Level("seating"), info = s.Level("info"), lounge = s.Level("lounge");
            Length = s.TerminalLength;
            if (Length < was - 0.5f) _agents.Clear();      // a smaller hall (a new airport): the old crowd would walk through walls. Otherwise people finish their way.
            float L = Length, W = Width, x0 = -W * 0.5f, z0 = -L * 0.5f;
            var mb = new MeshBuilder { Smoothness = 0.25f };
            mb.Box(new Vector3(0, -0.6f, 0), new Vector3(1600, 0.5f, 1600), Palette.Hex(0x1E242C));     // the table the cutaway stands on
            var g = new Ground();
            // Floors: pale stone in the hall, a warmer tone in the concourse, carpet blue at the gates.
            g.Rect("Concrete", 0, 0, W, L, 0.0f, 0, new Color(1.25f, 1.27f, 1.3f));
            g.Rect("Concrete", x0 + W * 0.86f, 0, W * 0.26f, L, 0.01f, 0, new Color(0.6f, 0.78f, 1.15f));
            g.Rect("Concrete", x0 + W * 0.56f, 0, W * 0.3f, L, 0.012f, 0, new Color(1.35f, 1.2f, 0.98f));
            // Walls: full height at the back and the ends, cut low toward the camera so the floor shows.
            Color wall = Palette.Hex(0xE7E3DA), trim = Palette.Hex(0x1F5FA8);
            mb.Box(new Vector3(0, 2.6f, -L * 0.5f - 0.3f), new Vector3(W + 1.2f, 5.2f, 0.6f), wall, Palette.White);
            mb.Box(new Vector3(W * 0.5f + 0.3f, 2.6f, 0), new Vector3(0.6f, 5.2f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(0, 0.5f, L * 0.5f + 0.3f), new Vector3(W + 1.2f, 1.0f, 0.6f), wall, Palette.White);
            mb.Box(new Vector3(-W * 0.5f - 0.3f, 0.5f, 0), new Vector3(0.6f, 1.0f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(0, 4.9f, -L * 0.5f + 0.05f), new Vector3(W, 0.5f, 0.1f), trim);
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
            int desks = Mathf.Clamp(checkin * 2, 2, 40), perIsland = 6, islands = (desks + perIsland - 1) / perIsland;
            float cx = x0 + W * 0.2f;
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
                    mb.Box(new Vector3(cx, 0.55f, dz), new Vector3(0.9f, 1.1f, 1.5f), Palette.White, trim);
                    mb.Box(new Vector3(cx + 0.1f, 1.25f, dz + 0.3f), new Vector3(0.1f, 0.4f, 0.5f), Palette.Hex(0x1B2028));                          // screen
                    mb.Box(new Vector3(cx + 0.9f, 0.3f, dz - 0.5f), new Vector3(0.8f, 0.3f, 0.5f), Palette.Hex(0x9AA0A6));                           // scale
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
            int lanes = Mathf.Clamp(security, 1, 24);
            float sx = x0 + W * 0.4f;
            mb.Box(new Vector3(sx, 1.3f, 0), new Vector3(0.3f, 2.6f, L), Palette.Hex(0xD5D8DC), Palette.White);                                    // the wall security pierces
            for (int i = 0; i < lanes; i++)
            {
                float lz = z0 + L * (i + 0.5f) / lanes;
                mb.Box(new Vector3(sx, 1.3f, lz), new Vector3(0.5f, 2.7f, Mathf.Min(4.2f, L / lanes - 0.6f)), Palette.Hex(0x2B3138));                  // the opening (dark)
                mb.Box(new Vector3(sx, 0.5f, lz - 0.8f), new Vector3(5.2f, 0.9f, 0.8f), Palette.Hex(0x4A5058), Palette.Hex(0x1B2028));              // belt
                mb.Box(new Vector3(sx, 1.25f, lz - 0.8f), new Vector3(1.6f, 1.0f, 1.1f), Palette.Hex(0xDADDE0), Palette.Hex(0x9AA0A6));             // scanner
                foreach (float e in new[] { -0.5f, 0.5f }) mb.Box(new Vector3(sx + 0.3f, 1.1f, lz + 0.8f + e), new Vector3(0.5f, 2.2f, 0.12f), Palette.Hex(0xDADDE0));
                mb.Box(new Vector3(sx + 0.3f, 2.25f, lz + 0.8f), new Vector3(0.5f, 0.14f, 1.12f), Palette.Hex(0x3FB26A));                            // the arch, green
                _lanes.Add(new Vector3(sx, 0, lz + 0.8f));
            }

            // ---- the concourse: whatever the player has placed on its floor
            DrawFloor(mb, g, x0, z0);

            // ---- gate lounges along the glass: rows of seats, a desk and a gate number each
            int gateCount = Mathf.Clamp(gates, 1, 24);
            float gx = x0 + W * 0.86f;
            for (int i = 0; i < gateCount; i++)
            {
                float gz = z0 + L * (i + 0.5f) / gateCount, span = Mathf.Min(7f, L / gateCount - 1.2f);
                mb.Box(new Vector3(W * 0.5f - 1.6f, 0.55f, gz + span * 0.4f), new Vector3(0.9f, 1.1f, 1.6f), Palette.White, trim);                  // gate desk
                mb.Box(new Vector3(W * 0.5f - 0.5f, 3.3f, gz), new Vector3(0.2f, 0.8f, 1.6f), Palette.Hex(0x12161B));
                mb.Box(new Vector3(W * 0.5f - 0.62f, 3.3f, gz), new Vector3(0.05f, 0.5f, 1.0f), Palette.Hex(0xF2C230));
                int rows = 2;
                for (int r = 0; r < rows; r++)
                    for (float sz = gz - span * 0.4f; sz <= gz + span * 0.25f; sz += 0.75f)
                    {
                        var sp = new Vector3(gx - 4.5f + r * 1.7f, 0, sz);
                        mb.Box(sp + Vector3.up * 0.42f, new Vector3(0.6f, 0.1f, 0.62f), Palette.Hex(0x2D4E7C));
                        mb.Box(sp + new Vector3(0.28f, 0.75f, 0), new Vector3(0.08f, 0.6f, 0.62f), Palette.Hex(0x2D4E7C));
                        if ((int)(sz * 4) % 3 == 0) _seats.Add(sp + new Vector3(-0.2f, 0, 0));
                    }
                _gates.Add(new Vector3(W * 0.5f - 2.4f, 0, gz));
            }
            _static = mb.ToObject("Static", transform);
            g.Build(_static.transform);
        }

        void Update()
        {
            var s = _sim;
            string key = s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + FloorKey(s);
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
                        a.Wait = a.Step < a.Path.Length ? Random.Range(0.5f, 7f) : 0;
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
            var path = new List<Vector3>();
            if (!arriving)
            {
                path.Add(R(_doors)); path.Add(R(_desks));
                var lane = R(_lanes); path.Add(lane + Vector3.left * 3.5f); path.Add(lane + Vector3.right * 3.5f);
                if (_shops.Count > 0 && Random.value < 0.7f) path.Add(R(_shops));
                if (_seats.Count > 0 && Random.value < 0.6f) path.Add(R(_seats));
                path.Add(R(_gates));
            }
            else
            {
                path.Add(R(_gates));
                if (_shops.Count > 0 && Random.value < 0.3f) path.Add(R(_shops));
                var lane = R(_lanes); path.Add(lane + Vector3.right * 3.5f + Vector3.forward * 1.6f); path.Add(lane + Vector3.left * 3.5f + Vector3.forward * 1.6f);
                path.Add(R(_doors));
            }
            var a = new Agent { Seed = Random.Range(1, 1 << 30), Arriving = arriving, Kind = Random.Range(0, 12), Path = path.ToArray(), Speed = Random.Range(1.6f, 2.6f), Phase = Random.Range(0, 4) };
            // Start part-way along, so the hall is full the moment you look in.
            int start = _agents.Count < want * 0.8f ? Random.Range(0, path.Count - 1) : 0;
            a.Pos = path[start]; a.Step = start + 1 < path.Count ? start + 1 : start; a.Target = Vector3.right;
            _agents.Add(a);
        }

        // ------------------------------------------------------------------ the concourse floor

        /// <summary>The floor's corner (cell 0,0) in the hall's own space: just past security, two metres in from the end wall.</summary>
        public Vector3 FloorCorner { get { return new Vector3(-Width * 0.5f + Width * 0.43f, 0, -Length * 0.5f + 2f); } }

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
            int h = s.State.terminal.Count;
            foreach (var p in s.State.terminal) h = h * 31 + p.id.GetHashCode() + p.x * 7 + p.z * 131 + p.rot * 3 + p.level * 17;
            return h.ToString();
        }

        void DrawFloor(MeshBuilder mb, Ground g, float x0, float z0)
        {
            var corner = FloorCorner;
            int rows = _sim.FloorRows;
            // The floor you can build on: a warm stone with fine joints at every cell.
            float fw = Sim.FloorCols * Sim.Cell, fl = rows * Sim.Cell;
            g.Rect("Concrete", corner.x + fw * 0.5f, corner.z + fl * 0.5f, fw, fl, 0.014f, 0, new Color(1.32f, 1.22f, 1.04f));
            var joint = new Color(0.62f, 0.58f, 0.5f);
            for (int x = 0; x <= Sim.FloorCols; x++) mb.Box(new Vector3(corner.x + x * Sim.Cell, 0.02f, corner.z + fl * 0.5f), new Vector3(0.05f, 0.012f, fl), joint);
            for (int z = 0; z <= rows; z += 1) mb.Box(new Vector3(corner.x + fw * 0.5f, 0.02f, corner.z + z * Sim.Cell), new Vector3(fw, 0.012f, 0.05f), joint);

            foreach (var p in _sim.State.terminal)
            {
                var d = Sim.AmenityById(p.id);
                int w, dp; Sim.Footprint(d, p.rot, out w, out dp);
                var c = corner + new Vector3((p.x + w * 0.5f) * Sim.Cell, 0, (p.z + dp * 0.5f) * Sim.Cell);
                mb.Push().Translate(c).RotateY(p.rot * 90);
                Amenity(mb, d, d.W * Sim.Cell, d.D * Sim.Cell, p.level, p.x * 7 + p.z * 13);
                mb.Pop();
                // Where passengers stop: in front of it (toward check-in), or on it for seats.
                var stop = c + new Vector3(-w * Sim.Cell * 0.5f - 0.7f, 0, 0);
                if (d.Gives == Want.Seating || d.Gives == Want.Lounge) _seats.Add(c); else _shops.Add(stop);
            }
        }

        static readonly Color[] Brand = { Palette.Hex(0xD9432F), Palette.Hex(0x2F8F5B), Palette.Hex(0xE2A521), Palette.Hex(0x6B4FBB), Palette.Hex(0x1E8FB0), Palette.Hex(0xC2417C) };

        /// <summary>
        /// One amenity, modelled about its middle with its front toward -x (the side passengers come from):
        /// w along x, d along z. Higher levels add a brighter sign, a canopy, more of what it sells.
        /// </summary>
        static void Amenity(MeshBuilder mb, AmenityDef def, float w, float d, int level, int seed)
        {
            Color wall = Palette.Hex(0xEDEAE2), dark = Palette.Hex(0x2B3138), wood = Palette.Hex(0x7A5A3A), steel = Palette.Hex(0xB9BEC4);
            Color brand = Brand[(seed + def.Id.Length) % Brand.Length];
            float hx = w * 0.5f, hz = d * 0.5f;
            void Sign(float y, Color c) { mb.Box(new Vector3(-hx + 0.12f, y, 0), new Vector3(0.24f, 0.6f, d - 0.3f), c); if (level >= 2) { mb.Glow = true; mb.Box(new Vector3(-hx - 0.02f, y, 0), new Vector3(0.05f, 0.3f, d * 0.55f), Palette.Hex(0xFFF2C8)); mb.Glow = false; } }
            void Unit(Color c)       // a shop unit: back and side walls, a fascia, a canopy at level 3
            {
                mb.Box(new Vector3(hx - 0.15f, 1.5f, 0), new Vector3(0.3f, 3.0f, d - 0.1f), wall);
                foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0, 1.2f, e * (hz - 0.1f)), new Vector3(w - 0.3f, 2.4f, 0.2f), wall);
                Sign(2.75f, c);
                if (level >= 3) mb.Box(new Vector3(-hx - 0.5f, 2.35f, 0), new Vector3(1.2f, 0.08f, d - 0.2f), c);
            }
            void Table(Vector3 at) { mb.Cylinder(at, at + Vector3.up * 0.72f, 0.06f, 5, steel); mb.Cylinder(at + Vector3.up * 0.72f, at + Vector3.up * 0.78f, 0.5f, 10, Palette.Hex(0xD9C9A8)); foreach (float e in new[] { -0.75f, 0.75f }) mb.Box(at + new Vector3(0, 0.25f, e), new Vector3(0.42f, 0.5f, 0.42f), brand); }
            void Seat(Vector3 at, Color c) { mb.Box(at + Vector3.up * 0.42f, new Vector3(0.6f, 0.1f, 0.6f), c); mb.Box(at + new Vector3(0.28f, 0.75f, 0), new Vector3(0.08f, 0.6f, 0.6f), c); }
            mb.Smoothness = 0.3f;
            switch (def.Id)
            {
                case "vending":
                    for (int k = 0; k < level; k++) { float z = (k - (level - 1) * 0.5f) * 0.62f; mb.Box(new Vector3(0.3f, 1.0f, z), new Vector3(0.8f, 2.0f, 0.58f), k % 2 == 0 ? Palette.Hex(0xC8362E) : Palette.Hex(0x1F5FA8)); mb.Glow = true; mb.Box(new Vector3(-0.12f, 1.2f, z), new Vector3(0.05f, 1.2f, 0.4f), Palette.Hex(0xDFF4FF)); mb.Glow = false; }
                    break;
                case "cafe": case "noodle": case "restaurant":
                    Unit(brand);
                    mb.Box(new Vector3(hx - 1.1f, 0.55f, 0), new Vector3(0.8f, 1.1f, d - 0.8f), wood, Palette.Hex(0xD9C9A8));          // counter
                    mb.Box(new Vector3(hx - 0.45f, 1.9f, 0), new Vector3(0.1f, 0.7f, d - 1f), dark);                                   // menu board
                    for (int k = 0; k < (int)(d / 1.6f); k++) mb.Cylinder(new Vector3(hx - 1.1f, 1.1f, -hz + 0.9f + k * 1.5f), new Vector3(hx - 1.1f, 1.45f, -hz + 0.9f + k * 1.5f), 0.14f, 8, steel);
                    if (w > 4.5f) for (int k = 0; k < (int)(d / 1.9f); k++) Table(new Vector3(-hx + 1.2f + (k % 2) * 1.5f, 0, -hz + 1.0f + k * 1.8f));
                    break;
                case "newsstand": case "gifts": case "fashion": case "dutyfree":
                    Unit(brand);
                    int racks = Mathf.Max(1, (int)(w / 1.3f));
                    for (int k = 0; k < racks; k++) mb.Box(new Vector3(-hx + 0.9f + k * 1.25f, 0.7f, 0), new Vector3(0.5f, 1.4f, d * 0.6f), Palette.Hex(0xF4F1EA), brand);
                    if (def.Id == "dutyfree") { mb.Smoothness = 0.9f; mb.Box(new Vector3(-hx + 0.3f, 1.2f, 0), new Vector3(0.08f, 2.4f, d - 1.5f), Palette.Hex(0x9FD3E8)); mb.Smoothness = 0.3f; }
                    break;
                case "seats":
                    for (float z = -hz + 0.5f; z <= hz - 0.4f; z += 0.72f) { Seat(new Vector3(-0.5f, 0, z), Palette.Hex(0x2D4E7C)); if (level >= 2) { mb.Push().Translate(0.6f, 0, z).RotateY(180); Seat(Vector3.zero, Palette.Hex(0x2D4E7C)); mb.Pop(); } }
                    break;
                case "recliners":
                    for (int k = 0; k < (int)(d / 1.25f); k++) { var at = new Vector3(0, 0, -hz + 0.8f + k * 1.2f); mb.Box(at + new Vector3(0, 0.35f, 0), new Vector3(w - 1.6f, 0.25f, 0.8f), Palette.Hex(0x3E6E8E)); mb.Push().Translate(at + new Vector3(w * 0.5f - 0.9f, 0.6f, 0)).Rotate(Quaternion.Euler(0, 0, 35)); mb.Box(Vector3.zero, new Vector3(1.0f, 0.2f, 0.8f), Palette.Hex(0x3E6E8E)); mb.Pop(); }
                    mb.Box(new Vector3(hx - 0.2f, 0.9f, 0), new Vector3(0.12f, 1.8f, d), Palette.Hex(0xC9D6DD));
                    break;
                case "charging":
                    mb.Box(new Vector3(0.3f, 1.0f, 0), new Vector3(0.5f, 0.08f, d - 0.3f), wood); mb.Box(new Vector3(0.5f, 0.5f, 0), new Vector3(0.1f, 1.0f, d - 0.4f), dark);
                    for (float z = -hz + 0.6f; z <= hz - 0.5f; z += 0.9f) { mb.Cylinder(new Vector3(-0.3f, 0, z), new Vector3(-0.3f, 0.7f, z), 0.05f, 5, steel); mb.Cylinder(new Vector3(-0.3f, 0.7f, z), new Vector3(-0.3f, 0.76f, z), 0.22f, 8, dark); }
                    break;
                case "lounge":
                    mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.2f, 0.04f, d - 0.2f), Palette.Hex(0x4B3A78));
                    mb.Smoothness = 0.9f; foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(0, 1.1f, e * (hz - 0.1f)), new Vector3(w - 0.2f, 2.2f, 0.08f), Palette.Hex(0x9FD3E8)); mb.Smoothness = 0.3f;
                    for (int k = 0; k < 3; k++) mb.Box(new Vector3(-hx + 1.6f + k * 2.3f, 0.4f, -hz + 1.3f), new Vector3(1.8f, 0.8f, 0.9f), Palette.Hex(0x6B4FBB), Palette.Hex(0x8A70D6));
                    mb.Box(new Vector3(0, 0.6f, hz - 1.0f), new Vector3(w - 2f, 1.2f, 0.9f), Palette.Hex(0x3A2E26), Palette.Hex(0xD9C9A8));
                    Sign(2.6f, Palette.Hex(0x6B4FBB));
                    break;
                case "toilets":
                    mb.Box(new Vector3(0.2f, 1.4f, 0), new Vector3(w - 0.5f, 2.8f, d - 0.2f), Palette.Hex(0xDDE6EA), Palette.Hex(0xC2CED4));
                    foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(-hx + 0.42f, 1.05f, e * d * 0.24f), new Vector3(0.1f, 2.1f, 0.9f), e < 0 ? Palette.Hex(0x1F5FA8) : Palette.Hex(0xC2417C)); mb.Box(new Vector3(-hx + 0.36f, 2.45f, e * d * 0.24f), new Vector3(0.06f, 0.4f, 0.4f), Palette.White); }
                    break;
                case "fountain":
                    mb.Box(new Vector3(0.3f, 0.6f, 0), new Vector3(0.6f, 1.2f, 0.8f), steel); mb.Smoothness = 0.9f; mb.Box(new Vector3(0.05f, 1.22f, 0), new Vector3(0.5f, 0.05f, 0.6f), Palette.Hex(0x6FB0CC)); mb.Smoothness = 0.3f;
                    break;
                case "bins":
                    Color[] bc = { Palette.Hex(0x2F8F5B), Palette.Hex(0x1F5FA8), Palette.Hex(0xE2A521) };
                    for (int k = 0; k < 3; k++) mb.Box(new Vector3(0.2f, 0.5f, (k - 1) * 0.5f), new Vector3(0.45f, 1.0f, 0.42f), bc[k], dark);
                    break;
                case "janitor":
                    mb.Box(new Vector3(0.4f, 1.1f, 0), new Vector3(0.9f, 2.2f, d - 0.4f), Palette.Hex(0xCFD6DA)); mb.Box(new Vector3(-0.5f, 0.5f, -hz + 0.8f), new Vector3(0.8f, 0.8f, 0.6f), Palette.Hex(0xE2A521), dark);
                    mb.Cylinder(new Vector3(-0.5f, 0.9f, -hz + 0.6f), new Vector3(-0.3f, 1.9f, -hz + 0.6f), 0.03f, 4, wood);
                    break;
                case "plants":
                    mb.Box(new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 1.2f), Palette.Hex(0xC9CED3), Palette.Hex(0x4A3A2A));
                    mb.Cylinder(new Vector3(0, 0.8f, 0), new Vector3(0, 2.3f, 0), 0.1f, 0.07f, 5, wood);
                    for (int k = 0; k < 6; k++) { mb.Push().Translate(0, 2.3f, 0).Rotate(Quaternion.Euler(0, k * 60, 38)); mb.Box(new Vector3(0.7f, 0, 0), new Vector3(1.4f, 0.05f, 0.3f), Palette.Hex(0x3E8E4A)); mb.Pop(); }
                    break;
                case "tv":
                    mb.Box(new Vector3(0.5f, 1.6f, 0), new Vector3(0.2f, 3.2f, d - 0.3f), dark);
                    mb.Glow = true; for (int k = 0; k < 2 + level; k++) mb.Box(new Vector3(0.36f, 0.9f + (k / 2) * 1.0f, (k % 2 == 0 ? -1 : 1) * d * 0.22f), new Vector3(0.06f, 0.8f, d * 0.38f), k % 2 == 0 ? Palette.Hex(0x7FD0FF) : Palette.Hex(0xFFD98A)); mb.Glow = false;
                    break;
                case "arcade":
                    for (int k = 0; k < 2 + level; k++) { float z = -hz + 0.7f + (k % 3) * 1.2f, x = k < 3 ? 0.9f : -0.6f; mb.Box(new Vector3(x, 0.9f, z), new Vector3(0.8f, 1.8f, 0.8f), Brand[k % Brand.Length], dark); mb.Glow = true; mb.Box(new Vector3(x - 0.42f, 1.3f, z), new Vector3(0.05f, 0.5f, 0.55f), Palette.Hex(0x9BE7FF)); mb.Glow = false; }
                    break;
                case "play":
                    mb.Box(new Vector3(0, 0.05f, 0), new Vector3(w - 0.3f, 0.08f, d - 0.3f), Palette.Hex(0x4CB8A0));
                    mb.Box(new Vector3(1.0f, 0.9f, 1.0f), new Vector3(1.4f, 1.8f, 1.4f), Palette.Hex(0xE2A521), Palette.Hex(0xD9432F));
                    mb.Push().Translate(-0.3f, 0.75f, 1.0f).Rotate(Quaternion.Euler(0, 0, 32)); mb.Box(Vector3.zero, new Vector3(2.4f, 0.1f, 0.8f), Palette.Hex(0x1E8FB0)); mb.Pop();
                    for (int k = 0; k < 4; k++) mb.Box(new Vector3(-1.6f + k * 0.9f, 0.3f, -1.4f), new Vector3(0.5f, 0.5f, 0.5f), Brand[k % Brand.Length]);
                    foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(new Vector3(ex * (hx - 0.2f), 0.35f, ez * (hz - 0.2f)), new Vector3(0.12f, 0.7f, 0.12f), Palette.White);
                    break;
                case "gallery":
                    mb.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.2f, 0.04f, d - 0.2f), Palette.Hex(0x34404E));
                    for (int k = 0; k < 3; k++) { var at = new Vector3(-hx + 1.2f + k * 1.8f, 0, 0); mb.Box(at + Vector3.up * 0.5f, new Vector3(0.9f, 1.0f, 0.9f), Palette.White); mb.Box(at + Vector3.up * 1.3f, new Vector3(1.1f, 0.14f, 0.16f), Palette.Hex(0xE8ECF0)); mb.Box(at + Vector3.up * 1.3f, new Vector3(0.3f, 0.06f, 1.2f), Palette.Hex(0xE8ECF0)); }
                    mb.Box(new Vector3(hx - 0.2f, 1.5f, 0), new Vector3(0.14f, 3.0f, d - 0.2f), Palette.Hex(0x26303C));
                    break;
                case "screens":
                    mb.Cylinder(Vector3.zero, Vector3.up * 2.4f, 0.08f, 5, steel); mb.Box(new Vector3(0, 2.7f, 0), new Vector3(0.16f, 0.8f, 1.5f), dark);
                    mb.Glow = true; mb.Box(new Vector3(-0.1f, 2.7f, 0), new Vector3(0.04f, 0.62f, 1.3f), Palette.Hex(0xF2C230)); mb.Glow = false;
                    break;
                case "infodesk":
                    mb.Cylinder(Vector3.zero, Vector3.up * 1.05f, 0.95f, 0.95f, 12, Palette.White); mb.Cylinder(Vector3.up * 1.05f, Vector3.up * 1.12f, 1.05f, 1.05f, 12, Palette.Hex(0x1F5FA8));
                    mb.Cylinder(Vector3.up * 1.1f, Vector3.up * 3f, 0.07f, 5, steel); mb.Box(Vector3.up * 3.2f, new Vector3(0.14f, 0.7f, 1.2f), Palette.Hex(0x1F5FA8));
                    break;
                default:
                    mb.Box(new Vector3(0, 0.6f, 0), new Vector3(w - 0.3f, 1.2f, d - 0.3f), brand);
                    break;
            }
            mb.Smoothness = 0.25f;
        }
    }
}
