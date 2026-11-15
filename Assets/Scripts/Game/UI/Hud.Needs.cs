using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// A tap on one of the passengers' ratings (the strip along the foot of the terminal, or a row in BUILD): what holds
    /// it back, said specifically, and the way to fix it. For the queues that is the part of the hall limiting the flow,
    /// with INSPECT (the camera goes to it) and its upgrade; for everything else, where it is missing on the floor and
    /// the catalogue page that sells it.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _needCard, _needButtons;
        Text _needText;
        int _need = -1;
        float _needIn;

        void BuildNeedCard()
        {
            _needCard = Ui.ArtImg("Need card", _safe, "panel", Color.white).rectTransform;
            _needCard.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 124), new Vector2(1260, 170));
            _needCard.GetComponent<Image>().raycastTarget = true;
            _needText = Ui.Label("Text", _needCard, "", 26, Color.white, TextAnchor.MiddleLeft, false);
            _needText.rectTransform.Fill(); _needText.rectTransform.offsetMin = new Vector2(24, 10); _needText.rectTransform.offsetMax = new Vector2(-560, -10);
            _needText.resizeTextForBestFit = true; _needText.resizeTextMinSize = 18; _needText.resizeTextMaxSize = 26; _needText.verticalOverflow = VerticalWrapMode.Truncate;
            _needButtons = Ui.Rect("Buttons", _needCard);
            _needButtons.anchorMin = new Vector2(1, 0); _needButtons.anchorMax = new Vector2(1, 1); _needButtons.pivot = new Vector2(1, 0.5f);
            _needButtons.offsetMin = new Vector2(-548, 16); _needButtons.offsetMax = new Vector2(-14, -16);
            var x = Ui.Button("Close", _needCard, "X", new Color(0, 0, 0, 0), CloseNeed, 30);
            ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(2, 2), new Vector2(54, 50));
            _needCard.gameObject.SetActive(false);
        }

        /// <summary>The catalogue page that sells what answers a want.</summary>
        static string ShopGroupFor(Want w)
        {
            switch (w)
            {
                case Want.Food: return "Food";
                case Want.Shopping: return "Shops";
                case Want.Toilets: case Want.Clean: return "Hygiene";
                case Want.Fun: return "Entertainment";
                case Want.Info: return "Information";
                default: return "Comfort";
            }
        }

        static string NeedWord(Need n) { return n.Queue ? "QUEUES" : n.Name.ToUpper(); }

        /// <summary>Opens the card for one of the passengers' needs (an index into <see cref="Sim.Needs"/>).</summary>
        void OpenNeed(int i)
        {
            if (i < 0 || i >= _sim.Needs.Count) return;
            ClosePick(); _pax = null; _paxCard.gameObject.SetActive(false);
            _need = i; _needIn = 0;
            _needCard.gameObject.SetActive(true);
            Sfx.Play("click", 0.4f);
            Telemetry.Feature("need_card", _sim.Needs[i].Queue ? "queues" : _sim.Needs[i].Want.ToString());
        }

        void CloseNeed()
        {
            _need = -1;
            if (_needCard != null) _needCard.gameObject.SetActive(false);
            NeedOverlay(null);
        }

        /// <summary>Opens the terminal catalogue on a page (going inside first when needed).</summary>
        void OpenShopGroup(string group)
        {
            GoInside(true);
            _shopGroup = group;
            ClosePick(); CloseNeed();
            OpenTab(ShopTab);
        }

        /// <summary>Looks at the part of the hall holding the queues up.</summary>
        void InspectPart(string part)
        {
            GoInside(true);
            if (Interior.Instance == null) return;
            var at = Interior.Instance.PartPoint(part);
            CameraRig.Instance.Focus = new Vector3(at.x, 0, CameraRig.Instance.Focus.z);
            if (CameraRig.Instance.Distance > 140) CameraRig.Instance.Distance = 140;
            Sfx.Play("open", 0.4f);
        }

        void UpdateNeedCard()
        {
            if (_need < 0) return;
            if (!CameraRig.Inside || _tab >= 0 || _placing != null) { CloseNeed(); return; }
            if ((_needIn -= Time.unscaledDeltaTime) > 0) return;
            _needIn = 0.5f;
            var needs = _sim.Needs;
            if (_need >= needs.Count) { CloseNeed(); return; }
            var n = needs[_need];
            string pct = n.Unasked ? "NOT NEEDED YET" : Mathf.RoundToInt(n.Value * 100) + "%";
            string col = n.Unasked ? "#FFFFFF90" : n.Value >= 0.8f ? "#5ED58A" : n.Value >= 0.5f ? "#FFC83D" : "#FF7A6B";
            string body = n.Unasked ? "An airport this size gets by without. Passengers will look for it as their numbers grow."
                : n.Value >= 0.999f ? "Passengers have no complaints here." : n.Advice + ".";
            _needText.text = "<size=34><b>" + NeedWord(n) + "</b></size>  <color=" + col + ">" + pct + "</color>" + NeedCoverageLine(n) + "\n<color=#FFFFFFC0>" + body + "</color>";
            Ui.Clear(_needButtons);
            if (n.Queue)
            {
                var limit = _sim.FlowLimiters;
                string part = limit[0], fix = _sim.FlowFix;
                NeedButton(0, "INSPECT\n" + PartLabel(part), null, Ui.Blue, () => InspectPart(part));
                if (fix == null) NeedButton(1, "UPGRADE", "Being built", Ui.Green, null);
                else
                {
                    string blocked = _sim.UpgradeBlocked(fix);
                    NeedButton(1, "UPGRADE " + PartLabel(fix) + "\n" + Format.Money(_sim.Cost(fix)), blocked, Ui.Green, () =>
                    {
                        float secs = _sim.BuildSeconds(fix);
                        if (_sim.Upgrade(fix)) { Sfx.Play("build", 0.7f); Toast(secs > 0 ? Content.Facilities[fix].Name + ": the crew has started" : Content.Facilities[fix].Name + " upgraded to level " + _sim.Level(fix)); if (secs > 60) Reminders.AskOnce(); _needIn = 0; }
                    });
                }
                return;
            }
            var want = n.Want;
            NeedButton(0, _overlayWant == (int)want ? "HIDE\nTHE MAP" : "SHOW\nON THE FLOOR", null, Ui.Blue, () => { NeedOverlay(_overlayWant == (int)want ? (Want?)null : want); _needIn = 0; });
            NeedButton(1, "SHOP\n" + ShopGroupFor(want).ToUpper(), null, Ui.Green, () => OpenShopGroup(ShopGroupFor(want)));
        }

        static string PartLabel(string id) { return id == "checkin" ? "CHECK-IN" : id == "security" ? "SECURITY" : "GATES"; }

        void NeedButton(int slot, string text, string blocked, Color tint, System.Action act)
        {
            bool ok = blocked == null && act != null;
            var b = Ui.Button("B" + slot, _needButtons, null, ok ? tint : Locked, () => { if (ok) act(); else if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); } }, 28);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(slot * 0.5f, 0); rt.anchorMax = new Vector2(slot * 0.5f + 0.5f, 1); rt.offsetMin = new Vector2(slot == 0 ? 0 : 6, 0); rt.offsetMax = new Vector2(slot == 0 ? -6 : 0, 0);
            ButtonFace(b.transform, text, blocked);
        }
    }
}
