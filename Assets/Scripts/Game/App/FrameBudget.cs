using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crosswind.Game
{
    /// <summary>
    /// Keeps a phone cool and the frame rate steady. On a device the 3D view renders at no more than about 2000 pixels
    /// across (an iPhone's own 2556 or an iPad's 2732 cost half again in fill for detail nobody sees at arm's length;
    /// the HUD stays sharp, it isn't scaled), and steps down further, 0.05 at a time, while frames run long, and back
    /// up once they've been comfortably quick for a while. Battery saver (Settings) runs at 30 fps.
    /// Nothing changes in the Editor: the URP asset there is the project's file. (The same script as Last Tide's.)
    /// </summary>
    public sealed class FrameBudget : MonoBehaviour
    {
        const string SaverKey = "cw_battery_saver";
        const float Floor = 0.6f, Step = 0.05f;

        public static FrameBudget Instance { get; private set; }
        public static bool BatterySaver
        {
            get { return PlayerPrefs.GetInt(SaverKey, 0) == 1; }
            set { PlayerPrefs.SetInt(SaverKey, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
        }

        /// <summary>The 3D view's resolution as a share of the screen's (1 in the Editor).</summary>
        public static float Scale { get; private set; } = 1f;

        float _ceiling = 1f, _sum, _slowFor, _quickFor;
        int _frames;

        public static void Apply()
        {
            Application.targetFrameRate = BatterySaver ? 30 : 60;
        }

        void Awake()
        {
            Instance = this;
            Apply();
            if (Application.isEditor) { enabled = false; return; }
            int wide = Mathf.Max(Screen.width, Screen.height);
            _ceiling = Mathf.Clamp(2000f / Mathf.Max(1, wide), Floor, 1f);
            SetScale(_ceiling);
        }

        void Update()
        {
            // Half-second windows of unscaled frame time against the frame the target rate allows.
            _sum += Time.unscaledDeltaTime; _frames++;
            if (_sum < 0.5f) return;
            float avg = _sum / _frames, budget = 1f / Mathf.Max(1, Application.targetFrameRate);
            _sum = 0; _frames = 0;
            if (avg > budget * 1.12f) { _slowFor += 0.5f; _quickFor = 0; }
            else if (avg < budget * 1.03f) { _quickFor += 0.5f; _slowFor = 0; }
            else { _slowFor = 0; _quickFor = 0; }
            // Two slow seconds lower it; twenty steady ones raise it (so it doesn't hunt up and down).
            if (_slowFor >= 2f && Scale > Floor + 0.001f) { SetScale(Scale - Step); _slowFor = 0; }
            else if (_quickFor >= 20f && Scale < _ceiling - 0.001f) { SetScale(Scale + Step); _quickFor = 0; }
        }

        static void SetScale(float s)
        {
            Scale = Mathf.Clamp(s, Floor, 1f);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.renderScale = Scale;
        }

        void OnApplicationPause(bool paused)
        {
            // Back from the background: a fresh start, so the pause isn't read as one very long frame.
            _sum = 0; _frames = 0; _slowFor = 0;
        }
    }
}
