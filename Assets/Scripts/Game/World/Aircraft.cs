using System;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Aircraft built in code so every airline can wear its own paint: a lofted fuselage with a rounded nose and an
    /// upswept tail, swept wings with winglets, engines (propellers on the small ones), fin and tailplane, rows of
    /// cabin windows, a cockpit, doors and landing gear. Nose toward +Z, wheels on y = 0.
    /// </summary>
    public static class Aircraft
    {
        sealed class Spec
        {
            public float Length, Radius, Span, Sweep, WingZ, WingY;     // metres; WingY as a share of the radius (-1 low .. 1 high)
            public int Engines; public bool Props; public bool TTail; public bool Hump;
        }

        static Spec SpecOf(AircraftClass c)
        {
            switch (c)
            {
                case AircraftClass.A: return new Spec { Length = 6.2f, Radius = 0.5f, Span = 7.4f, Sweep = 0.02f, WingZ = 0.12f, WingY = 0.95f, Engines = 1, Props = true };
                case AircraftClass.B: return new Spec { Length = 8.8f, Radius = 0.56f, Span = 9.6f, Sweep = 0.05f, WingZ = 0.06f, WingY = 0.95f, Engines = 2, Props = true, TTail = true };
                case AircraftClass.C: return new Spec { Length = 10.8f, Radius = 0.62f, Span = 10.4f, Sweep = 0.3f, WingZ = 0.03f, WingY = -0.6f, Engines = 2 };
                case AircraftClass.D: return new Spec { Length = 12.8f, Radius = 0.78f, Span = 12.0f, Sweep = 0.34f, WingZ = 0.03f, WingY = -0.6f, Engines = 2 };
                default: return new Spec { Length = 14.2f, Radius = 0.9f, Span = 12.6f, Sweep = 0.38f, WingZ = 0.04f, WingY = -0.6f, Engines = 4, Hump = true };
            }
        }

        public static float Length(AircraftClass c) { return SpecOf(c).Length; }

        struct Paint { public Color Body, Belly, Tail, Line, Engine; public int Style; public bool Windows; }

        static Paint PaintOf(string airline)
        {
            Color white = Palette.Hex(0xF4F5F2), grey = Palette.Hex(0xB9BEC4);
            switch (airline)
            {
                case "puffin": return new Paint { Body = white, Belly = Palette.Hex(0xE8742A), Tail = Palette.Hex(0xE8742A), Line = Palette.Hex(0x2A2F38), Engine = white, Style = 0, Windows = true };
                case "sunhop": return new Paint { Body = white, Belly = white, Tail = Palette.Hex(0xF4B81E), Line = Palette.Hex(0xF07A1A), Engine = Palette.Hex(0xF4B81E), Style = 1, Windows = true };
                case "zip": return new Paint { Body = white, Belly = grey, Tail = Palette.Hex(0xD72C6B), Line = Palette.Hex(0xD72C6B), Engine = Palette.Hex(0xD72C6B), Style = 2, Windows = true };
                case "meridian": return new Paint { Body = white, Belly = Palette.Hex(0x16345E), Tail = Palette.Hex(0x16345E), Line = Palette.Hex(0xC9A227), Engine = Palette.Hex(0x16345E), Style = 0, Windows = true };
                case "atlas": return new Paint { Body = Palette.Hex(0xC9CDD1), Belly = Palette.Hex(0x4D5E3A), Tail = Palette.Hex(0x4D5E3A), Line = Palette.Hex(0xE2B93B), Engine = grey, Style = 0, Windows = false };
                case "fjord": return new Paint { Body = white, Belly = white, Tail = Palette.Hex(0x157A8C), Line = Palette.Hex(0x7FD1D9), Engine = white, Style = 1, Windows = true };
                case "solstice": return new Paint { Body = Palette.Hex(0xFFF3DC), Belly = Palette.Hex(0xE2542A), Tail = Palette.Hex(0xE2542A), Line = Palette.Hex(0xF6A21E), Engine = Palette.Hex(0xF6A21E), Style = 2, Windows = true };
                case "crown": return new Paint { Body = Palette.Hex(0x23262B), Belly = Palette.Hex(0x23262B), Tail = Palette.Hex(0x23262B), Line = Palette.Hex(0xD4AF37), Engine = Palette.Hex(0xD4AF37), Style = 0, Windows = true };
                default: return new Paint { Body = white, Belly = grey, Tail = Palette.Steel, Line = Palette.SteelDark, Engine = white, Style = 0, Windows = true };
            }
        }

        public static MeshBuilder Build(AircraftClass c, string airline)
        {
            var s = SpecOf(c);
            var p = PaintOf(airline);
            var mb = new MeshBuilder { Smoothness = 0.72f };
            float L = s.Length, R = s.Radius;
            float gear = R * (s.WingY > 0 ? 0.55f : 0.75f) + (s.Props ? 0 : R * 0.55f);     // clearance under the belly
            float cy = gear + R;                                                           // the fuselage's centre line

            // ---- fuselage: rings from the tail cone to the nose
            const int rings = 26, seg = 18;
            Func<float, float> radius = t =>
            {
                // t: 0 at the tail tip, 1 at the nose tip
                if (t > 0.86f) { float q = (t - 0.86f) / 0.14f; return R * Mathf.Sqrt(Mathf.Max(0, 1 - q * q)) * (1 - 0.12f * q) + 0.02f * (1 - q); }
                if (t < 0.32f) { float q = 1 - t / 0.32f; return R * (1 - 0.86f * q * q); }
                return R;
            };
            Func<float, float> lift = t => t < 0.32f ? R * 0.62f * Mathf.Pow(1 - t / 0.32f, 1.6f) : t > 0.9f ? -R * 0.18f * (t - 0.9f) / 0.1f : 0;   // tail sweeps up, nose droops
            Func<float, float> hump = t => s.Hump && t > 0.55f && t < 0.93f ? R * 0.42f * Mathf.Sin((t - 0.55f) / 0.38f * Mathf.PI) : 0;
            Func<float, float, Color> skin = (t, up) =>
            {
                // up: -1 at the keel .. 1 at the crown
                float tailEdge = p.Style == 2 ? 0.46f - up * 0.16f : p.Style == 1 ? 0.3f + up * 0.1f : 0.26f - up * 0.05f;
                if (t < tailEdge) return p.Tail;
                if (p.Style == 1 && t < tailEdge + 0.035f) return p.Line;
                if (up < -0.42f) return p.Belly;
                if (p.Style != 2 && Mathf.Abs(up + 0.36f) < 0.07f) return p.Line;                     // the cheatline under the windows
                if (t > 0.975f) return Palette.Hex(0x3A3F46);                                         // radome
                return p.Body;
            };
            for (int i = 0; i < rings; i++)
            {
                float t0 = i / (float)rings, t1 = (i + 1) / (float)rings;
                float z0 = (t0 - 0.5f) * L, z1 = (t1 - 0.5f) * L, r0 = radius(t0), r1 = radius(t1);
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k / (float)seg * Mathf.PI * 2, a1 = (k + 1) / (float)seg * Mathf.PI * 2;
                    Vector3 Ring(float a, float r, float t, float z) { float up = Mathf.Cos(a); return new Vector3(Mathf.Sin(a) * r, cy + lift(t) + up * r + (up > 0 ? hump(t) * up : 0), z); }
                    Vector3 n0 = new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0), n1 = new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0);
                    float um = Mathf.Cos((a0 + a1) * 0.5f);
                    var col = skin((t0 + t1) * 0.5f, um);
                    mb.QuadSmooth(Ring(a0, r0, t0, z0), Ring(a0, r1, t1, z1), Ring(a1, r1, t1, z1), Ring(a1, r0, t0, z0), n0, n0, n1, n1, col, col, col, col);
                }
            }

            // ---- glass: cockpit and cabin windows (dark, glossy, a hair proud of the skin), doors as thin outlines
            mb.Smoothness = 0.95f;
            Color glass = Palette.Hex(0x1B2733);
            float zc = L * 0.5f - L * 0.105f;
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Push().Translate(sx * R * 0.5f, cy + R * 0.42f + hump(0.9f) * 0.3f, zc).Rotate(Quaternion.Euler(-22, sx * 34, 0));
                mb.Box(Vector3.zero, new Vector3(R * 0.62f, R * 0.3f, R * 0.08f), glass);
                mb.Pop();
            }
            mb.Push().Translate(0, cy + R * 0.48f + hump(0.9f) * 0.3f, zc + R * 0.22f).Rotate(Quaternion.Euler(-30, 0, 0));
            mb.Box(Vector3.zero, new Vector3(R * 0.66f, R * 0.3f, R * 0.08f), glass);
            mb.Pop();
            if (p.Windows)
            {
                float step = Mathf.Max(0.36f, R * 0.52f), from = -L * 0.2f, to = L * 0.33f;
                for (float z = from; z <= to; z += step)
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        mb.Box(new Vector3(sx * R * 0.985f, cy + R * 0.2f, z), new Vector3(0.03f, R * 0.2f, step * 0.42f), glass);
                        if (s.Hump && z > L * 0.06f) mb.Box(new Vector3(sx * R * 0.82f, cy + R * 0.78f + hump(0.74f) * 0.55f, z), new Vector3(0.03f, R * 0.16f, step * 0.4f), glass);
                    }
            }
            mb.Smoothness = 0.5f;
            foreach (float z in new[] { L * 0.36f, -L * 0.22f })
                foreach (float sx in new[] { -1f, 1f })
                    mb.Box(new Vector3(sx * R * 0.99f, cy + R * 0.05f, z), new Vector3(0.025f, R * 0.95f, R * 0.5f), MeshBuilder.Darken(p.Body, 0.82f));

            // ---- wings
            mb.Smoothness = 0.6f;
            Color wing = Palette.Hex(0xD9DCDF);
            float rootChord = L * (s.Props ? 0.16f : 0.2f), tipChord = rootChord * (s.Props ? 0.6f : 0.3f);
            float wy = cy + R * s.WingY * 0.82f, wz = L * s.WingZ;
            foreach (float sx in new[] { -1f, 1f })
            {
                var root = new Vector3(sx * R * 0.6f, wy, wz);
                var tip = new Vector3(sx * s.Span * 0.5f, wy + s.Span * (s.WingY > 0 ? 0.0f : 0.045f), wz - s.Span * 0.5f * s.Sweep);
                Surface(mb, root, tip, rootChord, tipChord, R * 0.2f, R * 0.07f, wing, sx);
                if (!s.Props)
                {
                    // Winglet in the airline's colour.
                    Surface(mb, tip, tip + new Vector3(sx * 0.12f, R * 0.95f, -tipChord * 0.5f), tipChord, tipChord * 0.45f, R * 0.07f, R * 0.04f, p.Tail, sx, true);
                }
                // Engines.
                for (int e = 0; e < s.Engines / (s.Engines == 1 ? 1 : 2); e++)
                {
                    if (s.Engines == 1) break;
                    float u = s.Engines == 4 ? 0.3f + e * 0.26f : (s.Props ? 0.28f : 0.34f);
                    var at = Vector3.Lerp(root, tip, u);
                    Engine(mb, at + new Vector3(0, s.Props ? -R * 0.05f : -R * 0.62f, rootChord * (s.Props ? 0.35f : 0.42f)), R * (s.Props ? 0.3f : (s.Engines == 4 ? 0.36f : 0.44f)), s.Props, p, R);
                }
            }
            if (s.Engines == 1) Engine(mb, new Vector3(0, cy - R * 0.05f, L * 0.5f - R * 0.3f), R * 0.5f, true, p, R, true);

            // ---- tail: fin and tailplane
            float tz = -L * 0.5f + L * 0.07f, ty = cy + lift(0.07f) + R * 0.45f;
            float finH = R * (s.Props ? 2.6f : 3.0f), finChord = L * 0.15f;
            Surface(mb, new Vector3(0, ty, tz + finChord * 0.35f), new Vector3(0, ty + finH, tz - finChord * 0.5f), finChord, finChord * 0.42f, R * 0.12f, R * 0.07f, p.Tail, 1, true);
            mb.Smoothness = 0.6f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float stabY = s.TTail ? ty + finH * 0.96f : ty - R * 0.2f, stabZ = s.TTail ? tz - finChord * 0.45f : tz + finChord * 0.3f;
                Surface(mb, new Vector3(sx * R * 0.2f, stabY, stabZ), new Vector3(sx * s.Span * 0.19f, stabY + R * 0.1f, stabZ - s.Span * 0.19f * (s.Sweep + 0.12f)), finChord * 0.7f, finChord * 0.32f, R * 0.09f, R * 0.05f, wing, sx);
            }

            // ---- lights: red and green at the wingtips, white on the tail, a red beacon on the roof
            mb.Mat(0.5f, 0, true);
            mb.Sphere(new Vector3(-s.Span * 0.5f, wy + s.Span * (s.WingY > 0 ? 0.0f : 0.045f), wz - s.Span * 0.5f * s.Sweep), Vector3.one * 0.13f, 6, Palette.Hex(0xFF3B30));
            mb.Sphere(new Vector3(s.Span * 0.5f, wy + s.Span * (s.WingY > 0 ? 0.0f : 0.045f), wz - s.Span * 0.5f * s.Sweep), Vector3.one * 0.13f, 6, Palette.Hex(0x34E36A));
            mb.Sphere(new Vector3(0, cy + R * 1.02f, L * 0.08f), Vector3.one * 0.12f, 6, Palette.Hex(0xFF3B30));
            mb.Sphere(new Vector3(0, cy + lift(0.01f), -L * 0.5f + 0.05f), Vector3.one * 0.11f, 6, Palette.White);
            if (p.Windows)
                for (float z = -L * 0.2f; z <= L * 0.33f; z += Mathf.Max(0.36f, R * 0.52f) * 2)
                    foreach (float sx in new[] { -1f, 1f }) mb.Box(new Vector3(sx * R * 0.992f, cy + R * 0.2f, z), new Vector3(0.03f, R * 0.12f, 0.12f), Palette.Hex(0xFFE9B0));
            mb.Mat(0.6f);

            // ---- landing gear
            mb.Smoothness = 0.2f;
            float mainZ = wz - rootChord * 0.15f, mainX = s.WingY > 0 ? R * 0.9f : R * 1.5f;
            foreach (var g in new[] { new Vector3(0, 0, L * 0.5f - L * 0.16f), new Vector3(-mainX, 0, mainZ), new Vector3(mainX, 0, mainZ) })
            {
                float wr = Mathf.Max(0.17f, R * 0.3f);
                mb.Cylinder(new Vector3(g.x, wr, g.z), new Vector3(g.x, cy - R * 0.7f, g.z), 0.06f, 6, Palette.Steel);
                foreach (float o in new[] { -0.13f, 0.13f })
                    mb.Cylinder(new Vector3(g.x + o - 0.05f, wr, g.z), new Vector3(g.x + o + 0.05f, wr, g.z), wr, 10, Palette.Tyre);
            }
            return mb;
        }

        /// <summary>A wing, fin or tailplane: a lens-shaped section lofted from the root to the tip (chords along Z, swept by the tip's Z).</summary>
        static void Surface(MeshBuilder mb, Vector3 root, Vector3 tip, float rootChord, float tipChord, float rootThick, float tipThick, Color col, float side, bool vertical = false)
        {
            // Four points a section: leading edge, top, trailing edge, bottom. "Up" is +Y for wings, sideways for fins and winglets.
            Vector3 thick = vertical ? Vector3.right : Vector3.up;
            Vector3[] Section(Vector3 c, float chord, float t) => new[]
            {
                c + Vector3.forward * chord * 0.5f, c + Vector3.forward * chord * 0.1f + thick * t * 0.5f,
                c - Vector3.forward * chord * 0.5f, c + Vector3.forward * chord * 0.1f - thick * t * 0.5f,
            };
            var a = Section(root, rootChord, rootThick); var b = Section(tip, tipChord, tipThick);
            var centre = (root + tip) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 p0 = a[i], p1 = a[j], p2 = b[j], p3 = b[i];
                // Wind each panel so it faces away from the surface's own middle.
                var n = Vector3.Cross(p1 - p0, p2 - p0);
                var outward = (p0 + p1 + p2 + p3) * 0.25f - centre;
                var top = i == 3 || i == 0 ? MeshBuilder.Lighten(col, 0.04f) : MeshBuilder.Darken(col, 0.93f);
                if (Vector3.Dot(n, outward) >= 0) mb.Quad(p0, p1, p2, p3, top); else mb.Quad(p3, p2, p1, p0, top);
            }
            // Close the tip.
            mb.Quad(b[0], b[1], b[2], b[3], col); mb.Quad(b[3], b[2], b[1], b[0], col);
        }

        static void Engine(MeshBuilder mb, Vector3 at, float r, bool prop, Paint p, float R, bool nose = false)
        {
            mb.Smoothness = 0.7f;
            float len = prop ? r * 4.2f : r * 3.3f;
            var back = at - Vector3.forward * len * 0.55f; var front = at + Vector3.forward * len * 0.45f;
            if (!nose)
            {
                mb.Cylinder(back, front, r * (prop ? 0.7f : 0.78f), r, 14, p.Engine);
                if (!prop) mb.Box(at + new Vector3(0, r * 1.05f, -len * 0.1f), new Vector3(r * 0.22f, r * 0.9f, len * 0.6f), Palette.Hex(0xD9DCDF));     // pylon
            }
            if (prop)
            {
                // Spinner and blades.
                mb.Sphere(front + Vector3.forward * r * 0.15f, new Vector3(r * 0.42f, r * 0.42f, r * 0.7f), 8, Palette.Hex(0x2A2F38));
                mb.Smoothness = 0.3f;
                for (int k = 0; k < 4; k++)
                {
                    mb.Push().Translate(front + Vector3.forward * r * 0.2f).Rotate(Quaternion.Euler(0, 0, k * 90 + 25));
                    mb.Box(new Vector3(0, r * 1.25f, 0), new Vector3(r * 0.24f, r * 2.3f, 0.04f), Palette.Hex(0x30343B));
                    mb.Pop();
                }
            }
            else
            {
                // The intake: a bright lip, the dark fan inside, a spinner.
                mb.Cylinder(front - Vector3.forward * 0.02f, front + Vector3.forward * 0.06f, r * 1.03f, r * 0.96f, 14, Palette.Hex(0xC8CCD0));
                mb.Smoothness = 0.2f;
                mb.Cylinder(front + Vector3.forward * 0.02f, front + Vector3.forward * 0.065f, r * 0.86f, r * 0.86f, 14, Palette.Hex(0x16191D));
                mb.Sphere(front + Vector3.forward * 0.05f, new Vector3(r * 0.3f, r * 0.3f, r * 0.4f), 8, Palette.Hex(0x8A9096));
                mb.Cylinder(back - Vector3.forward * r * 0.5f, back, r * 0.38f, r * 0.66f, 12, Palette.Hex(0x5A6068));                                      // exhaust cone
            }
            mb.Smoothness = 0.6f;
        }
    }
}
