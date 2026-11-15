using Crosswind.Core;
using UnityEngine;

namespace Crosswind.Game
{
    /// <summary>
    /// The co-op week on the FRIENDS sheet: one goal for you and your friends (passengers handled this week, set for
    /// how many of you have flown), the group's total and your part of it, and four tiers that each pay a few gems
    /// once a week (Core/Sim.Coop.cs). This week's count goes to the friends service every few minutes while signed
    /// in; nothing about any one friend is shown, only the group's total and how many flew.
    /// </summary>
    public sealed partial class Hud
    {
        const float CoopEvery = 180, CoopEveryOpen = 60;     // seconds between reports while playing / with the sheet open
        float _coopSent = -999;

        /// <summary>Every frame: reports this week's passengers when it is time (signed in only).</summary>
        void UpdateCoop()
        {
            if (!FriendsClient.SignedIn) return;
            float every = _tab == FriendsTab ? CoopEveryOpen : CoopEvery;
            if (Time.unscaledTime - _coopSent < every) return;
            _coopSent = Time.unscaledTime;
            var s = _sim;
            FriendsClient.Run(FriendsClient.ReportCoop(s.Week, s.CoopMine, g => { if (s.SetCoopGroup(g.Week, g.Total, g.Mine, g.Members)) _dirty = true; }));
        }

        void CoopSection()
        {
            var s = _sim;
            if (!FriendsClient.SignedIn) return;
            var goal = s.CoopGoal;
            long group = s.CoopGroup, mine = s.CoopMine;
            int members = s.CoopMembers, claimed = s.CoopClaimed, reached = s.CoopReached;
            Header("CO-OP WEEK  ·  ENDS IN " + Format.Time(s.EventLeft).ToUpper());

            int next = Mathf.Min(reached, goal.Length - 1);
            bool all = reached >= goal.Length;
            bool alone = members == 1 && group == mine;
            string body = alone
                ? "You have handled " + Format.Money(mine) + " passengers this week. Every passenger your friends handle counts too; the goal grows as they join in."
                : "Together you have handled " + Format.Money(group) + " passengers this week; your share is " + Format.Money(mine) + " (" + Mathf.RoundToInt((float)s.CoopShareOfGroup * 100) + "%). Set for " + members + " of you: you and the friends who have flown this week.";
            Ico("users", TintSocial).Bar(all ? 1 : group / (double)goal[next], TintGreen).Entry(
                all ? "Every tier reached this week" : "Handle " + Format.Money(goal[next]) + " passengers together",
                Format.Money(group) + (all ? "" : " / " + Format.Money(goal[next])),
                body, null, null, null);

            for (int i = 0; i < goal.Length; i++)
            {
                int gems = Sim.CoopGems[i];
                bool done = i < claimed, ready = i == claimed && group >= goal[i];
                string blocked = ready ? null : group >= goal[i] ? "Claim tier " + (claimed + 1) + " first" : Format.Money(goal[i] - group) + " to go";
                Ico(done ? "medal" : "target", done ? TintPlain : group >= goal[i] ? TintGold : TintSocial).Entry("Tier " + (i + 1), done ? "CLAIMED" : Format.Money(goal[i]) + " passengers",
                    "Together, " + Format.Money(goal[i]) + " passengers this week: " + Gems(gems) + " for you, once a week.",
                    done ? null : "CLAIM\n+" + Gems(gems), blocked, () =>
                    {
                        int got = s.ClaimCoop();
                        if (got <= 0) return;
                        Reward(0, got); Sfx.Play("claim"); Toast("Co-op tier reached: +" + Gems(got));
                        GameRoot.Instance.Save();
                    }, Ui.Gold);
            }
        }
    }
}
