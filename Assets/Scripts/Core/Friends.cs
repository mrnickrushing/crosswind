using System;
using System.Text;

namespace Crosswind.Core
{
    /// <summary>A friend's airport as the friends service (Tools/friends-worker) lists it.</summary>
    [Serializable]
    public sealed class FriendInfo
    {
        public string Code;
        public string Name;
        public string Map;
        public int Stars;
        public int Flights;
        public double LastSeen;     // unix seconds
    }

    /// <summary>
    /// The friends rules that don't need the network: what a display name and a friend code may look like (the same
    /// as the server's, worker.js), how "last seen" reads, and when a friend's gift can be collected again.
    /// </summary>
    public static class FriendRules
    {
        /// <summary>Friend codes are drawn from this: no 0/O/1/I, so a code reads the same aloud or handwritten.</summary>
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 6;
        public const int NameMax = 20;
        public const int MaxFriends = 50;
        public const string DefaultName = "Airport Manager";
        /// <summary>One gift per friend every 20 hours: a daily habit that doesn't slip later each day.</summary>
        public const double GiftEvery = 20 * 3600;
        public const double OnlineWithin = 5 * 60;

        /// <summary>
        /// The display name as the server will store it: letters, digits, spaces and '-._ only (so no control
        /// characters, emoji or markup), runs of spaces become one, at most 20 characters, and "Airport Manager"
        /// when nothing is left.
        /// </summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name) || Rude(name)) return DefaultName;
            var sb = new StringBuilder(NameMax);
            int n = Math.Min(name.Length, 400);
            for (int i = 0; i < n; i++)
            {
                char c = name[i];
                bool ok = char.IsLetter(c) || char.IsDigit(c) || c == ' ' || c == '\'' || c == '-' || c == '.' || c == '_';
                if (!ok) continue;
                if (c == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' ')) continue;
                sb.Append(c);
            }
            if (sb.Length > NameMax) sb.Length = NameMax;
            string s = sb.ToString().Trim(' ');
            return s.Length == 0 || Rude(s) ? DefaultName : s;
        }

        // Names other players see are screened (App Review guideline 1.2), the same way as the server (worker.js):
        // folded to lower-case letters (common digit and symbol swaps undone, everything else a gap), then refused
        // when it holds one of Roots anywhere, even split up by gaps, or one of Words as a whole word.
        static readonly string[] Roots = { "fuck", "fuk", "shit", "cunt", "nigg", "niga", "faggot", "bitch", "whore", "slut",
            "rapist", "nazi", "hitler", "porn", "penis", "vagina", "retard", "wank", "twat", "kike", "chink", "dildo", "pussy",
            "bastard", "asshole", "jizz", "boner", "blowjob", "handjob", "kkk" };
        static readonly string[] Words = { "ass", "arse", "dicks", "cock", "cocks", "fag", "fags", "spic", "tit", "tits", "cum",
            "sex", "rape", "raped", "nude", "nudes", "anal", "piss", "coon", "gook", "homo", "dyke", "tranny" };
        const string SwapFrom = "0134578@$!", SwapTo = "oieastbasi";

        /// <summary>True when a name would be refused for what it says (it then shows as "Airport Manager").</summary>
        public static bool Rude(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int n = Math.Min(name.Length, 400);
            var folded = new StringBuilder(n);
            for (int i = 0; i < n; i++)
            {
                char c = char.ToLowerInvariant(name[i]);
                int k = SwapFrom.IndexOf(c);
                if (k >= 0) c = SwapTo[k];
                folded.Append(c >= 'a' && c <= 'z' ? c : ' ');
            }
            var words = folded.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string joined = string.Concat(words);
            foreach (var r in Roots) if (joined.Contains(r)) return true;
            foreach (var w in words) if (Array.IndexOf(Words, w) >= 0) return true;
            return false;
        }

        /// <summary>The code in upper case when it is six characters of <see cref="Alphabet"/> (typed in any case,
        /// spaces around it ignored), else null.</summary>
        public static string ValidCode(string code)
        {
            if (code == null) return null;
            string c = code.Trim().ToUpperInvariant();
            if (c.Length != CodeLength) return null;
            for (int i = 0; i < c.Length; i++) if (Alphabet.IndexOf(c[i]) < 0) return null;
            return c;
        }

        /// <summary>"online now" under 5 minutes, then "12 min ago", "3 h ago", "2 days ago".</summary>
        public static string LastSeenText(double lastSeenUnix, double nowUnix)
        {
            double ago = nowUnix - lastSeenUnix;
            if (double.IsNaN(ago) || ago < OnlineWithin) return "online now";   // also a clock a little behind the server's
            if (ago < 3600) return (int)(ago / 60) + " min ago";
            if (ago < 86400) return (int)(ago / 3600) + " h ago";
            double days = Math.Floor(ago / 86400);
            if (days > 9999) return "long ago";
            return days < 2 ? "1 day ago" : (int)days + " days ago";
        }

        /// <summary>True when this friend's gift can be collected: never collected (0), or 20 hours have passed.
        /// A collection stamped in the future (the phone's clock was set back) is not ready, so winding the clock
        /// forward and back doesn't pay out twice.</summary>
        public static bool GiftReady(double lastGiftUnix, double nowUnix)
        {
            if (lastGiftUnix <= 0) return true;
            return nowUnix - lastGiftUnix >= GiftEvery;
        }

        /// <summary>Seconds until this friend's gift is ready (0 when it is).</summary>
        public static double GiftWait(double lastGiftUnix, double nowUnix)
        {
            if (GiftReady(lastGiftUnix, nowUnix)) return 0;
            return Math.Min(GiftEvery, GiftEvery - (nowUnix - lastGiftUnix));
        }
    }
}
