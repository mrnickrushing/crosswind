using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// A photo studio for the interface: a model built in code is photographed once under its own two lights, on a
    /// clear background, and kept as a sprite. The terminal catalogue's pictures and the menu icons are made here, so
    /// what a button shows is the thing itself.
    /// </summary>
    public static class Studio
    {
        const int Layer = 31;
        static readonly Dictionary<string, Sprite> _made = new Dictionary<string, Sprite>();
        static Camera _cam; static Light _key, _fill;

        /// <summary>The picture called <paramref name="key"/>, taken the first time it is asked for.</summary>
        public static Sprite Shot(string key, System.Func<MeshBuilder> build, float yaw = 62, float pitch = 24, int size = 256)
        {
            Sprite made;
            if (_made.TryGetValue(key, out made) && made != null) return made;
            var mb = build();
            var go = mb.ToObject("Studio " + key, null, false);
            go.layer = Layer;
            var at = new Vector3(0, -900, 0);
            go.transform.position = at;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var b = mesh.bounds;
            if (_cam == null)
            {
                var c = new GameObject("Studio camera");
                Object.DontDestroyOnLoad(c);
                _cam = c.AddComponent<Camera>();
                _cam.enabled = false;
                _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0, 0, 0, 0);
                _cam.cullingMask = 1 << Layer; _cam.orthographic = true; _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 400;
                _key = new GameObject("Studio key").AddComponent<Light>(); Object.DontDestroyOnLoad(_key.gameObject);
                _key.type = LightType.Directional; _key.cullingMask = 1 << Layer; _key.intensity = 1.5f; _key.color = new Color(1f, 0.97f, 0.92f); _key.shadows = LightShadows.None;
                _fill = new GameObject("Studio fill").AddComponent<Light>(); Object.DontDestroyOnLoad(_fill.gameObject);
                _fill.type = LightType.Directional; _fill.cullingMask = 1 << Layer; _fill.intensity = 0.6f; _fill.color = new Color(0.75f, 0.85f, 1f); _fill.shadows = LightShadows.None;
            }
            var rot = Quaternion.Euler(pitch, yaw, 0);
            _cam.transform.rotation = rot;
            _cam.transform.position = at + b.center - rot * Vector3.forward * 200;
            // Fit the box: its corners, as the camera sees them.
            float half = 0.01f;
            var inv = Quaternion.Inverse(rot);
            for (int i = 0; i < 8; i++)
            {
                var seen = inv * Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                half = Mathf.Max(half, Mathf.Abs(seen.x), Mathf.Abs(seen.y));
            }
            _cam.orthographicSize = half * 1.06f;
            // The key from over the camera's left shoulder, the fill from the other side.
            _key.transform.rotation = Quaternion.Euler(40, yaw - 34, 0);
            _fill.transform.rotation = Quaternion.Euler(18, yaw + 80, 0);
            _key.enabled = _fill.enabled = true;
            // Only the studio's lights fall on it: the scene's own (the sun at whatever hour, the lamps) are kept off this layer.
            var others = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l != _key && l != _fill && (l.cullingMask & (1 << Layer)) != 0).ToList();
            foreach (var l in others) l.cullingMask &= ~(1 << Layer);
            var ambientMode = RenderSettings.ambientMode; var ambient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            _cam.targetTexture = rt;
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(true);
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            foreach (var l in others) l.cullingMask |= 1 << Layer;
            _key.enabled = _fill.enabled = false;
            // Out of the studio at once: the next picture may be taken in this same frame.
            go.SetActive(false);
            Object.Destroy(mesh);
            Object.Destroy(go);
            // Trimmed to a square round what was drawn, so every picture fills its tile whatever its shape.
            var px = tex.GetPixels32();
            int x0 = size, x1 = -1, y0 = size, y1 = -1;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (px[y * size + x].a > 10) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            var rect = new Rect(0, 0, size, size);
            if (x1 >= x0 && y1 >= y0)
            {
                int side = Mathf.Min(size, Mathf.Max(x1 - x0, y1 - y0) + 1 + 6);
                int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                int rx = Mathf.Clamp(cx - side / 2, 0, size - side), ry = Mathf.Clamp(cy - side / 2, 0, size - side);
                rect = new Rect(rx, ry, side, side);
            }
            return _made[key] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100);
        }
    }
}
