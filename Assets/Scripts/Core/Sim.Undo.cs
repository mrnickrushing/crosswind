using System;
using System.Collections.Generic;

namespace Crosswind.Core
{
    /// <summary>One change to the terminal floor that UNDO can take back.</summary>
    public sealed class TerminalEdit
    {
        public string Kind;             // placed, moved, upgraded, sold
        public PlacedAmenity Item;      // the item itself (a sold one is kept here, ready to go back)
        public int X, Z, Rot;           // where it stood before a move, or where it stood when sold
        public double Money;            // what the change cost (placed, upgraded) or paid (sold)
        public double Rating;           // the rating it earned (taken back with it)
        public double At;               // real time it was made
    }

    /// <summary>
    /// UNDO for the terminal floor: the last few placements, moves, upgrades and sales can be taken back, newest first,
    /// for a while after they were made, exactly (the money paid comes back, the rating earned goes, a sale is bought
    /// back for what it paid). Not saved: it belongs to the sitting. Taking a change back also takes back what it
    /// counted toward the week's event and today's tasks, so nothing can be farmed by placing and undoing.
    /// </summary>
    public sealed partial class Sim
    {
        public const int UndoDepth = 12;
        /// <summary>How long a change can be taken back (seconds of real time).</summary>
        public const double UndoSeconds = 600;
        readonly List<TerminalEdit> _undo = new List<TerminalEdit>();

        void Remember(string kind, PlacedAmenity item, int x, int z, int rot, double money, double rating)
        {
            _undo.Add(new TerminalEdit { Kind = kind, Item = item, X = x, Z = z, Rot = rot, Money = money, Rating = rating, At = RealNow() });
            if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
        }

        /// <summary>The change UNDO would take back (null: nothing recent).</summary>
        public TerminalEdit LastEdit
        {
            get
            {
                _undo.RemoveAll(e => RealNow() - e.At > UndoSeconds);
                return _undo.Count > 0 ? _undo[_undo.Count - 1] : null;
            }
        }

        /// <summary>What UNDO says it will do ("Undo: Toilets placed").</summary>
        public string UndoLabel
        {
            get
            {
                var e = LastEdit;
                return e == null ? null : AmenityById(e.Item.id).Name + " " + e.Kind;
            }
        }

        /// <summary>Why the last change can't be taken back (null: it can).</summary>
        public string UndoBlocked
        {
            get
            {
                var e = LastEdit;
                if (e == null) return "Nothing to undo";
                var d = AmenityById(e.Item.id);
                switch (e.Kind)
                {
                    case "placed": case "upgraded": return State.terminal.Contains(e.Item) ? null : "It has gone";
                    case "moved": return !State.terminal.Contains(e.Item) ? "It has gone" : FloorFree(d, e.X, e.Z, e.Rot, e.Item) ? null : "Its old spot is taken";
                    default:
                        if (!FloorFree(d, e.X, e.Z, e.Rot)) return "Its old spot is taken";
                        return State.money < e.Money ? "Need " + Format.Money(e.Money - State.money) + " to buy it back" : null;
                }
            }
        }

        /// <summary>Takes back the last change to the floor. Returns false (and changes nothing) when it can't.</summary>
        public bool Undo()
        {
            if (UndoBlocked != null) return false;
            var e = LastEdit;
            _undo.RemoveAt(_undo.Count - 1);
            var p = e.Item;
            switch (e.Kind)
            {
                case "placed":
                    State.terminal.Remove(p);
                    State.money += e.Money; UnRate(e.Rating); Uncount("upgrade", 1);
                    break;
                case "upgraded":
                    p.level = Math.Max(1, p.level - 1);
                    State.money += e.Money; UnRate(e.Rating); Uncount("upgrade", 1);
                    break;
                case "moved":
                    p.x = e.X; p.z = e.Z; p.rot = e.Rot;
                    break;
                default:        // sold: back where it stood, for what the sale paid
                    p.x = e.X; p.z = e.Z; p.rot = e.Rot;
                    State.terminal.Add(p);
                    State.money -= e.Money;
                    break;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes back rating a change earned, never below the star the airport has (stars, once won, stay).</summary>
        void UnRate(double xp)
        {
            State.ratingXp = Math.Max(Content.StarXp[Stars], State.ratingXp - xp);
        }

        /// <summary>Takes back what a change counted toward the week's event and today's tasks.</summary>
        void Uncount(string kind, double n)
        {
            RollEvent();
            if (CurrentEvent.Counts == kind) State.eventProgress = Math.Max(0, State.eventProgress - n);
            foreach (var t in State.tasks) if (t.kind == kind && !t.claimed) t.progress = Math.Max(0, t.progress - n);
        }
    }
}
