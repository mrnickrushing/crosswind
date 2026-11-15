using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// RECRUIT on the STAFF sheet, under the crates: the tokens held, the week's three on the board with their price
    /// and HIRE, when the board changes, and the one wanted pick (a grid of the whole team to choose from).
    /// </summary>
    public sealed partial class Hud
    {
        bool _pickWanted;

        static string Tokens(int n) { return n + (n == 1 ? " token" : " tokens"); }

        void RecruitSection()
        {
            var s = _sim;
            var board = s.RecruitBoard;
            Header("RECRUIT  ·  " + Tokens(s.State.recruitTokens).ToUpper() + "  ·  NEW BOARD IN " + Format.Time(s.RecruitLeft).ToUpper());
            Ico("tag", TintGold).Entry("Recruit tokens: " + s.State.recruitTokens, null,
                "One with every crate, more for a spare copy of someone at the top level. Hire anyone on this week's board, once each: Common "
                + Sim.RecruitPrice[0] + ", Rare " + Sim.RecruitPrice[1] + ", Epic " + Sim.RecruitPrice[2] + ", Legendary " + Sim.RecruitPrice[3] + " tokens.", null, null, null);

            foreach (var id in board)
            {
                var d = Sim.CrewById(id);
                int level = s.CrewLevel(id), cost = s.RecruitCost(id);
                bool known = level > 0, wanted = s.State.wanted == id, hired = s.State.boardHired.Contains(id);
                var tint = RarityColor[(int)d.Rarity];
                var row = Row(168);
                var tile = Face(row, d, new Vector2(0, 0.5f), new Vector2(88, 0), 140, true);
                if (known) Badge(tile, level + "", tint);
                string tag = "<size=28><color=#" + ColorUtility.ToHtmlStringRGB(tint) + ">" + RarityName[(int)d.Rarity] + "</color></size>"
                    + (wanted ? "  <size=28><color=#" + ColorUtility.ToHtmlStringRGB(Ui.Gold) + ">WANTED</color></size>" : "");
                var t = Ui.Label("Title", row, d.Name + "  " + tag, 38, Color.white, TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(176, 112); t.rectTransform.offsetMax = new Vector2(-250, -12);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 38; t.verticalOverflow = VerticalWrapMode.Truncate;
                string what = !known ? "joins your team: " + Sim.CrewPerk(d, 1)
                    : level >= Sim.MaxCrewLevel ? "already at the top level"
                    : "a copy toward level " + (level + 1) + " (" + s.CopiesToNext(id) + " more)";
                var b = Ui.Label("Body", row, d.Role + "  ·  " + what + (wanted ? "\nYour wanted pick: a quarter off." : ""), 28, Ui.Dim, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(176, 10); b.rectTransform.offsetMax = new Vector2(-250, -60);
                b.resizeTextForBestFit = true; b.resizeTextMinSize = 20; b.resizeTextMaxSize = 28; b.verticalOverflow = VerticalWrapMode.Truncate;
                string blocked = s.RecruitBlocked(id);
                var btn = Ui.Button("Hire", row, null, hired ? Ui.Green : blocked != null ? Locked : Ui.Gold, () =>
                {
                    if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); return; }
                    var got = s.Recruit(id);
                    if (got == null) return;
                    GameRoot.Instance.Save();
                    ShowCrate(got);
                    _dirty = true;
                }, 34);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(214, 118));
                ButtonFace(btn.transform, hired ? "HIRED" : "HIRE\n" + Tokens(cost).ToUpper(), hired ? null : blocked);
            }

            // The wanted pick: crates are unchanged; on the board they cost less and come at least one week in four.
            var w = Sim.CrewById(s.State.wanted);
            string rule = "Crates are unchanged. On the board your pick costs a quarter less, and is on it at least one week in " + Sim.WantedEvery + ".";
            if (w != null)
            {
                int due = s.WantedDueIn;
                string when = due == 0 ? "ON THE BOARD NOW" : due < 0 ? "AT THE TOP LEVEL" : due == 1 ? "ON THE BOARD NEXT WEEK AT THE LATEST" : "ON THE BOARD WITHIN " + due + " WEEKS";
                Pic(w.Id, RarityColor[(int)w.Rarity], s.CrewLevel(w.Id) > 0 ? s.CrewLevel(w.Id) + "" : null)
                    .Entry("Wanted: " + w.Name, when, rule, _pickWanted ? "DONE" : "CHANGE", null, () => _pickWanted = !_pickWanted, Ui.Blue);
            }
            else Ico("target", TintGold).Entry("Wanted: nobody yet", null, "Pick the one person you are working toward. " + rule, _pickWanted ? "DONE" : "CHOOSE", null, () => _pickWanted = !_pickWanted, Ui.Blue);
            if (_pickWanted) WantedPicker();
        }

        /// <summary>The whole team, eight to a row: a tap picks someone as wanted, a tap on the pick clears it.</summary>
        void WantedPicker()
        {
            var s = _sim;
            const int PerRow = 8;
            var all = Sim.Crew.OrderBy(c => (int)c.Rarity).ToList();
            for (int r = 0; r * PerRow < all.Count; r++)
            {
                var row = Row(176);
                for (int i = 0; i < PerRow && r * PerRow + i < all.Count; i++)
                {
                    var d = all[r * PerRow + i];
                    int level = s.CrewLevel(d.Id);
                    bool wanted = s.State.wanted == d.Id, top = level >= Sim.MaxCrewLevel;
                    var at = new Vector2((i + 0.5f) / PerRow, 0.5f);
                    var tile = Face(row, d, at, new Vector2(0, 16), 112, true);
                    if (level > 0) Badge(tile, level + "", RarityColor[(int)d.Rarity]);
                    if (top) Ui.Img("Dim", tile, new Color(0, 0, 0, 0.55f), true).rectTransform.Fill();
                    var name = Ui.Label("Name", row, d.Name.Split(' ').Last(), 22, wanted ? Ui.Gold : top ? Ui.Dim : Color.white);
                    name.rectTransform.Place(at, new Vector2(0.5f, 0.5f), new Vector2(0, -66), new Vector2(150, 30));
                    name.resizeTextForBestFit = true; name.resizeTextMinSize = 16; name.resizeTextMaxSize = 22;
                    string blocked = wanted ? null : s.WantedBlocked(d.Id);
                    var hit = Ui.Button("Pick", row, null, new Color(1, 1, 1, 0), () =>
                    {
                        if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); return; }
                        s.SetWanted(wanted ? "" : d.Id);
                        int due = s.WantedDueIn;
                        Toast(wanted ? "Nobody is wanted now" : due == 0 ? d.Name + " is wanted, and on the board this week" : d.Name + " is wanted: on the board within " + due + (due == 1 ? " week" : " weeks"));
                        _pickWanted = false;
                        _dirty = true;
                    });
                    ((RectTransform)hit.transform).Place(at, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(150, 170));
                }
            }
        }
    }
}
