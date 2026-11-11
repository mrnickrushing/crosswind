using System;
using System.IO;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>The save file: JSON in the app's own folder, written through a temp file so a crash can't leave half a save.</summary>
    public static class SaveSystem
    {
        static string Path { get { return System.IO.Path.Combine(Application.persistentDataPath, "crosswind_save.json"); } }

        public static double Now { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0; } }

        public static GameState Load()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var s = JsonUtility.FromJson<GameState>(File.ReadAllText(Path));
                if (s != null) s.Repair();
                return s;
            }
            catch (Exception e) { Debug.LogWarning("[Crosswind] save unreadable: " + e.Message); return null; }
        }

        public static void Save(GameState s)
        {
            try
            {
                s.savedAt = Now;
                string tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (Exception e) { Debug.LogWarning("[Crosswind] save failed: " + e.Message); }
        }

        public static void Delete() { try { if (File.Exists(Path)) File.Delete(Path); } catch (Exception) { } }
    }
}
