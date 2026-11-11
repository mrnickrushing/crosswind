using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The operations centre (OPS on the rail): the whole screen beside the rail, in six pages. CONTRACTS is the
    /// market and the book; PLANNER the day laid out by stand and hour, where a contract is picked up and set down;
    /// RECORD every contract's three marks; AIRLINES what each airline thinks of the airport and a switch to keep it
    /// away; FLIGHTS everything in the pattern and due today; FINANCES the day's books and the last week's.
    /// </summary>
    public sealed partial class Hud
    {
        const int OpsTab = 6;
        static readonly string[] OpsPages = { "CONTRACTS", "PLANNER", "RECORD", "AIRLINES", "FLIGHTS", "FINANCES" };
        /// <summary>The colour of each part of the day (planner slots: all day, morning, afternoon, evening, night).</summary>
        public static readonly Color[] PartColor = { Palette.Hex(0x52647C), Palette.Hex(0x2FAE98), Palette.Hex(0xD9A92A), Palette.Hex(0xD9567A), Palette.Hex(0x5557B0) };

        RectTransform _ops, _opsScroll, _opsList, _opsFixed, _opsDot;
        Text _opsCash;
        readonly List<Button> _opsTabs = new List<Button>();
        int _opsPage, _opsPick;             // the page shown, and the contract picked up in the planner (0: none)
        int _dropArmed; float _dropArmedAt;
        double _marketSeen;                 // the market the player last looked at (its change time)
        RectTransform _list;                // where Row and Header put their rows (null: the drawer)

        void BuildOps()
        {
            _ops = Ui.ArtImg("Ops", _safe, "panel", Color.white).rectTransform;
            _ops.anchorMin = new Vector2(0, 0); _ops.anchorMax = new Vector2(1, 1);
            _ops.offsetMin = new Vector2(152, 12); _ops.offsetMax = new Vector2(-12, -12);
            _ops.GetComponent<Image>().raycastTarget = true;
            Ui.Img("Back", _ops, Ui.Ink, true).rectTransform.Fill(5);
            Ui.Label("Title", _ops, "OPERATIONS", 44, Color.white, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -12), new Vector2(300, 70));
            var rule = Ui.Img("Rule", _ops, new Color(0.25f, 0.76f, 1f, 0.9f)).rectTransform; rule.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -80), new Vector2(90, 4));
            var strip = Ui.Rect("Tabs", _ops);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1); strip.pivot = new Vector2(0.5f, 1);
            strip.offsetMin = new Vector2(330, -84); strip.offsetMax = new Vector2(-380, -12);
            for (int i = 0; i < OpsPages.Length; i++)
            {
                int k = i;
                var b = Ui.Button(OpsPages[i], strip, OpsPages[i], Ui.Card, () => { _opsPage = k; _opsPick = 0; _opsList.anchoredPosition = Vector2.zero; Sfx.Play("open", 0.4f); _dirty = true; }, 30);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / (float)OpsPages.Length, 0); rt.anchorMax = new Vector2((i + 1) / (float)OpsPages.Length, 1); rt.offsetMin = new Vector2(4, 0); rt.offsetMax = new Vector2(-4, 0);
                var l = b.GetComponentInChildren<Text>(); l.resizeTextForBestFit = true; l.resizeTextMinSize = 16; l.resizeTextMaxSize = 30;
                _opsTabs.Add(b);
            }
            var coin = Ui.ArtImg("Coin", _ops, "coin", Color.white, 0);
            coin.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-316, -24), new Vector2(48, 48));
            _opsCash = Ui.Label("Cash", _ops, "", 36, Color.white, TextAnchor.MiddleLeft);
            _opsCash.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -18), new Vector2(210, 60));
            _opsCash.resizeTextForBestFit = true; _opsCash.resizeTextMinSize = 20; _opsCash.resizeTextMaxSize = 36;
            var close = Ui.Button("Close", _ops, "X", new Color(0, 0, 0, 0), () => OpenTab(-1), 46);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(90, 80));

            var view = Ui.Img("View", _ops, new Color(0, 0, 0, 0.001f)).rectTransform;
            view.Fill(); view.offsetMin = new Vector2(16, 16); view.offsetMax = new Vector2(-16, -98);
            view.GetComponent<Image>().raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            _opsScroll = view;
            _opsList = Ui.Rect("List", view);
            _opsList.anchorMin = new Vector2(0, 1); _opsList.anchorMax = new Vector2(1, 1); _opsList.pivot = new Vector2(0.5f, 1);
            _opsList.offsetMin = Vector2.zero; _opsList.offsetMax = Vector2.zero;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _opsList; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            _opsFixed = Ui.Rect("Fixed", _ops);
            _opsFixed.Fill(); _opsFixed.offsetMin = new Vector2(16, 16); _opsFixed.offsetMax = new Vector2(-16, -98);
            _ops.gameObject.SetActive(false);
        }

        /// <summary>Opens the operations centre on a page (0 contracts .. 5 finances).</summary>
        public void OpenOps(int page)
        {
            _opsPage = Mathf.Clamp(page, 0, OpsPages.Length - 1);
            OpenTab(OpsTab);
        }

        /// <summary>Something in OPS wants the player: a contract with no place in the day, or offers not yet looked at.</summary>
        bool OpsWaiting { get { return _sim.ContractsOpen && (_sim.State.bookings.Exists(b => b.hour < 0) || (_sim.State.market.Count > 0 && _sim.State.marketAt != _marketSeen && _sim.State.bookings.Count < _sim.BookingCap)); } }

        void RefreshOps()
        {
            for (int i = 0; i < _opsTabs.Count; i++) _opsTabs[i].GetComponent<Image>().color = i == _opsPage ? Ui.Blue : Ui.Card;
            _opsCash.text = Format.Money(_sim.State.money);
            Ui.Clear(_opsList); Ui.Clear(_opsFixed);
            bool grid = _opsPage == 1 && _sim.ContractsOpen;
            _opsScroll.gameObject.SetActive(!grid); _opsFixed.gameObject.SetActive(grid);
            _list = _opsList; _rowY = 0;
            if (_opsPage <= 2 && !_sim.ContractsOpen)
            {
                Header("OPENS AT " + Sim.ContractStars + " STARS");
                Ico("handshake", TintGold).Entry("Contracts", "AT " + Sim.ContractStars + " STARS",
                    "Beside their routes, airlines put charters and series of flights on the market. Sign them, plan each onto a stand and an hour, and fly them well: every contract is marked, and good marks build your reputation with that airline.", null, null, null);
            }
            else switch (_opsPage)
            {
                case 0: ContractsPage(); break;
                case 1: PlannerPage(); break;
                case 2: RecordPage(); break;
                case 3: AirlinesPage(); break;
                case 4: FlightsPage(); break;
                default: FinancesPage(); break;
            }
            _opsList.sizeDelta = new Vector2(0, _rowY);
            _list = null;
        }

        // ------------------------------------------------------------------ table pieces

        /// <summary>A cell of a row: from one share of its width to another.</summary>
        static RectTransform Span(Transform row, float x0, float x1, float padX = 8, float padY = 8)
        {
            var rt = Ui.Rect("Cell", row);
            rt.anchorMin = new Vector2(x0, 0); rt.anchorMax = new Vector2(x1, 1); rt.offsetMin = new Vector2(padX, padY); rt.offsetMax = new Vector2(-padX, -padY);
            return rt;
        }

        static Text CellText(Transform row, float x0, float x1, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = true)
        {
            var l = Ui.Label("T", Span(row, x0, x1), text, size, color, anchor, bold);
            l.rectTransform.Fill();
            l.resizeTextForBestFit = true; l.resizeTextMinSize = Mathf.Min(16, size); l.resizeTextMaxSize = size; l.verticalOverflow = VerticalWrapMode.Truncate;
            return l;
        }

        /// <summary>The line of column names over a table.</summary>
        void TableHead(params object[] cols)
        {
            var list = _list ?? _sheetList;
            var head = Ui.Img("Head", list, new Color(0, 0, 0, 0.35f), true).rectTransform;
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = Vector2.zero; head.offsetMax = Vector2.zero; head.anchoredPosition = new Vector2(0, -_rowY); head.sizeDelta = new Vector2(0, 46);
            for (int i = 0; i + 2 < cols.Length; i += 3)
                CellText(head, (float)cols[i + 1], (float)cols[i + 2], (string)cols[i], 24, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleCenter);
            _rowY += 52;
        }

        /// <summary>An airline's plate: its colour with its name on it.</summary>
        static void Plate(Transform row, float x0, float x1, string airline)
        {
            var c = airline == "own" ? Palette.Hex(Sim.LiveryHex[Mathf.Clamp(GameRoot.Sim.State.livery, 0, Sim.LiveryHex.Length - 1)]) : Models.AirlineColor(airline);
            var cell = Span(row, x0, x1, 10, 12);
            var bg = Ui.Img("Plate", cell, c, true); bg.rectTransform.Fill();
            bool pale = c.r * 0.3f + c.g * 0.6f + c.b * 0.1f > 0.62f;
            var l = Ui.Label("Name", bg.transform, Content.Airlines[airline].Name.ToUpper(), 30, pale ? Ui.Ink : Color.white);
            l.rectTransform.Fill(10); l.resizeTextForBestFit = true; l.resizeTextMinSize = 14; l.resizeTextMaxSize = 30; l.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>The part of the day as a coloured chip.</summary>
        static void PartChip(Transform row, float x0, float x1, int part, bool hours = true)
        {
            var cell = Span(row, x0, x1, 6, 16);
            var bg = Ui.Img("Part", cell, PartColor[part], true); bg.rectTransform.Fill();
            var l = Ui.Label("L", bg.transform, Sim.SlotName[part] + (hours ? "\n" + Sim.SlotHours[part] : ""), 26, Color.white);
            l.rectTransform.Fill(4); l.resizeTextForBestFit = true; l.resizeTextMinSize = 12; l.resizeTextMaxSize = 26; l.verticalOverflow = VerticalWrapMode.Truncate; l.lineSpacing = 0.85f;
        }

        /// <summary>The services a flight needs as a line of little tiles; one the airport can't give is greyed with a red edge.</summary>
        void NeedTiles(Transform row, float x0, float x1, IEnumerable<Service> needs)
        {
            var cell = Span(row, x0, x1, 4, 4);
            var list = needs.ToList(); const float size = 50, gap = 6;
            float start = -((list.Count - 1) * (size + gap)) / 2f;
            for (int i = 0; i < list.Count; i++)
            {
                bool have = _sim.Crews(list[i]) > 0;
                if (!have) { var edge = Ui.Img("Edge", cell, Ui.Red, true); edge.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(start + i * (size + gap), 0), new Vector2(size + 6, size + 6)); }
                var tile = Ui.Img("Need", cell, have ? ServiceColor(list[i]) : Ui.Off, true);
                tile.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(start + i * (size + gap), 0), new Vector2(size, size));
                var ic = Ui.Img("Ic", tile.transform, Color.white); ic.sprite = Ui.Art(ServiceIcon(list[i])); ic.preserveAspect = true;
                ic.rectTransform.Fill(5);
            }
        }

        /// <summary>A grade in a box (the lowest an airline accepts); "ANY" when it has no demand.</summary>
        static void GradeBox(Transform row, float x0, float x1, int grade)
        {
            var cell = Span(row, x0, x1, 4, 4);
            var box = Ui.Img("Box", cell, grade >= 4 ? new Color(1, 1, 1, 0.12f) : grade == 0 ? Ui.Gold : grade == 1 ? Ui.Green : Ui.Blue, true);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(grade >= 4 ? 84 : 58, 58));
            Ui.Label("G", box.transform, grade >= 4 ? "ANY" : Sim.Grades[grade], grade >= 4 ? 24 : 38, grade >= 4 ? Ui.Dim : Ui.Ink).rectTransform.Fill();
        }

        /// <summary>A bar that fills from the left.</summary>
        static void Meter(Transform row, float x0, float x1, float value, Color tint, float height = 14, float y = 0)
        {
            var cell = Span(row, x0, x1, 10, 0);
            var track = Ui.Img("Track", cell, new Color(0, 0, 0, 0.5f), true).rectTransform;
            track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f); track.offsetMin = new Vector2(0, y - height / 2); track.offsetMax = new Vector2(0, y + height / 2);
            var fill = Ui.Img("Fill", track, tint, true).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp(value, 0.03f, 1f), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        /// <summary>A contract mark: made (tick), lost (cross) or not yet known (a dash), with the count under it.</summary>
        static void Mark(Transform row, float x0, float x1, int state, string count)
        {
            var cell = Span(row, x0, x1, 4, 4);
            var box = Ui.Img("Mark", cell, state > 0 ? Ui.Green : state < 0 ? Ui.Red : new Color(1, 1, 1, 0.1f), true);
            box.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, count != null ? 10 : 0), new Vector2(52, 52));
            if (state > 0) { var tick = Ui.Img("Tick", box.transform, Color.white); tick.sprite = Ui.Icon("checkmark"); tick.preserveAspect = true; tick.rectTransform.Fill(9); }
            else Ui.Label("X", box.transform, state < 0 ? "X" : "-", 34, state < 0 ? Color.white : Ui.Dim).rectTransform.Fill();
            if (count != null) Ui.Label("N", cell, count, 22, Ui.Dim, TextAnchor.LowerCenter, false).rectTransform.Fill();
        }

        static string Hh(int hour) { return hour.ToString("00") + ":00"; }

        // ------------------------------------------------------------------ CONTRACTS

        void ContractsPage()
        {
            var s = _sim;
            _marketSeen = s.State.marketAt;
            // The market's clock, the book, and what the airport charges.
            var top = Row(104);
            CellText(top, 0.01f, 0.26f, "NEW OFFERS IN\n<color=#FFFFFF>" + Format.Time(s.MarketIn) + "</color>", 30, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleLeft);
            var rush = Ui.Button("Rush", top, null, s.State.gems >= s.MarketGems ? Ui.Blue : Locked, () =>
            {
                if (s.RushMarket()) { Sfx.Play("confirm"); Toast("New offers on the market"); } else { Sfx.Play("error", 0.5f); Toast("Not enough gems"); }
                _dirty = true;
            }, 30);
            var rr = (RectTransform)rush.transform; rr.anchorMin = new Vector2(0.26f, 0); rr.anchorMax = new Vector2(0.42f, 1); rr.offsetMin = new Vector2(6, 12); rr.offsetMax = new Vector2(-6, -12);
            ButtonFace(rush.transform, "NEW OFFERS NOW\n" + Gems(s.MarketGems), null);
            CellText(top, 0.44f, 0.66f, "CONTRACT BOOK\n<color=#FFFFFF>" + s.State.bookings.Count + " of " + s.BookingCap + "</color>  <size=22>(the tower keeps it)</size>", 30, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleLeft);
            CellText(top, 0.66f, 0.99f, "FEE POLICY\n<color=#FFFFFF>" + Sim.FeeName[s.FeeLevel] + "</color>  <size=22>" + Mathf.RoundToInt(s.FeePay * 100) + "% a flight (change it in EMPIRE)</size>", 30, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleLeft);

            Header("ON THE MARKET");
            TableHead("AIRLINE", 0f, 0.17f, "FLIGHT", 0.17f, 0.33f, "AIRCRAFT", 0.33f, 0.46f, "WHEN", 0.46f, 0.57f, "NEEDS", 0.57f, 0.72f, "STANDARD", 0.72f, 0.79f, "REWARD", 0.79f, 0.89f);
            if (s.State.market.Count == 0)
                Ico("clock").Entry("Nothing on offer", null, "Every offer has been taken. The airlines post new ones when the clock above runs out.", null, null, null);
            foreach (var b in s.State.market.ToList())
            {
                int id = b.id;
                var plane = Content.Aircraft[(AircraftClass)b.plane];
                var row = Row(112);
                Plate(row, 0f, 0.17f, b.airline);
                CellText(row, 0.17f, 0.33f, s.FlightNo(b) + "  <size=24><color=#FFFFFF90>" + (b.kind >= Sim.Freight ? "<color=#FFC83D>" + Sim.KindName(b) + "</color>" : Sim.KindName(b)) + (b.flights > 1 ? " · " + b.flights + " flights" : "") + "</color></size>\n<size=24><color=#FFFFFF90>" + Content.Places[b.place % Content.Places.Length] + "</color></size>", 34, Color.white, TextAnchor.MiddleLeft);
                CellText(row, 0.33f, 0.46f, plane.Name + "\n<size=24><color=#FFFFFF90>" + plane.Seats + " seats</color></size>", 30, Color.white);
                PartChip(row, 0.46f, 0.57f, b.part);
                NeedTiles(row, 0.57f, 0.72f, Content.NeedsOf(b.needs));
                GradeBox(row, 0.72f, 0.79f, b.grade);
                CellText(row, 0.79f, 0.89f, "<color=#FFC83D>" + Format.Money(b.reward) + "</color>\n<size=24>+" + b.rep + " reputation</size>", 34, Color.white);
                string blocked = s.SignContractBlocked(id);
                var sign = Ui.Button("Sign", row, null, blocked == null ? Ui.Green : Locked, () =>
                {
                    if (s.SignContract(id)) { Sfx.Play("confirm"); var nb = s.Booking(id); Toast(nb != null && nb.hour >= 0 ? s.FlightNo(nb) + " signed: stand " + (nb.stand + 1) + " at " + Hh(nb.hour) + " every day" : "Signed"); }
                    else { Toast(blocked ?? "Can't sign that"); Sfx.Play("error", 0.5f); }
                    _dirty = true;
                }, 34);
                var sr = (RectTransform)sign.transform; sr.anchorMin = new Vector2(0.89f, 0); sr.anchorMax = new Vector2(1, 1); sr.offsetMin = new Vector2(4, 10); sr.offsetMax = new Vector2(-12, -10);
                ButtonFace(sign.transform, "SIGN", blocked);
            }

            Header("IN YOUR BOOK  ·  ONE FLIGHT A DAY EACH");
            if (s.State.bookings.Count == 0)
                Ico("handshake", TintGold).Entry("No contracts yet", null, "Sign an offer above. It is planned onto a free stand at a quiet hour for you; move it in the PLANNER. When its last flight has left it is marked in the RECORD.", null, null, null);
            else TableHead("AIRLINE", 0f, 0.17f, "FLIGHT", 0.17f, 0.33f, "AIRCRAFT", 0.33f, 0.46f, "PLANNED", 0.46f, 0.6f, "FLOWN", 0.6f, 0.74f, "PAYS", 0.74f, 0.84f);
            foreach (var b in s.State.bookings.ToList())
            {
                int id = b.id;
                var plane = Content.Aircraft[(AircraftClass)b.plane];
                var row = Row(104);
                Plate(row, 0f, 0.17f, b.airline);
                CellText(row, 0.17f, 0.33f, s.FlightNo(b) + "  <size=24><color=#FFFFFF90>" + Sim.KindName(b) + "</color></size>\n<size=24><color=#FFFFFF90>" + Content.Places[b.place % Content.Places.Length] + "</color></size>", 34, Color.white, TextAnchor.MiddleLeft);
                CellText(row, 0.33f, 0.46f, plane.Name, 30, Color.white);
                if (b.hour >= 0) CellText(row, 0.46f, 0.6f, "STAND " + (b.stand + 1) + "  ·  " + Hh(b.hour) + "\n<size=22><color=#FFFFFF90>" + Sim.SlotName[b.part].ToLower() + (b.airborne ? ", here now" : b.lastDay == s.FuelDay ? ", flown today" : "") + "</color></size>", 30, Color.white);
                else CellText(row, 0.46f, 0.6f, "NOT PLANNED", 30, Ui.Red);
                CellText(row, 0.6f, 0.74f, b.flown + " of " + b.flights, 28, Color.white, TextAnchor.UpperCenter);
                Meter(row, 0.6f, 0.74f, b.flown / (float)b.flights, Ui.Green, 14, -16);
                CellText(row, 0.74f, 0.84f, "<color=#FFC83D>" + Format.Money(b.reward) + "</color>\n<size=22>+" + b.rep + " rep at full marks</size>", 30, Color.white);
                var plan = Ui.Button("Plan", row, "PLAN", b.hour < 0 ? Ui.Gold : Ui.Blue, () => { _opsPage = 1; _opsPick = id; _dirty = true; Sfx.Play("open", 0.4f); }, 30);
                var pr = (RectTransform)plan.transform; pr.anchorMin = new Vector2(0.84f, 0); pr.anchorMax = new Vector2(0.92f, 1); pr.offsetMin = new Vector2(4, 14); pr.offsetMax = new Vector2(-4, -14);
                bool armed = _dropArmed == id && Time.unscaledTime - _dropArmedAt < 4;
                var drop = Ui.Button("Drop", row, armed ? "SURE?" : "DROP", armed ? Ui.Red : Locked, () =>
                {
                    if (_dropArmed == id && Time.unscaledTime - _dropArmedAt < 4) { s.DropContract(id); _dropArmed = 0; Sfx.Play("close", 0.6f); }
                    else { _dropArmed = id; _dropArmedAt = Time.unscaledTime; Toast("Dropping it costs " + Mathf.Max(1, b.rep / 4) + " reputation with " + Content.Airlines[b.airline].Name + ". Tap again to drop"); }
                    _dirty = true;
                }, 28);
                var dr = (RectTransform)drop.transform; dr.anchorMin = new Vector2(0.92f, 0); dr.anchorMax = new Vector2(1, 1); dr.offsetMin = new Vector2(4, 14); dr.offsetMax = new Vector2(-12, -14);
            }
        }

        // ------------------------------------------------------------------ PLANNER

        /// <summary>The column of the planner an hour of the clock falls in: the airport day runs 06:00 to 06:00.</summary>
        static int Col(int hour) { return (hour - 6 + 24) % 24; }

        void PlannerPage()
        {
            var s = _sim;
            var root = _opsFixed;
            const float LabelW = 150, TopH = 70, BandH = 44, HourH = 34, ChartH = 150;
            var picked = _opsPick != 0 ? s.Booking(_opsPick) : null;
            if (picked == null) _opsPick = 0;

            // The strip over the grid: the day, what is picked up, what has no place yet, and AUTO PLAN.
            int wd = s.Weekday;
            string day = Sim.Weekdays[wd] + "  ·  " + (Sim.Weekend(wd) ? "holiday airlines fly more today, business airlines less" : "business airlines fly more today, holiday airlines less");
            string say = picked != null
                ? "<color=#FFC83D>" + s.FlightNo(picked) + "</color>  " + Content.Aircraft[(AircraftClass)picked.plane].Name + "  ·  " + Sim.SlotName[picked.part].ToLower() + " " + Sim.SlotHours[picked.part] + "  ·  tap a green hour to set it down"
                : s.State.bookings.Count == 0 ? "No contracts to plan yet: sign one in CONTRACTS" : "Tap a contract to pick it up, then a green hour on a stand to set it down";
            var info = Ui.Label("Info", root, day + "\n<size=26><color=#FFFFFFB0>" + say + "</color></size>", 30, new Color(0.45f, 0.8f, 1f), TextAnchor.UpperLeft);
            info.rectTransform.anchorMin = new Vector2(0, 1); info.rectTransform.anchorMax = new Vector2(1, 1); info.rectTransform.pivot = new Vector2(0, 1);
            info.rectTransform.offsetMin = new Vector2(8, -TopH); info.rectTransform.offsetMax = new Vector2(-520, 0);
            info.resizeTextForBestFit = true; info.resizeTextMinSize = 18; info.resizeTextMaxSize = 30; info.verticalOverflow = VerticalWrapMode.Truncate;
            var auto = Ui.Button("Auto", root, "AUTO PLAN", Ui.Green, () =>
            {
                int n = s.AutoPlanContracts();
                Toast(s.State.bookings.Count == 0 ? "Nothing to plan" : n == s.State.bookings.Count ? "Every contract has a stand and a quiet hour" : n + " of " + s.State.bookings.Count + " planned: the rest need another stand");
                _opsPick = 0; Sfx.Play("build", 0.7f); _dirty = true;
            }, 32);
            ((RectTransform)auto.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -2), new Vector2(240, 62));
            // Contracts with no place in the day wait here.
            var loose = s.State.bookings.Where(b => b.hour < 0).ToList();
            for (int i = 0; i < loose.Count && i < 2; i++)
            {
                var b = loose[i]; int id = b.id;
                var chip = Ui.Button("Loose", root, s.FlightNo(b) + " ?", _opsPick == id ? Ui.Gold : Ui.Red, () => { _opsPick = _opsPick == id ? 0 : id; _dirty = true; Sfx.Play("click", 0.5f); }, 28);
                ((RectTransform)chip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-254 - i * 128, -2), new Vector2(122, 62));
            }

            // The grid: stands down, the day's 24 hours across from 06:00.
            var grid = Ui.Rect("Grid", root);
            grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
            grid.offsetMin = new Vector2(LabelW, ChartH + 8); grid.offsetMax = new Vector2(0, -(TopH + BandH + HourH + 6));
            var heads = Ui.Rect("Heads", root);
            heads.anchorMin = new Vector2(0, 1); heads.anchorMax = new Vector2(1, 1); heads.pivot = new Vector2(0.5f, 1);
            heads.offsetMin = new Vector2(LabelW, -(TopH + BandH + HourH + 4)); heads.offsetMax = new Vector2(0, -(TopH + 2));
            int[] order = { 1, 2, 3, 4 };
            for (int p = 0; p < 4; p++)
            {
                var band = Ui.Img("Band", heads, PartColor[order[p]], true).rectTransform;
                band.anchorMin = new Vector2(p / 4f, 1); band.anchorMax = new Vector2((p + 1) / 4f, 1); band.pivot = new Vector2(0.5f, 1);
                band.offsetMin = new Vector2(2, -BandH); band.offsetMax = new Vector2(-2, 0);
                Ui.Label("L", band, Sim.SlotName[order[p]], 28, Color.white).rectTransform.Fill();
            }
            for (int k = 0; k < 24; k++)
            {
                var t = Ui.Label("H", heads, ((k + 6) % 24).ToString("00"), 24, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform;
                t.anchorMin = new Vector2(k / 24f, 0); t.anchorMax = new Vector2((k + 1) / 24f, 0); t.pivot = new Vector2(0.5f, 0); t.offsetMin = new Vector2(0, 0); t.offsetMax = new Vector2(0, HourH);
            }
            int stands = Mathf.Max(1, s.StandCount);
            int today = s.FuelDay;
            for (int st = 0; st < stands; st++)
            {
                float y0 = 1 - (st + 1) / (float)stands, y1 = 1 - st / (float)stands;
                // The stand's name at the left.
                var name = Ui.ArtImg("Stand", root, "card", Color.white).rectTransform;
                name.anchorMin = new Vector2(0, 0); name.anchorMax = new Vector2(0, 1);
                // Its rectangle follows the grid's row: same vertical offsets, through a holder.
                name.SetParent(grid, false);
                name.anchorMin = new Vector2(0, y0); name.anchorMax = new Vector2(0, y1); name.pivot = new Vector2(1, 0.5f);
                name.offsetMin = new Vector2(-LabelW, 3); name.offsetMax = new Vector2(-6, -3);
                var nl = Ui.Label("N", name, "STAND " + (st + 1) + (s.HeavyStand(st) ? "\n<size=20><color=#FFC83D>WIDE-BODY</color></size>" : ""), 30, Color.white);
                nl.rectTransform.Fill(4); nl.resizeTextForBestFit = true; nl.resizeTextMinSize = 14; nl.resizeTextMaxSize = 30;
                var lane = Ui.Img("Lane", grid, new Color(0, 0, 0, 0.32f), true).rectTransform;
                lane.anchorMin = new Vector2(0, y0); lane.anchorMax = new Vector2(1, y1); lane.offsetMin = new Vector2(0, 3); lane.offsetMax = new Vector2(0, -3);
                // A wash of each part's colour, and a line at every hour.
                for (int p = 0; p < 4; p++)
                {
                    var wash = Ui.Img("Wash", lane, new Color(PartColor[order[p]].r, PartColor[order[p]].g, PartColor[order[p]].b, 0.13f)).rectTransform;
                    wash.anchorMin = new Vector2(p / 4f, 0); wash.anchorMax = new Vector2((p + 1) / 4f, 1); wash.offsetMin = new Vector2(2, 2); wash.offsetMax = new Vector2(-2, -2);
                }
                for (int k = 1; k < 24; k++)
                {
                    var tick = Ui.Img("Tick", lane, new Color(1, 1, 1, k % 6 == 0 ? 0.22f : 0.07f)).rectTransform;
                    tick.anchorMin = new Vector2(k / 24f, 0); tick.anchorMax = new Vector2(k / 24f, 1); tick.offsetMin = new Vector2(-1, 4); tick.offsetMax = new Vector2(1, -4);
                }
                // Where the picked contract may go on this stand.
                if (picked != null)
                    for (int hour = Sim.PartStart(picked.part); hour <= Sim.LastHour(picked); hour++)
                    {
                        if (s.PlanBlocked(picked.id, st, hour) != null) continue;
                        int hh = hour, stand = st, id = picked.id;
                        var free = Ui.Button("Free", lane, null, new Color(0.25f, 0.85f, 0.45f, 0.4f), () =>
                        {
                            if (s.Plan(id, stand, hh)) { Toast(s.FlightNo(s.Booking(id)) + ": stand " + (stand + 1) + " at " + Hh(hh)); Sfx.Play("build", 0.6f); }
                            _opsPick = 0; _dirty = true;
                        }, 20);
                        var fr = (RectTransform)free.transform;
                        fr.anchorMin = new Vector2(Col(hour) / 24f, 0); fr.anchorMax = new Vector2((Col(hour) + 1) / 24f, 1); fr.offsetMin = new Vector2(3, 6); fr.offsetMax = new Vector2(-3, -6);
                    }
                // The contracts planned here.
                foreach (var b in s.State.bookings)
                {
                    if (b.stand != st || b.hour < 0) continue;
                    int id = b.id; int w = Sim.BlockHours((AircraftClass)b.plane);
                    var c = Models.AirlineColor(b.airline);
                    bool flown = b.lastDay == today && !b.airborne, on = _opsPick == id;
                    if (on) { var glow = Ui.Img("Sel", lane, Ui.Gold, true).rectTransform; glow.anchorMin = new Vector2(Col(b.hour) / 24f, 0); glow.anchorMax = new Vector2((Col(b.hour) + w) / 24f, 1); glow.offsetMin = new Vector2(0, 2); glow.offsetMax = new Vector2(0, -2); }
                    var block = Ui.Button("Block", lane, null, new Color(c.r, c.g, c.b, flown ? 0.55f : 1f), () => { _opsPick = _opsPick == id ? 0 : id; _dirty = true; Sfx.Play("click", 0.5f); }, 20);
                    var br = (RectTransform)block.transform;
                    br.anchorMin = new Vector2(Col(b.hour) / 24f, 0); br.anchorMax = new Vector2((Col(b.hour) + w) / 24f, 1); br.offsetMin = new Vector2(4, 6); br.offsetMax = new Vector2(-4, -6);
                    bool pale = c.r * 0.3f + c.g * 0.6f + c.b * 0.1f > 0.62f;
                    var l = Ui.Label("L", block.transform, (w > 1 ? s.FlightNo(b) : Content.Airlines[b.airline].Code + "\n" + b.number) + "\n<size=20>" + (b.airborne ? "HERE" : b.flown + "/" + b.flights) + "</size>", 26, pale ? Ui.Ink : Color.white);
                    l.rectTransform.Fill(3); l.resizeTextForBestFit = true; l.resizeTextMinSize = 12; l.resizeTextMaxSize = 26; l.verticalOverflow = VerticalWrapMode.Truncate; l.lineSpacing = 0.85f;
                }
            }
            // Now.
            float nowAt = (float)(s.State.time % Sim.DayLength) / Sim.DayLength;
            var now = Ui.Img("Now", grid, Ui.Gold).rectTransform;
            now.anchorMin = new Vector2(nowAt, 0); now.anchorMax = new Vector2(nowAt, 1); now.offsetMin = new Vector2(-2, -4); now.offsetMax = new Vector2(2, 4);

            // Under the grid: how busy the routes make each hour, against what the airport can turn round.
            var chart = Ui.Rect("Chart", root);
            chart.anchorMin = new Vector2(0, 0); chart.anchorMax = new Vector2(1, 0); chart.pivot = new Vector2(0.5f, 0);
            chart.offsetMin = new Vector2(LabelW, 4); chart.offsetMax = new Vector2(0, ChartH);
            var load = s.HourlyTraffic(); float cap = s.HourlyCapacity();
            float topLoad = Mathf.Max(cap * 1.15f, load.Max() * 1.08f, 1);
            var cl = Ui.Label("L", root, "FLIGHTS\nAN HOUR\n<size=20><color=#FFFFFF90>line: what you\ncan handle</color></size>", 26, new Color(0.45f, 0.8f, 1f), TextAnchor.MiddleLeft);
            cl.rectTransform.anchorMin = new Vector2(0, 0); cl.rectTransform.anchorMax = new Vector2(0, 0); cl.rectTransform.pivot = new Vector2(0, 0);
            cl.rectTransform.anchoredPosition = new Vector2(8, 4); cl.rectTransform.sizeDelta = new Vector2(LabelW - 12, ChartH - 4);
            cl.resizeTextForBestFit = true; cl.resizeTextMinSize = 14; cl.resizeTextMaxSize = 26;
            for (int k = 0; k < 24; k++)
            {
                int h = (k + 6) % 24;
                bool over = cap > 0 && load[h] > cap * 1.0001f;
                var bar = Ui.Img("Bar", chart, over ? Ui.Red : Color.Lerp(PartColor[order[k / 6]], Color.white, 0.15f)).rectTransform;
                bar.anchorMin = new Vector2(k / 24f, 0); bar.anchorMax = new Vector2((k + 1) / 24f, Mathf.Max(0.04f, load[h] / topLoad)); bar.offsetMin = new Vector2(5, 0); bar.offsetMax = new Vector2(-5, 0);
            }
            if (cap > 0)
            {
                var line = Ui.Img("Cap", chart, new Color(1, 1, 1, 0.85f)).rectTransform;
                line.anchorMin = new Vector2(0, cap / topLoad); line.anchorMax = new Vector2(1, cap / topLoad); line.offsetMin = new Vector2(0, -2); line.offsetMax = new Vector2(0, 2);
            }
        }

        // ------------------------------------------------------------------ RECORD

        void RecordPage()
        {
            var s = _sim;
            int total = s.RepTotal;
            Header("REPUTATION " + total + "  ·  PUNCTUAL: 4 IN 5 ON TIME  ·  SERVICED: NEVER FUELLED BY A SUPPLIER  ·  STANDARDS: PASSENGERS AT THE AIRLINE'S GRADE 4 TIMES IN 5");
            TableHead("FLIGHT", 0f, 0.13f, "AIRLINE", 0.13f, 0.31f, "CONTRACT", 0.31f, 0.47f, "FLOWN", 0.47f, 0.59f, "PUNCTUAL", 0.59f, 0.69f, "SERVICED", 0.69f, 0.79f, "STANDARDS", 0.79f, 0.89f, "RESULT", 0.89f, 1f);
            if (s.State.bookings.Count == 0 && s.State.record.Count == 0)
                Ico("medal", TintGold).Entry("Nothing on record yet", null, "Every contract you fly is kept here with its three marks. Full marks pay the whole reward and the whole reputation; each mark missed takes a fifth of the reward off.", null, null, null);
            foreach (var b in s.State.bookings)
            {
                var plane = Content.Aircraft[(AircraftClass)b.plane];
                bool punctual, serviced, standards; s.Marks(b, out punctual, out serviced, out standards);
                var row = Row(100);
                CellText(row, 0f, 0.13f, s.FlightNo(b), 32, Color.white);
                Plate(row, 0.13f, 0.31f, b.airline);
                CellText(row, 0.31f, 0.47f, plane.Name + "\n<size=22><color=#FFFFFF90>" + Sim.KindName(b).ToLower() + ", needs grade " + (b.grade >= 4 ? "any" : Sim.Grades[b.grade]) + "</color></size>", 28, Color.white);
                CellText(row, 0.47f, 0.59f, b.flown + " of " + b.flights, 28, Color.white, TextAnchor.UpperCenter);
                Meter(row, 0.47f, 0.59f, b.flown / (float)b.flights, Ui.Green, 14, -16);
                Mark(row, 0.59f, 0.69f, b.flown == 0 ? 0 : punctual ? 1 : -1, b.onTime + "/" + b.flown);
                Mark(row, 0.69f, 0.79f, b.flown == 0 ? 0 : serviced ? 1 : -1, b.served + "/" + b.flown);
                Mark(row, 0.79f, 0.89f, b.flown == 0 ? 0 : standards ? 1 : -1, b.pleased + "/" + b.flown);
                CellText(row, 0.89f, 1f, "In progress", 26, Ui.Dim);
            }
            foreach (var r in s.State.record)
            {
                var plane = Content.Aircraft[(AircraftClass)Mathf.Clamp(r.plane, 0, 4)];
                var row = Row(100);
                CellText(row, 0f, 0.13f, Content.Airlines[r.airline].Code + " " + r.number + "\n<size=20><color=#FFFFFF80>day " + r.day + "</color></size>", 30, Color.white);
                Plate(row, 0.13f, 0.31f, r.airline);
                CellText(row, 0.31f, 0.47f, plane.Name + "\n<size=22><color=#FFFFFF90>" + (Sim.KindName(r.kind).ToLower() + (r.flights > 1 ? " of " + r.flights : "")) + "</color></size>", 28, Color.white);
                CellText(row, 0.47f, 0.59f, r.dropped ? "dropped" : "all " + r.flights, 26, Ui.Dim);
                Mark(row, 0.59f, 0.69f, r.dropped ? 0 : r.punctual ? 1 : -1, r.dropped ? null : r.onTime + "/" + r.flights);
                Mark(row, 0.69f, 0.79f, r.dropped ? 0 : r.serviced ? 1 : -1, r.dropped ? null : r.served + "/" + r.flights);
                Mark(row, 0.79f, 0.89f, r.dropped ? 0 : r.standards ? 1 : -1, r.dropped ? null : r.pleased + "/" + r.flights);
                CellText(row, 0.89f, 1f, r.dropped ? "<color=#FF8070>" + r.rep + " rep</color>" : "<color=#7CE08A>+" + r.rep + " rep</color>\n<size=24><color=#FFC83D>+" + Format.Money(r.paid) + "</color></size>", 30, Color.white);
            }
        }

        // ------------------------------------------------------------------ AIRLINES

        void AirlinesPage()
        {
            var s = _sim;
            Header("REPUTATION WITH AN AIRLINE RAISES WHAT ALL ITS FLIGHTS PAY AND BRINGS LONGER CONTRACTS");
            TableHead("AIRLINE", 0f, 0.19f, "REPUTATION", 0.19f, 0.39f, "MOOD", 0.39f, 0.53f, "ASKS FOR", 0.53f, 0.61f, "LIKES TO FLY", 0.61f, 0.82f, "ROUTES", 0.82f, 0.89f, "", 0.89f, 1f);
            var lines = s.KnownAirlines.Union(s.State.contracts.Select(c => Content.Routes[c.route].Airline).Where(a => a != "own")).Distinct()
                .OrderByDescending(a => s.Rep(a)).ThenByDescending(a => s.State.contracts.Count(c => Content.Routes[c.route].Airline == a)).ToList();
            foreach (var a in lines)
            {
                string id = a; var d = Content.Airlines[a];
                int tier = s.RepTier(a), rep = s.Rep(a), next = s.RepToNext(a);
                float mood = s.Relation(a);
                int routes = s.State.contracts.Count(c => Content.Routes[c.route].Airline == a);
                var row = Row(112);
                Plate(row, 0f, 0.19f, a);
                CellText(row, 0.19f, 0.39f, rep + "  <size=24><color=#FFC83D>" + Sim.RepName[tier].ToUpper() + "</color></size>  <size=22><color=#FFFFFF90>pays +" + Mathf.RoundToInt((float)(s.RepPay(a) - 1) * 100) + "%</color></size>", 34, Color.white, TextAnchor.UpperCenter);
                Meter(row, 0.19f, 0.39f, tier + 1 < Sim.RepAt.Length ? (rep - Sim.RepAt[tier]) / (float)(Sim.RepAt[tier + 1] - Sim.RepAt[tier]) : 1, Ui.Gold, 14, -8);
                CellText(row, 0.19f, 0.39f, next > 0 ? next + " to " + Sim.RepName[tier + 1].ToLower() : "top tier", 22, Ui.Dim, TextAnchor.LowerCenter, false);
                CellText(row, 0.39f, 0.53f, Sim.Mood(mood).ToUpper() + "\n<size=22><color=#FFFFFF90>" + Sim.PartnerName[s.Partner(a)].ToLower() + "</color></size>", 30, mood >= 65 ? Ui.Green : mood >= 40 ? Color.white : Ui.Red);
                GradeBox(row, 0.53f, 0.61f, d.Grade);
                for (int i = 0; i < d.Likes.Length && i < 3; i++) PartChip(row, 0.61f + i * 0.07f, 0.61f + (i + 1) * 0.07f, d.Likes[i], false);
                CellText(row, 0.82f, 0.89f, routes.ToString(), 36, routes > 0 ? Color.white : Ui.Dim);
                bool active = s.Active(a); string blocked = active ? s.PauseBlocked(a) : null;
                var sw = Ui.Button("Active", row, null, !active ? Locked : blocked != null ? Ui.Green : Ui.Green, () =>
                {
                    if (s.SetActive(id, !active)) { Toast(d.Name + (active ? " is paused: no flights and no offers until you let it back" : " is flying here again")); Sfx.Play(active ? "close" : "confirm", 0.6f); }
                    else { Toast(blocked ?? "Can't change that"); Sfx.Play("error", 0.5f); }
                    _dirty = true;
                }, 30);
                var sr = (RectTransform)sw.transform; sr.anchorMin = new Vector2(0.89f, 0); sr.anchorMax = new Vector2(1, 1); sr.offsetMin = new Vector2(4, 12); sr.offsetMax = new Vector2(-12, -12);
                ButtonFace(sw.transform, active ? "ACTIVE\ntap to pause" : "PAUSED\ntap to let back", null);
            }
            var own = s.State.contracts.Count(c => Content.Routes[c.route].Airline == "own");
            if (own > 0) Ico("paint", Palette.Hex(Sim.LiveryHex[Mathf.Clamp(s.State.livery, 0, Sim.LiveryHex.Length - 1)])).Entry("Your airline", s.FleetSize + " AIRCRAFT", "Your own aircraft fly " + own + (own == 1 ? " route" : " routes") + " from here. Buy aircraft and keep them checked in AIRLINES.", null, null, null);
        }

        // ------------------------------------------------------------------ FLIGHTS

        void FlightsPage()
        {
            var s = _sim;
            Header(s.State.flights.Count + " OF " + s.TowerSlots + " IN THE PATTERN  ·  " + Mathf.RoundToInt(s.OnTimeToday * 100) + "% ON TIME TODAY  ·  " + s.State.dayFlights + " FLIGHTS TODAY");
            TableHead("FLIGHT", 0f, 0.12f, "AIRLINE", 0.12f, 0.3f, "TO", 0.3f, 0.45f, "AIRCRAFT", 0.45f, 0.58f, "STAND", 0.58f, 0.65f, "STATUS", 0.65f, 0.8f, "TURNAROUND", 0.8f, 0.92f);
            if (s.State.flights.Count == 0) Ico("arrival").Entry("No traffic", null, "Nothing in the pattern or on the ground right now.", null, null, null);
            foreach (var f in s.State.flights.OrderByDescending(x => NeedsCrew(x)).ThenByDescending(x => (int)x.phase).ThenBy(x => x.id))
            {
                int id = f.id;
                var r = Content.Routes[f.route]; var plane = Content.Aircraft[r.Class];
                Color c; string st = StatusOf(f, out c);
                var row = Row(88);
                CellText(row, 0f, 0.12f, s.FlightNo(f), 32, Amber);
                Plate(row, 0.12f, 0.3f, r.Airline);
                CellText(row, 0.3f, 0.45f, r.Destination + (f.contract != 0 ? "\n<size=20><color=#FFC83D>CONTRACT</color></size>" : ""), 28, Color.white);
                CellText(row, 0.45f, 0.58f, plane.Name, 28, Color.white);
                CellText(row, 0.58f, 0.65f, f.stand >= 0 ? (f.stand + 1).ToString() : "-", 34, f.stand >= 0 ? Color.white : Ui.Dim);
                CellText(row, 0.65f, 0.8f, st, 30, c);
                if (f.phase >= Phase.Deplane && f.phase <= Phase.TaxiOut)
                {
                    float target = s.OnTimeTarget(r), used = (float)(s.State.time - f.parkedAt - f.held);
                    Meter(row, 0.8f, 0.92f, used / target, used > target ? Ui.Red : used > target * 0.8f ? Ui.Gold : Ui.Green, 16);
                }
                var view = Ui.Button("View", row, "VIEW", Ui.Blue, () => { OpenTab(-1); if (CameraRig.Inside) { CameraRig.Instance.SetInside(false); OnInsideChanged(); } LookAtFlight(id); }, 28);
                var vr = (RectTransform)view.transform; vr.anchorMin = new Vector2(0.92f, 0); vr.anchorMax = new Vector2(1, 1); vr.offsetMin = new Vector2(4, 12); vr.offsetMax = new Vector2(-12, -12);
            }
            var due = s.State.bookings.Where(b => b.hour >= 0 && !b.airborne && b.lastDay != s.FuelDay && b.flown < b.flights).OrderBy(b => Col(b.hour)).ToList();
            if (due.Count > 0) Header("CONTRACT FLIGHTS STILL TO COME TODAY");
            foreach (var b in due)
            {
                var row = Row(80);
                CellText(row, 0f, 0.12f, s.FlightNo(b), 32, Amber);
                Plate(row, 0.12f, 0.3f, b.airline);
                CellText(row, 0.3f, 0.45f, Content.Places[b.place % Content.Places.Length], 28, Color.white);
                CellText(row, 0.45f, 0.58f, Content.Aircraft[(AircraftClass)b.plane].Name, 28, Color.white);
                CellText(row, 0.58f, 0.65f, (b.stand + 1).ToString(), 34, Color.white);
                bool missed = Col(b.hour) < Col((int)s.Hour);
                CellText(row, 0.65f, 0.8f, missed ? "TOMORROW " + Hh(b.hour) : "DUE " + Hh(b.hour), 30, missed ? Ui.Dim : Ui.Green);
                CellText(row, 0.8f, 1f, (b.flown + 1) + " of " + b.flights + (s.Active(b.airline) ? "" : "  ·  airline paused"), 26, Ui.Dim);
            }
        }

        // ------------------------------------------------------------------ FINANCES

        void FinancesPage()
        {
            var s = _sim; var t = s.State.today;
            double flights = t.Flights, income = t.Income, costs = s.RunningCosts;
            Header(Sim.Weekdays[s.Weekday] + ", DAY " + (s.FuelDay + 1) + "  ·  THE BOOKS CLOSE AT 06:00");

            // Cash, today's result so far, the loan; and the last week as bars.
            var row = Row(250);
            CellText(row, 0.01f, 0.24f, "CASH\n<size=54><color=#FFFFFF>" + Format.Money(s.State.money) + "</color></size>", 26, new Color(0.45f, 0.8f, 1f), TextAnchor.UpperLeft);
            double result = income - costs - System.Math.Max(0, t.fuelBought - t.fuelBack);
            CellText(row, 0.01f, 0.24f, "TODAY SO FAR\n<size=40><color=" + (result >= 0 ? "#7CE08A" : "#FF8070") + ">" + (result >= 0 ? "+" : "-") + Format.Money(System.Math.Abs(result)) + "</color></size>", 24, new Color(0.45f, 0.8f, 1f), TextAnchor.LowerLeft);
            CellText(row, 0.24f, 0.42f, "INCOME TODAY\n<size=40><color=#FFFFFF>" + Format.Money(income) + "</color></size>", 24, new Color(0.45f, 0.8f, 1f), TextAnchor.UpperLeft);
            CellText(row, 0.24f, 0.42f, (s.InDebt ? "OWED TO THE BANK\n<size=36><color=#FF8070>" + Format.Money(s.State.loanOwed) + "</color></size>" : "NO LOAN\n<size=24><color=#FFFFFF90>borrow in EMPIRE</color></size>"), 24, new Color(0.45f, 0.8f, 1f), TextAnchor.LowerLeft);
            var chart = Span(row, 0.44f, 0.99f, 8, 14);
            var days = s.State.ledger.Take(Sim.LedgerDays).Reverse().ToList();
            double top = System.Math.Max(1, System.Math.Max(System.Math.Max(income, costs), days.Count > 0 ? days.Max(d => System.Math.Max(d.Income, d.Costs)) : 0));
            int slots = Sim.LedgerDays + 1;
            for (int i = 0; i < slots; i++)
            {
                bool isToday = i == slots - 1; int k = i - (slots - 1 - days.Count);
                var d = isToday ? null : k >= 0 && k < days.Count ? days[k] : null;
                double inc = isToday ? income : d != null ? d.Income : 0, cost = isToday ? costs : d != null ? d.Costs : 0;
                float x0 = i / (float)slots, x1 = (i + 1) / (float)slots;
                if (isToday || d != null)
                {
                    var a = Ui.Img("Inc", chart, isToday ? new Color(0.25f, 0.72f, 0.42f, 0.55f) : Ui.Green, true).rectTransform;
                    a.anchorMin = new Vector2(x0, 0); a.anchorMax = new Vector2((x0 + x1) / 2, Mathf.Max(0.02f, (float)(inc / top) * 0.82f)); a.offsetMin = new Vector2(8, 34); a.offsetMax = new Vector2(-2, 34);
                    var b = Ui.Img("Cost", chart, isToday ? new Color(0.9f, 0.36f, 0.3f, 0.55f) : Ui.Red, true).rectTransform;
                    b.anchorMin = new Vector2((x0 + x1) / 2, 0); b.anchorMax = new Vector2(x1, Mathf.Max(0.02f, (float)(cost / top) * 0.82f)); b.offsetMin = new Vector2(2, 34); b.offsetMax = new Vector2(-8, 34);
                }
                var lab = Ui.Label("D", chart, isToday ? "TODAY" : d != null ? "DAY " + d.day : "", 20, isToday ? Color.white : Ui.Dim, TextAnchor.LowerCenter, false).rectTransform;
                lab.anchorMin = new Vector2(x0, 0); lab.anchorMax = new Vector2(x1, 0); lab.pivot = new Vector2(0.5f, 0); lab.offsetMin = new Vector2(0, 0); lab.offsetMax = new Vector2(0, 30);
            }
            var key = Ui.Label("Key", chart, "<color=#40B86B>income</color>  <color=#E65C4D>wages and upkeep</color>", 22, Color.white, TextAnchor.UpperLeft, false).rectTransform;
            key.anchorMin = new Vector2(0, 1); key.anchorMax = new Vector2(1, 1); key.pivot = new Vector2(0.5f, 1); key.offsetMin = new Vector2(0, -30); key.offsetMax = Vector2.zero;

            var last = s.State.ledger.Count > 0 ? s.State.ledger[0] : null;
            System.Func<double, string> was = v => last != null ? "YESTERDAY " + Format.Money(v) : null;
            System.Func<double, double, float> share = (v, of) => of > 0 ? (float)(v / of) : 0f;
            Header("INCOME TODAY  ·  " + Format.Money(income));
            Ico("runway", TintAir).Bar(share(t.landing, income), Ui.Green).Entry("Landing fees  " + Format.Money(t.landing), was(last != null ? last.landing : 0), "What airlines pay to land, more when the flight leaves on time. Raised by fee policy, partner levels and reputation.", null, null, null);
            Ico("users", TintTerm).Bar(share(t.pax, income), Ui.Green).Entry("Passenger charges  " + Format.Money(t.pax), was(last != null ? last.pax : 0), "A charge on every passenger, scaled by how satisfied they are with the terminal.", null, null, null);
            Ico("fuel", Palette.Fuel).Bar(share(t.fuel, income), Ui.Green).Entry("Fuel margin  " + Format.Money(t.fuel), was(last != null ? last.fuel : 0), "Your margin on the fuel each flight takes from your tanks.", null, null, null);
            Ico("shop", TintGold).Bar(share(t.retail, income), Ui.Green).Entry("Shops and food  " + Format.Money(t.retail), was(last != null ? last.retail : 0), "What passengers spend at what you have placed in the terminal.", null, null, null);
            if (s.Level("cargo") > 0) Ico("cargo", Palette.Hex(0x9A6A3A)).Bar(share(t.cargo, income), Ui.Green).Entry("Cargo  " + Format.Money(t.cargo), was(last != null ? last.cargo : 0), "Freight in the hold of every flight.", null, null, null);
            Ico("coins", TintSocial).Bar(share(t.Other, income), Ui.Green).Entry("Everything else  " + Format.Money(t.Other), was(last != null ? last.Other : 0), "Contract rewards, signing bonuses, the car park, day bonuses, charters and gifts.", null, null, null);

            Header("COSTS  ·  WAGES AND UPKEEP ARE CHARGED WHEN THE DAY CLOSES");
            Ico("manager", TintRed).Entry("Wages  " + Format.Money(s.Wages), was(last != null ? last.wages : 0), "The crews and desks of every department, their managers, and the staff of everything in the terminal. They grow as each department is built up.", null, null, null);
            Ico("hammer", TintRed).Entry("Upkeep  " + Format.Money(s.Upkeep), was(last != null ? last.upkeep : 0), "Keeping the runway, the stands, the taxiways, the tower and the car park in working order.", null, null, null);
            if (s.Level("fuel") > 0) Ico("fuel", TintPlain).Entry("Fuel bought  " + Format.Money(t.fuelBought), was(last != null ? last.fuelBought : 0), "Tankers bought today. Departures paid " + Format.Money(t.fuelBack) + " of it back at the pump.", null, null, null);
            Ico("box", TintPlain).Entry("Building and purchases  " + Format.Money(s.SpentToday), was(last != null ? last.spent : 0), "Upgrades, terminal items, managers, research and aircraft bought today.", null, null, null);
            if (s.InDebt || t.loanPaid + t.repaid > 0) Ico("bank", TintPlain).Entry("Loan repayments  " + Format.Money(t.loanPaid + t.repaid), was(last != null ? last.loanPaid + last.repaid : 0), "The bank takes a fifth of every payment until the loan is repaid.", null, null, null);
        }
    }
}
