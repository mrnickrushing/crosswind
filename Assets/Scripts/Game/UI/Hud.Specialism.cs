using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>The SPECIALISATION section of BUILD: the four, what each gives and takes, and CHOOSE (the first one free).</summary>
    public sealed partial class Hud
    {
        void SpecialismSection()
        {
            var s = _sim;
            // Shown from a star before it opens, so the choice is in view while the player builds toward it.
            if (s.Stars < Sim.SpecStars - 1 && s.Specialism == "") return;
            var mine = Sim.SpecialismById(s.Specialism);
            double wait = s.SpecialismWait, cost = s.SpecialismCost;
            Header("SPECIALISATION  ·  " + (mine == null ? (s.Stars < Sim.SpecStars ? "OPENS AT " + Sim.SpecStars + " STARS" : "CHOOSE ONE: THE FIRST IS FREE")
                : mine.Name.ToUpper() + (wait > 0 ? "  ·  CHANGE IN " + Format.Time(wait).ToUpper() : "  ·  CHANGE FOR " + Format.Money(cost))));
            foreach (var d in Sim.Specialisms)
            {
                bool current = s.Specialised(d.Id);
                string body = d.Blurb + "\n<color=#7FE0A0>+ " + string.Join(".  + ", d.Pros) + ".</color>\n<color=#FF9A80>- " + string.Join(".  - ", d.Cons) + ".</color>";
                if (current) Bar(1, Ui.Gold);
                string id = d.Id;
                Ico(d.Icon, current ? TintGold : TintPlain).Entry(d.Name, current ? "YOUR SPECIALISATION" : null, body,
                    current ? null : "CHOOSE\n" + (cost <= 0 ? "FREE" : Format.Money(cost)), current ? null : s.SpecialismBlocked(id),
                    () => { if (s.ChooseSpecialism(id)) Sfx.Play("confirm"); }, Ui.Gold);
            }
        }
    }
}
