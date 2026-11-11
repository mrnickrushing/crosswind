using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The first minute, shown not told: a bouncing pointer over the next thing to tap (the FUEL button on the first
    /// parked aircraft, CLAIM on the guide card, then BUILD), until the player has done each once. And tapping an
    /// aircraft opens a small card about its flight.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _hand, _info;
        Text _handText, _infoText;
        int _infoFlight = -1;
        float _infoT;

        void BuildGuide()
        {
            var img = Ui.Img("Hand", _root, Ui.Gold); img.sprite = Ui.Circle;
            _hand = img.rectTransform;
            _hand.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            _handText = Ui.Label("T", _hand, "", 34, Color.white);
            _handText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(520, 50));
            Outline(_handText);
            _hand.gameObject.SetActive(false);

            _info = Ui.Img("Info", _root, Ui.Panel, true).rectTransform;
            _info.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(520, 190));
            _infoText = Ui.Label("T", _info, "", 30, Color.white, TextAnchor.UpperLeft, false);
            _infoText.rectTransform.Fill(18);
            _info.gameObject.SetActive(false);
        }

        void UpdateGuide()
        {
            // The pointer.
            var st = _sim.State;
            Vector2? at = null; string say = null;
            if (!st.tutorialDone && _tab < 0 && _away == null)
            {
                var waiting = _serviceButtons.FirstOrDefault(b => b.Rt.gameObject.activeSelf);
                if (st.flightsHandled == 0 && waiting != null) { at = waiting.Rt.anchoredPosition + new Vector2(0, -70); say = "Tap to send the fuel truck"; }
                else if (_sim.GoalDone && st.goalIndex < 3) { at = ((RectTransform)_goalClaim.transform).anchoredPosition + new Vector2(_root.rect.width * 0.5f - 130, _root.rect.height * 0.5f - 420); say = "Collect your reward"; }
                else if (st.goalIndex == 2 && _sim.UpgradeBlocked("stands") == null) { at = new Vector2(-_root.rect.width * 0.4f, -_root.rect.height * 0.5f + 250); say = "Open BUILD and add a stand"; }
                if (st.goalIndex >= 3) { st.tutorialDone = true; }
            }
            _hand.gameObject.SetActive(at.HasValue);
            if (at.HasValue)
            {
                _hand.anchoredPosition = at.Value + new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 26);
                _hand.SetAsLastSibling();
                _handText.text = say;
            }

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
