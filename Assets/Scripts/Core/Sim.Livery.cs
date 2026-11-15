using System;

namespace Crosswind.Core
{
    /// <summary>A paint pattern or tail emblem for your own aircraft, and what opens it: a star rating or a fleet size.</summary>
    public sealed class LiveryItem
    {
        public string Id, Name, Blurb;
        public int Stars;       // opens at this rating (0: from the start)
        public int Fleet;       // or at this many aircraft owned at once
    }

    /// <summary>
    /// Your airline's look beyond its colour: a paint pattern, a second (trim) colour, an emblem on the fin and a
    /// name. Patterns and emblems open through play, by star rating or by the size of the fleet; nothing is bought.
    /// Zero everywhere is the look the airline had before any of this (Classic, matched trim, no emblem), so older
    /// saves keep it. What has been earned stays earned after selling up: having sold means having reached the top
    /// rating, and the largest fleet is remembered (State.fleetBest).
    /// </summary>
    public sealed partial class Sim
    {
        public const int AirlineNameMax = FriendRules.NameMax;
        public const string DefaultAirlineName = "Your airline";

        public static readonly LiveryItem[] LiveryPatterns =
        {
            new LiveryItem { Id = "classic", Name = "Classic", Blurb = "A swept tail and a painted belly, the look your airline started with." },
            new LiveryItem { Id = "plain", Name = "Plain", Blurb = "A clean white fuselage under a painted tail." },
            new LiveryItem { Id = "cheatline", Name = "Cheatline", Blurb = "A stripe in your colour along the cabin, a trim line under it.", Stars = 2 },
            new LiveryItem { Id = "twotone", Name = "Two-Tone", Blurb = "The lower half in your colour, a trim line along the join.", Stars = 3 },
            new LiveryItem { Id = "swoosh", Name = "Swoosh", Blurb = "A band sweeping up from the nose to the tail, edged in trim.", Stars = 4 },
            new LiveryItem { Id = "full", Name = "Full Colour", Blurb = "The whole fuselage in your colour, the tail in your trim.", Fleet = 5 },
        };

        public static readonly LiveryItem[] Emblems =
        {
            new LiveryItem { Id = "none", Name = "None", Blurb = "A bare fin." },
            new LiveryItem { Id = "star", Name = "Star", Blurb = "A five-pointed star on the fin." },
            new LiveryItem { Id = "chevron", Name = "Chevron", Blurb = "A forward chevron, for an airline in a hurry.", Stars = 2 },
            new LiveryItem { Id = "ring", Name = "Ring", Blurb = "A bold ring round the fin's middle.", Stars = 3 },
            new LiveryItem { Id = "wing", Name = "Wing", Blurb = "Three swept feathers.", Fleet = 2 },
            new LiveryItem { Id = "leaf", Name = "Leaf", Blurb = "A single leaf, for the quiet ones.", Stars = 5 },
        };

        /// <summary>Trim colours: 0 matches the pattern (a pale shade of your colour, or white), then the palette.</summary>
        public static readonly string[] Trims = { "Matched", "Skyline Blue", "Signal Red", "Forest Green", "Sunrise Orange", "Royal Purple", "Graphite" };

        /// <summary>The trim's index into <see cref="LiveryHex"/>, or -1 for the pattern's own match.</summary>
        public static int TrimColour(int trim) { return trim <= 0 || trim >= Trims.Length ? -1 : trim - 1; }

        /// <summary>The best rating reached: selling a stake needs the top one, so that counts after a move.</summary>
        public int LiveryStars { get { return Math.Max(Stars, State.mapsRun.Count > 0 ? Content.SellStars : 0); } }

        /// <summary>The most aircraft owned at once, at any airport.</summary>
        public int FleetBest { get { return Math.Max(FleetSize, State.fleetBest); } }

        string ItemBlocked(LiveryItem[] items, int i)
        {
            if (i < 0 || i >= items.Length) return "Unknown";
            var it = items[i];
            if (it.Stars > 0 && LiveryStars < it.Stars) return "Opens at " + it.Stars + " stars";
            if (it.Fleet > 0 && FleetBest < it.Fleet) return "Opens with " + it.Fleet + " aircraft";
            return null;
        }

        public string PatternBlocked(int i) { return ItemBlocked(LiveryPatterns, i); }
        public string EmblemBlocked(int i) { return ItemBlocked(Emblems, i); }

        public LiveryItem Pattern { get { return LiveryPatterns[Clamp(State.liveryPattern, LiveryPatterns.Length)]; } }
        public LiveryItem Emblem { get { return Emblems[Clamp(State.emblem, Emblems.Length)]; } }
        public string TrimName { get { return Trims[Clamp(State.liveryTrim, Trims.Length)]; } }

        static int Clamp(int i, int n) { return i < 0 || i >= n ? 0 : i; }

        public bool SetPattern(int i)
        {
            if (PatternBlocked(i) != null) return false;
            State.liveryPattern = i;
            Changed?.Invoke();
            return true;
        }

        public bool SetEmblem(int i)
        {
            if (EmblemBlocked(i) != null) return false;
            State.emblem = i;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Trim colours are all open; the index wraps like <see cref="SetLivery"/>.</summary>
        public void SetTrim(int i)
        {
            State.liveryTrim = ((i % Trims.Length) + Trims.Length) % Trims.Length;
            Changed?.Invoke();
        }

        /// <summary>The next open pattern or emblem after the current one (wrapping), for a cycle button.</summary>
        public int NextPattern() { return NextOpen(LiveryPatterns, State.liveryPattern, PatternBlocked); }
        public int NextEmblem() { return NextOpen(Emblems, State.emblem, EmblemBlocked); }

        static int NextOpen(LiveryItem[] items, int from, Func<int, string> blocked)
        {
            for (int k = 1; k <= items.Length; k++)
            {
                int i = (from + k) % items.Length;
                if (blocked(i) == null) return i;
            }
            return from;
        }

        /// <summary>How many of the patterns (or the emblems) are open.</summary>
        public int OpenCount(bool emblems)
        {
            var items = emblems ? Emblems : LiveryPatterns; int n = 0;
            for (int i = 0; i < items.Length; i++) if (ItemBlocked(items, i) == null) n++;
            return n;
        }

        public string AirlineName { get { return string.IsNullOrEmpty(State.airlineName) ? DefaultAirlineName : State.airlineName; } }

        /// <summary>
        /// Names your airline, screened like a manager's name (FriendRules: letters, digits and a little punctuation,
        /// at most 20, nothing rude). An empty name goes back to "Your airline"; a refused one changes nothing.
        /// </summary>
        public bool RenameAirline(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) { State.airlineName = ""; Changed?.Invoke(); return true; }
            if (FriendRules.Rude(name)) return false;
            string clean = FriendRules.CleanName(name);
            if (clean == FriendRules.DefaultName && name.Trim() != FriendRules.DefaultName) return false;
            State.airlineName = clean;
            Changed?.Invoke();
            return true;
        }
    }
}
