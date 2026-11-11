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

        /// <summary>A portrait canvas (1080 x 1920 reference, expanding on taller or wider screens) and the event system.</summary>
        public static RectTransform Canvas(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1080, 1920);
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
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.fadeDuration = 0.05f; b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (label != null) Label("Label", img.transform, label, size, Color.white).rectTransform.Fill(6);
            return b;
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

        public void OnPointerDown(PointerEventData e) { _touches[e.pointerId] = e.position; }
        public void OnPointerUp(PointerEventData e) { _touches.Remove(e.pointerId); }

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
}
