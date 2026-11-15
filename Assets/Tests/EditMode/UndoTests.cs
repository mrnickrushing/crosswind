using System.Linq;
using Crosswind.Core;
using NUnit.Framework;

namespace Crosswind.Tests
{
    /// <summary>UNDO on the terminal floor: exact, newest first, for a while, and never a way to farm anything.</summary>
    public class UndoTests
    {
        double _clock;

        Sim Airport()
        {
            _clock = 1_800_000_000;
            var s = new Sim(GameState.New()) { BuildTimeScale = 0 };
            s.RealNow = () => _clock;
            s.State.money = 1e6; s.State.ratingXp = Content.StarXp[3] + 5;
            return s;
        }

        [Test]
        public void APlacementComesBackExactly()
        {
            var s = Airport();
            double money = s.State.money, xp = s.State.ratingXp;
            Assert.IsTrue(s.PlaceAnywhere("cafe"));
            Assert.AreEqual("Coffee Bar placed", s.UndoLabel);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(money, s.State.money, 1e-6, "every penny back");
            Assert.AreEqual(xp, s.State.ratingXp, 1e-6, "and the rating it earned goes");
            Assert.IsEmpty(s.State.terminal);
            Assert.IsNull(s.LastEdit);
            Assert.IsFalse(s.Undo(), "nothing more to undo");
        }

        [Test]
        public void ChangesComeBackNewestFirst()
        {
            var s = Airport();
            Assert.IsTrue(s.PlaceAnywhere("seats"));
            var p = s.State.terminal[0]; int x = p.x, z = p.z;
            Assert.IsTrue(s.UpgradeAmenity(p));
            Assert.IsTrue(s.FloorFree(Sim.AmenityById("seats"), x, z + 3, 0, p) && s.MoveAmenity(p, x, z + 3, 0) || s.MoveAmenity(p, x, z - 3, 0));
            Assert.IsTrue(s.Undo()); Assert.AreEqual(x, p.x); Assert.AreEqual(z, p.z);
            Assert.IsTrue(s.Undo()); Assert.AreEqual(1, p.level);
            Assert.IsTrue(s.Undo()); Assert.IsEmpty(s.State.terminal);
        }

        [Test]
        public void ASaleIsBoughtBackForWhatItPaid()
        {
            var s = Airport();
            Assert.IsTrue(s.PlaceAnywhere("gifts"));
            var p = s.State.terminal[0];
            double before = s.State.money;
            Assert.IsTrue(s.SellAmenity(p));
            Assert.Greater(s.State.money, before);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(before, s.State.money, 1e-6);
            Assert.Contains(p, s.State.terminal);
        }

        [Test]
        public void PlacingAndUndoingFarmsNothing()
        {
            var s = Airport();
            s.State.tasks.Clear();
            s.State.tasks.Add(new TaskState { kind = "upgrade", target = 10, gems = 5 });
            double xp = s.State.ratingXp; int stars = s.Stars;
            for (int i = 0; i < 20; i++) { Assert.IsTrue(s.PlaceAnywhere("vending")); Assert.IsTrue(s.Undo()); }
            Assert.AreEqual(0, s.State.tasks[0].progress, "the task counts nothing");
            Assert.AreEqual(xp, s.State.ratingXp, 1e-6);
            Assert.AreEqual(stars, s.Stars);
            if (s.CurrentEvent.Counts == "upgrade") Assert.AreEqual(0, s.State.eventProgress, 1e-9);
        }

        [Test]
        public void OnlyRecentChangesAndOnlyWhenTheyStillFit()
        {
            var s = Airport();
            Assert.IsTrue(s.PlaceAnywhere("toilets"));
            _clock += Sim.UndoSeconds + 1;
            Assert.IsNull(s.LastEdit, "ten minutes on, it's just how the floor is");
            Assert.IsFalse(s.Undo());

            // A sale can't come back onto a spot something else now stands on.
            var p = s.State.terminal[0]; int x = p.x, z = p.z;
            Assert.IsTrue(s.SellAmenity(p));
            Assert.IsTrue(s.Place("toilets", x, z, p.rot));
            Assert.IsTrue(s.Undo(), "the newest change (the new toilets) goes first");
            Assert.IsNull(s.UndoBlocked, "now the spot is free again for the old ones");
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(1, s.State.terminal.Count);
        }
    }
}
