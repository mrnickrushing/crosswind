using Crosswind.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crosswind.Game
{
    /// <summary>Boots the game in an empty scene: the simulation from the save, the light, the camera, the airport and the HUD.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public static Sim Sim { get; private set; }
        /// <summary>What the airport earned while the app was closed, waiting for the welcome-back card (0: nothing).</summary>
        public double AwaySeconds { get; private set; }
        /// <summary>Game seconds per real second (the Editor play bot runs faster).</summary>
        public static float Speed = 1f;

        float _saveIn = 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Crosswind").AddComponent<GameRoot>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            var state = SaveSystem.Load();
            bool fresh = state == null;
            if (fresh) state = GameState.New();
            Sim = new Sim(state);
            if (!fresh && state.savedAt > 0) AwaySeconds = System.Math.Max(0, SaveSystem.Now - state.savedAt);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.35f;
            sun.shadowBias = 0.6f; sun.shadowNormalBias = 0.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -128, 0);   // low from behind the camera's left: long shadows toward the viewer's right
            // The sky: an HDRI for the background, the ambient light and the reflections in glass and paint.
            var skyTex = Resources.Load<Cubemap>("Sky/sky");
            if (skyTex != null)
            {
                var sky = new Material(Shader.Find("Skybox/Cubemap"));
                sky.SetTexture("_Tex", skyTex); sky.SetFloat("_Exposure", 1.05f); sky.SetFloat("_Rotation", 120);
                RenderSettings.skybox = sky;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = skyTex;
                RenderSettings.reflectionIntensity = 0.9f;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.83f, 0.92f);
            RenderSettings.fogStartDistance = 420; RenderSettings.fogEndDistance = 1100;

            Shader.SetGlobalFloat("_NightGlow", 0);
            Sfx.Init(transform);
            var rig = CameraRig.Create();
            Look.Create(rig.Cam, sun);
            AirportView.Create(transform);
            Hud.Create();
        }

        void Update()
        {
            Sim.Tick(Time.deltaTime * Speed);
            if ((_saveIn -= Time.unscaledDeltaTime) <= 0) { _saveIn = 10f; Save(); }
        }

        public void ClearAway() { AwaySeconds = 0; }

        public void Save() { SaveSystem.Save(Sim.State); }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
            else if (Sim != null && Sim.State.savedAt > 0) { AwaySeconds = System.Math.Max(0, SaveSystem.Now - Sim.State.savedAt); if (Hud.Instance != null) Hud.Instance.OfferAway(); }
        }

        void OnApplicationQuit() { Save(); }

        /// <summary>Starts over (Editor and Settings): deletes the save and rebuilds everything.</summary>
        public void Restart()
        {
            SaveSystem.Delete();
            Sim = new Sim(GameState.New());
            AwaySeconds = 0;
            if (AirportView.Instance != null) Destroy(AirportView.Instance.gameObject);
            if (Hud.Instance != null) Destroy(Hud.Instance.gameObject);
            AirportView.Create(transform);
            Hud.Create();
        }
    }
}
