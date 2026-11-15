using System;

namespace Crosswind.Core
{
    /// <summary>What a watched advert can pay for. Always the player's choice, never shown unasked.</summary>
    public enum AdReward { Double, Boost }

    /// <summary>
    /// Rewarded adverts: the only way to double (the time away paid twice over, half an hour of double income) short
    /// of the Golden Runway purchase, a few times a day. The rules are here; showing the advert is <c>Game/Platform/Ads</c>.
    /// </summary>
    public sealed partial class Sim
    {
        public const int AdsPerDay = 6;

        public int AdsLeft { get { return State.adDay == Today ? Math.Max(0, AdsPerDay - State.adCount) : AdsPerDay; } }

        /// <summary>Whether this reward can be offered right now (something to gain, and adverts left today).</summary>
        public bool AdOffered(AdReward reward)
        {
            if (AdsLeft <= 0) return false;
            switch (reward)
            {
                case AdReward.Double: return true;
                case AdReward.Boost: return BoostLeft + Content.BoostMinutes * 60 <= 4 * 3600 + 1;      // the boost holds four hours at most
            }
            return false;
        }

        void CountAd()
        {
            if (State.adDay != Today) { State.adDay = Today; State.adCount = 0; }
            State.adCount++;
            Changed?.Invoke();
        }

        /// <summary>Pays the time away twice over for a watched advert. Returns what was paid (0 if not on offer).</summary>
        public double CollectOfflineByAd(double secondsAway)
        {
            if (!AdOffered(AdReward.Double)) return 0;
            double got = CollectOffline(secondsAway, 2);
            if (got > 0) CountAd();
            return got;
        }

        /// <summary>Half an hour of double income for a watched advert. False if it wasn't on offer.</summary>
        public bool BoostByAd()
        {
            if (!AdOffered(AdReward.Boost)) return false;
            AddBoost();
            CountAd();
            return true;
        }
    }
}
