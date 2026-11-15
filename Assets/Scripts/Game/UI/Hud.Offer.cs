using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The card a shop row opens before anything is bought: what is spent once and what is kept for good, side by side,
    /// and for a Time Warp exactly what this airport would get right now (the money after the bank's share, and the
    /// builds, research and timers it would finish). BUY goes to the App Store from here.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _offer;

        void OpenOffer(ProductDef prod)
        {
            CloseOffer();
            Telemetry.OfferViewed(prod.Id);
            var s = _sim;
            _offer = Ui.Img("Offer", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _offer.Fill(); _offer.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _offer, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 700));
            var back = Ui.Img("Back", box, new Color(0.07f, 0.1f, 0.15f, 1f), true).rectTransform; back.Fill(10);
            Ui.Label("T", box, prod.Name.ToUpper(), 54, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(900, 70));
            Ui.Label("P", box, Store.Price(prod), 34, Ui.Gold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -102), new Vector2(900, 44));

            var once = new List<string>(); var keep = new List<string>();
            s.ProductContents(prod, once, keep);
            string extra = null;
            if (prod.WarpHours > 0)
            {
                // What this airport would get right now.
                var w = s.PreviewWarp(prod.WarpHours * 3600);
                once.Clear();
                once.Add("+" + Format.Money(w.Money) + " now" + (w.ToBank > 0 ? " (" + Format.Money(w.ToBank) + " more goes to the bank)" : ""));
                foreach (var f in w.Finishes) once.Add("finishes " + f);
                if (w.Finishes.Count == 0) once.Add("every build, study and timer " + prod.WarpHours + " hours on (none would finish yet)");
                extra = "Worked out from your airport as it is now: it earns " + Format.Money(s.WarpEarnings(60)) + " a minute.";
            }
            Column(box, 0, "SPENT ONCE", once, Ui.Blue);
            Column(box, 1, "KEPT FOR GOOD", keep, Ui.Green);
            if (extra != null) Ui.Label("Note", box, extra, 24, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(900, 40));

            bool owned = prod.Once && s.Owns(prod.Id);
            var buy = Ui.Button("Buy", box, owned ? "OWNED" : "BUY  " + Store.Price(prod), owned ? Ui.Off : Ui.Gold, () =>
            {
                if (owned) return;
                CloseOffer();
                Store.Buy(prod, (ok, msg) => { Toast(msg); if (ok) Sfx.Play("confirm"); _dirty = true; });
            }, 42);
            ((RectTransform)buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(190, 40), new Vector2(420, 112));
            if (!owned) buy.GetComponentInChildren<Text>().color = Ui.Ink;
            var no = Ui.Button("No", box, "NOT NOW", Ui.Off, CloseOffer, 38);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-230, 40), new Vector2(340, 112));
            _offer.SetAsLastSibling();
        }

        /// <summary>One of the card's two columns: a heading and its lines (a dash when there is nothing).</summary>
        static void Column(RectTransform box, int i, string head, List<string> lines, Color tint)
        {
            var col = Ui.Img("Col", box, new Color(1, 1, 1, 0.05f), true).rectTransform;
            col.anchorMin = new Vector2(i * 0.5f, 0); col.anchorMax = new Vector2(i * 0.5f + 0.5f, 1);
            col.offsetMin = new Vector2(i == 0 ? 40 : 10, 214); col.offsetMax = new Vector2(i == 0 ? -10 : -40, -160);
            var h = Ui.Img("Head", col, tint, true).rectTransform; h.Band(true, 0, 54);
            Ui.Label("H", h, head, 28, Color.white).rectTransform.Fill();
            var body = Ui.Label("Lines", col, lines.Count == 0 ? "<color=#FFFFFF60>Nothing</color>" : "• " + string.Join("\n• ", lines), 28, Color.white, TextAnchor.UpperLeft, false);
            body.rectTransform.Fill(); body.rectTransform.offsetMin = new Vector2(20, 14); body.rectTransform.offsetMax = new Vector2(-16, -70);
            body.resizeTextForBestFit = true; body.resizeTextMinSize = 18; body.resizeTextMaxSize = 28; body.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void CloseOffer() { if (_offer != null) { Destroy(_offer.gameObject); _offer = null; } }
    }
}
