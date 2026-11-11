using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>Models built from code: aircraft by size class in their airline's colours, vehicles, buildings.
    /// Placeholders with the right silhouettes until free scanned and kit models replace them.</summary>
    public static class Models
    {
        public static Color AirlineColor(string airline)
        {
            switch (airline)
            {
                case "puffin": return Palette.Hex(0xE8742A);
                case "sunhop": return Palette.Hex(0xF2C230);
                case "zip": return Palette.Hex(0xD83A6A);
                case "meridian": return Palette.Hex(0x1E4E8C);
                case "atlas": return Palette.Hex(0x4A5A3A);
                default: return Palette.Steel;
            }
        }

        public static float Length(AircraftClass c)
        {
            switch (c) { case AircraftClass.A: return 5.5f; case AircraftClass.B: return 9f; case AircraftClass.C: return 13f; case AircraftClass.D: return 18f; default: return 23f; }
        }

        /// <summary>An aircraft, nose toward +Z, wheels on y = 0.</summary>
        public static MeshBuilder Plane(AircraftClass c, string airline)
        {
            var mb = new MeshBuilder { Smoothness = 0.55f };
            float L = Length(c), r = L * 0.075f, y = r * 1.5f;
            var body = Palette.White; var livery = AirlineColor(airline);
            bool prop = c == AircraftClass.A || c == AircraftClass.B;
            // Fuselage: a tube with a rounded nose and a tapered tail.
            mb.Cylinder(new Vector3(0, y, -L * 0.3f), new Vector3(0, y, L * 0.36f), r, r, 12, body);
            mb.Sphere(new Vector3(0, y, L * 0.36f), new Vector3(r, r, r * 2.2f), 10, body);
            mb.Cylinder(new Vector3(0, y + r * 0.25f, -L * 0.5f), new Vector3(0, y, -L * 0.3f), r * 0.3f, r, 12, body);
            // The livery: a belly band and the tail.
            mb.Box(new Vector3(0, y - r * 0.55f, 0), new Vector3(r * 1.5f, r * 0.5f, L * 0.62f), livery);
            mb.Box(new Vector3(0, y + r * 1.9f, -L * 0.43f), new Vector3(r * 0.16f, r * 2.6f, L * 0.13f), livery);
            mb.Box(new Vector3(0, y + r * 0.5f, -L * 0.45f), new Vector3(L * 0.3f, r * 0.14f, L * 0.08f), body);
            // Cockpit glass.
            mb.Smoothness = 0.9f;
            mb.Box(new Vector3(0, y + r * 0.45f, L * 0.37f), new Vector3(r * 1.3f, r * 0.5f, r * 1.1f), Palette.Hex(0x2A3A4A));
            mb.Smoothness = 0.55f;
            // Wings (high on the little ones, low and swept-looking on the jets).
            float span = L * (prop ? 1.15f : 1.0f), wy = prop ? y + r * 0.85f : y - r * 0.5f;
            mb.Box(new Vector3(0, wy, L * 0.02f), new Vector3(span, r * 0.16f, L * 0.16f), body);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(s * span * 0.49f, wy + r * 0.25f, L * 0.0f), new Vector3(r * 0.14f, r * 0.6f, L * 0.1f), livery);     // wingtips
                int engines = c >= AircraftClass.D ? 2 : 1;
                for (int e = 0; e < engines; e++)
                {
                    float ex = s * span * (0.2f + e * 0.17f);
                    if (prop)
                    {
                        mb.Cylinder(new Vector3(ex, wy - r * 0.1f, L * 0.02f), new Vector3(ex, wy - r * 0.1f, L * 0.16f), r * 0.32f, r * 0.26f, 8, Palette.Steel);
                        mb.Box(new Vector3(ex, wy - r * 0.1f, L * 0.17f), new Vector3(r * 1.9f, r * 0.12f, 0.06f), Palette.Black);
                    }
                    else
                        mb.Cylinder(new Vector3(ex, wy - r * 0.6f, L * -0.02f), new Vector3(ex, wy - r * 0.6f, L * 0.13f), r * 0.42f, r * 0.42f, 10, Palette.Steel);
                }
            }
            // Wheels.
            mb.Smoothness = 0.1f;
            foreach (var w in new[] { new Vector3(0, 0, L * 0.3f), new Vector3(-r * 1.4f, 0, -L * 0.04f), new Vector3(r * 1.4f, 0, -L * 0.04f) })
            {
                mb.Box(w + Vector3.up * (y - r) * 0.5f, new Vector3(0.12f, y - r, 0.12f), Palette.SteelDark);
                mb.Cylinder(w + new Vector3(-0.15f, r * 0.3f, 0), w + new Vector3(0.15f, r * 0.3f, 0), r * 0.3f, 8, Palette.Tyre);
            }
            return mb;
        }

        /// <summary>A service truck in its department's colour, nose toward +Z.</summary>
        public static MeshBuilder Truck(Service s)
        {
            var mb = new MeshBuilder { Smoothness = 0.4f };
            Color c = s == Service.Fuel ? Palette.Fuel : s == Service.Baggage ? Palette.Baggage : s == Service.Catering ? Palette.Catering : Palette.Cleaning;
            mb.Box(new Vector3(0, 0.75f, 0.9f), new Vector3(1.1f, 0.9f, 0.9f), Palette.White);                 // cab
            if (s == Service.Fuel) mb.Cylinder(new Vector3(0, 0.9f, -1.3f), new Vector3(0, 0.9f, 0.4f), 0.55f, 0.55f, 10, c);
            else mb.Box(new Vector3(0, s == Service.Catering ? 1.1f : 0.8f, -0.5f), new Vector3(1.15f, s == Service.Catering ? 1.5f : 0.9f, 1.8f), c);
            mb.Smoothness = 0.1f;
            foreach (float z in new[] { -1f, 0.8f }) foreach (float x in new[] { -0.55f, 0.55f })
                mb.Cylinder(new Vector3(x - 0.08f, 0.25f, z), new Vector3(x + 0.08f, 0.25f, z), 0.25f, 8, Palette.Tyre);
            return mb;
        }

        /// <summary>A flat-roofed block with a band of windows.</summary>
        public static void Block(MeshBuilder mb, Vector3 centre, Vector3 size, Color wall, Color roof, bool windows = true)
        {
            mb.Smoothness = 0.2f;
            mb.Box(centre + Vector3.up * size.y * 0.5f, size, wall, roof);
            if (!windows) return;
            mb.Smoothness = 0.85f;
            mb.Box(centre + Vector3.up * size.y * 0.55f, new Vector3(size.x + 0.08f, size.y * 0.3f, size.z * 0.86f), Palette.Glass);
            mb.Box(centre + Vector3.up * size.y * 0.55f, new Vector3(size.x * 0.86f, size.y * 0.3f, size.z + 0.08f), Palette.Glass);
            mb.Smoothness = 0.2f;
        }
    }
}
