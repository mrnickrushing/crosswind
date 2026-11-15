using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Crosswind.Game
{
    /// <summary>
    /// Anonymous play statistics, sent to the game's own Cloudflare Worker (`Tools/stats-worker`, stored in D1), to see
    /// where players stop and what they use: sittings, the tutorial's steps, each guide goal reached, stars gained,
    /// airports sold, the first manager of each department, contracts finished or given up, layout edits, features
    /// tried (once a sitting each), offers looked at, rewarded adverts finished, and a frame-rate sample every five
    /// minutes. Never purchases: those stay with the App Store and RevenueCat, so these events can't be tied to them. The player is a random id made on this device, hashed before it leaves it; nothing about
    /// the person, no advertising id, no IP kept, no tracking. Off in the Editor and when the player turns it off in
    /// Settings. Events wait in a small queue (kept across launches) and go out in batches.
    /// </summary>
    public sealed class Telemetry : MonoBehaviour
    {
        /// <summary>Where the events go (the Worker in Tools/stats-worker). Empty: nothing is sent.</summary>
        public const string Endpoint = "https://crosswind-stats.mrnickrushing.workers.dev/v1/events";
        const string Salt = "crosswind-2b9e";     // fixed: the same player keeps the same hash across launches
        const string IdKey = "cw_stats_id", OffKey = "cw_stats_off", QueueKey = "cw_stats_queue";
        const int QueueMax = 200, BatchMax = 50;
        const float SendEvery = 60f, SampleEvery = 300f;

        public static Telemetry Instance { get; private set; }

        /// <summary>The player's choice in Settings (on unless turned off).</summary>
        public static bool Sharing
        {
            get { return PlayerPrefs.GetInt(OffKey, 0) == 0; }
            set
            {
                PlayerPrefs.SetInt(OffKey, value ? 0 : 1);
                if (!value && Instance != null) Instance._queue.Clear();   // nothing more leaves, not even what waited
                PlayerPrefs.DeleteKey(QueueKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Delete account: the waiting events go, and the next ones come from a new anonymous id.</summary>
        public static void Forget()
        {
            if (Instance == null) return;
            Instance._queue.Clear();
            PlayerPrefs.DeleteKey(QueueKey);
            PlayerPrefs.SetString(IdKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.Save();
            Instance._user = Hash(PlayerPrefs.GetString(IdKey) + Salt);
        }

        static bool Active { get { return !Application.isEditor && Endpoint.Length > 0 && Sharing; } }

        /// <summary>The last events made, sent or not (the Editor and tests read these).</summary>
        public static readonly List<string> Recent = new List<string>();

        readonly List<string> _queue = new List<string>();
        string _user, _sessionId;
        bool _firstLaunch, _started, _sending, _test;
        float _sendIn = 10f, _sampleIn = SampleEvery, _frameSum, _sittingStart;
        int _frames;
        Sim _sim;
        int _goal = -1, _stars = -1, _airports = -1, _tutStep = -1;
        bool _tutDone;
        readonly HashSet<string> _managers = new HashSet<string>();
        readonly HashSet<string> _featuresThisSitting = new HashSet<string>();
        readonly Dictionary<string, int> _edits = new Dictionary<string, int>();

        /// <summary>Queues an event if statistics are on (anywhere in the game).</summary>
        public static void Track(string type, params (string key, string value)[] extra) { if (Instance != null) Instance.Send(type, extra); }

        /// <summary>A feature was used: sent the first time in a sitting (how many players find it, not how often they tap).</summary>
        public static void Feature(string name, string detail = null)
        {
            if (Instance == null || !Instance._featuresThisSitting.Add(name)) return;
            Instance.Send("Feature.used", ("Feature.name", name), ("Feature.detail", detail ?? ""));
        }

        /// <summary>A change to the terminal's layout (placed, moved, sold, upgraded, undone): counted, sent as one event when the sitting ends.</summary>
        public static void LayoutEdit(string kind)
        {
            if (Instance == null) return;
            int n; Instance._edits.TryGetValue(kind, out n); Instance._edits[kind] = n + 1;
        }

        /// <summary>An offer in the shop was opened (its preview card).</summary>
        public static void OfferViewed(string product) { Track("Offer.viewed", ("Offer.product", product)); }

        void SendEdits()
        {
            if (_edits.Count == 0) return;
            var extra = new List<(string, string)>();
            int total = 0;
            foreach (var e in _edits) { extra.Add(("Layout." + e.Key, e.Value.ToString(CultureInfo.InvariantCulture))); total += e.Value; }
            Send("Layout.edits", total, extra.ToArray());
            _edits.Clear();
        }

        void Watch(Sim s)
        {
            s.ContractDone += r => Send(r.dropped ? "Contract.dropped" : "Contract.finished", ("Contract.kind", r.kind.ToString(CultureInfo.InvariantCulture)),
                ("Contract.marks", ((r.punctual ? 1 : 0) + (r.serviced ? 1 : 0) + (r.standards ? 1 : 0)).ToString(CultureInfo.InvariantCulture)),
                ("Contract.flights", r.flights.ToString(CultureInfo.InvariantCulture)));
            s.DayEnded += r => Send("Day.closed", r.score, ("Day.grade", r.grade ?? ""), ("Day.index", r.day.ToString(CultureInfo.InvariantCulture)));
        }

        void Awake()
        {
            Instance = this;
            _firstLaunch = !PlayerPrefs.HasKey(IdKey);
            if (_firstLaunch) { PlayerPrefs.SetString(IdKey, Guid.NewGuid().ToString("N")); PlayerPrefs.Save(); }
            _user = Hash(PlayerPrefs.GetString(IdKey) + Salt);
            _sessionId = Guid.NewGuid().ToString();
            _test = TestBuild;
            var saved = PlayerPrefs.GetString(QueueKey, "");
            if (saved.Length > 0 && Sharing) _queue.AddRange(saved.Split('\n'));
            PlayerPrefs.DeleteKey(QueueKey);
            _sittingStart = Time.realtimeSinceStartup;
        }

        void Update()
        {
            var s = GameRoot.Sim;
            if (s == null) return;
            if (s != _sim)
            {
                _sim = s; _goal = s.State.goalIndex; _stars = s.Stars; _airports = s.State.mapsRun.Count;
                _tutStep = s.State.tutorialStep; _tutDone = s.State.tutorialDone;
                _managers.Clear(); foreach (var m in s.State.managers) _managers.Add(m);
                Watch(s);
            }
            if (!_started) { _started = true; Send("Session.started", ("Session.firstLaunch", _firstLaunch ? "true" : "false")); }
            if (s.State.goalIndex != _goal) { if (s.State.goalIndex > _goal) Send("Goal.reached"); _goal = s.State.goalIndex; }
            if (s.Stars != _stars) { if (s.Stars > _stars) Send("Stars.gained"); _stars = s.Stars; }
            if (s.State.tutorialStep != _tutStep) { _tutStep = s.State.tutorialStep; if (!s.State.tutorialDone) Send("Tutorial.step", ("Tutorial.step", _tutStep.ToString(CultureInfo.InvariantCulture))); }
            if (s.State.tutorialDone != _tutDone) { _tutDone = s.State.tutorialDone; if (_tutDone) Send("Tutorial.finished", ("Tutorial.step", s.State.tutorialStep.ToString(CultureInfo.InvariantCulture))); }
            if (s.State.managers.Count != _managers.Count)
                foreach (var m in s.State.managers)
                    if (_managers.Add(m)) Send(_managers.Count == 1 ? "Automation.first" : "Automation.hired", ("Automation.department", m));
            if (s.State.mapsRun.Count != _airports) { _airports = s.State.mapsRun.Count; Send("Airport.sold", ("Game.wings", s.State.wings.ToString(CultureInfo.InvariantCulture))); }

            _frameSum += Time.unscaledDeltaTime; _frames++;
            if ((_sampleIn -= Time.unscaledDeltaTime) <= 0)
            {
                float fps = _frames / Mathf.Max(0.001f, _frameSum);
                Send("Performance.sample", fps, ("Performance.fps", Mathf.RoundToInt(fps).ToString(CultureInfo.InvariantCulture)), ("Performance.screen", Screen.width + "x" + Screen.height));
                _sampleIn = SampleEvery; _frameSum = 0; _frames = 0;
            }
            if ((_sendIn -= Time.unscaledDeltaTime) <= 0 || _queue.Count >= BatchMax) { _sendIn = SendEvery; Flush(); }
        }

        /// <summary>Queues one event with where the game stands (step, level, island, day, minutes played).</summary>
        public void Send(string type, params (string key, string value)[] extra) { Send(type, float.NaN, extra); }

        public void Send(string type, float value, params (string key, string value)[] extra)
        {
            var s = GameRoot.Sim;
            var p = new List<(string, string)>
            {
                ("App.version", Application.version),
                ("Device.platform", Application.platform == RuntimePlatform.IPhonePlayer ? (SystemInfo.deviceModel.StartsWith("iPad") ? "iPadOS" : "iOS") : Application.platform.ToString()),
                ("Device.model", SystemInfo.deviceModel),
                ("Device.os", SystemInfo.operatingSystem),
                ("Device.locale", CultureInfo.CurrentCulture.Name),
                ("App.debug", Debug.isDebugBuild ? "true" : "false"),
            };
            if (s != null)
            {
                p.Add(("Game.goal", s.CurrentGoal != null ? s.CurrentGoal.Id : "end"));
                p.Add(("Game.goalIndex", s.State.goalIndex.ToString(CultureInfo.InvariantCulture)));
                p.Add(("Game.stars", s.Stars.ToString(CultureInfo.InvariantCulture)));
                p.Add(("Game.map", s.State.map));
                p.Add(("Game.airports", s.State.mapsRun.Count.ToString(CultureInfo.InvariantCulture)));
                p.Add(("Game.minutesPlayed", Mathf.FloorToInt((float)(s.State.time / 60)).ToString(CultureInfo.InvariantCulture)));
                p.Add(("Game.managers", s.State.managers.Count.ToString(CultureInfo.InvariantCulture)));
            }
            p.Add(("Session.minutes", Mathf.FloorToInt((Time.realtimeSinceStartup - _sittingStart) / 60f).ToString(CultureInfo.InvariantCulture)));
            foreach (var e in extra) p.Add(e);

            var j = new StringBuilder(512);
            j.Append("{\"clientUser\":").Append(Str(_user))
             .Append(",\"sessionID\":").Append(Str(_sessionId)).Append(",\"type\":").Append(Str(type))
             .Append(",\"isTestMode\":").Append(_test ? "true" : "false")
             .Append(",\"receivedAt\":").Append(Str(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
            if (!float.IsNaN(value)) j.Append(",\"floatValue\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            j.Append(",\"payload\":{");
            for (int i = 0; i < p.Count; i++) j.Append(i > 0 ? "," : "").Append(Str(p[i].Item1)).Append(':').Append(Str(p[i].Item2 ?? ""));
            j.Append("}}");
            var json = j.ToString();

            Recent.Add(json);
            if (Recent.Count > 30) Recent.RemoveAt(0);
            if (!Active) return;
            _queue.Add(json);
            if (_queue.Count > QueueMax) _queue.RemoveRange(0, _queue.Count - QueueMax);   // the oldest go first
        }

        void Flush()
        {
            if (!Active || _sending || _queue.Count == 0 || Application.internetReachability == NetworkReachability.NotReachable) return;
            StartCoroutine(Post(Mathf.Min(BatchMax, _queue.Count)));
        }

        IEnumerator Post(int n)
        {
            _sending = true;
            var body = "[" + string.Join(",", _queue.GetRange(0, n)) + "]";
            using (var req = new UnityWebRequest(Endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                req.timeout = 15;
                yield return req.SendWebRequest();
                long code = req.responseCode;
                // Stored, or refused for good (bad input would fail again): either way they leave the queue. A full
                // server queue (503) or no connection keeps them for the next try.
                if (code == 200 || code == 400 || code == 413 || code == 422) _queue.RemoveRange(0, Mathf.Min(n, _queue.Count));
                if (code == 503) _sendIn = 10f;
            }
            _sending = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SendEdits();
                Send("Session.paused");
                Keep();
                Flush();
            }
            else
            {
                _sessionId = Guid.NewGuid().ToString();   // back after a while: a new sitting
                _sittingStart = Time.realtimeSinceStartup;
                _featuresThisSitting.Clear();
                Send("Session.started", ("Session.firstLaunch", "false"));
            }
        }

        void OnApplicationQuit() { SendEdits(); Keep(); }

        /// <summary>What hasn't gone out yet waits for the next launch.</summary>
        void Keep()
        {
            if (!Active) return;
            if (_queue.Count > 0) PlayerPrefs.SetString(QueueKey, string.Join("\n", _queue)); else PlayerPrefs.DeleteKey(QueueKey);
            PlayerPrefs.Save();
        }

        static string Str(string s)
        {
            var b = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }

        static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var b = new StringBuilder(64);
                foreach (var x in h) b.Append(x.ToString("x2"));
                return b.ToString();
            }
        }

/// <summary>A debug or TestFlight build: its statistics are marked test, and it shows test adverts only.</summary>
        public static bool TestBuild { get { return Debug.isDebugBuild || IsTestFlight(); } }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool CWIsTestFlight();
        static bool IsTestFlight() { try { return CWIsTestFlight(); } catch (Exception) { return false; } }
#else
        static bool IsTestFlight() { return false; }
#endif
    }
}
