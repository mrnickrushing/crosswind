using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Inside the terminal: the passengers' ratings along the bottom, the SHOP that sells everything you can put on
    /// the concourse floor, placing it (tap the floor, ROTATE, PLACE), and what a tap shows: a placed thing can be
    /// upgraded, moved or sold; a passenger tells you who they are and how they rate each part of your terminal.
    /// </summary>
    public sealed partial class Hud
    {
        const int ShopTab = 9;
        RectTransform _termBar, _placeBar, _itemCard, _paxCard, _bayCard;
        Text _bayText; Button _bayBuy; int _bay = -1;
        readonly List<Image> _needFills = new List<Image>();
        readonly List<Text> _needLabels = new List<Text>();
        Text _placeText, _itemText, _paxText;
        Button _placeOk, _itemUp;
        AmenityDef _placing; PlacedAmenity _moving, _picked;
        int _placeX, _placeZ, _placeRot;
        GameObject _ghost; Material _ghostMat;
        Interior.Agent _pax;
        string _shopGroup = "Food";

        void BuildTerminalUi()
        {
            Interior.Thought = OnThought;
            // Ratings and SHOP.
            _termBar = Ui.ArtImg("Terminal bar", _safe, "panel", Color.white).rectTransform;
            _termBar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 12), new Vector2(1500, 100));
            _termBar.GetComponent<Image>().raycastTarget = true;
            var shop = Ui.Button("Shop", _termBar, "SHOP", Ui.Green, () => { ClosePick(); OpenTab(_tab == ShopTab ? -1 : ShopTab); }, 34);
            ((RectTransform)shop.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(190, 80));
            var shopLabel = shop.GetComponentInChildren<Text>().rectTransform; shopLabel.offsetMin = new Vector2(64, 10);
            Ui.IconImg("shoppingCart", shop.transform, Color.white, new Vector2(0, 0.5f), new Vector2(40, 2), 44);
            int n = _sim.Needs.Count;
            for (int i = 0; i < n; i++)
            {
                var cell = Ui.Rect("Need", _termBar);
                cell.anchorMin = new Vector2(0, 0); cell.anchorMax = new Vector2(0, 1); cell.pivot = new Vector2(0, 0.5f);
                float cw = (1500 - 230) / (float)n;
                cell.offsetMin = new Vector2(218 + i * cw, 10); cell.offsetMax = new Vector2(218 + (i + 1) * cw - 10, -10);
                var label = Ui.Label("Name", cell, "", 20, Ui.Dim, TextAnchor.UpperLeft, false);
                label.rectTransform.Fill(); label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var well = Ui.ArtImg("Well", cell, "well", Color.white, 44, 3f).rectTransform;
                well.anchorMin = new Vector2(0, 0); well.anchorMax = new Vector2(1, 0); well.pivot = new Vector2(0.5f, 0); well.offsetMin = new Vector2(0, 4); well.offsetMax = new Vector2(0, 26);
                var inRt = Ui.Rect("In", well); inRt.Fill(4);
                var fill = Ui.Img("Fill", inRt, Ui.Green, true);
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.5f, 1); fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                _needFills.Add(fill); _needLabels.Add(label);
            }
            _termBar.gameObject.SetActive(false);

            // Placing: where it will stand, turn it, pay for it.
            _placeBar = Ui.ArtImg("Place bar", _safe, "panel", Color.white).rectTransform;
            _placeBar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 12), new Vector2(1180, 100));
            _placeBar.GetComponent<Image>().raycastTarget = true;
            _placeText = Ui.Label("Text", _placeBar, "", 28, Color.white, TextAnchor.MiddleLeft, false);
            _placeText.rectTransform.Fill(); _placeText.rectTransform.offsetMin = new Vector2(24, 8); _placeText.rectTransform.offsetMax = new Vector2(-640, -8);
            var cancel = Ui.Button("Cancel", _placeBar, "CANCEL", Ui.Off, StopPlacing, 30);
            ((RectTransform)cancel.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(180, 80));
            _placeOk = Ui.Button("Ok", _placeBar, "PLACE", Ui.Green, ConfirmPlace, 30);
            ((RectTransform)_placeOk.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-204, 0), new Vector2(230, 80));
            var turn = Ui.Button("Turn", _placeBar, "ROTATE", Ui.Blue, () => { _placeRot = (_placeRot + 1) & 3; Sfx.Play("click", 0.5f); }, 30);
            ((RectTransform)turn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-446, 0), new Vector2(180, 80));
            _placeBar.gameObject.SetActive(false);

            // A placed thing, picked.
            _itemCard = Ui.ArtImg("Item", _safe, "panel", Color.white).rectTransform;
            _itemCard.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 124), new Vector2(980, 150));
            _itemCard.GetComponent<Image>().raycastTarget = true;
            _itemText = Ui.Label("Text", _itemCard, "", 28, Color.white, TextAnchor.MiddleLeft, false);
            _itemText.rectTransform.Fill(); _itemText.rectTransform.offsetMin = new Vector2(24, 10); _itemText.rectTransform.offsetMax = new Vector2(-560, -10);
            _itemUp = Ui.Button("Up", _itemCard, "UPGRADE", Ui.Green, () => { if (_picked != null && _sim.UpgradeAmenity(_picked)) { Sfx.Play("build", 0.7f); Toast(Sim.AmenityById(_picked.id).Name + " is now level " + _picked.level); } else if (_picked != null) { Toast(_sim.AmenityUpgradeBlocked(_picked)); Sfx.Play("error", 0.5f); } }, 28);
            ((RectTransform)_itemUp.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-340, 0), new Vector2(210, 110));
            var move = Ui.Button("Move", _itemCard, "MOVE", Ui.Blue, () => { if (_picked == null) return; _moving = _picked; _placing = Sim.AmenityById(_picked.id); _placeX = _picked.x; _placeZ = _picked.z; _placeRot = _picked.rot; ClosePick(); OpenTab(-1); }, 28);
            ((RectTransform)move.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-180, 0), new Vector2(150, 110));
            var sell = Ui.Button("Sell", _itemCard, "SELL", Ui.Red, () => { if (_picked == null) return; double back = _sim.SellValue(_picked); string name = Sim.AmenityById(_picked.id).Name; if (_sim.SellAmenity(_picked)) { Toast(name + " sold: +" + Format.Money(back)); Sfx.Play("coin", 0.6f); } ClosePick(); }, 28);
            ((RectTransform)sell.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(150, 110));
            _itemCard.gameObject.SetActive(false);

            // A fenced bay, tapped: what opening it gives and costs.
            _bayCard = Ui.ArtImg("Bay", _safe, "panel", Color.white).rectTransform;
            _bayCard.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 124), new Vector2(980, 150));
            _bayCard.GetComponent<Image>().raycastTarget = true;
            _bayText = Ui.Label("Text", _bayCard, "", 28, Color.white, TextAnchor.MiddleLeft, false);
            _bayText.rectTransform.Fill(); _bayText.rectTransform.offsetMin = new Vector2(24, 10); _bayText.rectTransform.offsetMax = new Vector2(-330, -10);
            _bayBuy = Ui.Button("Open", _bayCard, "OPEN", Ui.Green, () =>
            {
                string why = _sim.BuyBayBlocked(_bay);
                if (why != null) { Toast(why); Sfx.Play("error", 0.5f); return; }
                _sim.BuyBay(_bay);
                Sfx.Play("build", 0.8f); Toast("More floor to build on");
                GameRoot.Instance.Save();
                CloseBay();
            }, 30);
            ((RectTransform)_bayBuy.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(290, 110));
            _bayCard.gameObject.SetActive(false);

            // A passenger, asked.
            _paxCard = Ui.ArtImg("Passenger", _safe, "panel", Color.white).rectTransform;
            _paxCard.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(500, 500));
            _paxCard.GetComponent<Image>().raycastTarget = true;
            _paxText = Ui.Label("Text", _paxCard, "", 26, Color.white, TextAnchor.UpperLeft, false);
            _paxText.rectTransform.Fill(); _paxText.rectTransform.offsetMin = new Vector2(24, 16); _paxText.rectTransform.offsetMax = new Vector2(-20, -18);
            var px = Ui.Button("Close", _paxCard, "X", new Color(0, 0, 0, 0), () => { _pax = null; _paxCard.gameObject.SetActive(false); }, 36);
            ((RectTransform)px.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(70, 64));
            _paxCard.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ what passengers make of it

        static string WantIcon(Want w) { return w == Want.Food ? "burger" : w == Want.Shopping ? "shop" : w == Want.Toilets ? "restroom" : w == Want.Fun ? "gamepad" : w == Want.Clean ? "broom" : w == Want.Info ? "sign" : "couch"; }

        /// <summary>
        /// A passenger has stopped at something on the concourse: now and then a bubble rises over them with what it
        /// was for and a mark for how well this terminal answers that need, as that passenger sees it.
        /// </summary>
        void OnThought(Interior.Agent who, string amenity)
        {
            if (!CameraRig.Inside || _tab >= 0 || _placing != null || GameRoot.Speed > 3 || _pops.Count > 10 || Random.value > 0.45f) return;
            var d = Sim.AmenityById(amenity);
            if (d == null || Interior.Instance == null) return;
            var needs = _sim.Needs;
            var want = d.Gives == Want.Lounge ? Want.Seating : d.Gives;
            int i = needs.FindIndex(n => !n.Queue && n.Want == want);
            if (i < 0) return;
            float v = Personal(needs[i].Value, who.Seed, i);
            var rt = Ui.Rect("Thought", _world);
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(104, 118));
            var tail = Ui.ArtImg("Tail", rt, "tail", Color.white, 0);
            tail.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(34, 34));
            var bg = Ui.ArtImg("Bubble", rt, "bubble", Color.white);
            bg.rectTransform.Fill(); bg.rectTransform.offsetMin = new Vector2(0, 16);
            var pic = Ui.Img("Icon", bg.transform, Color.white); pic.sprite = IconArt.Of(WantIcon(want)); pic.preserveAspect = true;
            pic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 6), new Vector2(72, 72));
            // The mark: a smile on green, a shrug on amber, a warning on red.
            var mark = Ui.Img("Mark", bg.transform, v >= 0.75f ? Ui.Green : v >= 0.5f ? Ui.Gold : Ui.Red, true);
            mark.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(4, 0), new Vector2(46, 46));
            if (v >= 0.75f) { var face = Ui.Img("Face", mark.transform, Color.white); face.sprite = Ui.Ic("smile"); face.preserveAspect = true; face.rectTransform.Fill(7); }
            else Ui.Label("Sign", mark.transform, v >= 0.5f ? "~" : "!", 34, Color.white).rectTransform.Fill();
            var p = Float(rt, 2.8f);
            p.World = Interior.Instance.AgentWorld(who) + Vector3.up * 2.3f;
            p.Drift = new Vector2(0, 22);
        }

        static Color Meter(float v) { return v >= 0.8f ? Ui.Green : v >= 0.5f ? Ui.Gold : Ui.Red; }
        static string WantWord(Want w) { return w == Want.Food ? "food" : w == Want.Shopping ? "shopping" : w == Want.Seating ? "seating" : w == Want.Toilets ? "toilets" : w == Want.Fun ? "entertainment" : w == Want.Clean ? "cleanliness" : w == Want.Info ? "wayfinding" : "the passenger fee"; }
        static string Gives(AmenityDef d, int level = 1)
        {
            float k = level == 1 ? 1 : level == 2 ? 2 : 3.5f;
            return "+" + Mathf.RoundToInt(d.Points * k) + " " + WantWord(d.Gives) + (d.AlsoPoints > 0 ? ", +" + Mathf.RoundToInt(d.AlsoPoints * k) + " " + WantWord(d.Also) : "") + (d.Earns ? ", earns from every passenger" : "");
        }

        void ClosePick() { _picked = null; _itemCard.gameObject.SetActive(false); CloseBay(); }
        void CloseBay() { _bay = -1; if (_bayCard != null) _bayCard.gameObject.SetActive(false); }

        void StopPlacing()
        {
            _placing = null; _moving = null;
            if (_ghost != null) _ghost.SetActive(false);
        }

        void ConfirmPlace()
        {
            if (_placing == null) return;
            bool ok = _moving != null ? _sim.MoveAmenity(_moving, _placeX, _placeZ, _placeRot) : _sim.Place(_placing.Id, _placeX, _placeZ, _placeRot);
            if (!ok) { Toast(_moving != null ? "No room there" : _sim.PlaceBlocked(_placing.Id, _placeX, _placeZ, _placeRot)); Sfx.Play("error", 0.5f); return; }
            Sfx.Play("build", 0.8f);
            Toast(_placing.Name + (_moving != null ? " moved" : " placed: " + Gives(_placing)));
            GameRoot.Instance.Save();
            if (_moving != null) { StopPlacing(); return; }
            // Keep going with the same thing while there's room and money: the next free spot beside it.
            var next = _sim.FindSpot(_placing.Id);
            if (next == null || _sim.State.money < _sim.AmenityCost(_placing.Id)) StopPlacing(); else { _placeX = next.x; _placeZ = next.z; _placeRot = next.rot; }
        }

        void StartPlacing(AmenityDef d)
        {
            var spot = _sim.FindSpot(d.Id);
            if (spot == null) { Toast(_sim.NextBay >= 0 ? "The open floor is full: tap a fenced bay to open more" : "The floor is full: sell something, or grow the terminal"); return; }
            _placing = d; _moving = null; _placeX = spot.x; _placeZ = spot.z; _placeRot = spot.rot;
            ClosePick(); OpenTab(-1);
            var at = Interior.Instance.FloorPoint(spot.x, spot.z, 1, 1);
            CameraRig.Instance.Focus = new Vector3(at.x, 0, at.z);
            if (CameraRig.Instance.Distance > 150) CameraRig.Instance.Distance = 150;
        }

        /// <summary>A tap inside the terminal: put the ghost there, or pick what stands there, or ask the passenger.</summary>
        void TerminalTap(Vector2 screen)
        {
            var inside = Interior.Instance;
            if (inside == null) return;
            var ray = CameraRig.Instance.Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return;
            var hit = ray.origin + ray.direction * ((Interior.Origin.y - ray.origin.y) / ray.direction.y);
            int cx, cz; inside.CellAt(hit, out cx, out cz);
            if (_placing != null)
            {
                int w, d; Sim.Footprint(_placing, _placeRot, out w, out d);
                _placeX = Mathf.Clamp(cx - (w - 1) / 2, 0, _sim.FloorCols - w); _placeZ = Mathf.Clamp(cz - (d - 1) / 2, 0, Mathf.Max(0, _sim.FloorRows - d));
                Sfx.Play("click", 0.4f);
                return;
            }
            var pax = inside.AgentNear(hit);
            if (pax != null) { _pax = pax; ClosePick(); OpenTab(-1); _paxCard.gameObject.SetActive(true); Sfx.Play("click", 0.4f); return; }
            var item = cx >= 0 && cz >= 0 ? inside.ItemAt(cx, cz) : null;
            if (item != null) { CloseBay(); _picked = item; _pax = null; _paxCard.gameObject.SetActive(false); _itemCard.gameObject.SetActive(true); Sfx.Play("click", 0.4f); return; }
            // Fenced floor: offer to open it.
            if (cx >= 0 && cz >= 0 && cx < _sim.FloorCols && cz < _sim.FloorRows && !_sim.BayOpen(Sim.BayOf(cz)))
            {
                ClosePick(); _pax = null; _paxCard.gameObject.SetActive(false);
                _bay = Sim.BayOf(cz); _bayCard.gameObject.SetActive(true); Sfx.Play("click", 0.4f);
                return;
            }
            ClosePick(); _pax = null; _paxCard.gameObject.SetActive(false);
        }

        static readonly string[] First = { "Ana", "Ben", "Chidi", "Dana", "Elif", "Farid", "Grace", "Hugo", "Ines", "Jonas", "Keiko", "Liam", "Mina", "Noor", "Omar", "Priya", "Quinn", "Rosa", "Sven", "Tara", "Uma", "Viktor", "Wen", "Yara", "Zeke" };
        static readonly string[] Last = { "Alvarez", "Bauer", "Castell", "Dimitrov", "Eze", "Fontaine", "Guerra", "Haddad", "Ivanova", "Jensen", "Kovac", "Lindqvist", "Moreau", "Nakata", "Okoye", "Petrov", "Quist", "Rahman", "Soto", "Tanaka", "Ueda", "Varga", "Whitlock", "Yilmaz", "Zamora" };

        /// <summary>How this one passenger rates a need: the terminal's score, seen through their own day (steady per person).</summary>
        static float Personal(float value, int seed, int i)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)((i + 1) * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            float noise = (h % 1000) / 1000f - 0.5f;
            return Mathf.Clamp01(value + noise * 0.3f);
        }

        GameObject _paxRing;
        /// <summary>A bright disc under the passenger whose card is open, so you can see who is talking.</summary>
        void PaxRing(bool on, Vector3 at)
        {
            if (_paxRing == null)
            {
                if (!on) return;
                _paxRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(_paxRing.GetComponent<Collider>());
                var mr = _paxRing.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.82f, 0.25f, 0.75f) };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_paxRing.activeSelf != on) _paxRing.SetActive(on);
            if (!on) return;
            float r = 1.5f + Mathf.Sin(Time.unscaledTime * 5) * 0.15f;
            _paxRing.transform.position = at + Vector3.up * 0.04f;
            _paxRing.transform.localScale = new Vector3(r, 0.01f, r);
        }

        void UpdateTerminalUi()
        {
            bool inside = CameraRig.Inside;
            if (_pax == null || !inside || !_paxCard.gameObject.activeSelf) PaxRing(false, Vector3.zero);
            if (!inside && (_placing != null || _picked != null || _pax != null || _bay >= 0)) { StopPlacing(); ClosePick(); _pax = null; _paxCard.gameObject.SetActive(false); }
            if (!inside && _tab == ShopTab) OpenTab(-1);
            _termBar.gameObject.SetActive(inside && _placing == null && _tab < 0);
            _placeBar.gameObject.SetActive(inside && _placing != null);
            if (!inside) return;

            var needs = _sim.Needs;
            for (int i = 0; i < needs.Count && i < _needFills.Count; i++)
            {
                _needFills[i].rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, needs[i].Value), 1);
                _needFills[i].color = Meter(needs[i].Value);
                _needLabels[i].text = (needs[i].Queue ? "QUEUES" : needs[i].Want == Want.Food ? "FOOD" : needs[i].Want == Want.Shopping ? "SHOPS" : needs[i].Want == Want.Seating ? "SEATS" : needs[i].Want == Want.Toilets ? "TOILETS" : needs[i].Want == Want.Fun ? "FUN" : needs[i].Want == Want.Clean ? "CLEAN" : "SIGNS") + "  <color=#FFFFFF>" + Mathf.RoundToInt(needs[i].Value * 100) + "%</color>";
            }

            if (_placing != null)
            {
                int w, d; Sim.Footprint(_placing, _placeRot, out w, out d);
                _placeX = Mathf.Clamp(_placeX, 0, _sim.FloorCols - w); _placeZ = Mathf.Clamp(_placeZ, 0, Mathf.Max(0, _sim.FloorRows - d));
                bool free = _sim.FloorFree(_placing, _placeX, _placeZ, _placeRot, _moving);
                double cost = _moving != null ? 0 : _sim.AmenityCost(_placing.Id);
                bool can = free && _sim.State.money >= cost;
                if (_ghost == null)
                {
                    _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(_ghost.GetComponent<Collider>());
                    _ghostMat = new Material(Shader.Find("Sprites/Default"));
                    _ghost.GetComponent<MeshRenderer>().sharedMaterial = _ghostMat;
                    _ghost.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                _ghost.SetActive(true);
                _ghost.transform.position = Interior.Instance.FloorPoint(_placeX, _placeZ, w, d) + Vector3.up * (0.9f + Mathf.Sin(Time.unscaledTime * 4) * 0.05f);
                _ghost.transform.localScale = new Vector3(w * Sim.Cell - 0.1f, 1.8f, d * Sim.Cell - 0.1f);
                _ghostMat.color = can ? new Color(0.3f, 0.95f, 0.5f, 0.45f) : new Color(1f, 0.3f, 0.25f, 0.45f);
                _placeText.text = "<b>" + _placing.Name + "</b>  " + _placing.W + " x " + _placing.D + "\n<color=#FFFFFFA0>Tap the floor to choose the spot." + (free ? "" : _placeZ >= 0 && _placeZ + d <= _sim.FloorRows && !_sim.BayOpen(Sim.BayOf(_placeZ)) ? "  <color=#FF8070>That floor is not open yet.</color>" : "  <color=#FF8070>No room there.</color>") + "</color>";
                _placeOk.GetComponent<Image>().color = can ? Ui.Green : Ui.Off;
                _placeOk.GetComponentInChildren<Text>().text = _moving != null ? "PUT HERE" : "PLACE\n<size=24>" + Format.Money(cost) + "</size>";
            }
            else if (_ghost != null && _ghost.activeSelf) _ghost.SetActive(false);

            if (_picked != null)
            {
                if (!_sim.State.terminal.Contains(_picked)) ClosePick();
                else
                {
                    var d = Sim.AmenityById(_picked.id);
                    _itemText.text = "<size=34><b>" + d.Name + "</b></size>  <color=#7CCBFF>LEVEL " + _picked.level + "</color>\n" + Gives(d, _picked.level)
                        + (_picked.level < Sim.MaxAmenityLevel ? "\n<color=#FFFFFFA0>Next level: " + Gives(d, _picked.level + 1) + "</color>" : "\n<color=#FFFFFFA0>Fully upgraded</color>");
                    bool max = _picked.level >= Sim.MaxAmenityLevel;
                    _itemUp.gameObject.SetActive(!max);
                    if (!max) { _itemUp.GetComponent<Image>().color = _sim.AmenityUpgradeBlocked(_picked) == null ? Ui.Green : Ui.Off; _itemUp.GetComponentInChildren<Text>().text = "UPGRADE\n<size=24>" + Format.Money(_sim.AmenityUpgradeCost(_picked)) + "</size>"; }
                }
            }

            if (_bay >= 0)
            {
                if (_sim.BayOpen(_bay) || _bay >= _sim.BayCount || _placing != null) CloseBay();
                else
                {
                    string why = _sim.BuyBayBlocked(_bay);
                    int rows = _sim.BayRowsOf(_bay);
                    _bayText.text = "<size=34><b>Fenced bay</b></size>  <color=#7CCBFF>" + (rows * (int)Sim.Cell) + " x " + (_sim.FloorCols * (int)Sim.Cell) + " m OF FLOOR</color>\nOpen it to build here: " + (rows * _sim.FloorCols) + " more cells."
                        + (why != null && !why.StartsWith("Need") ? "\n<color=#FFC873>" + why + "</color>" : "\n<color=#FFFFFFA0>Each bay you open makes the next dearer.</color>");
                    _bayBuy.GetComponent<Image>().color = why == null ? Ui.Green : Ui.Off;
                    _bayBuy.GetComponentInChildren<Text>().text = "OPEN FLOOR\n<size=24>" + Format.Money(_sim.BayCost) + "</size>";
                }
            }

            if (_pax != null)
            {
                if (Interior.Instance == null || !Interior.Instance.Alive(_pax)) { _pax = null; _paxCard.gameObject.SetActive(false); }
                else
                {
                    int seed = _pax.Seed;
                    string name = First[seed % First.Length] + " " + Last[(seed / 31) % Last.Length];
                    var routes = _sim.State.contracts; var r = routes.Count > 0 ? Content.Routes[routes[seed % routes.Count].route] : null;
                    string trip = r == null ? "" : (_pax.Arriving ? "Just in from " : "Flying to ") + r.Destination + (r.Airline == "own" ? "" : " with " + Content.Airlines[r.Airline].Name);
                    float sum = 0; int worst = 0; float worstV = 2; int best = 0; float bestV = -1;
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < needs.Count; i++)
                    {
                        float v = Personal(needs[i].Value, seed, i); sum += v;
                        if (v < worstV) { worstV = v; worst = i; }
                        if (v > bestV) { bestV = v; best = i; }
                        int stars = Mathf.Clamp(Mathf.RoundToInt(v * 5), 1, 5);
                        string col = v >= 0.8f ? "#5ED58A" : v >= 0.5f ? "#FFC83D" : "#FF7A6B";
                        sb.Append(needs[i].Name).Append("  <color=").Append(col).Append(">").Append(new string('★', stars)).Append("</color><color=#FFFFFF30>").Append(new string('★', 5 - stars)).Append("</color>\n");
                    }
                    float mood = sum / needs.Count;
                    string moodWord = mood >= 0.85f ? "Delighted" : mood >= 0.65f ? "Happy" : mood >= 0.45f ? "Getting by" : "Fed up";
                    string think = worstV >= 0.8f ? "\"Best airport I've been through. " + needs[best].Name + " especially.\""
                        : needs[worst].Queue ? "\"That queue took forever.\""
                        : needs[worst].Want == Want.Food ? "\"Is there nowhere to eat in this place?\""
                        : needs[worst].Want == Want.Seating ? "\"I've been standing for an hour.\""
                        : needs[worst].Want == Want.Toilets ? "\"The queue for the toilets is longer than security.\""
                        : needs[worst].Want == Want.Fun ? "\"Three hours to kill and nothing to do.\""
                        : needs[worst].Want == Want.Clean ? "\"Someone should empty these bins.\""
                        : needs[worst].Want == Want.Shopping ? "\"I wanted a present. There's nothing to buy.\""
                        : "\"Which way is my gate?\"";
                    _paxText.text = "<size=36><b>" + name + "</b></size>\n<color=#FFFFFFA0>" + trip + "</color>\n<color=" + (mood >= 0.65f ? "#5ED58A" : mood >= 0.45f ? "#FFC83D" : "#FF7A6B") + ">" + moodWord + "</color>\n\n" + sb + "\n<i>" + think + "</i>";
                    PaxRing(true, Interior.Instance.AgentWorld(_pax));
                    CameraRig.Instance.Focus = Vector3.Lerp(CameraRig.Instance.Focus, new Vector3(Interior.Instance.AgentWorld(_pax).x, 0, Interior.Instance.AgentWorld(_pax).z), Time.unscaledDeltaTime * 2);
                }
            }
        }

        void ShopSheet()
        {
            var s = _sim;
            _sheetTitle.text = "TERMINAL CATALOGUE";
            // Groups across the top, in two rows: what the terminal is built of first, then everything you can place.
            var groups = new List<string> { "Services" };
            groups.AddRange(Sim.Amenities.Select(a => a.Group).Distinct());
            int perRow = (groups.Count + 1) / 2;
            for (int r = 0; r < 2; r++)
            {
                var tabs = Row(70);
                tabs.GetComponent<Image>().color = new Color(1, 1, 1, 0);
                _rowY -= 8;
                for (int i = 0; i < perRow && r * perRow + i < groups.Count; i++)
                {
                    string gname = groups[r * perRow + i];
                    var b = Ui.Button("Group", tabs, gname.ToUpper(), gname == _shopGroup ? Ui.Blue : Ui.Card, () => { _shopGroup = gname; _sheetList.anchoredPosition = Vector2.zero; _dirty = true; Sfx.Play("click", 0.5f); }, 24);
                    var rt = (RectTransform)b.transform;
                    rt.anchorMin = new Vector2(i / (float)perRow, 0); rt.anchorMax = new Vector2((i + 1) / (float)perRow, 1); rt.offsetMin = new Vector2(3, 3); rt.offsetMax = new Vector2(-3, -3);
                    var l = b.GetComponentInChildren<Text>(); l.resizeTextForBestFit = true; l.resizeTextMinSize = 14; l.resizeTextMaxSize = 24;
                }
            }
            _rowY += 8;
            var worst = s.WorstNeed;
            Header("PASSENGERS WANT MOST: " + worst.Name.ToUpper() + " (" + Mathf.RoundToInt(worst.Value * 100) + "%)  ·  " + s.State.terminal.Count + " THINGS PLACED");

            RectTransform row = null; int col = 0;
            System.Func<RectTransform> card = () =>
            {
                if (col == 0) { row = Row(376); row.GetComponent<Image>().color = new Color(1, 1, 1, 0); }
                var c = Ui.ArtImg("Card", row, "card", Color.white).rectTransform;
                c.anchorMin = new Vector2(col * 0.5f, 0); c.anchorMax = new Vector2(col * 0.5f + 0.5f, 1); c.offsetMin = new Vector2(col == 0 ? 0 : 6, 0); c.offsetMax = new Vector2(col == 0 ? -6 : 0, 0);
                col = (col + 1) % 2;
                return c;
            };

            if (_shopGroup == "Services")
            {
                // The terminal's own machinery, built by level: the desks, the lanes, the gates, the way in.
                foreach (var id in Content.FacilityOrder)
                {
                    var d = Content.Facilities[id];
                    if (d.Group != "Terminal") continue;
                    string fid = id; int level = s.Level(id);
                    var c = card();
                    var well = Ui.ArtImg("Well", c, "well", Color.white).rectTransform;
                    well.anchorMin = new Vector2(0, 1); well.anchorMax = new Vector2(1, 1); well.pivot = new Vector2(0.5f, 1); well.offsetMin = new Vector2(12, -182); well.offsetMax = new Vector2(-12, -12);
                    var art = IconArt.Of(FacilityIcon(id));
                    if (art != null)
                    {
                        var glow = Ui.ArtImg("Glow", well, "glow", new Color(0.5f, 0.82f, 1f, 0.45f), 0);
                        glow.preserveAspect = false; glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(300, 190));
                        var pic = Ui.Img("Picture", well, Color.white); pic.sprite = art; pic.preserveAspect = true; pic.rectTransform.Fill(8);
                    }
                    else
                    {
                        var tile = Ui.ArtImg("Tile", well, "button", FacilityTint(id), 44, 1.6f).rectTransform;
                        tile.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128, 128));
                        var sp = Ui.Ic(FacilityIcon(id));
                        if (sp != null) { var g = Ui.Img("Glyph", tile, Color.white); g.sprite = sp; g.preserveAspect = true; g.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(78, 78)); }
                    }
                    Chip(well, level > 0 ? "LEVEL " + level : "NOT BUILT", new Vector2(0, 1), level > 0 ? Ui.Blue : Ui.Off);
                    var build = s.Building(id);
                    if (build != null) Chip(well, Format.Time(build.doneAt - s.RealNow()).ToUpper() + " LEFT", new Vector2(1, 1), Ui.Gold);
                    CardText(c, d.Name, d.Blurb);
                    string blocked = build != null ? "Being built" : s.UpgradeBlocked(id);
                    float secs = s.BuildSeconds(id);
                    CardButton(c, level >= d.MaxLevel ? "MAX" : (level > 0 ? "UPGRADE\n" : "BUILD\n") + Format.Money(s.Cost(id)), level >= d.MaxLevel ? null : blocked, Ui.Green, level < d.MaxLevel, () =>
                    {
                        if (s.Upgrade(fid)) { if (secs > 60) Reminders.AskOnce(); Toast(secs > 0 ? d.Name + ": the crew has started" : d.Name + " upgraded to level " + s.Level(fid)); Sfx.Play("build", 0.7f); }
                    });
                }
                return;
            }
            foreach (var d in Sim.Amenities.Where(a => a.Group == _shopGroup))
            {
                var def = d;
                string blocked = s.Stars < d.Stars ? "Needs " + d.Stars + " stars" : s.State.money < s.AmenityCost(d.Id) ? "Need " + Format.Money(s.AmenityCost(d.Id) - s.State.money) + " more" : s.FindSpot(d.Id) == null ? "No room on the floor" : null;
                var c = card();
                var well = Ui.ArtImg("Well", c, "well", Color.white).rectTransform;
                well.anchorMin = new Vector2(0, 1); well.anchorMax = new Vector2(1, 1); well.pivot = new Vector2(0.5f, 1); well.offsetMin = new Vector2(12, -182); well.offsetMax = new Vector2(-12, -12);
                var glow = Ui.ArtImg("Glow", well, "glow", new Color(0.5f, 0.82f, 1f, 0.45f), 0);
                glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(300, 190));
                glow.preserveAspect = false;
                bool locked = s.Stars < d.Stars;
                var pic = Ui.Img("Picture", well, locked ? new Color(0.25f, 0.3f, 0.38f, 0.9f) : Color.white); pic.sprite = Interior.Picture(d); pic.preserveAspect = true;
                pic.rectTransform.Fill(8);
                Chip(well, d.W + " x " + d.D, new Vector2(1, 1), new Color(0, 0, 0, 0.55f));
                int own = s.Placed(d.Id);
                if (own > 0) Chip(well, "OWN " + own, new Vector2(0, 1), Ui.Blue);
                if (locked) Chip(well, d.Stars + " STARS", new Vector2(0, 0), Ui.Off);
                else if (d.Earns) Chip(well, "EARNS", new Vector2(0, 0), TintGold);
                CardText(c, d.Name, Gives(d).Replace(", earns from every passenger", "") + ".  " + d.Blurb);
                CardButton(c, "PLACE\n" + Format.Money(s.AmenityCost(d.Id)), blocked, Ui.Green, true, () => StartPlacing(def));
            }
        }

        /// <summary>A small label in a corner of a catalogue picture.</summary>
        static void Chip(RectTransform parent, string text, Vector2 corner, Color tint)
        {
            var chip = Ui.Img("Chip", parent, tint, true).rectTransform;
            chip.Place(corner, corner, new Vector2(corner.x < 0.5f ? 8 : -8, corner.y < 0.5f ? 8 : -8), new Vector2(Mathf.Max(84, 30 + text.Length * 14), 38));
            var l = Ui.Label("L", chip, text, 22, Color.white); l.rectTransform.Fill(4);
            l.resizeTextForBestFit = true; l.resizeTextMinSize = 12; l.resizeTextMaxSize = 22;
        }

        /// <summary>A catalogue card's name and the two lines under it.</summary>
        static void CardText(RectTransform card, string name, string body)
        {
            var t = Ui.Label("Name", card, name, 32, Color.white, TextAnchor.MiddleLeft);
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1); t.rectTransform.pivot = new Vector2(0.5f, 1); t.rectTransform.offsetMin = new Vector2(18, -228); t.rectTransform.offsetMax = new Vector2(-14, -186);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = 32; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            var b = Ui.Label("Body", card, body, 22, Ui.Dim, TextAnchor.UpperLeft, false);
            b.rectTransform.anchorMin = new Vector2(0, 0); b.rectTransform.anchorMax = new Vector2(1, 1); b.rectTransform.offsetMin = new Vector2(18, 92); b.rectTransform.offsetMax = new Vector2(-14, -228);
            b.resizeTextForBestFit = true; b.resizeTextMinSize = 15; b.resizeTextMaxSize = 22; b.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>The button along a catalogue card's foot.</summary>
        void CardButton(RectTransform card, string text, string blocked, Color tint, bool live, System.Action act)
        {
            bool ok = blocked == null && live;
            var btn = Ui.Button("Do", card, null, ok ? tint : Locked, () => { if (ok) { act(); _dirty = true; } else if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); } }, 30);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.offsetMin = new Vector2(12, 10); rt.offsetMax = new Vector2(-12, 88);
            // One line on a wide button: the action and the price side by side, the reason on the strip.
            ButtonFace(btn.transform, text.Replace("\n", "   "), live ? blocked : null);
        }
    }
}
