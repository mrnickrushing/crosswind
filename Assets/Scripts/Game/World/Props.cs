using System.Collections.Generic;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The terminal's furniture: free models (Resources/Models/Props/{id}, see Tools/art/props.py and CREDITS.md), each
    /// one unit on its longest side, standing on the ground with its front toward +Z. They are merged into whatever a
    /// <see cref="MeshBuilder"/> is building, sized in metres, so a furnished hall is still one mesh.
    /// </summary>
    public static class Props
    {
        sealed class Model { public Vector3[] Verts, Normals; public Color32[] Colors; public int[] Tris; public Vector3 Size; }
        static readonly Dictionary<string, Model> _models = new Dictionary<string, Model>();

        static Model Load(string id)
        {
            Model m;
            if (_models.TryGetValue(id, out m)) return m;
            var mesh = Resources.Load<Mesh>("Models/Props/" + id);
            if (mesh != null && mesh.isReadable)
            {
                m = new Model { Verts = mesh.vertices, Normals = mesh.normals, Colors = mesh.colors32, Tris = mesh.triangles, Size = mesh.bounds.size };
                if (m.Colors == null || m.Colors.Length != m.Verts.Length) { m.Colors = new Color32[m.Verts.Length]; for (int i = 0; i < m.Colors.Length; i++) m.Colors[i] = new Color32(200, 200, 200, 255); }
            }
            else Debug.LogWarning("No prop model: " + id);
            return _models[id] = m;
        }

        /// <summary>A prop's size as modelled (longest side 1): x across, y up, z front to back. Zero if it isn't there.</summary>
        public static Vector3 Size(string id) { var m = Load(id); return m == null ? Vector3.zero : m.Size; }

        /// <summary>
        /// Adds a prop standing at <paramref name="at"/>, <paramref name="tall"/> metres high. Yaw 0 turns its front to
        /// the builder's -x (the side passengers come from); 90 to +z, 180 to +x, 270 to -z.
        /// </summary>
        public static void Tall(this MeshBuilder mb, string id, Vector3 at, float yaw, float tall, Color? tint = null)
        {
            var m = Load(id);
            if (m != null) Add(mb, m, at, yaw, tall / Mathf.Max(0.001f, m.Size.y), tint);
        }

        /// <summary>The same, sized by its width (across its front) in metres.</summary>
        public static void Wide(this MeshBuilder mb, string id, Vector3 at, float yaw, float wide, Color? tint = null)
        {
            var m = Load(id);
            if (m != null) Add(mb, m, at, yaw, wide / Mathf.Max(0.001f, m.Size.x), tint);
        }

        /// <summary>The same, sized by its longest side in metres.</summary>
        public static void Long(this MeshBuilder mb, string id, Vector3 at, float yaw, float longest, Color? tint = null)
        {
            var m = Load(id);
            if (m != null) Add(mb, m, Matrix4x4.TRS(at, Quaternion.Euler(0, yaw - 90, 0), Vector3.one * longest), tint, null);
        }

        /// <summary>Stretched to a box: <paramref name="across"/> its front, <paramref name="tall"/>, <paramref name="deep"/> front to back (tables and counters, whose legs can be any length).</summary>
        public static void Fit(this MeshBuilder mb, string id, Vector3 at, float yaw, float across, float tall, float deep, Color? tint = null)
        {
            var m = Load(id);
            if (m == null) return;
            var k = new Vector3(across / Mathf.Max(0.001f, m.Size.x), tall / Mathf.Max(0.001f, m.Size.y), deep / Mathf.Max(0.001f, m.Size.z));
            Add(mb, m, Matrix4x4.TRS(at, Quaternion.Euler(0, yaw - 90, 0), k), tint, null);
        }

        /// <summary>Turned any way (a model aircraft climbing, a broom leaning), sized by its longest side.</summary>
        public static void Place(this MeshBuilder mb, string id, Vector3 at, Quaternion rot, float longest, Color? tint = null)
        {
            var m = Load(id);
            if (m != null) Add(mb, m, Matrix4x4.TRS(at, rot * Quaternion.Euler(0, -90, 0), Vector3.one * longest), tint, null);
        }

        /// <summary>Standing <paramref name="tall"/> metres high with its red parts (a flag's cloth) repainted.</summary>
        public static void Painted(this MeshBuilder mb, string id, Vector3 at, float yaw, float tall, Color paint)
        {
            var m = Load(id);
            if (m != null) Add(mb, m, Matrix4x4.TRS(at, Quaternion.Euler(0, yaw - 90, 0), Vector3.one * (tall / Mathf.Max(0.001f, m.Size.y))), null, paint);
        }

        static void Add(MeshBuilder mb, Model m, Vector3 at, float yaw, float scale, Color? tint)
        {
            Add(mb, m, Matrix4x4.TRS(at, Quaternion.Euler(0, yaw - 90, 0), Vector3.one * scale), tint, null);
        }

        static void Add(MeshBuilder mb, Model m, Matrix4x4 place, Color? tint, Color? paint)
        {
            var local = mb.Matrix * place;
            int start = mb.Verts.Count;
            var surface = new Vector3(mb.Smoothness, mb.Metallic, 0);
            bool tinted = tint.HasValue; Color t = tinted ? tint.Value : Color.white;
            bool painted = paint.HasValue; Color p = painted ? paint.Value : Color.white;
            byte alpha = (byte)(mb.Glow ? 0 : 255);
            for (int i = 0; i < m.Verts.Length; i++)
            {
                mb.Verts.Add(local.MultiplyPoint3x4(m.Verts[i]));
                mb.Normals.Add(local.MultiplyVector(m.Normals[i]).normalized);
                var c = m.Colors[i];
                if (tinted) c = new Color32((byte)Mathf.Min(255, c.r * t.r), (byte)Mathf.Min(255, c.g * t.g), (byte)Mathf.Min(255, c.b * t.b), 255);
                if (painted)
                {
                    int hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (c.r > c.g * 1.9f && c.r > c.b * 1.9f) { float v = hi / 255f; c = new Color32((byte)(p.r * v * 255), (byte)(p.g * v * 255), (byte)(p.b * v * 255), 255); }
                }
                c.a = alpha;
                mb.Colors.Add(c);
                mb.Surface.Add(surface);
            }
            for (int i = 0; i < m.Tris.Length; i++) mb.Tris.Add(start + m.Tris[i]);
        }
    }
}
