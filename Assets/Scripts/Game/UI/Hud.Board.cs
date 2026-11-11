using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The flight board at the top right: every aircraft in the pattern and on the ground, its flight number, where
    /// it is from, and what it is doing right now, in the amber-on-black of a departures hall. Tap a row to look at
    /// that aircraft. It steps aside while a drawer is open.
    /// </summary>
    public sealed partial class Hud
    {
        const int BoardRows = 6;
        RectTransform _board;
        readonly List<Text[]> _boardRows = new List<Text[]>();
        readonly int[] _boardFlight = new int[BoardRows];
        Text _boardCount;
        float _boardIn;
        static readonly Color Amber = new Color(1f, 0.72f, 0.2f);

        void BuildBoard()
        {
            _board = Ui.ArtImg("Board", _safe, "panel", Color.white).rectTransform;
            _board.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(520, 76 + BoardRows * 40));
            Ui.Label("Cap", _board, "FLIGHTS", 24, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -10), new Vector2(200, 34));
            _boardCount = Ui.Label("Count", _board, "", 22, Ui.Dim, TextAnchor.MiddleRight, false);
            _boardCount.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -10), new Vector2(300, 34));
            var well = Ui.ArtImg("Well", _board, "well", Color.white).rectTransform;
            well.Fill(); well.offsetMin = new Vector2(12, 12); well.offsetMax = new Vector2(-12, -50);
            for (int i = 0; i < BoardRows; i++)
            {
                int k = i;
                var row = Ui.Button("Row", well, null, new Color(0, 0, 0, 0), () => LookAtFlight(_boardFlight[k]), 20);
                var rt = (RectTransform)row.transform;
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(8, 0); rt.offsetMax = new Vector2(-8, 0); rt.anchoredPosition = new Vector2(0, -6 - i * 40); rt.sizeDelta = new Vector2(-16, 38);
                var code = Ui.Label("Code", rt, "", 26, Amber, TextAnchor.MiddleLeft); code.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(110, 36));
                var dest = Ui.Label("Dest", rt, "", 26, Color.white, TextAnchor.MiddleLeft, false); dest.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(118, 0), new Vector2(210, 36));
                dest.horizontalOverflow = HorizontalWrapMode.Overflow; dest.resizeTextForBestFit = true; dest.resizeTextMinSize = 16; dest.resizeTextMaxSize = 26;
                var status = Ui.Label("Status", rt, "", 24, Amber, TextAnchor.MiddleRight); status.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(150, 36));
                _boardRows.Add(new[] { code, dest, status });
            }
        }

        static string FlightCode(FlightState f)
        {
            var r = Content.Routes[f.route];
            string name = r.Airline == "own" ? "YA" : Content.Airlines[r.Airline].Name;
            string two = r.Airline == "own" ? "YA" : (name.Substring(0, 1) + (name.IndexOf(' ') > 0 && name.IndexOf(' ') + 1 < name.Length ? name.Substring(name.IndexOf(' ') + 1, 1) : name.Substring(1, 1))).ToUpper();
            return two + (100 + f.id * 7 % 900);
        }

        void UpdateBoard()
        {
            bool show = _tab < 0 && !CameraRig.Inside;
            if (_board.gameObject.activeSelf != show) _board.gameObject.SetActive(show);
            var clock = _weather.transform.parent.gameObject; if (clock.activeSelf != (_tab < 0)) { clock.SetActive(_tab < 0); _chips.gameObject.SetActive(_tab < 0); }
            if (!show || (_boardIn -= Time.unscaledDeltaTime) > 0) return;
            _boardIn = 0.4f;
            var s = _sim;
            // Those that need you first, then by how far along they are.
            var flights = s.State.flights.OrderByDescending(f => NeedsCrew(f)).ThenByDescending(f => (int)f.phase).ThenBy(f => f.id).Take(BoardRows).ToList();
            _boardCount.text = s.State.flights.Count + " OF " + s.TowerSlots + " IN THE PATTERN";
            for (int i = 0; i < BoardRows; i++)
            {
                var t = _boardRows[i];
                if (i >= flights.Count) { t[0].text = i == 0 ? "" : ""; t[1].text = i == 0 && flights.Count == 0 ? "No traffic" : ""; t[1].color = Ui.Dim; t[2].text = ""; _boardFlight[i] = -1; continue; }
                var f = flights[i]; var r = Content.Routes[f.route];
                _boardFlight[i] = f.id;
                t[0].text = FlightCode(f);
                t[1].text = r.Destination; t[1].color = Color.white;
                bool late = f.phase >= Phase.Deplane && f.phase <= Phase.Board && s.State.time - f.parkedAt - f.held > s.OnTimeTarget(r);
                bool needs = NeedsCrew(f);
                var leavingJob = f.phase == Phase.Board && f.boarded ? f.jobs.Find(j => !j.done) : null;
                string st; Color c = Amber;
                switch (f.phase)
                {
                    case Phase.Approach: st = "HOLDING"; c = Ui.Dim; break;
                    case Phase.Landing: st = "LANDING"; break;
                    case Phase.TaxiIn: st = "TAXI IN"; break;
                    case Phase.Deplane: st = "ARRIVED"; break;
                    case Phase.Service: st = needs ? "NEEDS CREW" : "SERVICING"; if (needs) c = Ui.Red; break;
                    case Phase.Board:
                        if (!f.boarded) { st = "BOARDING"; c = Ui.Green; }
                        else if (leavingJob == null) { st = "GATE CLOSED"; c = Ui.Green; }
                        else if (needs) { st = leavingJob.service == Service.Deice ? "NEEDS DE-ICE" : "NEEDS TUG"; c = Ui.Red; }
                        else { st = leavingJob.service == Service.Deice ? "DE-ICING" : "PUSHBACK"; }
                        break;
                    case Phase.TaxiOut: st = "TAXI OUT"; break;
                    default: st = "DEPARTING"; c = Ui.Green; break;
                }
                if (late && !needs) { st = "DELAYED"; c = Ui.Red; }
                t[2].text = st; t[2].color = c;
            }
        }

        /// <summary>A crew is wanted and nobody has sent one: a service at the stand, or the spray or the tug after boarding.</summary>
        static bool NeedsCrew(FlightState f)
        {
            if (f.phase == Phase.Service) return f.jobs.Any(j => !j.dispatched && !j.done);
            if (f.phase != Phase.Board || !f.boarded) return false;
            var j2 = f.jobs.Find(j => !j.done);
            return j2 != null && !j2.dispatched;
        }

        /// <summary>Swings the camera to an aircraft (from the board).</summary>
        void LookAtFlight(int id)
        {
            if (id < 0 || AirportView.Instance == null) return;
            var plane = AirportView.Instance.Planes.FirstOrDefault(p => p.Flight.id == id);
            if (plane == null) return;
            CameraRig.Instance.Focus = new Vector3(plane.Head.x, 0, plane.Head.z);
            Sfx.Play("open", 0.4f);
        }

        void OnInsideChanged() { _dirty = true; if (!CameraRig.Inside) { StopPlacing(); ClosePick(); } else if (_tab >= 0 && _tab != ShopTab) OpenTab(-1); }
    }
}
