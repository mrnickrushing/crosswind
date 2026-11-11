using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>Shared model helpers: airline colours, the aircraft object, free kit models (cars, cones) and a plain block.</summary>
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
                case "fjord": return Palette.Hex(0x2FA3A0);
                case "solstice": return Palette.Hex(0xF0624E);
                case "crown": return Palette.Hex(0x8A6B1E);
                default: return Palette.Steel;
            }
        }

        public static float Length(AircraftClass c)
        {
            return Aircraft.Length(c);
        }

        /// <summary>A flight's aircraft, built in code for its class and painted in its airline's livery (see <see cref="Aircraft"/>).</summary>
        public static GameObject PlaneObject(AircraftClass c, string airline, string name, Transform parent)
        {
            return Aircraft.Build(c, airline).ToObject(name, parent);
        }

        /// <summary>A free kit model (Resources/Models/{id}: one unit on its longest side, or one unit tall), placed, turned
        /// and sized in the parent's space. Null when it isn't there.</summary>
        public static GameObject Kit(string id, Transform parent, Vector3 at, float yaw, float size)
        {
            var mesh = Resources.Load<Mesh>("Models/" + id);
            if (mesh == null) return null;
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.Kit;
            return go;
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
