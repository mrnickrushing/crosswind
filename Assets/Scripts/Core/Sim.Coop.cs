using System;

namespace Crosswind.Core
{
    /// <summary>
    /// The co-op week: you and your friends handle passengers toward one shared goal. Each airport counts its own
    /// passengers for the week (both ways, every flight that pays, at whichever airport is being run); the friends
    /// service (Tools/friends-worker, /v1/coop/report) adds the caller's count to their mutual friends' for the same
    /// week. The goal grows with the group, and each of its tiers pays a few gems once a week, claimed here: the
    /// server only ever reports totals, the save keeps what was claimed.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>A solo player's tiers (passengers in a week). Keep in step with COOP_BASE in worker.js.</summary>
        public static readonly long[] CoopBase = { 2500, 10000, 30000, 80000 };
        public static readonly int[] CoopGems = { 3, 5, 8, 12 };
        /// <summary>You and every friend the server can count (the list holds at most 50).</summary>
        public const int CoopMaxMembers = FriendRules.MaxFriends + 1;

        /// <summary>
        /// Passengers the group needs for this tier: a solo goal, and four fifths of one more for every other member,
        /// so a group is a little easier per head than playing alone. Whole numbers only (the server works it out the
        /// same way and the two must agree to the passenger).
        /// </summary>
        public static long CoopTarget(int tier, int members)
        {
            members = Math.Max(1, Math.Min(CoopMaxMembers, members));
            return CoopBase[tier] * (5 + 4L * (members - 1)) / 5;
        }

        public static long[] CoopTargets(int members)
        {
            var t = new long[CoopBase.Length];
            for (int i = 0; i < t.Length; i++) t[i] = CoopTarget(i, members);
            return t;
        }

        void RollCoop()
        {
            if (State.coopWeek == Week) return;
            State.coopWeek = Week; State.coopPassengers = 0; State.coopFriends = 0; State.coopMembers = 1; State.coopClaimed = 0;
        }

        void CountCoop(long passengers)
        {
            RollCoop();
            State.coopPassengers += Math.Max(0, passengers);
        }

        /// <summary>Passengers this airport has handled this week.</summary>
        public long CoopMine { get { RollCoop(); return State.coopPassengers; } }
        /// <summary>Your count now plus the friends' as the server last said: your own share never waits for the server.</summary>
        public long CoopGroup { get { RollCoop(); return State.coopPassengers + State.coopFriends; } }
        public int CoopMembers { get { RollCoop(); return State.coopMembers; } }
        public long[] CoopGoal { get { return CoopTargets(CoopMembers); } }
        /// <summary>Your part of the group's passengers, 0..1 (1 alone).</summary>
        public double CoopShareOfGroup { get { long g = CoopGroup; return g > 0 ? (double)CoopMine / g : 1; } }

        /// <summary>Tiers the group has reached this week (0..4).</summary>
        public int CoopReached
        {
            get { long g = CoopGroup; int n = 0; foreach (var t in CoopGoal) if (g >= t) n++; return n; }
        }

        public int CoopClaimed { get { RollCoop(); return State.coopClaimed; } }
        /// <summary>Tiers reached but not yet claimed this week.</summary>
        public int CoopWaiting { get { return Math.Max(0, CoopReached - CoopClaimed); } }

        /// <summary>Claims the next tier reached; its gems, or 0 when none is waiting. Each tier pays once a week, in order,
        /// even if the goal later grows past it (a friend joined) or the week's total is reported lower.</summary>
        public int ClaimCoop()
        {
            if (CoopWaiting <= 0) return 0;
            int gems = CoopGems[State.coopClaimed];
            State.coopClaimed++;
            State.gems += gems;
            Changed?.Invoke();
            return gems;
        }

        /// <summary>
        /// What the server said of the group: the week it counted, the total over you and your friends, your own count
        /// as it holds it, and the members counted. A reply for another week (Monday came while it was on its way) is
        /// ignored. The friends' part is kept, so the group total moves with your own flights between reports.
        /// </summary>
        public bool SetCoopGroup(int week, long total, long mine, int members)
        {
            RollCoop();
            if (week != Week) return false;
            State.coopFriends = Math.Max(0, total - Math.Max(0, mine));
            State.coopMembers = Math.Max(1, Math.Min(CoopMaxMembers, members));
            Changed?.Invoke();
            return true;
        }
    }
}
