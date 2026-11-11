using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>The view over the airport: a three-quarter angle, dragged to pan and pinched (or scrolled) to zoom.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }

        /// <summary>The point on the ground the camera looks at, and how far back it stands.</summary>
        public Vector3 Focus = new Vector3(6, 0, -40);
        public float Distance = 230f;
        const float Pitch = 40f, Yaw = 128f, MinDistance = 90f, MaxDistance = 700f;
        Vector3 _focusV;
        float _shown;

        /// <summary>Looking inside the terminal (the camera sits over <see cref="Interior.Origin"/>) or over the airfield.</summary>
        public static bool Inside { get; private set; }
        Vector3 _outFocus; float _outDistance;

        public void SetInside(bool inside)
        {
            if (inside == Inside) return;
            Inside = inside;
            if (inside) { _outFocus = Focus; _outDistance = Distance; Focus = Interior.Origin + new Vector3(0, 0, -6); Distance = 235; }
            else { Focus = _outFocus; Distance = _outDistance; }
            _shown = Distance;
        }

        public static CameraRig Create()
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.fieldOfView = 22;
            rig.Cam.nearClipPlane = 1f; rig.Cam.farClipPlane = 2200f;
            rig.Cam.clearFlags = CameraClearFlags.Skybox;
            rig.Cam.backgroundColor = new Color(0.72f, 0.82f, 0.9f);
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            rig._shown = rig.Distance;
            rig.Place();
            Instance = rig;
            return rig;
        }

        /// <summary>Drag by screen pixels: the ground moves with the finger.</summary>
        public void Pan(Vector2 pixels)
        {
            float perPixel = 2f * Distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            var fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            Focus -= right * pixels.x * perPixel + fwd * pixels.y * perPixel / Mathf.Sin(Pitch * Mathf.Deg2Rad);
            if (Inside)
            {
                float half = Interior.Instance != null ? Interior.Instance.Length * 0.5f : 40;
                Focus.x = Mathf.Clamp(Focus.x, Interior.Origin.x - 34, Interior.Origin.x + 34);
                Focus.z = Mathf.Clamp(Focus.z, -half, half);
                return;
            }
            Focus.x = Mathf.Clamp(Focus.x, -90, 110);
            Focus.z = Mathf.Clamp(Focus.z, -140, 140);
        }

        /// <summary>Zoom by a factor (over 1 = closer).</summary>
        public void Zoom(float factor) { Distance = Mathf.Clamp(Distance / Mathf.Max(0.01f, factor), Inside ? 45f : MinDistance, Inside ? 260f : MaxDistance); }

        void LateUpdate()
        {
            _shown = Mathf.Lerp(_shown, Distance, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
            Place();
        }

        void Place()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.rotation = rot;
            transform.position = Focus - rot * Vector3.forward * _shown;
        }
    }
}
