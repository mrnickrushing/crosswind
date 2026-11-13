using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Asking for an App Store rating, once: after a star-up banner (three stars and up, the tutorial done) a small
    /// card asks "Enjoying Crosswind?". RATE IT brings up Apple's own rating sheet; either answer means it never
    /// asks again on this device. Settings keeps a Rate row that opens the App Store page.
    /// </summary>
    public sealed partial class Hud
    {
        const string RateAskedPref = "cw_rate_asked";
        const int RateFromStars = 3;
        public const string ReviewUrl = "https://apps.apple.com/app/id6833564340?action=write-review";

        float _rateIn = -1;
        RectTransform _rateCard;

        static bool RateAsked { get { return PlayerPrefs.GetInt(RateAskedPref, 0) == 1; } }

        static void MarkRateAsked() { PlayerPrefs.SetInt(RateAskedPref, 1); PlayerPrefs.Save(); }

        /// <summary>A good moment just happened (a new star): ask once the screen is clear again.</summary>
        void RateSoon(int stars)
        {
            if (RateAsked || stars < RateFromStars) return;
            _rateIn = 2f;
        }

        void UpdateRate()
        {
            if (_rateIn < 0) return;
            // Wait for the star banner, the tutorial, a sheet, and any card of the game's own (welcome back, a crate...).
            if (_bannerT > 0 || TutStep != Tut.Done || _tab >= 0 || OtherModal() || _crateCard != null || _incCard != null || _placing != null) return;
            if ((_rateIn -= Time.unscaledDeltaTime) > 0) return;
            _rateIn = -1;
            if (RateAsked) return;
            MarkRateAsked();
            ShowRateCard();
        }

        void ShowRateCard()
        {
            _rateCard = Ui.Img("Rate", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _rateCard.Fill(); _rateCard.GetComponent<Image>().raycastTarget = true;
            var box = Ui.Img("Box", _rateCard, Ui.Panel, true).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 520));
            Ui.Label("T", box, "ENJOYING CROSSWIND?", 58, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(840, 80));
            Ui.Label("B", box, "If you like running your airport, a quick rating on the App Store helps other players find it.", 36, Ui.Dim, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(820, 180));
            var yes = Ui.Button("Yes", box, "RATE IT", Ui.Gold, () => { CloseRateCard(); RequestRating(); }, 46);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 50), new Vector2(400, 120));
            var no = Ui.Button("No", box, "NO THANKS", Ui.Off, CloseRateCard, 46);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 50), new Vector2(400, 120));
            Sfx.Play("open");
        }

        void CloseRateCard()
        {
            if (_rateCard != null) Destroy(_rateCard.gameObject);
            _rateCard = null;
        }

        /// <summary>Apple's in-app rating sheet (the App Store page where that isn't available).</summary>
        static void RequestRating()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (UnityEngine.iOS.Device.RequestStoreReview()) return;
#endif
            Application.OpenURL(ReviewUrl);
        }

        /// <summary>The Settings row: straight to the App Store's review page.</summary>
        static void OpenStoreReview()
        {
            MarkRateAsked();
            Application.OpenURL(ReviewUrl);
        }
    }
}
