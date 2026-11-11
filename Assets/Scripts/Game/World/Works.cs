using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The working buildings of the airfield, built from code: a depot for each service department (its shed, its yard
    /// and what stands in it), the maintenance hangar and the cargo terminal. Fronts face +Z, toward the apron.
    /// </summary>
    public static class Works
    {
        static readonly Color Clad = Palette.Hex(0xD5D9DD), CladDark = Palette.Hex(0xAEB5BC), Dark = Palette.Hex(0x2B3138), Steel = Palette.Hex(0xB9BEC4), Door = Palette.Hex(0x4A535C), Glass = Palette.Hex(0x6FA6C2);

        /// <summary>A clad shed: profiled walls, a low gable roof, a colour band at the eaves. Centre of its floor at c.</summary>
        static void Shed(MeshBuilder mb, Vector3 c, float w, float d, float h, Color band, Color roof)
        {
            mb.Mat(0.3f);
            mb.Box(c + Vector3.up * h * 0.5f, new Vector3(w, h, d), Clad);
            mb.Box(c + Vector3.up * 0.25f, new Vector3(w + 0.06f, 0.5f, d + 0.06f), Palette.Hex(0x8C9298));                 // plinth
            // Cladding ribs on the front and the back.
            for (float x = -w * 0.5f + 0.5f; x < w * 0.5f; x += 1.0f) foreach (float e in new[] { -1f, 1f }) mb.Box(c + new Vector3(x, h * 0.5f + 0.2f, e * (d * 0.5f + 0.01f)), new Vector3(0.06f, h - 0.5f, 0.03f), CladDark);
            mb.Box(c + Vector3.up * (h - 0.3f), new Vector3(w + 0.08f, 0.5f, d + 0.08f), band);
            mb.Mat(0.45f, 0.3f);
            mb.GableRoof(c + Vector3.up * h, new Vector3(w, Mathf.Max(0.7f, d * 0.12f), d), roof, Clad, 0.35f);
            mb.Mat(0.3f);
            // Downpipes at the front corners.
            foreach (float e in new[] { -1f, 1f }) mb.Box(c + new Vector3(e * (w * 0.5f - 0.15f), h * 0.5f, d * 0.5f + 0.06f), new Vector3(0.1f, h, 0.1f), CladDark);
        }

        /// <summary>A roller door on a front at z, its slats, and a lamp over it; open ones show the dark inside.</summary>
        static void Roller(MeshBuilder mb, Vector3 foot, float w, float h, bool open = false)
        {
            mb.Box(foot + new Vector3(0, h * 0.5f, 0.02f), new Vector3(w + 0.2f, h + 0.1f, 0.06f), Dark);
            if (open) mb.Box(foot + new Vector3(0, h * 0.5f, 0.04f), new Vector3(w, h, 0.04f), Palette.Hex(0x14181C));
            int slats = (int)((open ? h * 0.25f : h) / 0.22f);
            for (int k = 0; k < slats; k++) mb.Box(foot + new Vector3(0, h - 0.11f - k * 0.22f, 0.06f), new Vector3(w, 0.18f, 0.04f), Door);
            mb.Glow = true; mb.Box(foot + new Vector3(0, h + 0.3f, 0.1f), new Vector3(0.5f, 0.1f, 0.14f), Palette.Hex(0xFFE9B0)); mb.Glow = false;
        }

        static void Window(MeshBuilder mb, Vector3 at, float w, float h)
        {
            mb.Box(at + new Vector3(0, 0, 0.02f), new Vector3(w + 0.12f, h + 0.12f, 0.05f), Dark);
            mb.Mat(0.92f); mb.Box(at + new Vector3(0, 0, 0.04f), new Vector3(w, h, 0.04f), Glass); mb.Mat(0.3f);
        }

        /// <summary>An upright storage tank: shell, a shallow dome, a rail round the top and a stair up the side.</summary>
        static void Tank(MeshBuilder mb, Vector3 foot, float r, float h, Color shell, Color band)
        {
            mb.Mat(0.6f, 0.4f);
            mb.Cylinder(foot, foot + Vector3.up * h, r, r, 18, shell);
            mb.Sphere(foot + Vector3.up * h, new Vector3(r, r * 0.22f, r), 12, shell);
            mb.Mat(0.35f);
            mb.Cylinder(foot + Vector3.up * (h * 0.62f), foot + Vector3.up * (h * 0.62f + 0.45f), r + 0.02f, r + 0.02f, 18, band, false);
            mb.Cylinder(foot + Vector3.up * (h - 0.05f), foot + Vector3.up * (h + 0.03f), r + 0.08f, r + 0.08f, 18, Steel, false);
            // A stair spiralling a quarter of the way round.
            int steps = (int)(h / 0.35f);
            for (int k = 0; k < steps; k++)
            {
                float a = (k / (float)steps) * 100f * Mathf.Deg2Rad + 0.6f;
                mb.Push().Translate(foot + new Vector3(Mathf.Cos(a) * (r + 0.22f), 0.2f + k * (h - 0.2f) / steps, Mathf.Sin(a) * (r + 0.22f))).RotateY(-a * Mathf.Rad2Deg);
                mb.Box(Vector3.zero, new Vector3(0.44f, 0.05f, 0.3f), Steel); mb.Box(new Vector3(0.2f, 0.4f, 0), new Vector3(0.03f, 0.8f, 0.03f), Palette.PaintYellow);
                mb.Pop();
            }
        }

        static void Pipe(MeshBuilder mb, Vector3 a, Vector3 b, float r, Color c) { mb.Mat(0.6f, 0.4f); mb.Cylinder(a, b, r, 8, c); mb.Mat(0.3f); }

        static void Bollards(MeshBuilder mb, Vector3 a, Vector3 b, int n)
        {
            for (int k = 0; k < n; k++) { var p = Vector3.Lerp(a, b, n == 1 ? 0.5f : k / (float)(n - 1)); mb.Cylinder(p, p + Vector3.up * 0.8f, 0.09f, 6, Palette.PaintYellow); mb.Cylinder(p + Vector3.up * 0.55f, p + Vector3.up * 0.68f, 0.095f, 6, Dark, false); }
        }

        /// <summary>A department's depot on its pad: the shed at <paramref name="at"/> (front toward +Z), growing with its level.</summary>
        public static void Depot(MeshBuilder mb, string id, Vector3 at, int level)
        {
            float grow = Mathf.Min(8, level) * 0.35f;
            float w = 9f + grow, d = 6f, h = 4.2f + Mathf.Min(8, level) * 0.12f;
            float front = at.z + d * 0.5f;
            switch (id)
            {
                case "fuel":
                {
                    // The fuel farm: tanks in a bunded yard, a pump house, pipes to a loading gantry where a bowser fills.
                    int tanks = 1 + Mathf.Min(3, level / 3);
                    float r = level >= 6 ? 1.9f : 1.5f, th = level >= 6 ? 5.6f : 4.4f;
                    float yardW = tanks * (r * 2 + 0.9f) + 0.8f;
                    var yard = at + new Vector3(1.5f, 0, -0.4f);
                    mb.Box(yard + new Vector3(0, 0.04f, 0), new Vector3(yardW, 0.08f, r * 2 + 1.6f), Palette.Hex(0x9A9488));
                    foreach (float e in new[] { -1f, 1f }) { mb.Box(yard + new Vector3(0, 0.35f, e * (r + 0.8f)), new Vector3(yardW, 0.7f, 0.18f), Palette.Hex(0xC9C5BB)); mb.Box(yard + new Vector3(e * yardW * 0.5f, 0.35f, 0), new Vector3(0.18f, 0.7f, r * 2 + 1.6f), Palette.Hex(0xC9C5BB)); }
                    for (int k = 0; k < tanks; k++)
                    {
                        var tp = yard + new Vector3((k - (tanks - 1) * 0.5f) * (r * 2 + 0.9f), 0.08f, 0);
                        Tank(mb, tp, r, th, Palette.Hex(0xE6E9EC), Palette.Fuel);
                        Pipe(mb, tp + new Vector3(0, 0.5f, r), tp + new Vector3(0, 0.5f, r + 1.5f), 0.12f, Palette.Hex(0xB3261E));
                    }
                    Pipe(mb, yard + new Vector3(-yardW * 0.5f - 2.5f, 0.5f, r + 1.5f), yard + new Vector3(yardW * 0.5f - 1f, 0.5f, r + 1.5f), 0.12f, Palette.Hex(0xB3261E));
                    // Pump house.
                    var ph = at + new Vector3(-yardW * 0.5f - 1.6f, 0, -1.2f);
                    mb.Box(ph + Vector3.up * 1.3f, new Vector3(2.6f, 2.6f, 3f), Clad, CladDark); mb.Box(ph + Vector3.up * 2.45f, new Vector3(2.66f, 0.3f, 3.06f), Palette.Fuel);
                    Roller(mb, ph + new Vector3(0, 0, 1.5f), 1.4f, 2.0f);
                    Pipe(mb, ph + new Vector3(0, 0.5f, 1.5f), ph + new Vector3(0, 0.5f, r + 2.7f), 0.12f, Palette.Hex(0xB3261E));
                    // Loading gantry: a canopy on four posts over the fill lane, two loading arms.
                    var gy = at + new Vector3(-yardW * 0.5f - 1.6f, 0, 5.6f);
                    foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(gy + new Vector3(ex * 1.9f, 1.9f, ez * 2.4f), new Vector3(0.16f, 3.8f, 0.16f), Steel);
                    mb.Box(gy + Vector3.up * 3.9f, new Vector3(4.6f, 0.2f, 5.6f), Palette.Hex(0xE6E9EC)); mb.Box(gy + Vector3.up * 3.75f, new Vector3(4.66f, 0.2f, 5.66f), Palette.Fuel);
                    foreach (float ez in new[] { -1f, 1f }) { Pipe(mb, gy + new Vector3(1.9f, 0.3f, ez * 1.2f), gy + new Vector3(1.9f, 2.6f, ez * 1.2f), 0.08f, Palette.Hex(0xB3261E)); Pipe(mb, gy + new Vector3(1.9f, 2.6f, ez * 1.2f), gy + new Vector3(0.9f, 2.2f, ez * 1.2f), 0.06f, Steel); }
                    Bollards(mb, gy + new Vector3(-2.3f, 0, -2.6f), gy + new Vector3(-2.3f, 0, 2.6f), 4);
                    // Hazard sign and an earthing reel.
                    mb.Box(yard + new Vector3(yardW * 0.5f - 0.6f, 1.4f, r + 0.92f), new Vector3(0.9f, 0.7f, 0.05f), Palette.PaintYellow);
                    mb.Box(yard + new Vector3(yardW * 0.5f - 0.6f, 1.4f, r + 0.95f), new Vector3(0.12f, 0.4f, 0.03f), Dark);
                    break;
                }
                case "baggage":
                {
                    // The baggage hall: a long shed, a belt running out under a canopy with bags on it, dollies waiting.
                    Shed(mb, at, w, d, h, Palette.Baggage, Palette.Hex(0x7E8A96));
                    Roller(mb, new Vector3(at.x - w * 0.28f, 0, front), 2.6f, 2.8f, true); Roller(mb, new Vector3(at.x + w * 0.28f, 0, front), 2.6f, 2.8f);
                    Window(mb, new Vector3(at.x, 3.1f, front), 1.6f, 0.7f);
                    var belt = new Vector3(at.x - w * 0.28f, 0, front);
                    mb.Box(belt + new Vector3(0, 0.6f, 2.4f), new Vector3(1.1f, 0.14f, 4.8f), Dark);
                    foreach (float z in new[] { 0.6f, 2.4f, 4.2f }) foreach (float e in new[] { -1f, 1f }) mb.Box(belt + new Vector3(e * 0.5f, 0.3f, z), new Vector3(0.08f, 0.6f, 0.08f), Steel);
                    Color[] bags = { Palette.Hex(0x2D4E7C), Palette.Hex(0x8E2F2F), Palette.Hex(0x3C3C40), Palette.Hex(0x2F7A5B), Palette.Hex(0xC98A2C) };
                    for (int k = 0; k < 6; k++) mb.Box(belt + new Vector3((k % 2) * 0.16f - 0.08f, 0.82f, 0.5f + k * 0.75f), new Vector3(0.5f, 0.3f, 0.6f), bags[k % bags.Length]);
                    for (int k = 0; k < Mathf.Min(4, 1 + level / 2); k++)
                    {
                        var cp = new Vector3(at.x + w * 0.1f + k * 1.5f, 0, front + 3.2f);
                        mb.Box(cp + Vector3.up * 0.4f, new Vector3(1.1f, 0.1f, 1.7f), Palette.Hex(0x4A535C));
                        foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) { mb.Box(cp + new Vector3(ex * 0.5f, 0.95f, ez * 0.8f), new Vector3(0.05f, 1.0f, 0.05f), Steel); mb.Cylinder(cp + new Vector3(ex * 0.5f, 0.16f, ez * 0.55f), cp + new Vector3(ex * 0.62f, 0.16f, ez * 0.55f), 0.16f, 8, Palette.Tyre); }
                        mb.Box(cp + Vector3.up * 1.47f, new Vector3(1.14f, 0.05f, 1.74f), k % 2 == 0 ? Palette.Baggage : Palette.Hex(0xD7DBDF));
                    }
                    break;
                }
                case "catering":
                {
                    // The flight kitchen: a raised dock under a canopy, chiller units and flues on the roof, trolleys on the dock.
                    Shed(mb, at, w, d, h, Palette.Catering, Palette.Hex(0x8A8F96));
                    mb.Box(new Vector3(at.x, 0.55f, front + 0.9f), new Vector3(w * 0.8f, 1.1f, 1.8f), Palette.Hex(0xB8BCC0), Palette.Hex(0x9A9EA2));
                    for (int k = 0; k < 3; k++) Roller(mb, new Vector3(at.x + (k - 1) * w * 0.26f, 1.1f, front), 1.7f, 2.2f, k == 1);
                    mb.Box(new Vector3(at.x, 3.7f, front + 1.1f), new Vector3(w * 0.84f, 0.12f, 2.3f), Palette.Catering);
                    foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(at.x + e * w * 0.4f, 2.4f, front + 2.1f), new Vector3(0.1f, 2.6f, 0.1f), Steel);
                    for (int k = 0; k < 4; k++) { var tp = new Vector3(at.x - w * 0.3f + k * 0.7f, 1.1f, front + 1.2f); mb.Mat(0.7f, 0.5f); mb.Box(tp + Vector3.up * 0.55f, new Vector3(0.4f, 1.0f, 0.8f), Steel); mb.Mat(0.3f); }
                    for (int k = 0; k < 2 + Mathf.Min(2, level / 3); k++) { var up = at + new Vector3(-w * 0.3f + k * 1.9f, h + 0.75f, -1.2f); mb.Box(up, new Vector3(1.4f, 0.8f, 1.2f), Palette.Hex(0xC4C9CE)); mb.Cylinder(up + Vector3.up * 0.4f, up + Vector3.up * 0.46f, 0.42f, 10, Dark); }
                    foreach (float e in new[] { 0f, 0.7f }) { mb.Mat(0.7f, 0.5f); mb.Cylinder(at + new Vector3(w * 0.36f, h, 1.2f - e), at + new Vector3(w * 0.36f, h + 2.2f, 1.2f - e), 0.2f, 8, Steel); mb.Mat(0.3f); }
                    break;
                }
                case "cleaning":
                {
                    // The cabin services store: a smaller shed, laundry cages, a water tank on a stand, a wash bay.
                    Shed(mb, at, w * 0.8f, d, h * 0.9f, Palette.Cleaning, Palette.Hex(0x86929A));
                    Roller(mb, new Vector3(at.x - w * 0.14f, 0, front), 2.4f, 2.6f); Window(mb, new Vector3(at.x + w * 0.2f, 1.8f, front), 1.4f, 1.0f);
                    mb.Box(new Vector3(at.x + w * 0.2f, 1.0f, front + 0.02f), new Vector3(0.9f, 2.0f, 0.05f), Palette.Cleaning);
                    var wt = new Vector3(at.x + w * 0.52f, 0, at.z);
                    foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(wt + new Vector3(ex * 0.7f, 1.0f, ez * 0.7f), new Vector3(0.1f, 2.0f, 0.1f), Steel);
                    mb.Mat(0.5f, 0.2f); mb.Cylinder(wt + Vector3.up * 2.0f, wt + Vector3.up * 3.6f, 0.95f, 0.95f, 14, Palette.Hex(0x4AA7C8)); mb.Mat(0.3f);
                    for (int k = 0; k < 4; k++) { var cg = new Vector3(at.x - w * 0.38f + (k % 2) * 0.9f, 0, front + 1.0f + (k / 2) * 0.9f); mb.Box(cg + Vector3.up * 0.55f, new Vector3(0.7f, 0.9f, 0.7f), k % 2 == 0 ? Palette.Hex(0xE6E9EC) : Palette.Hex(0x9FD3E8)); foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(cg + new Vector3(ex * 0.36f, 0.6f, ez * 0.36f), new Vector3(0.03f, 1.2f, 0.03f), Steel); }
                    break;
                }
                case "pushback":
                {
                    // The tug shed: open-fronted bays, tow bars racked on the wall, a spare tug in one bay, a diesel tank.
                    float sw = w * 0.9f;
                    mb.Mat(0.3f);
                    mb.Box(new Vector3(at.x, h * 0.45f, at.z - d * 0.5f + 0.1f), new Vector3(sw, h * 0.9f, 0.2f), Clad);
                    foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(at.x + e * sw * 0.5f, h * 0.45f, at.z), new Vector3(0.2f, h * 0.9f, d), Clad);
                    mb.Box(new Vector3(at.x, 0.03f, at.z), new Vector3(sw, 0.05f, d), Palette.Hex(0x70767C));
                    for (int k = 0; k <= 3; k++) mb.Box(new Vector3(at.x - sw * 0.5f + k * sw / 3f, h * 0.45f, front - 0.1f), new Vector3(0.22f, h * 0.9f, 0.22f), Palette.Pushback);
                    mb.Push().Translate(at.x, h * 0.9f + 0.25f, at.z).Rotate(Quaternion.Euler(-4, 0, 0)); mb.Mat(0.45f, 0.3f); mb.Box(Vector3.zero, new Vector3(sw + 0.6f, 0.14f, d + 0.8f), Palette.Hex(0x7E8A96)); mb.Mat(0.3f); mb.Pop();
                    mb.Box(new Vector3(at.x, h * 0.9f - 0.1f, front - 0.1f), new Vector3(sw + 0.1f, 0.4f, 0.24f), Palette.Pushback);
                    for (int k = 0; k < 3; k++) { Pipe(mb, new Vector3(at.x - sw * 0.3f, 1.2f + k * 0.4f, at.z - d * 0.5f + 0.3f), new Vector3(at.x + sw * 0.05f, 1.2f + k * 0.4f, at.z - d * 0.5f + 0.3f), 0.05f, Palette.Hex(0xD9D9D9)); }
                    mb.Push().Translate(at.x + sw * 0.3f, 0.05f, at.z + 0.2f).Scale(0.92f); mb.Append(Gse.Build(Service.Pushback)); mb.Pop();
                    var dt = new Vector3(at.x - sw * 0.5f - 1.1f, 0, at.z - 1f);
                    mb.Mat(0.5f, 0.3f); mb.Cylinder(dt + new Vector3(0, 0.9f, -1.1f), dt + new Vector3(0, 0.9f, 1.1f), 0.6f, 12, Palette.Hex(0x3E7A4A)); mb.Mat(0.3f);
                    foreach (float e in new[] { -1f, 1f }) mb.Box(dt + new Vector3(0, 0.2f, e * 0.7f), new Vector3(1.0f, 0.4f, 0.2f), Dark);
                    break;
                }
                case "deice":
                {
                    // The de-icing pad's plant: glycol tanks with an orange band, a heater skid, hoses, a wind sock.
                    Shed(mb, at + new Vector3(-w * 0.2f, 0, 0), w * 0.55f, d, h * 0.85f, Palette.Deice, Palette.Hex(0x7E8A96));
                    Roller(mb, new Vector3(at.x - w * 0.2f, 0, front), 2.6f, 2.6f);
                    for (int k = 0; k < 2 + Mathf.Min(1, level / 4); k++) Tank(mb, at + new Vector3(w * 0.22f + k * 2.6f, 0, -0.8f), 1.1f, 3.8f + k * 0.3f, Palette.Hex(0xE6E9EC), Palette.Hex(0xF07A1A));
                    var sk = at + new Vector3(w * 0.3f, 0, 2.2f);
                    mb.Box(sk + Vector3.up * 0.15f, new Vector3(3.2f, 0.2f, 1.4f), Dark);
                    mb.Mat(0.6f, 0.4f); mb.Box(sk + new Vector3(-0.8f, 0.8f, 0), new Vector3(1.2f, 1.1f, 1.0f), Palette.Hex(0xF07A1A)); mb.Cylinder(sk + new Vector3(0.6f, 0.75f, -0.4f), sk + new Vector3(0.6f, 0.75f, 0.4f), 0.5f, 12, Steel); mb.Mat(0.3f);
                    Pipe(mb, sk + new Vector3(0.6f, 0.75f, -0.4f), at + new Vector3(w * 0.22f, 0.75f, 0.3f), 0.08f, Palette.Deice);
                    var ws = at + new Vector3(w * 0.55f, 0, 3.4f);
                    mb.Cylinder(ws, ws + Vector3.up * 4.5f, 0.05f, 6, Steel);
                    mb.Push().Translate(ws + Vector3.up * 4.4f).Rotate(Quaternion.Euler(0, 30, -12)); mb.Cylinder(Vector3.zero, new Vector3(1.4f, 0, 0), 0.24f, 0.1f, 8, Palette.Hex(0xF07A1A), false); mb.Cylinder(new Vector3(0.45f, 0, 0), new Vector3(0.9f, 0, 0), 0.2f, 0.15f, 8, Palette.White, false); mb.Pop();
                    break;
                }
            }
            mb.Mat(0.3f);
        }

        /// <summary>The maintenance hangar: an arched roof on ribs, tall sliding doors standing half open (front toward -Z, the apron), an annex of workshops.</summary>
        public static void Hangar(MeshBuilder mb, Vector3 at, float w)
        {
            float d = 20f, wall = 6.5f, rise = 4.5f;
            mb.Mat(0.3f);
            mb.Box(at + new Vector3(0, wall * 0.5f, 0), new Vector3(w, wall, d), Clad);
            // The vault: a flattened barrel, with ribs standing proud of it.
            mb.Mat(0.5f, 0.35f);
            mb.Push().Translate(at + Vector3.up * wall).Scale(new Vector3(1, rise / (w * 0.5f), 1));
            mb.Cylinder(new Vector3(0, 0, -d * 0.5f), new Vector3(0, 0, d * 0.5f), w * 0.5f, w * 0.5f, 24, Palette.Hex(0xBFC6CC));
            for (float z = -d * 0.5f + 0.3f; z <= d * 0.5f; z += 3.2f) mb.Cylinder(new Vector3(0, 0, z - 0.15f), new Vector3(0, 0, z + 0.15f), w * 0.5f + 0.18f, w * 0.5f + 0.18f, 24, Palette.Hex(0x8E979F));
            mb.Pop();
            mb.Mat(0.3f);
            // The door wall: a dark opening, four leaves on tracks, two slid aside; a stripe and a row of high windows.
            float z0 = at.z - d * 0.5f;
            mb.Box(new Vector3(at.x, wall * 0.5f, z0 - 0.02f), new Vector3(w * 0.86f, wall - 0.3f, 0.1f), Palette.Hex(0x161A1E));
            float leaf = w * 0.86f / 4f;
            foreach (int k in new[] { 0, 3 }) { var lp = new Vector3(at.x - w * 0.43f + (k + 0.5f) * leaf, wall * 0.5f, z0 - 0.12f); mb.Box(lp, new Vector3(leaf - 0.1f, wall - 0.4f, 0.12f), Palette.Hex(0xC9CED3)); mb.Box(lp + new Vector3(0, 0.6f, -0.07f), new Vector3(leaf - 0.5f, 0.5f, 0.03f), Palette.Hex(0x2F6FB5)); mb.Mat(0.92f); mb.Box(lp + new Vector3(0, wall * 0.25f, -0.07f), new Vector3(leaf - 0.8f, 0.8f, 0.03f), Glass); mb.Mat(0.3f); }
            foreach (int k in new[] { 0, 3 }) { var lp = new Vector3(at.x - w * 0.43f + (k + 0.5f) * leaf + (k == 0 ? 0.6f : -0.6f), wall * 0.5f, z0 - 0.28f); mb.Box(lp, new Vector3(leaf - 0.1f, wall - 0.4f, 0.12f), Palette.Hex(0xBCC2C8)); }
            mb.Box(new Vector3(at.x, wall - 0.05f, z0 - 0.2f), new Vector3(w * 0.9f, 0.3f, 0.5f), Palette.Hex(0x70767C));
            mb.Box(new Vector3(at.x, wall + 0.6f, z0 - 0.05f), new Vector3(w * 0.5f, 0.7f, 0.08f), Palette.Hex(0x2F6FB5));
            // Inside the open doors: the nose of an aircraft on jacks and a work stand, lit.
            mb.Push().Translate(at.x, 0, z0 + 6f);
            mb.Mat(0.55f); mb.Cylinder(new Vector3(0, 2.0f, -4.5f), new Vector3(0, 2.0f, 4f), 1.0f, 1.0f, 12, Palette.Hex(0xF1F3F5)); mb.Sphere(new Vector3(0, 2.0f, -4.5f), new Vector3(1.0f, 1.0f, 2.0f), 10, Palette.Hex(0xF1F3F5));
            mb.Box(new Vector3(0, 1.85f, 0.5f), new Vector3(Mathf.Min(w * 0.7f, 13f), 0.16f, 2.2f), Palette.Hex(0xE4E7EA));
            mb.Mat(0.3f);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 2.4f, 0.9f, -3.2f), new Vector3(1.6f, 1.8f, 1.0f), Palette.PaintYellow);
            mb.Pop();
            mb.Glow = true; for (float x = -w * 0.3f; x <= w * 0.3f; x += w * 0.2f) mb.Box(new Vector3(at.x + x, wall - 0.5f, z0 + 2f), new Vector3(1.2f, 0.1f, 0.3f), Palette.Hex(0xFFF4D0)); mb.Glow = false;
            // Workshops along one side, and the apron line leading in.
            var ax = at + new Vector3(w * 0.5f + 2.2f, 0, 2f);
            mb.Box(ax + Vector3.up * 1.7f, new Vector3(4.4f, 3.4f, 12f), Clad, Palette.Hex(0x8E979F));
            mb.Box(ax + Vector3.up * 3.1f, new Vector3(4.46f, 0.4f, 12.06f), Palette.Hex(0x2F6FB5));
            for (int k = 0; k < 4; k++) { mb.Mat(0.92f); mb.Box(ax + new Vector3(0, 1.8f, -4.5f + k * 3f) + new Vector3(2.22f, 0, 0), new Vector3(0.04f, 1.0f, 1.8f), Glass); mb.Mat(0.3f); }
            mb.Box(new Vector3(at.x, 0.045f, z0 - 5f), new Vector3(0.25f, 0.02f, 10f), Palette.PaintYellow);
        }

        /// <summary>The cargo terminal: a long warehouse with dock doors and a canopy (front toward -Z), air freight containers and pallets on its apron.</summary>
        public static void Cargo(MeshBuilder mb, Vector3 at, int level)
        {
            float w = 20f + Mathf.Min(10, level) * 1.2f, d = 11f, h = 6f;
            var brand = Palette.Hex(0x9A6A3A);
            Shed(mb, at, w, d, h, brand, Palette.Hex(0x7E8A96));
            float z0 = at.z - d * 0.5f;
            mb.Push().Translate(0, 0, 0);
            int doors = Mathf.Max(3, (int)(w / 4.5f));
            for (int k = 0; k < doors; k++)
            {
                float x = at.x - w * 0.5f + (k + 0.5f) * w / doors;
                // Doors on the back face: mirror the roller by building it rotated.
                mb.Push().Translate(x, 0, z0).RotateY(180); Roller(mb, Vector3.zero, 2.8f, 3.4f, k % 3 == 1); mb.Pop();
                mb.Box(new Vector3(x, 4.3f, z0 - 0.08f), new Vector3(0.9f, 0.6f, 0.05f), brand);
            }
            mb.Pop();
            mb.Box(new Vector3(at.x, 4.9f, z0 - 1.6f), new Vector3(w, 0.14f, 3.2f), Palette.Hex(0xBFC6CC));
            for (float x = -w * 0.5f + 0.4f; x <= w * 0.5f; x += w / doors) mb.Box(new Vector3(at.x + x, 2.45f, z0 - 3.0f), new Vector3(0.14f, 4.9f, 0.14f), Steel);
            // Offices on the end.
            var of = at + new Vector3(-w * 0.5f - 2.6f, 0, 1f);
            mb.Box(of + Vector3.up * 2.2f, new Vector3(5f, 4.4f, 8f), Palette.Hex(0xE4E0D6), Palette.Hex(0x8E979F));
            for (int fl = 0; fl < 2; fl++) { mb.Mat(0.92f); mb.Box(of + new Vector3(0, 1.4f + fl * 1.8f, -4.02f), new Vector3(4.2f, 0.9f, 0.04f), Glass); mb.Mat(0.3f); }
            // The yard: unit load devices (the angled-corner containers), netted pallets, a row of dollies.
            Color[] uld = { Palette.Hex(0xD7DBDF), Palette.Hex(0xC9CED3), Palette.Hex(0xB8C4CE) };
            var rng = new System.Random(77);
            int n = Mathf.Min(18, 4 + level * 2);
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(at.x - w * 0.5f + 2f + (k % 6) * 3.1f, 0, z0 - 6f - (k / 6) * 3.4f);
                if (k % 4 == 3)
                {
                    mb.Box(p + Vector3.up * 0.1f, new Vector3(2.2f, 0.14f, 2.2f), Palette.Hex(0x8C9298));
                    for (int b = 0; b < 5; b++) mb.Box(p + new Vector3(-0.5f + (b % 2) * 1.0f, 0.5f + (b / 4) * 0.7f, -0.5f + (b / 2 % 2) * 1.0f), new Vector3(0.9f, 0.7f, 0.9f), b % 2 == 0 ? Palette.Hex(0xB98A52) : Palette.Hex(0xA67A45));
                }
                else
                {
                    mb.Mat(0.6f, 0.5f);
                    mb.Box(p + Vector3.up * 1.0f, new Vector3(2.0f, 1.7f, 2.2f), uld[k % uld.Length]);
                    mb.Push().Translate(p + new Vector3(1.2f, 1.25f, 0)).Rotate(Quaternion.Euler(0, 0, -38)); mb.Box(Vector3.zero, new Vector3(0.9f, 1.3f, 2.2f), uld[k % uld.Length]); mb.Pop();
                    mb.Mat(0.3f);
                    mb.Box(p + new Vector3(0, 1.0f, -1.11f), new Vector3(1.5f, 1.3f, 0.03f), k % 2 == 0 ? brand : Palette.Hex(0x4A5A3A));
                    mb.Box(p + Vector3.up * 0.1f, new Vector3(2.9f, 0.12f, 2.3f), Dark);
                }
            }
            mb.Mat(0.3f);
        }
    }
}
