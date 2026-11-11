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
            var go = Models.PlaneObject(route.Class, route.Airline, "Flight " + f.id, parent);
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
            if ((_phase == Phase.Landing || _phase == Phase.Takeoff) && GameRoot.Speed <= 3) Sfx.Aircraft(Route.Class);
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

        static string TruckModel(Service s) { return s == Service.Fuel ? "truck" : s == Service.Baggage || s == Service.Pushback ? "tractor" : s == Service.Catering ? "delivery" : s == Service.Deice ? "firetruck" : "van"; }
        ParticleSystem _spray;
        readonly Dictionary<Service, float> _truckT = new Dictionary<Service, float>();

        /// <summary>
        /// Each service under way has its vehicle: it drives out from its depot, works beside the aircraft, and drives
        /// home when the job is done.
        /// </summary>
        void Trucks()
        {
            int k = 0;
            float L = Models.Length(Route.Class);
            foreach (var j in Flight.jobs)
            {
                bool tug = j.service == Service.Pushback;
                // The tug stays on the nose through the push back off the stand.
                bool pushing = tug && j.done && _phase == Phase.TaxiOut && 1 - Mathf.Clamp01(Flight.timer / _duration) < 0.16f;
                bool working = ((_phase == Phase.Service || _phase == Phase.Board) && j.dispatched && !j.done) || pushing;
                GameObject t; float at;
                _trucks.TryGetValue(j.service, out t);
                _truckT.TryGetValue(j.service, out at);
                if (t == null)
                {
                    if (!working) { k++; continue; }
                    t = Models.Kit(TruckModel(j.service), transform.parent, Vector3.zero, 0, j.service == Service.Baggage ? 3.2f : tug ? 4.2f : j.service == Service.Deice ? Mathf.Clamp(L * 0.3f, 3.6f, 6f) : 5f) ?? Models.Truck(j.service).ToObject("Truck", transform.parent);
                    _trucks[j.service] = t; at = 0;
                }
                // 0 at the depot, 1 beside the aircraft.
                at = Mathf.MoveTowards(at, working ? 1 : 0, Time.deltaTime * Mathf.Max(1, GameRoot.Speed) * 0.55f);
                _truckT[j.service] = at;
                var side = Layout.Stand(StandOrLast()) + (k % 2 == 0 ? 1 : -1) * Vector3.forward * (L * 0.2f + 2.6f) + Vector3.left * (k / 2 * 3.4f - 1f);
                // The tug couples to the nose gear (the aircraft is nose-in, facing +X); the de-icer works off the wing.
                if (tug) side = (pushing ? transform.position : Layout.Stand(StandOrLast())) + Vector3.right * (L * 0.5f + 1.2f) + Vector3.forward * 0.01f;
                else if (j.service == Service.Deice) side = Layout.Stand(StandOrLast()) + Vector3.forward * (L * 0.34f + 3f) + Vector3.left * (L * 0.05f);
                Vector3 depot;
                if (AirportView.Instance == null || !AirportView.Instance.DepotAt.TryGetValue(Content.ServiceFacility(j.service), out depot)) depot = side + Vector3.back * 30;
                // Out along the apron's south edge, then up the stand's lane.
                var corner = new Vector3(side.x - 6, 0, depot.z + 6);
                float e = Mathf.SmoothStep(0, 1, at);
                Vector3 pos, dir;
                if (e < 0.5f) { pos = Vector3.Lerp(depot, corner, e * 2); dir = corner - depot; }
                else if (e < 0.9f) { var mid = new Vector3(side.x - 6, 0, side.z); pos = Vector3.Lerp(corner, mid, (e - 0.5f) / 0.4f); dir = mid - corner; }
                else { var mid = new Vector3(side.x - 6, 0, side.z); pos = Vector3.Lerp(mid, side, (e - 0.9f) / 0.1f); dir = side - mid; }
                if (!working) dir = -dir;
                if (pushing) { pos = side; dir = Vector3.right; }
                if (j.service == Service.Deice) Spray(working && at > 0.97f, L);
                t.SetActive(at > 0.001f || working);
                t.transform.position = pos + Vector3.up * 0.05f;
                if (dir.sqrMagnitude > 0.01f) t.transform.rotation = Quaternion.Slerp(t.transform.rotation, Quaternion.LookRotation(-dir.normalized), 1 - Mathf.Exp(-10 * Time.deltaTime));   // kit vehicles face -Z
                k++;
            }
        }

        /// <summary>De-icing fluid: a pale mist drifting over the wings while the crew is at work.</summary>
        void Spray(bool on, float length)
        {
            if (_spray == null)
            {
                if (!on) return;
                var go = new GameObject("Spray");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, length * 0.16f + 1.5f, 0);
                _spray = go.AddComponent<ParticleSystem>();
                _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _spray.main;
                main.startLifetime = 1.6f; main.startSpeed = 1.2f; main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startColor = new Color(0.86f, 0.95f, 1f, 0.34f); main.gravityModifier = 0.25f; main.maxParticles = 120;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = _spray.emission; em.rateOverTime = 34;
                var sh = _spray.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(length * 0.25f, 0.4f, length * 0.8f);
                var col = _spray.colorOverLifetime; col.enabled = true;
                var gr = new Gradient();
                gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
                col.color = gr;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = WeatherFx.Mist;
            }
            if (on && !_spray.isEmitting) _spray.Play();
            else if (!on && _spray.isEmitting) _spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDestroy() { foreach (var t in _trucks.Values) if (t != null) Destroy(t); }
    }
}
