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
        RectTransform _termBar, _placeBar, _itemCard, _paxCard;
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

        static Color Meter(float v) { return v >= 0.8f ? Ui.Green : v >= 0.5f ? Ui.Gold : Ui.Red; }
        static string WantWord(Want w) { return w == Want.Food ? "food" : w == Want.Shopping ? "shopping" : w == Want.Seating ? "seating" : w == Want.Toilets ? "toilets" : w == Want.Fun ? "entertainment" : w == Want.Clean ? "cleanliness" : w == Want.Info ? "wayfinding" : "the passenger fee"; }
        static string Gives(AmenityDef d, int level = 1)
        {
            float k = level == 1 ? 1 : level == 2 ? 2 : 3.5f;
            return "+" + Mathf.RoundToInt(d.Points * k) + " " + WantWord(d.Gives) + (d.AlsoPoints > 0 ? ", +" + Mathf.RoundToInt(d.AlsoPoints * k) + " " + WantWord(d.Also) : "") + (d.Earns ? ", earns from every passenger" : "");
        }

        void ClosePick() { _picked = null; _itemCard.gameObject.SetActive(false); }

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
            if (spot == null) { Toast("The floor is full: sell something, or grow the terminal"); return; }
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
                _placeX = Mathf.Clamp(cx - (w - 1) / 2, 0, Sim.FloorCols - w); _placeZ = Mathf.Clamp(cz - (d - 1) / 2, 0, Mathf.Max(0, _sim.FloorRows - d));
                Sfx.Play("click", 0.4f);
                return;
            }
            var pax = inside.AgentNear(hit);
            if (pax != null) { _pax = pax; ClosePick(); OpenTab(-1); _paxCard.gameObject.SetActive(true); Sfx.Play("click", 0.4f); return; }
            var item = cx >= 0 && cz >= 0 ? inside.ItemAt(cx, cz) : null;
            if (item != null) { _picked = item; _pax = null; _paxCard.gameObject.SetActive(false); _itemCard.gameObject.SetActive(true); Sfx.Play("click", 0.4f); return; }
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
            if (!inside && (_placing != null || _picked != null || _pax != null)) { StopPlacing(); ClosePick(); _pax = null; _paxCard.gameObject.SetActive(false); }
            if (!inside && _tab == ShopTab) OpenTab(-1);
            _termBar.gameObject.SetActive(inside && _placing == null);
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
                _placeX = Mathf.Clamp(_placeX, 0, Sim.FloorCols - w); _placeZ = Mathf.Clamp(_placeZ, 0, Mathf.Max(0, _sim.FloorRows - d));
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
                _placeText.text = "<b>" + _placing.Name + "</b>  " + _placing.W + " x " + _placing.D + "\n<color=#FFFFFFA0>Tap the floor to choose the spot." + (free ? "" : "  <color=#FF8070>No room there.</color>") + "</color>";
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
            _sheetTitle.text = "TERMINAL SHOP";
            // Groups across the top.
            var groups = Sim.Amenities.Select(a => a.Group).Distinct().ToList();
            var tabs = Row(84);
            tabs.GetComponent<Image>().color = new Color(1, 1, 1, 0);
            for (int i = 0; i < groups.Count; i++)
            {
                string gname = groups[i];
                var b = Ui.Button("Group", tabs, gname.ToUpper(), gname == _shopGroup ? Ui.Blue : Ui.Card, () => { _shopGroup = gname; _sheetList.anchoredPosition = Vector2.zero; _dirty = true; }, 22);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i / (float)groups.Count, 0); rt.anchorMax = new Vector2((i + 1) / (float)groups.Count, 1); rt.offsetMin = new Vector2(3, 4); rt.offsetMax = new Vector2(-3, -4);
                var l = b.GetComponentInChildren<Text>(); l.resizeTextForBestFit = true; l.resizeTextMinSize = 14; l.resizeTextMaxSize = 22;
            }
            var worst = s.WorstNeed;
            Header("PASSENGERS WANT MOST: " + worst.Name.ToUpper() + " (" + Mathf.RoundToInt(worst.Value * 100) + "%)");
            foreach (var d in Sim.Amenities.Where(a => a.Group == _shopGroup))
            {
                var def = d;
                string blocked = s.Stars < d.Stars ? "Needs " + d.Stars + " stars" : s.State.money < s.AmenityCost(d.Id) ? "Need " + Format.Money(s.AmenityCost(d.Id) - s.State.money) + " more" : s.FindSpot(d.Id) == null ? "No room" : null;
                Ico(WantIcon(d.Gives), TintTerm).Entry(d.Name, (s.Placed(d.Id) > 0 ? "OWN " + s.Placed(d.Id) + "  ·  " : "") + d.W + " x " + d.D, d.Blurb + "  " + Gives(d), "PLACE\n<size=28>" + Format.Money(s.AmenityCost(d.Id)) + "</size>", blocked, () => StartPlacing(def), Ui.Green);
            }
        }
    }
}
