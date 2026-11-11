using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The save in the player's own iCloud (key-value storage, no server of ours): written beside the local file
    /// at most once a minute and when the game goes to the background, read at launch. A new phone continues the
    /// airport at once; a newer save from another device is offered, never forced (<see cref="CloudRules"/>).
    /// Only on an iPhone signed in to iCloud; elsewhere every call does nothing.
    /// </summary>
    public static class CloudSave
    {
        const string SaveKey = "crosswind.save", MetaKey = "crosswind.meta", SeenPref = "cloud.seen", PushedPref = "cloud.pushed";
        const float Every = 60f;
        static float _next;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool CWCloudAvailable();
        [DllImport("__Internal")] static extern void CWCloudSet(string key, string value);
        [DllImport("__Internal")] static extern string CWCloudGet(string key);
        [DllImport("__Internal")] static extern void CWCloudRemove(string key);
        [DllImport("__Internal")] static extern void CWCloudSync();
        public static bool Available { get { try { return CWCloudAvailable(); } catch (Exception) { return false; } } }
#else
        static bool CWCloudAvailable() { return false; }
        static void CWCloudSet(string key, string value) { }
        static string CWCloudGet(string key) { return null; }
        static void CWCloudRemove(string key) { }
        static void CWCloudSync() { }
        public static bool Available { get { return false; } }
#endif

        /// <summary>This device, as the meta names it.</summary>
        public static string Me { get { return SystemInfo.deviceUniqueIdentifier; } }

        /// <summary>Saved-at of the newest iCloud save this device took, kept over or wrote itself.</summary>
        public static double Seen
        {
            get { double v; return double.TryParse(PlayerPrefs.GetString(SeenPref, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0; }
            set { PlayerPrefs.SetString(SeenPref, value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); PlayerPrefs.Save(); }
        }

        /// <summary>Real time of this device's last upload (0 = never), for Settings.</summary>
        public static double LastPushed
        {
            get { double v; return double.TryParse(PlayerPrefs.GetString(PushedPref, "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0; }
        }

        /// <summary>Raised when a newer save from another device turns up mid-game (the HUD asks what to do).</summary>
        public static event Action<CloudMeta> NewerElsewhere;

        public static CloudMeta Meta()
        {
            if (!Available) return null;
            return CloudMeta.Parse(CWCloudGet(MetaKey));
        }

        /// <summary>The save in iCloud, or null (none, unreadable, or no iCloud). Call at launch.</summary>
        public static GameState Fetch()
        {
            if (!Available) return null;
            try
            {
                CWCloudSync();
                var packed = CWCloudGet(SaveKey);
                if (string.IsNullOrEmpty(packed)) return null;
                var s = JsonUtility.FromJson<GameState>(Unpack(packed));
                return s != null && s.levels != null && s.levels.Count > 0 ? s : null;
            }
            catch (Exception e) { Debug.LogWarning("iCloud save unreadable: " + e.Message); return null; }
        }

        /// <summary>Writes the save to iCloud (at most once a minute unless <paramref name="now"/>), unless another
        /// device has saved since: then <see cref="NewerElsewhere"/> fires and nothing is written.</summary>
        public static void Push(GameState s, int stars, bool now = false)
        {
            if (!now && Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            if (!Available || s == null) return;
            try
            {
                var meta = Meta();
                if (CloudRules.Decide(true, meta, Me, Seen) == CloudStep.Offer) { NewerElsewhere?.Invoke(meta); return; }
                CWCloudSet(SaveKey, Pack(JsonUtility.ToJson(s)));
                CWCloudSet(MetaKey, new CloudMeta { SavedAt = s.savedAt, Stars = stars, Device = Me }.ToString());
                Seen = s.savedAt;
                PlayerPrefs.SetString(PushedPref, s.savedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("iCloud save failed: " + e.Message); }
        }

        /// <summary>Removes the save from iCloud (Delete account).</summary>
        public static void Erase()
        {
            if (Available) { CWCloudRemove(SaveKey); CWCloudRemove(MetaKey); }
            PlayerPrefs.DeleteKey(SeenPref);
            PlayerPrefs.DeleteKey(PushedPref);
        }

        static string Pack(string json)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress))
                {
                    var b = Encoding.UTF8.GetBytes(json);
                    gz.Write(b, 0, b.Length);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        static string Unpack(string packed)
        {
            using (var ms = new MemoryStream(Convert.FromBase64String(packed)))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
                return r.ReadToEnd();
        }
    }
}
