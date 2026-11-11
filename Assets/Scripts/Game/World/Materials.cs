using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>What a surface is, for the shader's detail (plain for now: the meshes are vertex-coloured).</summary>
    public enum SurfaceKind
    {
        Auto = -2, None = -1,
        Brick, Siding, Shingles, RoofTiles, Tin, Stone, Concrete, PaintedMetal, Rust, Bark, WoodGrain, Planks, Gravel, Dirt,
    }

    /// <summary>Runtime-created materials (shaders live in Resources/Shaders so builds include them).</summary>
    public static class Materials
    {
        static Material _world;

        /// <summary>Vertex colours, lit, with per-vertex smoothness and metallic: everything built from code uses it.</summary>
        public static Material World
        {
            get
            {
                if (_world != null) return _world;
                var s = Resources.Load<Shader>("Shaders/VertexLit") ?? Shader.Find("Crosswind/VertexLit");
                _world = new Material(s) { name = "World", enableInstancing = true };
                return _world;
            }
        }
    }

    public static class Palette
    {
        public static Color Hex(uint rgb) { return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1); }

        public static readonly Color Grass = Hex(0x6E9A4A), GrassDark = Hex(0x5A8440), GrassWorn = Hex(0x9A9A5C);
        public static readonly Color Asphalt = Hex(0x3C4046), AsphaltPale = Hex(0x565A60), Concrete = Hex(0xB8B4AA), ConcreteDark = Hex(0x8E8A82);
        public static readonly Color Gravel = Hex(0x9A8E7C), Dirt = Hex(0x8A6E4E);
        public static readonly Color Paint = Hex(0xF2F0E6), PaintYellow = Hex(0xF2C230);
        public static readonly Color Glass = Hex(0x7FB4CC), Steel = Hex(0x9AA2AA), SteelDark = Hex(0x5A6068);
        public static readonly Color Wood = Hex(0x9C6B3F), WoodPale = Hex(0xC8A27A), Roof = Hex(0xA84A3A), RoofBlue = Hex(0x3E6A8A);
        public static readonly Color White = Hex(0xF4F4F0), Black = Hex(0x22252A), Tyre = Hex(0x1E1E20);
        public static readonly Color Fuel = Hex(0xD8442E), Baggage = Hex(0x3C7AC8), Catering = Hex(0xE89A2A), Cleaning = Hex(0x4AB07A);
    }
}
