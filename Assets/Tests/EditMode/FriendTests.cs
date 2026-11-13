using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class FriendTests
    {
        const double Now = 1_800_000_000;

        [Test]
        public void RudeNamesAreRefused()
        {
            foreach (var bad in new[] { "F u c k", "sh1t head", "Big Ass", "N.a.z.i", "Pen Island" })
                Assert.AreEqual(FriendRules.DefaultName, FriendRules.CleanName(bad), bad);
            foreach (var fine in new[] { "Cassandra", "Dickens", "Bassett", "Grass Hopper", "J0hn", "Dick Smith", "Essex Air" })
                Assert.AreEqual(fine, FriendRules.CleanName(fine), fine);
        }

        [Test]
        public void ANameKeepsLettersDigitsAndAFewMarks()
        {
            Assert.AreEqual("O'Hare-2 Intl._", FriendRules.CleanName("O'Hare-2 Intl._"));
            Assert.AreEqual("Zoë Ångström 7", FriendRules.CleanName("Zoë Ångström 7"));
            Assert.AreEqual("Test One", FriendRules.CleanName("  Te<st>\u0007\n One!!  "));
            Assert.AreEqual("scriptalert1script", FriendRules.CleanName("<script>alert(1)</script>"));
            Assert.AreEqual("Sky Port", FriendRules.CleanName("Sky ✈️ 😀 Port"), "emoji go, and the gap they leave closes");
        }

        [Test]
        public void ANameIsAtMostTwentyCharactersAndNeverEmpty()
        {
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRST", FriendRules.CleanName("ABCDEFGHIJKLMNOPQRSTUVWXYZ"));
            Assert.AreEqual("Nineteen characters", FriendRules.CleanName("Nineteen characters and more"), "no space left at the cut");
            Assert.AreEqual(FriendRules.DefaultName, FriendRules.CleanName(null));
            Assert.AreEqual(FriendRules.DefaultName, FriendRules.CleanName(""));
            Assert.AreEqual(FriendRules.DefaultName, FriendRules.CleanName("   \t "));
            Assert.AreEqual(FriendRules.DefaultName, FriendRules.CleanName("!!! \u0000 ???"));
            Assert.AreEqual("Airport Manager", FriendRules.DefaultName);
            Assert.AreEqual("Airport Manager", FriendRules.CleanName(FriendRules.DefaultName), "cleaning twice changes nothing");
        }

        [Test]
        public void ACodeIsSixUnambiguousCharactersInAnyCase()
        {
            Assert.AreEqual("PWW7RT", FriendRules.ValidCode("PWW7RT"));
            Assert.AreEqual("PWW7RT", FriendRules.ValidCode(" pww7rt\n"));
            Assert.IsNull(FriendRules.ValidCode(null));
            Assert.IsNull(FriendRules.ValidCode(""));
            Assert.IsNull(FriendRules.ValidCode("PWW7R"));
            Assert.IsNull(FriendRules.ValidCode("PWW7RTX"));
            Assert.IsNull(FriendRules.ValidCode("PWW 7RT"));
            foreach (var bad in new[] { "PWW7R0", "PWW7RO", "PWW7R1", "PWW7RI", "pww7ri", "PWW7R-", "PWW7RÉ" })
                Assert.IsNull(FriendRules.ValidCode(bad), bad);
            Assert.AreEqual(32, FriendRules.Alphabet.Length);
            foreach (char c in "0O1I") Assert.Less(FriendRules.Alphabet.IndexOf(c), 0);
            Assert.AreEqual("ABCDEF", FriendRules.ValidCode("abcdef"));
            Assert.AreEqual("234567", FriendRules.ValidCode("234567"));
        }

        [Test]
        public void LastSeenReadsInTheLargestUnitThatFits()
        {
            Assert.AreEqual("online now", FriendRules.LastSeenText(Now, Now));
            Assert.AreEqual("online now", FriendRules.LastSeenText(Now - 299, Now));
            Assert.AreEqual("online now", FriendRules.LastSeenText(Now + 120, Now), "the server's clock runs ahead of the phone's");
            Assert.AreEqual("5 min ago", FriendRules.LastSeenText(Now - 300, Now));
            Assert.AreEqual("12 min ago", FriendRules.LastSeenText(Now - 12 * 60 - 30, Now));
            Assert.AreEqual("59 min ago", FriendRules.LastSeenText(Now - 3599, Now));
            Assert.AreEqual("1 h ago", FriendRules.LastSeenText(Now - 3600, Now));
            Assert.AreEqual("3 h ago", FriendRules.LastSeenText(Now - 3 * 3600 - 1000, Now));
            Assert.AreEqual("23 h ago", FriendRules.LastSeenText(Now - 86399, Now));
            Assert.AreEqual("1 day ago", FriendRules.LastSeenText(Now - 86400, Now));
            Assert.AreEqual("2 days ago", FriendRules.LastSeenText(Now - 2 * 86400 - 5, Now));
            Assert.AreEqual("400 days ago", FriendRules.LastSeenText(Now - 400 * 86400.0, Now));
            Assert.AreEqual("long ago", FriendRules.LastSeenText(0, 1e15));
        }

        [Test]
        public void AFriendsGiftComesBackEveryTwentyHours()
        {
            Assert.IsTrue(FriendRules.GiftReady(0, Now), "never collected");
            Assert.IsFalse(FriendRules.GiftReady(Now, Now));
            Assert.IsFalse(FriendRules.GiftReady(Now - 20 * 3600 + 1, Now));
            Assert.IsTrue(FriendRules.GiftReady(Now - 20 * 3600, Now));
            Assert.IsTrue(FriendRules.GiftReady(Now - 3 * 86400, Now));
            Assert.IsFalse(FriendRules.GiftReady(Now + 3600, Now), "a clock set back doesn't pay out twice");

            Assert.AreEqual(0, FriendRules.GiftWait(0, Now));
            Assert.AreEqual(20 * 3600, FriendRules.GiftWait(Now, Now), 0.001);
            Assert.AreEqual(3600, FriendRules.GiftWait(Now - 19 * 3600, Now), 0.001);
            Assert.AreEqual(0, FriendRules.GiftWait(Now - 21 * 3600, Now));
            Assert.AreEqual(20 * 3600, FriendRules.GiftWait(Now + 86400, Now), 0.001, "never shows more than the full wait");
        }

        [Test]
        public void AFriendIsAPlainRecord()
        {
            var f = new FriendInfo { Code = "PWW7RT", Name = "Second Field", Map = "coast", Stars = 5, Flights = 900, LastSeen = Now - 7200 };
            Assert.AreEqual(f.Code, FriendRules.ValidCode(f.Code.ToLowerInvariant()));
            Assert.AreEqual(f.Name, FriendRules.CleanName(f.Name));
            Assert.AreEqual("2 h ago", FriendRules.LastSeenText(f.LastSeen, Now));
        }
    }
}
