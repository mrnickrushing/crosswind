using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The floor map over the concourse (a rating's SHOW ON THE FLOOR, and while something is being placed): every open
    /// cell tinted by whether passengers there have that want within reach (green) or not (red), and, while placing,
    /// the cells the new item would bring into reach in gold. One mesh of quads a few centimetres over the floor,
    /// rebuilt only when what it shows changes.
    /// </summary>
    public sealed class FloorMap : MonoBehaviour
    {
        static FloorMap _instance;
        MeshFilter _mf;
        Mesh _mesh;
        int _key = int.MinValue;

        static readonly Color Reached = new Color(0.25f, 0.85f, 0.45f, 0.42f), Missing = new Color(0.95f, 0.3f, 0.25f, 0.5f), Gained = new Color(1f, 0.82f, 0.25f, 0.7f);

        /// <summary>Shows the map for a want (null: hides it), with a ghost item's reach when one is being placed.</summary>
        public static void Show(Want? want, PlacedAmenity ghost = null)
        {
            var inside = Interior.Instance;
            if (inside == null) return;
            if (_instance == null || _instance.transform.parent != inside.transform)
            {
                if (!want.HasValue) return;
                var go = new GameObject("Floor map");
                go.transform.SetParent(inside.transform, false);
                _instance = go.AddComponent<FloorMap>();
                _instance._mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                _instance._mesh = new Mesh { name = "Floor map" };
                _instance._mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                _instance._mf.sharedMesh = _instance._mesh;
            }
            _instance.Set(want, ghost);
        }

        void Set(Want? want, PlacedAmenity ghost)
        {
            var s = GameRoot.Sim;
            bool on = want.HasValue && Sim.HasReach(want.Value) && s != null;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) { _key = int.MinValue; return; }
            int key = (int)want.Value * 7 + s.State.terminal.Count * 131 + s.State.bays.Count * 977 + s.FloorCols * 31 + s.FloorRows * 8191 + Mathf.RoundToInt(s.Coverage(want.Value) * 100000);
            if (ghost != null) key = key * 31 + ghost.x * 1000 + ghost.z * 7 + ghost.rot * 3 + 1;
            if (key == _key) return;
            _key = key;
            Build(s, want.Value, ghost);
        }

        void Build(Sim s, Want w, PlacedAmenity ghost)
        {
            var inside = Interior.Instance;
            int cols = s.FloorCols, rows = s.FloorRows;
            int gx0 = 0, gz0 = 0, gx1 = -1, gz1 = -1;
            if (ghost != null) s.ReachRect(ghost, w, out gx0, out gz0, out gx1, out gz1);
            var verts = new System.Collections.Generic.List<Vector3>(); var colors = new System.Collections.Generic.List<Color>(); var tris = new System.Collections.Generic.List<int>();
            float c = Sim.Cell, pad = 0.12f, y = 0.09f;
            var corner = inside.FloorCorner;
            for (int x = 0; x < cols; x++)
                for (int z = 0; z < rows; z++)
                {
                    if (!s.CellOpen(x, z)) continue;
                    bool reached = s.Covered(w, x, z);
                    bool gain = !reached && x >= gx0 && x <= gx1 && z >= gz0 && z <= gz1;
                    var col = gain ? Gained : reached ? Reached : Missing;
                    var o = corner + new Vector3(x * c + pad, y, z * c + pad);
                    int i = verts.Count;
                    verts.Add(o); verts.Add(o + new Vector3(0, 0, c - 2 * pad)); verts.Add(o + new Vector3(c - 2 * pad, 0, c - 2 * pad)); verts.Add(o + new Vector3(c - 2 * pad, 0, 0));
                    colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
            _mesh.Clear();
            _mesh.SetVertices(verts); _mesh.SetColors(colors); _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
        }
    }
}
