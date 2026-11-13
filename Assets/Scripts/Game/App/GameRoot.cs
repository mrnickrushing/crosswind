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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;   // KeepAwake lets it sleep again after a while untouched
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(cam.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            AppleAccount.Start();
            GameCenter.Start();
            Reminders.Clear();
            var state = SaveSystem.Load();
            // The save in the player's iCloud: a new phone continues from it; a newer one from another device is offered.
            var cloud = CloudSave.Fetch();
            if (cloud != null)
            {
                var meta = CloudSave.Meta();
                var step = CloudRules.Decide(state != null, meta, CloudSave.Me, CloudSave.Seen);
                if (step == CloudStep.Adopt) { cloud.Repair(); state = cloud; CloudSave.Seen = cloud.savedAt; }
                else if (step == CloudStep.Offer) { PendingCloud = cloud; PendingMeta = meta; }
            }
            CloudSave.NewerElsewhere += meta => { if (PendingCloud != null) return; var c = CloudSave.Fetch(); if (c == null) return; PendingCloud = c; PendingMeta = meta; if (Hud.Instance != null) Hud.Instance.OfferCloud(); };
            bool fresh = state == null;
            if (fresh) state = GameState.New();
            Sim = new Sim(state);
            if (!fresh && state.savedAt > 0) AwaySeconds = System.Math.Max(0, SaveSystem.Now - state.savedAt);
            Store.Start();

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
            gameObject.AddComponent<Telemetry>();   // anonymous play statistics (off in the Editor)
            gameObject.AddComponent<FrameBudget>(); // on a device: the 3D view at no more than about 2000 px across, lower while frames run long
            Sfx.Init(transform);
            var rig = CameraRig.Create();
            Look.Create(rig.Cam, sun);
            StartCoroutine(Launch());
        }

        bool _ready;

        /// <summary>The launch, a step a frame behind the loading screen: the airport, then the interface.</summary>
        System.Collections.IEnumerator Launch()
        {
            var loading = LoadingScreen.Show();
            yield return null; yield return null;          // the screen is up before the heavy work starts
            loading.Progress = 0.25f;
            AirportView.Create(transform);
            loading.Progress = 0.7f;
            yield return null;
            Hud.Create();
            loading.Progress = 0.9f;
            yield return null;
            _ready = true;
            loading.Done = true;
        }

        void Update()
        {
            AppleAccount.Update();
            if (!_ready) return;
            Sim.Tick(Time.deltaTime * Speed);
            GameCenter.Sync(Sim);
            Sfx.TickMusic(Sim.Night, Time.unscaledDeltaTime);
            Sfx.TickAmbience(CameraRig.Inside, Interior.Instance != null ? Interior.Instance.Crowd : 0, Time.unscaledDeltaTime);
            if ((_saveIn -= Time.unscaledDeltaTime) <= 0) { _saveIn = 10f; Save(); }
            KeepAwake();
        }

        float _lastTouch;
        /// <summary>The screen stays on while the airport is being played and for five minutes after the last touch,
        /// then the device's own auto-lock applies again (an idle airport earns offline anyway).</summary>
        void KeepAwake()
        {
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p != null && p.press.isPressed) _lastTouch = Time.unscaledTime;
            int want = Time.unscaledTime - _lastTouch < 300f ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (Screen.sleepTimeout != want) Screen.sleepTimeout = want;
        }

        public void ClearAway() { AwaySeconds = 0; }

        public void Save(bool now = false) { SaveSystem.Save(Sim.State); if (PendingCloud == null) CloudSave.Push(Sim.State, Sim.Stars, now); }

        /// <summary>A newer save from another device, waiting for the player's choice (null: none).</summary>
        public GameState PendingCloud { get; private set; }
        public CloudMeta PendingMeta { get; private set; }

        /// <summary>The player's answer to "continue from your other device?".</summary>
        public void ResolveCloud(bool useCloud)
        {
            var cloud = PendingCloud; var meta = PendingMeta;
            PendingCloud = null; PendingMeta = null;
            if (cloud == null) return;
            CloudSave.Seen = meta != null ? meta.SavedAt : cloud.savedAt;
            if (useCloud) { cloud.Repair(); Rebuild(cloud); SaveSystem.Save(Sim.State); }
            else Save(true);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { Save(true); Reminders.Schedule(); }
            else Reminders.Clear();
            if (!paused)
                if (Sim != null && Sim.State.savedAt > 0) { AwaySeconds = System.Math.Max(0, SaveSystem.Now - Sim.State.savedAt); if (Hud.Instance != null) Hud.Instance.OfferAway(); }
        }

        void OnApplicationQuit() { Save(true); }

        /// <summary>Starts over (Editor and Settings): deletes the save and rebuilds everything.</summary>
        public void Restart()
        {
            SaveSystem.Delete();
            CloudSave.Erase();
            Rebuild(GameState.New());
        }

        void Rebuild(GameState state)
        {
            Sim = new Sim(state);
            AwaySeconds = 0;
            if (AirportView.Instance != null) Destroy(AirportView.Instance.gameObject);
            if (Hud.Instance != null) Destroy(Hud.Instance.gameObject);
            AirportView.Create(transform);
            Hud.Create();
        }
    }
}
