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

        sealed class Agent { public int Kind; public Vector3 Pos, Target; public int Step; public float Wait, Speed, Phase; public bool Arriving; public Vector3[] Path; }
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
            Length = Mathf.Clamp(46 + (checkin + security + gates) * 3.2f + shops * 2f, 50, 190);
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
            mb.Box(new Vector3(0, 2.6f, L * 0.5f + 0.3f), new Vector3(W + 1.2f, 5.2f, 0.6f), wall, Palette.White);
            mb.Box(new Vector3(W * 0.5f + 0.3f, 2.6f, 0), new Vector3(0.6f, 5.2f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(0, 0.5f, -L * 0.5f - 0.3f), new Vector3(W + 1.2f, 1.0f, 0.6f), wall, Palette.White);
            mb.Box(new Vector3(-W * 0.5f - 0.3f, 0.5f, 0), new Vector3(0.6f, 1.0f, L + 1.2f), wall, Palette.White);
            mb.Box(new Vector3(0, 4.9f, L * 0.5f - 0.05f), new Vector3(W, 0.5f, 0.1f), trim);
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

            // ---- the concourse: shop units along a walkway, cafes with tables, plants, information desks
            int units = Mathf.Clamp(shops, 0, 18);
            Color[] brand = { Palette.Hex(0xD9432F), Palette.Hex(0x2F8F5B), Palette.Hex(0xE2A521), Palette.Hex(0x6B4FBB), Palette.Hex(0x1E8FB0), Palette.Hex(0xC2417C) };
            float shx = x0 + W * 0.56f;
            for (int i = 0; i < units; i++)
            {
                float uz = z0 + L * (i + 0.5f) / Mathf.Max(1, units);
                float uw = Mathf.Min(8f, L / Mathf.Max(1, units) - 1.2f);
                var c = brand[i % brand.Length];
                bool cafe = i % 3 == 1;
                mb.Box(new Vector3(shx + 3.4f, 1.6f, uz), new Vector3(0.3f, 3.2f, uw), Palette.Hex(0xEDEAE2));                                     // back wall
                foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(shx + 1.2f, 1.2f, uz + e * uw * 0.5f), new Vector3(4.6f, 2.4f, 0.2f), Palette.Hex(0xEDEAE2));
                mb.Box(new Vector3(shx - 1.1f, 2.7f, uz), new Vector3(0.3f, 0.7f, uw + 0.2f), c);                                                    // fascia in the shop's colour
                mb.Box(new Vector3(shx - 1.28f, 2.7f, uz), new Vector3(0.05f, 0.3f, uw * 0.5f), Palette.White);
                if (cafe)
                {
                    mb.Box(new Vector3(shx + 2.2f, 0.55f, uz), new Vector3(0.9f, 1.1f, uw * 0.7f), Palette.Hex(0x6B4A2E), Palette.Hex(0xD9C9A8));     // counter
                    for (int k = 0; k < 3; k++)
                    {
                        var tp = new Vector3(shx - 3.2f - (k % 2) * 1.8f, 0, uz + (k - 1) * uw * 0.3f);
                        mb.Cylinder(tp, tp + Vector3.up * 0.75f, 0.06f, 5, Palette.Steel);
                        mb.Cylinder(tp + Vector3.up * 0.75f, tp + Vector3.up * 0.8f, 0.55f, 10, Palette.Hex(0xD9C9A8));
                        _seats.Add(tp + new Vector3(0.8f, 0, 0));
                    }
                }
                else
                    for (int k = 0; k < 3; k++)
                        mb.Box(new Vector3(shx + 1.6f - k * 1.3f, 0.7f, uz + (k - 1) * uw * 0.22f), new Vector3(0.6f, 1.4f, uw * 0.5f), Palette.Hex(0xF4F1EA), c);   // shelving
                _shops.Add(new Vector3(shx - 2.2f, 0, uz));
            }
            // Planters down the walkway.
            for (float z = z0 + 6; z < L * 0.5f - 4; z += 12)
            {
                var pp = new Vector3(shx - 6.5f, 0, z);
                mb.Box(pp + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 1.4f), Palette.Hex(0xC9CED3), Palette.Hex(0x4A3A2A));
                mb.Cylinder(pp + Vector3.up * 0.8f, pp + Vector3.up * 2.6f, 0.12f, 0.08f, 5, Palette.Hex(0x7A5A3A));
                for (int k = 0; k < 6; k++)
                {
                    mb.Push().Translate(pp + Vector3.up * 2.6f).Rotate(Quaternion.Euler(0, k * 60, 38));
                    mb.Box(new Vector3(0.8f, 0, 0), new Vector3(1.6f, 0.05f, 0.34f), Palette.Hex(0x3E8E4A));
                    mb.Pop();
                }
            }
            for (int i = 0; i < Mathf.Clamp(info, 0, 6); i++)
            {
                var ip = new Vector3(x0 + W * 0.46f, 0, z0 + L * (i + 0.5f) / Mathf.Clamp(info, 1, 6));
                mb.Cylinder(ip, ip + Vector3.up * 1.05f, 1.2f, 1.2f, 12, Palette.White);
                mb.Cylinder(ip + Vector3.up * 1.05f, ip + Vector3.up * 1.12f, 1.3f, 1.3f, 12, trim);
                mb.Cylinder(ip + Vector3.up * 1.1f, ip + Vector3.up * 3f, 0.08f, 5, Palette.Steel);
                mb.Box(ip + Vector3.up * 3.2f, new Vector3(1.2f, 0.7f, 0.15f), trim);
            }

            // ---- gate lounges along the glass: rows of seats, a desk and a gate number each
            int gateCount = Mathf.Clamp(gates, 1, 24);
            float gx = x0 + W * 0.86f;
            for (int i = 0; i < gateCount; i++)
            {
                float gz = z0 + L * (i + 0.5f) / gateCount, span = Mathf.Min(7f, L / gateCount - 1.2f);
                mb.Box(new Vector3(W * 0.5f - 1.6f, 0.55f, gz + span * 0.4f), new Vector3(0.9f, 1.1f, 1.6f), Palette.White, trim);                  // gate desk
                mb.Box(new Vector3(W * 0.5f - 0.5f, 3.3f, gz), new Vector3(0.2f, 0.8f, 1.6f), Palette.Hex(0x12161B));
                mb.Box(new Vector3(W * 0.5f - 0.62f, 3.3f, gz), new Vector3(0.05f, 0.5f, 1.0f), Palette.Hex(0xF2C230));
                int rows = 2 + Mathf.Min(3, seating / 2);
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
            if (lounge > 0)
            {
                // The lounge: a carpeted corner with sofas and a bar.
                var lp = new Vector3(gx, 0, L * 0.5f - 7);
                g.Rect("Concrete", lp.x, lp.z, 15, 12, 0.02f, 0, new Color(0.95f, 0.65f, 1.2f));
                for (int k = 0; k < 4; k++) mb.Box(lp + new Vector3(-4.5f + k * 3f, 0.4f, -2), new Vector3(2.2f, 0.8f, 1.0f), Palette.Hex(0x6B4FBB), Palette.Hex(0x8A70D6));
                mb.Box(lp + new Vector3(0, 0.6f, 4f), new Vector3(9f, 1.2f, 1.0f), Palette.Hex(0x3A2E26), Palette.Hex(0xD9C9A8));
            }

            _static = mb.ToObject("Static", transform);
            g.Build(_static.transform);
        }

        void Update()
        {
            var s = _sim;
            string key = s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.Level("shops") + "|" + s.Level("seating") + "|" + s.Level("info") + "|" + s.Level("lounge");
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
            var a = new Agent { Kind = Random.Range(0, 12), Path = path.ToArray(), Speed = Random.Range(1.6f, 2.6f), Phase = Random.Range(0, 4) };
            // Start part-way along, so the hall is full the moment you look in.
            int start = _agents.Count < want * 0.8f ? Random.Range(0, path.Count - 1) : 0;
            a.Pos = path[start]; a.Step = start + 1 < path.Count ? start + 1 : start; a.Target = Vector3.right;
            _agents.Add(a);
        }
    }
}
