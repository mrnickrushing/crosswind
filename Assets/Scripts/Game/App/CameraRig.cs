using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>The view over the airport: a three-quarter angle, dragged to pan and pinched (or scrolled) to zoom.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }

        /// <summary>The point on the ground the camera looks at, and how far back it stands.</summary>
        public Vector3 Focus = new Vector3(6, 0, -30);
        public float Distance = 300f;
        const float Pitch = 40f, Yaw = -42f, MinDistance = 90f, MaxDistance = 700f;
        Vector3 _focusV;
        float _shown;

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
            Focus.x = Mathf.Clamp(Focus.x, -90, 110);
            Focus.z = Mathf.Clamp(Focus.z, -140, 140);
        }

        /// <summary>Zoom by a factor (over 1 = closer).</summary>
        public void Zoom(float factor) { Distance = Mathf.Clamp(Distance / Mathf.Max(0.01f, factor), MinDistance, MaxDistance); }

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
