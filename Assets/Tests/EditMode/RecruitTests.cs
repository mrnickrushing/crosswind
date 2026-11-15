using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class RecruitTests
    {
        const double Start = 1_800_000_000;
        double _now;

        Sim Fresh() { _now = Start; var s = new Sim(GameState.New()); s.RealNow = () => _now; s.BuildTimeScale = 0; return s; }

        static void Hold(Sim s, string id, int copies)
        {
            var e = s.State.crew.Find(c => c.id == id);
            if (e == null) s.State.crew.Add(new LevelEntry { id = id, level = copies }); else e.level = copies;
        }

        [Test]
        public void EveryCrateGivesAToken()
        {
            var s = Fresh();
            s.State.crates = 5;
            for (int i = 0; i < 5; i++) { var got = s.OpenCrate(); Assert.AreEqual(1, got.Tokens); Assert.IsFalse(got.Spare); }
            Assert.AreEqual(5, s.State.recruitTokens);
        }

        [Test]
        public void ASpareAtTheTopLevelGivesTokensByRarityAndStillGems()
        {
            var s = Fresh();
            foreach (var c in Sim.Crew) Hold(s, c.Id, 31);
            s.State.crates = 1;
            int gems = s.State.gems;
            var got = s.OpenCrate();
            Assert.IsTrue(got.Spare);
            Assert.AreEqual(1 + Sim.SpareTokens[(int)got.Who.Rarity], got.Tokens);
            Assert.AreEqual(got.Tokens, s.State.recruitTokens);
            Assert.AreEqual(gems + Sim.GemsPerSpare * (1 + (int)got.Who.Rarity), s.State.gems, "the gems back are kept");
            Assert.AreEqual(31, s.Copies(got.Who.Id), "no copy past the top");
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 8 }, Sim.SpareTokens);
        }

        [Test]
        public void TheBoardIsStableWithinAWeekAndChangesTheNext()
        {
            var s = Fresh();
            int week = s.Week;
            var board = new List<string>(s.RecruitBoard);
            Assert.AreEqual(Sim.BoardSize, board.Distinct().Count());
            // Getting someone mid-week doesn't redraw it.
            s.State.recruitTokens = 1000;
            Assert.IsNotNull(s.Recruit(board[0]));
            s.State.crates = 20; while (s.State.crates > 0) s.OpenCrate();
            _now += 3600;
            Assert.AreEqual(week, s.Week);
            CollectionAssert.AreEqual(board, s.RecruitBoard);
            // A new week, a new board, and the hires of the old one are forgotten.
            var last = board;
            for (int w = 1; w <= 30; w++)
            {
                _now = Start + w * 7 * 86400.0;
                Assert.AreEqual(week + w, s.Week);
                var next = new List<string>(s.RecruitBoard);
                CollectionAssert.AreNotEquivalent(last, next, "week " + w);
                Assert.AreEqual(Sim.BoardSize, next.Distinct().Count());
                Assert.LessOrEqual(next.Count(id => Sim.CrewById(id).Rarity == Rarity.Legendary), 1, "at most one Legendary");
                Assert.AreEqual(0, s.State.boardHired.Count);
                last = next;
            }
        }

        [Test]
        public void TheBoardIsTheSameForEveryoneThatWeek()
        {
            var a = Fresh(); var b = Fresh();
            a.State.crates = 30; while (a.State.crates > 0) a.OpenCrate();      // a different collection...
            foreach (var c in Sim.Crew) { Hold(a, c.Id, 1); Hold(b, c.Id, 1); }  // ...made equal: the draw only reads the week and the levels
            CollectionAssert.AreEqual(a.RecruitBoard, b.RecruitBoard);
        }

        [Test]
        public void SomeoneNotMetIsOnTheBoardWhileAnyoneIsLeft()
        {
            var s = Fresh();
            foreach (var c in Sim.Crew) if (c.Id != "quill") Hold(s, c.Id, 3);
            for (int w = 0; w < 40; w++)
            {
                _now = Start + w * 7 * 86400.0;
                Assert.Contains("quill", s.RecruitBoard, "week " + w);
            }
            // Everyone at the top level: the board still fills.
            foreach (var c in Sim.Crew) Hold(s, c.Id, 31);
            _now += 7 * 86400.0;
            Assert.AreEqual(Sim.BoardSize, s.RecruitBoard.Count);
            Assert.AreEqual("Already at the top level", s.RecruitBlocked(s.RecruitBoard[0]));
        }

        [Test]
        public void HiringSpendsTokensAndAddsTheMember()
        {
            var s = Fresh();
            string id = s.RecruitBoard.First(x => s.CrewLevel(x) == 0);
            int cost = s.RecruitCost(id);
            Assert.AreEqual(Sim.RecruitPrice[(int)Sim.CrewById(id).Rarity], cost);
            s.State.recruitTokens = cost - 1;
            Assert.AreEqual("Need 1 more token", s.RecruitBlocked(id));
            Assert.IsNull(s.Recruit(id));
            s.State.recruitTokens = cost + 3;
            var got = s.Recruit(id);
            Assert.IsNotNull(got);
            Assert.IsTrue(got.New);
            Assert.AreEqual(3, s.State.recruitTokens);
            Assert.AreEqual(1, s.CrewLevel(id));
            Assert.IsTrue(s.OnDuty(id), "a free place is filled at once");
            Assert.AreEqual("Hired this week", s.RecruitBlocked(id), "once a week each");
            Assert.AreEqual("Not on the board this week", s.RecruitBlocked(Sim.Crew.First(c => !s.RecruitBoard.Contains(c.Id)).Id));
        }

        [Test]
        public void HiringSomeoneYouHaveAddsACopy()
        {
            var s = Fresh();
            string id = s.RecruitBoard[0];
            Hold(s, id, 2);
            s.State.recruitTokens = 100;
            var got = s.Recruit(id);
            Assert.AreEqual(3, s.Copies(id));
            Assert.IsTrue(got.LevelUp);
            Assert.AreEqual(2, got.Level);
            Assert.AreEqual(0, got.Tokens, "hiring gives no tokens back");
        }

        [Test]
        public void TheWantedPickCostsLessAndIsOnTheBoardAtLeastOneWeekInFour()
        {
            foreach (var d in Sim.Crew)
            {
                var s = Fresh();
                _now = Start;
                var first = s.RecruitBoard;
                Assert.IsTrue(s.SetWanted(d.Id));
                int seenAt = first.Contains(d.Id) ? 0 : -1;
                for (int w = 1; w <= Sim.WantedEvery && seenAt < 0; w++)
                {
                    _now = Start + w * 7 * 86400.0;
                    if (s.RecruitBoard.Contains(d.Id)) seenAt = w;
                }
                Assert.GreaterOrEqual(seenAt, 0, d.Id + " never came");
                Assert.AreEqual(0, s.WantedDueIn);
                int p = Sim.RecruitPrice[(int)d.Rarity];
                Assert.AreEqual(p - p / 4, s.RecruitCost(d.Id));
                Assert.Less(s.RecruitCost(d.Id), p);
                Assert.LessOrEqual(s.RecruitBoard.Count(id => Sim.CrewById(id).Rarity == Rarity.Legendary), 1);
            }
        }

        [Test]
        public void WeeksNobodyLookedCountTowardTheWantedPick()
        {
            var s = Fresh();
            var off = Sim.Crew.First(c => !s.RecruitBoard.Contains(c.Id));
            s.SetWanted(off.Id);
            Assert.AreEqual(Sim.WantedEvery, s.WantedDueIn);
            _now = Start + Sim.WantedEvery * 7 * 86400.0;          // away for four weeks
            Assert.Contains(off.Id, s.RecruitBoard);
        }

        [Test]
        public void TheWantedPickCanBeClearedAndNotSetAtTheTop()
        {
            var s = Fresh();
            Hold(s, "flint", 31);
            Assert.IsFalse(s.SetWanted("flint"));
            Assert.IsFalse(s.SetWanted("nobody"));
            Assert.IsTrue(s.SetWanted("zara"));
            Assert.AreEqual("zara", s.State.wanted);
            Assert.IsTrue(s.SetWanted(null));
            Assert.AreEqual("", s.State.wanted);
            Assert.AreEqual(-1, s.WantedDueIn);
        }

        [Test]
        public void TheCratesRollTheSameWhateverTheBoardAndTheWantedPick()
        {
            var a = Fresh(); var b = Fresh();
            b.SetWanted("flint");
            for (int i = 0; i < 500; i++)
            {
                a.State.crates = 1; b.State.crates = 1;
                if (i % 7 == 0) { _now += 86400; var board = b.RecruitBoard; }
                Assert.AreEqual(a.OpenCrate().Who.Id, b.OpenCrate().Who.Id, "crate " + i);
            }
            Assert.AreEqual(a.State.seed, b.State.seed);
            CollectionAssert.AreEqual(new[] { 70, 22, 7, 1 }, Sim.CrateOdds);
            Assert.AreEqual(10, Sim.CratePity);
        }

        [Test]
        public void AnOldSaveGetsAnEmptyBoardAndNoTokens()
        {
            var st = GameState.New();
            st.board = null; st.boardHired = null; st.wanted = null; st.recruitTokens = -3;
            st.Repair();
            Assert.AreEqual(0, st.recruitTokens);
            Assert.AreEqual("", st.wanted);
            var s = new Sim(st); s.RealNow = () => Start;
            Assert.AreEqual(Sim.BoardSize, s.RecruitBoard.Count);
            st.board = new List<string> { "nobody", "dot", "gus" };
            st.wanted = "nobody";
            st.Repair();
            Assert.AreEqual(0, st.board.Count);
            Assert.AreEqual("", st.wanted);
        }
    }
}
