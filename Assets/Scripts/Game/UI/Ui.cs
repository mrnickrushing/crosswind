using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>Small helpers for building the interface from code: a canvas, rounded panels, labels and buttons.</summary>
    public static class Ui
    {
        static Font _bold, _body;
        static Sprite _round, _circle;

        public static Font Bold { get { return _bold ?? (_bold = Resources.Load<Font>("Fonts/BarlowCondensed-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")); } }
        public static Font Body { get { return _body ?? (_body = Resources.Load<Font>("Fonts/BarlowSemiCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")); } }

        public static readonly Color Ink = new Color(0.09f, 0.12f, 0.16f, 1f), Panel = new Color(0.12f, 0.16f, 0.21f, 0.96f), Card = new Color(0.17f, 0.22f, 0.28f, 1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.24f), Green = new Color(0.25f, 0.72f, 0.42f), Blue = new Color(0.2f, 0.52f, 0.86f), Red = new Color(0.9f, 0.36f, 0.3f);
        public static readonly Color Dim = new Color(1, 1, 1, 0.6f), Off = new Color(0.32f, 0.36f, 0.42f);

        /// <summary>A landscape canvas (1920 x 1080 reference, expanding on taller or wider screens) and the event system.</summary>
        public static RectTransform Canvas(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Wider than 16:9 (every notched iPhone) the canvas is 940 tall, not 1080: everything is drawn 15% larger, so
            // the smallest labels clear 11 points. 16:9 and iPads are unchanged (they are limited by width).
            s.referenceResolution = new Vector2(1920, 940);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors (and pivots) at one point, then a position and a size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretches to fill the parent, inset by a margin.</summary>
        public static RectTransform Fill(this RectTransform rt, float margin = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(margin, margin); rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        /// <summary>Stretches across the parent's width at the top or the bottom, with a height.</summary>
        public static RectTransform Band(this RectTransform rt, bool top, float y, float height, float margin = 0)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0); rt.anchorMax = new Vector2(1, top ? 1 : 0); rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.offsetMin = new Vector2(margin, 0); rt.offsetMax = new Vector2(-margin, 0);
            rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(-margin * 2, height);
            return rt;
        }

        public static Image Img(string name, Transform parent, Color color, bool rounded = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = Round; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = true)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Bold : Body; t.fontSize = size; t.color = color; t.alignment = anchor; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = true;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Color color, Action onClick, int size = 40)
        {
            var img = Img(name, parent, color, true);
            var glossy = color.a > 0.5f ? Art("button", 44) : null;      // see-through buttons stay plain hit areas
            if (glossy != null) { img.sprite = glossy; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.6f; }
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.fadeDuration = 0.05f; b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("click", 0.5f); onClick(); });
            if (label != null)
            {
                var l = Label("Label", img.transform, label, size, Color.white);
                l.rectTransform.Fill(6);
                if (glossy != null) { l.rectTransform.offsetMin = new Vector2(8, 10); var sh = l.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.45f); sh.effectDistance = new Vector2(0, -3); }
            }
            return b;
        }

        static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();
        /// <summary>A white icon from Resources/UI (tinted by the Image's colour); null when missing.</summary>
        public static Sprite Icon(string name)
        {
            Sprite s;
            if (!_icons.TryGetValue(name, out s)) _icons[name] = s = Resources.Load<Sprite>("UI/" + name);
            return s;
        }

        public static Image IconImg(string name, Transform parent, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            var img = Img("Icon", parent, color);
            img.sprite = Icon(name); img.preserveAspect = true;
            img.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return img;
        }

        static readonly Dictionary<string, Sprite> _art = new Dictionary<string, Sprite>();
        /// <summary>A painted piece from Resources/UI/Fx (Tools/art/ui_art.py), nine-sliced when a border is given; null when missing.</summary>
        static readonly Dictionary<string, Sprite> _ic = new Dictionary<string, Sprite>();
        /// <summary>A white menu glyph from Resources/UI/Ic (null when there is none by that name).</summary>
        public static Sprite Ic(string name)
        {
            Sprite s;
            if (_ic.TryGetValue(name, out s)) return s;
            var tex = Resources.Load<Texture2D>("UI/Ic/" + name);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100) : null;
            _ic[name] = s;
            return s;
        }

        public static Sprite Art(string name, int border = 0)
        {
            Sprite s; string key = name + border;
            if (_art.TryGetValue(key, out s)) return s;
            var tex = Resources.Load<Texture2D>("UI/Fx/" + name);
            s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            return _art[key] = s;
        }

        /// <summary>An image of a painted piece, sliced so its corners keep their shape at any size.</summary>
        public static Image ArtImg(string name, Transform parent, string art, Color color, int border = 44, float corner = 1f)
        {
            var img = Img(name, parent, color);
            var sp = Art(art, border);
            if (sp != null) { img.sprite = sp; if (border > 0) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = corner; } else img.preserveAspect = true; }
            else if (border > 0) { img.sprite = Round; img.type = Image.Type.Sliced; }
            return img;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

        /// <summary>A white rounded rectangle, nine-sliced.</summary>
        public static Sprite Round
        {
            get
            {
                if (_round != null) return _round;
                const int n = 64, r = 22;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r))), dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                return _round = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                return _circle = Sprite.Create(tex, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            }
        }
    }

    /// <summary>The empty part of the screen: drag pans the camera, two fingers (or the wheel) zoom.</summary>
    public sealed class DragPad : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        readonly Dictionary<int, Vector2> _touches = new Dictionary<int, Vector2>();

        Vector2 _downAt; float _downTime;
        public void OnPointerDown(PointerEventData e) { _touches[e.pointerId] = e.position; _downAt = e.position; _downTime = Time.unscaledTime; }
        public void OnPointerUp(PointerEventData e)
        {
            _touches.Remove(e.pointerId);
            // A touch that barely moved and didn't linger is a tap on the world.
            if (_touches.Count == 0 && (e.position - _downAt).sqrMagnitude < 20 * 20 && Time.unscaledTime - _downTime < 0.4f && Hud.Instance != null) Hud.Instance.TapWorld(e.position);
        }

        public void OnDrag(PointerEventData e)
        {
            var rig = CameraRig.Instance;
            if (rig == null) return;
            if (_touches.Count >= 2 && _touches.ContainsKey(e.pointerId))
            {
                // Pinch: the change in distance between this finger and the other one.
                Vector2 other = Vector2.zero;
                foreach (var kv in _touches) if (kv.Key != e.pointerId) { other = kv.Value; break; }
                float before = Vector2.Distance(_touches[e.pointerId], other), after = Vector2.Distance(e.position, other);
                if (before > 1) rig.Zoom(after / before);
            }
            else rig.Pan(e.delta);
            _touches[e.pointerId] = e.position;
        }

        public void OnScroll(PointerEventData e) { if (CameraRig.Instance != null) CameraRig.Instance.Zoom(1 + Mathf.Clamp(e.scrollDelta.y, -3, 3) * 0.08f); }
    }

    /// <summary>Keeps the controls inside the screen's safe area (clear of the notch and the home bar).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;
        /// <summary>Editor only: pretend the screen has these insets (left/right, bottom, as shares of the screen) to check a notched phone.</summary>
        public static Vector2 DebugInset;
        void Update()
        {
            var sa = Screen.safeArea;
            if (Application.isEditor && DebugInset != Vector2.zero) sa = new Rect(DebugInset.x * Screen.width, DebugInset.y * Screen.height, Screen.width * (1 - DebugInset.x * 2), Screen.height * (1 - DebugInset.y));
            if (sa == _last || Screen.width <= 0 || Screen.height <= 0) return;
            _last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
