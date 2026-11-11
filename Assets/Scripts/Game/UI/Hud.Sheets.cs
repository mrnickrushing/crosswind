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
            if (tab >= 0) { _sheetList.anchoredPosition = Vector2.zero; RefreshSheet(); }
        }

        float _rowY;

        RectTransform Row(float height)
        {
            var row = Ui.Img("Row", _sheetList, Ui.Card, true).rectTransform;
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = Vector2.zero; row.offsetMax = Vector2.zero; row.anchoredPosition = new Vector2(0, -_rowY); row.sizeDelta = new Vector2(0, height);
            _rowY += height + 12;
            return row;
        }

        void Header(string text)
        {
            var t = Ui.Label("Header", _sheetList, text, 34, Ui.Dim, TextAnchor.LowerLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.anchoredPosition = new Vector2(12, -_rowY); rt.sizeDelta = new Vector2(0, 50);
            _rowY += 58;
        }

        /// <summary>A row: title, a line under it, and a button on the right that is grey with the reason when it can't be used.</summary>
        void Entry(string title, string tag, string body, string button, string blocked, System.Action act, Color? accent = null)
        {
            var row = Row(168);
            var t = Ui.Label("Title", row, title + (tag != null ? "  <size=32><color=#FFFFFF90>" + tag + "</color></size>" : ""), 42, Color.white, TextAnchor.UpperLeft);
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(24, 0); t.rectTransform.offsetMax = new Vector2(-300, -12);
            var b = Ui.Label("Body", row, body, 30, Ui.Dim, TextAnchor.UpperLeft, false);
            b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(24, 10); b.rectTransform.offsetMax = new Vector2(-300, -64);
            b.resizeTextForBestFit = true; b.resizeTextMinSize = 22; b.resizeTextMaxSize = 30; b.verticalOverflow = VerticalWrapMode.Truncate;
            if (button == null) return;
            bool ok = blocked == null;
            var btn = Ui.Button("Do", row, null, ok ? (accent ?? Ui.Green) : Ui.Off, () => { if (ok) { act(); _dirty = true; } else Toast(blocked); }, 40);
            ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(264, 124));
            var l = Ui.Label("L", btn.transform, ok ? button : button + "\n<size=24>" + blocked + "</size>", 40, Color.white);
            l.rectTransform.Fill(8); l.resizeTextForBestFit = true; l.resizeTextMinSize = 20; l.resizeTextMaxSize = 40;
        }

        void RefreshSheet()
        {
            if (_tab < 0) return;
            Ui.Clear(_sheetList);
            _rowY = 0;
            _sheetTitle.text = TabNames[_tab];
            var s = _sim;
            if (_tab == 0)
            {
                foreach (var group in new[] { "Airside", "Services", "Terminal" })
                {
                    Header(group.ToUpper());
                    foreach (var id in Content.FacilityOrder)
                    {
                        var d = Content.Facilities[id];
                        if (d.Group != group) continue;
                        int level = s.Level(id);
                        string blocked = s.UpgradeBlocked(id);
                        string why = blocked == null ? null : blocked.StartsWith("Need ") ? blocked : blocked;
                        Entry(d.Name, level > 0 ? "LEVEL " + level : "NOT BUILT", d.Blurb, level >= d.MaxLevel ? "MAX" : (level > 0 ? "UPGRADE\n" : "BUILD\n") + "<size=34>" + Format.Money(s.Cost(id)) + "</size>",
                            blocked != null && blocked.StartsWith("Need ") ? blocked : why, () => { if (s.Upgrade(id)) Toast(d.Name + (level > 0 ? " upgraded to level " + (level + 1) : " built")); });
                    }
                }
            }
            else if (_tab == 1)
            {
                Header("YOUR ROUTES");
                foreach (var c in s.State.contracts)
                {
                    var r = Content.Routes[c.route]; var plane = Content.Aircraft[r.Class];
                    Entry(r.Destination, Content.Airlines[r.Airline].Name, plane.Name + " (" + plane.Seats + " seats)  ·  " + r.FlightsPerHour + " flights an hour  ·  " + Format.Money(s.FlightValue(r)) + " a flight on time",
                        null, null, null);
                }
                var offers = s.Offers.ToList();
                Header(offers.Count > 0 ? "ON OFFER" : "NO NEW OFFERS: RAISE YOUR STAR RATING");
                foreach (var r in offers)
                {
                    var plane = Content.Aircraft[r.Class];
                    string needs = string.Join(", ", r.Needs.Select(n => Content.Facilities[Content.ServiceFacility(n)].Name));
                    Entry(r.Destination, Content.Airlines[r.Airline].Name, plane.Name + " (" + plane.Seats + " seats)  ·  " + r.FlightsPerHour + " an hour  ·  needs " + needs
                        + (r.SigningBonus > 0 ? "  ·  signing bonus " + Format.Money(r.SigningBonus) : ""),
                        "SIGN", s.SignBlocked(r.Id), () => { if (s.Sign(r.Id)) Toast(Content.Airlines[r.Airline].Name + " now flies to " + r.Destination); }, Ui.Blue);
                }
                var next = Content.Routes.Values.Where(r => !s.Signed(r.Id) && r.Stars > s.Stars).OrderBy(r => r.Stars).FirstOrDefault();
                if (next != null) Entry(Content.Airlines[next.Airline].Name, "AT " + next.Stars + " STARS", "\"" + Content.Airlines[next.Airline].Motto + "\"  They are watching how you run the place.", null, null, null);
            }
            else if (_tab == 3) ResearchSheet();
            else if (_tab == 4) EmpireSheet();
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
                        Entry(d.Name + " manager", "LEVEL " + ml, "Sends the crews out the moment an aircraft parks. Crews work " + (ml * 10) + "% faster; training adds 10%.",
                            ml >= Sim.MaxManagerLevel ? null : "TRAIN\n<size=34>" + Format.Money(s.TrainCost(id)) + "</size>", s.TrainBlocked(id), () => { if (s.Train(id)) Toast(d.Name + " manager trained to level " + (ml + 1)); }, Ui.Blue);
                    else
                        Entry(d.Name + " manager", null, "No more tapping for this service, and its flights earn while the app is closed.",
                            "HIRE\n<size=34>" + Format.Money(s.ManagerCost(id)) + "</size>", s.HireBlocked(id), () => { if (s.Hire(id)) Toast(d.Name + " now runs itself"); }, Ui.Gold);
                }
                Entry("Earning while away", null, "With every service on a route managed, that route keeps flying for up to " + Format.Time(s.OfflineCapSeconds) + " with the app closed. Right now: " + Format.Money(s.IdleRate() * 60) + " a minute.", null, null, null);
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
                Entry(r.Name, Format.Time(s.ResearchLeft) + " LEFT", r.Blurb, "FINISH\n<size=34>" + s.ResearchSkipGems + " gems</size>", s.State.gems < s.ResearchSkipGems ? "Not enough gems" : null,
                    () => { s.SkipResearch(); }, Ui.Blue);
            }
            foreach (var branch in Content.Research.Select(r => r.Branch).Distinct())
            {
                Header(branch.ToUpper());
                foreach (var r in Content.Research.Where(x => x.Branch == branch))
                {
                    if (s.Researched(r.Id)) { Entry(r.Name, "DONE", r.Blurb, null, null, null); continue; }
                    if (s.State.research != null && s.State.research.id == r.Id) continue;
                    // Only the next project of a branch and the one after it are shown: the rest is a surprise.
                    if (r.Requires != null && !s.Researched(r.Requires) && Content.ResearchById(r.Requires).Requires != null && !s.Researched(Content.ResearchById(r.Requires).Requires)) continue;
                    Entry(r.Name, Format.Time(r.Seconds), r.Blurb, "RESEARCH\n<size=34>" + Format.Money(s.ResearchCost(r)) + "</size>", s.ResearchBlocked(r.Id),
                        () => { if (s.StartResearch(r.Id)) Toast("Research started: " + r.Name); }, Ui.Blue);
                }
            }
        }

        void EmpireSheet()
        {
            var s = _sim;
            Header("TODAY");
            for (int i = 0; i < s.State.tasks.Count; i++)
            {
                var task = s.State.tasks[i]; int k = i;
                bool done = task.progress >= task.target;
                Entry(Sim.TaskText(task), task.claimed ? "CLAIMED" : Format.Money(task.progress) + " / " + Format.Money(task.target), "Reward: " + task.gems + " gems. New tasks every day.",
                    task.claimed ? null : "CLAIM\n<size=34>" + task.gems + " gems</size>", done ? null : "Not done yet", () => { if (s.ClaimTask(k)) Toast("+" + task.gems + " gems"); }, Ui.Blue);
            }
            Header("BOOST");
            Entry("Double income", s.Boosted ? Format.Time(s.BoostLeft) + " LEFT" : null, "Everything the airport earns is doubled for " + Content.BoostMinutes + " minutes. Stacks up to 4 hours.",
                "BOOST\n<size=34>" + Content.BoostGems + " gems</size>", s.State.gems < Content.BoostGems ? "Not enough gems" : null, () => { if (s.BuyBoost()) Toast("Income doubled for " + Content.BoostMinutes + " minutes"); }, Ui.Gold);

            Header(s.Map.Name.ToUpper() + "  ·  " + s.State.wings + " WINGS (+" + Mathf.RoundToInt(s.State.wings * Content.WingBonus * 100) + "% income)");
            Entry("Sell your stake", s.Stars >= Content.SellStars ? "WORTH " + s.WingsOnSale + " WINGS" : null,
                "Sell this airport for Wings, which raise all income for good, and start a new one somewhere harder and richer. Research, gems and achievements stay with you."
                + (s.Stars < Content.SellStars ? " Reach " + Content.SellStars + " stars first." : ""), null, null, null);
            foreach (var m in Content.Maps)
            {
                if (m.Id == s.State.map) continue;
                bool run = s.State.mapsRun.Contains(m.Id);
                Entry(m.Name, run ? "RUN BEFORE" : m.WingsToOpen > 0 ? m.WingsToOpen + " WINGS" : null, m.Blurb + " " + m.Twist + "  Income x" + Format.Money(m.IncomeScale) + ".",
                    "MOVE", s.SellBlocked(m.Id), () => ConfirmSell(m), Ui.Gold);
            }

            Header("ACHIEVEMENTS");
            foreach (var a in Sim.Achievements.OrderBy(a => s.AchievementClaimed(a) ? 2 : s.AchievementDone(a) ? 0 : 1))
            {
                bool claimed = s.AchievementClaimed(a), done = s.AchievementDone(a);
                Entry(a.Name, claimed ? "CLAIMED" : Format.Money(System.Math.Min(a.Target, a.Progress(s))) + " / " + Format.Money(a.Target), a.Text,
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

        // ------------------------------------------------------------------ welcome back

        /// <summary>Shows what the airport earned while the app was closed, if it earned anything worth a card.</summary>
        public void OfferAway()
        {
            double away = GameRoot.Instance.AwaySeconds;
            double earned = _sim.OfflineEarnings(away);
            if (away < 60 || earned < 1) { GameRoot.Instance.ClearAway(); return; }
            if (_away != null) Destroy(_away.gameObject);
            _away = Ui.Img("Away", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _away.Fill(); _away.GetComponent<Image>().raycastTarget = true;
            var box = Ui.Img("Box", _away, Ui.Panel, true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
            Ui.Label("T", box, "WELCOME BACK", 64, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(820, 80));
            Ui.Label("S", box, "Your managers kept the airport running for " + Format.Time(System.Math.Min(away, _sim.OfflineCapSeconds)) + (away > _sim.OfflineCapSeconds ? " (the most they can without you)" : ""), 36, Ui.Dim, TextAnchor.MiddleCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(800, 110));
            Ui.Label("M", box, "+" + Format.Money(earned), 120, Ui.Gold).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(820, 150));
            var ok = Ui.Button("Collect", box, "COLLECT", Ui.Green, () =>
            {
                _sim.CollectOffline(GameRoot.Instance.AwaySeconds);
                GameRoot.Instance.ClearAway();
                GameRoot.Instance.Save();
                Destroy(_away.gameObject); _away = null;
            }, 56);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(520, 130));
        }
    }
}
