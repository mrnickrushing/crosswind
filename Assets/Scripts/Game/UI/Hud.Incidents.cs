using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Incidents on screen: a red DECISION button that counts down while one waits (above the opportunity button, or
    /// beside the rail while a drawer is open),
    /// and the card it opens: what happened, the two answers with what each will do, and afterwards how it went.
    /// The card never opens by itself; an incident nobody answers settles the passive way and says so in a toast.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _incBtn, _incCard, _incBar;
        Text _incTitle, _incTime, _incCardTime;
        Image _incIcon;
        string _incShown;

        static readonly Color IncidentRed = new Color(0.86f, 0.3f, 0.24f);

        void BuildIncidentUi()
        {
            var b = Ui.Button("Incident", _safe, null, IncidentRed, OpenIncident);
            _incBtn = (RectTransform)b.transform;
            _incBtn.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 176), new Vector2(310, 140));
            _incIcon = Ui.Img("Icon", _incBtn, Color.white);
            _incIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 6), new Vector2(92, 92));
            _incIcon.preserveAspect = true; _incIcon.raycastTarget = false;
            var head = Ui.Label("Head", _incBtn, "DECISION", 26, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -12), new Vector2(120, 30));
            _incTime = Ui.Label("Time", _incBtn, "", 30, Color.white, TextAnchor.MiddleRight);
            _incTime.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -10), new Vector2(80, 34));
            _incTitle = Ui.Label("Title", _incBtn, "", 30, Color.white, TextAnchor.MiddleLeft);
            _incTitle.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(112, 20), new Vector2(186, 74));
            _incTitle.horizontalOverflow = HorizontalWrapMode.Wrap; _incTitle.lineSpacing = 0.85f;
            _incTitle.resizeTextForBestFit = true; _incTitle.resizeTextMinSize = 18; _incTitle.resizeTextMaxSize = 30;
            _incBtn.gameObject.SetActive(false);

            _sim.IncidentAppeared += i => { _dirty = true; if (GameRoot.Speed <= 3) Sfx.Play("chime", 0.8f); };
            _sim.IncidentSettled += line =>
            {
                _dirty = true;
                // Answered from the card, the card itself says how it went; otherwise a toast that stays a little longer.
                if (_incCard == null && GameRoot.Speed <= 3) { Toast(line); _toastT = 4.5f; }
            };
        }

        void UpdateIncidentUi()
        {
            var def = _sim.Incident;
            bool show = def != null && _tab != OpsTab && _incCard == null && _away == null;      // OPS has its own INCIDENTS page
            if (_incBtn.gameObject.activeSelf != show) _incBtn.gameObject.SetActive(show);
            if (show)
            {
                _incTitle.text = def.Title;
                _incTime.text = Mathf.CeilToInt((float)_sim.IncidentLeft) + "s";
                var icon = IconArt.Of(def.Icon);
                _incIcon.sprite = icon; _incIcon.enabled = icon != null;
                _incBtn.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 5) * 0.03f);
                // Above the opportunity button when both are up; clear of the ratings strip indoors.
                // With a drawer open on the right it moves to the foot of the airfield, beside the rail.
                float y = (CameraRig.Inside ? 124 : 20) + (_opp.gameObject.activeSelf ? 156 : 0);
                _incBtn.anchoredPosition = _tab >= 0 ? new Vector2(-(_safe.rect.width - 450), 20) : new Vector2(-20, y);
            }
            if (_incCard != null && _incShown != null)
            {
                // The incident on the card ran out, or another took its place: the card has nothing left to ask.
                if (def == null || def.Id != _incShown) { CloseIncident(); return; }
                float left = (float)_sim.IncidentLeft;
                _incCardTime.text = "Decide within " + Mathf.CeilToInt(left) + "s";
                _incBar.anchorMax = new Vector2(Mathf.Clamp01(left / Sim.IncidentSeconds), 1);
            }
        }

        void CloseIncident()
        {
            if (_incCard != null) Destroy(_incCard.gameObject);
            _incCard = null; _incShown = null;
        }

        RectTransform IncidentBox(string title, float height)
        {
            if (_incCard != null) Destroy(_incCard.gameObject);
            _incCard = Ui.Img("Incident card", _root, new Color(0, 0, 0, 0.72f)).rectTransform;
            _incCard.Fill(); _incCard.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _incCard, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1180, height));
            Ui.Img("Back", box, new Color(0.07f, 0.1f, 0.15f, 1f), true).rectTransform.Fill(10);
            var plate = Ui.ArtImg("Plate", box, "plate", Color.white).rectTransform;
            plate.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(760, 100));
            var t = Ui.Label("T", plate, title.ToUpper(), 48, Color.white);
            t.rectTransform.Fill(24); t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 48;
            return box;
        }

        void OpenIncident()
        {
            var def = _sim.Incident;
            if (def == null) return;
            Sfx.Play("open");
            var box = IncidentBox(def.Title, 740);
            _incShown = def.Id;
            var close = Ui.Button("Close", box, "X", new Color(0, 0, 0, 0), CloseIncident, 44);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -10), new Vector2(84, 84));

            var well = Ui.Img("Well", box, new Color(0.12f, 0.17f, 0.24f, 1f), true).rectTransform;
            well.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -84), new Vector2(170, 170));
            var icon = IconArt.Of(def.Icon);
            if (icon != null) { var im = Ui.Img("Icon", well, Color.white); im.sprite = icon; im.preserveAspect = true; im.rectTransform.Fill(14); }
            var story = Ui.Label("Story", box, _sim.IncidentStory, 38, Color.white, TextAnchor.MiddleLeft, false);
            story.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -84), new Vector2(900, 130));
            story.horizontalOverflow = HorizontalWrapMode.Wrap;
            story.resizeTextForBestFit = true; story.resizeTextMinSize = 26; story.resizeTextMaxSize = 38;
            // The time left: a bar that runs down, and the seconds.
            var track = Ui.Img("Track", box, new Color(1, 1, 1, 0.12f), true).rectTransform;
            track.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(236, -226), new Vector2(560, 14));
            _incBar = Ui.Img("Bar", track, IncidentRed, true).rectTransform;
            _incBar.anchorMin = Vector2.zero; _incBar.anchorMax = Vector2.one; _incBar.offsetMin = _incBar.offsetMax = Vector2.zero;
            _incCardTime = Ui.Label("Left", box, "", 28, Ui.Dim, TextAnchor.MiddleLeft, false);
            _incCardTime.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(812, -214), new Vector2(330, 38));

            for (int c = 0; c < def.Choices.Length; c++)
            {
                int choice = c;
                string blocked = _sim.IncidentBlocked(c);
                var tint = blocked != null ? Locked : c == 0 ? Ui.Green : Ui.Blue;
                var b = Ui.Button("Choice " + c, box, null, tint, () =>
                {
                    if (_sim.IncidentBlocked(choice) != null) { Sfx.Play("error"); return; }
                    string title = _sim.Incident.Title;
                    _sim.DecideIncident(choice);
                    ShowIncidentResult(title, _sim.State.incidentLog[0].line);
                });
                var rt = (RectTransform)b.transform;
                rt.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280 - c * 220), new Vector2(1100, 200));
                var head = Ui.Label("Head", rt, def.Choices[c].Label, 46, Color.white, TextAnchor.MiddleLeft);
                head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -22), new Vector2(1020, 60));
                Shade(head);
                var does = Ui.Label("Does", rt, _sim.IncidentDoes(c), 32, new Color(1, 1, 1, 0.92f), TextAnchor.UpperLeft, false);
                does.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -88), new Vector2(blocked != null ? 700 : 1020, 84));
                does.horizontalOverflow = HorizontalWrapMode.Wrap;
                does.resizeTextForBestFit = true; does.resizeTextMinSize = 22; does.resizeTextMaxSize = 32;
                if (blocked != null)
                {
                    var why = Ui.Label("Why", rt, blocked, 30, new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleRight);
                    why.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 34), new Vector2(330, 70));
                    why.horizontalOverflow = HorizontalWrapMode.Wrap;
                    why.resizeTextForBestFit = true; why.resizeTextMinSize = 20; why.resizeTextMaxSize = 30;
                }
            }
        }

        /// <summary>OPS, INCIDENTS: the one waiting (if any), what the last ones did, and how they went.</summary>
        void IncidentsPage()
        {
            var s = _sim;
            if (s.Stars < Sim.IncidentStars)
            {
                Header("FROM " + Sim.IncidentStars + " STARS");
                Ico("bell", TintGold).Entry("Incidents", "AT " + Sim.IncidentStars + " STARS", "Every few minutes something lands on your desk that the plan did not cover. Each is a choice between two answers with different costs.", null, null, null);
                return;
            }
            Header(s.State.incidentsDecided + " DECIDED  ·  ONE COMES UP EVERY FEW MINUTES AND WAITS " + (int)Sim.IncidentSeconds + " SECONDS  ·  UNANSWERED, IT SETTLES ITSELF THE PASSIVE WAY");
            var def = s.Incident;
            if (def != null)
                Ico(def.Icon, TintGold).Entry(def.Title, "WAITING · " + Mathf.CeilToInt((float)s.IncidentLeft) + "s", s.IncidentStory, "DECIDE", null, OpenIncident, IncidentRed);
            float mood = s.MoodShift;
            if (mood != 0)
                Ico("smile", TintGold).Entry("Passengers' mood", (mood > 0 ? "+" : "") + Mathf.RoundToInt(mood * 100) + " POINTS", (mood > 0 ? "Lifted" : "Dented") + " by a recent decision, for another " + Format.Time(s.MoodLeft) + ".", null, null, null);
            if (s.State.incidentLog.Count == 0 && def == null)
                Ico("bell", TintGold).Entry("Nothing yet", null, "Your decisions and how they went are kept here.", null, null, null);
            foreach (var r in s.State.incidentLog)
            {
                var d = Sim.Incidents.Find(x => x.Id == r.id);
                if (d == null) continue;
                string answer = r.lapsed ? "NO ANSWER" : r.choice >= 0 && r.choice < d.Choices.Length ? d.Choices[r.choice].Label : "";
                Ico(d.Icon).Entry(d.Title, "DAY " + r.day + " · " + answer, r.line, null, null, null);
            }
        }

        void ShowIncidentResult(string title, string line)
        {
            Sfx.Play("confirm");
            var box = IncidentBox(title, 470);
            _incShown = null;
            var text = Ui.Label("Line", box, line, 40, Color.white, TextAnchor.MiddleCenter, false);
            text.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1040, 200));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 26; text.resizeTextMaxSize = 40;
            var ok = Ui.Button("Ok", box, "OK", Ui.Green, CloseIncident, 52);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 116));
        }
    }
}
