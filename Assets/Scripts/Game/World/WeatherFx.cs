using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>Rain and snow you can see: one particle system that follows the camera and changes with the weather.</summary>
    public sealed class WeatherFx : MonoBehaviour
    {
        ParticleSystem _ps;
        ParticleSystemRenderer _r;
        Weather _shown = (Weather)(-1);
        Material _rain;

        static Material _mist;
        /// <summary>Soft round puffs (de-icing spray, dust): the weather material with the painted glow as its shape.</summary>
        public static Material Mist
        {
            get
            {
                if (_mist != null) return _mist;
                var basis = Resources.Load<Material>("Materials/Weather");
                _mist = basis != null ? new Material(basis) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                _mist.SetFloat("_Surface", 1); _mist.SetColor("_BaseColor", Color.white);
                var tex = Resources.Load<Texture2D>("UI/Fx/glow"); if (tex != null) _mist.SetTexture("_BaseMap", tex);
                _mist.renderQueue = 3000; _mist.SetInt("_SrcBlend", 5); _mist.SetInt("_DstBlend", 10); _mist.SetInt("_ZWrite", 0);
                _mist.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                return _mist;
            }
        }

        public static WeatherFx Create(Transform parent)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeatherFx>();
            w._ps = go.AddComponent<ParticleSystem>();
            w._r = go.GetComponent<ParticleSystemRenderer>();
            var basis = Resources.Load<Material>("Materials/Weather");
            var mat = basis != null ? new Material(basis) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1); mat.SetColor("_BaseColor", new Color(1, 1, 1, 0.55f));
            mat.renderQueue = 3000; mat.SetInt("_SrcBlend", 5); mat.SetInt("_DstBlend", 10); mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            w._r.sharedMaterial = mat; w._rain = mat;
            var main = w._ps.main; main.loop = true; main.maxParticles = 2500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = w._ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(260, 1, 260);
            var em = w._ps.emission; em.rateOverTime = 0;
            return w;
        }

        void LateUpdate()
        {
            var cam = CameraRig.Instance;
            if (cam == null) return;
            transform.position = cam.Focus + Vector3.up * 70;
            var w = CameraRig.Inside ? Weather.Clear : GameRoot.Sim.Weather;
            if (w == _shown) return;
            _shown = w;
            var main = _ps.main; var em = _ps.emission;
            bool snow = w == Weather.Snow, rain = w == Weather.Rain || w == Weather.Storm;
            em.rateOverTime = snow ? 500 : w == Weather.Storm ? 1400 : rain ? 700 : 0;
            main.startSpeed = snow ? 9 : 75;
            main.startLifetime = snow ? 8 : 1.0f;
            main.startSize = snow ? new ParticleSystem.MinMaxCurve(0.5f, 1.1f) : 0.22f;
            _r.sharedMaterial = snow ? Mist : _rain;      // flakes are soft and round; rain is drawn as streaks
            main.startColor = snow ? new Color(1, 1, 1, 0.9f) : new Color(0.8f, 0.88f, 1f, 0.5f);
            _r.renderMode = snow ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            _r.lengthScale = 14; _r.velocityScale = 0;
            transform.rotation = Quaternion.Euler(snow ? 90 : 78, 20, 0);     // emits along +Z: point it down (rain slants)
            if (em.rateOverTime.constant > 0) _ps.Play(); else _ps.Stop();
        }
    }
}
