using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using GoogleMobileAds.Api;
#endif

namespace Crosswind.Game
{
    /// <summary>
    /// Rewarded adverts (Google AdMob): only ever shown because the player tapped a WATCH button, and paid out through
    /// <c>Sim.Ads</c>. Requests are non-personalised ("npa"), so nothing is tracked and iOS never asks
    /// for tracking permission. In the Editor an advert is "watched" at once so the rewards can be tried.
    /// </summary>
    public static class Ads
    {
        // The rewarded unit from the AdMob account, and Google's public test unit, which only ever serves test
        // adverts: debug and TestFlight builds use that one, so nobody testing taps a real advert (Google closes
        // accounts for that).
        const string LiveUnit = "ca-app-pub-2509790196257770/1461715811", TestUnit = "ca-app-pub-3940256099942544/1712485313";
        static string RewardedUnit { get { return Telemetry.TestBuild ? TestUnit : LiveUnit; } }

        static bool _started, _loading;
        static float _retryAt;
#if UNITY_IOS && !UNITY_EDITOR
        static RewardedAd _ad;
#endif

        /// <summary>An advert is loaded and can be shown this moment.</summary>
        public static bool Ready
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_IOS
                return _ad != null && _ad.CanShowAd();
#else
                return false;
#endif
            }
        }

        /// <summary>Starts the SDK and loads the first advert. Called once the game is running, never at launch.</summary>
        public static void Start()
        {
            if (_started) return;
            _started = true;
#if UNITY_IOS && !UNITY_EDITOR
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.G });      // family-safe adverts only
            MobileAds.Initialize(_ => Load());
#endif
        }

        /// <summary>Keeps an advert waiting: tries again a while after a failed load.</summary>
        public static void Tick()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_started && _ad == null && !_loading && _retryAt > 0 && Time.unscaledTime >= _retryAt) Load();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static void Load()
        {
            if (_loading) return;
            _loading = true; _retryAt = 0;
            var request = new AdRequest();
            request.Extras.Add("npa", "1");       // non-personalised: no tracking
            RewardedAd.Load(RewardedUnit, request, (ad, error) =>
            {
                _loading = false;
                if (error != null || ad == null) { _retryAt = Time.unscaledTime + 60; return; }
                _ad = ad;
            });
        }
#endif

        /// <summary>Shows an advert; <paramref name="done"/> hears true only if it was watched to the reward.</summary>
        public static void Show(Action<bool> done, string reward = "")
        {
            // A finished advert is counted (anonymously, with what it was for); one that wasn't finished, as offered but not finished.
            var inner = done;
            done = watched => { Telemetry.Track(watched ? "Ad.completed" : "Ad.unfinished", ("Ad.reward", reward)); inner(watched); };
#if UNITY_EDITOR
            done(true);
#elif UNITY_IOS
            if (!Ready) { done(false); return; }
            var ad = _ad; _ad = null;
            bool earned = false, told = false;
            Action finish = () => { if (told) return; told = true; ad.Destroy(); AudioListener.volume = Sfx.Muted ? 0 : 1; Load(); done(earned); };
            ad.OnAdFullScreenContentClosed += finish;
            ad.OnAdFullScreenContentFailed += _ => finish();
            AudioListener.volume = 0;
            ad.Show(_ => earned = true);
#else
            done(false);
#endif
        }
    }
}
