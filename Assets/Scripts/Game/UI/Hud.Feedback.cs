using System.Collections.Generic;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// What the game says back: a coin chip for every flight paid, a star chip when the rating grows, the toast, the
    /// reward chips of a claimed goal, and the banner for a new star. All built from one "chip" (a dark pill with a
    /// coloured rim and a round medallion) so they read as one family, and all pop in with a spring and float away.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly Color ChipFill = new Color(0.07f, 0.1f, 0.14f, 0.96f), GemBlue = new Color(0.35f, 0.8f, 1f);
        CanvasGroup _toastGroup, _bannerGroup;
        Image _toastRim;
        readonly List<RectTransform> _bannerStars = new List<RectTransform>();
        Text _bannerTitle, _bannerSub;
        RectTransform _bannerRays;
        float _toastAge, _bannerAge, _ratingShown = -1, _ratingOwed, _ratingWait, _starPulse;

        /// <summary>A pill with a coloured rim; returns the inside to put things in.</summary>
        static RectTransform Pill(string name, Transform parent, Color rim, out RectTransform outer, float border = 4, bool shine = true)
        {
            // The painted plate (dark, glossy, with its own shadow) under a rim in the chip's colour.
            var o = Ui.ArtImg(name, parent, "plate", Color.white);
            outer = o.rectTransform;
            var ring = Ui.ArtImg("Rim", outer, "rim", rim);
            ring.rectTransform.Fill();
            var inner = Ui.Rect("In", outer);
            inner.Fill(border); inner.offsetMin = new Vector2(border, border + 5);
            return inner;
        }

        /// <summary>A chip: medallion on the left (a letter, or an icon from Resources/UI), bold text beside it. Sized to its text.</summary>
        RectTransform Chip(Transform parent, string text, Color tint, string medal, int size = 44)
        {
            RectTransform outer;
            var inner = Pill("Chip", parent, tint, out outer);
            float h = size + 44;
            var disc = Ui.ArtImg("Medal", inner, medal, Color.white, 0);
            disc.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(h - 16, h - 16));
            var t = Ui.Label("Text", inner, text, size, Color.white, TextAnchor.MiddleLeft);
            Outline(t);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = Mathf.Min(900, t.preferredWidth);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 1); t.rectTransform.offsetMin = new Vector2(h - 2, 0); t.rectTransform.offsetMax = new Vector2(-18, 0);
            outer.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w + h + 30, h));
            return outer;
        }

        Pop Float(RectTransform rt, float life)
        {
            var p = new Pop { Rt = rt, Group = rt.gameObject.AddComponent<CanvasGroup>(), Life = life };
            p.Group.blocksRaycasts = false; p.Group.interactable = false; p.Group.alpha = 0;
            _pops.Add(p);
            return p;
        }

        /// <summary>Scale for something popping in: up past full size, then settling.</summary>
        static float Spring(float t) { return t < 0.14f ? Mathf.Lerp(0.5f, 1.16f, t / 0.14f) : t < 0.26f ? Mathf.Lerp(1.16f, 1f, (t - 0.14f) / 0.12f) : 1f; }

        // ------------------------------------------------------------------ flights paid, rating gained, rewards

        void OnPaid(FlightState f, double paid, bool onTime)
        {
            if (GameRoot.Speed <= 3) Sfx.Play("coin", 0.35f);
            if (_pops.Count > 14) return;                                   // a rush of departures: the top bar still counts them
            var chip = Chip(_world, "+" + Format.Money(paid) + (onTime ? "" : "  <size=28><color=#FF9080>LATE</color></size>"), onTime ? Ui.Gold : Ui.Red, "coin");
            var p = Float(chip, 1.8f);
            p.World = new Vector3(Layout.RunwayX, 6, Layout.RunwayHalf(_sim) * 0.5f);
            p.Drift = new Vector2(Random.Range(-30f, 30f), 150);
        }

        /// <summary>What a claimed goal paid: its chips rise from the guide card.</summary>
        void Reward(double money, int gems)
        {
            float y = -520;
            if (money >= 1) { var p = Float(Chip(_root, "+" + Format.Money(money), Ui.Gold, "coin", 52), 2.2f); p.Screen = true; p.At = new Vector2(0, _root.rect.height * 0.5f + y); p.Drift = new Vector2(0, 90); y -= 100; }
            if (gems > 0) { var p = Float(Chip(_root, "+" + gems + (gems == 1 ? " gem" : " gems"), GemBlue, "gem", 52), 2.2f); p.Screen = true; p.At = new Vector2(0, _root.rect.height * 0.5f + y); p.Drift = new Vector2(0, 90); p.Age = -0.15f; }
            if (money < 1 && gems <= 0) Toast("Done");
        }

        void UpdatePops()
        {
            // The rating: what it gained over the last moment, as one chip under the star bar, and the bar glows.
            float rating = (float)_sim.State.ratingXp;
            if (_ratingShown < 0 || rating < _ratingShown) _ratingShown = rating;
            if (rating > _ratingShown) { _ratingOwed += rating - _ratingShown; _ratingShown = rating; if (_ratingWait <= 0) _ratingWait = 0.6f; }
            if (_ratingWait > 0 && (_ratingWait -= Time.unscaledDeltaTime) <= 0 && _ratingOwed >= 1)
            {
                if (_tab < 0 && GameRoot.Speed <= 3)
                {
                    var p = Float(Chip(_root, "+" + Format.Money(_ratingOwed) + " rating", Ui.Gold, "star", 34), 1.5f);
                    p.Screen = true; p.At = new Vector2(_root.rect.width * 0.5f - 210, _root.rect.height * 0.5f - 232); p.Drift = new Vector2(0, 46);
                }
                _starPulse = 0.6f; _ratingOwed = 0;
            }
            if (_starPulse > 0) _starPulse -= Time.unscaledDeltaTime;
            if (_starFill != null) _starFill.color = Color.Lerp(Ui.Gold, Color.white, Mathf.Clamp01(_starPulse / 0.6f) * 0.7f);

            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.Age += Time.unscaledDeltaTime;
                if (p.Age < 0) continue;
                float t = p.Age / p.Life;
                if (t >= 1) { Destroy(p.Rt.gameObject); _pops.RemoveAt(i); continue; }
                Vector2 at = p.At;
                if (!p.Screen) { var c = ToCanvas(p.World); at = new Vector2(c.x, c.y); }
                float rise = 1 - (1 - t) * (1 - t);                       // quick at first, then hanging
                p.Rt.anchoredPosition = at + p.Drift * rise;
                p.Rt.localScale = Vector3.one * Spring(t);
                p.Group.alpha = t < 0.08f ? t / 0.08f : t > 0.72f ? (1 - t) / 0.28f : 1;
            }
        }

        // ------------------------------------------------------------------ toast and the star banner

        void BuildFeedback()
        {
            RectTransform outer;
            var inner = Pill("Toast", _root, new Color(1, 1, 1, 0.28f), out outer, 3);
            _toast = outer; _toastRim = outer.GetComponent<Image>();
            _toast.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(900, 92));
            _toastText = Ui.Label("Text", inner, "", 38, Color.white, TextAnchor.MiddleCenter, false);
            _toastText.rectTransform.Fill(14);
            _toastText.resizeTextForBestFit = true; _toastText.resizeTextMinSize = 24; _toastText.resizeTextMaxSize = 38;
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>(); _toastGroup.blocksRaycasts = false;
            _toast.gameObject.SetActive(false);

            // The star banner: a gold-rimmed plate, a gold band across its top, the stars landing one by one.
            var plate = Pill("Banner", _root, Ui.Gold, out outer, 10, false);
            _banner = outer;
            _banner.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -430), new Vector2(940, 330));
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>(); _bannerGroup.blocksRaycasts = false;
            _bannerRays = Ui.ArtImg("Rays", plate, "rays", new Color(1f, 0.82f, 0.3f, 0.35f), 0).rectTransform;
            _bannerRays.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(620, 620));
            plate.gameObject.AddComponent<RectMask2D>();
            var band = Ui.Img("Band", plate, Ui.Gold, true).rectTransform;
            band.Band(true, 6, 58, 6);
            Ui.Label("Kicker", band, "NEW RATING", 34, Ui.Ink).rectTransform.Fill();
            _bannerTitle = Ui.Label("Title", plate, "", 70, Color.white);
            _bannerTitle.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 78), new Vector2(880, 84));
            Outline(_bannerTitle);
            _bannerSub = Ui.Label("Sub", plate, "", 32, Ui.Dim, TextAnchor.MiddleCenter, false);
            _bannerSub.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(880, 46));
            _banner.gameObject.SetActive(false);
        }

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toastText.text = text;
            float w = Mathf.Clamp(_toastText.preferredWidth + 90, 320, 960);
            _toast.sizeDelta = new Vector2(w, 92);
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            _toastT = 2.4f; _toastAge = 0;
        }

        void OnStar(int stars)
        {
            foreach (var st in _bannerStars) Destroy(st.gameObject);
            _bannerStars.Clear();
            var plate = _banner.GetChild(0);
            float size = stars > 7 ? 64 : 76, gap = size + 10, x0 = -(stars - 1) * gap * 0.5f;
            for (int i = 0; i < stars; i++)
            {
                var img = Ui.ArtImg("Star", plate, "star", Color.white, 0);
                img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x0 + i * gap, -128), new Vector2(size + 14, size + 14));
                img.rectTransform.localScale = Vector3.zero;
                _bannerStars.Add(img.rectTransform);
            }
            _bannerTitle.text = stars + "-STAR AIRPORT!";
            _bannerSub.text = "New airlines and buildings unlocked  ·  +1 team crate";
            _banner.gameObject.SetActive(true);
            _banner.SetAsLastSibling();
            Sfx.Play("star");
            _bannerT = 4f; _bannerAge = 0;
            _dirty = true;
        }

        void UpdateFeedback()
        {
            float dt = Time.unscaledDeltaTime;
            if (_toastT > 0)
            {
                _toastT -= dt; _toastAge += dt;
                if (_toastT <= 0) _toast.gameObject.SetActive(false);
                else
                {
                    _toast.localScale = Vector3.one * Spring(_toastAge / 1.4f);
                    _toast.anchoredPosition = new Vector2(0, 330 - 40 * Mathf.Clamp01(1 - _toastAge / 0.18f));
                    _toastGroup.alpha = Mathf.Clamp01(_toastAge / 0.1f) * Mathf.Clamp01(_toastT / 0.35f);
                }
            }
            if (_bannerT > 0)
            {
                _bannerT -= dt; _bannerAge += dt;
                if (_bannerT <= 0) _banner.gameObject.SetActive(false);
                else
                {
                    _banner.localScale = Vector3.one * Spring(_bannerAge / 1.6f);
                    _bannerRays.localRotation = Quaternion.Euler(0, 0, _bannerAge * 24);
                    _bannerGroup.alpha = Mathf.Clamp01(_bannerAge / 0.12f) * Mathf.Clamp01(_bannerT / 0.4f);
                    for (int i = 0; i < _bannerStars.Count; i++)
                    {
                        float a = (_bannerAge - 0.25f - i * 0.11f) / 0.5f;
                        _bannerStars[i].localScale = Vector3.one * (a <= 0 ? 0 : Spring(Mathf.Min(1, a)) * Mathf.Clamp01(a / 0.14f));
                        _bannerStars[i].localRotation = Quaternion.Euler(0, 0, a <= 0 ? 0 : Mathf.Lerp(-40, 0, Mathf.Clamp01(a / 0.3f)));
                    }
                }
            }
        }
    }
}
