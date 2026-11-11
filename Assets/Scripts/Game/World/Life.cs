using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The small things that make the airport look alive: passengers walking between each parked aircraft and the
    /// terminal while it deplanes and boards, and traffic on the road. People are a handful of meshes posed mid-stride
    /// (person_N_w0..w3), swapped to walk; nothing here changes the simulation.
    /// </summary>
    public sealed class Life : MonoBehaviour
    {
        const int People = 12, MaxWalkers = 90;
        static Mesh[][] _walk;
        static Mesh[] _idle;

        sealed class Walker { public GameObject Go; public MeshFilter Mf; public int Kind; public Vector3 From, To; public float T, Speed, Phase; public int Flight; }
        sealed class Car { public GameObject Go; public float Z, Speed; public int Dir; }

        readonly List<Walker> _walkers = new List<Walker>();
        readonly Stack<Walker> _pool = new Stack<Walker>();
        readonly List<Car> _cars = new List<Car>();
        readonly Dictionary<int, float> _nextFor = new Dictionary<int, float>();
        Sim _sim;

        public static Life Create(Transform parent)
        {
            var go = new GameObject("Life");
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Life>();
            l._sim = GameRoot.Sim;
            if (_walk == null)
            {
                _walk = new Mesh[People][]; _idle = new Mesh[People];
                for (int i = 0; i < People; i++)
                {
                    _walk[i] = new Mesh[4];
                    for (int f = 0; f < 4; f++) _walk[i][f] = Resources.Load<Mesh>("Models/person_" + i + "_w" + f);
                    _idle[i] = Resources.Load<Mesh>("Models/person_" + i + "_idle");
                }
            }
            // Road traffic: cars from the free kit, and our own shuttle bus and lorries.
            string[] models = { "sedan", "taxi", "suv", "bus", "hatchback", "lorry", "lorry2", "sedan", "pickup", "coupe" };
            for (int i = 0; i < models.Length; i++)
            {
                var c = models[i] == "bus" ? Gse.Bus().ToObject("Bus", go.transform)
                    : models[i] == "lorry" ? Gse.Lorry(Palette.Hex(0xD7DBDF)).ToObject("Lorry", go.transform)
                    : models[i] == "lorry2" ? Gse.Lorry(Palette.Hex(0x3E7A4A)).ToObject("Lorry", go.transform)
                    : Models.Kit(models[i], go.transform, Vector3.zero, 0, 4.2f);
                if (c == null) continue;
                l._cars.Add(new Car { Go = c, Z = -200 + i * 53, Speed = 14 + i % 3 * 4, Dir = i % 2 == 0 ? 1 : -1 });
            }
            return l;
        }

        Light[] _flood;

        /// <summary>Floodlights: three pools of light over the apron and one over the car park, on from dusk.</summary>
        void Floodlights(AirportView view)
        {
            if (_flood == null)
            {
                _flood = new Light[4];
                for (int i = 0; i < 4; i++)
                {
                    var l = new GameObject("Floodlight").AddComponent<Light>();
                    l.transform.SetParent(transform, false);
                    l.type = LightType.Point; l.range = 70; l.color = new Color(1f, 0.93f, 0.78f); l.shadows = LightShadows.None;
                    _flood[i] = l;
                }
            }
            float night = GameRoot.Sim.Night, termX = Layout.TerminalX(GameRoot.Sim);
            for (int i = 0; i < 3; i++) _flood[i].transform.position = new Vector3((Layout.TaxiX + termX) * 0.5f, 22, -40 + i * 40);
            _flood[3].transform.position = new Vector3(view.RoadX + 16, 16, -30);
            foreach (var l in _flood) { l.intensity = night * 260f; l.enabled = night > 0.02f && !CameraRig.Inside; }
        }

        GameObject _radar;

        void Radar(AirportView view)
        {
            if (_radar == null)
            {
                var mb = new MeshBuilder { Smoothness = 0.4f };
                mb.Box(new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1.0f, 0.12f), Palette.Hex(0xE8372E));
                mb.Box(new Vector3(0, 0.5f, 0.02f), new Vector3(2.9f, 0.7f, 0.12f), Palette.White);
                mb.Cylinder(Vector3.zero, new Vector3(0, 0.3f, 0), 0.12f, 6, Palette.Steel);
                _radar = mb.ToObject("Radar", transform);
            }
            _radar.transform.position = view.TowerTop;
            _radar.transform.rotation = Quaternion.Euler(0, Time.time * 70, 0);
        }

        void Update()
        {
            var view = AirportView.Instance;
            if (view != null) { Radar(view); Floodlights(view); }
            if (view == null || _walk == null || _walk[0][0] == null) return;
            float dt = Time.deltaTime * Mathf.Max(1, GameRoot.Speed);

            // Traffic: each car keeps its lane and wraps round.
            foreach (var c in _cars)
            {
                c.Z += c.Dir * c.Speed * dt;
                if (c.Z > 210) c.Z = -210; else if (c.Z < -210) c.Z = 210;
                c.Go.transform.position = new Vector3(view.RoadX + c.Dir * 1.7f, 0.05f, c.Z);
                c.Go.transform.rotation = Quaternion.Euler(0, c.Dir > 0 ? 180 : 0, 0);      // the kit's cars face -Z
            }

            // Passengers: a stream from each aircraft to the door while it deplanes, and back while it boards.
            foreach (var f in _sim.State.flights)
            {
                if (f.phase != Phase.Deplane && f.phase != Phase.Board) continue;
                // Jets board through the bridge, out of sight; the props are too low for one and use their own steps.
                if (view.HasBridge(f.stand) && Content.Routes[f.route].Class >= AircraftClass.C) continue;
                float next;
                _nextFor.TryGetValue(f.id, out next);
                if (Time.time < next || _walkers.Count >= MaxWalkers) continue;
                var plane = Content.Aircraft[Content.Routes[f.route].Class];
                _nextFor[f.id] = Time.time + Mathf.Lerp(0.9f, 0.22f, Mathf.InverseLerp(9, 300, plane.Seats)) / Mathf.Max(1, GameRoot.Speed);
                var stand = Layout.Stand(f.stand) + new Vector3(1.5f, 0, 2.2f + Random.Range(-0.5f, 0.5f));
                var door = view.TerminalDoor + new Vector3(-0.5f, 0, Random.Range(-1.5f, 1.5f));
                // People keep to the stand's own lane, then along the front of the terminal to the door.
                Spawn(f.id, f.phase == Phase.Deplane ? stand : door, f.phase == Phase.Deplane ? door : stand);
            }

            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                var w = _walkers[i];
                float len = Mathf.Max(0.1f, Vector3.Distance(w.From, w.To));
                w.T += dt * w.Speed / len;
                if (w.T >= 1) { w.Go.SetActive(false); _pool.Push(w); _walkers.RemoveAt(i); continue; }
                // An L-shaped path: across to the terminal front first, then along it.
                var corner = new Vector3(Mathf.Lerp(w.From.x, w.To.x, 0.85f), 0, w.From.z);
                float leg = Vector3.Distance(w.From, corner) / (Vector3.Distance(w.From, corner) + Vector3.Distance(corner, w.To) + 0.001f);
                Vector3 a = w.T < leg ? w.From : corner, b = w.T < leg ? corner : w.To;
                float q = w.T < leg ? w.T / Mathf.Max(0.001f, leg) : (w.T - leg) / Mathf.Max(0.001f, 1 - leg);
                var pos = Vector3.Lerp(a, b, q);
                w.Go.transform.position = pos;
                var dir = b - a;
                if (dir.sqrMagnitude > 0.01f) w.Go.transform.rotation = Quaternion.LookRotation(-dir.normalized);     // the kit's people face -Z
                w.Mf.sharedMesh = _walk[w.Kind][(int)(w.Phase + Time.time * 7 * Mathf.Min(3, GameRoot.Speed)) & 3];
            }
        }

        void Spawn(int flight, Vector3 from, Vector3 to)
        {
            Walker w;
            if (_pool.Count > 0) w = _pool.Pop();
            else
            {
                w = new Walker { Go = new GameObject("Passenger") };
                w.Go.transform.SetParent(transform, false);
                w.Mf = w.Go.AddComponent<MeshFilter>();
                var mr = w.Go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Materials.Kit;
                w.Go.transform.localScale = Vector3.one * 1.5f;       // a little larger than life, to read from the camera's height
            }
            w.Kind = Random.Range(0, People); w.From = from; w.To = to; w.T = 0; w.Speed = Random.Range(2.6f, 3.6f); w.Phase = Random.Range(0, 4); w.Flight = flight;
            w.Mf.sharedMesh = _walk[w.Kind][0];
            w.Go.SetActive(true);
            _walkers.Add(w);
        }
    }
}
