using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The airport you can see: ground, runway, taxiways, stands, terminal, service depots and tower, rebuilt from the
    /// simulation's levels whenever they change, plus one <see cref="PlaneView"/> per flight.
    /// </summary>
    public sealed class AirportView : MonoBehaviour
    {
        public static AirportView Instance { get; private set; }

        readonly Dictionary<int, PlaneView> _planes = new Dictionary<int, PlaneView>();
        GameObject _static;
        Transform _kitRoot;
        /// <summary>Where passengers go in and out of the terminal on the apron side, the landside road, and each depot's yard.</summary>
        public Vector3 TerminalDoor { get; private set; }
        public float RoadX { get; private set; }
        public Vector3 TowerTop { get; private set; }
        public readonly Dictionary<string, Vector3> DepotAt = new Dictionary<string, Vector3>();
        string _built = "";
        Sim _sim;

        public static AirportView Create(Transform parent)
        {
            var go = new GameObject("Airport");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<AirportView>();
            Instance = v;
            v._sim = GameRoot.Sim;
            v._sim.FlightChanged += v.OnFlight;
            v.Rebuild();
            Life.Create(go.transform);
            WeatherFx.Create(go.transform);
            Interior.Create(go.transform);
            foreach (var f in v._sim.State.flights) v.OnFlight(f);
            return v;
        }

        void OnDestroy()
        {
            if (_sim != null) _sim.FlightChanged -= OnFlight;
            if (Instance == this) Instance = null;
        }

        public IEnumerable<PlaneView> Planes { get { return _planes.Values; } }

        void OnFlight(FlightState f)
        {
            PlaneView p;
            if (!_planes.TryGetValue(f.id, out p))
            {
                if (f.phase == Phase.Gone) return;
                p = PlaneView.Create(transform, f);
                _planes[f.id] = p;
            }
            p.Enter();
            if (f.phase == Phase.Gone) { _planes.Remove(f.id); Destroy(p.gameObject, 0.1f); }
        }

        GameObject _closed;
        readonly HashSet<int> _bridged = new HashSet<int>();
        /// <summary>Whether this stand has a boarding bridge (nobody walks across the apron to it).</summary>
        public bool HasBridge(int stand) { return _bridged.Contains(stand); }

        /// <summary>
        /// A passenger boarding bridge from a rotunda at the terminal wall (x1, z1) to its cab at (x0, z0): a glazed
        /// tunnel in two telescoping sections, the cab with its bellows toward the aircraft (-z), and the wheeled
        /// drive column under the outer end.
        /// </summary>
        static void Bridge(MeshBuilder mb, float x1, float z1, float x0, float z0, float cab)
        {
            Color skin = Palette.Hex(0xE3E6E9), trim = Palette.Hex(0x9AA3AB), glass = Palette.Hex(0x7FB6CF), dark = Palette.Hex(0x2B3138);
            float y = 2.75f;
            float sm = mb.Smoothness;
            var from = new Vector3(x1 - 1.0f, 0, z1); var to = new Vector3(x0, 0, z0);
            float len = Vector3.Distance(from, to), yaw = Mathf.Atan2(-(to.z - from.z), to.x - from.x) * Mathf.Rad2Deg;
            // Rotunda at the terminal, with the link into the wall.
            mb.Box(new Vector3(x1 - 0.3f, y, z1), new Vector3(1.0f, 1.7f, 1.5f), skin);
            mb.Cylinder(from + Vector3.up * (y - 0.9f), from + Vector3.up * (y + 0.9f), 1.25f, 1.25f, 14, skin);
            mb.Cylinder(from + Vector3.up * (y + 0.9f), from + Vector3.up * (y + 1.0f), 1.35f, 1.35f, 14, trim);
            mb.Cylinder(from, from + Vector3.up * (y - 0.9f), 0.28f, 0.28f, 8, trim);
            // Two tunnel sections along the line to the cab, the outer one slimmer so it slides inside.
            mb.Push().Translate(from).RotateY(yaw);
            void Tunnel(float a, float b, float h, float w)
            {
                mb.Box(new Vector3((a + b) * 0.5f, y, 0), new Vector3(b - a, h, w), skin);
                mb.Box(new Vector3((a + b) * 0.5f, y + h * 0.5f + 0.04f, 0), new Vector3(b - a, 0.08f, w + 0.1f), trim);
                mb.Box(new Vector3((a + b) * 0.5f, y - h * 0.5f - 0.04f, 0), new Vector3(b - a, 0.08f, w + 0.1f), trim);
                mb.Mat(0.92f);
                foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3((a + b) * 0.5f, y + 0.12f, e * (w * 0.5f + 0.005f)), new Vector3(b - a - 0.5f, h * 0.42f, 0.03f), glass);
                mb.Mat(sm);
                for (float x = a + 0.25f; x < b; x += 1.4f) foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(x, y + 0.12f, e * (w * 0.5f + 0.02f)), new Vector3(0.07f, h * 0.44f, 0.03f), trim);
            }
            Tunnel(1.0f, len * 0.5f, 1.7f, 1.5f);
            Tunnel(len * 0.5f - 0.4f, len - cab * 0.7f, 1.5f, 1.3f);
            // Drive column and bogie under the outer section.
            float dx = len - cab - 0.6f;
            mb.Box(new Vector3(dx, (y - 0.75f) * 0.5f, 0), new Vector3(0.34f, y - 0.75f, 0.34f), trim);
            mb.Box(new Vector3(dx, 0.42f, 0), new Vector3(0.5f, 0.24f, 1.7f), dark);
            mb.Mat(0.1f);
            foreach (float e in new[] { -1f, 1f }) mb.Cylinder(new Vector3(dx - 0.14f, 0.3f, e * 0.7f), new Vector3(dx + 0.14f, 0.3f, e * 0.7f), 0.3f, 10, Palette.Tyre);
            mb.Mat(sm);
            mb.Pop();
            // The cab: a drum, a dark bellows canopy toward the aircraft, a yellow bumper under it.
            mb.Cylinder(to + Vector3.up * (y - 0.85f), to + Vector3.up * (y + 0.85f), cab, cab, 12, skin);
            mb.Cylinder(to + Vector3.up * (y + 0.85f), to + Vector3.up * (y + 0.95f), cab + 0.08f, cab + 0.08f, 12, trim);
            mb.Box(new Vector3(x0, y, z0 - cab - 0.3f), new Vector3(1.3f, 1.6f, 0.75f), dark);
            mb.Box(new Vector3(x0, y - 0.86f, z0 - cab - 0.42f), new Vector3(1.4f, 0.12f, 0.6f), Palette.PaintYellow);
            // Gate plate over the rotunda.
            mb.Box(new Vector3(x1 - 2.3f, y + 1.35f, z1), new Vector3(0.1f, 0.6f, 1.0f), dark);
            mb.Glow = true; mb.Box(new Vector3(x1 - 2.36f, y + 1.35f, z1), new Vector3(0.02f, 0.4f, 0.7f), Palette.PaintYellow); mb.Glow = false;
        }

        void Update()
        {
            // The levels that shape the airport, as one key: rebuild only when it changes.
            var s = _sim;
            string key = s.Level("runway") + "|" + s.Level("stands") + "|" + s.Level("heavy") + "|" + s.Level("taxiways") + "|" + s.Level("tower") + "|" + s.Level("fuel") + "|" + s.Level("baggage") + "|"
                 + s.Level("catering") + "|" + s.Level("cleaning") + "|" + s.Level("pushback") + "|" + s.Level("deice") + "|" + s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.Level("shops") + "|" + s.Level("lounge") + "|" + s.Level("parking") + "|" + s.Level("hangar") + "|" + s.Level("cargo") + "|" + s.Level("ils") + "|" + s.Stars + "|" + s.State.map;
            if (key != _built) Rebuild();
            if (_closed != null && _closed.activeSelf != s.RunwayClosed) _closed.SetActive(s.RunwayClosed);
            _built = key;
        }

        void Rebuild()
        {
            _bridged.Clear();
            if (_static != null) Destroy(_static);
            var s = _sim;
            var mb = new MeshBuilder { Smoothness = 0.08f };
            float half = Layout.RunwayHalf(s), w = Layout.RunwayWidth(s);
            int rl = s.Level("runway");
            float termX = Layout.TerminalX(s);

            // Ground: grass, with a darker mown strip round the runway.
            // The land this airport sits on: meadow, island sand, desert, alpine green, snow, or the city's edge.
            string map = s.State.map;
            Color ground = map == "coral" ? Palette.Hex(0xD9C58C) : map == "dune" ? Palette.Hex(0xCFA868) : map == "polar" ? Palette.Hex(0xE8EEF2) : map == "alpine" ? Palette.Hex(0x5E8F52) : map == "metro" ? Palette.Hex(0x7D9468) : Palette.Grass;
            Color stripe = MeshBuilder.Darken(ground, 0.94f), tree = map == "dune" ? Palette.Hex(0x8A9A4A) : map == "polar" ? Palette.Hex(0x3E5A48) : Palette.Hex(0x47683A);
            var g = new Ground();
            bool green = map == "meadow" || map == "alpine" || map == "metro";
            if (green) g.Rect("Grass", 20, 0, 1400, 1400, -0.2f); else if (map != "coral") mb.Box(new Vector3(20, -0.3f, 0), new Vector3(1400, 0.5f, 1400), ground);
            if (map == "coral")
            {
                // An island: turquoise shallows round the sand on every side, deeper blue beyond.
                mb.Smoothness = 0.85f;
                mb.Box(new Vector3(20, -0.32f, 0), new Vector3(2400, 0.5f, 2400), Palette.Hex(0x1E86A8));
                mb.Box(new Vector3(40, -0.3f, 0), new Vector3(560, 0.5f, 560), Palette.Hex(0x4CC4C9));
                mb.Smoothness = 0.08f;
                mb.Box(new Vector3(40, -0.27f, 0), new Vector3(470, 0.5f, 470), ground);
            }
            if (green) g.Rect("Grass", Layout.RunwayX, 0, w + 16, half * 2 + 34, -0.03f, 0, new Color(0.82f, 0.86f, 0.78f)); else mb.Box(new Vector3(Layout.RunwayX, -0.04f, 0), new Vector3(w + 14, 0.04f, half * 2 + 30), MeshBuilder.Darken(ground, 0.86f));
            // (No mowing stripes across the whole country: only the runway strip is kept short.)

            // Runway: grass strip, gravel, asphalt, then concrete with full markings.
            Color surface = rl <= 1 ? Palette.GrassWorn : rl <= 2 ? Palette.Gravel : rl <= 9 ? Palette.Asphalt : Palette.AsphaltPale;
            if (rl >= 3) { g.Rect("Asphalt", Layout.RunwayX, 0, w, half * 2, 0.03f); g.Rect("Tarmac", Layout.RunwayX, 0, w + 3, half * 2 + 6, 0.015f, 0, new Color(1.25f, 1.25f, 1.25f)); }
            else mb.Box(new Vector3(Layout.RunwayX, 0.0f, 0), new Vector3(w, 0.06f, half * 2), surface);
            if (rl >= 3)
            {
                for (float z = -half + 14; z < half - 14; z += 9) mb.Box(new Vector3(Layout.RunwayX, 0.04f, z), new Vector3(0.35f, 0.02f, 4.5f), Palette.Paint);
                foreach (float e in new[] { -1f, 1f })
                    for (int k = 0; k < 6; k++) mb.Box(new Vector3(Layout.RunwayX - w * 0.38f + k * w * 0.152f, 0.04f, e * (half - 5)), new Vector3(0.5f, 0.02f, 6f), Palette.Paint);
                if (rl >= 6)
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(Layout.RunwayX + sx * (w * 0.5f - 0.4f), 0.04f, 0), new Vector3(0.25f, 0.02f, half * 2 - 4), Palette.Paint);
            }
            else
                foreach (float e in new[] { -1f, 1f })   // a grass strip is marked by white boards at its corners
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(Layout.RunwayX + sx * w * 0.5f, 0.15f, e * half), new Vector3(0.9f, 0.3f, 0.5f), Palette.Paint);
            if (rl >= 5)   // edge lights
                for (float z = -half; z <= half; z += 10)
                {
                    mb.Glow = true;
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(Layout.RunwayX + sx * (w * 0.5f + 0.6f), 0.2f, z), new Vector3(0.45f, 0.4f, 0.45f), Palette.Hex(0xFFE9A8));
                    mb.Glow = false;
                }

            // Taxiway and its links to the runway.
            int tl = s.Level("taxiways");
            bool taxiPaved = tl >= 4 || rl >= 6 || s.StandCount > 2;      // nobody taxis a jet over gravel
            Color taxi = tl == 0 ? Palette.Dirt : Palette.Gravel;
            float tw = 4.5f + Mathf.Min(6, tl) * 0.35f;
            float lineUp = Layout.LineUpZ(s), exit = Layout.ExitZ(s);
            if (taxiPaved) g.Rect("Tarmac", Layout.TaxiX, (lineUp + Mathf.Max(exit, 46)) * 0.5f, tw, Mathf.Max(exit, 46) - lineUp + tw, 0.035f);
            else mb.Box(new Vector3(Layout.TaxiX, 0.01f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(tw, 0.06f, Mathf.Max(exit, 46) - lineUp + tw), taxi);
            foreach (float z in new[] { lineUp, exit })
                if (taxiPaved) g.Rect("Tarmac", (Layout.RunwayX + Layout.TaxiX) * 0.5f, z, Layout.TaxiX - Layout.RunwayX, tw, 0.035f); else mb.Box(new Vector3((Layout.RunwayX + Layout.TaxiX) * 0.5f, 0.01f, z), new Vector3(Layout.TaxiX - Layout.RunwayX, 0.06f, tw), taxi);
            if (taxiPaved) mb.Box(new Vector3(Layout.TaxiX, 0.05f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(0.2f, 0.02f, Mathf.Max(exit, 46) - lineUp), Palette.PaintYellow);
            if (tl >= 2)
            {
                mb.Glow = true;
                for (float z = lineUp; z <= Mathf.Max(exit, 46); z += 8)
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(Layout.TaxiX + sx * (tw * 0.5f + 0.4f), 0.15f, z), new Vector3(0.32f, 0.3f, 0.32f), Palette.Hex(0x4A9BFF));
                mb.Glow = false;
            }

            // Apron and stands.
            int cols = Layout.Columns(s);
            float apronW = termX - Layout.TaxiX + 2;
            if (s.StandCount <= 2) mb.Box(new Vector3(Layout.TaxiX + apronW * 0.5f, 0.0f, 0), new Vector3(apronW, 0.05f, 112), Palette.Gravel);
            else g.Rect("Concrete", Layout.TaxiX + apronW * 0.5f, 0, apronW, 112, 0.025f);
            bool paved = s.StandCount > 2;
            if (paved)
            {
                // The apron is poured in slabs: sealed joints every 7.5 m, and darker patches where the engines idle.
                var joint = new Color(0.33f, 0.34f, 0.36f);
                for (float x = Layout.TaxiX + 4; x < termX; x += 7.5f) mb.Box(new Vector3(x, 0.03f, 0), new Vector3(0.09f, 0.012f, 112), joint);
                for (float z = -52.5f; z <= 52.5f; z += 7.5f) mb.Box(new Vector3(Layout.TaxiX + apronW * 0.5f, 0.03f, z), new Vector3(apronW, 0.012f, 0.09f), joint);
            }
            for (int i = 0; i < s.StandCount; i++)
            {
                var p = Layout.Stand(i);
                float y = 0.05f;
                mb.Box(new Vector3(p.x - 3, y, p.z), new Vector3(12, 0.02f, 0.28f), Palette.PaintYellow);                  // lead-in line
                mb.Box(new Vector3(p.x + 3.4f, y, p.z), new Vector3(0.3f, 0.02f, 3f), Palette.PaintYellow);                // stop bar
                if (!paved) continue;
                // The stand's box in red, hatched at the service side, with a white equipment bay.
                foreach (float sz in new[] { -5.6f, 5.6f }) mb.Box(new Vector3(p.x - 1.5f, y, p.z + sz), new Vector3(15, 0.02f, 0.18f), Palette.Hex(0xC8362E));
                mb.Box(new Vector3(p.x + 6f, y, p.z), new Vector3(0.18f, 0.02f, 11.2f), Palette.Hex(0xC8362E));
                for (int k = 0; k < 5; k++)
                {
                    mb.Push().Translate(p.x - 6.5f + k * 1.2f, y, p.z - 4.6f).RotateY(45);
                    mb.Box(Vector3.zero, new Vector3(0.14f, 0.02f, 1.8f), Palette.Paint);
                    mb.Pop();
                }
                mb.Box(new Vector3(p.x - 4.2f, y, p.z - 5.45f), new Vector3(5.6f, 0.02f, 0.12f), Palette.Paint);
                mb.Box(new Vector3(p.x - 4.2f, y, p.z - 3.7f), new Vector3(5.6f, 0.02f, 0.12f), Palette.Paint);
                if (s.HeavyStand(i))
                {
                    // A wide-body stand: a second, longer lead-in to its own stop bar, a strengthened concrete pad
                    // under the main gear, and a wingtip clearance line either side.
                    mb.Box(new Vector3(p.x - 0.5f, 0.035f, p.z), new Vector3(9f, 0.02f, 9.4f), new Color(0.56f, 0.57f, 0.58f));
                    mb.Box(new Vector3(p.x - 3, y, p.z), new Vector3(12, 0.02f, 0.28f), Palette.PaintYellow);
                    mb.Box(new Vector3(p.x + 4.6f, y, p.z), new Vector3(0.3f, 0.02f, 4.4f), Palette.PaintYellow);
                    foreach (float sz in new[] { -7.6f, 7.6f })
                        for (float x = p.x - 8f; x < p.x + 5f; x += 2f) mb.Box(new Vector3(x, y, p.z + sz), new Vector3(1.1f, 0.02f, 0.16f), Palette.Paint);
                    mb.Box(new Vector3(p.x - 9.6f, y, p.z - 1.2f), new Vector3(1.8f, 0.02f, 1.4f), Palette.Hex(0xC8362E));
                    mb.Box(new Vector3(p.x - 9.6f, y + 0.005f, p.z - 1.2f), new Vector3(1.1f, 0.02f, 0.22f), Palette.Paint);
                    foreach (float hz in new[] { -0.45f, 0.45f }) mb.Box(new Vector3(p.x - 9.6f, y + 0.005f, p.z - 1.2f + hz), new Vector3(0.22f, 0.02f, 0.22f), Palette.Paint);
                }
                // The stand's number board: yellow on black at the lane.
                mb.Box(new Vector3(p.x - 9.6f, y, p.z + 1.6f), new Vector3(1.8f, 0.02f, 1.4f), Palette.Black);
                mb.Box(new Vector3(p.x - 9.6f, y + 0.005f, p.z + 1.6f), new Vector3(1.2f, 0.02f, 0.8f), Palette.PaintYellow);
            }
            // Ground equipment parked at each stand: a baggage-cart train, steps, a power unit, cones round the wingtips.
            var kitEarly = new GameObject("Kit").transform;
            _kitRoot = kitEarly;
            for (int i = 0; i < s.StandCount; i++)
            {
                var p = Layout.Stand(i);
                var prng = new System.Random(100 + i);
                if (s.Level("baggage") > 0)
                    for (int k = 0; k < 3; k++)
                    {
                        var cp = new Vector3(p.x - 6.6f + k * 1.9f, 0, p.z - 4.6f);
                        mb.Smoothness = 0.3f;
                        mb.Box(cp + Vector3.up * 0.45f, new Vector3(1.6f, 0.12f, 1.1f), Palette.Hex(0x3C4652));
                        mb.Box(cp + new Vector3(0, 0.85f, 0), new Vector3(1.5f, 0.7f, 1.0f), k == 1 ? Palette.Hex(0x2F6FB5) : Palette.Hex(0xC9CED3));
                        foreach (float wx in new[] { -0.6f, 0.6f }) foreach (float wz in new[] { -0.45f, 0.45f })
                            mb.Cylinder(cp + new Vector3(wx, 0.18f, wz - 0.05f), cp + new Vector3(wx, 0.18f, wz + 0.05f), 0.18f, 6, Palette.Tyre);
                    }
                if (paved)
                {
                    // Ground power unit (yellow) and a set of steps.
                    mb.Box(new Vector3(p.x + 4.6f, 0.55f, p.z - 4.4f), new Vector3(1.5f, 1.0f, 1.0f), Palette.PaintYellow, Palette.Hex(0xC79A1E));
                    mb.Box(new Vector3(p.x + 4.6f, 1.12f, p.z - 4.4f), new Vector3(0.5f, 0.14f, 0.5f), Palette.SteelDark);
                    if (s.Level("gates") < 3)
                        for (int k = 0; k < 5; k++) mb.Box(new Vector3(p.x + 1.6f + k * 0.32f, 0.2f + k * 0.2f, p.z + 4.9f), new Vector3(0.34f, 0.4f + k * 0.4f, 1.0f), Palette.Hex(0xE4E7EA), Palette.Hex(0x9AA0A6));
                    for (int k = 0; k < 4; k++)
                        Gse.Cone(mb, new Vector3(p.x - 1 + (k % 2) * 3.4f, 0.04f, p.z + (k < 2 ? -5.2f : 5.2f) + (float)prng.NextDouble() * 0.3f));
                }
            }
            if (paved)
            {
                // Chain-link fence along the airside boundary (posts and a faint mesh band).
                float fx = Layout.RunwayX - w * 0.5f - 16, fz0 = -half - 30, fz1 = half + 30;
                for (float z = fz0; z <= fz1; z += 6) mb.Box(new Vector3(fx, 1.1f, z), new Vector3(0.12f, 2.2f, 0.12f), Palette.Steel);
                mb.Box(new Vector3(fx, 1.3f, (fz0 + fz1) * 0.5f), new Vector3(0.04f, 1.7f, fz1 - fz0), new Color(0.62f, 0.66f, 0.7f, 1));
            }
            if (rl >= 4)
            {
                // Threshold numbers (blocks of paint), aiming points, and PAPI lights beside the touchdown zone.
                foreach (float e in new[] { -1f, 1f })
                {
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Box(new Vector3(Layout.RunwayX + sx * w * 0.14f, 0.045f, e * (half - 16)), new Vector3(w * 0.1f, 0.02f, 5f), Palette.Paint);
                        mb.Box(new Vector3(Layout.RunwayX + sx * w * 0.27f, 0.045f, e * (half - 32)), new Vector3(w * 0.14f, 0.02f, 9f), Palette.Paint);
                    }
                    for (int k = 0; k < 4; k++)
                        mb.Box(new Vector3(Layout.RunwayX - w * 0.5f - 3 - k * 1.3f, 0.3f, e * (half - 30)), new Vector3(0.7f, 0.5f, 0.7f), k < 2 ? Palette.Hex(0xE8372E) : Palette.White);
                }
            }
            if (paved)
            {
                // The apron's taxilane (yellow centre line down each column's lane) and a service road along the terminal (white edges).
                for (int col = 0; col < cols; col++)
                    mb.Box(new Vector3(Layout.StandX0 + col * Layout.ColumnGap - 9f, 0.05f, 0), new Vector3(0.25f, 0.02f, 108), Palette.PaintYellow);
                foreach (float off in new[] { -3.2f, -6.4f })
                    for (float z = -54; z < 54; z += 3f) mb.Box(new Vector3(termX + off, 0.05f, z), new Vector3(0.14f, 0.02f, 1.6f), Palette.Paint);
                // Floodlight masts along the apron's edge.
                for (float z = -48; z <= 50; z += 24)
                {
                    var lp = new Vector3(termX - 1.2f, 0, z);
                    mb.Cylinder(lp, lp + Vector3.up * 14, 0.22f, 0.14f, 6, Palette.Steel);
                    mb.Box(lp + new Vector3(-0.8f, 14.2f, 0), new Vector3(2.4f, 0.5f, 1.4f), Palette.SteelDark);
                    mb.Glow = true; mb.Box(lp + new Vector3(-0.8f, 13.9f, 0), new Vector3(2.2f, 0.12f, 1.2f), Palette.White); mb.Glow = false;
                }
            }

            // Terminal: a shed at first, then a hall with a glass front to the apron that grows along the stands.
            int tLevel = Mathf.Min(s.Level("checkin"), Mathf.Min(s.Level("security"), s.Level("gates")));
            float tLen = Mathf.Min(108, 16 + tLevel * 6 + s.StandCount * 3f), tDepth = 9 + Mathf.Min(10, tLevel) * 0.9f, tH = 4.5f + Mathf.Min(6, tLevel / 3) * 1.6f;
            float tMidZ = -45.5f + tLen * 0.5f - 6;
            TerminalDoor = new Vector3(termX, 0, Mathf.Clamp(-45.5f, tMidZ - tLen * 0.5f + 3, tMidZ + tLen * 0.5f - 3));
            bool shed = tLevel <= 1 && s.StandCount <= 1;
            if (shed)
            {
                mb.Smoothness = 0.1f;
                mb.Box(new Vector3(termX + 4, 1.5f, -45.5f), new Vector3(7, 3, 9), Palette.WoodPale);
                mb.GableRoof(new Vector3(termX + 4, 3f, -45.5f), new Vector3(7, 1.6f, 9), Palette.Roof, Palette.WoodPale, 0.5f);
                mb.Box(new Vector3(termX + 0.4f, 1.1f, -45.5f), new Vector3(0.2f, 2.2f, 1.4f), Palette.Hex(0x4A3A2A));     // the door
                mb.Box(new Vector3(termX + 4, 4.9f, -43f), new Vector3(0.12f, 2.2f, 0.12f), Palette.Steel);              // aerial
                // The little airfield you inherit: a name board, a picket fence, a bench, a bicycle shed, flower tubs.
                mb.Box(new Vector3(termX - 1.5f, 1.3f, -38.5f), new Vector3(0.14f, 2.6f, 0.14f), Palette.Wood);
                mb.Box(new Vector3(termX - 1.5f, 2.3f, -38.5f), new Vector3(0.12f, 0.9f, 3.4f), Palette.White, Palette.Hex(0x1F5FA8));
                mb.Box(new Vector3(termX - 1.56f, 2.3f, -38.5f), new Vector3(0.04f, 0.3f, 2.4f), Palette.Hex(0x1F5FA8));
                for (float z = -52; z <= -32; z += 0.9f) mb.Box(new Vector3(termX + 9.5f, 0.45f, z), new Vector3(0.1f, 0.9f, 0.5f), Palette.White);
                mb.Box(new Vector3(termX + 9.5f, 0.55f, -42), new Vector3(0.06f, 0.1f, 20f), Palette.White);
                mb.Box(new Vector3(termX - 0.8f, 0.45f, -49.5f), new Vector3(0.6f, 0.1f, 2.0f), Palette.Wood);
                foreach (float bz in new[] { -50.3f, -48.7f }) mb.Box(new Vector3(termX - 0.8f, 0.22f, bz), new Vector3(0.5f, 0.44f, 0.12f), Palette.SteelDark);
                foreach (float fz in new[] { -41.5f, -49.8f })
                {
                    mb.Cylinder(new Vector3(termX - 0.2f, 0, fz), new Vector3(termX - 0.2f, 0.5f, fz), 0.5f, 0.6f, 8, Palette.Hex(0x9A6B4A));
                    mb.Sphere(new Vector3(termX - 0.2f, 0.75f, fz), new Vector3(0.6f, 0.4f, 0.6f), 6, Palette.Hex(0xE2557A));
                }
                Models.Kit("sedan", kitEarly, new Vector3(termX + 6.5f, 0.05f, -52), 20, 4.2f);
                mb.Push().Translate(Layout.TaxiX + 9, 0.05f, -57).RotateY(100); mb.Append(Gse.Build(Service.Fuel)); mb.Pop(); mb.Smoothness = 0.3f;
            }
            else
            {
                var c = new Vector3(termX + tDepth * 0.5f, 0, tMidZ);
                mb.Smoothness = 0.25f;
                mb.Box(c + Vector3.up * tH * 0.5f, new Vector3(tDepth, tH, tLen), Palette.Hex(0xE9E6DE), Palette.Hex(0xCFCBC2));
                // Glass curtain walls, apron side and landside, between white mullions.
                mb.Smoothness = 0.92f;
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 0.06f), tH * 0.52f, c.z), new Vector3(0.14f, tH * 0.72f, tLen - 2.4f), Palette.Hex(0x4E86A8));
                mb.Glow = true;
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 0.14f), tH * 0.34f, c.z), new Vector3(0.05f, tH * 0.22f, tLen - 3f), Palette.Hex(0xFFE2A0));
                mb.Glow = false;
                mb.Smoothness = 0.3f;
                for (float z = c.z - tLen * 0.5f + 1.2f; z <= c.z + tLen * 0.5f - 1.1f; z += 3f)
                    foreach (float side in new[] { -1f, 1f })
                        mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 0.12f), tH * 0.52f, z), new Vector3(0.18f, tH * 0.74f, 0.22f), Palette.White);
                // The roof: a pale membrane (textured), a parapet round its edge, plant on top.
                g.Rect("Concrete", c.x, c.z, tDepth + 2.2f, tLen + 1.4f, tH + 0.52f, 0, new Color(0.86f, 0.87f, 0.9f));
                foreach (float side in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 1.1f), tH + 0.75f, c.z), new Vector3(0.25f, 0.5f, tLen + 1.6f), Palette.White);
                    mb.Box(new Vector3(c.x, tH + 0.75f, c.z + side * (tLen * 0.5f + 0.7f)), new Vector3(tDepth + 2.4f, 0.5f, 0.25f), Palette.White);
                }
                // A coloured fascia band along both long sides: the airport's own blue.
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 0.1f), tH * 0.93f, c.z), new Vector3(0.2f, tH * 0.12f, tLen - 0.4f), Palette.Hex(0x1F5FA8));
                mb.Box(c + Vector3.up * (tH + 0.25f), new Vector3(tDepth + 2.4f, 0.5f, tLen + 1.6f), Palette.White, Palette.Hex(0xD8D6CF));
                for (float z = c.z - tLen * 0.5f + 5; z < c.z + tLen * 0.5f - 4; z += 9)
                {
                    mb.Box(new Vector3(c.x + 1.6f, tH + 1.05f, z), new Vector3(2.4f, 1.0f, 3f), Palette.Hex(0xAEB4BA), Palette.Hex(0x8C9298));
                    mb.Cylinder(new Vector3(c.x + 1.6f, tH + 1.55f, z - 0.7f), new Vector3(c.x + 1.6f, tH + 1.62f, z - 0.7f), 0.55f, 10, Palette.Hex(0x3A4048));
                    mb.Cylinder(new Vector3(c.x + 1.6f, tH + 1.55f, z + 0.7f), new Vector3(c.x + 1.6f, tH + 1.62f, z + 0.7f), 0.55f, 10, Palette.Hex(0x3A4048));
                    mb.Box(new Vector3(c.x + 3.4f, tH + 0.75f, z), new Vector3(0.3f, 0.3f, 6f), Palette.Hex(0x9AA0A6));        // duct
                }
                // A canopy over the drop-off, and the doors.
                mb.Box(new Vector3(c.x + tDepth * 0.5f + 3.2f, 3.4f, c.z), new Vector3(5.5f, 0.3f, Mathf.Min(tLen * 0.7f, 40)), Palette.White);
                for (float z = c.z - Mathf.Min(tLen * 0.3f, 18); z <= c.z + Mathf.Min(tLen * 0.3f, 18) + 0.1f; z += 6)
                    mb.Cylinder(new Vector3(c.x + tDepth * 0.5f + 5.4f, 0, z), new Vector3(c.x + tDepth * 0.5f + 5.4f, 3.3f, z), 0.16f, 8, Palette.Steel);
                if (tLevel >= 4)
                {
                    // The departures hall: a taller glass volume in the middle, with a curved white roof.
                    float hl = Mathf.Min(tLen * 0.42f, 34), hh = tH + 4.5f;
                    var hc = new Vector3(c.x + tDepth * 0.5f + 1.5f, 0, c.z);
                    mb.Smoothness = 0.92f;
                    mb.Box(hc + Vector3.up * hh * 0.5f, new Vector3(6.5f, hh, hl), Palette.Hex(0x4A8FB5));
                    mb.Smoothness = 0.3f;
                    for (float z = hc.z - hl * 0.5f; z <= hc.z + hl * 0.5f + 0.1f; z += hl / 8f)
                        mb.Box(new Vector3(hc.x + 3.3f, hh * 0.5f, z), new Vector3(0.2f, hh, 0.3f), Palette.White);
                    for (float y = 3; y < hh; y += 3) mb.Box(new Vector3(hc.x + 3.3f, y, hc.z), new Vector3(0.16f, 0.16f, hl), Palette.White);
                    // A barrel vault of glass over the hall, on white steel ribs.
                    mb.Smoothness = 0.94f;
                    mb.Cylinder(new Vector3(hc.x, hh - 2.4f, hc.z - hl * 0.5f), new Vector3(hc.x, hh - 2.4f, hc.z + hl * 0.5f), 4.2f, 4.2f, 18, Palette.Hex(0x5C9FC4));
                    mb.Smoothness = 0.3f;
                    for (float z = hc.z - hl * 0.5f; z <= hc.z + hl * 0.5f + 0.1f; z += hl / 8f)
                        mb.Cylinder(new Vector3(hc.x, hh - 2.4f, z - 0.14f), new Vector3(hc.x, hh - 2.4f, z + 0.14f), 4.32f, 4.32f, 18, Palette.White);
                    mb.Box(new Vector3(hc.x + 3.45f, hh * 0.86f, hc.z), new Vector3(0.14f, 1.3f, hl * 0.5f), Palette.Hex(0x1F5FA8));     // the airport's sign board
                }
                // Skylights along the roof; the shopping concourse is a glass atrium that lengthens with its level.
                mb.Smoothness = 0.92f;
                for (float z = c.z - tLen * 0.5f + 3; z < c.z + tLen * 0.5f - 3; z += 4.5f)
                    mb.Box(new Vector3(c.x - tDepth * 0.22f, tH + 0.55f, z), new Vector3(tDepth * 0.28f, 0.14f, 2.6f), Palette.Hex(0x5E9CBE));
                mb.Glow = true;
                for (float z = c.z - tLen * 0.5f + 3; z < c.z + tLen * 0.5f - 3; z += 4.5f)
                    mb.Box(new Vector3(c.x - tDepth * 0.22f, tH + 0.63f, z), new Vector3(tDepth * 0.2f, 0.02f, 1.6f), Palette.Hex(0xFFF0C8));
                mb.Glow = false;
                if (s.Level("shops") > 0)
                {
                    float sl = Mathf.Min(tLen - 8, 8 + s.Level("shops") * 2.5f);
                    mb.Box(new Vector3(c.x + tDepth * 0.2f, tH + 1.6f, c.z), new Vector3(tDepth * 0.34f, 2.4f, sl), Palette.Hex(0x6FB0CC));
                    mb.Smoothness = 0.3f;
                    mb.Box(new Vector3(c.x + tDepth * 0.2f, tH + 2.9f, c.z), new Vector3(tDepth * 0.34f + 0.5f, 0.25f, sl + 0.5f), Palette.White);
                    for (float z = c.z - sl * 0.5f; z <= c.z + sl * 0.5f + 0.1f; z += sl / 4f)
                        mb.Box(new Vector3(c.x + tDepth * 0.2f, tH + 1.6f, z), new Vector3(tDepth * 0.34f + 0.2f, 2.5f, 0.18f), Palette.White);
                }
                mb.Smoothness = 0.3f;
                if (s.Level("lounge") > 0)
                {
                    mb.Smoothness = 0.9f;
                    mb.Box(new Vector3(c.x, tH + 1.6f, c.z + tLen * 0.5f - 6), new Vector3(tDepth * 0.8f, 2.4f, 8), Palette.Hex(0x9A7ACB));
                    mb.Smoothness = 0.3f;
                }
                // Jet bridges to the nearest column of stands once the gates are worth it; steps before that.
                int lastCol = Layout.Columns(s) - 1;
                for (int i = lastCol * Layout.PerColumn; i < s.StandCount; i++)
                {
                    var p = Layout.Stand(i);
                    if (p.z > c.z + tLen * 0.5f || p.z < c.z - tLen * 0.5f) continue;
                    if (s.Level("gates") >= 3)
                    {
                        _bridged.Add(i);
                        Bridge(mb, termX, p.z + 3.6f, p.x + 5.2f, p.z + 2.7f, 1.05f);
                        // The second bridge, to the door just ahead of the wing.
                        if (s.HeavyStand(i)) Bridge(mb, termX, p.z + 7.4f, p.x + 1.2f, p.z + 2.7f, 1.0f);
                    }
                }
            }

            // Landside: the road, the car park (cars by its level), and the town beyond, which grows with the stars.
            float roadX = termX + tDepth + 12;
            RoadX = roadX;
            mb.Smoothness = 0.08f;
            g.Rect("Tarmac", roadX, 0, 7, 420, 0.03f);
            for (float z = -200; z < 200; z += 8) mb.Box(new Vector3(roadX, 0.04f, z), new Vector3(0.2f, 0.02f, 3.5f), Palette.Paint);
            for (float z = -190; z <= 190; z += 26)
            {
                var sl = new Vector3(roadX + 4.2f, 0, z);
                mb.Cylinder(sl, sl + Vector3.up * 6, 0.1f, 0.07f, 5, Palette.SteelDark);
                mb.Box(sl + new Vector3(-0.9f, 6f, 0), new Vector3(1.8f, 0.1f, 0.14f), Palette.SteelDark);
                mb.Glow = true; mb.Box(sl + new Vector3(-1.6f, 5.9f, 0), new Vector3(0.6f, 0.12f, 0.3f), Palette.Hex(0xFFE2A0)); mb.Glow = false;
            }
            int park = s.Level("parking");
            float lotW = 16 + Mathf.Min(6, park) * 3, lotL = 26 + Mathf.Min(10, park) * 5;
            var lot = new Vector3(roadX + 5 + lotW * 0.5f, 0, -46 + lotL * 0.5f);
            g.Rect("Tarmac", lot.x, lot.z, lotW, lotL, 0.03f, 0, new Color(1.25f, 1.25f, 1.25f));
            var kit = kitEarly;
            var rng = new System.Random(11);
            string[] cars = { "sedan", "suv", "hatchback", "taxi", "pickup", "coupe", "sedan", "suv" };
            int rows = Mathf.Max(1, Mathf.FloorToInt(lotW / 6.5f)), perRow = Mathf.FloorToInt(lotL / 3.2f);
            int wantCars = Mathf.Min(rows * perRow, 3 + park * 5 + s.Stars * 2);
            for (int r = 0, placed = 0; r < rows && placed < wantCars; r++)
                for (int k = 0; k < perRow && placed < wantCars; k++)
                {
                    mb.Box(new Vector3(lot.x - lotW * 0.5f + 3.2f + r * 6.5f, 0.04f, lot.z - lotL * 0.5f + 0.4f + k * 3.2f), new Vector3(4.6f, 0.02f, 0.12f), Palette.Paint);
                    if (rng.NextDouble() < 0.25) continue;
                    Models.Kit(cars[rng.Next(cars.Length)], kit, new Vector3(lot.x - lotW * 0.5f + 3.2f + r * 6.5f, 0.05f, lot.z - lotL * 0.5f + 2f + k * 3.2f), rng.Next(2) * 180 + 90, 4.2f);
                    placed++;
                }
            // The town.
            // The town beside the airport grows with its rating: houses and shops first, offices, then glass towers
            // (kept back from the road, never under the approach). Each plot has its pavement.
            int blocks = 6 + s.Stars * 5;
            for (int i = 0; i < blocks; i++)
            {
                float bx = roadX + lotW + 22 + (i % 5) * 17 + (float)rng.NextDouble() * 4, bz = -150 + (i / 5) * 24 + (float)rng.NextDouble() * 6;
                bool tall = s.Stars >= 4 && rng.NextDouble() < 0.12 + s.Stars * 0.03;
                var at = new Vector3(bx, 0, bz);
                g.Rect("Concrete", bx, bz, 15, 20, 0.02f, 0, new Color(0.82f, 0.82f, 0.82f));
                if (tall) Scenery.Building(mb, at, 10 + rng.Next(3), 11 + rng.Next(4), 36 + rng.Next(40), rng);
                else if (rng.Next(5) < 2 || s.Stars < 3) Scenery.House(mb, at, 8 + rng.Next(3), 10 + rng.Next(4), rng);
                else Scenery.Building(mb, at, 10 + rng.Next(3), 12 + rng.Next(5), 9 + rng.Next(4 + s.Stars * 2), rng);
            }

            // Service depots south of the apron: a works building each, tanks for the fuel farm.
            float dx = Layout.TaxiX + 10;
            foreach (var id in Content.Departments)
            {
                int l = s.Level(id);
                DepotAt[id] = new Vector3(dx, 0, -62);
                if (l > 0)
                {
                    Works.Depot(mb, id, new Vector3(dx, 0, -71), l);
                    g.Rect("Concrete", dx, -66, 15, 17, 0.03f, 0, new Color(0.85f, 0.85f, 0.85f));
                }
                dx += 19;
            }
            // North of the apron: the hangar and the cargo terminal.
            if (s.Level("hangar") > 0)
            {
                float hw = 18 + Mathf.Min(10, s.Level("hangar")) * 1.2f;
                var hp = new Vector3(Layout.TaxiX + 18, 0, 78);
                // Doors to the west, onto the taxiway (and toward the camera).
                mb.Push().Translate(hp).RotateY(90); Works.Hangar(mb, Vector3.zero, hw); mb.Pop();
                mb.Smoothness = 0.3f;
                g.Rect("Concrete", hp.x - 15, hp.z, 10, hw, 0.03f);
            }
            if (s.Level("cargo") > 0)
            {
                var cp = new Vector3(Layout.TaxiX + 52, 0, 76);
                float cw = 20f + Mathf.Min(10, s.Level("cargo")) * 1.2f;
                mb.Push().Translate(cp + new Vector3(10, 0, 6)).RotateY(90); Works.Cargo(mb, Vector3.zero, s.Level("cargo")); mb.Pop();
                mb.Smoothness = 0.3f;
                g.Rect("Concrete", cp.x - 2, cp.z + 4, 38, cw + 14, 0.03f, 0, new Color(0.85f, 0.85f, 0.85f));
            }
            if (s.Level("ils") > 0)
            {
                // Approach lights off the south end, and the radar by the tower.
                for (int k = 1; k <= 4 + s.Level("ils"); k++)
                    mb.Box(new Vector3(Layout.RunwayX, 0.4f, -half - k * 7), new Vector3(k % 3 == 0 ? 6 : 0.4f, 0.8f, 0.4f), Palette.PaintYellow);
            }

            // Tower: a tapered concrete shaft, a base building, a glass cab with a walkway, antennas and a beacon.
            int tower = s.Level("tower");
            float th = 9 + tower * 2.4f;
            var tp = new Vector3(Layout.TaxiX + 4, 0, 60);
            TowerTop = tp + Vector3.up * (th + 4.4f);
            mb.Smoothness = 0.25f;
            Models.Block(mb, tp + new Vector3(3.5f, 0, 0), new Vector3(9, 3.6f, 7), Palette.Hex(0xE4E1D9), Palette.Hex(0x4A5058));
            mb.Cylinder(tp, tp + Vector3.up * th, 1.9f, 1.35f, 14, Palette.Hex(0xE9E6DE));
            for (float y = 3; y < th - 1; y += 2.4f) mb.Box(tp + new Vector3(0, y, -1.75f + y * 0.02f), new Vector3(0.5f, 1.0f, 0.12f), Palette.Hex(0x2B3A48));   // slit windows
            mb.Cylinder(tp + Vector3.up * th, tp + Vector3.up * (th + 0.5f), 3.5f, 3.7f, 14, Palette.Hex(0xC9CED3));                                             // walkway
            mb.Smoothness = 0.95f;
            mb.Cylinder(tp + Vector3.up * (th + 0.5f), tp + Vector3.up * (th + 2.9f), 2.7f, 3.3f, 14, Palette.Hex(0x3E7FA6));                                   // the cab's glass, leaning out
            mb.Smoothness = 0.3f;
            mb.Cylinder(tp + Vector3.up * (th + 2.9f), tp + Vector3.up * (th + 3.3f), 3.6f, 3.5f, 14, Palette.White);
            mb.Cylinder(tp + Vector3.up * (th + 3.3f), tp + Vector3.up * (th + 4.2f), 0.8f, 0.6f, 8, Palette.Hex(0xAEB4BA));
            mb.Cylinder(tp + new Vector3(1.6f, th + 3.3f, 0.8f), tp + new Vector3(1.6f, th + 7.5f, 0.8f), 0.05f, 4, Palette.Steel);
            mb.Cylinder(tp + new Vector3(-1.4f, th + 3.3f, -1f), tp + new Vector3(-1.4f, th + 6f, -1f), 0.05f, 4, Palette.Steel);
            mb.Glow = true;
            mb.Sphere(tp + new Vector3(1.6f, th + 7.6f, 0.8f), Vector3.one * 0.3f, 6, Palette.Hex(0xFF4A3A));
            mb.Cylinder(tp + Vector3.up * (th + 1.4f), tp + Vector3.up * (th + 1.9f), 3.08f, 3.2f, 14, Palette.Hex(0xBFE6FF));
            mb.Glow = false;

            // What makes it read as a tower: the flare under the walkway, a handrail, the cab's mullions and roof
            // overhang, vertical fins up the shaft, and a radar turning head on the roof.
            mb.Smoothness = 0.25f;
            mb.Cylinder(tp + Vector3.up * (th - 2.6f), tp + Vector3.up * th, 1.42f, 3.3f, 14, Palette.Hex(0xDDDAD2));
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                var foot = tp + new Vector3(Mathf.Cos(a) * 1.95f, 0, Mathf.Sin(a) * 1.95f); var head = tp + new Vector3(Mathf.Cos(a) * 1.42f, th - 2.6f, Mathf.Sin(a) * 1.42f);
                mb.Cylinder(foot, head, 0.16f, 0.12f, 4, Palette.Hex(0xC4C0B6));
            }
            for (int k = 0; k < 14; k++)
            {
                float a = (k + 0.5f) * (360f / 14f) * Mathf.Deg2Rad; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                mb.Cylinder(tp + dir * 3.62f + Vector3.up * (th + 0.5f), tp + dir * 3.62f + Vector3.up * (th + 1.45f), 0.04f, 4, Palette.Steel);                       // rail posts
                mb.Cylinder(tp + dir * 2.72f + Vector3.up * (th + 0.5f), tp + dir * 3.32f + Vector3.up * (th + 2.9f), 0.07f, 4, Palette.Hex(0x1E2A36));             // mullions
            }
            mb.Cylinder(tp + Vector3.up * (th + 1.4f), tp + Vector3.up * (th + 1.46f), 3.66f, 3.66f, 14, Palette.Steel, false);
            mb.Cylinder(tp + Vector3.up * (th + 3.3f), tp + Vector3.up * (th + 3.36f), 3.9f, 3.9f, 14, Palette.Hex(0xC9CED3));
            // Radar: a pedestal and a curved bar antenna (it faces down the runway).
            mb.Cylinder(tp + new Vector3(0, th + 4.2f, 0), tp + new Vector3(0, th + 5.0f, 0), 0.14f, 6, Palette.Steel);
            mb.Push().Translate(tp + new Vector3(0, th + 5.1f, 0)).RotateY(25);
            mb.Box(Vector3.zero, new Vector3(3.2f, 0.5f, 0.12f), Palette.Hex(0xE8372E)); mb.Box(new Vector3(0, 0.05f, 0.2f), new Vector3(2.8f, 0.3f, 0.3f), Palette.Hex(0xD9D9D9));
            mb.Pop();
            mb.Smoothness = 0.3f;

            // A windsock by the runway: every airfield has one.
            var ws = new Vector3(Layout.RunwayX + w * 0.5f + 5, 0, -half + 20);
            mb.Cylinder(ws, ws + Vector3.up * 5, 0.1f, 0.08f, 6, Palette.Steel);
            mb.Cylinder(ws + Vector3.up * 4.8f, ws + new Vector3(0, 4.5f, 2.6f), 0.45f, 0.15f, 8, Palette.Fuel);

            // The country round the field, right up to the fence: four blocks (west, east, south, north) and, landside,
            // the land past either end of the town. Each map fills them its own way (see Scenery).
            float clearX0 = Layout.RunwayX - 26, clearX1 = roadX + 132, clearZ0 = Mathf.Min(-half - 90, -175), clearZ1 = half + 114;
            float townZ1 = -150 + (blocks / 5 + 1) * 24 + 14;
            var country = new List<Rect>
            {
                new Rect(-680, -700, clearX0 + 680, 1400), new Rect(clearX1, -700, 720 - clearX1, 1400),
                new Rect(clearX0, -700, clearX1 - clearX0, clearZ0 + 700), new Rect(clearX0, clearZ1, clearX1 - clearX0, 700 - clearZ1),
            };
            if (-170 - clearZ0 > 40) country.Add(new Rect(roadX + 9, clearZ0, clearX1 - roadX - 9, -170 - clearZ0));
            if (clearZ1 - townZ1 > 40) country.Add(new Rect(roadX + 9, townZ1, clearX1 - roadX - 9, clearZ1 - townZ1));
            Scenery.Build(mb, g, map, country, ground, tree);
            // The airside fence: posts and a top rail round the runway and the apron.
            {
                float fx0 = Layout.RunwayX - 24, fx1 = termX - 1, fz0 = -half - 88, fz1 = half + 112;
                mb.Smoothness = 0.4f;
                Color post = Palette.Hex(0x8C949C);
                void Run(Vector3 a, Vector3 b)
                {
                    float len = Vector3.Distance(a, b); int posts = Mathf.Max(2, Mathf.RoundToInt(len / 6f));
                    for (int i = 0; i <= posts; i++) { var q = Vector3.Lerp(a, b, i / (float)posts); mb.Box(q + Vector3.up * 1.1f, new Vector3(0.14f, 2.2f, 0.14f), post); }
                    var mid = (a + b) * 0.5f; bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                    foreach (float y in new[] { 2.15f, 1.2f }) mb.Box(mid + Vector3.up * y, alongX ? new Vector3(len, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, len), post);
                }
                Run(new Vector3(fx0, 0, fz0), new Vector3(fx0, 0, fz1));
                Run(new Vector3(fx0, 0, fz0), new Vector3(fx1, 0, fz0));
                Run(new Vector3(fx0, 0, fz1), new Vector3(fx1, 0, fz1));
            }

            _static = mb.ToObject("Static", transform);
            // The resurfacing gang, shown only while the runway is shut: closed crosses at both ends, a paver and
            // rollers on a fresh black strip, lorries, cones across the turn-offs.
            var cb = new MeshBuilder { Smoothness = 0.3f };
            foreach (float e in new[] { -1f, 1f })
                foreach (float a in new[] { -45f, 45f })
                { cb.Push().Translate(Layout.RunwayX, 0.06f, e * (half - 12)).RotateY(a); cb.Box(Vector3.zero, new Vector3(1.1f, 0.02f, w * 0.9f), Palette.PaintYellow); cb.Pop(); }
            cb.Smoothness = 0.05f;
            cb.Box(new Vector3(Layout.RunwayX - w * 0.2f, 0.055f, -6), new Vector3(w * 0.4f, 0.02f, 46), Palette.Hex(0x15171A));
            cb.Smoothness = 0.4f;
            // Paver: a hopper, a body, the screed behind.
            cb.Push().Translate(Layout.RunwayX - w * 0.2f, 0.06f, 18);
            cb.Box(new Vector3(0, 0.9f, 0), new Vector3(2.2f, 1.2f, 3.4f), Palette.Hex(0xF07A1A)); cb.Box(new Vector3(0, 0.7f, 2.3f), new Vector3(2.6f, 0.9f, 1.4f), Palette.Hex(0x3A3F45));
            cb.Box(new Vector3(0, 0.3f, -2.0f), new Vector3(w * 0.4f, 0.3f, 0.6f), Palette.Hex(0x3A3F45)); cb.Box(new Vector3(0, 1.9f, -0.4f), new Vector3(1.8f, 0.08f, 1.6f), Palette.Hex(0xF07A1A));
            foreach (float ex in new[] { -1f, 1f }) cb.Box(new Vector3(ex * 0.9f, 0.3f, 0), new Vector3(0.4f, 0.6f, 3.0f), Palette.Tyre);
            cb.Pop();
            // Two rollers following it.
            foreach (float rz in new[] { 6f, -8f })
            {
                cb.Push().Translate(Layout.RunwayX - w * 0.2f + (rz > 0 ? 0.6f : -0.6f), 0.06f, rz);
                cb.Box(new Vector3(0, 1.0f, 0), new Vector3(1.5f, 0.8f, 1.6f), Palette.PaintYellow); cb.Box(new Vector3(0, 1.75f, 0), new Vector3(1.3f, 0.06f, 1.4f), Palette.PaintYellow);
                foreach (float ez in new[] { -1f, 1f }) cb.Cylinder(new Vector3(-0.75f, 0.5f, ez * 1.2f), new Vector3(0.75f, 0.5f, ez * 1.2f), 0.5f, 12, Palette.Hex(0x70767C));
                cb.Pop();
            }
            cb.Push().Translate(Layout.RunwayX + w * 0.2f, 0.05f, 30).RotateY(180); cb.Append(Gse.Lorry(Palette.Hex(0xF07A1A))); cb.Pop();
            cb.Push().Translate(Layout.RunwayX + w * 0.25f, 0.05f, -22); cb.Append(Gse.Lorry(Palette.Hex(0x70767C))); cb.Pop();
            cb.Smoothness = 0.3f;
            for (int k = 0; k < 7; k++) { Gse.Cone(cb, new Vector3(Layout.RunwayX + w * 0.5f + 1.5f, 0.05f, Layout.ExitZ(s) - 6 + k * 2)); Gse.Cone(cb, new Vector3(Layout.RunwayX + w * 0.5f + 1.5f, 0.05f, Layout.LineUpZ(s) - 6 + k * 2)); }
            cb.Glow = true;
            for (int k = 0; k < 6; k++) cb.Sphere(new Vector3(Layout.RunwayX + (k % 2 == 0 ? -1 : 1) * w * 0.45f, 0.5f, -24 + k * 10), Vector3.one * 0.28f, 6, Palette.Hex(0xFFB020));
            cb.Glow = false;
            _closed = cb.ToObject("RunwayWorks", _static.transform);
            _closed.SetActive(s.RunwayClosed);
            g.Build(_static.transform);
            kit.SetParent(_static.transform, false);
        }
    }
}
