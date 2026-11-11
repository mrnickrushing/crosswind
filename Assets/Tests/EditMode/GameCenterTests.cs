using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>docs/appstore/gamecenter.json is what App Store Connect is set up from: it has to list the game's achievements.</summary>
    public class GameCenterTests
    {
        static string File()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                {
                    var p = Path.Combine(d.FullName, "docs", "appstore", "gamecenter.json");
                    if (System.IO.File.Exists(p)) return System.IO.File.ReadAllText(p);
                }
            Assert.Fail("docs/appstore/gamecenter.json not found");
            return null;
        }

        [Test]
        public void EveryAchievementIsInTheGameCenterFile()
        {
            var json = File();
            var listed = Regex.Matches(json, "\"id\": \"crosswind\\.ach\\.([a-z0-9_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            CollectionAssert.AreEquivalent(Sim.Achievements.Select(a => a.Id).ToList(), listed);
            foreach (var a in Sim.Achievements)
            {
                var m = Regex.Match(json, "\"id\": \"crosswind\\.ach\\." + a.Id + "\",[^}]*?\"points\": (\\d+)", RegexOptions.Singleline);
                Assert.IsTrue(m.Success, a.Id);
                Assert.AreEqual(a.Gems, int.Parse(m.Groups[1].Value), a.Id + ": Game Center points are the gems");
            }
            Assert.LessOrEqual(Sim.Achievements.Sum(a => a.Gems), 1000, "Game Center allows 1,000 points in all");
            Assert.LessOrEqual(Sim.Achievements.Max(a => a.Gems), 100, "and 100 for one achievement");
        }

        [Test]
        public void TheThreeBoardsAreInTheFile()
        {
            var json = File();
            foreach (var id in new[] { "crosswind.board.flights", "crosswind.board.wings", "crosswind.board.topdays" }) StringAssert.Contains("\"" + id + "\"", json);
        }
    }
}
