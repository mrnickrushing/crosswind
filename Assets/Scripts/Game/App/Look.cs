using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crosswind.Game
{
    /// <summary>
    /// The picture: a filmic tone curve, a touch of bloom on glass and lights, a little more contrast and colour, a
    /// faint vignette, and shadows that reach across the whole field. The weather tints and dims it.
    /// </summary>
    public sealed class Look : MonoBehaviour
    {
        public static Look Instance { get; private set; }
        ColorAdjustments _grade;
        Light _sun;
        float _gloom;

        public static Look Create(Camera cam, Light sun)
        {
            var go = new GameObject("Look");
            var look = go.AddComponent<Look>();
            Instance = look;
            look._sun = sun;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) { urp.shadowDistance = 520; urp.shadowCascadeCount = 2; }

            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = p;
            var tone = p.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.6f);
            look._grade = p.Add<ColorAdjustments>();
            look._grade.postExposure.Override(0.35f); look._grade.contrast.Override(14f); look._grade.saturation.Override(16f);
            var vig = p.Add<Vignette>(); vig.intensity.Override(0.2f); vig.smoothness.Override(0.5f);
            return look;
        }

        void Update()
        {
            var sim = GameRoot.Sim;
            if (sim == null) return;
            var w = sim.Weather;
            float want = w == Core.Weather.Clear ? 0 : w == Core.Weather.Rain ? 0.45f : w == Core.Weather.Fog ? 0.6f : w == Core.Weather.Storm ? 0.9f : 0.5f;
            _gloom = Mathf.MoveTowards(_gloom, want, Time.unscaledDeltaTime * 0.4f);
            _grade.postExposure.Override(Mathf.Lerp(0.35f, -0.25f, _gloom));
            _grade.saturation.Override(Mathf.Lerp(16f, -30f, _gloom));
            _grade.colorFilter.Override(Color.Lerp(Color.white, w == Core.Weather.Snow ? new Color(0.9f, 0.95f, 1f) : new Color(0.78f, 0.84f, 0.95f), _gloom));
            if (_sun != null) { _sun.intensity = Mathf.Lerp(1.35f, 0.5f, _gloom); _sun.shadowStrength = Mathf.Lerp(0.85f, 0.3f, _gloom); }
            float fog = w == Core.Weather.Fog ? 1 : w == Core.Weather.Snow ? 0.6f : w == Core.Weather.Storm ? 0.5f : w == Core.Weather.Rain ? 0.3f : 0;
            RenderSettings.fogStartDistance = Mathf.Lerp(RenderSettings.fogStartDistance, Mathf.Lerp(420, 120, fog), Time.unscaledDeltaTime);
            RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, Mathf.Lerp(1100, 420, fog), Time.unscaledDeltaTime);
        }
    }
}
