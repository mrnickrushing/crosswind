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
            else
            {
                Header("MANAGERS RUN A DEPARTMENT FOR YOU, EVEN WHILE YOU'RE AWAY");
                foreach (var id in Content.FacilityOrder)
                {
                    var d = Content.Facilities[id];
                    if (d.Group != "Services") continue;
                    bool hired = s.State.managers.Contains(id);
                    Entry(d.Name + " manager", hired ? "HIRED" : null, hired ? "Sends the crews out the moment an aircraft parks." : "No more tapping for this service, and its flights earn while the app is closed.",
                        hired ? null : "HIRE\n<size=34>" + Format.Money(s.ManagerCost(id)) + "</size>", hired ? null : s.HireBlocked(id), () => { if (s.Hire(id)) Toast(d.Name + " now runs itself"); }, Ui.Gold);
                }
                Entry("Earning while away", null, "With every service on a route managed, that route keeps flying for up to " + Content.OfflineHours + " hours with the app closed. Right now: " + Format.Money(s.IdleRate() * 60) + " a minute.", null, null, null);
            }
            _sheetList.sizeDelta = new Vector2(0, _rowY);
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
            Ui.Label("S", box, "Your managers kept the airport running for " + Format.Time(System.Math.Min(away, Content.OfflineHours * 3600)) + (away > Content.OfflineHours * 3600 ? " (the most they can without you)" : ""), 36, Ui.Dim, TextAnchor.MiddleCenter, false)
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
