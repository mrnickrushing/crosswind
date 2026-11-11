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

        sealed class ServiceButton { public RectTransform Rt; public Image Bg; public Text Label; public int Flight; public Service Service; }
        sealed class Pop { public RectTransform Rt; public Text Text; public Vector3 World; public float Age; }

        static readonly string[] TabNames = { "BUILD", "AIRLINES", "STAFF", "RESEARCH", "EMPIRE" };
        Text _gems, _weather, _chips, _oppText;
        RectTransform _opp, _empireDot;

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
            h._sim.OpportunityAppeared += o => Sfx.Play("chime", 0.6f);
            h._sim.WeatherChanged += w => { if (w != Weather.Clear) h.Toast(w + ": the runway slows down"); };
            h._shownMoney = h._sim.State.money;
            h.OfferAway();
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

            // Top: money, income, stars.
            var top = Ui.Img("Top", _root, Ui.Panel, true).rectTransform;
            top.Band(true, -70, 190, 24);
            _money = Ui.Label("Money", top, "0", 84, Color.white, TextAnchor.MiddleLeft);
            _money.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -14), new Vector2(560, 96));
            var coin = Ui.Img("Coin", top, Ui.Gold); coin.sprite = Ui.Circle;
            coin.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(66, 66));
            Ui.Label("C", coin.transform, "$", 46, Ui.Ink).rectTransform.Fill();
            _rate = Ui.Label("Rate", top, "", 36, Ui.Green, TextAnchor.MiddleLeft, false);
            _rate.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(32, 18), new Vector2(360, 50));
            var gem = Ui.Img("Gem", top, new Color(0.45f, 0.85f, 0.95f)); gem.sprite = Ui.Circle;
            gem.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(400, 24), new Vector2(40, 40));
            _gems = Ui.Label("Gems", top, "0", 36, Color.white, TextAnchor.MiddleLeft);
            _gems.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(450, 18), new Vector2(160, 50));
            _starText = Ui.Label("Stars", top, "", 52, Ui.Gold, TextAnchor.MiddleRight);
            _starText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -22), new Vector2(380, 70));
            var bar = Ui.Img("StarBar", top, new Color(0, 0, 0, 0.45f), true).rectTransform;
            bar.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(360, 26));
            _starFill = Ui.Img("Fill", bar, Ui.Gold, true);
            _starFill.rectTransform.anchorMin = Vector2.zero; _starFill.rectTransform.anchorMax = new Vector2(0, 1); _starFill.rectTransform.offsetMin = _starFill.rectTransform.offsetMax = Vector2.zero;

            // The guide card.
            _goal = Ui.Img("Goal", _root, Ui.Panel, true).rectTransform;
            _goal.Band(true, -276, 150, 24);
            _goalText = Ui.Label("Text", _goal, "", 38, Color.white, TextAnchor.UpperLeft, false);
            _goalText.rectTransform.Fill(); _goalText.rectTransform.offsetMin = new Vector2(28, 44); _goalText.rectTransform.offsetMax = new Vector2(-250, -16);
            _goalText.resizeTextForBestFit = true; _goalText.resizeTextMinSize = 26; _goalText.resizeTextMaxSize = 38;
            var gbar = Ui.Img("Bar", _goal, new Color(0, 0, 0, 0.45f), true).rectTransform;
            gbar.anchorMin = new Vector2(0, 0); gbar.anchorMax = new Vector2(1, 0); gbar.pivot = new Vector2(0, 0); gbar.offsetMin = new Vector2(28, 16); gbar.offsetMax = new Vector2(-250, 38);
            _goalFill = Ui.Img("Fill", gbar, Ui.Green, true);
            _goalFill.rectTransform.anchorMin = Vector2.zero; _goalFill.rectTransform.anchorMax = new Vector2(0, 1); _goalFill.rectTransform.offsetMin = _goalFill.rectTransform.offsetMax = Vector2.zero;
            _goalCount = Ui.Label("Count", _goal, "", 34, Ui.Dim, TextAnchor.MiddleCenter);
            _goalCount.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(210, 100));
            _goalClaim = Ui.Button("Claim", _goal, "CLAIM", Ui.Green, () => { if (_sim.ClaimGoal()) { Toast("Reward collected"); Sfx.Play("claim"); _dirty = true; } }, 44);
            ((RectTransform)_goalClaim.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(210, 104));

            // Under the guide: the weather, and what is running (a boost, a rush).
            _weather = Ui.Label("Weather", _root, "", 34, Color.white, TextAnchor.MiddleLeft);
            _weather.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -440), new Vector2(500, 50));
            Outline(_weather);
            _chips = Ui.Label("Chips", _root, "", 34, Ui.Gold, TextAnchor.MiddleRight);
            _chips.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -440), new Vector2(620, 50));
            Outline(_chips);
            // An opportunity: a big button on the right that counts down.
            var opp = Ui.Button("Opportunity", _root, "", Ui.Gold, () => { if (_sim.TakeOpportunity()) { _dirty = true; Sfx.Play("confirm"); } }, 38);
            _opp = (RectTransform)opp.transform;
            _opp.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 240), new Vector2(330, 150));
            _oppText = opp.GetComponentInChildren<Text>(); _oppText.color = Ui.Ink;
            _opp.gameObject.SetActive(false);

            // Bottom bar and the sheet it opens.
            var bottom = Ui.Img("Bottom", _root, Ui.Panel).rectTransform;
            bottom.Band(false, 0, 190);
            bottom.GetComponent<Image>().raycastTarget = true;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int k = i;
                var b = Ui.Button(TabNames[i], bottom, TabNames[i], Ui.Card, () => OpenTab(_tab == k ? -1 : k), 34);
                var rt = (RectTransform)b.transform;
                float n = TabNames.Length;
                rt.anchorMin = new Vector2(i / n, 0); rt.anchorMax = new Vector2((i + 1) / n, 1); rt.offsetMin = new Vector2(i == 0 ? 16 : 6, 54); rt.offsetMax = new Vector2(i == n - 1 ? -16 : -6, -18);
                _tabs.Add(b);
            }
            // A dot on EMPIRE when a task or an achievement is waiting to be claimed.
            var dot = Ui.Img("Dot", _tabs[4].transform, Ui.Red); dot.sprite = Ui.Circle;
            _empireDot = dot.rectTransform; _empireDot.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(30, 30));
            _sheet = Ui.Img("Sheet", _root, Ui.Panel, true).rectTransform;
            _sheet.anchorMin = new Vector2(0, 0); _sheet.anchorMax = new Vector2(1, 0); _sheet.pivot = new Vector2(0.5f, 0);
            _sheet.offsetMin = new Vector2(16, 0); _sheet.offsetMax = new Vector2(-16, 0); _sheet.anchoredPosition = new Vector2(0, 196); _sheet.sizeDelta = new Vector2(-32, 860);
            _sheet.GetComponent<Image>().raycastTarget = true;
            _sheetTitle = Ui.Label("Title", _sheet, "", 52, Color.white, TextAnchor.MiddleLeft);
            _sheetTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -14), new Vector2(700, 80));
            var close = Ui.Button("Close", _sheet, "X", new Color(0, 0, 0, 0), () => OpenTab(-1), 56);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -8), new Vector2(100, 92));
            var view = Ui.Img("View", _sheet, new Color(0, 0, 0, 0.001f)).rectTransform;
            view.Fill(); view.offsetMin = new Vector2(16, 16); view.offsetMax = new Vector2(-16, -104);
            view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            _sheetList = Ui.Rect("List", view);
            _sheetList.anchorMin = new Vector2(0, 1); _sheetList.anchorMax = new Vector2(1, 1); _sheetList.pivot = new Vector2(0.5f, 1);
            _sheetList.offsetMin = Vector2.zero; _sheetList.offsetMax = Vector2.zero;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _sheetList; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            _sheet.gameObject.SetActive(false);

            // Toast and the star banner.
            _toast = Ui.Img("Toast", _root, new Color(0, 0, 0, 0.82f), true).rectTransform;
            _toast.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(900, 90));
            _toastText = Ui.Label("Text", _toast, "", 38, Color.white, TextAnchor.MiddleCenter, false);
            _toastText.rectTransform.Fill(12);
            _toast.gameObject.SetActive(false);
            _banner = Ui.Img("Banner", _root, new Color(0.1f, 0.13f, 0.18f, 0.95f), true).rectTransform;
            _banner.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -460), new Vector2(900, 200));
            Ui.Label("Text", _banner, "", 60, Ui.Gold).rectTransform.Fill(16);
            _banner.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            // Money counts up to its value instead of jumping.
            _shownMoney += (_sim.State.money - _shownMoney) * (1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            if (System.Math.Abs(_sim.State.money - _shownMoney) < 0.6) _shownMoney = _sim.State.money;
            _money.text = Format.Money(_shownMoney);
            _rate.text = "+" + Format.Money(_sim.State.incomeRate * 60) + " / min";
            int stars = _sim.Stars;
            _starText.text = stars + " <size=40>STAR" + (stars == 1 ? "" : "S") + "</size>";
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
            _weather.text = w == Weather.Clear ? "" : (w == Weather.Rain ? "RAIN" : w == Weather.Fog ? "FOG" : w == Weather.Storm ? "STORM" : "SNOW") + "  <size=28>runway at " + Mathf.RoundToInt(_sim.WeatherFactor * 100) + "%</size>";
            _chips.text = (_sim.Rush ? "RUSH x2  " + Format.Time(_sim.State.rushUntil - _sim.State.time) + "   " : "") + (_sim.Boosted ? "BOOST x2  " + Format.Time(_sim.BoostLeft) : "");
            var o = _sim.State.opportunity;
            _opp.gameObject.SetActive(o != null && _tab < 0);
            if (o != null)
            {
                _oppText.text = (o.kind == "charter" ? "CHARTER\n<size=44>+" + Format.Money(o.value) + "</size>" : o.kind == "rush" ? "RUSH HOUR\n<size=34>double pay</size>" : "LUCKY FIND\n<size=34>+" + (int)o.value + " gems</size>")
                    + "\n<size=26>" + Mathf.CeilToInt((float)(o.expiresAt - _sim.State.time)) + "s</size>";
                _opp.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6) * 0.03f);
            }
            _empireDot.gameObject.SetActive(_sim.AchievementsWaiting > 0 || _sim.State.tasks.Exists(x => !x.claimed && x.progress >= x.target));
            UpdateServiceButtons();
            UpdatePops();
            if ((_refreshIn -= Time.unscaledDeltaTime) <= 0 || _dirty) { _refreshIn = 0.5f; if (_tab >= 0) RefreshSheet(); _dirty = false; }
            if (_toastT > 0 && (_toastT -= Time.unscaledDeltaTime) <= 0) _toast.gameObject.SetActive(false);
            if (_bannerT > 0 && (_bannerT -= Time.unscaledDeltaTime) <= 0) _banner.gameObject.SetActive(false);
        }

        static void Outline(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.75f); o.effectDistance = new Vector2(2, -2); }

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toastText.text = text;
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            _toastT = 2.2f;
        }

        void OnStar(int stars)
        {
            _banner.GetComponentInChildren<Text>().text = stars + "-STAR AIRPORT!\n<size=38><color=#FFFFFFB0>New airlines and buildings unlocked</color></size>";
            _banner.gameObject.SetActive(true);
            _banner.SetAsLastSibling();
            Sfx.Play("star");
            _bannerT = 3.5f;
            _dirty = true;
        }

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

        static Color ServiceColor(Service s) { return s == Service.Fuel ? Palette.Fuel : s == Service.Baggage ? Palette.Baggage : s == Service.Catering ? Palette.Catering : Palette.Cleaning; }
        static string ServiceWord(Service s) { return s == Service.Fuel ? "FUEL" : s == Service.Baggage ? "BAGS" : s == Service.Catering ? "FOOD" : "CLEAN"; }

        void UpdateServiceButtons()
        {
            int used = 0;
            if (AirportView.Instance != null)
                foreach (var plane in AirportView.Instance.Planes)
                {
                    var f = plane.Flight;
                    if (f.phase != Phase.Service) continue;
                    int k = 0, waiting = f.jobs.Count(j => !j.dispatched && !j.done);
                    foreach (var j in f.jobs)
                    {
                        if (j.dispatched || j.done) continue;
                        var c = ToCanvas(plane.Head);
                        if (c.z < 0) { k++; continue; }
                        var b = ButtonAt(used++);
                        b.Flight = f.id; b.Service = j.service;
                        b.Bg.color = _sim.CrewFree(j.service) ? ServiceColor(j.service) : Ui.Off;
                        b.Label.text = ServiceWord(j.service);
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
                var b = Ui.Button("Service", _world, "", Color.white, null, 36);
                sb.Rt = (RectTransform)b.transform; sb.Bg = b.GetComponent<Image>(); sb.Label = b.GetComponentInChildren<Text>();
                sb.Rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 96));
                b.onClick.AddListener(() => { if (_sim.Dispatch(sb.Flight, sb.Service)) { _dirty = true; Sfx.Play("dispatch", 0.7f); } else Sfx.Play("error", 0.5f); });
                _serviceButtons.Add(sb);
            }
            _serviceButtons[i].Rt.gameObject.SetActive(true);
            return _serviceButtons[i];
        }

        // ------------------------------------------------------------------ pay pop-ups

        void OnPaid(FlightState f, double paid, bool onTime)
        {
            if (GameRoot.Speed <= 3) Sfx.Play("coin", 0.35f);
            var t = Ui.Label("Pay", _world, "+" + Format.Money(paid) + (onTime ? "" : "  <size=30>LATE</size>"), 46, onTime ? Ui.Gold : Ui.Red);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60));
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(2, -2);
            _pops.Add(new Pop { Rt = t.rectTransform, Text = t, World = new Vector3(Layout.RunwayX, 6, Layout.RunwayHalf(_sim) * 0.5f) });
        }

        void UpdatePops()
        {
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.Age += Time.unscaledDeltaTime;
                var c = ToCanvas(p.World);
                p.Rt.anchoredPosition = new Vector2(c.x, c.y + p.Age * 90);
                p.Text.color = new Color(p.Text.color.r, p.Text.color.g, p.Text.color.b, Mathf.Clamp01(1.6f - p.Age));
                if (p.Age > 1.6f) { Destroy(p.Rt.gameObject); _pops.RemoveAt(i); }
            }
        }
    }
}
