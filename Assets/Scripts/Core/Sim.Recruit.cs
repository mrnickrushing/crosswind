using System;
using System.Collections.Generic;
using System.Linq;

namespace Crosswind.Core
{
    /// <summary>
    /// A sure way to a particular person, beside the crates (whose odds and Epic guarantee are untouched). Every crate
    /// opened gives a recruit token; a spare copy of someone already at the top level gives more, by rarity. Each week
    /// the recruit board offers three of the team, drawn from the week number (at most one Legendary, someone you have
    /// not met while there is anyone left), each hireable once that week for tokens: a new face joins, someone you have
    /// gains a copy. One wanted pick costs a quarter less there and is on the board at least one week in four. Tokens
    /// come only from crates; nothing sells them, so no money buys a person.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>Tokens to hire someone off the board, by rarity (Common, Rare, Epic, Legendary).</summary>
        public static readonly int[] RecruitPrice = { 5, 12, 30, 80 };
        /// <summary>Tokens on top of the crate's own one for a spare copy of someone at the top level, by rarity.</summary>
        public static readonly int[] SpareTokens = { 1, 2, 4, 8 };
        public const int BoardSize = 3, WantedEvery = 4;

        /// <summary>Adds a copy of someone (nothing past the top level) and fills a free duty place with a new face.</summary>
        CrateResult Join(CrewDef who)
        {
            var res = new CrateResult { Who = who };
            int before = CrewLevel(who.Id);
            if (before >= MaxCrewLevel) res.Spare = true;
            else
            {
                var e = State.crew.Find(l => l.id == who.Id);
                if (e == null) State.crew.Add(new LevelEntry { id = who.Id, level = 1 }); else e.level++;
            }
            res.Level = CrewLevel(who.Id);
            res.New = before == 0;
            res.LevelUp = before > 0 && res.Level > before;
            if (res.New && State.duty.Count(OnDuty) < DutyPlaces && !State.duty.Contains(who.Id)) State.duty.Add(who.Id);    // a free place is filled at once
            return res;
        }

        static int TokensFor(CrateResult r) { return 1 + (r.Spare ? SpareTokens[(int)r.Who.Rarity] : 0); }

        // ------------------------------------------------------------------ the board

        /// <summary>This week's three on offer (ids), drawn the first time the week is looked at and then kept.</summary>
        public List<string> RecruitBoard { get { EnsureBoard(); return State.board; } }
        /// <summary>Seconds until the board changes (Monday at local midnight, with the weekly event).</summary>
        public double RecruitLeft { get { return EventLeft; } }

        void EnsureBoard()
        {
            int week = Week;
            if (State.boardWeek == week && State.board.Count > 0) return;
            if (State.wanted != "" && State.board.Count > 0 && week > State.boardWeek + 1)
                State.wantedMissed += week - State.boardWeek - 1;       // weeks nobody looked count as missed: that only brings the wanted pick sooner
            State.board = DrawBoard(week, State.wanted != "" && State.wantedMissed >= WantedEvery - 1 ? State.wanted : null);
            State.boardWeek = week;
            State.boardHired.Clear();
            if (State.wanted != "") State.wantedMissed = State.board.Contains(State.wanted) ? 0 : State.wantedMissed + 1;
        }

        /// <summary>
        /// Three people for a week: a shuffle seeded by the week alone (its own dice, so the run's never shift), those
        /// not yet at the top level first, at most one Legendary, and someone not met yet while anyone is left.
        /// </summary>
        List<string> DrawBoard(int week, string force)
        {
            uint x = (uint)week * 2654435761u ^ 0x5bd1e995u;
            if (x == 0) x = 1;
            Func<double> rand = () => { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return (x & 0xFFFFFF) / (double)0x1000000; };
            for (int i = 0; i < 4; i++) rand();
            Func<List<CrewDef>, List<CrewDef>> shuffle = list =>
            {
                for (int i = list.Count - 1; i > 0; i--) { int j = Math.Min(i, (int)(rand() * (i + 1))); var t = list[i]; list[i] = list[j]; list[j] = t; }
                return list;
            };
            var order = shuffle(Crew.Where(c => CrewLevel(c.Id) < MaxCrewLevel).ToList());
            order.AddRange(shuffle(Crew.Where(c => CrewLevel(c.Id) >= MaxCrewLevel).ToList()));    // only if the others run out
            Func<CrewDef, bool> legend = c => c.Rarity == Rarity.Legendary;

            var picks = new List<CrewDef>();
            var forced = force != null ? CrewById(force) : null;
            if (forced != null && CrewLevel(forced.Id) >= MaxCrewLevel) forced = null;
            if (forced != null) picks.Add(forced);
            foreach (var c in order)
            {
                if (picks.Count >= BoardSize) break;
                if (picks.Contains(c) || (legend(c) && picks.Any(legend))) continue;
                picks.Add(c);
            }
            if (!picks.Any(c => CrewLevel(c.Id) == 0))
                foreach (var c in order)
                {
                    if (CrewLevel(c.Id) > 0 || picks.Contains(c)) continue;
                    int slot = picks.Count - 1;
                    if (legend(c)) { int l = picks.FindIndex(p => legend(p)); if (l >= 0) slot = l; }
                    if (forced != null && slot == 0) continue;      // the wanted pick keeps its place
                    picks[slot] = c;
                    break;
                }
            return picks.OrderBy(c => (int)c.Rarity).ThenBy(c => Crew.IndexOf(c)).Select(c => c.Id).ToList();
        }

        /// <summary>Tokens to hire someone off the board (a quarter less for the wanted pick).</summary>
        public int RecruitCost(string id)
        {
            var d = CrewById(id);
            if (d == null) return 0;
            int p = RecruitPrice[(int)d.Rarity];
            return id == State.wanted ? p - p / 4 : p;
        }

        public string RecruitBlocked(string id)
        {
            if (!RecruitBoard.Contains(id)) return "Not on the board this week";
            if (State.boardHired.Contains(id)) return "Hired this week";
            if (CrewLevel(id) >= MaxCrewLevel) return "Already at the top level";
            int need = RecruitCost(id) - State.recruitTokens;
            if (need > 0) return "Need " + need + " more token" + (need == 1 ? "" : "s");
            return null;
        }

        /// <summary>Hires someone off the board for tokens: they join, or gain a copy. Null when it can't be done.</summary>
        public CrateResult Recruit(string id)
        {
            if (RecruitBlocked(id) != null) return null;
            State.recruitTokens -= RecruitCost(id);
            State.boardHired.Add(id);
            var res = Join(CrewById(id));
            Changed?.Invoke();
            return res;
        }

        // ------------------------------------------------------------------ the wanted pick

        public string WantedBlocked(string id)
        {
            if (CrewById(id) == null) return "Nobody by that name";
            if (CrewLevel(id) >= MaxCrewLevel) return "Already at the top level";
            return null;
        }

        /// <summary>Sets the one person you are working toward ("" or null clears it). A new pick starts the four weeks again.</summary>
        public bool SetWanted(string id)
        {
            id = id ?? "";
            if (id == State.wanted) return true;
            if (id != "" && WantedBlocked(id) != null) return false;
            State.wanted = id;
            State.wantedMissed = 0;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Weeks until the wanted pick is sure to be on the board: 0 when they are on it now, -1 with no pick (or one at the top level).</summary>
        public int WantedDueIn
        {
            get
            {
                if (State.wanted == "" || CrewLevel(State.wanted) >= MaxCrewLevel) return -1;
                if (RecruitBoard.Contains(State.wanted)) return 0;
                return Math.Max(1, WantedEvery - State.wantedMissed);
            }
        }
    }
}
