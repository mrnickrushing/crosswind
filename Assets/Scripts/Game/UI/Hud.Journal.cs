using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>EMPIRE's JOURNAL (the firsts, page by page, and their gems) and HALL OF FAME (every airport sold, with its trophy and last photo).</summary>
    public sealed partial class Hud
    {
        static string PageTitle(string page)
        {
            switch (page) { case Sim.PageAirlines: return "Airlines"; case Sim.PageClasses: return "Aircraft"; case Sim.PageContracts: return "Contracts"; }
            return "Milestones";
        }

        static string PageIcon(string page)
        {
            switch (page) { case Sim.PageAirlines: return "handshake"; case Sim.PageClasses: return "runway"; case Sim.PageContracts: return "pen"; }
            return "medal";
        }

        static string MapName(string id) { var m = Content.Maps.Find(x => x.Id == id); return m != null ? m.Name : id; }

        /// <summary>When a first happened: its airport day, and the airport when it was another one.</summary>
        string When(JournalEntry e)
        {
            if (e.day <= 0) return "before the journal";
            return "day " + e.day + (e.map != _sim.State.map ? ", " + MapName(e.map) : "");
        }

        void JournalSection()
        {
            var s = _sim;
            int n = s.State.journal.Count;
            Header("JOURNAL  ·  " + n + (n == 1 ? " FIRST" : " FIRSTS") + " RECORDED");
            foreach (var page in Sim.JournalPages.Concat(new[] { Sim.JournalMilestone }))
            {
                var items = Sim.PageItems(page);
                int seen = s.PageSeen(page), gems = Sim.PageGems(page);
                bool paid = s.PagePaid(page), milestones = page == Sim.JournalMilestone;
                string body = string.Join("  ·  ", items.Select(i =>
                {
                    var e = s.Noted(page, i);
                    if (e != null) return "<color=#FFFFFF>" + Sim.JournalName(page, i) + "</color> <color=#FFFFFF80>(" + When(e) + ")</color>";
                    return milestones ? "<color=#FFFFFF50>" + Sim.JournalName(page, i) + "</color>" : "<color=#FFFFFF50>?</color>";
                }));
                if (!milestones && !paid) body += "\nA full page pays " + Gems(gems) + ", once.";
                string tag = seen + " / " + items.Count + (paid ? "  ·  COLLECTED" : "");
                var bar = Ico(PageIcon(page), TintGold).Bar(seen / (float)Mathf.Max(1, items.Count), seen >= items.Count ? Ui.Gold : Ui.Green);
                if (milestones || paid) bar.Entry(PageTitle(page), tag, body, null, null, null);
                else bar.Entry(PageTitle(page), tag, body, "COLLECT\n" + Gems(gems), s.ClaimPageBlocked(page), () =>
                {
                    int got = s.ClaimPage(page);
                    if (got > 0) { Sfx.Play("confirm"); Toast(PageTitle(page) + " page complete: +" + Gems(got)); }
                }, Ui.Gold);
            }
        }

        void HallOfFameSection()
        {
            var s = _sim; var hall = s.State.legacy;
            int t = s.State.trophies;
            Header("HALL OF FAME  ·  " + t + (t == 1 ? " TROPHY" : " TROPHIES"));
            if (hall.Count == 0 && t == 0)
            {
                Ico("trophy", TintGold).Entry("No airport sold yet", null, "Sell an airport and it takes its place here for good: its stars, its flights, its best contract and the last photo you took there. Every sale adds a trophy.", null, null, null);
                return;
            }
            // The trophies: a gold star for every airport sold.
            var row = Row(120);
            const int fit = 12;
            for (int i = 0; i < Mathf.Min(t, fit); i++)
                Ui.ArtImg("Trophy", row, "star", Color.white, 0).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24 + i * 92, 0), new Vector2(84, 84));
            if (t > fit)
                Ui.Label("More", row, "+" + (t - fit), 40, Ui.Gold, TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24 + fit * 92, 0), new Vector2(160, 84));

            for (int i = hall.Count - 1; i >= 0; i--)
            {
                var l = hall[i];
                string body;
                if (l.before) body = "Sold before the hall of fame was kept, so only its name is known.";
                else
                {
                    body = Format.Money(l.flights) + " flights  ·  earned " + Format.Money(l.earned) + "  ·  " + l.daysAtA + (l.daysAtA == 1 ? " day" : " days") + " at grade A  ·  "
                        + l.items + (l.items == 1 ? " terminal item" : " terminal items");
                    AirlineDef a;
                    if (!string.IsNullOrEmpty(l.bestAirline) && Content.Airlines.TryGetValue(l.bestAirline, out a))
                        body += "\nBest contract: " + a.Name + " " + a.Code + " " + l.bestNumber + ", a " + Sim.KindName(l.bestKind).ToLower() + " that earned " + l.bestRep + " reputation";
                }
                Ico("trophy", TintGold).Entry(l.name, l.before ? "SOLD" : l.stars + (l.stars == 1 ? " STAR" : " STARS"), body, null, null, null);
                var shot = Thumb(l.photo);
                if (shot != null) PutThumb(LastRow(), shot);
            }
        }

        RectTransform LastRow() { var list = _list ?? _sheetList; return (RectTransform)list.GetChild(list.childCount - 1); }

        /// <summary>The photo over the row's icon tile, framed, at its own shape.</summary>
        static void PutThumb(RectTransform row, Texture2D tex)
        {
            float aspect = tex.width / (float)Mathf.Max(1, tex.height), w = 108, h = 108;
            if (aspect >= 1) h = w / aspect; else w = h * aspect;
            var frame = Ui.Img("Photo", row, Ui.Gold, true).rectTransform;
            frame.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72, 0), new Vector2(w + 8, h + 8));
            var pic = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            pic.transform.SetParent(frame, false);
            pic.texture = tex; pic.raycastTarget = false;
            pic.rectTransform.Fill(4);
        }

        // Photos read once per launch: null when the file is not on this device.
        static readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();

        static Texture2D Thumb(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Texture2D tex;
            if (_thumbs.TryGetValue(path, out tex)) return tex;
            tex = null;
            try
            {
                if (System.IO.File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path), true)) { Destroy(tex); tex = null; }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Hall of fame photo not read: " + e.Message); tex = null; }
            return _thumbs[path] = tex;
        }

        /// <summary>
        /// The last photo photo mode saved, for the hall of fame. Read by name (Hud.LastPhoto, a static string or
        /// property) so this builds whether or not photo mode is in the build; null when there is none.
        /// </summary>
        static string LastPhotoPath()
        {
            const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var f = typeof(Hud).GetField("LastPhoto", any);
            if (f != null) return f.GetValue(null) as string;
            var p = typeof(Hud).GetProperty("LastPhoto", any);
            return p != null ? p.GetValue(null, null) as string : null;
        }
    }
}
