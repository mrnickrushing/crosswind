using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// One flight's aircraft in the world. The simulation says which phase it is in and how long is left; this turns
    /// that into where the aircraft is: circling on approach, flaring onto the runway, following the taxi lines,
    /// parked nose-in at its stand with the service trucks that are working on it, then out and away.
    /// </summary>
    public sealed class PlaneView : MonoBehaviour
    {
        public FlightState Flight { get; private set; }
        public RouteDef Route { get; private set; }
        float _duration = 1;
        Phase _phase = (Phase)(-1);
        List<Vector3> _path;
        readonly Dictionary<Service, GameObject> _trucks = new Dictionary<Service, GameObject>();
        float _holdAngle;
        Vector3 _vel;
        Sim _sim;

        public static PlaneView Create(Transform parent, FlightState f)
        {
            var route = Content.Routes[f.route];
            var go = Models.Plane(route.Class, route.Airline).ToObject("Flight " + f.id, parent);
            var v = go.AddComponent<PlaneView>();
            v.Flight = f; v.Route = route; v._sim = GameRoot.Sim;
            v._holdAngle = f.id * 1.7f;
            return v;
        }

        /// <summary>The simulation moved this flight to a new phase.</summary>
        public void Enter()
        {
            if (Flight.phase == _phase) return;
            _phase = Flight.phase;
            _duration = Mathf.Max(0.01f, Flight.timer);
            _path = _phase == Phase.TaxiIn ? Layout.TaxiIn(_sim, Flight.stand) : _phase == Phase.TaxiOut ? Layout.TaxiOut(_sim, StandOrLast()) : null;
            if (Flight.stand >= 0) _lastStand = Flight.stand;
        }

        int _lastStand;
        int StandOrLast() { return Flight.stand >= 0 ? Flight.stand : _lastStand; }

        /// <summary>Above the aircraft, for the service buttons.</summary>
        public Vector3 Head { get { return transform.position + Vector3.up * (Models.Length(Route.Class) * 0.35f + 2.5f); } }

        void Update()
        {
            float p = 1 - Mathf.Clamp01(Flight.timer / _duration);
            float half = Layout.RunwayHalf(_sim);
            Vector3 pos = transform.position, dir = transform.forward;
            switch (_phase)
            {
                case Phase.Approach:
                {
                    // A holding pattern south of the field, stacked by flight.
                    _holdAngle += Time.deltaTime * GameRoot.Speed * 0.35f;
                    var c = new Vector3(Layout.RunwayX - 18, 22 + (Flight.id % 4) * 4, -half - 75);
                    pos = c + new Vector3(Mathf.Cos(_holdAngle), 0, Mathf.Sin(_holdAngle)) * 26;
                    dir = new Vector3(-Mathf.Sin(_holdAngle), 0, Mathf.Cos(_holdAngle));
                    break;
                }
                case Phase.Landing:
                {
                    float z0 = -half - 60, zTouch = -half + 14, zEnd = Layout.ExitZ(_sim);
                    float flare = 0.42f;
                    if (p < flare) { float q = p / flare; pos = new Vector3(Layout.RunwayX, Mathf.Lerp(16, 0, q * q * (3 - 2 * q)), Mathf.Lerp(z0, zTouch, q)); }
                    else { float q = (p - flare) / (1 - flare); pos = new Vector3(Layout.RunwayX, 0, Mathf.Lerp(zTouch, zEnd, 1 - (1 - q) * (1 - q))); }
                    dir = Vector3.forward;
                    break;
                }
                case Phase.TaxiIn:
                    pos = Layout.Along(_path, Mathf.SmoothStep(0, 1, p), out dir);
                    break;
                case Phase.Deplane: case Phase.Service: case Phase.Board:
                    pos = Layout.Stand(StandOrLast()); dir = Vector3.right;
                    break;
                case Phase.TaxiOut:
                    // Pushed back tail-first off the stand, then forward down the taxiway to hold short of the runway.
                    pos = Layout.Along(_path, Mathf.SmoothStep(0, 1, p), out dir);
                    if (p < 0.16f) dir = Vector3.right;
                    break;
                case Phase.Takeoff:
                {
                    float z0 = Layout.LineUpZ(_sim), rotate = 0.55f;
                    float run = p * p;                                             // accelerating
                    pos = new Vector3(Layout.RunwayX, p < rotate ? 0 : Mathf.Pow((p - rotate) / (1 - rotate), 1.6f) * 26, Mathf.Lerp(z0, half + 90, run));
                    dir = p < rotate ? Vector3.forward : new Vector3(0, 0.28f, 1).normalized;
                    break;
                }
                case Phase.Gone:
                    return;
            }
            // Eased, so a phase change never snaps the aircraft.
            bool snap = (transform.position - pos).sqrMagnitude > 90 * 90 || transform.position == Vector3.zero;
            transform.position = snap ? pos : Vector3.SmoothDamp(transform.position, pos, ref _vel, 0.12f);
            if (dir.sqrMagnitude > 0.001f)
            {
                var want = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = snap ? want : Quaternion.Slerp(transform.rotation, want, 1 - Mathf.Exp(-6 * Time.deltaTime * Mathf.Max(1, GameRoot.Speed)));
            }
            Trucks();
        }

        /// <summary>A truck stands by the aircraft for each service that is under way.</summary>
        void Trucks()
        {
            int k = 0;
            foreach (var j in Flight.jobs)
            {
                bool working = _phase == Phase.Service && j.dispatched && !j.done;
                GameObject t;
                if (!_trucks.TryGetValue(j.service, out t))
                {
                    if (!working) { k++; continue; }
                    t = Models.Truck(j.service).ToObject("Truck " + j.service, transform.parent);
                    _trucks[j.service] = t;
                }
                t.SetActive(working);
                if (working)
                {
                    float L = Models.Length(Route.Class);
                    var side = (k % 2 == 0 ? 1 : -1) * Vector3.forward * (L * 0.2f + 2.2f) + Vector3.left * (k / 2 * 3f - 1f);
                    t.transform.position = Layout.Stand(StandOrLast()) + side;
                    t.transform.rotation = Quaternion.LookRotation(k % 2 == 0 ? Vector3.back : Vector3.forward);
                }
                k++;
            }
        }

        void OnDestroy() { foreach (var t in _trucks.Values) if (t != null) Destroy(t); }
    }
}
