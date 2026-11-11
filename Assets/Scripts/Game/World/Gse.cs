using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Ground support equipment, built from code at the apron's scale: each department's own vehicle, the way it
    /// looks on a real ramp. All of them face -Z (the direction they drive), wheels on y = 0, about 4 m long.
    /// </summary>
    public static class Gse
    {
        static readonly Color White = Palette.Hex(0xF1F3F5), Glass = Palette.Hex(0x22303C), Dark = Palette.Hex(0x2B3138), Steel = Palette.Hex(0xB9BEC4), Amber = Palette.Hex(0xFFB020);

        public static MeshBuilder Build(Service s)
        {
            var mb = new MeshBuilder { Smoothness = 0.45f };
            switch (s)
            {
                case Service.Fuel: Bowser(mb); break;
                case Service.Baggage: BaggageTrain(mb); break;
                case Service.Catering: HighLoader(mb); break;
                case Service.Pushback: Tug(mb); break;
                case Service.Deice: Deicer(mb); break;
                default: CabinVan(mb); break;
            }
            return mb;
        }

        static void Wheel(MeshBuilder mb, float x, float z, float r = 0.3f, float wide = 0.2f)
        {
            float s = mb.Smoothness; mb.Mat(0.1f);
            mb.Cylinder(new Vector3(x - wide * 0.5f, r, z), new Vector3(x + wide * 0.5f, r, z), r, 10, Palette.Tyre);
            mb.Mat(0.6f, 0.5f);
            mb.Cylinder(new Vector3(x - wide * 0.52f, r, z), new Vector3(x + wide * 0.52f, r, z), r * 0.5f, 8, Steel);
            mb.Mat(s);
        }

        static void Axle(MeshBuilder mb, float halfTrack, float z, float r = 0.3f, float wide = 0.2f) { Wheel(mb, -halfTrack, z, r, wide); Wheel(mb, halfTrack, z, r, wide); }

        /// <summary>A forward-control truck cab at the front (-Z), its back wall at z = back.</summary>
        static void Cab(MeshBuilder mb, float back, float width, Color c, float floor = 0.5f, float height = 1.25f, float len = 1.05f)
        {
            float z = back - len * 0.5f;
            mb.Box(new Vector3(0, floor + height * 0.3f, z), new Vector3(width, height * 0.6f, len), c);
            mb.Box(new Vector3(0, floor + height * 0.8f, z + 0.04f), new Vector3(width - 0.08f, height * 0.4f, len - 0.1f), c);
            float sm = mb.Smoothness; mb.Mat(0.92f);
            mb.Box(new Vector3(0, floor + height * 0.78f, back - len - 0.005f + 0.05f), new Vector3(width - 0.22f, height * 0.3f, 0.04f), Glass);            // windscreen
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * (width * 0.5f - 0.03f), floor + height * 0.78f, z - 0.05f), new Vector3(0.03f, height * 0.28f, len * 0.5f), Glass);
            mb.Mat(sm);
            mb.Box(new Vector3(0, floor + 0.08f, back - len - 0.04f), new Vector3(width + 0.04f, 0.2f, 0.1f), Dark);                                         // bumper
            mb.Glow = true;
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * (width * 0.5f - 0.18f), floor + 0.3f, back - len - 0.01f), new Vector3(0.2f, 0.1f, 0.03f), Palette.Hex(0xFFF4D0));
            mb.Box(new Vector3(0, floor + height + 0.07f, z), new Vector3(0.36f, 0.1f, 0.14f), Amber);                                                       // beacon bar
            mb.Glow = false;
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * (width * 0.5f + 0.1f), floor + height * 0.75f, back - len + 0.12f), new Vector3(0.1f, 0.22f, 0.05f), Dark);   // mirrors
        }

        static void Chassis(MeshBuilder mb, float z0, float z1, float width = 0.9f, float y = 0.42f)
        {
            mb.Box(new Vector3(0, y, (z0 + z1) * 0.5f), new Vector3(width, 0.16f, z1 - z0), Dark);
        }

        /// <summary>Red-and-white chevrons across a vehicle's tail.</summary>
        static void Chevrons(MeshBuilder mb, float z, float y, float width, float height)
        {
            int n = Mathf.Max(4, (int)(width / 0.18f));
            for (int k = 0; k < n; k++) mb.Box(new Vector3(-width * 0.5f + (k + 0.5f) * width / n, y, z), new Vector3(width / n, height, 0.02f), k % 2 == 0 ? Palette.Hex(0xD9342B) : White);
        }

        static void Bowser(MeshBuilder mb)
        {
            Chassis(mb, -1.9f, 2.1f);
            Cab(mb, -1.0f, 1.5f, White);
            mb.Box(new Vector3(0, 0.62f, -1.5f), new Vector3(1.52f, 0.14f, 1.0f), Palette.Fuel);                // the operator's stripe on the cab
            // The tank: an oval barrel on saddles, a catwalk and manholes along the top, the stripe along its flank.
            mb.Mat(0.7f, 0.55f);
            mb.Push().Translate(0, 1.22f, 0.5f).Scale(new Vector3(1f, 0.82f, 1f));
            mb.Cylinder(new Vector3(0, 0, -1.35f), new Vector3(0, 0, 1.35f), 0.78f, 0.78f, 16, Palette.Hex(0xD8DCE0));
            mb.Sphere(new Vector3(0, 0, -1.35f), new Vector3(0.78f, 0.78f, 0.2f), 12, Palette.Hex(0xD8DCE0));
            mb.Sphere(new Vector3(0, 0, 1.35f), new Vector3(0.78f, 0.78f, 0.2f), 12, Palette.Hex(0xD8DCE0));
            mb.Pop();
            mb.Mat(0.45f);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.775f, 1.2f, 0.5f), new Vector3(0.03f, 0.3f, 2.5f), Palette.Fuel);
            foreach (float z in new[] { -0.5f, 0.5f, 1.5f }) mb.Box(new Vector3(0, 0.56f, z), new Vector3(1.2f, 0.16f, 0.16f), Dark);
            mb.Box(new Vector3(0, 1.9f, 0.5f), new Vector3(0.5f, 0.04f, 2.6f), Steel);
            foreach (float z in new[] { -0.3f, 0.6f, 1.5f }) mb.Cylinder(new Vector3(0, 1.9f, z), new Vector3(0, 1.98f, z), 0.2f, 10, Dark);
            // The pump cabinet behind the cab, its hose reel, and the hazard plate.
            mb.Box(new Vector3(0, 0.95f, -0.88f), new Vector3(1.5f, 0.9f, 0.3f), Palette.Hex(0xC9CED3));
            mb.Cylinder(new Vector3(-0.78f, 0.95f, -0.88f), new Vector3(-0.7f, 0.95f, -0.88f), 0.3f, 12, Palette.Hex(0xB3261E));
            mb.Cylinder(new Vector3(-0.8f, 0.95f, -0.88f), new Vector3(-0.79f, 0.95f, -0.88f), 0.12f, 8, Dark);
            mb.Push().Translate(0.78f, 1.0f, 1.2f).Rotate(Quaternion.Euler(45, 0, 0)); mb.Box(Vector3.zero, new Vector3(0.02f, 0.3f, 0.3f), Palette.Hex(0xE8372E)); mb.Pop();
            mb.Box(new Vector3(0, 0.5f, 2.08f), new Vector3(1.5f, 0.12f, 0.08f), Dark);
            Chevrons(mb, 2.12f, 0.75f, 1.4f, 0.22f);
            mb.Glow = true; foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.6f, 0.5f, 2.13f), new Vector3(0.16f, 0.1f, 0.02f), Palette.Hex(0xE8372E)); mb.Glow = false;
            Axle(mb, 0.66f, -1.45f, 0.36f, 0.24f); Axle(mb, 0.66f, 0.9f, 0.36f, 0.24f); Axle(mb, 0.66f, 1.65f, 0.36f, 0.24f);
        }

        static void BaggageTrain(MeshBuilder mb)
        {
            // The tractor: a squat slab with an open seat, a roll bar and a beacon.
            var c = Palette.Baggage;
            mb.Box(new Vector3(0, 0.5f, -1.6f), new Vector3(1.0f, 0.5f, 1.3f), c);
            mb.Box(new Vector3(0, 0.82f, -1.95f), new Vector3(0.9f, 0.2f, 0.5f), c);
            mb.Box(new Vector3(0, 0.36f, -2.28f), new Vector3(1.04f, 0.26f, 0.08f), Dark);
            mb.Box(new Vector3(0, 0.82f, -1.3f), new Vector3(0.6f, 0.14f, 0.45f), Dark);                              // seat
            mb.Box(new Vector3(0, 1.08f, -1.08f), new Vector3(0.6f, 0.4f, 0.08f), Dark);
            mb.Cylinder(new Vector3(0, 0.9f, -1.62f), new Vector3(0, 1.12f, -1.5f), 0.02f, 4, Dark); mb.Cylinder(new Vector3(0, 1.1f, -1.52f), new Vector3(0, 1.14f, -1.48f), 0.13f, 8, Dark);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.44f, 1.2f, -1.02f), new Vector3(0.05f, 0.95f, 0.05f), Dark);
            mb.Box(new Vector3(0, 1.68f, -1.02f), new Vector3(0.93f, 0.05f, 0.05f), Dark);
            mb.Glow = true; mb.Cylinder(new Vector3(0, 1.7f, -1.02f), new Vector3(0, 1.82f, -1.02f), 0.07f, 8, Amber);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.36f, 0.6f, -2.26f), new Vector3(0.16f, 0.1f, 0.03f), Palette.Hex(0xFFF4D0));
            mb.Glow = false;
            Axle(mb, 0.5f, -2.0f, 0.24f, 0.18f); Axle(mb, 0.5f, -1.2f, 0.27f, 0.2f);
            // Two carts on a drawbar: a deck with corner posts, a canvas roof, cases and bags stacked on it.
            Color[] bags = { Palette.Hex(0x2D4E7C), Palette.Hex(0x8E2F2F), Palette.Hex(0x3C3C40), Palette.Hex(0x2F7A5B), Palette.Hex(0xC98A2C), Palette.Hex(0x6B4FBB) };
            for (int k = 0; k < 2; k++)
            {
                float z = 0.15f + k * 1.75f;
                mb.Box(new Vector3(0, 0.3f, z - 0.82f), new Vector3(0.05f, 0.05f, 0.4f), Dark);                       // drawbar
                mb.Box(new Vector3(0, 0.4f, z), new Vector3(1.0f, 0.1f, 1.4f), Palette.Hex(0x4A535C));
                foreach (float ex in new[] { -1f, 1f }) foreach (float ez in new[] { -1f, 1f }) mb.Box(new Vector3(ex * 0.47f, 0.95f, z + ez * 0.67f), new Vector3(0.05f, 1.0f, 0.05f), Steel);
                mb.Box(new Vector3(0, 1.47f, z), new Vector3(1.04f, 0.05f, 1.44f), k == 0 ? Palette.Hex(0xD7DBDF) : c);
                mb.Box(new Vector3(0.49f, 0.95f, z), new Vector3(0.02f, 0.9f, 1.3f), Palette.Hex(0x5E6A75));          // the closed side curtain
                var rng = new System.Random(31 + k);
                for (int b = 0; b < 7; b++)
                {
                    float bw = 0.26f + (float)rng.NextDouble() * 0.2f, bh = 0.22f + (float)rng.NextDouble() * 0.16f, bl = 0.4f + (float)rng.NextDouble() * 0.2f;
                    mb.Box(new Vector3(-0.22f + (b % 2) * 0.4f, 0.45f + bh * 0.5f + (b / 4) * 0.36f, z - 0.45f + (b % 4) * 0.3f), new Vector3(bw, bh, bl), bags[rng.Next(bags.Length)]);
                }
                Axle(mb, 0.46f, z - 0.45f, 0.16f, 0.12f); Axle(mb, 0.46f, z + 0.45f, 0.16f, 0.12f);
            }
        }

        static void HighLoader(MeshBuilder mb)
        {
            var c = Palette.Catering;
            Chassis(mb, -1.9f, 2.0f);
            Cab(mb, -1.0f, 1.5f, White);
            // The body rides up on a scissor lift to the cabin door, with a bridge out over the cab.
            float lift = 1.15f, by = 0.62f + lift;
            foreach (float e in new[] { -1f, 1f })
                foreach (float t in new[] { -1f, 1f })
                {
                    mb.Push().Translate(e * 0.5f, 0.55f + lift * 0.5f, 0.55f).Rotate(Quaternion.Euler(t * Mathf.Atan2(lift, 2.2f) * Mathf.Rad2Deg, 0, 0));
                    mb.Box(Vector3.zero, new Vector3(0.07f, 0.07f, Mathf.Sqrt(lift * lift + 2.2f * 2.2f)), Steel); mb.Pop();
                }
            mb.Box(new Vector3(0, 0.56f, 0.55f), new Vector3(1.2f, 0.1f, 2.6f), Dark);
            mb.Box(new Vector3(0, by + 0.95f, 0.55f), new Vector3(1.56f, 1.9f, 2.8f), White);
            mb.Box(new Vector3(0, by + 0.5f, 0.55f), new Vector3(1.58f, 0.34f, 2.82f), c);
            mb.Box(new Vector3(0, by + 1.5f, 0.55f), new Vector3(1.58f, 0.08f, 2.82f), c);
            mb.Box(new Vector3(0, by + 1.92f, 0.55f), new Vector3(1.5f, 0.04f, 2.7f), Palette.Hex(0xD0D4D8));
            // A knife and fork on the side, in blocks of paint.
            foreach (float e in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(e * 0.79f, by + 1.05f, 0.3f), new Vector3(0.02f, 0.5f, 0.05f), c); mb.Box(new Vector3(e * 0.79f, by + 1.25f, 0.3f), new Vector3(0.02f, 0.14f, 0.16f), c);
                mb.Box(new Vector3(e * 0.79f, by + 1.05f, 0.75f), new Vector3(0.02f, 0.5f, 0.05f), c); mb.Box(new Vector3(e * 0.79f, by + 1.2f, 0.78f), new Vector3(0.02f, 0.24f, 0.1f), c);
            }
            // The front platform with rails, and the roller door behind it.
            mb.Box(new Vector3(0, by + 0.04f, -1.35f), new Vector3(1.3f, 0.07f, 1.0f), Steel);
            foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(e * 0.63f, by + 0.5f, -1.35f), new Vector3(0.04f, 0.04f, 1.0f), Amber); foreach (float z in new[] { -1.82f, -1.35f }) mb.Box(new Vector3(e * 0.63f, by + 0.28f, z), new Vector3(0.04f, 0.5f, 0.04f), Amber); }
            for (int k = 0; k < 6; k++) mb.Box(new Vector3(0, by + 0.25f + k * 0.24f, -0.86f), new Vector3(1.1f, 0.2f, 0.02f), Palette.Hex(0xAEB4BA));
            Chevrons(mb, 1.97f, by + 0.5f, 1.5f, 0.3f);
            foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(e * 0.85f, 0.3f, -0.6f), new Vector3(0.3f, 0.06f, 0.3f), Dark); mb.Box(new Vector3(e * 0.85f, 0.45f, -0.6f), new Vector3(0.08f, 0.3f, 0.08f), Steel); mb.Box(new Vector3(e * 0.85f, 0.3f, 1.9f), new Vector3(0.3f, 0.06f, 0.3f), Dark); mb.Box(new Vector3(e * 0.85f, 0.45f, 1.9f), new Vector3(0.08f, 0.3f, 0.08f), Steel); }   // stabilisers down
            Axle(mb, 0.66f, -1.45f, 0.34f, 0.22f); Axle(mb, 0.66f, 1.3f, 0.34f, 0.26f);
        }

        static void CabinVan(MeshBuilder mb)
        {
            var c = Palette.Cleaning;
            // A panel van with the crew's stripe, a roof rack with a ladder, a side door, and a vacuum hose reel at the back.
            mb.Box(new Vector3(0, 0.95f, 0.35f), new Vector3(1.5f, 1.3f, 2.9f), White);
            mb.Box(new Vector3(0, 0.62f, -1.45f), new Vector3(1.5f, 0.64f, 0.8f), White);
            mb.Push().Translate(0, 1.27f, -1.23f).Rotate(Quaternion.Euler(26, 0, 0)); mb.Mat(0.92f); mb.Box(Vector3.zero, new Vector3(1.36f, 0.74f, 0.05f), Glass); mb.Mat(0.45f); mb.Pop();
            mb.Mat(0.92f); foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.755f, 1.25f, -0.7f), new Vector3(0.02f, 0.4f, 0.6f), Glass); mb.Mat(0.45f);
            foreach (float e in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(e * 0.757f, 0.8f, 0.35f), new Vector3(0.02f, 0.26f, 2.9f), c);
                mb.Box(new Vector3(e * 0.757f, 1.0f, 0.5f), new Vector3(0.022f, 1.1f, 0.03f), Palette.Hex(0xC4C9CE)); mb.Box(new Vector3(e * 0.757f, 1.0f, 1.3f), new Vector3(0.022f, 1.1f, 0.03f), Palette.Hex(0xC4C9CE));
                // The crew's mark: a sparkle.
                mb.Box(new Vector3(e * 0.76f, 1.3f, 0.9f), new Vector3(0.02f, 0.3f, 0.06f), c); mb.Box(new Vector3(e * 0.76f, 1.3f, 0.9f), new Vector3(0.02f, 0.06f, 0.3f), c);
            }
            mb.Box(new Vector3(0, 0.36f, -1.86f), new Vector3(1.54f, 0.2f, 0.1f), Dark); mb.Box(new Vector3(0, 0.36f, 1.82f), new Vector3(1.54f, 0.2f, 0.1f), Dark);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(e * 0.6f, 1.64f, 0.4f), new Vector3(0.04f, 0.08f, 2.4f), Dark);
            mb.Box(new Vector3(0.1f, 1.72f, 0.4f), new Vector3(0.4f, 0.05f, 2.2f), Amber);
            for (int k = 0; k < 6; k++) mb.Box(new Vector3(0.1f, 1.74f, -0.55f + k * 0.38f), new Vector3(0.4f, 0.06f, 0.04f), Steel);
            mb.Cylinder(new Vector3(-0.3f, 1.3f, 1.82f), new Vector3(-0.3f, 1.3f, 1.9f), 0.26f, 12, Palette.Hex(0x39424A));
            mb.Glow = true;
            foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(e * 0.56f, 0.72f, -1.86f), new Vector3(0.2f, 0.1f, 0.03f), Palette.Hex(0xFFF4D0)); mb.Box(new Vector3(e * 0.62f, 0.8f, 1.81f), new Vector3(0.12f, 0.24f, 0.03f), Palette.Hex(0xE8372E)); }
            mb.Box(new Vector3(0, 1.66f, -0.9f), new Vector3(0.5f, 0.1f, 0.14f), Amber);
            mb.Glow = false;
            Axle(mb, 0.66f, -1.2f, 0.32f, 0.2f); Axle(mb, 0.66f, 1.15f, 0.32f, 0.2f);
        }

        static void Tug(MeshBuilder mb)
        {
            // A pushback tractor: long, wide and very low so it fits under a nose, a small cab to one side at the front,
            // four big wheels half sunk in the body, and the tow bar out ahead.
            var c = Palette.Hex(0xE8B21A);
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(1.9f, 0.56f, 3.6f), c, Palette.Hex(0xC79A12));
            mb.Box(new Vector3(0, 0.26f, 0), new Vector3(1.7f, 0.2f, 3.3f), Dark);
            foreach (float z in new[] { -1.84f, 1.84f }) mb.Box(new Vector3(0, 0.45f, z), new Vector3(1.96f, 0.5f, 0.12f), Dark);
            for (int k = 0; k < 8; k++) { mb.Box(new Vector3(-0.84f + k * 0.24f, 0.45f, -1.91f), new Vector3(0.12f, 0.36f, 0.02f), k % 2 == 0 ? Amber : Dark); mb.Box(new Vector3(-0.84f + k * 0.24f, 0.45f, 1.91f), new Vector3(0.12f, 0.36f, 0.02f), k % 2 == 0 ? Amber : Dark); }
            mb.Box(new Vector3(-0.42f, 1.15f, -0.9f), new Vector3(0.95f, 0.75f, 1.0f), c);
            mb.Mat(0.92f);
            mb.Box(new Vector3(-0.42f, 1.22f, -1.405f), new Vector3(0.8f, 0.46f, 0.02f), Glass); mb.Box(new Vector3(-0.42f, 1.22f, -0.395f), new Vector3(0.8f, 0.46f, 0.02f), Glass);
            foreach (float e in new[] { -1f, 1f }) mb.Box(new Vector3(-0.42f + e * 0.48f, 1.22f, -0.9f), new Vector3(0.02f, 0.46f, 0.84f), Glass);
            mb.Mat(0.45f);
            mb.Box(new Vector3(-0.42f, 1.55f, -0.9f), new Vector3(1.0f, 0.06f, 1.06f), Palette.Hex(0xC79A12));
            mb.Box(new Vector3(0.45f, 0.86f, 0.5f), new Vector3(0.8f, 0.16f, 1.6f), Palette.Hex(0xC79A12));                 // engine cover
            for (int k = 0; k < 5; k++) mb.Box(new Vector3(0.45f, 0.95f, -0.1f + k * 0.3f), new Vector3(0.6f, 0.02f, 0.08f), Dark);
            mb.Cylinder(new Vector3(0.75f, 0.9f, 1.4f), new Vector3(0.75f, 1.5f, 1.4f), 0.05f, 6, Steel);                   // exhaust
            mb.Glow = true;
            mb.Cylinder(new Vector3(-0.42f, 1.58f, -0.9f), new Vector3(-0.42f, 1.72f, -0.9f), 0.09f, 8, Amber);
            foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(e * 0.75f, 0.62f, -1.91f), new Vector3(0.2f, 0.1f, 0.03f), Palette.Hex(0xFFF4D0)); mb.Box(new Vector3(e * 0.75f, 0.62f, 1.91f), new Vector3(0.2f, 0.1f, 0.03f), Palette.Hex(0xE8372E)); }
            mb.Glow = false;
            // The tow bar to the nose gear.
            mb.Mat(0.6f, 0.5f);
            mb.Cylinder(new Vector3(0, 0.3f, -1.9f), new Vector3(0, 0.3f, -3.1f), 0.05f, 6, Palette.Hex(0xD9D9D9));
            mb.Box(new Vector3(0, 0.3f, -3.15f), new Vector3(0.3f, 0.1f, 0.12f), Palette.Hex(0xD9D9D9));
            mb.Mat(0.45f);
            foreach (float e in new[] { -1f, 1f }) { Wheel(mb, e * 0.88f, -1.1f, 0.42f, 0.3f); Wheel(mb, e * 0.88f, 1.1f, 0.42f, 0.3f); }
        }

        static void Deicer(MeshBuilder mb)
        {
            var c = Palette.Hex(0xF07A1A);
            Chassis(mb, -1.9f, 2.0f);
            Cab(mb, -1.0f, 1.5f, c);
            // The fluid tank, heater cabinet, and the boom folded up and forward with the operator's basket at its end.
            mb.Box(new Vector3(0, 1.1f, 0.6f), new Vector3(1.5f, 1.1f, 2.6f), White);
            mb.Box(new Vector3(0, 0.8f, 0.6f), new Vector3(1.52f, 0.18f, 2.62f), c);
            foreach (float e in new[] { -1f, 1f }) { mb.Box(new Vector3(e * 0.76f, 1.3f, 0.3f), new Vector3(0.02f, 0.3f, 0.3f), Palette.Deice); mb.Box(new Vector3(e * 0.76f, 1.3f, 0.3f), new Vector3(0.022f, 0.05f, 0.42f), White); mb.Box(new Vector3(e * 0.76f, 1.3f, 0.3f), new Vector3(0.022f, 0.42f, 0.05f), White); }
            mb.Box(new Vector3(0, 1.72f, 1.5f), new Vector3(1.0f, 0.14f, 0.7f), Palette.Hex(0xC9CED3));
            mb.Cylinder(new Vector3(0, 1.65f, 0.9f), new Vector3(0, 1.95f, 0.9f), 0.3f, 12, Dark);                           // turret
            float ang = 34f, len = 3.0f;
            mb.Push().Translate(0, 1.95f, 0.9f).Rotate(Quaternion.Euler(ang, 0, 0));
            mb.Box(new Vector3(0, 0, -len * 0.5f), new Vector3(0.26f, 0.26f, len), c);
            mb.Box(new Vector3(0, 0, -len - 0.5f), new Vector3(0.18f, 0.18f, 1.2f), White);
            mb.Mat(0.6f, 0.5f); mb.Cylinder(new Vector3(0.2f, -0.1f, -0.3f), new Vector3(0.2f, -0.02f, -len * 0.6f), 0.05f, 6, Steel); mb.Mat(0.45f);
            mb.Pop();
            var tip = new Vector3(0, 1.95f + Mathf.Sin(ang * Mathf.Deg2Rad) * (len + 1.1f), 0.9f - Mathf.Cos(ang * Mathf.Deg2Rad) * (len + 1.1f));
            mb.Box(tip + new Vector3(0, -0.05f, -0.4f), new Vector3(0.9f, 0.06f, 0.8f), Dark);
            foreach (float e in new[] { -1f, 1f }) { mb.Box(tip + new Vector3(e * 0.44f, 0.4f, -0.4f), new Vector3(0.04f, 0.9f, 0.8f), c); }
            mb.Box(tip + new Vector3(0, 0.4f, -0.8f), new Vector3(0.9f, 0.9f, 0.04f), c); mb.Box(tip + new Vector3(0, 0.4f, 0f), new Vector3(0.9f, 0.9f, 0.04f), c);
            mb.Cylinder(tip + new Vector3(0, 0.7f, -0.8f), tip + new Vector3(0, 0.45f, -1.5f), 0.04f, 6, Steel);                // the nozzle
            mb.Glow = true; mb.Box(tip + new Vector3(0, 0.9f, -0.4f), new Vector3(0.3f, 0.08f, 0.1f), Amber); mb.Glow = false;
            Chevrons(mb, 1.92f, 0.95f, 1.4f, 0.26f);
            Axle(mb, 0.66f, -1.45f, 0.36f, 0.24f); Axle(mb, 0.66f, 1.3f, 0.36f, 0.26f);
        }
    }
}
