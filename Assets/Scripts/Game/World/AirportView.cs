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
                + s.Level("catering") + "|" + s.Level("cleaning") + "|" + s.Level("checkin") + "|" + s.Level("security") + "|" + s.Level("gates") + "|" + s.Level("shops");
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
            mb.Box(new Vector3(20, -0.3f, 0), new Vector3(1400, 0.5f, 1400), Palette.Grass);
            mb.Box(new Vector3(Layout.RunwayX, -0.04f, 0), new Vector3(w + 14, 0.04f, half * 2 + 30), Palette.GrassDark);

            // Runway: grass strip, gravel, asphalt, then concrete with full markings.
            Color surface = rl <= 1 ? Palette.GrassWorn : rl <= 2 ? Palette.Gravel : rl <= 9 ? Palette.Asphalt : Palette.AsphaltPale;
            mb.Box(new Vector3(Layout.RunwayX, 0.0f, 0), new Vector3(w, 0.06f, half * 2), surface);
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
            mb.Box(new Vector3(Layout.TaxiX, 0.01f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(tw, 0.06f, Mathf.Max(exit, 46) - lineUp + tw), taxi);
            foreach (float z in new[] { lineUp, exit })
                mb.Box(new Vector3((Layout.RunwayX + Layout.TaxiX) * 0.5f, 0.01f, z), new Vector3(Layout.TaxiX - Layout.RunwayX, 0.06f, tw), taxi);
            if (tl >= 4) mb.Box(new Vector3(Layout.TaxiX, 0.05f, (lineUp + Mathf.Max(exit, 46)) * 0.5f), new Vector3(0.2f, 0.02f, Mathf.Max(exit, 46) - lineUp), Palette.PaintYellow);

            // Apron and stands.
            int cols = Layout.Columns(s);
            float apronW = termX - Layout.TaxiX + 2;
            mb.Box(new Vector3(Layout.TaxiX + apronW * 0.5f, 0.0f, 0), new Vector3(apronW, 0.05f, 112), s.StandCount <= 2 ? Palette.Gravel : Palette.Concrete);
            for (int i = 0; i < s.StandCount; i++)
            {
                var p = Layout.Stand(i);
                mb.Box(new Vector3(p.x - 3, 0.04f, p.z), new Vector3(12, 0.02f, 0.25f), Palette.PaintYellow);             // lead-in line
                mb.Box(new Vector3(p.x + 3.2f, 0.04f, p.z), new Vector3(0.3f, 0.02f, 3f), Palette.PaintYellow);            // stop bar
                mb.Box(new Vector3(p.x + 5.2f, 0.04f, p.z - 5.2f), new Vector3(1.6f, 0.02f, 1.6f), Palette.Paint);         // stand number pad
            }

            // Terminal: grows along the stands with the check-in, security and gate levels; shops add a glass front.
            int tLevel = Mathf.Min(s.Level("checkin"), Mathf.Min(s.Level("security"), s.Level("gates")));
            float tLen = Mathf.Min(108, 16 + tLevel * 6 + s.StandCount * 3f), tDepth = 9 + Mathf.Min(10, tLevel) * 0.9f, tH = 4 + Mathf.Min(6, tLevel / 3) * 1.6f;
            if (tLevel <= 1 && s.StandCount <= 1)
            {
                // The shed you inherit: boards and a tin roof.
                mb.Box(new Vector3(termX + 4, 1.5f, -45.5f), new Vector3(7, 3, 9), Palette.WoodPale);
                mb.GableRoof(new Vector3(termX + 4, 3f, -45.5f), new Vector3(7, 1.6f, 9), Palette.Roof, Palette.WoodPale, 0.5f);
            }
            else
            {
                Models.Block(mb, new Vector3(termX + tDepth * 0.5f, 0, -45.5f + tLen * 0.5f - 6), new Vector3(tDepth, tH, tLen), Palette.Concrete, Palette.ConcreteDark);
                if (s.Level("shops") > 0)
                {
                    mb.Smoothness = 0.85f;
                    mb.Box(new Vector3(termX + tDepth + 1.5f, tH * 0.4f, -45.5f + tLen * 0.5f - 6), new Vector3(3, tH * 0.8f, Mathf.Min(tLen, 10 + s.Level("shops") * 4)), Palette.Glass);
                    mb.Smoothness = 0.2f;
                }
                // Jet bridges from the terminal to the first column of stands once the gates are worth it.
                if (s.Level("gates") >= 4 && cols == 1)
                    for (int i = 0; i < Mathf.Min(s.StandCount, Layout.PerColumn); i++)
                        mb.Box(new Vector3(termX - 3.5f, 2.2f, Layout.Stand(i).z + 3.2f), new Vector3(7, 1.4f, 1.3f), Palette.Steel);
            }
            // Car park and road landside.
            float roadX = termX + tDepth + 9;
            mb.Box(new Vector3(roadX, 0.0f, 0), new Vector3(6, 0.05f, 130), Palette.Asphalt);
            mb.Box(new Vector3(roadX + 12, 0.0f, -30), new Vector3(14, 0.05f, 30 + tLevel * 3), Palette.AsphaltPale);

            // Service depots south of the apron, each in its colour, each growing with its level.
            float dx = Layout.TaxiX + 8;
            foreach (var (id, color) in new[] { ("fuel", Palette.Fuel), ("baggage", Palette.Baggage), ("catering", Palette.Catering), ("cleaning", Palette.Cleaning) })
            {
                int l = s.Level(id);
                if (l > 0)
                {
                    float size = 4 + Mathf.Min(8, l) * 0.5f;
                    if (id == "fuel")
                        for (int k = 0; k < 1 + Mathf.Min(3, l / 3); k++)
                        {
                            mb.Smoothness = 0.5f;
                            mb.Cylinder(new Vector3(dx + k * 3.2f, 0, -64), new Vector3(dx + k * 3.2f, 2.5f + l * 0.12f, -64), 1.4f, 1.4f, 12, Palette.White);
                            mb.Cylinder(new Vector3(dx + k * 3.2f, 1.2f, -64), new Vector3(dx + k * 3.2f, 1.7f, -64), 1.43f, 1.43f, 12, color);
                        }
                    else Models.Block(mb, new Vector3(dx + size * 0.5f, 0, -64), new Vector3(size, 3 + l * 0.1f, 6), color, Palette.ConcreteDark, false);
                }
                dx += 16;
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
            var rng = new System.Random(7);
            for (int i = 0; i < 520; i++)
            {
                float x = (float)rng.NextDouble() * 900 - 430, z = (float)rng.NextDouble() * 900 - 450;
                bool clear = x > Layout.RunwayX - 22 && x < roadX + 34 && z > -half - 55 && z < half + 55;
                if (clear) continue;
                float h = 3 + (float)rng.NextDouble() * 4;
                mb.Smoothness = 0.05f;
                mb.Cylinder(new Vector3(x, 0, z), new Vector3(x, h * 0.4f, z), 0.3f, 0.25f, 5, Palette.Wood);
                mb.Sphere(new Vector3(x, h * 0.75f, z), new Vector3(h * 0.38f, h * 0.45f, h * 0.38f), 6, i % 3 == 0 ? Palette.GrassDark : Palette.Hex(0x4E7A3A));
            }

            _static = mb.ToObject("Static", transform);
        }
    }
}
