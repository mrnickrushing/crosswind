using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>The bottom sheets: BUILD (facilities and their upgrades), AIRLINES (routes signed and on offer),
    /// STAFF (a manager for each service department), and the welcome-back card.</summary>
    public sealed partial class Hud
    {
        void OpenTab(int tab)
        {
            _tab = tab;
            _sheet.gameObject.SetActive(tab >= 0);
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].GetComponent<Image>().color = i == tab ? Ui.Blue : Ui.Card;
            Sfx.Play(tab >= 0 ? "open" : "close", 0.5f);
            if (tab >= 0) { _sheetList.anchoredPosition = Vector2.zero; RefreshSheet(); }
        }

        static string Gems(int n) { return n + (n == 1 ? " gem" : " gems"); }

        float _rowY;

        RectTransform Row(float height)
        {
            var row = Ui.ArtImg("Row", _sheetList, "card", Color.white).rectTransform;
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = Vector2.zero; row.offsetMax = Vector2.zero; row.anchoredPosition = new Vector2(0, -_rowY); row.sizeDelta = new Vector2(0, height);
            _rowY += height + 12;
            return row;
        }

        void Header(string text)
        {
            var t = Ui.Label("Header", _sheetList, text, 28, new Color(0.45f, 0.8f, 1f), TextAnchor.LowerLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.anchoredPosition = new Vector2(12, -_rowY); rt.sizeDelta = new Vector2(0, 50);
            _rowY += 58;
        }

        // The icon for the next row (set by Ico, used once).
        string _ic; Color _icTint;
        static readonly Color TintAir = Palette.Hex(0x3F78C2), TintTerm = Palette.Hex(0x2A9D94), TintGold = Palette.Hex(0xD79A1E), TintSocial = Palette.Hex(0x8466D6),
            TintPlain = Palette.Hex(0x52647C), TintRed = Palette.Hex(0xC8483C), TintLab = Palette.Hex(0x2F9BD6), TintGreen = Palette.Hex(0x38A862);

        /// <summary>Gives the next row an icon tile: <c>Ico("bell").Entry(...)</c>.</summary>
        Hud Ico(string name, Color? tint = null) { _ic = name; _icTint = tint ?? TintPlain; return this; }
        float _bar = -1; Color _barTint;
        /// <summary>Gives the next row a progress bar along its foot (0..1).</summary>
        Hud Bar(double value, Color? tint = null) { _bar = Mathf.Clamp01((float)value); _barTint = tint ?? Ui.Green; return this; }

        static string FacilityIcon(string id) { return id; }
        static Color FacilityTint(string id)
        {
            switch (id)
            {
                case "fuel": return Palette.Fuel; case "baggage": return Palette.Baggage; case "catering": return Palette.Catering; case "cleaning": return Palette.Cleaning;
                case "pushback": return Palette.Hex(0xC79A12); case "deice": return Palette.Deice; case "hangar": return Palette.Hex(0x6E7F96); case "cargo": return Palette.Hex(0x9A6A3A);
            }
            return Content.Facilities[id].Group == "Terminal" ? TintTerm : TintAir;
        }
        static string WantIcon(Want w, bool queue = false)
        {
            if (queue) return "queue";
            switch (w) { case Want.Food: return "burger"; case Want.Shopping: return "shop"; case Want.Seating: return "couch"; case Want.Toilets: return "restroom"; case Want.Fun: return "gamepad"; case Want.Clean: return "broom"; case Want.Info: return "sign"; default: return "crown"; }
        }
        /// <summary>Red when passengers are unhappy with it, amber in between, green when they have no complaints.</summary>
        static Color ScoreTint(float v) { return v < 0.4f ? TintRed : v < 0.8f ? TintGold : TintGreen; }

        /// <summary>A row: an icon tile, a title, a line under it, and a button on the right that is grey with the reason when it can't be used.</summary>
        void Entry(string title, string tag, string body, string button, string blocked, System.Action act, Color? accent = null)
        {
            var row = Row(150);
            float left = 24;
            if (_ic != null)
            {
                var tile = Ui.ArtImg("Tile", row, "button", _icTint, 44, 1.6f).rectTransform;
                tile.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(108, 108));
                var sp = Ui.Ic(_ic);
                if (sp != null)
                {
                    var shade = Ui.Img("Shade", tile, new Color(0, 0, 0, 0.35f)); shade.sprite = sp; shade.preserveAspect = true;
                    shade.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(64, 64));
                    var g = Ui.Img("Glyph", tile, Color.white); g.sprite = sp; g.preserveAspect = true;
                    g.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 64));
                }
                left = 144; _ic = null;
            }
            float right = button == null ? -24 : -250;
            var t = Ui.Label("Title", row, title + (tag != null ? "  <size=26><color=#FFFFFF90>" + tag + "</color></size>" : ""), 36, Color.white, TextAnchor.UpperLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(left, 0); t.rectTransform.offsetMax = new Vector2(right, -10);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.resizeTextForBestFit = true; t.resizeTextMinSize = 22; t.resizeTextMaxSize = 36;
            t.rectTransform.offsetMin = new Vector2(left, 96);
            var b = Ui.Label("Body", row, body, 30, Ui.Dim, TextAnchor.UpperLeft, false);
            b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(left, _bar >= 0 ? 34 : 10); b.rectTransform.offsetMax = new Vector2(right, -56);
            b.resizeTextForBestFit = true; b.resizeTextMinSize = 20; b.resizeTextMaxSize = 28; b.verticalOverflow = VerticalWrapMode.Truncate;
            if (_bar >= 0)
            {
                var track = Ui.Img("Bar", row, new Color(0, 0, 0, 0.45f), true).rectTransform;
                track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = new Vector2(0, 0); track.offsetMin = new Vector2(left, 16); track.offsetMax = new Vector2(right - 4, 28);
                var fill = Ui.Img("Fill", track, _barTint, true).rectTransform;
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Max(0.02f, _bar), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                _bar = -1;
            }
            if (button == null) return;
            bool ok = blocked == null;
            var btn = Ui.Button("Do", row, null, ok ? (accent ?? Ui.Green) : Ui.Off, () => { if (ok) { act(); Sfx.Play("build", 0.7f); _dirty = true; } else { Toast(blocked); Sfx.Play("error", 0.5f); } }, 40);
            ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 112));
            var l = Ui.Label("L", btn.transform, ok ? button : button + "\n<size=22>" + blocked + "</size>", 34, Color.white);
            l.rectTransform.Fill(10); l.resizeTextForBestFit = true; l.resizeTextMinSize = 16; l.resizeTextMaxSize = 34;
        }

        void RefreshSheet()
        {
            if (_tab < 0) return;
            Ui.Clear(_sheetList);
            _rowY = 0;
            if (_tab == ShopTab) { ShopSheet(); _sheetList.sizeDelta = new Vector2(0, _rowY); return; }
            _sheetTitle.text = TabNames[_tab];
            var s = _sim;
            if (_tab == 0)
            {
                if (s.State.builds.Count > 0)
                {
                    Header("BUILDING (" + s.State.builds.Count + " OF " + s.Builders + " CREWS)");
                    foreach (var job in s.State.builds.ToList())
                    {
                        var jd = Content.Facilities[job.facility];
                        Ico("hammer", TintGold).Bar(1 - (job.doneAt - s.RealNow()) / System.Math.Max(1, job.total), Ui.Gold).Entry(jd.Name, Format.Time(job.doneAt - s.RealNow()) + " LEFT", "Level " + (s.Level(job.facility) + 1) + " is going up. Finished even if you close the game.",
                            "FINISH\n<size=34>" + Gems(s.BuildSkipGems(job)) + "</size>", s.State.gems < s.BuildSkipGems(job) ? "Not enough gems" : null, () => s.SkipBuild(job.facility), Ui.Blue);
                    }
                }
                Header("PASSENGERS: " + Mathf.RoundToInt(s.Satisfaction * 100) + "% SATISFIED (THEY SPEND MORE WHEN HAPPY)");
                foreach (var need in s.Needs)
                    Ico(WantIcon(need.Want, need.Queue), ScoreTint(need.Value)).Bar(need.Value, ScoreTint(need.Value)).Entry(need.Name, Mathf.RoundToInt(need.Value * 100) + "%", need.Value >= 0.999f ? "Passengers have no complaints here." : need.Advice + ".", null, null, null);
                foreach (var group in new[] { "Airside", "Services", "Terminal" })
                {
                    Header(group.ToUpper());
                    foreach (var id in Content.FacilityOrder)
                    {
                        var d = Content.Facilities[id];
                        if (d.Group != group) continue;
                        int level = s.Level(id);
                        if (s.Building(id) != null) continue;
                        float secs = s.BuildSeconds(id);
                        string blocked = s.UpgradeBlocked(id);
                        string why = blocked == null ? null : blocked.StartsWith("Need ") ? blocked : blocked;
                        Ico(FacilityIcon(id), FacilityTint(id)).Entry(d.Name, (level > 0 ? "LEVEL " + level : "NOT BUILT") + (secs > 0 ? " · " + Format.Time(secs) + " TO BUILD" : ""), d.Blurb, level >= d.MaxLevel ? "MAX" : (level > 0 ? "UPGRADE\n" : "BUILD\n") + "<size=34>" + Format.Money(s.Cost(id)) + "</size>",
                            blocked != null && blocked.StartsWith("Need ") ? blocked : why, () => { if (s.Upgrade(id)) if (secs > 60) Reminders.AskOnce(); Toast(secs > 0 ? d.Name + ": the crew has started" : d.Name + (level > 0 ? " upgraded to level " + (level + 1) : " built")); });
                    }
                }
            }
            else if (_tab == 1)
            {
                Header("YOUR ROUTES");
                foreach (var c in s.State.contracts.ToList())
                {
                    var r = Content.Routes[c.route]; var plane = Content.Aircraft[r.Class];
                    if (r.Airline == "own") { Ico("stands", Palette.Hex(Sim.LiveryHex[Mathf.Clamp(s.State.livery, 0, Sim.LiveryHex.Length - 1)])).Entry(r.Destination, "YOUR AIRLINE", plane.Name + "  ·  " + s.Owned(r.Class) + " aircraft  ·  " + Format.Money(s.FlightValue(r) * Sim.OwnPay) + " a flight on time", null, null, null); continue; }
                    float mood = s.Relation(r.Airline);
                    int nextTerms = (c.terms + 1) % 3;
                    Ico("stands", Models.AirlineColor(r.Airline)).Entry(r.Destination, Content.Airlines[r.Airline].Name + " · " + Sim.Mood(mood).ToUpper(),
                        plane.Name + "  ·  " + Mathf.RoundToInt(r.FlightsPerHour * Sim.TermsFlights(c.terms)) + " flights an hour  ·  " + Format.Money(s.FlightValue(r) * Sim.TermsPay(c.terms) * s.RelationPay(r.Airline)) + " a flight on time  ·  terms: " + Sim.TermsName[c.terms],
                        "TERMS\n<size=30>" + Sim.TermsName[nextTerms] + "</size>", null, () => { s.SetTerms(r.Id, nextTerms); Toast(r.Destination + ": " + Sim.TermsBlurb[nextTerms]); }, Ui.Card);
                }
                PlanSection();
                Header("AIRLINES: ON-TIME FLIGHTS PLEASE THEM, LATE ONES COST YOU");
                foreach (var line in s.State.contracts.Select(c => Content.Routes[c.route].Airline).Where(a => a != "own").Distinct())
                {
                    float mood = s.Relation(line);
                    Ico("handshake", Models.AirlineColor(line)).Entry(Content.Airlines[line].Name, Sim.Mood(mood).ToUpper() + "  " + Mathf.RoundToInt(mood) + "/100", "\"" + Content.Airlines[line].Motto + "\"  Pays " + Mathf.RoundToInt((float)s.RelationPay(line) * 100) + "% of its fees."
                        + (mood < 20 ? "  One more bad day and they cancel a route." : mood >= 85 ? "  Your best partner." : ""), null, null, null);
                }
                Header("YOUR AIRLINE  ·  " + s.FleetSize + " AIRCRAFT  ·  EACH FLIGHT PAYS " + Mathf.RoundToInt((float)Sim.OwnPay * 100) + "%");
                Ico("paint", Palette.Hex(Sim.LiveryHex[Mathf.Clamp(s.State.livery, 0, Sim.LiveryHex.Length - 1)])).Entry("Livery: " + Sim.Liveries[s.State.livery], null, "Your aircraft fly in your colours. New aircraft and the next arrivals wear the new paint.", "CHANGE", null,
                    () => { s.SetLivery(s.State.livery + 1); Toast("Livery: " + Sim.Liveries[s.State.livery]); }, Palette.Hex(Sim.LiveryHex[s.State.livery]));
                foreach (AircraftClass ac in System.Enum.GetValues(typeof(AircraftClass)))
                {
                    var pl = Content.Aircraft[ac]; var own = Content.Routes["own_" + ac.ToString().ToLower()];
                    Ico("stands", TintAir).Entry(pl.Name, s.Owned(ac) > 0 ? "OWN " + s.Owned(ac) : null, pl.Seats + " seats on your " + own.Destination + " route  ·  " + Format.Money(s.FlightValue(own) * Sim.OwnPay) + " a flight  ·  each one you own adds flights",
                        "BUY\n<size=34>" + Format.Money(s.AircraftCost(ac)) + "</size>", s.BuyAircraftBlocked(ac), () => { if (s.BuyAircraft(ac)) Toast(pl.Name + " joins your fleet"); }, Ui.Gold);
                }
                var offers = s.Offers.ToList();
                Header(offers.Count > 0 ? "ON OFFER" : "NO NEW OFFERS: RAISE YOUR STAR RATING");
                foreach (var r in offers)
                {
                    var plane = Content.Aircraft[r.Class];
                    string needs = string.Join(", ", r.Needs.Select(n => Content.Facilities[Content.ServiceFacility(n)].Name));
                    Ico("arrival", Models.AirlineColor(r.Airline)).Entry(r.Destination, Content.Airlines[r.Airline].Name, plane.Name + " (" + plane.Seats + " seats)  ·  " + r.FlightsPerHour + " an hour  ·  needs " + needs
                        + (r.SigningBonus > 0 ? "  ·  signing bonus " + Format.Money(r.SigningBonus) : ""),
                        "SIGN", s.SignBlocked(r.Id), () => { if (s.Sign(r.Id)) Toast(Content.Airlines[r.Airline].Name + " now flies to " + r.Destination); }, Ui.Blue);
                }
                var next = Content.Routes.Values.Where(r => !s.Signed(r.Id) && r.Stars > s.Stars).OrderBy(r => r.Stars).FirstOrDefault();
                if (next != null) Ico("lock").Entry(Content.Airlines[next.Airline].Name, "AT " + next.Stars + " STARS", "\"" + Content.Airlines[next.Airline].Motto + "\"  They are watching how you run the place.", null, null, null);
            }
            else if (_tab == 3) ResearchSheet();
            else if (_tab == 4) EmpireSheet();
            else if (_tab == FriendsTab) FriendsSheet();
            else
            {
                Header("MANAGERS RUN A DEPARTMENT FOR YOU, EVEN WHILE YOU'RE AWAY");
                foreach (var id in Content.FacilityOrder)
                {
                    var d = Content.Facilities[id];
                    if (d.Group != "Services") continue;
                    bool hired = s.State.managers.Contains(id);
                    if (d.Group != "Services" || id == "hangar" || id == "cargo") continue;
                    int ml = s.ManagerLevel(id);
                    if (hired)
                        Ico("manager", FacilityTint(id)).Entry(d.Name + " manager", "LEVEL " + ml, "Sends the crews out the moment an aircraft parks. Crews work " + (ml * 10) + "% faster; training adds 10%.",
                            ml >= Sim.MaxManagerLevel ? null : "TRAIN\n<size=34>" + Format.Money(s.TrainCost(id)) + "</size>", s.TrainBlocked(id), () => { if (s.Train(id)) Toast(d.Name + " manager trained to level " + (ml + 1)); }, Ui.Blue);
                    else
                        Ico("manager", FacilityTint(id)).Entry(d.Name + " manager", null, "No more tapping for this service, and its flights earn while the app is closed.",
                            "HIRE\n<size=34>" + Format.Money(s.ManagerCost(id)) + "</size>", s.HireBlocked(id), () => { if (s.Hire(id)) Toast(d.Name + " now runs itself"); }, Ui.Gold);
                }
                CrewSection();
                Ico("clock", TintGold).Entry("Earning while away", null, "With every service on a route managed, that route keeps flying for up to " + Format.Time(s.OfflineCapSeconds) + " with the app closed. Right now: " + Format.Money(s.IdleRate() * 60) + " a minute.", null, null, null);
            }
            _sheetList.sizeDelta = new Vector2(0, _rowY);
        }

        void ResearchSheet()
        {
            var s = _sim;
            if (s.State.research != null)
            {
                var r = Content.ResearchById(s.State.research.id);
                Header("UNDER WAY");
                Ico("flask", TintGold).Entry(r.Name, Format.Time(s.ResearchLeft) + " LEFT", r.Blurb, "FINISH\n<size=34>" + Gems(s.ResearchSkipGems) + "</size>", s.State.gems < s.ResearchSkipGems ? "Not enough gems" : null,
                    () => { s.SkipResearch(); }, Ui.Blue);
            }
            foreach (var branch in Content.Research.Select(r => r.Branch).Distinct())
            {
                Header(branch.ToUpper());
                foreach (var r in Content.Research.Where(x => x.Branch == branch))
                {
                    if (s.Researched(r.Id)) { Ico("medal", TintGreen).Entry(r.Name, "DONE", r.Blurb, null, null, null); continue; }
                    if (s.State.research != null && s.State.research.id == r.Id) continue;
                    // Only the next project of a branch and the one after it are shown: the rest is a surprise.
                    if (r.Requires != null && !s.Researched(r.Requires) && Content.ResearchById(r.Requires).Requires != null && !s.Researched(Content.ResearchById(r.Requires).Requires)) continue;
                    Ico("flask", TintLab).Entry(r.Name, Format.Time(r.Seconds), r.Blurb, "RESEARCH\n<size=34>" + Format.Money(s.ResearchCost(r)) + "</size>", s.ResearchBlocked(r.Id),
                        () => { if (s.StartResearch(r.Id)) Toast("Research started: " + r.Name); }, Ui.Blue);
                }
            }
        }

        void EmpireSheet()
        {
            var s = _sim;
            FinanceSection();
            Header("TODAY");
            for (int i = 0; i < s.State.tasks.Count; i++)
            {
                var task = s.State.tasks[i]; int k = i;
                bool done = task.progress >= task.target;
                Ico("tasks", task.claimed ? TintPlain : TintGreen).Bar(task.progress / System.Math.Max(1, task.target)).Entry(Sim.TaskText(task), task.claimed ? "CLAIMED" : Format.Money(task.progress) + " / " + Format.Money(task.target), "Reward: " + task.gems + " gems. New tasks every day.",
                    task.claimed ? null : "CLAIM\n<size=34>" + task.gems + " gems</size>", done ? null : "Not done yet", () => { if (s.ClaimTask(k)) Toast("+" + task.gems + " gems"); }, Ui.Blue);
            }
            var ev = s.CurrentEvent;
            Header("THIS WEEK'S EVENT  ·  ENDS IN " + Format.Time(s.EventLeft).ToUpper());
            int evClaimed = s.State.eventClaimed;
            string marks = string.Join("   ", ev.Targets.Select((tg, i) => (i < evClaimed ? "<color=#7BD88F>" : s.State.eventProgress >= tg ? "<color=#FFC83D>" : "<color=#FFFFFF80>") + Format.Money(tg) + " → " + ev.Gems[i] + " gems</color>"));
            Ico("star", TintGold).Entry(ev.Name, Format.Money(s.State.eventProgress) + (evClaimed < ev.Targets.Length ? " / " + Format.Money(ev.Targets[evClaimed]) : " · ALL DONE"), ev.Blurb + "\n" + marks,
                evClaimed < ev.Targets.Length ? "CLAIM\n<size=34>" + ev.Gems[evClaimed] + " gems</size>" : null, s.EventWaiting > 0 ? null : "Not reached yet", () => { int g = s.ClaimEvent(); if (g > 0) Toast(ev.Name + ": +" + g + " gems"); }, Ui.Gold);
            Header("DAILY GIFT");
            Ico("calendar", TintSocial).Entry("Day " + (s.SignInIndex + 1) + " of 7", s.SignInReady ? null : "COME BACK TOMORROW", "A gift every day you play: " + string.Join(", ", Sim.SignInGems) + " gems. Miss a day and it starts again.",
                s.SignInReady ? "CLAIM\n<size=34>" + Sim.SignInGems[s.SignInIndex] + " gems</size>" : null, null, () => { int g = s.ClaimSignIn(); if (g > 0) Toast("Daily gift: +" + g + " gems"); }, Ui.Gold);
            Header("BOOST");
            Ico("bolt", TintGreen).Entry("Free boost", s.FreeBoostReady ? "READY" : "IN " + Format.Time(s.FreeBoostIn), "Double income for " + Content.BoostMinutes + " minutes, on the house, every " + Sim.FreeBoostHours + " hours.",
                "TAKE IT", s.FreeBoostReady ? null : "Not ready yet", () => { if (s.TakeFreeBoost()) Toast("Income doubled for " + Content.BoostMinutes + " minutes"); }, Ui.Green);
            Ico("bolt", TintGold).Entry("Double income", s.Boosted ? Format.Time(s.BoostLeft) + " LEFT" : null, "Everything the airport earns is doubled for " + Content.BoostMinutes + " minutes. Stacks up to 4 hours.",
                "BOOST\n<size=34>" + Content.BoostGems + " gems</size>", s.State.gems < Content.BoostGems ? "Not enough gems" : null, () => { if (s.BuyBoost()) Toast("Income doubled for " + Content.BoostMinutes + " minutes"); }, Ui.Gold);

            Header(s.Map.Name.ToUpper() + "  ·  " + s.State.wings + " WINGS (+" + Mathf.RoundToInt(s.State.wings * Content.WingBonus * 100) + "% income)");
            Ico("wings", TintGold).Entry("Sell your stake", s.Stars >= Content.SellStars ? "WORTH " + s.WingsOnSale + " WINGS" : null,
                "Sell this airport for Wings, which raise all income for good, and start a new one somewhere harder and richer. Research, gems and achievements stay with you."
                + (s.Stars < Content.SellStars ? " Reach " + Content.SellStars + " stars first." : ""), null, null, null);
            foreach (var m in Content.Maps)
            {
                if (m.Id == s.State.map) continue;
                bool run = s.State.mapsRun.Contains(m.Id);
                Ico("globe", TintAir).Entry(m.Name, run ? "RUN BEFORE" : m.WingsToOpen > 0 ? m.WingsToOpen + " WINGS" : null, m.Blurb + " " + m.Twist + "  Income x" + Format.Money(m.IncomeScale) + ".",
                    "MOVE", s.SellBlocked(m.Id), () => ConfirmSell(m), Ui.Gold);
            }

            Header("SHOP" + (Store.Available ? "" : " (OPENS WITH THE APP STORE RELEASE)"));
            foreach (var prod in Sim.Products)
            {
                bool owned = prod.Once && s.Owns(prod.Id);
                Ico("cart", TintGold).Entry(prod.Name, owned ? "OWNED" : null, prod.Blurb, owned ? null : "BUY\n<size=34>" + Store.Price(prod) + "</size>", null,
                    () => Store.Buy(prod, (ok, msg) => { Toast(msg); if (ok) Sfx.Play("confirm"); _dirty = true; }), Ui.Gold);
            }
            Header("SETTINGS");
            Ico("sound").Entry("Sound", Sfx.Muted ? "OFF" : "ON", "Interface sounds and the airfield.", Sfx.Muted ? "TURN ON" : "TURN OFF", null, () => { Sfx.Muted = !Sfx.Muted; }, Ui.Card);
            Ico("sound", TintSocial).Entry("Music", Sfx.Music ? "ON" : "OFF", "A calm loop by day and a quieter one after dark.", Sfx.Music ? "TURN OFF" : "TURN ON", null, () => { Sfx.Music = !Sfx.Music; }, Ui.Blue);
            Ico("chartbar").Entry("Share anonymous play statistics", Telemetry.Sharing ? "ON" : "OFF", "Tells us where players get stuck: goals reached, stars, frame rate. Nothing about you, no tracking.", Telemetry.Sharing ? "TURN OFF" : "TURN ON", null, () => { Telemetry.Sharing = !Telemetry.Sharing; }, Ui.Card);
            Ico("bell").Entry("Reminders", Reminders.On ? "ON" : "OFF", "A notification when the builders or research finish, a free crate is ready, or the airport has earned all it can.", Reminders.On ? "TURN OFF" : "TURN ON", null, () => { Reminders.On = !Reminders.On; }, Ui.Card);
            Ico("cloud", TintAir).Entry("iCloud save", CloudSave.Available ? "ON" : "OFF", CloudSave.Available ? "Your airport is kept in your own iCloud and follows you to a new phone." + (CloudSave.LastPushed > 0 ? " Last saved " + Format.Time(System.Math.Max(0, SaveSystem.Now - CloudSave.LastPushed)) + " ago." : "")
                : "Sign in to iCloud in the iPhone's Settings to keep your airport safe and move it to a new phone.", null, null, null);
            Ico("trash", TintRed).Entry("Erase everything", null, "Deletes your airports, team and progress from this device and from your iCloud, and gives the statistics a new anonymous id. This can't be undone.", "ERASE", null, ConfirmErase, Ui.Red);
            Header("CREDITS");
            Ico("stands", TintAir).Entry("Aircraft models", "CC BY 4.0", "Kestrel 9: MaX3Dd  ·  Heron 50: StarlingVoxel  ·  Albatross 160: Crimexix  ·  Condor 300: Vitos545  ·  Leviathan 520: maurogsw. From Sketchfab, recoloured and resized.", null, null, null);
            Ico("users", TintSocial).Entry("People, vehicles and buildings", "CC0", "Characters by Quaternius. Cars, trucks and town buildings by Kenney. Ground and floor textures from Poly Haven and ambientCG.", null, null, null);
            Ico("pen").Entry("Icons and lettering", null, "Menu icons from Font Awesome Free (CC BY 4.0, fontawesome.com). Lettering in Barlow by Jeremy Tribby (SIL Open Font License).", null, null, null);
            Ico("star", TintGold).Entry("Crosswind", "RUSHING TECHNOLOGIES", "Thank you for playing.", null, null, null);
            Header("ACHIEVEMENTS");
            foreach (var a in Sim.Achievements.OrderBy(a => s.AchievementClaimed(a) ? 2 : s.AchievementDone(a) ? 0 : 1))
            {
                bool claimed = s.AchievementClaimed(a), done = s.AchievementDone(a);
                Ico("trophy", TintGold).Bar(System.Math.Min(a.Target, a.Progress(s)) / a.Target, Ui.Gold).Entry(a.Name, claimed ? "CLAIMED" : Format.Money(System.Math.Min(a.Target, a.Progress(s))) + " / " + Format.Money(a.Target), a.Text,
                    claimed ? null : "CLAIM\n<size=34>" + a.Gems + " gems</size>", done ? null : "Not done yet", () => { if (s.ClaimAchievement(a.Id)) Toast(a.Name + ": +" + a.Gems + " gems"); }, Ui.Blue);
            }
        }

        RectTransform _confirm;

        /// <summary>Selling is for good: say what goes and what stays, and ask.</summary>
        void ConfirmSell(MapDef m)
        {
            if (_confirm != null) Destroy(_confirm.gameObject);
            _confirm = Ui.Img("Confirm", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _confirm.Fill(); _confirm.GetComponent<Image>().raycastTarget = true;
            var box = Ui.Img("Box", _confirm, Ui.Panel, true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 720));
            Ui.Label("T", box, "MOVE TO " + m.Name.ToUpper() + "?", 58, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(840, 80));
            Ui.Label("B", box, "You sell " + _sim.Map.Name + " for <color=#FFC83D>" + _sim.WingsOnSale + " Wings</color> (+" + Mathf.RoundToInt(_sim.WingsOnSale * Content.WingBonus * 100) + "% income, for good).\n\n"
                + "Its buildings, routes, managers and money stay behind.\nYour Wings, gems, research and achievements come with you.\n\n" + m.Twist, 36, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(820, 380));
            var yes = Ui.Button("Yes", box, "SELL AND MOVE", Ui.Gold, () =>
            {
                if (_sim.SellAndMove(m.Id)) { Toast("Welcome to " + m.Name); GameRoot.Instance.Save(); OpenTab(-1); }
                Destroy(_confirm.gameObject); _confirm = null;
            }, 46);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 50), new Vector2(400, 120));
            var no = Ui.Button("No", box, "STAY", Ui.Off, () => { Destroy(_confirm.gameObject); _confirm = null; }, 46);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 50), new Vector2(400, 120));
        }

        void ConfirmErase()
        {
            if (_confirm != null) Destroy(_confirm.gameObject);
            _confirm = Ui.Img("Confirm", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _confirm.Fill(); _confirm.GetComponent<Image>().raycastTarget = true;
            var box = Ui.Img("Box", _confirm, Ui.Panel, true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 620));
            Ui.Label("T", box, "ERASE EVERYTHING?", 58, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(840, 80));
            Ui.Label("B", box, "Every airport, your team, gems, Wings and research are deleted from this device and from your iCloud.\n\nYou start again with a grass strip. This can't be undone.", 36, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(820, 280));
            var yes = Ui.Button("Yes", box, "ERASE", Ui.Red, () =>
            {
                Destroy(_confirm.gameObject); _confirm = null;
                Telemetry.Forget();
                GameRoot.Instance.Restart();
            }, 46);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 50), new Vector2(400, 120));
            var no = Ui.Button("No", box, "KEEP IT", Ui.Green, () => { Destroy(_confirm.gameObject); _confirm = null; }, 46);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 50), new Vector2(400, 120));
        }

        // ------------------------------------------------------------------ a newer save on another device

        /// <summary>Another device saved since this one last looked: ask which airport to continue, never overwrite either unasked.</summary>
        public void OfferCloud()
        {
            var cloud = GameRoot.Instance.PendingCloud; var meta = GameRoot.Instance.PendingMeta;
            if (cloud == null || _cloud != null) return;
            _cloud = Ui.Img("Cloud", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _cloud.Fill(); _cloud.GetComponent<Image>().raycastTarget = true;
            var box = Ui.Img("Box", _cloud, Ui.Panel, true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 700));
            Ui.Label("T", box, "CONTINUE FROM YOUR OTHER DEVICE?", 52, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(840, 80));
            int theirs = meta != null ? meta.Stars : new Sim(cloud).Stars;
            Ui.Label("B", box, "iCloud has a newer save from another device:\n<color=#FFC83D>" + theirs + (theirs == 1 ? " star" : " stars") + "</color>, saved " + Format.Time(System.Math.Max(0, SaveSystem.Now - cloud.savedAt)) + " ago.\n\n"
                + "This device has a <color=#FFC83D>" + _sim.Stars + "-star</color> airport.\n\nThe one you don't pick is replaced.", 36, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(820, 360));
            var yes = Ui.Button("Yes", box, "USE ICLOUD", Ui.Blue, () => { Destroy(_cloud.gameObject); _cloud = null; GameRoot.Instance.ResolveCloud(true); }, 44);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 50), new Vector2(400, 120));
            var no = Ui.Button("No", box, "KEEP THIS ONE", Ui.Off, () => { Destroy(_cloud.gameObject); _cloud = null; GameRoot.Instance.ResolveCloud(false); }, 44);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 50), new Vector2(400, 120));
        }

        RectTransform _cloud;

        // ------------------------------------------------------------------ welcome back

        /// <summary>Shows what the airport earned while the app was closed, if it earned anything worth a card.</summary>
        public void OfferAway()
        {
            double away = GameRoot.Instance.AwaySeconds;
            double earned = _sim.OfflineEarnings(away);
            if (away < 60 || earned < 1) { GameRoot.Instance.ClearAway(); return; }
            // A few minutes away is not an occasion: pay it and say so quietly.
            if (away < 600)
            {
                double got = _sim.CollectOffline(away);
                GameRoot.Instance.ClearAway();
                if (got > 0) Toast("While you were away: +" + Format.Money(got));
                return;
            }
            if (_away != null) Destroy(_away.gameObject);
            _away = Ui.Img("Away", _root, new Color(0, 0, 0, 0.72f)).rectTransform;
            _away.Fill(); _away.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _away, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 660));
            var back = Ui.Img("Back", box, new Color(0.07f, 0.1f, 0.15f, 1f), true).rectTransform; back.Fill(10);      // the panel art is glass: this card must not be
            var plate = Ui.ArtImg("Plate", box, "plate", Color.white).rectTransform;
            plate.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(640, 104));
            Ui.Label("T", plate, "WELCOME BACK", 54, Color.white).rectTransform.Fill();
            Ui.Label("S", box, "Your managers kept the airport running for " + Format.Time(System.Math.Min(away, _sim.OfflineCapSeconds)) + (away > _sim.OfflineCapSeconds ? " (the most they can without you)" : ""), 34, Ui.Dim, TextAnchor.MiddleCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(800, 100));
            var rays = Ui.ArtImg("Rays", box, "rays", new Color(1f, 0.82f, 0.3f, 0.22f), 0).rectTransform;
            rays.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(520, 520));
            rays.gameObject.AddComponent<Spin>().Speed = 14;
            var coin = Ui.ArtImg("Coin", box, "coin", Color.white, 0).rectTransform;
            coin.Place(new Vector2(0.5f, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 16), new Vector2(120, 120));
            var m = Ui.Label("M", box, "+" + Format.Money(earned), 120, Ui.Gold, TextAnchor.MiddleLeft);
            m.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-130, 16), new Vector2(560, 150));
            Outline(m);
            var ok = Ui.Button("Collect", box, "COLLECT", Ui.Green, () =>
            {
                double got = _sim.CollectOffline(GameRoot.Instance.AwaySeconds);
                GameRoot.Instance.ClearAway();
                GameRoot.Instance.Save();
                Destroy(_away.gameObject); _away = null;
                if (got > 0) { Reward(got, 0); Sfx.Play("claim"); }
            }, 56);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(520, 124));
        }

        // ------------------------------------------------------------------ finance

        static readonly string[] IncomeName = { "Landing fees", "Passenger fees", "Fuel", "Shops and food", "Cargo" };

        void FinanceSection()
        {
            var s = _sim;
            Header("FINANCE");
            int next = (s.FeeLevel + 1) % 5;
            Ico("tag", TintGold).Entry("Fee policy: " + Sim.FeeName[s.FeeLevel], null, Sim.FeeBlurb(s.FeeLevel), "CHANGE\n<size=24>" + Sim.FeeName[next] + "</size>", s.FeeBlocked(next),
                () => { if (s.SetFees(next)) Toast("Fees: " + Sim.FeeName[next].ToLower() + ". " + Sim.FeeBlurb(next)); }, Ui.Blue);
            if (s.InDebt)
                Ico("bank", TintRed).Entry("Loan", "OWED " + Format.Money(s.State.loanOwed), "The bank takes " + Mathf.RoundToInt((float)Sim.LoanShare * 100) + "% of every payment until it is repaid (" + Mathf.RoundToInt((float)(1 - s.State.loanOwed / System.Math.Max(1, s.State.loanTotal)) * 100) + "% done). Pay it off early from cash.",
                    "REPAY", s.State.money >= 1 ? null : "No cash", () => { if (s.RepayLoan()) Sfx.Play("coin", 0.6f); }, Ui.Gold);
            else
                foreach (var l in Sim.Loans)
                {
                    var loan = l;
                    Ico("bank", TintGreen).Entry(l.Name, "+" + Format.Money(s.LoanAmount(l)), "Cash now; " + Format.Money(System.Math.Floor(s.LoanAmount(l) * l.Interest)) + " back, taken as a fifth of every payment.", "BORROW", s.LoanBlocked(l),
                        () => { double a = s.LoanAmount(loan); if (s.TakeLoan(loan.Id)) { Reward(a, 0); Sfx.Play("coin", 0.7f); } }, Ui.Green);
                }
            double total = s.State.incomeBy.Sum();
            if (total > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 5; i++) if (s.State.incomeBy[i] > 0) parts.Add(IncomeName[i] + " " + Mathf.RoundToInt((float)(s.State.incomeBy[i] / total * 100)) + "%");
                Ico("pie", TintLab).Entry("Where the money comes from", Format.Money(total), string.Join("  ·  ", parts), null, null, null);
            }
        }
    }
}
