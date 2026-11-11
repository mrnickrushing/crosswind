using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The screen: money, income and stars at the top; the guide card under them; service buttons floating over the
    /// aircraft that are waiting for a crew; BUILD, AIRLINES and STAFF sheets from the bottom bar; pay pop-ups, toasts,
    /// the star banner and the welcome-back card.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        Sim _sim;
        RectTransform _root, _world, _sheet, _sheetList, _toast, _goal, _banner, _away;
        Text _money, _rate, _starText, _goalText, _goalCount, _toastText, _sheetTitle;
        Image _starFill, _goalFill;
        Button _goalClaim;
        readonly List<Button> _tabs = new List<Button>();
        readonly List<ServiceButton> _serviceButtons = new List<ServiceButton>();
        readonly List<Pop> _pops = new List<Pop>();
        int _tab = -1;
        bool _dirty = true;
        float _refreshIn, _toastT, _bannerT;
        double _shownMoney;

        sealed class ServiceButton { public RectTransform Rt; public Image Bg, Tail, Icon; public Text Label; public int Flight; public Service Service; }
        sealed class Pop { public RectTransform Rt; public CanvasGroup Group; public Vector3 World; public Vector2 At, Drift; public bool Screen; public float Age, Life = 1.7f; }

        static readonly string[] TabNames = { "BUILD", "AIRLINES", "STAFF", "RESEARCH", "EMPIRE", "FRIENDS" };
        static readonly string[] TabIcons = { "wrench", "plane", "gear", "zoom", "trophy", "multiplayer" };
        Text _gems, _weather, _chips, _oppText;
        RectTransform _opp, _empireDot, _staffDot;

        public static Hud Create()
        {
            var go = new GameObject("HUD");
            var h = go.AddComponent<Hud>();
            Instance = h;
            h._sim = GameRoot.Sim;
            h.Build();
            h._sim.Changed += h.OnChanged;
            h._sim.Toast += h.Toast;
            h._sim.FlightPaid += h.OnPaid;
            h._sim.StarGained += h.OnStar;
            h._sim.RouteLost += r => { h._dirty = true; Sfx.Play("error"); };
            h._sim.BuildDone += f => { h._dirty = true; if (GameRoot.Speed <= 3) Sfx.Play("build"); };
            h._sim.OpportunityAppeared += o => Sfx.Play("chime", 0.6f);
            h._sim.WeatherChanged += w => { if (w != Weather.Clear) h.Toast(w + ": the runway slows down"); };
            h._shownMoney = h._sim.State.money;
            h.OfferAway();
            h.OfferCloud();
            return h;
        }

        void OnDestroy()
        {
            if (_sim != null) { _sim.Changed -= OnChanged; _sim.Toast -= Toast; _sim.FlightPaid -= OnPaid; _sim.StarGained -= OnStar; }
            if (Instance == this) Instance = null;
        }

        void OnChanged() { _dirty = true; }

        // ------------------------------------------------------------------ layout

        void Build()
        {
            _root = Ui.Canvas("Canvas", transform);
            // Everything not covered by a control drags the camera.
            var pad = Ui.Img("Pad", _root, new Color(0, 0, 0, 0));
            pad.rectTransform.Fill(); pad.raycastTarget = true;
            pad.gameObject.AddComponent<DragPad>();
            _world = Ui.Rect("World", _root); _world.Fill();
            // The controls keep clear of the notch and the home bar.
            _safe = Ui.Rect("Safe", _root); _safe.Fill();
            _safe.gameObject.AddComponent<SafeArea>();

            // Left: the navigation rail.
            var rail = Ui.ArtImg("Rail", _safe, "panel", Color.white).rectTransform;
            rail.anchorMin = new Vector2(0, 0); rail.anchorMax = new Vector2(0, 1); rail.pivot = new Vector2(0, 0.5f);
            rail.offsetMin = new Vector2(12, 12); rail.offsetMax = new Vector2(140, -12);
            rail.GetComponent<Image>().raycastTarget = true;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int k = i;
                var b = Ui.Button(TabNames[i], rail, TabNames[i], Ui.Card, () => OpenTab(_tab == k ? -1 : k), 22);
                var lab = b.GetComponentInChildren<Text>().rectTransform; lab.anchorMin = new Vector2(0, 0); lab.anchorMax = new Vector2(1, 0.36f); lab.offsetMin = new Vector2(2, 8); lab.offsetMax = new Vector2(-2, 0);
                Ui.IconImg(TabIcons[i], b.transform, Color.white, new Vector2(0.5f, 0.62f), Vector2.zero, 50);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12 - i * 124), new Vector2(112, 116));
                _tabs.Add(b);
            }
            var dot = Ui.Img("Dot", _tabs[4].transform, Ui.Red); dot.sprite = Ui.Circle;
            _empireDot = dot.rectTransform; _empireDot.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(26, 26));
            var dot2 = Ui.Img("Dot", _tabs[2].transform, Ui.Red); dot2.sprite = Ui.Circle;
            _staffDot = dot2.rectTransform; _staffDot.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(26, 26));
            // INSIDE / OUTSIDE at the foot of the rail: look into the terminal, or back over the airfield.
            var inside = Ui.Button("Inside", rail, "TERMINAL", Ui.Blue, null, 22);
            ((RectTransform)inside.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(112, 118));
            var insideText = inside.GetComponentInChildren<Text>();
            var il = insideText.rectTransform; il.anchorMin = new Vector2(0, 0); il.anchorMax = new Vector2(1, 0.36f); il.offsetMin = new Vector2(2, 8); il.offsetMax = new Vector2(-2, 0);
            Ui.IconImg("home", inside.transform, Color.white, new Vector2(0.5f, 0.62f), Vector2.zero, 50);
            inside.onClick.AddListener(() => { Sfx.Play("open", 0.5f); CameraRig.Instance.SetInside(!CameraRig.Inside); insideText.text = CameraRig.Inside ? "AIRFIELD" : "TERMINAL"; OnInsideChanged(); });

            // Top left: the airport's vital signs in one compact block (cash, income, gems, rating).
            var top = Ui.ArtImg("Status", _safe, "panel", Color.white).rectTransform;
            top.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(152, -12), new Vector2(660, 132));
            var coin = Ui.ArtImg("Coin", top, "coin", Color.white, 0);
            coin.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(64, 64));
            _money = Ui.Label("Money", top, "0", 60, Color.white, TextAnchor.MiddleLeft);
            _money.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -8), new Vector2(290, 74));
            _rate = Ui.Label("Rate", top, "", 28, Ui.Green, TextAnchor.MiddleLeft, false);
            _rate.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 12), new Vector2(300, 38));
            var div1 = Ui.Img("Div", top, new Color(1, 1, 1, 0.12f)).rectTransform; div1.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(384, 0), new Vector2(2, 96));
            var gem = Ui.ArtImg("Gem", top, "gem", Color.white, 0);
            gem.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(398, -20), new Vector2(46, 46));
            _gems = Ui.Label("Gems", top, "0", 36, Color.white, TextAnchor.MiddleLeft);
            _gems.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(450, -18), new Vector2(110, 50));
            Ui.Label("GemsCap", top, "GEMS", 20, Ui.Dim, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(402, 14), new Vector2(120, 30));
            var div2 = Ui.Img("Div", top, new Color(1, 1, 1, 0.12f)).rectTransform; div2.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(508, 0), new Vector2(2, 96));
            var starIcon = Ui.ArtImg("Star", top, "star", Color.white, 0);
            starIcon.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(520, -16), new Vector2(52, 52));
            _starText = Ui.Label("Stars", top, "", 44, Ui.Gold, TextAnchor.MiddleLeft);
            _starText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(578, -14), new Vector2(80, 56));
            var bar = Ui.ArtImg("StarBar", top, "well", Color.white, 44, 3f).rectTransform;
            bar.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(522, 18), new Vector2(124, 20));
            var barIn = Ui.Rect("In", bar); barIn.Fill(4);
            _starFill = Ui.Img("Fill", barIn, Ui.Gold, true);
            _starFill.rectTransform.anchorMin = Vector2.zero; _starFill.rectTransform.anchorMax = new Vector2(0, 1); _starFill.rectTransform.offsetMin = _starFill.rectTransform.offsetMax = Vector2.zero;

            // The guide card under it.
            _goal = Ui.ArtImg("Goal", _safe, "panel", Color.white).rectTransform;
            _goal.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(152, -152), new Vector2(660, 132));
            var accent = Ui.Img("Accent", _goal, Ui.Green, true).rectTransform; accent.anchorMin = new Vector2(0, 0); accent.anchorMax = new Vector2(0, 1); accent.offsetMin = new Vector2(8, 14); accent.offsetMax = new Vector2(14, -14);
            Ui.Label("Cap", _goal, "NEXT", 20, Ui.Green, TextAnchor.UpperLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -10), new Vector2(200, 26));
            _goalText = Ui.Label("Text", _goal, "", 30, Color.white, TextAnchor.UpperLeft, false);
            _goalText.rectTransform.Fill(); _goalText.rectTransform.offsetMin = new Vector2(26, 30); _goalText.rectTransform.offsetMax = new Vector2(-200, -34);
            _goalText.resizeTextForBestFit = true; _goalText.resizeTextMinSize = 20; _goalText.resizeTextMaxSize = 28; _goalText.verticalOverflow = VerticalWrapMode.Truncate;
            var gbar = Ui.Img("Bar", _goal, new Color(0, 0, 0, 0.45f), true).rectTransform;
            gbar.anchorMin = new Vector2(0, 0); gbar.anchorMax = new Vector2(1, 0); gbar.pivot = new Vector2(0, 0); gbar.offsetMin = new Vector2(26, 12); gbar.offsetMax = new Vector2(-200, 24);
            _goalFill = Ui.Img("Fill", gbar, Ui.Green, true);
            _goalFill.rectTransform.anchorMin = Vector2.zero; _goalFill.rectTransform.anchorMax = new Vector2(0, 1); _goalFill.rectTransform.offsetMin = _goalFill.rectTransform.offsetMax = Vector2.zero;
            _goalCount = Ui.Label("Count", _goal, "", 28, Ui.Dim, TextAnchor.MiddleCenter);
            _goalCount.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(176, 84));
            _goalClaim = Ui.Button("Claim", _goal, "CLAIM", Ui.Green, () => { var g = _sim.CurrentGoal; if (_sim.ClaimGoal()) { Reward(g.Money * _sim.Map.IncomeScale, g.Gems); Sfx.Play("claim"); _dirty = true; } }, 36);
            ((RectTransform)_goalClaim.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(176, 84));

            // Top centre: the clock and the weather; under it what is running (a boost, a rush).
            var clock = Ui.ArtImg("Clock", _safe, "panel", Color.white).rectTransform;
            clock.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, -12), new Vector2(380, 58));
            _weather = Ui.Label("Weather", clock, "", 30, Color.white);
            _weather.rectTransform.Fill(4);
            _chips = Ui.Label("Chips", _safe, "", 30, Ui.Gold);
            _chips.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, -76), new Vector2(620, 44));
            Outline(_chips);

            BuildBoard();

            // An opportunity: a button at the bottom right that counts down.
            var opp = Ui.Button("Opportunity", _safe, "", Ui.Gold, () => { if (_sim.TakeOpportunity()) { _dirty = true; Sfx.Play("confirm"); } }, 34);
            _opp = (RectTransform)opp.transform;
            _opp.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(310, 140));
            _oppText = opp.GetComponentInChildren<Text>(); _oppText.color = Ui.Ink;
            _opp.gameObject.SetActive(false);

            // The drawer the rail opens: down the right-hand side, the airfield still in view beside it.
            _sheet = Ui.ArtImg("Sheet", _safe, "panel", Color.white).rectTransform;
            _sheet.anchorMin = new Vector2(1, 0); _sheet.anchorMax = new Vector2(1, 1); _sheet.pivot = new Vector2(1, 0.5f);
            _sheet.offsetMin = new Vector2(-892, 12); _sheet.offsetMax = new Vector2(-12, -12);
            _sheet.GetComponent<Image>().raycastTarget = true;
            _sheetTitle = Ui.Label("Title", _sheet, "", 44, Color.white, TextAnchor.MiddleLeft);
            _sheetTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -10), new Vector2(600, 70));
            var rule = Ui.Img("Rule", _sheet, new Color(0.25f, 0.76f, 1f, 0.9f)).rectTransform; rule.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -78), new Vector2(90, 4));
            var close = Ui.Button("Close", _sheet, "X", new Color(0, 0, 0, 0), () => OpenTab(-1), 46);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -6), new Vector2(90, 80));
            var view = Ui.Img("View", _sheet, new Color(0, 0, 0, 0.001f)).rectTransform;
            view.Fill(); view.offsetMin = new Vector2(16, 16); view.offsetMax = new Vector2(-16, -92);
            view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            _sheetList = Ui.Rect("List", view);
            _sheetList.anchorMin = new Vector2(0, 1); _sheetList.anchorMax = new Vector2(1, 1); _sheetList.pivot = new Vector2(0.5f, 1);
            _sheetList.offsetMin = Vector2.zero; _sheetList.offsetMax = Vector2.zero;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _sheetList; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            _sheet.gameObject.SetActive(false);

            BuildTerminalUi();
            BuildGuide();
            // Toast and the star banner.
            BuildFeedback();
        }

        RectTransform _safe;

        /// <summary>The middle of a control, as a position on the canvas (for pointers and chips that fly from it).</summary>
        Vector2 CanvasPos(RectTransform rt) { var p = _root.InverseTransformPoint(rt.TransformPoint(rt.rect.center)); return new Vector2(p.x, p.y); }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            // Money counts up to its value instead of jumping.
            _shownMoney += (_sim.State.money - _shownMoney) * (1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            if (System.Math.Abs(_sim.State.money - _shownMoney) < 0.6) _shownMoney = _sim.State.money;
            _money.text = Format.Money(_shownMoney);
            _rate.text = "+" + Format.Money(_sim.State.incomeRate * 60) + " / min";
            int stars = _sim.Stars;
            _starText.text = stars.ToString();
            _starFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(_sim.StarProgress), 1);

            var g = _sim.CurrentGoal;
            _goal.gameObject.SetActive(g != null);
            if (g != null)
            {
                bool done = _sim.GoalDone;
                _goalText.text = g.Text;
                _goalFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(_sim.GoalProgress / g.Target)), 1);
                _goalClaim.gameObject.SetActive(done);
                _goalCount.gameObject.SetActive(!done);
                _goalCount.text = g.Target > 1 ? Format.Money(_sim.GoalProgress) + " / " + Format.Money(g.Target) : "+" + Format.Money(g.Money);
            }

            _gems.text = _sim.State.gems.ToString();
            var w = _sim.Weather;
            int hh = (int)_sim.Hour, mm = (int)((_sim.Hour - hh) * 60) / 10 * 10;
            string clock = hh.ToString("00") + ":" + mm.ToString("00") + (w == Weather.Clear ? "   CLEAR" : "   ");
            _weather.text = clock + (w == Weather.Clear ? "" : (w == Weather.Rain ? "RAIN" : w == Weather.Fog ? "FOG" : w == Weather.Storm ? "STORM" : "SNOW") + "  <size=24>runway " + Mathf.RoundToInt(_sim.WeatherFactor * 100) + "%</size>");
            _chips.text = (_sim.Rush ? "RUSH x2  " + Format.Time(_sim.State.rushUntil - _sim.State.time) + "   " : "") + (_sim.Boosted ? "BOOST x2  " + Format.Time(_sim.BoostLeft) : "");
            var o = _sim.State.opportunity;
            _opp.gameObject.SetActive(o != null && _tab < 0);
            if (o != null)
            {
                _oppText.text = (o.kind == "charter" ? "CHARTER\n<size=44>+" + Format.Money(o.value) + "</size>" : o.kind == "rush" ? "RUSH HOUR\n<size=34>double pay</size>" : "LUCKY FIND\n<size=34>+" + (int)o.value + " gems</size>")
                    + "\n<size=26>" + Mathf.CeilToInt((float)(o.expiresAt - _sim.State.time)) + "s</size>";
                _opp.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6) * 0.03f);
            }
            _staffDot.gameObject.SetActive(_sim.State.crates > 0 || _sim.FreeCrateReady);
            _empireDot.gameObject.SetActive(_sim.SignInReady || _sim.EventWaiting > 0 || _sim.FreeBoostReady || _sim.AchievementsWaiting > 0 || _sim.State.tasks.Exists(x => !x.claimed && x.progress >= x.target));
            UpdateBoard();
            UpdateTerminalUi();
            UpdateServiceButtons();
            UpdatePops();
            UpdateGuide();
            if ((_refreshIn -= Time.unscaledDeltaTime) <= 0 || _dirty) { _refreshIn = 0.5f; if (_tab >= 0) RefreshSheet(); _dirty = false; }
            UpdateFeedback();
        }

        static void Outline(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.75f); o.effectDistance = new Vector2(2, -2); }


        /// <summary>A point in the world as a position on the canvas (z under 0: behind the camera).</summary>
        Vector3 ToCanvas(Vector3 world)
        {
            var cam = CameraRig.Instance.Cam;
            var sp = cam.WorldToScreenPoint(world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out local);
            return new Vector3(local.x, local.y, sp.z);
        }

        // ------------------------------------------------------------------ service buttons over waiting aircraft

        static Color ServiceColor(Service s) { return s == Service.Fuel ? Palette.Fuel : s == Service.Baggage ? Palette.Baggage : s == Service.Catering ? Palette.Catering : s == Service.Pushback ? Palette.Pushback : s == Service.Deice ? Palette.Deice : Palette.Cleaning; }
        static string ServiceWord(Service s) { return s == Service.Fuel ? "FUEL" : s == Service.Baggage ? "BAGS" : s == Service.Catering ? "FOOD" : s == Service.Pushback ? "TUG" : s == Service.Deice ? "DE-ICE" : "CLEAN"; }
        static string ServiceIcon(Service s) { return s == Service.Fuel ? "svc_fuel" : s == Service.Baggage ? "svc_bags" : s == Service.Catering ? "svc_food" : s == Service.Pushback ? "svc_tug" : s == Service.Deice ? "svc_deice" : "svc_clean"; }

        void UpdateServiceButtons()
        {
            int used = 0;
            if (AirportView.Instance != null && !CameraRig.Inside)
                foreach (var plane in AirportView.Instance.Planes)
                {
                    var f = plane.Flight;
                    bool leaving = f.phase == Phase.Board && f.boarded;
                    if (f.phase != Phase.Service && !leaving) continue;
                    // After boarding the jobs go one at a time (the spray, then the tug): only the next one is offered.
                    var first = leaving ? f.jobs.Find(j => !j.done) : null;
                    int k = 0, waiting = leaving ? 1 : f.jobs.Count(j => !j.dispatched && !j.done);
                    foreach (var j in f.jobs)
                    {
                        if (j.dispatched || j.done || (leaving && j != first)) continue;
                        var c = ToCanvas(plane.Head);
                        if (c.z < 0) { k++; continue; }
                        var b = ButtonAt(used++);
                        b.Flight = f.id; b.Service = j.service;
                        b.Bg.color = _sim.CrewFree(j.service) ? ServiceColor(j.service) : Ui.Off;
                        b.Tail.color = b.Bg.color;
                        b.Label.text = ServiceWord(j.service);
                        b.Icon.sprite = Ui.Art(ServiceIcon(j.service));
                        b.Rt.anchoredPosition = new Vector2(c.x + (k - (waiting - 1) / 2f) * 150, c.y + 20 + Mathf.Sin(Time.unscaledTime * 5 + f.id) * 6);
                        k++;
                    }
                }
            for (int i = used; i < _serviceButtons.Count; i++) _serviceButtons[i].Rt.gameObject.SetActive(false);
        }

        ServiceButton ButtonAt(int i)
        {
            while (_serviceButtons.Count <= i)
            {
                var sb = new ServiceButton();
                // A painted speech bubble in the service's colour: its icon large, its word small beneath, a tail to the aircraft.
                var b = Ui.Button("Service", _world, "", new Color(0, 0, 0, 0), null, 36);
                sb.Rt = (RectTransform)b.transform; sb.Label = b.GetComponentInChildren<Text>();
                sb.Rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(150, 164));
                sb.Tail = Ui.ArtImg("Tail", sb.Rt, "tail", Color.white, 0);
                sb.Tail.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(46, 46));
                sb.Bg = Ui.ArtImg("Bubble", sb.Rt, "bubble", Color.white);
                sb.Bg.rectTransform.Fill(); sb.Bg.rectTransform.offsetMin = new Vector2(0, 18);
                sb.Icon = Ui.Img("Icon", sb.Bg.transform, Color.white); sb.Icon.preserveAspect = true;
                sb.Icon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(84, 84));
                sb.Label.transform.SetParent(sb.Bg.transform, false); sb.Label.transform.SetAsLastSibling();
                sb.Label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(140, 34));
                sb.Label.fontSize = 28; Outline(sb.Label);
                b.onClick.AddListener(() => { if (_sim.Dispatch(sb.Flight, sb.Service)) { _dirty = true; Sfx.Play("dispatch", 0.7f); } else Sfx.Play("error", 0.5f); });
                _serviceButtons.Add(sb);
            }
            _serviceButtons[i].Rt.gameObject.SetActive(true);
            return _serviceButtons[i];
        }
    }
}
