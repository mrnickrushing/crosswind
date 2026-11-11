using System.Collections.Generic;
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
        static Material _kit;
        /// <summary>For the free kit meshes: the same shader, ignoring the surface channel those meshes don't carry
        /// (reading it as smoothness made some of them shine like mirrors).</summary>
        public static Material Kit
        {
            get
            {
                if (_kit != null) return _kit;
                _kit = new Material(World) { name = "Kit" };
                _kit.SetFloat("_KitSurface", 1);
                return _kit;
            }
        }

        static Material _world;
        static readonly Dictionary<string, Material> _surfaces = new Dictionary<string, Material>();

        /// <summary>Metres one repeat of a surface covers.</summary>
        public static float Tile(string surface) { return surface == "Grass" ? 9f : surface == "Concrete" ? 13f : surface == "Asphalt" ? 7f : 8f; }

        /// <summary>A textured ground material (Resources/Textures/{name}.jpg + {name}Normal.jpg), lit, matte.</summary>
        public static Material Surface(string name, Color tint)
        {
            if (tint == Color.white) return Surface(name);
            string key = name + tint;
            Material m;
            if (_surfaces.TryGetValue(key, out m)) return m;
            m = new Material(Surface(name)) { name = key };
            m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
            return _surfaces[key] = m;
        }

        public static Material Surface(string name)
        {
            Material m;
            if (_surfaces.TryGetValue(name, out m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            var tex = Resources.Load<Texture2D>("Textures/" + name);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", name == "Asphalt" ? new Color(1.9f, 1.9f, 2.0f) : name == "Grass" ? new Color(1.05f, 1.12f, 0.9f) : name == "Concrete" ? new Color(1.15f, 1.15f, 1.12f) : Color.white);
            m.SetFloat("_Smoothness", name == "Grass" ? 0.05f : 0.22f);
            m.SetFloat("_Metallic", 0);
            var n = Resources.Load<Texture2D>("Textures/" + name + "Normal");
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            return _surfaces[name] = m;
        }


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

    /// <summary>
    /// Ground laid in real textures: quads on the ground whose UVs are their world position, so asphalt, concrete and
    /// grass tile seamlessly across pieces. One mesh per surface.
    /// </summary>
    public sealed class Ground
    {
        sealed class Part { public string Surface; public Color Tint; public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector2> Uv = new List<Vector2>(); public readonly List<int> T = new List<int>(); public readonly List<Color> C = new List<Color>(); }
        readonly Dictionary<string, Part> _parts = new Dictionary<string, Part>();

        /// <summary>A rectangle of a surface, centred at (x, z), at height y, optionally turned and tinted.</summary>
        public void Rect(string surface, float x, float z, float width, float length, float y = 0.02f, float yaw = 0, Color? tint = null)
        {
            // A tint is its own material (the lit shader doesn't read vertex colours), so pieces batch by surface and tint.
            var tc = tint ?? Color.white;
            string key = surface + "|" + tc.r.ToString("0.00") + "," + tc.g.ToString("0.00") + "," + tc.b.ToString("0.00");
            Part p;
            if (!_parts.TryGetValue(key, out p)) { _parts[key] = p = new Part(); p.Surface = surface; p.Tint = tc; }
            float tile = Materials.Tile(surface);
            var rot = Quaternion.Euler(0, yaw, 0);
            int i0 = p.V.Count;
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) })
            {
                var w = new Vector3(x, y, z) + rot * new Vector3(c.x * width * 0.5f, 0, c.y * length * 0.5f);
                p.V.Add(w); p.Uv.Add(new Vector2(w.x / tile, w.z / tile)); p.C.Add(tint ?? Color.white);
            }
            p.T.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
        }

        public void Build(Transform parent)
        {
            foreach (var kv in _parts)
            {
                var go = new GameObject("Ground " + kv.Key);
                go.transform.SetParent(parent, false);
                var m = new Mesh { name = kv.Key };
                m.SetVertices(kv.Value.V); m.SetUVs(0, kv.Value.Uv); m.SetColors(kv.Value.C); m.SetTriangles(kv.Value.T, 0);
                m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Materials.Surface(kv.Value.Surface, kv.Value.Tint);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
