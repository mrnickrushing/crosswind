using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crosswind.Game
{
    /// <summary>
    /// Accumulates flat-shaded, vertex-colored geometry with a transform stack. Every procedural model
    /// in the game (trains, buildings, trees, track) is built with this and rendered with the single
    /// LastTide/VertexLit material, so the whole world batches into very few draw calls.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Verts = new List<Vector3>(4096);
        public readonly List<Vector3> Normals = new List<Vector3>(4096);
        public readonly List<Color32> Colors = new List<Color32>(4096);
        public readonly List<Vector3> Surface = new List<Vector3>(4096); // x = smoothness, y = metallic, z = SurfaceKind + 1 (0 = plain)
        public readonly List<int> Tris = new List<int>(8192);

        Matrix4x4 _m = Matrix4x4.identity;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public float Smoothness = 0.15f;
        public float Metallic = 0f;
        public bool Glow;
        /// <summary>Detail texture for subsequent primitives; Auto picks it from the color's palette entry.</summary>
        public SurfaceKind Surf = SurfaceKind.Auto;

        public int VertexCount { get { return Verts.Count; } }

        // ------------------------------------------------------------ transform stack

        /// <summary>The current local-to-world transform (for placing art inside a pushed frame).</summary>
        public Matrix4x4 Matrix { get { return _m; } }
        public MeshBuilder Push() { _stack.Push(_m); return this; }
        public MeshBuilder Pop() { _m = _stack.Pop(); return this; }
        public MeshBuilder Translate(Vector3 t) { _m = _m * Matrix4x4.Translate(t); return this; }
        public MeshBuilder Translate(float x, float y, float z) { return Translate(new Vector3(x, y, z)); }
        public MeshBuilder Rotate(Quaternion q) { _m = _m * Matrix4x4.Rotate(q); return this; }
        public MeshBuilder RotateY(float deg) { return Rotate(Quaternion.Euler(0, deg, 0)); }
        public MeshBuilder Scale(Vector3 s) { _m = _m * Matrix4x4.Scale(s); return this; }
        public MeshBuilder Scale(float s) { return Scale(new Vector3(s, s, s)); }
        public MeshBuilder SetMatrix(Matrix4x4 m) { _m = m; return this; }

        /// <summary>Sets surface properties for subsequent primitives.</summary>
        public MeshBuilder Mat(float smooth, float metal = 0, bool glow = false) { Smoothness = smooth; Metallic = metal; Glow = glow; return this; }
        public MeshBuilder Matte() { return Mat(0.12f); }
        public MeshBuilder Paint() { return Mat(0.55f); }
        public MeshBuilder Shiny() { return Mat(0.75f, 0.6f); }
        public MeshBuilder Lit() { return Mat(0.3f, 0, true); }

        // ------------------------------------------------------------ low level

        Color32 C(Color c)
        {
            var c32 = (Color32)c;
            c32.a = (byte)(Glow ? 0 : 255);
            return c32;
        }

        int V(Vector3 p, Vector3 n, Color c)
        {
            Verts.Add(_m.MultiplyPoint3x4(p));
            Normals.Add(_m.MultiplyVector(n).normalized);
            Colors.Add(C(c));
            var kind = Surf == SurfaceKind.Auto ? SurfaceKind.None : Surf;
            Surface.Add(new Vector3(Smoothness, Metallic, kind == SurfaceKind.None || Glow ? 0 : (int)kind + 1));
            return Verts.Count - 1;
        }

        /// <summary>Flat triangle; normal computed from winding (clockwise = front, Unity convention).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = V(a, n, col); V(b, n, col); V(c, n, col);
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (n == Vector3.zero) n = Vector3.Cross(c - a, d - a).normalized;
            int i = V(a, n, col); V(b, n, col); V(c, n, col); V(d, n, col);
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
            Tris.Add(i); Tris.Add(i + 2); Tris.Add(i + 3);
        }

        /// <summary>Smooth quad with explicit per-vertex normals/colors (terrain, rails).</summary>
        public void QuadSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Color ca, Color cb, Color cc, Color cd)
        {
            int i = V(a, na, ca); V(b, nb, cb); V(c, nc, cc); V(d, nd, cd);
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
            Tris.Add(i); Tris.Add(i + 2); Tris.Add(i + 3);
        }

        // ------------------------------------------------------------ primitives

        /// <summary>Axis-aligned box (in the current transform) centered at c.</summary>
        public void Box(Vector3 c, Vector3 size, Color col) { Box(c, size, col, col); }

        /// <summary>Box with a separate top color (roofs, platforms).</summary>
        public void Box(Vector3 c, Vector3 size, Color side, Color top)
        {
            var h = size * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z),
                    p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z),
                    p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z),
                    p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, side); // -z
            Quad(p101, p111, p011, p001, side); // +z
            Quad(p001, p011, p010, p000, side); // -x
            Quad(p100, p110, p111, p101, side); // +x
            Quad(p010, p011, p111, p110, top);  // +y
            Quad(p001, p000, p100, p101, Darken(side, 0.6f)); // -y
        }

        /// <summary>Box from min corner — convenient for walls.</summary>
        public void BoxMin(Vector3 min, Vector3 size, Color col) { Box(min + size * 0.5f, size, col); }

        /// <summary>Frustum/cylinder between a and b (any orientation).</summary>
        public void Cylinder(Vector3 a, Vector3 b, float ra, float rb, int seg, Color col, bool caps = true, bool smooth = true)
        {
            var axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            var dir = axis / len;
            var u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(dir, u);
            float slope = (ra - rb) / len;
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg * Mathf.PI * 2, t1 = (i + 1) / (float)seg * Mathf.PI * 2;
                Vector3 d0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0), d1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
                Vector3 a0 = a + d0 * ra, a1 = a + d1 * ra, b0 = b + d0 * rb, b1 = b + d1 * rb;
                if (smooth)
                {
                    Vector3 n0 = (d0 + dir * slope).normalized, n1 = (d1 + dir * slope).normalized;
                    QuadSmooth(a0, a1, b1, b0, n0, n1, n1, n0, col, col, col, col);
                }
                else Quad(a0, a1, b1, b0, col);
                if (caps)
                {
                    if (ra > 0) Tri(a, a1, a0, col);
                    if (rb > 0) Tri(b, b0, b1, col);
                }
            }
        }

        public void Cylinder(Vector3 a, Vector3 b, float r, int seg, Color col, bool caps = true)
        { Cylinder(a, b, r, r, seg, col, caps); }

        /// <summary>Low-poly ellipsoid.</summary>
        public void Sphere(Vector3 c, Vector3 radius, int seg, Color col, bool smooth = true)
        {
            int rings = Mathf.Max(3, seg / 2);
            for (int r = 0; r < rings; r++)
            {
                float p0 = Mathf.PI * r / rings - Mathf.PI / 2, p1 = Mathf.PI * (r + 1) / rings - Mathf.PI / 2;
                for (int s = 0; s < seg; s++)
                {
                    float t0 = Mathf.PI * 2 * s / seg, t1 = Mathf.PI * 2 * (s + 1) / seg;
                    Vector3 n00 = Sph(p0, t0), n01 = Sph(p0, t1), n10 = Sph(p1, t0), n11 = Sph(p1, t1);
                    Vector3 a = c + Vector3.Scale(n00, radius), b = c + Vector3.Scale(n10, radius), cc = c + Vector3.Scale(n11, radius), d = c + Vector3.Scale(n01, radius);
                    if (smooth) QuadSmooth(a, b, cc, d, n00, n10, n11, n01, col, col, col, col);
                    else Quad(a, b, cc, d, col);
                }
            }
        }

        static Vector3 Sph(float phi, float theta) { return new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta)); }

        /// <summary>Gable roof: ridge along X. Base centered at c (bottom of the roof), size = (length, height, depth).</summary>
        public void GableRoof(Vector3 c, Vector3 size, Color roof, Color gable, float overhang = 0.3f)
        {
            float hx = size.x / 2 + overhang, hz = size.z / 2 + overhang, h = size.y;
            Vector3 l0 = c + new Vector3(-hx, 0, -hz), l1 = c + new Vector3(-hx, 0, hz), r0 = c + new Vector3(hx, 0, -hz), r1 = c + new Vector3(hx, 0, hz);
            Vector3 rl = c + new Vector3(-hx, h, 0), rr = c + new Vector3(hx, h, 0);
            Quad(l0, rl, rr, r0, roof);
            Quad(r1, rr, rl, l1, roof);
            // gables (inset to wall plane)
            float gx = size.x / 2;
            Tri(c + new Vector3(-gx, 0, size.z / 2), c + new Vector3(-gx, h * (size.z / 2) / hz, 0), c + new Vector3(-gx, 0, -size.z / 2), gable);
            Tri(c + new Vector3(gx, 0, -size.z / 2), c + new Vector3(gx, h * (size.z / 2) / hz, 0), c + new Vector3(gx, 0, size.z / 2), gable);
            // underside
            Quad(l1, l0, r0, r1, Darken(roof, 0.5f));
        }

        /// <summary>Four-sided pyramid / hip roof.</summary>
        public void HipRoof(Vector3 c, Vector3 size, Color roof, float overhang = 0.3f, float ridge = 0.35f)
        {
            float hx = size.x / 2 + overhang, hz = size.z / 2 + overhang, h = size.y;
            float rx = hx * ridge;
            Vector3 a = c + new Vector3(-hx, 0, -hz), b = c + new Vector3(hx, 0, -hz), d = c + new Vector3(-hx, 0, hz), e = c + new Vector3(hx, 0, hz);
            Vector3 t0 = c + new Vector3(-rx, h, 0), t1 = c + new Vector3(rx, h, 0);
            Quad(a, t0, t1, b, roof);
            Quad(e, t1, t0, d, roof);
            Tri(d, t0, a, roof);
            Tri(b, t1, e, roof);
            Quad(d, a, b, e, Darken(roof, 0.5f));
        }

        /// <summary>Prism with an arbitrary convex polygon footprint (mesa strata, platforms).</summary>
        public void Prism(IList<Vector2> poly, float y0, float y1, Color side, Color top, float topInset, SurfaceKind topSurf)
        {
            // Sides in the current surface, cap in topSurf.
            var keep = Surf;
            Prism(poly, y0, y1, side, Color.clear, topInset, skipTop: true);
            Surf = topSurf;
            int n = poly.Count;
            Vector2 center = Vector2.zero;
            foreach (var p in poly) center += p;
            center /= n;
            for (int i = 0; i < n; i++)
            {
                var q0 = Vector2.Lerp(poly[i], center, topInset); var q1 = Vector2.Lerp(poly[(i + 1) % n], center, topInset);
                Tri(new Vector3(center.x, y1, center.y), new Vector3(q1.x, y1, q1.y), new Vector3(q0.x, y1, q0.y), top);
            }
            Surf = keep;
        }

        public void Prism(IList<Vector2> poly, float y0, float y1, Color side, Color top, float topInset = 0, bool skipTop = false)
        {
            int n = poly.Count;
            Vector2 center = Vector2.zero;
            foreach (var p in poly) center += p;
            center /= n;
            for (int i = 0; i < n; i++)
            {
                var p0 = poly[i]; var p1 = poly[(i + 1) % n];
                var q0 = Vector2.Lerp(p0, center, topInset); var q1 = Vector2.Lerp(p1, center, topInset);
                Quad(new Vector3(p0.x, y0, p0.y), new Vector3(q0.x, y1, q0.y), new Vector3(q1.x, y1, q1.y), new Vector3(p1.x, y0, p1.y), side);
                if (!skipTop) Tri(new Vector3(center.x, y1, center.y), new Vector3(q1.x, y1, q1.y), new Vector3(q0.x, y1, q0.y), top);
            }
        }

        /// <summary>
        /// Smooth-shaded prism walls (no cap): vertex normals averaged around the outline so curved cliffs don't break
        /// into flat facets. <paramref name="tint"/> gives each outline vertex a brightness (weathering streaks);
        /// the foot of the wall is darkened by <paramref name="footShade"/>.
        /// </summary>
        public void PrismWalls(IList<Vector2> poly, float y0, float y1, Color side, System.Func<int, float> tint, float footShade = 0.85f)
        {
            int n = poly.Count;
            Vector2 center = Vector2.zero;
            foreach (var p in poly) center += p;
            center /= n;
            var normals = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 Out(Vector2 a0, Vector2 a1)
                {
                    var e = a1 - a0;
                    var o = new Vector2(e.y, -e.x).normalized;
                    return Vector2.Dot(o, (a0 + a1) * 0.5f - center) < 0 ? -o : o;
                }
                var m = Out(poly[(i + n - 1) % n], poly[i]) + Out(poly[i], poly[(i + 1) % n]);
                normals[i] = new Vector3(m.x, 0, m.y).normalized;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var p0 = poly[i]; var p1 = poly[j];
                Color t0 = Darken(side, tint(i)), t1 = Darken(side, tint(j));
                QuadSmooth(new Vector3(p0.x, y0, p0.y), new Vector3(p0.x, y1, p0.y), new Vector3(p1.x, y1, p1.y), new Vector3(p1.x, y0, p1.y),
                    normals[i], normals[i], normals[j], normals[j], Darken(t0, footShade), t0, t1, Darken(t1, footShade));
            }
        }

        /// <summary>Appends another builder's geometry, transformed by the current matrix.</summary>
        public void Append(MeshBuilder other)
        {
            int baseIndex = Verts.Count;
            for (int i = 0; i < other.Verts.Count; i++)
            {
                Verts.Add(_m.MultiplyPoint3x4(other.Verts[i]));
                Normals.Add(_m.MultiplyVector(other.Normals[i]).normalized);
            }
            Colors.AddRange(other.Colors);
            Surface.AddRange(other.Surface);
            foreach (var t in other.Tris) Tris.Add(t + baseIndex);
        }

        public void Clear()
        {
            Verts.Clear(); Normals.Clear(); Colors.Clear(); Surface.Clear(); Tris.Clear();
            _m = Matrix4x4.identity; _stack.Clear();
        }

        public Mesh ToMesh(string name = "Procedural")
        {
            var mesh = new Mesh { name = name };
            if (Verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(1, Surface);
            mesh.SetTriangles(Tris, 0, true);
            return mesh;
        }

        /// <summary>Creates a GameObject with this geometry and the shared world material.</summary>
        public GameObject ToObject(string name, Transform parent, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.World;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static Color Darken(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a); }
        public static Color Lerp(Color a, Color b, float t) { return Color.Lerp(a, b, t); }
        public static Color Lighten(Color c, float f) { return Color.Lerp(c, Color.white, f); }
    }
}
