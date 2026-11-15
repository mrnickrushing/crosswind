using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// YOUR AIRLINE in AIRLINES: its name in the heading, then rows to rename it and to cycle its colour, trim,
    /// pattern and tail emblem (Sim.Livery). Patterns and emblems still to earn are listed with what opens them.
    /// New aircraft and the next arrivals wear the new paint; the ones already on the ground keep theirs.
    /// </summary>
    public sealed partial class Hud
    {
        void LiverySection()
        {
            var s = _sim;
            var main = Palette.Hex(Sim.LiveryHex[Mathf.Clamp(s.State.livery, 0, Sim.LiveryHex.Length - 1)]);
            int ti = Sim.TrimColour(s.State.liveryTrim);
            var trim = ti >= 0 ? Palette.Hex(Sim.LiveryHex[ti]) : MeshBuilder.Lighten(main, 0.45f);
            const string Paint = "  New aircraft and the next arrivals wear it.";

            Header(s.AirlineName.ToUpper() + "  ·  " + s.FleetSize + " AIRCRAFT  ·  EACH FLIGHT PAYS " + Mathf.RoundToInt((float)Sim.OwnPay * 100) + "%");
            Ico("pen", main).Entry("Name: " + s.AirlineName, null, "Up to " + Sim.AirlineNameMax + " letters. Leave it empty for \"" + Sim.DefaultAirlineName + "\".", "RENAME", null,
                () => Prompt("AIRLINE NAME", Sim.DefaultAirlineName, s.State.airlineName, Sim.AirlineNameMax, false, v =>
                {
                    if (s.RenameAirline(v)) { GameRoot.Instance.Save(); Toast("Your airline: " + s.AirlineName); Sfx.Play("confirm"); }
                    else { Toast("That name can't be used"); Sfx.Play("error", 0.5f); }
                    _dirty = true;
                }), Ui.Blue);
            Ico("paint", main).Entry("Colour: " + Sim.Liveries[s.State.livery], null, "Your aircraft fly in your colours." + Paint, "CHANGE", null,
                () => { s.SetLivery(s.State.livery + 1); Toast("Colour: " + Sim.Liveries[s.State.livery]); }, main);
            Ico("paint", trim).Entry("Trim: " + s.TrimName, null, "The second colour, for stripes, engines and the emblem. Matched picks a shade that suits the pattern." + Paint, "CHANGE", null,
                () => { s.SetTrim(s.State.liveryTrim + 1); Toast("Trim: " + s.TrimName); }, trim);
            int open = s.OpenCount(false);
            Ico("tag", main).Entry("Pattern: " + s.Pattern.Name, open + " OF " + Sim.LiveryPatterns.Length + " OPEN", s.Pattern.Blurb + StillToEarn(Sim.LiveryPatterns, s.PatternBlocked), "NEXT",
                open > 1 ? null : "Earn stars to open more", () => { s.SetPattern(s.NextPattern()); Toast("Pattern: " + s.Pattern.Name); }, Ui.Card);
            open = s.OpenCount(true);
            Ico("star", trim).Entry("Emblem: " + s.Emblem.Name, open + " OF " + Sim.Emblems.Length + " OPEN", s.Emblem.Blurb + StillToEarn(Sim.Emblems, s.EmblemBlocked), "NEXT",
                open > 1 ? null : "Earn stars to open more", () => { s.SetEmblem(s.NextEmblem()); Toast("Emblem: " + s.Emblem.Name); }, Ui.Card);
        }

        /// <summary>"  Still to earn: Swoosh at 4 stars, Full Colour with 5 aircraft." (empty when all are open)</summary>
        static string StillToEarn(LiveryItem[] items, System.Func<int, string> blocked)
        {
            var shut = new List<string>();
            for (int i = 0; i < items.Length; i++)
                if (blocked(i) != null) shut.Add(items[i].Name + (items[i].Stars > 0 ? " at " + items[i].Stars + " stars" : " with " + items[i].Fleet + " aircraft"));
            return shut.Count == 0 ? "" : "  Still to earn: " + string.Join(", ", shut) + ".";
        }
    }
}
