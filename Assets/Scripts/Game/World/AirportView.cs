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

        void Update()
        {
            // The levels that shape the airport, as one key: rebuild only when it changes.
            var s = _sim;
            string key = s.Level("runway") + "|" + s.Level("stands") + "|" + s.Level("taxiways") + "|" + s.Level("tower") + "|" + s.Level("fuel") + "|" + s.Level("baggage") + "|"
                + s.Level("catering") + "|" + s.Level("cleaning") + "|" + s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.Level("shops") + "|" + s.Level("lounge") + "|" + s.Level("parking") + "|" + s.Level("hangar") + "|" + s.Level("cargo") + "|" + s.Level("ils") + "|" + s.Stars + "|" + s.State.map;
            if (key != _built) Rebuild();
            _built = key;
        }

        void Rebuild()
        {
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
            Color stripe = MeshBuilder.Darken(ground, 0.94f), tree = map == "dune" ? Palette.Hex(0x8A9A4A) : map == "polar" ? Palette.Hex(0x3E5A48) : Palette.Hex(0x4E7A3A);
            var g = new Ground();
            bool green = map == "meadow" || map == "alpine" || map == "metro";
            if (green) g.Rect("Grass", 20, 0, 1400, 1400, -0.05f); else mb.Box(new Vector3(20, -0.3f, 0), new Vector3(1400, 0.5f, 1400), ground);
            if (map == "coral") mb.Box(new Vector3(-420, -0.28f, 0), new Vector3(520, 0.5f, 1400), Palette.Hex(0x2E9CB4));     // the sea, west of the strip
            if (green) g.Rect("Grass", Layout.RunwayX, 0, w + 16, half * 2 + 34, -0.03f, 0, new Color(0.82f, 0.86f, 0.78f)); else mb.Box(new Vector3(Layout.RunwayX, -0.04f, 0), new Vector3(w + 14, 0.04f, half * 2 + 30), MeshBuilder.Darken(ground, 0.86f));
            for (int k = -14; k <= 14; k++)
                if (green) g.Rect("Grass", 20, k * 44f, 1400, 22, -0.04f, 0, new Color(0.9f, 0.93f, 0.88f)); else mb.Box(new Vector3(20, -0.045f, k * 44f), new Vector3(1400, 0.02f, 22), stripe);        // mowing stripes

            // Runway: grass strip, gravel, asphalt, then concrete with full markings.
            Color surface = rl <= 1 ? Palette.GrassWorn : rl <= 2 ? Palette.Gravel : rl <= 9 ? Palette.Asphalt : Palette.AsphaltPale;
            if (rl >= 3) { g.Rect("Asphalt", Layout.RunwayX, 0, w, half * 2, 0.03f); g.Rect("Tarmac", Layout.RunwayX, 0, w + 3, half * 2 + 6, 0.015f, 0, new Color(0.8f, 0.8f, 0.8f)); }
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
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(Layout.RunwayX + sx * (w * 0.5f + 0.6f), 0.15f, z), new Vector3(0.25f, 0.3f, 0.25f), Palette.PaintYellow);

            // Taxiway and its links to the runway.
            int tl = s.Level("taxiways");
            Color taxi = tl == 0 ? Palette.Dirt : tl < 4 ? Palette.Gravel : Palette.Asphalt;
            float tw = 4.5f + Mathf.Min(6, tl) * 0.35f;
            float lineUp = Layout.LineUpZ(s), exit = Layout.ExitZ(s);
            if (tl >= 4) g.Rect("Tarmac", Layout.TaxiX, (lineUp + Mathf.Max(exit, 46)) * 0.5f, tw, Mathf.Max(exit, 46) - lineUp + tw, 0.035f);
            else mb.Box(new Vector3(Layout.TaxiX, 0.01f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(tw, 0.06f, Mathf.Max(exit, 46) - lineUp + tw), taxi);
            foreach (float z in new[] { lineUp, exit })
                if (tl >= 4) g.Rect("Tarmac", (Layout.RunwayX + Layout.TaxiX) * 0.5f, z, Layout.TaxiX - Layout.RunwayX, tw, 0.035f); else mb.Box(new Vector3((Layout.RunwayX + Layout.TaxiX) * 0.5f, 0.01f, z), new Vector3(Layout.TaxiX - Layout.RunwayX, 0.06f, tw), taxi);
            if (tl >= 4) mb.Box(new Vector3(Layout.TaxiX, 0.05f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(0.2f, 0.02f, Mathf.Max(exit, 46) - lineUp), Palette.PaintYellow);

            // Apron and stands.
            int cols = Layout.Columns(s);
            float apronW = termX - Layout.TaxiX + 2;
            if (s.StandCount <= 2) mb.Box(new Vector3(Layout.TaxiX + apronW * 0.5f, 0.0f, 0), new Vector3(apronW, 0.05f, 112), Palette.Gravel);
            else g.Rect("Concrete", Layout.TaxiX + apronW * 0.5f, 0, apronW, 112, 0.025f);
            bool paved = s.StandCount > 2;
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
                // The stand's number board: yellow on black at the lane.
                mb.Box(new Vector3(p.x - 9.6f, y, p.z + 1.6f), new Vector3(1.8f, 0.02f, 1.4f), Palette.Black);
                mb.Box(new Vector3(p.x - 9.6f, y + 0.005f, p.z + 1.6f), new Vector3(1.2f, 0.02f, 0.8f), Palette.PaintYellow);
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
                mb.Smoothness = 0.3f;
                for (float z = c.z - tLen * 0.5f + 1.2f; z <= c.z + tLen * 0.5f - 1.1f; z += 3f)
                    foreach (float side in new[] { -1f, 1f })
                        mb.Box(new Vector3(c.x + side * (tDepth * 0.5f + 0.12f), tH * 0.52f, z), new Vector3(0.18f, tH * 0.74f, 0.22f), Palette.White);
                // The roof: a pale membrane (textured), a parapet round its edge, plant on top.
                g.Rect("Tarmac", c.x, c.z, tDepth + 2.2f, tLen + 1.4f, tH + 0.52f, 0, new Color(2.3f, 2.3f, 2.35f));
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
                // Skylights along the roof; the shopping concourse is a glass atrium that lengthens with its level.
                mb.Smoothness = 0.92f;
                for (float z = c.z - tLen * 0.5f + 3; z < c.z + tLen * 0.5f - 3; z += 4.5f)
                    mb.Box(new Vector3(c.x - tDepth * 0.22f, tH + 0.55f, z), new Vector3(tDepth * 0.28f, 0.14f, 2.6f), Palette.Hex(0x5E9CBE));
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
                        mb.Box(new Vector3((termX + p.x + 5) * 0.5f, 2.6f, p.z + 3.4f), new Vector3(termX - p.x - 5, 1.5f, 1.4f), Palette.Hex(0xDADDE0), Palette.Steel);
                        mb.Cylinder(new Vector3(p.x + 5.4f, 0, p.z + 3.4f), new Vector3(p.x + 5.4f, 1.9f, p.z + 3.4f), 0.3f, 8, Palette.SteelDark);
                        mb.Cylinder(new Vector3(p.x + 5.2f, 1.8f, p.z + 3.4f), new Vector3(p.x + 5.2f, 3.5f, p.z + 3.4f), 1.1f, 1.1f, 10, Palette.Hex(0xDADDE0));
                    }
                }
            }

            // Landside: the road, the car park (cars by its level), and the town beyond, which grows with the stars.
            float roadX = termX + tDepth + 12;
            RoadX = roadX;
            mb.Smoothness = 0.08f;
            g.Rect("Tarmac", roadX, 0, 7, 420, 0.03f);
            for (float z = -200; z < 200; z += 8) mb.Box(new Vector3(roadX, 0.04f, z), new Vector3(0.2f, 0.02f, 3.5f), Palette.Paint);
            int park = s.Level("parking");
            float lotW = 16 + Mathf.Min(6, park) * 3, lotL = 26 + Mathf.Min(10, park) * 5;
            var lot = new Vector3(roadX + 5 + lotW * 0.5f, 0, -46 + lotL * 0.5f);
            g.Rect("Tarmac", lot.x, lot.z, lotW, lotL, 0.03f, 0, new Color(1.25f, 1.25f, 1.25f));
            var kit = new GameObject("Kit").transform;
            _kitRoot = kit;
            var rng = new System.Random(11);
            string[] cars = { "sedan", "suv", "hatchback", "taxi", "van", "sedan", "suv" };
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
            string[] town = { "shop_a", "shop_b", "shop_c", "shop_d", "shop_e", "shop_f", "shop_g", "shop_h", "shop_i", "shop_j", "shop_k", "shop_l", "shop_m", "shop_n" };
            string[] high = { "tower_a", "tower_b", "tower_c", "tower_d", "tower_e" };
            int blocks = 6 + s.Stars * 5;
            for (int i = 0; i < blocks; i++)
            {
                float bx = roadX + lotW + 22 + (i % 5) * 17 + (float)rng.NextDouble() * 4, bz = -150 + (i / 5) * 24 + (float)rng.NextDouble() * 6;
                bool tall = s.Stars >= 4 && rng.NextDouble() < 0.12 + s.Stars * 0.03;
                if (tall) Models.Kit(high[rng.Next(high.Length)], kit, new Vector3(bx, 0, bz), rng.Next(4) * 90, 26 + rng.Next(22));
                else Models.Kit(town[rng.Next(town.Length)], kit, new Vector3(bx, 0, bz), rng.Next(4) * 90, 11 + rng.Next(5));
            }

            // Service depots south of the apron: a works building each, tanks for the fuel farm.
            float dx = Layout.TaxiX + 10;
            foreach (var (id, model) in new[] { ("fuel", "works_a"), ("baggage", "works_d"), ("catering", "works_h"), ("cleaning", "works_m") })
            {
                int l = s.Level(id);
                DepotAt[id] = new Vector3(dx, 0, -62);
                if (l > 0)
                {
                    float size = 7 + Mathf.Min(8, l) * 0.5f;
                    if (id == "fuel")
                    {
                        for (int k = 0; k < 1 + Mathf.Min(3, l / 3); k++) Models.Kit(l >= 6 ? "tank_large" : "tank", kit, new Vector3(dx + k * 4.5f - 2, 0, -70), 0, l >= 6 ? 5.5f : 4f);
                        Models.Kit("works_s", kit, new Vector3(dx - 7, 0, -70), 90, 6);
                    }
                    else Models.Kit(model, kit, new Vector3(dx, 0, -71), 0, size);
                    g.Rect("Concrete", dx, -66, 15, 17, 0.03f, 0, new Color(0.85f, 0.85f, 0.85f));
                }
                dx += 19;
            }
            // North of the apron: the hangar and the cargo terminal.
            if (s.Level("hangar") > 0)
            {
                float hw = 18 + Mathf.Min(10, s.Level("hangar")) * 1.2f;
                var hp = new Vector3(Layout.TaxiX + 18, 0, 78);
                mb.Smoothness = 0.35f;
                mb.Box(hp + Vector3.up * 4, new Vector3(hw, 8, 20), Palette.Hex(0xC8CCD0), Palette.Steel);
                mb.Cylinder(hp + new Vector3(-hw * 0.5f, 8, 0), hp + new Vector3(hw * 0.5f, 8, 0), 10f, 10f, 14, Palette.Hex(0xAEB4BA));
                mb.Box(hp + new Vector3(0, 3.2f, -10.1f), new Vector3(hw * 0.8f, 6.4f, 0.2f), Palette.Hex(0x38424C));
                g.Rect("Concrete", hp.x, hp.z - 16, hw, 12, 0.03f);
            }
            if (s.Level("cargo") > 0)
            {
                var cp = new Vector3(Layout.TaxiX + 52, 0, 76);
                Models.Kit("works_q", kit, cp, 180, 14);
                string[] boxes = { "container_a", "container_b", "container_c" };
                for (int k = 0; k < Mathf.Min(14, 3 + s.Level("cargo")); k++)
                    Models.Kit(boxes[k % 3], kit, cp + new Vector3(12 + (k % 4) * 3.2f, (k / 8) * 2.6f, -6 + (k / 4 % 2) * 7.5f), 0, 6);
                g.Rect("Concrete", cp.x + 8, cp.z - 2, 40, 22, 0.03f, 0, new Color(0.85f, 0.85f, 0.85f));
            }
            if (s.Level("ils") > 0)
            {
                // Approach lights off the south end, and the radar by the tower.
                for (int k = 1; k <= 4 + s.Level("ils"); k++)
                    mb.Box(new Vector3(Layout.RunwayX, 0.4f, -half - k * 7), new Vector3(k % 3 == 0 ? 6 : 0.4f, 0.8f, 0.4f), Palette.PaintYellow);
            }

            // Tower.
            int tower = s.Level("tower");
            float th = 6 + tower * 2.2f;
            var tp = new Vector3(Layout.TaxiX + 4, 0, 58);
            mb.Smoothness = 0.2f;
            mb.Cylinder(tp, tp + Vector3.up * th, 1.5f, 1.2f, 10, Palette.Concrete);
            mb.Smoothness = 0.85f;
            mb.Cylinder(tp + Vector3.up * th, tp + Vector3.up * (th + 2.2f), 2.6f, 3.0f, 10, Palette.Glass);
            mb.Smoothness = 0.2f;
            mb.Cylinder(tp + Vector3.up * (th + 2.2f), tp + Vector3.up * (th + 2.5f), 3.2f, 3.2f, 10, Palette.SteelDark);

            // A windsock by the runway: every airfield has one.
            var ws = new Vector3(Layout.RunwayX + w * 0.5f + 5, 0, -half + 20);
            mb.Cylinder(ws, ws + Vector3.up * 5, 0.1f, 0.08f, 6, Palette.Steel);
            mb.Cylinder(ws + Vector3.up * 4.8f, ws + new Vector3(0, 4.5f, 2.6f), 0.45f, 0.15f, 8, Palette.Fuel);

            // Trees round the edge so the field sits in a landscape.
            rng = new System.Random(7);
            for (int i = 0; i < (map == "dune" || map == "polar" ? 120 : 520); i++)
            {
                float x = (float)rng.NextDouble() * 900 - 430, z = (float)rng.NextDouble() * 900 - 450;
                bool clear = x > Layout.RunwayX - 22 && x < roadX + 150 && z > -half - 85 && z < half + 110;
                if (clear || (map == "coral" && x < -150)) continue;
                float h = 3 + (float)rng.NextDouble() * 4;
                mb.Smoothness = 0.05f;
                mb.Cylinder(new Vector3(x, 0, z), new Vector3(x, h * 0.4f, z), 0.3f, 0.25f, 5, Palette.Wood);
                mb.Sphere(new Vector3(x, h * 0.75f, z), new Vector3(h * 0.38f, h * 0.45f, h * 0.38f), 6, i % 3 == 0 ? MeshBuilder.Darken(tree, 0.85f) : tree);
            }

            _static = mb.ToObject("Static", transform);
            g.Build(_static.transform);
            kit.SetParent(_static.transform, false);
        }
    }
}
