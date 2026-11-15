using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Reach on the terminal floor, as the player sees it: the floor map (a rating's SHOW ON THE FLOOR, and on its own
    /// while something is being placed, for that thing's want), how much of the floor each rating reaches, the gain
    /// a spot would bring while placing, UNDO for the last changes to the floor, and a word the first time an older
    /// layout is seen under the new rule.
    /// </summary>
    public sealed partial class Hud
    {
        int _overlayWant = -1;
        string _hintKey, _hint;
        Button _undo;
        Text _undoText;

        void NeedOverlay(Want? want)
        {
            _overlayWant = want.HasValue ? (int)want.Value : -1;
            if (want.HasValue) Telemetry.Feature("floor_map", want.Value.ToString());
        }

        /// <summary>The need card's and BUILD's line on reach ("reaches 64% of the open floor").</summary>
        string NeedCoverageLine(Need n)
        {
            if (n.Queue || !Sim.HasReach(n.Want) || n.Unasked || _sim.Supply(n.Want) <= 0) return "";
            float c = _sim.Coverage(n.Want);
            string col = c >= 0.85f ? "#5ED58A" : c >= 0.6f ? "#FFC83D" : "#FF7A6B";
            return "\n<color=" + col + ">Reaches " + Mathf.RoundToInt(c * 100) + "% of the open floor</color>"
                + (_sim.ReachGrace > 0 ? "<color=#FFFFFF90> (your older layout counts in full for a while yet)</color>" : c < 0.85f ? "<color=#FFFFFF90>: passengers far from one rate it lower</color>" : "");
        }

        void BuildUndo()
        {
            _undo = Ui.Button("Undo", _safe, null, Ui.Card, () =>
            {
                string what = _sim.UndoLabel, why = _sim.UndoBlocked;
                if (why != null) { Toast(why); Sfx.Play("error", 0.5f); return; }
                if (_sim.Undo()) { Toast("Undone: " + what.ToLower()); Sfx.Play("close", 0.6f); Telemetry.LayoutEdit("undone"); ClosePick(); GameRoot.Instance.Save(); }
            }, 24);
            ((RectTransform)_undo.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-520, 124), new Vector2(300, 76));
            var icon = Ui.Img("Icon", _undo.transform, Color.white); icon.sprite = Ui.Ic("undo"); icon.preserveAspect = true;
            icon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 3), new Vector2(36, 36));
            _undoText = Ui.Label("Text", _undo.transform, "", 22, Color.white, TextAnchor.MiddleLeft, false);
            _undoText.rectTransform.Fill(); _undoText.rectTransform.offsetMin = new Vector2(64, 10); _undoText.rectTransform.offsetMax = new Vector2(-10, -6);
            _undoText.resizeTextForBestFit = true; _undoText.resizeTextMinSize = 14; _undoText.resizeTextMaxSize = 22; _undoText.verticalOverflow = VerticalWrapMode.Truncate;
            _undo.gameObject.SetActive(false);
        }

        /// <summary>Every frame indoors: the floor map, the UNDO button, and the one-time word about reach.</summary>
        void UpdateCoverage(bool inside)
        {
            if (_undo == null) BuildUndo();
            // The map: the placing item's want while placing, else the one the player asked to see.
            Want? show = null; PlacedAmenity ghost = null;
            if (inside && _placing != null && Sim.HasReach(_placing.Gives))
            {
                show = _placing.Gives;
                ghost = new PlacedAmenity { id = _placing.Id, x = _placeX, z = _placeZ, rot = _placeRot, level = _moving != null ? _moving.level : 1 };
            }
            else if (inside && _overlayWant >= 0 && _need >= 0) show = (Want)_overlayWant;
            FloorMap.Show(show, ghost);

            bool cardUp = _need >= 0 || _picked != null || _bay >= 0 || _paxCard.gameObject.activeSelf;
            string label = inside && _tab < 0 && _placing == null && !cardUp && !PhotoMode ? _sim.UndoLabel : null;
            if (_undo.gameObject.activeSelf != (label != null)) _undo.gameObject.SetActive(label != null);
            if (label != null) _undoText.text = "UNDO\n<color=#FFFFFFB0>" + label + "</color>";

            // The first look inside an older layout under the new rule: say what changed and where to look.
            if (inside && _tab < 0 && _sim.ReachGrace > 0 && PlayerPrefs.GetInt("cw_hint_reach", 0) == 0)
            {
                PlayerPrefs.SetInt("cw_hint_reach", 1); PlayerPrefs.Save();
                Toast("Passengers now walk to what they need: tap a rating to see where it's missing"); _toastT = 6f;
            }
        }

        /// <summary>The placing bar's word on reach: how much more floor this spot brings in.</summary>
        string PlaceReachHint()
        {
            if (_placing == null || !Sim.HasReach(_placing.Gives)) return "";
            // Worked out once per spot (the bar is redrawn every frame).
            string key = _placing.Id + "|" + _placeX + "|" + _placeZ + "|" + _placeRot + "|" + _sim.State.terminal.Count;
            if (key == _hintKey) return _hint;
            _hintKey = key;
            float gain = _sim.CoverageGain(_placing.Id, _placeX, _placeZ, _placeRot, _moving);
            return _hint = gain >= 0.005f ? "  <color=#FFD24A>+" + Mathf.RoundToInt(gain * 100) + "% of the floor in reach</color>" : "  <color=#FFFFFF80>reaches nothing new here</color>";
        }
    }
}
