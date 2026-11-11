using System.Collections.Generic;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// Sound: interface clips from Resources/Audio (takes named name, name_2, name_3 are picked at random), and the
    /// airfield itself made in code: a jet's roar for each landing and take-off and a quiet bed of wind and distant
    /// engines, so there is nothing to license and nothing to download.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        static AudioSource[] _pool;
        static AudioSource _bed;
        static AudioClip _jet, _prop;
        static int _next;

        public static bool Muted
        {
            get { return PlayerPrefs.GetInt("cw_muted", 0) == 1; }
            set { PlayerPrefs.SetInt("cw_muted", value ? 1 : 0); PlayerPrefs.Save(); AudioListener.volume = value ? 0 : 1; }
        }

        public static void Init(Transform root)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(root, false);
            _pool = new AudioSource[10];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = new GameObject("Voice").AddComponent<AudioSource>();
                _pool[i].transform.SetParent(go.transform, false);
                _pool[i].playOnAwake = false; _pool[i].spatialBlend = 0;
            }
            _jet = Roar("Jet", 3.2f, 900f, 0.0f);
            _prop = Roar("Prop", 2.6f, 420f, 95f);
            _bed = new GameObject("Bed").AddComponent<AudioSource>();
            _bed.transform.SetParent(go.transform, false);
            _bed.clip = Roar("Wind", 6f, 260f, 0f, loop: true); _bed.loop = true; _bed.volume = 0.05f; _bed.spatialBlend = 0;
            _bed.Play();
            AudioListener.volume = Muted ? 0 : 1;
        }

        /// <summary>Filtered noise swelling and fading (a pass-by), with an optional drone under it (propellers).</summary>
        static AudioClip Roar(string name, float seconds, float cutoff, float droneHz, bool loop = false)
        {
            const int rate = 22050;
            int n = (int)(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0, a = Mathf.Clamp01(2 * Mathf.PI * cutoff / rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += a * ((float)rng.NextDouble() * 2 - 1 - lp);
                float env = loop ? 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2) : Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.6f);
                float drone = droneHz > 0 ? Mathf.Sin(i * 2 * Mathf.PI * droneHz / rate) * 0.25f + Mathf.Sin(i * 2 * Mathf.PI * droneHz * 2.01f / rate) * 0.12f : 0;
                data[i] = (lp * 2.2f + drone) * env;
            }
            if (loop) for (int i = 0; i < 2000; i++) { float k = i / 2000f; data[i] = data[i] * k + data[n - 2000 + i] * (1 - k); }   // no click at the seam
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Clip(string name)
        {
            AudioClip[] takes;
            if (!_clips.TryGetValue(name, out takes))
            {
                var list = new List<AudioClip>();
                var a = Resources.Load<AudioClip>("Audio/" + name);
                if (a != null) list.Add(a);
                for (int i = 2; i <= 4; i++) { var b = Resources.Load<AudioClip>("Audio/" + name + "_" + i); if (b != null) list.Add(b); }
                _clips[name] = takes = list.ToArray();
            }
            return takes.Length == 0 ? null : takes[Random.Range(0, takes.Length)];
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (_pool == null) return;
            var clip = Clip(name);
            if (clip != null) Voice(clip, volume, pitch);
        }

        /// <summary>An aircraft on the runway: the bigger it is, the deeper and louder.</summary>
        public static void Aircraft(Core.AircraftClass c)
        {
            if (_pool == null) return;
            bool prop = c <= Core.AircraftClass.B;
            Voice(prop ? _prop : _jet, prop ? 0.16f : 0.2f + (int)c * 0.03f, prop ? 1.1f - (int)c * 0.15f : 1.25f - (int)c * 0.12f);
        }

        static void Voice(AudioClip clip, float volume, float pitch)
        {
            var s = _pool[_next]; _next = (_next + 1) % _pool.Length;
            s.clip = clip; s.volume = volume; s.pitch = pitch * Random.Range(0.97f, 1.03f);
            s.Play();
        }
    }
}
