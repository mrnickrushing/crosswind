using System;

namespace Crosswind.Core
{
    /// <summary>The manager's name, and the daily gift each friend's airport sends (collected here, counted on this device).</summary>
    public sealed partial class Sim
    {
        public const int FriendGiftGems = 3;

        public string DisplayName { get { return string.IsNullOrEmpty(State.playerName) ? FriendRules.DefaultName : State.playerName; } }

        public void Rename(string name)
        {
            State.playerName = FriendRules.CleanName(name);
            if (State.playerName == FriendRules.DefaultName) State.playerName = "";
            Changed?.Invoke();
        }

        double LastGift(string code) { var g = State.gifts.Find(x => x.id == code); return g != null ? g.doneAt : 0; }
        public bool GiftReady(string code) { return FriendRules.GiftReady(LastGift(code), RealNow()); }
        public double GiftWait(string code) { return FriendRules.GiftWait(LastGift(code), RealNow()); }

        /// <summary>Collects a friend's gift: a few gems, once every twenty hours from each friend.</summary>
        public bool CollectGift(string code)
        {
            code = FriendRules.ValidCode(code);
            if (code == null || !GiftReady(code)) return false;
            var g = State.gifts.Find(x => x.id == code);
            if (g == null) State.gifts.Add(new ResearchJob { id = code, doneAt = RealNow() }); else g.doneAt = RealNow();
            State.gems += FriendGiftGems;
            Changed?.Invoke();
            return true;
        }
    }
}
