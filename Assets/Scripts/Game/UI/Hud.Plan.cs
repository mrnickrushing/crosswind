using System.Linq;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The day planner on the AIRLINES sheet: a bar for every hour of the day against what the airport can turn
    /// around, a slot picker for each route, and AUTO PLAN for those who'd rather not.
    /// </summary>
    public sealed partial class Hud
    {
        void PlanSection()
        {
            var s = _sim;
            if (!s.PlannerOpen)
            {
                Header("DAY PLANNER");
                Ico("calendar").Entry("Day planner", "OPENS AT " + Sim.PlannerStars + " STARS", "Give each route a slot in the day. Flights in a slot pay up to 60% more, but a crowded hour chokes the runway.", null, null, null);
                return;
            }
            var load = s.HourlyLoad(); float cap = s.HourlyCapacity();
            int worst = s.WorstHour();
            Header(worst >= 0 ? "DAY PLANNER  ·  <color=#FF8070>" + worst.ToString("00") + ":00 IS OVER CAPACITY</color>" : "DAY PLANNER  ·  THE DAY FITS");

            var row = Row(360);
            float top = Mathf.Max(cap * 1.35f, load.Max() * 1.08f, 1);
            var chart = Ui.Rect("Chart", row);
            chart.anchorMin = new Vector2(0, 0); chart.anchorMax = new Vector2(1, 1); chart.offsetMin = new Vector2(28, 58); chart.offsetMax = new Vector2(-28, -96);
            int now = Mathf.FloorToInt(s.Hour) % 24;
            for (int h = 0; h < 24; h++)
            {
                bool over = cap > 0 && load[h] > cap * 1.0001f;
                var bar = Ui.Img("Bar", chart, over ? Ui.Red : h == now ? Ui.Gold : Ui.Blue).rectTransform;
                bar.anchorMin = new Vector2(h / 24f, 0); bar.anchorMax = new Vector2((h + 1) / 24f, Mathf.Max(0.012f, load[h] / top));
                bar.offsetMin = new Vector2(3, 0); bar.offsetMax = new Vector2(-3, 0);
                if (h % 6 == 0)
                {
                    var t = Ui.Label("Hour", chart, h.ToString("00"), 26, Ui.Dim, TextAnchor.UpperLeft, false).rectTransform;
                    t.anchorMin = new Vector2(h / 24f, 0); t.anchorMax = new Vector2(h / 24f, 0); t.pivot = new Vector2(0, 1);
                    t.anchoredPosition = new Vector2(3, -6); t.sizeDelta = new Vector2(80, 34);
                }
            }
            var line = Ui.Img("Capacity", chart, new Color(1, 1, 1, 0.85f)).rectTransform;
            line.anchorMin = new Vector2(0, cap / top); line.anchorMax = new Vector2(1, cap / top); line.offsetMin = new Vector2(0, -2); line.offsetMax = new Vector2(0, 2);
            var capLabel = Ui.Label("Cap", chart, "CAN HANDLE " + Mathf.RoundToInt(cap) + " AN HOUR", 24, Ui.Dim, TextAnchor.LowerRight).rectTransform;
            capLabel.anchorMin = new Vector2(0, cap / top); capLabel.anchorMax = new Vector2(1, cap / top); capLabel.pivot = new Vector2(0.5f, 0);
            capLabel.anchoredPosition = new Vector2(0, 4); capLabel.sizeDelta = new Vector2(0, 30);

            var title = Ui.Label("Title", row, "Flights an hour", 38, Color.white, TextAnchor.UpperLeft).rectTransform;
            title.Fill(); title.offsetMin = new Vector2(28, 0); title.offsetMax = new Vector2(-330, -14);
            var sub = Ui.Label("Sub", row, "Now " + now.ToString("00") + ":00  ·  red hours run late", 26, Ui.Dim, TextAnchor.UpperLeft, false).rectTransform;
            sub.Fill(); sub.offsetMin = new Vector2(28, 0); sub.offsetMax = new Vector2(-330, -58);
            var auto = Ui.Button("Auto", row, "AUTO PLAN", Ui.Green, () =>
            {
                int moved = s.AutoPlan();
                Toast(moved > 0 ? "Planned the day: " + moved + (moved == 1 ? " route moved" : " routes moved") : (s.State.contracts.Exists(c => c.slot > 0) ? "The day is already planned as well as it fits" : "No slot has room yet: more stands or a faster runway make room"));
                Sfx.Play("build", 0.7f); _dirty = true;
            }, 36);
            ((RectTransform)auto.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -14), new Vector2(290, 72));

            foreach (var c in s.State.contracts.ToList())
            {
                var r = Content.Routes[c.route];
                var slotRow = Row(176);
                string info = c.slot == 0 ? "all day at the usual pace" : Sim.SlotHours[c.slot] + "  ·  pays " + Mathf.RoundToInt(Sim.SlotPay(r, c.slot) * 100) + "%  ·  3x the pace";
                var name = Ui.Label("Name", slotRow, r.Destination + "  <size=28><color=#FFFFFF90>" + info + "</color></size>", 36, Color.white, TextAnchor.UpperLeft).rectTransform;
                name.Fill(); name.offsetMin = new Vector2(24, 0); name.offsetMax = new Vector2(-16, -12);
                for (int slot = 0; slot < 5; slot++)
                {
                    int pick = slot; string blocked = s.SlotBlocked(r.Id, slot); bool on = c.slot == slot;
                    var b = Ui.Button("Slot", slotRow, null, on ? Ui.Green : blocked != null ? Ui.Off : Ui.Ink, () =>
                    {
                        if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); return; }
                        if (s.SetSlot(r.Id, pick)) Toast(r.Destination + ": " + Sim.SlotName[pick].ToLower() + (pick > 0 ? ", each flight pays " + Mathf.RoundToInt(Sim.SlotPay(r, pick) * 100) + "%" : ""));
                        _dirty = true;
                    }, 30);
                    var rt = (RectTransform)b.transform;
                    rt.anchorMin = new Vector2(slot / 5f, 0); rt.anchorMax = new Vector2((slot + 1) / 5f, 0); rt.pivot = new Vector2(0.5f, 0);
                    rt.offsetMin = new Vector2(slot == 0 ? 18 : 6, 14); rt.offsetMax = new Vector2(slot == 4 ? -18 : -6, 100);
                    var l = Ui.Label("L", b.transform, Sim.SlotName[slot] + (slot > 0 ? "\n<size=24>+" + Mathf.RoundToInt((Sim.SlotPay(r, slot) - 1) * 100) + "%</size>" : "\n<size=24>steady</size>"), 30, blocked != null ? Ui.Dim : Color.white);
                    l.rectTransform.Fill(4); l.resizeTextForBestFit = true; l.resizeTextMinSize = 18; l.resizeTextMaxSize = 30;
                }
            }
        }
    }
}
