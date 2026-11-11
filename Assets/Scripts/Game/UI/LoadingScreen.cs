using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// What covers the launch while the airport is built behind it: the game's key picture, its name, a progress bar
    /// with an aircraft riding it, a tip, and the Rushing Technologies logo (every game's loading screen carries it).
    /// GameRoot moves <see cref="Progress"/> as it builds; the screen fades once it is told it's done and has been
    /// up long enough to be read.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }
        public float Progress;
        public bool Done;

        static readonly string[] Tips =
        {
            "Hire a manager and the department runs itself, even while you're away.",
            "On-time flights please an airline. Happy airlines pay up to 30% more.",
            "A crowded hour chokes the runway: spread your routes in the day planner.",
            "Every new star brings a team crate. Put your best people on duty.",
            "Queues at check-in and security cost you in the shops. Upgrade the slowest one.",
            "Storm coming? Landing aids keep the runway moving.",
            "Sell a finished airport for Wings: every one adds to all income, for good.",
        };

        CanvasGroup _group;
        RectTransform _fill, _plane, _rays;
        Text _percent;
        float _shown, _age, _fade = 1;

        public static LoadingScreen Show()
        {
            var go = new GameObject("Loading");
            var ls = go.AddComponent<LoadingScreen>();
            Instance = ls;
            ls.Build();
            return ls;
        }

        void Build()
        {
            var root = Ui.Canvas("Loading canvas", transform);
            root.GetComponent<Canvas>().sortingOrder = 100;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            var back = Ui.Img("Back", root, new Color(0.09f, 0.16f, 0.27f));
            back.rectTransform.Fill(); back.raycastTarget = true;
            var hero = Resources.Load<Sprite>("UI/Loading/hero");
            if (hero != null)
            {
                var art = Ui.Img("Hero", root, Color.white); art.sprite = hero;
                art.rectTransform.Fill();
                var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = hero.rect.width / hero.rect.height;
            }
            // Dark at the top (the name) and the bottom (the bar and the logo), the picture clear between.
            foreach (bool top in new[] { true, false })
                for (int i = 0; i < 6; i++)
                {
                    var band = Ui.Img("Shade", root, new Color(0.04f, 0.07f, 0.12f, 0.2f)).rectTransform;
                    band.Band(top, 0, (top ? 560 : 640) * (6 - i) / 6f);
                }

            _rays = Ui.ArtImg("Rays", root, "rays", new Color(1f, 0.85f, 0.4f, 0.13f), 0).rectTransform;
            _rays.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(1300, 1300));
            var title = Ui.Label("Title", root, "CROSSWIND", 190, Color.white);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(1040, 200));
            var o = title.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.05f, 0.1f, 0.2f, 0.9f); o.effectDistance = new Vector2(5, -5);
            var sh = title.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(0, -12);
            var ribbon = Ui.ArtImg("Ribbon", root, "button", Ui.Gold);
            ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(560, 96));
            var sub = Ui.Label("Sub", ribbon.transform, "AIRPORT TYCOON", 50, Ui.Ink);
            sub.rectTransform.Fill(); sub.rectTransform.offsetMin = new Vector2(0, 8);

            // The bar: a dark plate, a gold fill, a little aircraft on its leading edge.
            var bar = Ui.ArtImg("Bar", root, "plate", Color.white).rectTransform;
            bar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 372), new Vector2(860, 76));
            var track = Ui.Rect("Track", bar); track.Fill(); track.offsetMin = new Vector2(18, 24); track.offsetMax = new Vector2(-18, -18);
            _fill = Ui.Img("Fill", track, Ui.Gold, true).rectTransform;
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = new Vector2(0.02f, 1); _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            var planeImg = Ui.Img("Plane", track, Color.white); planeImg.sprite = Ui.Icon("plane"); planeImg.preserveAspect = true;
            _plane = planeImg.rectTransform;
            _plane.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
            _plane.localRotation = Quaternion.Euler(0, 0, -90);
            var ps = planeImg.gameObject.AddComponent<Shadow>(); ps.effectColor = new Color(0, 0, 0, 0.6f); ps.effectDistance = new Vector2(3, -3);
            _percent = Ui.Label("Percent", root, "", 40, Color.white);
            _percent.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 458), new Vector2(800, 50));
            var po = _percent.gameObject.AddComponent<Outline>(); po.effectColor = new Color(0, 0, 0, 0.7f); po.effectDistance = new Vector2(2, -2);

            var tip = Ui.Label("Tip", root, Tips[Random.Range(0, Tips.Length)], 36, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleCenter, false);
            tip.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 256), new Vector2(900, 100));
            var to = tip.gameObject.AddComponent<Outline>(); to.effectColor = new Color(0, 0, 0, 0.6f); to.effectDistance = new Vector2(2, -2);

            var logo = Resources.Load<Sprite>("UI/Brand/rushingtech");
            if (logo != null)
            {
                var li = Ui.Img("Logo", root, Color.white); li.sprite = logo; li.preserveAspect = true;
                li.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(260, 200));
            }
            else Ui.Label("Studio", root, "RUSHING TECHNOLOGIES", 34, Ui.Dim).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(800, 50));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _age += dt;
            // The bar never jumps and never sits still: it eases toward the real progress, creeping a little meanwhile.
            float goal = Done ? 1 : Mathf.Min(0.94f, Mathf.Max(Progress, _shown + dt * 0.03f));
            _shown = Mathf.MoveTowards(_shown, goal, dt * (Done ? 1.6f : 0.7f));
            _fill.anchorMax = new Vector2(Mathf.Max(0.02f, _shown), 1);
            _plane.anchorMin = _plane.anchorMax = new Vector2(Mathf.Max(0.02f, _shown), 0.5f);
            _plane.anchoredPosition = new Vector2(0, Mathf.Sin(_age * 6) * 2);
            _percent.text = _shown >= 0.999f ? "CLEARED FOR TAKE-OFF" : "Preparing the airport  " + Mathf.RoundToInt(_shown * 100) + "%";
            _rays.localRotation = Quaternion.Euler(0, 0, _age * 10);
            if (Done && _shown >= 0.999f && _age > 2.2f)
            {
                _fade -= dt * 2.2f;
                _group.alpha = Mathf.Clamp01(_fade);
                if (_fade <= 0) { if (Instance == this) Instance = null; Destroy(gameObject); }
            }
        }
    }
}
