using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Tapping an aircraft opens a small card about its flight (the first-run tutorial is in Hud.Tutorial).
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _info;
        Text _infoText;
        int _infoFlight = -1;
        float _infoT;

        void BuildGuide()
        {
            _info = Ui.Img("Info", _root, Ui.Panel, true).rectTransform;
            _info.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(520, 190));
            _infoText = Ui.Label("T", _info, "", 30, Color.white, TextAnchor.UpperLeft, false);
            _infoText.rectTransform.Fill(18);
            _info.gameObject.SetActive(false);
        }

        void UpdateGuide()
        {
            // The flight card follows its aircraft and closes by itself.
            if (_infoFlight >= 0)
            {
                var plane = AirportView.Instance != null ? AirportView.Instance.Planes.FirstOrDefault(p => p.Flight.id == _infoFlight) : null;
                if (plane == null || (_infoT -= Time.unscaledDeltaTime) <= 0) { _infoFlight = -1; _info.gameObject.SetActive(false); }
                else
                {
                    var c = ToCanvas(plane.Head);
                    _info.anchoredPosition = new Vector2(Mathf.Clamp(c.x, -_root.rect.width * 0.5f + 280, _root.rect.width * 0.5f - 280), c.y + 90);
                    var r = plane.Route; var f = plane.Flight; var ac = Content.Aircraft[r.Class];
                    string phase = f.phase == Phase.Approach ? "Holding for the runway" : f.phase == Phase.Landing ? "Landing" : f.phase == Phase.TaxiIn ? "Taxiing to stand " + (f.stand + 1)
                        : f.phase == Phase.Deplane ? "Passengers getting off" : f.phase == Phase.Service ? "Being serviced: " + string.Join(", ", f.jobs.Where(j => !j.done).Select(j => ServiceWord(j.service).ToLower()))
                        : f.phase == Phase.Board ? "Boarding" : f.phase == Phase.TaxiOut ? "Taxiing out" : "Taking off";
                    bool late = f.parkedAt > 0 && f.phase >= Phase.Deplane && _sim.State.time - f.parkedAt > _sim.OnTimeTarget(r);
                    _infoText.text = "<size=38><b>" + Content.Airlines[r.Airline].Name + "</b></size>\n" + ac.Name + " to " + r.Destination + "\n" + phase
                        + "\n<color=" + (late ? "#FF7A6B>Running late" : "#7BD88F>On time") + "</color>  ·  worth " + Format.Money(_sim.FlightValue(r, !late));
                }
            }
        }

        /// <summary>A tap on the world (the drag pad saw a touch that didn't move): the nearest aircraft under it, if any.</summary>
        public void TapWorld(Vector2 screen)
        {
            if (CameraRig.Instance != null && CameraRig.Inside) { TerminalTap(screen); return; }
            if (AirportView.Instance == null || CameraRig.Instance == null) return;
            PlaneView best = null; float bestD = 120f * 120f * (Screen.height / 1920f) * (Screen.height / 1920f);
            foreach (var p in AirportView.Instance.Planes)
            {
                var sp = CameraRig.Instance.Cam.WorldToScreenPoint(p.transform.position + Vector3.up);
                if (sp.z < 0) continue;
                float d = ((Vector2)sp - screen).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) { _infoFlight = -1; _info.gameObject.SetActive(false); return; }
            _infoFlight = best.Flight.id; _infoT = 5f;
            _info.gameObject.SetActive(true); _info.SetAsLastSibling();
            Sfx.Play("click", 0.4f);
        }
    }
}
