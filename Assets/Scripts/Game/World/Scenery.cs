using System.Collections.Generic;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The country round each airfield, so no two maps look alike from the air: farmland and hedgerows at Meadow,
    /// conifer forest under snow-capped peaks in the Alps, a city grid at Metro, palms and coves on the island,
    /// dunes and rock in the desert, drifts and pack ice in the Arctic. Built once into the static mesh from simple
    /// shapes; nothing here moves or is tapped.
    /// </summary>
    public static class Scenery
    {
        /// <param name="country">The land outside the airfield, as rectangles (x, z) to fill.</param>
        public static void Build(MeshBuilder mb, Ground g, string map, List<Rect> country, Color ground, Color tree)
        {
            var rng = new System.Random(7);
            switch (map)
            {
                case "alpine": Alpine(mb, g, country, rng, tree); break;
                case "metro": Metro(mb, g, country, rng, tree); break;
                case "coral": Island(mb, country, rng, ground); break;
                case "dune": Desert(mb, country, rng, ground); break;
                case "polar": Arctic(mb, country, rng, ground); break;
                default: Farmland(mb, g, country, rng, tree); break;
            }
        }

        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        /// <summary>Cuts a rectangle into fields of about this size and calls back for each (x0, z0, width, length).</summary>
        static void Fields(Rect blk, System.Random rng, float wMin, float wMax, float lMin, float lMax, System.Action<float, float, float, float> each)
        {
            for (float fx = blk.xMin; fx < blk.xMax - 1;)
            {
                float fw = Mathf.Min(blk.xMax - fx, R(rng, wMin, wMax)); if (blk.xMax - fx - fw < wMin * 0.5f) fw = blk.xMax - fx;
                for (float fz = blk.yMin; fz < blk.yMax - 1;)
                {
                    float fl = Mathf.Min(blk.yMax - fz, R(rng, lMin, lMax)); if (blk.yMax - fz - fl < lMin * 0.5f) fl = blk.yMax - fz;
                    each(fx, fz, fw, fl);
                    fz += fl;
                }
                fx += fw;
            }
        }

        // ------------------------------------------------------------------ buildings

        static readonly Color[] Walls = { Palette.Hex(0xC9C3B6), Palette.Hex(0xB4B9BF), Palette.Hex(0xD6CBB4), Palette.Hex(0xA9A39A), Palette.Hex(0xBFA990), Palette.Hex(0x9AA7B4), Palette.Hex(0xB5876A) };
        static readonly Color[] Roofs = { Palette.Hex(0x8C9096), Palette.Hex(0x7A7F86), Palette.Hex(0x9A948A), Palette.Hex(0x6E747C) };

        /// <summary>
        /// A town building seen from the air: walls in concrete, brick or glass with a band of windows on every floor,
        /// a parapet round a flat roof and the plant that sits on it. Tall ones are glass towers with a crown.
        /// </summary>
        public static void Building(MeshBuilder mb, Vector3 at, float w, float l, float h, System.Random rng, bool detail = true)
        {
            var wall = Walls[rng.Next(Walls.Length)]; var roof = Roofs[rng.Next(Roofs.Length)];
            Color glass = rng.Next(2) == 0 ? Palette.Hex(0x35526E) : Palette.Hex(0x2E4A52);
            bool tower = h > 34;
            if (tower) { wall = Color.Lerp(Palette.Hex(0x4C7FA8), Palette.Hex(0x7FA9BF), (float)rng.NextDouble()); glass = Palette.Hex(0xDCE3E8); }
            // A podium under the taller ones.
            if (h > 20) { mb.Smoothness = 0.2f; mb.Box(at + Vector3.up * 2.5f, new Vector3(w + 3, 5, l + 3), Palette.Hex(0x9A9C9E), roof); }
            mb.Smoothness = tower ? 0.86f : 0.16f;
            mb.Box(at + Vector3.up * h * 0.5f, new Vector3(w, h, l), wall, roof);
            // Floors: a ribbon of windows on each (on a glass tower, the pale floor edges instead).
            float floor = tower ? 4.2f : 3.4f;
            mb.Smoothness = tower ? 0.3f : 0.85f;
            // (Far-off buildings get every other ribbon: they are a texture at that distance.)
            if (!detail) floor *= 2;
            for (float y = (h > 20 ? 6.6f : 1.9f); y < h - 1.2f; y += floor)
            {
                // After dark some floors are lit and some are not.
                bool lit = !tower && rng.Next(5) < 2;
                mb.Glow = lit;
                mb.Box(at + Vector3.up * y, new Vector3(w + 0.12f, tower ? 0.35f : 1.5f, l + 0.12f), lit ? Palette.Hex(0xC9A86A) : glass);
            }
            mb.Glow = false;
            if (!detail)
            {
                mb.Smoothness = 0.2f;
                mb.Box(at + new Vector3(w * 0.15f, h + 1.1f, -l * 0.1f), new Vector3(w * 0.3f, 2.2f, l * 0.3f), Palette.Hex(0x8A8E94), roof);
                return;
            }
            // Piers up the corners of the masonry ones, so the ribbons read as windows between walls.
            if (!tower)
            {
                mb.Smoothness = 0.16f;
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                    mb.Box(at + new Vector3(sx * (w * 0.5f - 0.5f), h * 0.5f, sz * (l * 0.5f - 0.5f)), new Vector3(1.2f, h, 1.2f), wall, roof);
            }
            // The roof: a parapet, a plant room, a couple of units.
            mb.Smoothness = 0.2f;
            foreach (float sz in new[] { -1f, 1f }) mb.Box(at + new Vector3(0, h + 0.35f, sz * (l * 0.5f - 0.15f)), new Vector3(w, 0.7f, 0.3f), MeshBuilder.Darken(wall, 0.9f));
            foreach (float sx in new[] { -1f, 1f }) mb.Box(at + new Vector3(sx * (w * 0.5f - 0.15f), h + 0.35f, 0), new Vector3(0.3f, 0.7f, l), MeshBuilder.Darken(wall, 0.9f));
            mb.Box(at + new Vector3(w * R(rng, -0.2f, 0.2f), h + 1.3f, l * R(rng, -0.2f, 0.2f)), new Vector3(w * 0.3f, 2.6f, l * 0.3f), Palette.Hex(0x8A8E94), roof);
            mb.Box(at + new Vector3(-w * 0.28f, h + 0.5f, l * 0.3f), new Vector3(1.8f, 1f, 1.4f), Palette.Hex(0xA5A9AE));
            if (tower)
            {
                mb.Box(at + Vector3.up * (h + 2.4f), new Vector3(w * 0.72f, 4.8f, l * 0.72f), MeshBuilder.Darken(wall, 0.85f), roof);
                mb.Cylinder(at + new Vector3(w * 0.2f, h + 4.8f, 0), at + new Vector3(w * 0.2f, h + 13f, 0), 0.12f, 0.05f, 4, Palette.Steel, false);
            }
        }

        /// <summary>A house or a small shop: rendered walls under a pitched tiled roof.</summary>
        public static void House(MeshBuilder mb, Vector3 at, float w, float l, System.Random rng)
        {
            Color[] renders = { Palette.Hex(0xE4DCCB), Palette.Hex(0xD9C7A8), Palette.Hex(0xC9D0D4), Palette.Hex(0xB98A6E), Palette.Hex(0xE8E2D6) };
            Color[] tiles = { Palette.Hex(0x8A4B3A), Palette.Hex(0x5E5A58), Palette.Hex(0x74504A), Palette.Hex(0x4E5560) };
            var wall = renders[rng.Next(renders.Length)]; var tile = tiles[rng.Next(tiles.Length)];
            float h = R(rng, 5.5f, 8.5f);
            mb.Smoothness = 0.12f;
            mb.Box(at + Vector3.up * h * 0.5f, new Vector3(w, h, l), wall);
            mb.Smoothness = 0.8f;
            for (float y = 1.8f; y < h - 0.8f; y += 3f) mb.Box(at + Vector3.up * y, new Vector3(w + 0.1f, 1.2f, l * 0.72f), Palette.Hex(0x33485C));
            mb.Smoothness = 0.12f;
            mb.GableRoof(at + Vector3.up * h, new Vector3(w, R(rng, 2.2f, 3.4f), l), tile, wall, 0.5f);
        }

        // ------------------------------------------------------------------ trees

        public static void Broadleaf(MeshBuilder mb, float x, float z, float h, int i, Color tree)
        {
            mb.Smoothness = 0.05f;
            mb.Cylinder(new Vector3(x, 0, z), new Vector3(x, h * 0.4f, z), 0.3f, 0.25f, 4, Palette.Wood);
            var leaf = i % 3 == 0 ? MeshBuilder.Darken(tree, 0.82f) : i % 3 == 1 ? tree : MeshBuilder.Lighten(tree, 0.06f);
            mb.Sphere(new Vector3(x + h * 0.12f, h * 1.02f, z - h * 0.08f), new Vector3(h * 0.27f, h * 0.3f, h * 0.27f), 5, MeshBuilder.Lighten(leaf, 0.07f));
            mb.Sphere(new Vector3(x, h * 0.75f, z), new Vector3(h * 0.38f, h * 0.45f, h * 0.38f), 5, leaf);
            if (i % 3 == 0) mb.Sphere(new Vector3(x - h * 0.16f, h * 0.66f, z + h * 0.12f), new Vector3(h * 0.26f, h * 0.3f, h * 0.26f), 5, MeshBuilder.Darken(leaf, 0.9f));
        }

        /// <summary>A spruce: a short trunk under two stacked cones (snow on the top one where it is cold).</summary>
        static void Conifer(MeshBuilder mb, float x, float z, float h, int i, Color tree, bool snow = false)
        {
            mb.Smoothness = 0.05f;
            var leaf = i % 3 == 0 ? MeshBuilder.Darken(tree, 0.8f) : i % 3 == 1 ? tree : MeshBuilder.Darken(tree, 0.9f);
            mb.Cylinder(new Vector3(x, 0, z), new Vector3(x, h * 0.25f, z), 0.22f, 0.2f, 4, Palette.Wood, false);
            mb.Cylinder(new Vector3(x, h * 0.18f, z), new Vector3(x, h * 0.7f, z), h * 0.24f, h * 0.06f, 6, leaf, false);
            mb.Cylinder(new Vector3(x, h * 0.52f, z), new Vector3(x, h, z), h * 0.16f, 0.02f, 6, snow ? Color.Lerp(leaf, Color.white, 0.75f) : MeshBuilder.Lighten(leaf, 0.05f), false);
        }

        /// <summary>A palm: a leaning trunk in two pieces and a crown of drooping fronds.</summary>
        static void Palm(MeshBuilder mb, float x, float z, float h, System.Random rng)
        {
            mb.Smoothness = 0.08f;
            float lean = R(rng, 0, 360) * Mathf.Deg2Rad; var off = new Vector3(Mathf.Cos(lean), 0, Mathf.Sin(lean)) * h * 0.14f;
            var mid = new Vector3(x, h * 0.5f, z) + off * 0.5f; var top = new Vector3(x, h, z) + off;
            Color trunk = Palette.Hex(0x8C7355), frond = Palette.Hex(0x4F8A3C);
            mb.Cylinder(new Vector3(x, 0, z), mid, 0.3f, 0.22f, 5, trunk, false);
            mb.Cylinder(mid, top, 0.22f, 0.16f, 5, trunk, false);
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60 + R(rng, -12, 12)) * Mathf.Deg2Rad; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var tip = top + dir * h * 0.42f + Vector3.down * h * 0.12f;
                mb.Cylinder(top + Vector3.up * 0.1f, top + dir * h * 0.24f + Vector3.up * h * 0.05f, 0.1f, 0.5f, 3, k % 2 == 0 ? frond : MeshBuilder.Darken(frond, 0.85f), false);
                mb.Cylinder(top + dir * h * 0.24f + Vector3.up * h * 0.05f, tip, 0.5f, 0.03f, 3, k % 2 == 0 ? frond : MeshBuilder.Darken(frond, 0.85f), false);
            }
        }

        /// <summary>A peak: a broad cone of rock with a snow cap, far enough out to be a horizon.</summary>
        static void Peak(MeshBuilder mb, float x, float z, float radius, float height, Color rock, float snowFrom, System.Random rng)
        {
            mb.Smoothness = 0.04f;
            var apex = new Vector3(x + R(rng, -0.12f, 0.12f) * radius, height, z + R(rng, -0.12f, 0.12f) * radius);
            mb.Cylinder(new Vector3(x, -2, z), apex, radius, 0.5f, 9, rock, false, false);
            // The cap follows the same slope from part-way up.
            var from = Vector3.Lerp(new Vector3(x, -2, z), apex, snowFrom);
            mb.Cylinder(from + Vector3.up * 0.4f, apex + Vector3.up * 0.6f, radius * (1 - snowFrom) * 1.02f, 0.5f, 9, Palette.Hex(0xF2F5F8), false, false);
        }

        static void RingOfPeaks(MeshBuilder mb, System.Random rng, Color rock, float snowFrom, int count, float near = 560, float far = 760)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + R(rng, -0.3f, 0.3f)) / count * Mathf.PI * 2, d = R(rng, near, far);
                float rad = R(rng, 110, 210);
                Peak(mb, 20 + Mathf.Cos(a) * d, Mathf.Sin(a) * d, rad, rad * R(rng, 0.75f, 1.25f), i % 2 == 0 ? rock : MeshBuilder.Darken(rock, 0.88f), snowFrom, rng);
            }
        }

        // ------------------------------------------------------------------ the six countries

        static void Farmland(MeshBuilder mb, Ground g, List<Rect> country, System.Random rng, Color tree)
        {
            // Pasture, lush grass, ripening crop, stubble, ploughed earth, dark clover.
            var crops = new[] { new Color(1f, 1f, 1f), new Color(0.94f, 1f, 0.9f), new Color(1.1f, 1.06f, 0.9f), new Color(1.2f, 1.16f, 1.02f), new Color(1.05f, 0.9f, 0.8f), new Color(0.86f, 0.92f, 0.84f) };
            Color hedge = Palette.Hex(0x3C5532);
            int n = 0;
            foreach (var blk in country)
                Fields(blk, rng, 60, 140, 50, 140, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f;
                    int kind = rng.Next(crops.Length + 1);          // one more than the crops: a wood
                    if (kind < crops.Length) { if (kind > 0) g.Rect("Grass", cx, cz, fw - 1.6f, fl - 1.6f, -0.09f, 0, crops[kind]); }
                    else for (int t = 0; t < 16; t++) Broadleaf(mb, fx + 4 + R(rng, 0, fw - 8), fz + 4 + R(rng, 0, fl - 8), R(rng, 4, 8), n++, tree);
                    // Hedges on two sides (the neighbours bring the other two), with a tree every so often.
                    mb.Smoothness = 0.05f;
                    mb.Box(new Vector3(cx, 0.5f, fz), new Vector3(fw, 1.0f, 1.2f), hedge);
                    mb.Box(new Vector3(fx, 0.5f, cz), new Vector3(1.2f, 1.0f, fl), hedge);
                    for (float hx = fx + 6; hx < fx + fw; hx += R(rng, 24, 64)) Broadleaf(mb, hx, fz + R(rng, -1, 1), R(rng, 3.5f, 7), n++, tree);
                    for (float hz = fz + 8; hz < fz + fl; hz += R(rng, 28, 72)) Broadleaf(mb, fx + R(rng, -1, 1), hz, R(rng, 3.5f, 7), n++, tree);
                });
        }

        static void Alpine(MeshBuilder mb, Ground g, List<Rect> country, System.Random rng, Color tree)
        {
            Color fir = Palette.Hex(0x2F5038);
            int n = 0;
            foreach (var blk in country)
                Fields(blk, rng, 70, 150, 70, 150, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f;
                    int kind = rng.Next(5);
                    if (kind <= 2)
                    {
                        // Forest: firs thick enough to read as one dark mass, on darker ground.
                        g.Rect("Grass", cx, cz, fw, fl, -0.09f, 0, new Color(0.66f, 0.78f, 0.66f));
                        int trees = Mathf.RoundToInt(fw * fl / 230f);
                        for (int t = 0; t < trees; t++) Conifer(mb, fx + R(rng, 2, fw - 2), fz + R(rng, 2, fl - 2), R(rng, 6, 12), n++, fir);
                    }
                    else if (kind == 3)
                    {
                        // An alpine meadow with a hay barn.
                        g.Rect("Grass", cx, cz, fw - 2, fl - 2, -0.09f, 0, new Color(1.02f, 1.08f, 0.9f));
                        mb.Smoothness = 0.1f;
                        mb.Box(new Vector3(cx, 1.6f, cz), new Vector3(7, 3.2f, 5), Palette.Hex(0x6B4A32));
                        mb.GableRoof(new Vector3(cx, 3.2f, cz), new Vector3(7, 2.2f, 5), Palette.Hex(0x4A3A30), Palette.Hex(0x6B4A32), 0.5f);
                        for (int t = 0; t < 5; t++) Conifer(mb, fx + R(rng, 2, fw - 2), fz + R(rng, 2, fl - 2), R(rng, 6, 11), n++, fir);
                    }
                    else
                    {
                        // Scree and boulders.
                        g.Rect("Grass", cx, cz, fw - 2, fl - 2, -0.09f, 0, new Color(0.95f, 0.95f, 0.98f));
                        mb.Smoothness = 0.06f;
                        for (int t = 0; t < 7; t++)
                        {
                            float s = R(rng, 1.2f, 4.5f);
                            mb.Sphere(new Vector3(fx + R(rng, 4, fw - 4), s * 0.25f, fz + R(rng, 4, fl - 4)), new Vector3(s, s * 0.6f, s * 0.8f), 5, t % 2 == 0 ? Palette.Hex(0x8A8C88) : Palette.Hex(0x74766F), false);
                        }
                        for (int t = 0; t < 6; t++) Conifer(mb, fx + R(rng, 2, fw - 2), fz + R(rng, 2, fl - 2), R(rng, 5, 10), n++, fir);
                    }
                });
            RingOfPeaks(mb, rng, Palette.Hex(0x6E756E), 0.55f, 16);
        }

        static void Metro(MeshBuilder mb, Ground g, List<Rect> country, System.Random rng, Color tree)
        {
            // A city on every side: blocks of buildings between streets, a park here and there.
            int n = 0;
            foreach (var blk in country)
            {
                g.Rect("Tarmac", blk.center.x, blk.center.y, blk.width, blk.height, -0.12f, 0, new Color(0.62f, 0.62f, 0.64f));     // streets
                Fields(blk, rng, 46, 78, 46, 78, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f, street = 9;
                    float bw = fw - street, bl = fl - street;
                    if (bw < 14 || bl < 14) return;
                    // Further from the airfield the city climbs (nothing tall under the approach).
                    float away = Mathf.Max(Mathf.Abs(cx - 40) - 160, Mathf.Abs(cz) - 260);
                    float tall = Mathf.Clamp01(away / 380f);
                    if (rng.Next(9) == 0)
                    {
                        g.Rect("Grass", cx, cz, bw, bl, -0.06f);
                        for (int t = 0; t < 9; t++) Broadleaf(mb, fx + street * 0.5f + R(rng, 3, bw - 3), fz + street * 0.5f + R(rng, 3, bl - 3), R(rng, 4, 7), n++, tree);
                        return;
                    }
                    g.Rect("Concrete", cx, cz, bw, bl, -0.06f, 0, new Color(0.8f, 0.8f, 0.8f));     // the pavement
                    int nx = Mathf.Max(1, Mathf.RoundToInt(bw / 30f)), nz = Mathf.Max(1, Mathf.RoundToInt(bl / 30f));
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                        {
                            float w = bw / nx - 3, l = bl / nz - 3;
                            float px = fx + street * 0.5f + (ix + 0.5f) * bw / nx, pz = fz + street * 0.5f + (iz + 0.5f) * bl / nz;
                            float h = R(rng, 7, 16) + tall * R(rng, 0, 70) * (rng.Next(4) == 0 ? 1 : 0.35f);
                            if (h < 9 && rng.Next(2) == 0) House(mb, new Vector3(px, 0, pz), Mathf.Min(w, 12), Mathf.Min(l, 14), rng);
                            else Building(mb, new Vector3(px, 0, pz), w, l, h, rng, away < 120);
                        }
                });
            }
        }

        static void Island(MeshBuilder mb, List<Rect> country, System.Random rng, Color sand)
        {
            // Palms in groves, thinning toward the beach; pale scrub; nothing out in the water.
            foreach (var blk in country)
                Fields(blk, rng, 40, 70, 40, 70, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f;
                    float shore = 222 - Mathf.Max(Mathf.Abs(cx - 40), Mathf.Abs(cz));       // metres inland of the sand's edge
                    if (shore < 6) return;
                    int palms = rng.Next(3) == 0 ? 9 : rng.Next(3);
                    for (int t = 0; t < palms; t++)
                    {
                        float x = fx + R(rng, 2, fw - 2), z = fz + R(rng, 2, fl - 2);
                        if (Mathf.Abs(x - 40) > 220 || Mathf.Abs(z) > 220) continue;
                        Palm(mb, x, z, R(rng, 6, 10), rng);
                    }
                    mb.Smoothness = 0.05f;
                    for (int t = 0; t < 4; t++)
                    {
                        float x = fx + R(rng, 2, fw - 2), z = fz + R(rng, 2, fl - 2), s = R(rng, 0.8f, 1.8f);
                        if (Mathf.Abs(x - 40) > 220 || Mathf.Abs(z) > 220) continue;
                        mb.Sphere(new Vector3(x, s * 0.3f, z), new Vector3(s, s * 0.6f, s), 5, t % 2 == 0 ? Palette.Hex(0x7FA05A) : Palette.Hex(0x93A866), false);
                    }
                    // A low swell in the sand, a shade lighter or darker.
                    float r = R(rng, 14, 26);
                    if (shore > 30) mb.Sphere(new Vector3(cx, -r * 0.92f * 0.12f, cz), new Vector3(r, r * 0.12f, r * R(rng, 0.5f, 0.9f)), 8, rng.Next(2) == 0 ? MeshBuilder.Lighten(sand, 0.12f) : MeshBuilder.Darken(sand, 0.95f));
                });
            // Islets out in the lagoon, each with a palm or two.
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2 + R(rng, -0.2f, 0.2f), d = R(rng, 330, 520), r = R(rng, 16, 38);
                var c = new Vector3(40 + Mathf.Cos(a) * d, -0.06f, Mathf.Sin(a) * d);
                mb.Smoothness = 0.08f;
                mb.Cylinder(c + Vector3.down * 0.3f, c + Vector3.up * 0.02f, r * 1.35f, r * 1.3f, 12, Palette.Hex(0x67CFD0), true, false);       // shallows
                mb.Cylinder(c + Vector3.down * 0.3f, c + Vector3.up * 0.22f, r, r * 0.9f, 12, sand, true, false);
                for (int t = 0; t < 2 + rng.Next(4); t++) Palm(mb, c.x + R(rng, -r, r) * 0.55f, c.z + R(rng, -r, r) * 0.55f, R(rng, 6, 9), rng);
            }
        }

        static void Desert(MeshBuilder mb, List<Rect> country, System.Random rng, Color sand)
        {
            // Long dunes lying across the wind, rock outcrops, a little scrub; mesas on the horizon.
            foreach (var blk in country)
                Fields(blk, rng, 90, 170, 60, 110, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f;
                    mb.Smoothness = 0.04f;
                    float len = fw * R(rng, 0.5f, 0.7f), wid = fl * R(rng, 0.2f, 0.3f), hgt = R(rng, 9, 20);
                    // A dune is the top of a wide, flat ball sunk three quarters into the ground: a long low back, lit on
                    // one flank and in shade on the other.
                    mb.Sphere(new Vector3(cx, -hgt * 0.76f, cz), new Vector3(len, hgt, wid), 16, rng.Next(2) == 0 ? MeshBuilder.Lighten(sand, 0.08f) : MeshBuilder.Darken(sand, 0.97f));
                    if (rng.Next(3) == 0) mb.Sphere(new Vector3(cx + len * 0.35f, -hgt * 0.6f, cz + wid * 0.8f), new Vector3(len * 0.6f, hgt * 0.75f, wid * 0.7f), 14, MeshBuilder.Lighten(sand, 0.04f));
                    if (rng.Next(4) == 0)
                    {
                        float x = fx + R(rng, 6, fw - 6), z = fz + R(rng, 6, fl - 6);
                        for (int t = 0; t < 4; t++) { float s = R(rng, 1.5f, 5f); mb.Sphere(new Vector3(x + R(rng, -6, 6), s * 0.3f, z + R(rng, -6, 6)), new Vector3(s, s * 0.7f, s * 0.85f), 5, t % 2 == 0 ? Palette.Hex(0x9C6B48) : Palette.Hex(0x86583C), false); }
                    }
                    for (int t = 0; t < 3; t++)
                    {
                        float x = fx + R(rng, 2, fw - 2), z = fz + R(rng, 2, fl - 2), s = R(rng, 0.5f, 1.1f);
                        mb.Sphere(new Vector3(x, s * 0.4f, z), new Vector3(s, s * 0.7f, s), 4, Palette.Hex(0x8A9A4A), false);
                    }
                });
            // Mesas: flat-topped rock, banded, a long way off.
            for (int i = 0; i < 11; i++)
            {
                float a = (i + R(rng, -0.3f, 0.3f)) / 11f * Mathf.PI * 2, d = R(rng, 560, 740), r = R(rng, 60, 130), h = R(rng, 40, 90);
                var c = new Vector3(20 + Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                mb.Smoothness = 0.04f;
                mb.Cylinder(c + Vector3.down * 2, c + Vector3.up * h * 0.45f, r * 1.35f, r * 0.95f, 9, Palette.Hex(0xB07848), false, false);
                mb.Cylinder(c + Vector3.up * h * 0.45f, c + Vector3.up * h, r * 0.9f, r * 0.8f, 9, Palette.Hex(0xA2603C), true, false);
            }
        }

        static void Arctic(MeshBuilder mb, List<Rect> country, System.Random rng, Color snow)
        {
            Color fir = Palette.Hex(0x33503E);
            int n = 0;
            foreach (var blk in country)
                Fields(blk, rng, 80, 150, 70, 130, (fx, fz, fw, fl) =>
                {
                    float cx = fx + fw * 0.5f, cz = fz + fl * 0.5f;
                    int kind = rng.Next(6);
                    mb.Smoothness = 0.3f;
                    if (kind == 0)
                    {
                        // A frozen lake: blue ice, swept clear, with a pressure ridge.
                        mb.Smoothness = 0.85f;
                        mb.Cylinder(new Vector3(cx, -0.1f, cz), new Vector3(cx, 0.02f, cz), Mathf.Min(fw, fl) * 0.42f, Mathf.Min(fw, fl) * 0.4f, 14, Palette.Hex(0xA9D3E6), true, false);
                        return;
                    }
                    // Drifts: long low swells of snow lying with the wind.
                    for (int t = 0; t < 2; t++)
                    {
                        float len = fw * R(rng, 0.3f, 0.5f), wid = fl * R(rng, 0.12f, 0.2f), hgt = R(rng, 5, 11);
                        mb.Sphere(new Vector3(fx + R(rng, 0.2f, 0.8f) * fw, -hgt * 0.8f, fz + R(rng, 0.2f, 0.8f) * fl), new Vector3(len, hgt, wid), 14, t == 0 ? Palette.Hex(0xF7FAFC) : Palette.Hex(0xDCE6EE));
                    }
                    if (kind == 1)
                        for (int t = 0; t < 14; t++) Conifer(mb, fx + R(rng, 2, fw - 2), fz + R(rng, 2, fl - 2), R(rng, 5, 9), n++, fir, true);
                    else if (kind == 2)
                    {
                        mb.Smoothness = 0.06f;
                        for (int t = 0; t < 4; t++) { float s = R(rng, 1.5f, 4.5f); mb.Sphere(new Vector3(fx + R(rng, 4, fw - 4), s * 0.25f, fz + R(rng, 4, fl - 4)), new Vector3(s, s * 0.6f, s * 0.8f), 5, Palette.Hex(0x5E646B), false); }
                    }
                });
            RingOfPeaks(mb, rng, Palette.Hex(0x7C8792), 0.25f, 14);
        }
    }
}
