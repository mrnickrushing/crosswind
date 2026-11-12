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
        // What each shop stop and seat belongs to (null: a gate lounge's own seats).
        readonly List<string> _shopIds = new List<string>(), _seatIds = new List<string>();
        // The people who work here (standing at desks, tills and lanes), the carousels' centres, and the cars passing outside.
        readonly List<(Vector3 at, float yaw, int kind)> _staff = new List<(Vector3, float, int)>();
        readonly List<Vector3> _carousels = new List<Vector3>();
        readonly List<Matrix4x4> _bags = new List<Matrix4x4>();
        readonly List<(Transform car, float speed)> _traffic = new List<(Transform, float)>();
        static Mesh[] _bagMeshes;
        readonly List<Matrix4x4>[] _bagLists = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        float _roadZ0, _roadZ1;
        static readonly int[] StaffKinds = { 4, 10, 1, 6, 8 };
        // Arrivals: where they come through the security wall, and where they wait for their bags.
        readonly List<Vector3> _reclaim = new List<Vector3>();
        Vector3 _reclaimOut;      // the way out of baggage reclaim, past the carousels' near ends
        Vector3 _arrivalsOut;

        public sealed class Agent { public int Seed; public int Kind; public Vector3 Pos, Target; public int Step; public float Wait, Speed, Phase; public bool Arriving; public Vector3[] Path; public bool[] Stop; public string[] At; }

        /// <summary>A passenger has just stopped at something on the concourse (the amenity's id): the HUD shows what they make of it.</summary>
        public static System.Action<Agent, string> Thought;
        readonly List<Agent> _agents = new List<Agent>();
        static Mesh[][] _walk; static Mesh[] _idle;
        // The same people with a third of the triangles, for when the camera stands far back; and the plain shape that casts each one's shadow.
        static Mesh[][] _walkFar; static Mesh[] _idleFar; static Mesh _shadow;
        readonly List<Matrix4x4> _shadows = new List<Matrix4x4>();
        readonly Plane[] _planes = new Plane[6];
        /// <summary>Beyond this camera distance the crowd is drawn with its far models.</summary>
        const float FarCrowd = 85f;
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
                _walk = new Mesh[12][]; _idle = new Mesh[12]; _walkFar = new Mesh[12][]; _idleFar = new Mesh[12];
                for (int i = 0; i < 12; i++)
                {
                    _walk[i] = new Mesh[4]; _walkFar[i] = new Mesh[4];
                    for (int f = 0; f < 4; f++)
                    {
                        _walk[i][f] = Resources.Load<Mesh>("Models/person_" + i + "_w" + f);
                        _walkFar[i][f] = Resources.Load<Mesh>("Models/personfar_" + i + "_w" + f) ?? _walk[i][f];
                    }
                    _idle[i] = Resources.Load<Mesh>("Models/person_" + i + "_idle");
                    _idleFar[i] = Resources.Load<Mesh>("Models/personfar_" + i + "_idle") ?? _idle[i];
                }
                // A person's shadow is cast by a plain column their size: forty triangles in the shadow pass, not fifteen hundred.
                var col = new MeshBuilder();
                col.Cylinder(Vector3.zero, Vector3.up * 0.86f, 0.13f, 0.16f, 8, Color.grey);
                col.Sphere(Vector3.up * 0.93f, new Vector3(0.09f, 0.1f, 0.09f), 6, Color.grey);
                _shadow = col.ToMesh("Crowd shadow");
            }
            for (int i = 0; i < v._batches.Length; i++) v._batches[i] = new List<Matrix4x4>();
            v.Rebuild();
            return v;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>How full the hall is, 0 to 1 (the crowd's share of the most it draws).</summary>
        public float Crowd => _agents.Count / 340f;

        void Rebuild()
        {
            if (_static != null) Destroy(_static);
            _doors.Clear(); _desks.Clear(); _lanes.Clear(); _shops.Clear(); _seats.Clear(); _gates.Clear(); _shopIds.Clear(); _seatIds.Clear(); _staff.Clear(); _carousels.Clear(); _traffic.Clear();
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
                // Between one door and the next: a bench against the wall with a plant either side.
                if (i > 0)
                {
                    float mz = z0 + L * i / (float)doorCount;
                    mb.Wide("bench_park", new Vector3(x0 + 0.9f, 0, mz), 180, 1.8f);
                    foreach (float e in new[] { -1f, 1f }) mb.Tall("houseplant_c", new Vector3(x0 + 0.8f, 0, mz + e * 1.6f), 0, 1.2f);
                }
            }

            // ---- check-in: islands of desks with a baggage belt behind and queue barriers in front
            int desks = s.CheckinDesks, perIsland = 6, islands = (desks + perIsland - 1) / perIsland;
            float cx = x0 + 11.5f, checkinWidth = L - Sim.ReclaimWidth;      // the far end of the hall is the arrivals corner
            for (int i = 0; i < islands; i++)
            {
                float iz = z0 + checkinWidth * (i + 0.5f) / islands;
                int n = Mathf.Min(perIsland, desks - i * perIsland);
                // The baggage belt behind the desks: lengths of roller conveyor end to end.
                {
                    float run = n * 1.9f + 1; int pieces = Mathf.CeilToInt(run / 2f); float each = run / pieces;
                    for (int k = 0; k < pieces; k++) mb.Fit("conveyor", new Vector3(cx + 1.6f, 0, iz - run * 0.5f + (k + 0.5f) * each), 0, each, 0.62f, 0.9f);
                }
                mb.Box(new Vector3(cx + 2.6f, 2.9f, iz), new Vector3(0.25f, 1.1f, n * 1.9f), Palette.Hex(0x12161B));                                // flight board
                mb.Box(new Vector3(cx + 2.45f, 2.5f, iz), new Vector3(0.05f, 0.14f, n * 1.9f - 0.4f), Palette.Hex(0xF2C230));
                foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(cx + 2.6f, 1.2f, iz + e * n * 0.95f), new Vector3(0.18f, 2.4f, 0.18f), Palette.Steel);
                // Islands are lettered from the left as you face them (z runs right to left on screen).
                string island = "CHECK-IN " + (char)('A' + (islands - 1 - i));
                _labels.Add((island, new Vector3(cx + 2.42f, 3.0f, iz), 0, Mathf.Min(0.6f, (n * 1.9f - 0.6f) * 0.95f / island.Length * 1.7f), Color.white));
                // In the gap before the next island: seats back to back, a plant at the end of the row.
                float gap = checkinWidth / islands - (n * 1.9f + 1);
                if (i > 0 && gap > 5f)
                {
                    float gz2 = z0 + checkinWidth * i / (float)islands;
                    mb.Mat(0.7f, 0.6f); foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(cx - 0.5f, 0.2f, gz2 + e * 0.3f), new Vector3(4.4f, 0.06f, 0.08f), Palette.Steel); mb.Mat(0.25f);
                    for (float x = cx - 2.5f; x <= cx + 1.5f; x += 0.52f) { mb.Tall("chair", new Vector3(x, 0, gz2 - 0.3f), 270, 0.88f); mb.Tall("chair", new Vector3(x, 0, gz2 + 0.3f), 90, 0.88f); }
                    mb.Tall("plant_pot", new Vector3(cx - 3.1f, 0, gz2), 0, 0.95f);
                }
                for (int k = 0; k < n; k++)
                {
                    float dz = iz + (k - (n - 1) / 2f) * 1.9f;
                    mb.Fit("desk_white", new Vector3(cx, 0, dz), 0, 1.5f, 1.08f, 0.9f, deskTint);
                    mb.Long("screen", new Vector3(cx + 0.12f, 1.08f, dz + 0.32f), 180, 0.46f);
                    mb.Tall("chair_desk", new Vector3(cx + 0.8f, 0, dz + 0.3f), 0, 0.95f);
                    mb.Box(new Vector3(cx + 0.9f, 0.3f, dz - 0.5f), new Vector3(0.8f, 0.3f, 0.5f), Palette.Hex(0x9AA0A6));                           // scale
                    if ((k + i) % 2 == 0) mb.Tall("luggage", new Vector3(cx + 0.9f, 0.45f, dz - 0.5f), 90 + k * 25, 0.55f, (k + i) % 4 == 0 ? Color.white : new Color(0.6f, 0.9f, 1.3f));
                    else if ((k + i) % 3 == 0) mb.Tall("luggage", new Vector3(cx + 1.6f, 0.62f, dz + 0.4f), 20 + k * 40, 0.5f, new Color(1.3f, 0.7f, 0.6f));      // one already on the belt
                    _desks.Add(new Vector3(cx - 1.3f, 0, dz));
                    if ((k + i * 3) % 3 != 2) _staff.Add((new Vector3(cx + 0.72f, 0, dz - 0.2f), 0, StaffKinds[(k + i) % StaffKinds.Length]));       // two desks in three are open
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
            float lanesWidth = L - Sim.ArrivalsGap;      // the end of the wall is the arrivals' way out
            mb.Box(new Vector3(sx, 1.3f, 0), new Vector3(0.3f, 2.6f, L), Palette.Hex(0xD5D8DC), Palette.White);                                    // the wall security pierces
            for (int i = 0; i < lanes; i++)
            {
                float lz = z0 + lanesWidth * (i + 0.5f) / lanes;
                mb.Box(new Vector3(sx, 1.3f, lz), new Vector3(0.5f, 2.7f, Mathf.Min(4.2f, lanesWidth / lanes - 0.6f)), Palette.Hex(0x7C8792));                  // the opening
                // The X-ray machine astride the wall, a length of roller conveyor feeding it and another taking the trays away.
                mb.Long("scanner", new Vector3(sx, 0, lz - 1.0f), 0, 2.4f);
                foreach (float e in new[] { -1f, 1f }) mb.Fit("conveyor", new Vector3(sx + e * 2.2f, 0, lz - 1.0f), 90, 2.0f, 0.62f, 0.8f);
                foreach (float e in new[] { -0.5f, 0.5f }) mb.Box(new Vector3(sx + 0.3f, 1.1f, lz + 0.8f + e), new Vector3(0.5f, 2.2f, 0.12f), Palette.Hex(0xDADDE0));
                mb.Box(new Vector3(sx + 0.3f, 2.25f, lz + 0.8f), new Vector3(0.5f, 0.14f, 1.12f), Palette.Hex(0x3FB26A));                            // the arch, green
                // Bags riding the belt, in and out of the scanner.
                mb.Tall("luggage", new Vector3(sx - 2.2f, 0.62f, lz - 1.0f), 90 + i * 30, 0.5f, i % 2 == 0 ? Color.white : new Color(0.6f, 0.9f, 1.3f));
                mb.Tall("luggage", new Vector3(sx + 2.3f, 0.62f, lz - 1.0f), 60 + i * 50, 0.46f, i % 3 == 0 ? new Color(1.3f, 0.7f, 0.6f) : Color.white);
                _lanes.Add(new Vector3(sx, 0, lz + 0.8f));
                _staff.Add((new Vector3(sx + 1.1f, 0, lz + 1.75f), 20, StaffKinds[(i + 2) % StaffKinds.Length]));      // the officer past the arch
                // A sign over every lane while there are few, then over every third.
                if (lanes <= 6 || i % 3 == 1)
                {
                    mb.Box(new Vector3(sx, 3.05f, lz), new Vector3(0.24f, 0.8f, 4.4f), Palette.Hex(0x1F5FA8));
                    _labels.Add(("SECURITY", new Vector3(sx - 0.16f, 3.05f, lz), 0, 0.6f, Color.white));
                }
            }
            // ---- arrivals: their own way out through the end of the security wall, then baggage reclaim, then the doors
            _reclaim.Clear();
            float outZ = L * 0.5f - 1.7f;
            _arrivalsOut = new Vector3(sx, 0, outZ);
            mb.Box(new Vector3(sx, 1.3f, outZ), new Vector3(0.5f, 2.7f, 2.9f), Palette.Hex(0x7C8792));
            mb.Box(new Vector3(sx, 3.05f, outZ), new Vector3(0.24f, 0.8f, 3.1f), Palette.Hex(0x2F8F5B));
            _labels.Add(("ARRIVALS", new Vector3(sx - 0.16f, 3.05f, outZ), 0, 0.42f, Color.white));
            // One-way: a row of posts on the landside so nobody walks in.
            for (float z = outZ - 1.2f; z <= outZ + 1.21f; z += 0.8f) mb.Cylinder(new Vector3(sx - 1.4f, 0, z), new Vector3(sx - 1.4f, 0.9f, z), 0.05f, 6, Palette.Steel);
            int carousels = s.Carousels;
            float rz = L * 0.5f - 7.6f;
            for (int i = 0; i < carousels; i++)
            {
                // A carousel: two runs of belt and the turns at their ends round a dark island, bags going round.
                float rx = x0 + 5.6f + i * 5.4f;
                foreach (float e in new[] { -1f, 1f })
                {
                    for (int k = 0; k < 3; k++) mb.Fit("conveyor", new Vector3(rx + e * 0.85f, 0, rz + (k - 1) * 2f), 0, 2f, 0.5f, 0.8f);
                    mb.Fit("conveyor", new Vector3(rx, 0, rz + e * 3.4f), 90, 2.5f, 0.5f, 0.8f);
                }
                mb.Box(new Vector3(rx, 0.3f, rz), new Vector3(0.86f, 0.6f, 6f), Palette.Hex(0x2B3138));
                mb.Box(new Vector3(rx, 1.5f, rz), new Vector3(0.12f, 1.8f, 0.12f), Palette.Steel);
                mb.Box(new Vector3(rx, 2.6f, rz), new Vector3(0.2f, 0.7f, 1.3f), Palette.Hex(0x12161B));
                _labels.Add(((carousels - i).ToString(), new Vector3(rx - 0.12f, 2.6f, rz), 0, 0.6f, Palette.Hex(0xF2C230)));
                _carousels.Add(new Vector3(rx, 0, rz));
                // Where they stand to wait for a bag: along both sides.
                for (float z = rz - 2.4f; z <= rz + 2.41f; z += 1.2f) { _reclaim.Add(new Vector3(rx - 1.75f, 0, z)); _reclaim.Add(new Vector3(rx + 1.75f, 0, z)); }
            }
            // Its sign hangs on two posts at the near end of the carousels, clear of the security lanes behind.
            float signX = x0 + 2.9f;
            _reclaimOut = new Vector3(x0 + 1.8f, 0, rz - 4.4f);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(signX, 1.5f, rz + e * 3.1f), new Vector3(0.14f, 3.0f, 0.14f), Palette.Steel);
            mb.Box(new Vector3(signX, 3.05f, rz), new Vector3(0.24f, 0.8f, 6.4f), Palette.Hex(0x2F8F5B));
            _labels.Add(("BAGGAGE RECLAIM", new Vector3(signX - 0.16f, 3.05f, rz), 0, 0.55f, Color.white));

            // Vending and a cash machine against each end wall of the check-in hall.
            foreach (float e in new[] { -1f, 1f })
            {
                mb.Tall("vending", new Vector3(cx - 1.2f, 0, e * (L * 0.5f - 0.75f)), e < 0 ? 90 : 270, 1.7f);
                mb.Tall("atm", new Vector3(cx + 0.2f, 0, e * (L * 0.5f - 0.6f)), e < 0 ? 90 : 270, 1.75f);
                mb.Tall("houseplant_c", new Vector3(cx + 1.3f, 0, e * (L * 0.5f - 0.7f)), 0, 1.2f);
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
                        if ((int)(sz * 4) % 3 == 0) { _seats.Add(sp + new Vector3(-0.2f, 0, 0)); _seatIds.Add(null); }
                    }
                }
                _gates.Add(new Vector3(W * 0.5f - 2.4f, 0, gz));
                _staff.Add((new Vector3(W * 0.5f - 0.95f, 0, gz + span * 0.4f - 0.2f), 0, StaffKinds[(i + 1) % StaffKinds.Length]));
                _labels.Add(("A" + (gateCount - i), new Vector3(W * 0.5f - 0.68f, 3.3f, gz), 0, 0.42f, Palette.Hex(0x12161B)));
                // A tall plant against the glass between this gate and the next.
                if (i > 0 && L / gateCount >= 4.6f) mb.Tall("fiddle", new Vector3(W * 0.5f - 1.0f, 0, z0 + L * i / (float)gateCount), i * 40, 2.0f);
            }
            // ---- beyond the glass: the apron, a stand every so often with its lead-in line and a jet bridge to the wall
            float apronX = W * 0.5f + 0.6f;
            g.Rect("Tarmac", apronX + 40, 0, 80, L + 220, 0.004f, 0, new Color(1.1f, 1.1f, 1.12f));
            _apronSlots.Clear();
            int stands = Mathf.Clamp(Mathf.FloorToInt(L / ApronPitch), 1, 4);
            for (int i = 0; i < stands; i++)
            {
                float az = z0 + L * (i + 0.5f) / stands;
                _apronSlots.Add(az);
                mb.Box(new Vector3(apronX + 24, 0.02f, az), new Vector3(44, 0.02f, 0.3f), Palette.Hex(0xE2B93B));                                 // lead-in line
                mb.Box(new Vector3(apronX + 4.5f, 0.02f, az), new Vector3(0.3f, 0.02f, 9f), Palette.Hex(0xE2B93B));                               // stop bar
                // The bridge: a glazed tunnel on a leg, off to one side of the nose, ending in a cab at the door.
                float bz = az - 5.2f;
                mb.Box(new Vector3(apronX + 5.5f, 3.6f, bz), new Vector3(11f, 2.2f, 2.2f), Palette.Hex(0xDADDE0), Palette.White);
                mb.Smoothness = 0.9f; mb.Box(new Vector3(apronX + 5.5f, 3.8f, bz - 1.12f), new Vector3(10.4f, 0.9f, 0.05f), Palette.Hex(0x6FB0CC)); mb.Smoothness = 0.25f;
                mb.Box(new Vector3(apronX + 11.4f, 3.6f, bz + 1.2f), new Vector3(2.6f, 2.4f, 3.4f), Palette.Hex(0xC9CDD1), Palette.White);
                mb.Cylinder(new Vector3(apronX + 9.5f, 0, bz), new Vector3(apronX + 9.5f, 2.5f, bz), 0.35f, 8, Palette.Steel);
                mb.Box(new Vector3(apronX + 9.5f, 0.3f, bz), new Vector3(1.6f, 0.6f, 2.6f), Palette.Hex(0x39424A));
            }
            ClearApron();
            // ---- outside the doors: a pavement, the drop-off road with its centre line, and cars at the kerb
            float kerb = x0 - 0.7f;
            g.Rect("Concrete", kerb - 2.4f, 0, 4.8f, L + 50, 0.004f, 0, new Color(1.05f, 1.05f, 1.08f));
            g.Rect("Asphalt", kerb - 10.3f, 0, 11f, L + 50, 0.006f);
            for (float z = z0 - 20; z < L * 0.5f + 20; z += 6f) mb.Box(new Vector3(kerb - 10.3f, 0.02f, z), new Vector3(0.22f, 0.02f, 2.6f), Palette.White);
            mb.Box(new Vector3(kerb - 4.8f, 0.08f, 0), new Vector3(0.3f, 0.16f, L + 50), Palette.Hex(0xB9BEC4));
            _static = mb.ToObject("Static", transform);
            {
                // Taxis wait by the doors; other cars have stopped where they found room. Kit cars face -z: along the road.
                string[] cars = { "taxi", "sedan", "suv", "taxi", "hatchback", "coupe", "taxi", "pickup" };
                var rng = new System.Random(doorCount * 31 + 7);
                int k = 0;
                for (float z = z0 + 3; z < L * 0.5f - 3; z += 6.2f + (float)rng.NextDouble() * 5f, k++)
                {
                    bool far = k % 4 == 3;       // one in four is passing in the far lane, the other way
                    if (far) continue;
                    Models.Kit(cars[(k + rng.Next(3)) % cars.Length], _static.transform, new Vector3(kerb - 6.6f, 0.02f, z), 0, 4.5f);
                }
                // And traffic: a few cars driving past in each lane, round and round.
                _roadZ0 = z0 - 20; _roadZ1 = L * 0.5f + 20;
                int moving = Mathf.Clamp((int)(L / 22f), 3, 6);
                for (int m = 0; m < moving; m++)
                {
                    bool far = m % 2 == 0;
                    var car = Models.Kit(cars[(m * 3 + 1) % cars.Length], _static.transform, new Vector3(kerb - (far ? 12.1f : 8.95f), 0.02f, z0 - 20 + (float)rng.NextDouble() * (L + 40)), far ? 180 : 0, 4.5f);
                    if (car != null) _traffic.Add((car.transform, (far ? 1 : -1) * (7.5f + (float)rng.NextDouble() * 3f)));
                }
            }
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

        // ------------------------------------------------------------------ aircraft at the gates, seen through the glass

        /// <summary>Metres of glass a stand takes, and how much larger than the airfield's the aircraft are drawn here (people are life size indoors).</summary>
        const float ApronPitch = 34f, ApronScale = 2.5f;
        readonly List<float> _apronSlots = new List<float>();
        /// <summary>An aircraft at (or on its way to, or leaving) a stand beyond the glass.</summary>
        sealed class Stand { public GameObject Go; public int Slot; public float X, Z, Len, Age, Gone = -1; public bool Done; }
        /// <summary>Each stand's pushback tractor: parked by the bridge, under the nose while it pushes, then back.</summary>
        sealed class Tug { public GameObject Go; public Vector3 From; public float Back = -1; }
        readonly Dictionary<int, Tug> _tugs = new Dictionary<int, Tug>();
        const float TugHome = 8f;
        readonly Dictionary<int, Stand> _apron = new Dictionary<int, Stand>();
        float _apronIn, _apronSynced = -10;
        /// <summary>The taxiway beyond the stands: clear of the longest tail and half the widest wing.</summary>
        float TaxiX => Width * 0.5f + 5.6f + 14.2f * ApronScale + 6.3f * ApronScale + 3f;
        const float TurnIn = 3f, RollIn = 8f, PushBack = 10f, TurnOut = 3f;
        // The taxiway runs one way, toward -z: in from beyond the +z end of the hall, out past the -z end, so none appears or
        // vanishes in view; the time on it grows with the distance.
        float InDist(Stand p) { return Length * 0.5f + 90 - p.Z; }
        float InTime(Stand p) { return InDist(p) / 12f; }
        float OutDist(Stand p) { return p.Z + Length * 0.5f + 90; }
        float OutTime(Stand p) { return Mathf.Sqrt(OutDist(p) / 0.8f); }      // pulling away at 1.6 m/s² (0.8 t²)

        void ClearApron()
        {
            foreach (var p in _apron.Values) Drop(p.Go);
            foreach (var t in _tugs.Values) Drop(t.Go);
            _apron.Clear(); _tugs.Clear(); _apronIn = 0;
        }

        static void Drop(GameObject go)
        {
            if (go == null) return;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Destroy(mf.sharedMesh);      // the tug too, if it is still pushing
            Destroy(go);
        }

        /// <summary>
        /// The flights standing at the terminal take the stands beyond the glass in their own colours: they taxi along the
        /// apron, turn in and roll up to the bridge; when the flight leaves they push back, turn and taxi away. A stand stays
        /// taken until its aircraft is clear. Opening the hall shows those already parked where they are.
        /// </summary>
        void SyncApron()
        {
            bool instant = Time.time - _apronSynced > 1.5f;
            _apronSynced = Time.time;
            var parked = _sim.State.flights.Where(f => f.phase == Phase.Deplane || f.phase == Phase.Service || f.phase == Phase.Board).ToList();
            foreach (var kv in _apron.ToList())
                if (kv.Value.Gone < 0 && !kv.Value.Done && !parked.Exists(f => f.id == kv.Key))
                {
                    // Gone while still on its way in: it finishes rolling up to the bridge first, then pushes back.
                    if (instant || kv.Value.Age <= 0) { Drop(kv.Value.Go); _apron.Remove(kv.Key); }
                    else kv.Value.Done = true;
                }
            foreach (var f in parked.OrderBy(x => x.phase).ThenBy(x => x.stand))      // the latest in first: those boarding are about to go
            {
                if (_apron.ContainsKey(f.id)) continue;
                int slot = -1;
                for (int i = 0; i < _apronSlots.Count && slot < 0; i++) if (!_apron.Values.Any(p => p.Slot == i)) slot = i;
                if (slot < 0) break;
                var r = Content.Routes[f.route];
                var go = Models.PlaneObject(r.Class, r.Airline, "At the gate " + f.id, transform);
                go.transform.localScale = Vector3.one * ApronScale;
                var st = new Stand { Go = go, Slot = slot, Len = Models.Length(r.Class), X = Width * 0.5f + 5.6f + Models.Length(r.Class) * ApronScale * 0.5f, Z = _apronSlots[slot], Age = instant ? 999 : 0 };
                _apron[f.id] = st;
                PlaceStand(st);
            }
        }

        Vector3 TugPark(int slot) { return new Vector3(Width * 0.5f + 4.6f, 0, _apronSlots[slot] + 7.5f); }

        Tug TugAt(int slot)
        {
            Tug t;
            if (!_tugs.TryGetValue(slot, out t)) _tugs[slot] = t = new Tug();
            if (t.Go == null)
            {
                t.Go = Gse.Build(Service.Pushback).ToObject("Tug " + slot, transform);
                t.Go.transform.localPosition = TugPark(slot); t.Go.transform.localScale = Vector3.one * ApronScale; t.Back = -1;
            }
            return t;
        }

        void MoveApron(float dt)
        {
            for (int i = 0; i < _apronSlots.Count; i++)
            {
                var t = TugAt(i);
                if (t.Back < 0) continue;
                // Unhooked on the taxiway, it drives back to its place by the bridge (Gse vehicles face -Z, the way they drive).
                t.Back += dt;
                float u = Mathf.Clamp01(t.Back / TugHome);
                var home = TugPark(i);
                t.Go.transform.localPosition = Vector3.Lerp(t.From, home, u * u * (3 - 2 * u));
                var dir = home - t.From; dir.y = 0;
                t.Go.transform.localRotation = u >= 1 || dir.sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(-dir);
                if (u >= 1) t.Back = -1;
            }
            foreach (var kv in _apron.ToList())
            {
                var p = kv.Value;
                // One taxiway: an aircraft waits to push back, or to come in, until the way past its stand is clear.
                bool waiting = (p.Gone >= 0 ? p.Gone : p.Age) <= 0 && Blocked(p);
                if (!waiting) { if (p.Gone >= 0) p.Gone += dt; else p.Age += dt; }
                if (p.Done && p.Gone < 0 && p.Age >= InTime(p) + TurnIn + RollIn) p.Gone = 0;
                if (p.Gone > PushBack + TurnOut + OutTime(p)) { Drop(p.Go); _apron.Remove(kv.Key); continue; }
                PlaceStand(p);
            }
        }

        /// <summary>Whether another aircraft is on (or about to use) the stretch of taxiway this one needs.</summary>
        bool Blocked(Stand p)
        {
            foreach (var q in _apron.Values)
            {
                if (q == p || q.Go == null) continue;
                float qz = q.Go.transform.localPosition.z;
                if (p.Gone >= 0)
                {
                    if (q.Gone > 0 && q.Z > p.Z && qz > p.Z - 45) return true;                                  // one leaving from further up has yet to pass
                    if (q.Gone < 0 && q.Age > 0 && q.Age < InTime(q) + TurnIn && qz > p.Z - 45) return true;         // one coming in has yet to pass or turn
                }
                else if (q.Gone > 0 && qz > p.Z - 25) return true;                                                // one leaving is still above where this one turns in
            }
            return false;
        }

        /// <summary>Where an aircraft is along its way in or out (yaw 0: nose to +z; -90: nose to the glass).</summary>
        void PlaceStand(Stand p)
        {
            if (p.Go == null) return;
            float x = p.X, z = p.Z, yaw = -90, tx = TaxiX;
            float Ease(float u) { u = Mathf.Clamp01(u); return u * u * (3 - 2 * u); }
            if (p.Gone >= 0)
            {
                float g = p.Gone;
                if (g < PushBack) x = Mathf.Lerp(p.X, tx, Ease(g / PushBack));
                else if ((g -= PushBack) < TurnOut) { x = tx; yaw = Mathf.Lerp(-90, -180, Ease(g / TurnOut)); }
                else { g -= TurnOut; x = tx; yaw = 180; z = p.Z - Mathf.Min(OutDist(p), 0.8f * g * g); }
            }
            else
            {
                float a = p.Age;
                // In from beyond the far end, slowing to the turn (twice the average speed at first, 12 m/s on average).
                float taxi = InTime(p);
                if (a < taxi) { float u = Mathf.Clamp01(a / taxi); x = tx; z = p.Z + InDist(p) * (1 - u) * (1 - u); yaw = 180; }
                else if ((a -= taxi) < TurnIn) { x = tx; yaw = Mathf.Lerp(180, 270, Ease(a / TurnIn)); }
                else { a -= TurnIn; x = Mathf.Lerp(tx, p.X, 1 - (1 - Mathf.Clamp01(a / RollIn)) * (1 - Mathf.Clamp01(a / RollIn))); }
            }
            p.Go.transform.localPosition = new Vector3(x, 0, z);
            p.Go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            p.Go.SetActive(p.Gone >= 0 || p.Age > 0);      // still holding off the end of the taxiway
            if (p.Gone < 0) return;
            // The tug is under the nose while it pushes and turns the aircraft, then lets it go.
            var tug = TugAt(p.Slot);
            bool hooked = tug.Go.transform.parent == p.Go.transform;
            if (p.Gone < PushBack + TurnOut && !hooked)
            {
                tug.Go.transform.SetParent(p.Go.transform, false);
                tug.Go.transform.localPosition = new Vector3(0, 0, p.Len * 0.5f + 1.9f);
                tug.Go.transform.localRotation = Quaternion.identity; tug.Go.transform.localScale = Vector3.one; tug.Back = -1;
            }
            else if (p.Gone >= PushBack + TurnOut && hooked)
            {
                tug.Go.transform.SetParent(transform, true);
                tug.From = tug.Go.transform.localPosition; tug.From.y = 0; tug.Back = 0;
            }
        }

        void Update()
        {
            var s = _sim;
            string key = s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.FloorCols + "|" + FloorKey(s);
            // Rebuilt only while it is being looked at: upgrades made from the airfield cost nothing here until the hall is opened.
            if (key != _built && (CameraRig.Inside || _static == null)) { _built = key; Rebuild(); }
            if (!CameraRig.Inside || _walk == null || _walk[0][0] == null) return;
            if ((_apronIn -= Time.deltaTime) <= 0) { _apronIn = 0.5f; SyncApron(); }
            MoveApron(Time.deltaTime);
            MoveTraffic(Time.deltaTime);

            // The crowd grows with the passengers the airport moves, up to what a phone draws comfortably.
            int want = Mathf.Clamp((int)(s.PassengerDemand * 28) + 50, 50, 340);
            float dt = Time.deltaTime;
            if ((_spawn -= dt) <= 0 && _agents.Count < want) { _spawn = 0.02f; Spawn(_agents.Count % 3 == 0, want); }

            foreach (var b in _batches) b.Clear();
            _shadows.Clear();
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : null;
            if (cam != null) GeometryUtility.CalculateFrustumPlanes(cam, _planes);
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
                        if (a.Wait > 0 && a.At[a.Step - 1] != null && Thought != null) Thought(a, a.At[a.Step - 1]);
                        if (a.Step >= a.Path.Length) { _agents.RemoveAt(i); continue; }
                    }
                    else { a.Pos += to / d * Mathf.Min(d, a.Speed * dt); a.Target = to / d; walking = true; }
                }
                var rot = a.Target.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-a.Target) : Quaternion.identity;
                int frame = walking ? (int)(a.Phase + Time.time * 7) & 3 : 4;
                // Only those in view are drawn (their shadows from a little further out, so none pops at the screen's edge).
                var at = Origin + a.Pos;
                float outside = cam != null ? Outside(at + Vector3.up * 0.9f) : 0;
                if (outside > 5f) continue;
                var m = Matrix4x4.TRS(at, rot, Vector3.one * 1.75f);
                _shadows.Add(m);
                if (outside <= 1.4f) _batches[a.Kind * 5 + frame].Add(m);
            }
            // The staff at their posts: desks, lanes, gates and counters, facing the people they serve.
            foreach (var st in _staff)
            {
                var at = Origin + st.at;
                float outside = cam != null ? Outside(at + Vector3.up * 0.9f) : 0;
                if (outside > 5f) continue;
                var m = Matrix4x4.TRS(at, Quaternion.LookRotation(-(Quaternion.Euler(0, st.yaw, 0) * Vector3.left)), Vector3.one * 1.75f);
                _shadows.Add(m);
                if (outside <= 1.4f) _batches[st.kind * 5 + 4].Add(m);
            }
            DrawBags(cam);
            bool far = CameraRig.Instance != null && CameraRig.Instance.Distance > FarCrowd;
            for (int k = 0; k < 12; k++)
                for (int f = 0; f < 5; f++)
                {
                    var list = _batches[k * 5 + f];
                    var mesh = f < 4 ? (far ? _walkFar : _walk)[k][f] : (far ? _idleFar : _idle)[k];
                    if (list.Count > 0 && mesh != null) Graphics.DrawMeshInstanced(mesh, 0, Materials.Kit, list, null, UnityEngine.Rendering.ShadowCastingMode.Off, true);
                }
            if (_shadows.Count > 0) Graphics.DrawMeshInstanced(_shadow, 0, Materials.Kit, _shadows, null, UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly, false);
        }

        /// <summary>Cars on the drop-off road drive along their lane and come round again from the far end.</summary>
        void MoveTraffic(float dt)
        {
            float span = _roadZ1 - _roadZ0;
            if (span <= 0) return;
            foreach (var (car, speed) in _traffic)
            {
                if (car == null) continue;
                var p = car.localPosition;
                p.z += speed * dt;
                if (p.z > _roadZ1) p.z -= span; else if (p.z < _roadZ0) p.z += span;
                car.localPosition = p;
            }
        }

        /// <summary>Bags riding round each carousel: up one run of belt, across the end, down the other.</summary>
        void DrawBags(Camera cam)
        {
            if (_carousels.Count == 0) return;
            if (_bagMeshes == null)
            {
                Color[] tints = { Color.white, new Color(1.6f, 3.2f, 4.6f), new Color(4.6f, 1.4f, 1.2f), new Color(4.2f, 3.4f, 1.6f) };      // the case is near black: black, blue, red, tan
                _bagMeshes = new Mesh[tints.Length];
                for (int i = 0; i < tints.Length; i++) { var mb = new MeshBuilder(); mb.Tall("luggage", Vector3.zero, 90, 0.5f, tints[i]); _bagMeshes[i] = mb.ToMesh("Carousel bag " + i); }
            }
            foreach (var l in _bagLists) l.Clear();
            const float hx = 0.85f, hz = 3.4f, loop = 4 * hx + 4 * hz;
            float t = Time.time * 0.45f;
            for (int c = 0; c < _carousels.Count; c++)
            {
                var centre = Origin + _carousels[c];
                if (cam != null && Outside(centre + Vector3.up) > 5f) continue;
                int bags = 4 + (c * 5 + 3) % 4;
                for (int b = 0; b < bags; b++)
                {
                    // Spaced unevenly, as they come off the plane.
                    float d = Mathf.Repeat(t + loop * (b + 0.37f * Mathf.Sin(b * 2.3f + c)) / bags, loop);
                    Vector3 p, dir;
                    if (d < 2 * hz) { p = new Vector3(-hx, 0, -hz + d); dir = Vector3.forward; }
                    else if ((d -= 2 * hz) < 2 * hx) { p = new Vector3(-hx + d, 0, hz); dir = Vector3.right; }
                    else if ((d -= 2 * hx) < 2 * hz) { p = new Vector3(hx, 0, hz - d); dir = Vector3.back; }
                    else { d -= 2 * hz; p = new Vector3(hx - d, 0, -hz); dir = Vector3.left; }
                    var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, (b * 47) % 60 - 30, 0);
                    _bagLists[(b + c) % _bagLists.Length].Add(Matrix4x4.TRS(centre + p + Vector3.up * 0.5f, rot, Vector3.one));
                }
            }
            for (int i = 0; i < _bagLists.Length; i++)
                if (_bagLists[i].Count > 0) Graphics.DrawMeshInstanced(_bagMeshes[i], 0, Materials.World, _bagLists[i], null, UnityEngine.Rendering.ShadowCastingMode.On, true);
        }

        /// <summary>How far outside the camera's view a point is, in metres (0 or less: in view).</summary>
        float Outside(Vector3 p)
        {
            float worst = 0;
            for (int i = 0; i < 4; i++) { float d = -_planes[i].GetDistanceToPoint(p); if (d > worst) worst = d; }      // the four sides: near and far never matter here
            return worst;
        }

        void Spawn(bool arriving, int want = 100)
        {
            if (_doors.Count == 0 || _gates.Count == 0 || _lanes.Count == 0 || _desks.Count == 0) return;
            Vector3 R(List<Vector3> l) { return l[Random.Range(0, l.Count)] + new Vector3(Random.Range(-0.4f, 0.4f), 0, Random.Range(-0.4f, 0.4f)); }
            // Where they have business, in order; between those they keep to the aisles (see Walk).
            var goals = new List<Vector3>(); var what = new List<string>(); var pass = new List<bool>();
            void Go(Vector3 at, string id = null, bool through = false) { goals.Add(at); what.Add(id); pass.Add(through); }
            void Visit(List<Vector3> places, List<string> ids) { int k = Random.Range(0, places.Count); Go(places[k] + new Vector3(Random.Range(-0.4f, 0.4f), 0, Random.Range(-0.4f, 0.4f)), ids[k]); }
            if (!arriving)
            {
                Go(R(_doors)); Go(R(_desks));
                var lane = R(_lanes); Go(lane + Vector3.left * 3.5f); Go(lane + Vector3.right * 3f);
                if (_shops.Count > 0 && Random.value < 0.7f) Visit(_shops, _shopIds);
                if (_seats.Count > 0 && Random.value < 0.6f) Visit(_seats, _seatIds);
                Go(R(_gates));
            }
            else
            {
                Go(R(_gates));
                if (_shops.Count > 0 && Random.value < 0.3f) Visit(_shops, _shopIds);
                // Out by the arrivals passage at the end of the security wall, to a carousel for the bag, then the doors.
                Go(_arrivalsOut + new Vector3(2.6f, 0, Random.Range(-0.8f, 0.8f)));
                Go(_arrivalsOut + new Vector3(-3.2f, 0, Random.Range(-0.8f, 0.8f)), null, true);
                if (_reclaim.Count > 0 && Random.value < 0.8f)
                {
                    // Round the belts, never over them: in along the aisle past their far ends, out by their near ends and the sign.
                    var bag = R(_reclaim);
                    Go(new Vector3(bag.x, 0, _arrivalsOut.z + Random.Range(-0.6f, 0.6f)), null, true);
                    Go(bag);
                    Go(new Vector3(bag.x, 0, _reclaimOut.z + Random.Range(-0.5f, 0.5f)), null, true);
                    Go(_reclaimOut + new Vector3(0, 0, Random.Range(-0.6f, 0.6f)), null, true);
                }
                Go(R(_doors));
            }
            var path = new List<Vector3> { goals[0] };
            var stop = new List<bool> { true }; var at = new List<string> { null };
            for (int i = 1; i < goals.Count; i++)
            {
                Walk(goals[i - 1], goals[i], path);
                while (stop.Count < path.Count - 1) { stop.Add(false); at.Add(null); }
                stop.Add(!pass[i]); at.Add(what[i]);
            }
            var a = new Agent { Seed = Random.Range(1, 1 << 30), Arriving = arriving, Kind = Random.Range(0, 12), Path = path.ToArray(), Stop = stop.ToArray(), At = at.ToArray(), Speed = Random.Range(1.6f, 2.6f), Phase = Random.Range(0, 4) };
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
        /// <summary>Which of an item's looks stands at a cell.</summary>
        public static int Variant(int x, int z) { return (x * 7 + z * 13) % ItemVariants; }
        static readonly Dictionary<string, Mesh> _itemMeshes = new Dictionary<string, Mesh>();
        readonly List<(Mesh mesh, Vector3 at, float yaw)> _items = new List<(Mesh, Vector3, float)>();

        /// <summary>The mesh of an item as it will stand (for the placing preview, too).</summary>
        public static Mesh ItemMesh(AmenityDef d, int level, int variant)
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
                _items.Add((ItemMesh(d, p.level, Variant(p.x, p.z)), c, p.rot * 90f));
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
                if (d.Gives == Want.Seating || d.Gives == Want.Lounge) { _seats.Add(c); _seatIds.Add(d.Id); } else { _shops.Add(stop); _shopIds.Add(d.Id); }
                // Whoever works there, where they stand in the item's own space (x toward the back, z across) and the way they face.
                float ihx = d.W * Sim.Cell * 0.5f, ihz = d.D * Sim.Cell * 0.5f;
                var turn = Quaternion.Euler(0, p.rot * 90, 0);
                void Works(float lx, float lz, float faceYaw) { _staff.Add((c + turn * new Vector3(lx, 0, lz), p.rot * 90 + faceYaw, StaffKinds[(p.x * 3 + p.z + _staff.Count) % StaffKinds.Length])); }
                switch (d.Id)
                {
                    case "cafe": Works(ihx - 1.2f, 0.7f, 0); break;
                    case "noodle": Works(ihx - 1.35f, -0.4f, 0); Works(ihx - 1.35f, 0.9f, 0); break;
                    case "restaurant": Works(-ihx + 1.0f, ihz - 0.75f, 0); break;
                    case "newsstand": Works(-ihx + 1.3f, -0.42f, 0); break;
                    case "gifts": Works(0.2f, -ihz + 0.15f + 0.9f, 90); break;
                    case "fashion": Works(ihx - 1.25f, -1.45f, 0); break;
                    case "dutyfree": Works(-ihx + 1.45f, -2.45f, 0); Works(-ihx + 1.45f, 2.45f, 0); break;
                    case "lounge": Works(-ihx + 1.35f, ihz - 1.5f, 0); Works(0.8f, ihz - 0.82f, 270); break;
                    case "infodesk": Works(0.85f, -0.45f, 0); break;
                }
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
