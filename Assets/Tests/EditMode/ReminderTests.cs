using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    public class ReminderTests
    {
        static Sim Fresh() { var s = new Sim(GameState.New()); s.RealNow = () => 1_800_000_000; return s; }

        [Test]
        public void AQuietAirportOnlyRemindsAboutTomorrow()
        {
            var s = Fresh();
            s.TakeFreeCrate(); s.State.freeCrateAt = 0;
            var plan = s.ReminderPlan();
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(24 * 3600, plan[0].In, 1);
        }

        [Test]
        public void BuildsResearchCratesAndFullTillsAreWorthATap()
        {
            var s = Fresh();
            s.State.money = 1e9; s.State.ratingXp = Content.StarXp[4];
            s.Hire("fuel");
            s.TakeFreeCrate();
            foreach (var f in Content.FacilityOrder) { if (s.BuildSeconds(f) > 60 && s.Upgrade(f)) break; for (int i = 0; i < 12 && s.State.builds.Count == 0; i++) s.Upgrade(f); if (s.State.builds.Count > 0) break; }
            Assert.Greater(s.State.builds.Count, 0, "something is being built");
            var plan = s.ReminderPlan();
            Assert.LessOrEqual(plan.Count, 4);
            Assert.IsTrue(plan.Any(r => r.Title.Contains("crate")));
            Assert.IsTrue(plan.Any(r => r.Title.Contains("waiting for you")), "the tills fill up");
            for (int i = 1; i < plan.Count; i++) Assert.GreaterOrEqual(plan[i].In - plan[i - 1].In, 600, "never two within ten minutes");
            foreach (var r in plan) { Assert.Greater(r.In, 0); Assert.IsNotEmpty(r.Body); }
        }

        [Test]
        public void ANewPhoneAdoptsAndANewerSaveElsewhereIsOffered()
        {
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(true, null, "me", 0));
            var theirs = new CloudMeta { SavedAt = 500, Stars = 4, Device = "other" };
            Assert.AreEqual(CloudStep.Adopt, CloudRules.Decide(false, theirs, "me", 0));
            Assert.AreEqual(CloudStep.Offer, CloudRules.Decide(true, theirs, "me", 100));
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(true, theirs, "me", 500), "already seen");
            Assert.AreEqual(CloudStep.Upload, CloudRules.Decide(true, new CloudMeta { SavedAt = 900, Device = "me" }, "me", 100));
            var back = CloudMeta.Parse(theirs.ToString());
            Assert.AreEqual(4, back.Stars); Assert.AreEqual("other", back.Device); Assert.AreEqual(500, back.SavedAt);
        }
    }
}
