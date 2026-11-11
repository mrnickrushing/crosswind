using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The team on the STAFF sheet: crates (free, earned, for gems; the odds are printed), who is on duty, and
    /// everyone you have or have yet to meet. Opening a crate shows the person on a card.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly Color[] RarityColor = { new Color(0.55f, 0.62f, 0.7f), new Color(0.25f, 0.56f, 0.95f), new Color(0.68f, 0.4f, 0.95f), new Color(1f, 0.74f, 0.2f) };
        static readonly string[] RarityName = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        RectTransform _crateCard;

        static string Initials(string name)
        {
            var parts = name.Split(' ').Where(p => p.Length > 0 && p != "Captain" && p != "Sir").ToArray();
            return parts.Length >= 2 ? "" + parts[0][0] + parts[parts.Length - 1][0] : name.Substring(0, 1);
        }

        /// <summary>
        /// A person's picture in its tile: a lit backdrop in a colour, the painted bust, a bevelled frame over the edge.
        /// Someone not met yet is a dark shape with a question mark.
        /// </summary>
        RectTransform FaceTile(Transform parent, string face, string initials, Color tint, Vector2 anchor, Vector2 pos, float size, bool known = true)
        {
            var bg = Ui.Img("Face", parent, known ? Color.Lerp(tint, Color.black, 0.22f) : new Color(0.2f, 0.24f, 0.3f)); bg.sprite = Ui.Art("face_bg");
            bg.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var sp = Portraits.Of(face);
            if (sp != null)
            {
                var pic = Ui.Img("Portrait", bg.transform, known ? Color.white : new Color(0.04f, 0.06f, 0.09f, 0.9f)); pic.sprite = sp;
                pic.rectTransform.Fill();
            }
            else if (known) Ui.Label("I", bg.transform, initials, Mathf.RoundToInt(size * 0.4f), Color.white).rectTransform.Fill();
            var frame = Ui.Img("Frame", bg.transform, known ? Color.Lerp(tint, Color.white, 0.35f) : new Color(0.38f, 0.43f, 0.5f)); frame.sprite = Ui.Art("face_frame");
            frame.rectTransform.Fill();
            if (!known) Ui.Label("Q", bg.transform, "?", Mathf.RoundToInt(size * 0.46f), new Color(1, 1, 1, 0.55f)).rectTransform.Fill();
            return bg.rectTransform;
        }

        /// <summary>A round badge on a tile's lower right corner (a level).</summary>
        static void Badge(RectTransform tile, string text, Color tint)
        {
            float d = Mathf.Max(40, tile.sizeDelta.x * 0.34f);
            var ring = Ui.Img("Badge", tile, Color.Lerp(tint, Color.white, 0.35f)); ring.sprite = Ui.Circle;
            ring.rectTransform.Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-d * 0.44f, d * 0.44f), new Vector2(d, d));
            var disc = Ui.Img("Disc", ring.transform, Ui.Ink); disc.sprite = Ui.Circle;
            disc.rectTransform.Fill(d * 0.09f);
            Ui.Label("N", disc.transform, text, Mathf.RoundToInt(d * 0.56f), Color.white).rectTransform.Fill();
        }

        RectTransform Face(Transform parent, CrewDef d, Vector2 anchor, Vector2 pos, float size, bool known)
        {
            return FaceTile(parent, d.Id, Initials(d.Name), RarityColor[(int)d.Rarity], anchor, pos, size, known);
        }

        void CrewSection()
        {
            var s = _sim;
            int have = Sim.Crew.Count(c => s.CrewLevel(c.Id) > 0), on = Sim.Crew.Count(c => s.OnDuty(c.Id));
            Header("YOUR TEAM  ·  " + have + " OF " + Sim.Crew.Count + "  ·  ON DUTY " + on + " OF " + s.DutyPlaces);
            if (s.State.crates > 0)
                Ico("box", TintGold).Entry("Team crate" + (s.State.crates > 1 ? "s: " + s.State.crates : ""), "ONE PERSON INSIDE", "Someone new, or another copy toward a level. A crate comes with every star.", "OPEN", null, OpenCrate, Ui.Gold);
            Ico("gift", TintGreen).Entry("Free crate", s.FreeCrateReady ? "READY" : "IN " + Format.Time(s.FreeCrateIn).ToUpper(), "One free crate every " + Sim.FreeCrateHours + " hours.", "COLLECT", s.FreeCrateReady ? null : "Not yet",
                () => { if (s.TakeFreeCrate()) OpenCrate(); }, Ui.Green);
            Ico("cart", TintLab).Entry("Buy a crate", null, "Odds: Common " + Sim.CrateOdds[0] + "%, Rare " + Sim.CrateOdds[1] + "%, Epic " + Sim.CrateOdds[2] + "%, Legendary " + Sim.CrateOdds[3] + "%. An Epic or better is certain by every " + Sim.CratePity + "th crate.",
                "BUY\n<size=34>" + Gems(Sim.CrateGems) + "</size>", s.State.gems >= Sim.CrateGems ? null : "Need " + Gems(Sim.CrateGems - s.State.gems) + " more", () => { if (s.BuyCrate()) OpenCrate(); }, Ui.Blue);

            foreach (var d in Sim.Crew.OrderByDescending(c => s.OnDuty(c.Id)).ThenByDescending(c => s.CrewLevel(c.Id) > 0).ThenByDescending(c => (int)c.Rarity))
            {
                int level = s.CrewLevel(d.Id); bool known = level > 0, duty = s.OnDuty(d.Id);
                var row = Row(168);
                var tile = Face(row, d, new Vector2(0, 0.5f), new Vector2(88, 0), 140, known);
                if (known) Badge(tile, level + "", RarityColor[(int)d.Rarity]);
                string tag = "<size=28><color=#" + ColorUtility.ToHtmlStringRGB(RarityColor[(int)d.Rarity]) + ">" + RarityName[(int)d.Rarity] + "</color></size>";
                var t = Ui.Label("Title", row, (known ? d.Name : "Not met yet") + "  " + tag, 38, known ? Color.white : Ui.Dim, TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(176, 0); t.rectTransform.offsetMax = new Vector2(-250, -12);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 38; t.verticalOverflow = VerticalWrapMode.Truncate;
                t.rectTransform.offsetMin = new Vector2(176, 112);
                string next = !known ? d.Role + "  ·  " + Sim.CrewPerk(d, 1) + " at level 1"
                    : d.Role + "  ·  level " + level + "  ·  " + Sim.CrewPerk(d, level) + (level >= Sim.MaxCrewLevel ? "  ·  top level" : "  ·  " + s.CopiesToNext(d.Id) + " more for level " + (level + 1));
                var b = Ui.Label("Body", row, next + (known ? "\n<i>\"" + d.Line + "\"</i>" : ""), 28, Ui.Dim, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(176, 10); b.rectTransform.offsetMax = new Vector2(-250, -60);
                b.resizeTextForBestFit = true; b.resizeTextMinSize = 20; b.resizeTextMaxSize = 28; b.verticalOverflow = VerticalWrapMode.Truncate;
                if (!known) continue;
                string blocked = duty ? null : s.DutyBlocked(d.Id);
                var btn = Ui.Button("Duty", row, null, duty ? Ui.Green : blocked != null ? Locked : Ui.Blue, () =>
                {
                    if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); return; }
                    if (s.ToggleDuty(d.Id)) Toast(d.Name + (s.OnDuty(d.Id) ? " is on duty: " + Sim.CrewPerk(d, level) : " is resting"));
                    _dirty = true;
                }, 34);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(214, 118));
                ButtonFace(btn.transform, duty ? "ON DUTY" : "PUT ON DUTY", blocked != null ? "No free place" : null);
            }
        }

        void OpenCrate()
        {
            var got = _sim.OpenCrate();
            if (got == null) return;
            GameRoot.Instance.Save();
            ShowCrate(got);
        }

        void ShowCrate(CrateResult got)
        {
            if (_crateCard != null) Destroy(_crateCard.gameObject);
            var d = got.Who; var tint = RarityColor[(int)d.Rarity];
            var shade = Ui.Img("Crate", _root, new Color(0, 0, 0, 0.72f));
            shade.raycastTarget = true;
            _crateCard = shade.rectTransform; _crateCard.Fill(); _crateCard.SetAsLastSibling();
            // A wide card (the game is played in landscape): the portrait on the left, who they are and what they bring on the right.
            const float W = 1180, H = 620, Left = 420;
            var card = Ui.ArtImg("Card", _crateCard, "panel", Color.white).rectTransform;
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            Ui.Img("Back", card, Ui.Ink, true).rectTransform.Fill(5);      // the panel art is glass: nothing behind may read through a reward
            var glow = Ui.ArtImg("Glow", card, "glow", new Color(tint.r, tint.g, tint.b, 0.5f), 0);
            glow.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Left * 0.5f + 10, 0), new Vector2(560, 560));
            var rays = Ui.ArtImg("Rays", card, "rays", new Color(tint.r, tint.g, tint.b, 0.45f), 0);
            rays.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Left * 0.5f + 10, 0), new Vector2(600, 600));
            rays.gameObject.AddComponent<Spin>();
            var tile = Face(card, d, new Vector2(0, 0.5f), new Vector2(Left * 0.5f + 10, 16), 360, true);
            if (!got.New && got.GemsBack <= 0) Badge(tile, got.Level + "", tint);
            var chip = Ui.Img("Rarity", card, tint, true).rectTransform;
            chip.Place(new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(Left * 0.5f + 10, 34), new Vector2(250, 54));
            Ui.Label("L", chip, RarityName[(int)d.Rarity], 34, Ui.Ink).rectTransform.Fill();
            System.Func<string, Text, float, float, Text> right = (name, l, top, height) =>
            {
                var rt = l.rectTransform;
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(Left + 20, 0); rt.offsetMax = new Vector2(-44, 0); rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -top); rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
                l.resizeTextForBestFit = true; l.resizeTextMinSize = 18; l.resizeTextMaxSize = l.fontSize; l.verticalOverflow = VerticalWrapMode.Truncate;
                return l;
            };
            right("Name", Ui.Label("Name", card, d.Name, 68, Color.white, TextAnchor.MiddleLeft), 44, 84);
            right("Role", Ui.Label("Role", card, d.Role.ToUpper(), 36, Color.Lerp(tint, Color.white, 0.25f), TextAnchor.MiddleLeft), 126, 46);
            right("Line", Ui.Label("Line", card, "<i>\"" + d.Line + "\"</i>", 32, Ui.Dim, TextAnchor.UpperLeft, false), 188, 84);
            // What the crate did: on its own plate so it reads as the result.
            var plate = Ui.ArtImg("Got", card, "well", Color.white).rectTransform;
            plate.anchorMin = new Vector2(0, 1); plate.anchorMax = new Vector2(1, 1); plate.pivot = new Vector2(0.5f, 1);
            plate.offsetMin = new Vector2(Left + 10, 0); plate.offsetMax = new Vector2(-36, 0); plate.anchoredPosition = new Vector2(plate.anchoredPosition.x, -288); plate.sizeDelta = new Vector2(plate.sizeDelta.x, 150);
            string head = got.GemsBack > 0 ? "ALREADY AT THE TOP LEVEL" : got.New ? "NEW ON YOUR TEAM" : got.LevelUp ? "LEVEL " + got.Level + "!" : "LEVEL " + got.Level;
            string what = got.GemsBack > 0 ? Gems(got.GemsBack) + " back instead"
                : got.New ? Sim.CrewPerk(d, 1) + (_sim.OnDuty(d.Id) ? "  ·  on duty from now" : "  ·  put them on duty in STAFF")
                : got.LevelUp ? Sim.CrewPerk(d, got.Level)
                : _sim.CopiesToNext(d.Id) + " more for level " + (got.Level + 1);
            bool good = got.New || got.LevelUp;
            var hl = Ui.Label("Head", plate, head, 44, good ? Ui.Gold : Color.white, TextAnchor.MiddleLeft);
            hl.rectTransform.anchorMin = new Vector2(0, 0.5f); hl.rectTransform.anchorMax = new Vector2(1, 1); hl.rectTransform.offsetMin = new Vector2(26, 0); hl.rectTransform.offsetMax = new Vector2(-20, -8);
            hl.resizeTextForBestFit = true; hl.resizeTextMinSize = 20; hl.resizeTextMaxSize = 44;
            var wl = Ui.Label("What", plate, what, 34, Color.white, TextAnchor.MiddleLeft, false);
            wl.rectTransform.anchorMin = new Vector2(0, 0); wl.rectTransform.anchorMax = new Vector2(1, 0.5f); wl.rectTransform.offsetMin = new Vector2(26, 10); wl.rectTransform.offsetMax = new Vector2(-20, 0);
            wl.resizeTextForBestFit = true; wl.resizeTextMinSize = 18; wl.resizeTextMaxSize = 34;
            bool more = _sim.State.crates > 0;
            System.Action shut = () => { Destroy(_crateCard.gameObject); _crateCard = null; _dirty = true; };
            var ok = Ui.Button("Ok", card, more ? "OPEN ANOTHER (" + _sim.State.crates + ")" : "GOOD", more ? Ui.Gold : Ui.Green, () => { shut(); if (more) OpenCrate(); }, 44);
            ((RectTransform)ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 34), new Vector2(more ? 470 : 360, 110));
            if (more)
            {
                var close = Ui.Button("Close", card, "DONE", Locked, shut, 40);
                ((RectTransform)close.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36 - 470 - 16, 34), new Vector2(210, 110));
            }
            Sfx.Play(d.Rarity >= Rarity.Epic ? "star" : "chime", 0.8f);
        }
    }

    /// <summary>Turns slowly, whatever the game's speed (the rays behind a reward).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = 20;
        void Update() { transform.Rotate(0, 0, Speed * Time.unscaledDeltaTime); }
    }

    /// <summary>
    /// The staff's painted portraits (Resources/UI/Faces, cut from the sheets by Tools/art/portraits.py): the team by
    /// their id, a department's manager as <c>mgr_&lt;facility&gt;</c>. Null when there is no picture by that name.
    /// </summary>
    public static class Portraits
    {
        static readonly Dictionary<string, Sprite> _made = new Dictionary<string, Sprite>();

        public static Sprite Of(string id)
        {
            Sprite s;
            if (!_made.TryGetValue(id, out s)) _made[id] = s = Resources.Load<Sprite>("UI/Faces/" + id);
            return s;
        }
    }
}
