using System.IO;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Ambitions on screen: the pinned one under the goal card (what it needs, how much is ready, its reward; tap for
    /// EMPIRE), the AMBITIONS list in EMPIRE (each with its steps ticked off, PIN and CLAIM), and the moment one is
    /// reached: the camera goes to it (the first jumbo is followed down onto the runway, the hall is opened up), the
    /// banner and the fanfare. A new star also gets a before-and-after: the airfield from the same spot when the last
    /// star came and now (pictures kept in persistentDataPath/Stars).
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _amb;
        Text _ambText, _ambCount;
        Image _ambFill;
        int _follow = -1; float _followT;

        void BuildAmbition()
        {
            _amb = Ui.ArtImg("Ambition", _safe, "panel", Color.white).rectTransform;
            _amb.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(152, -292), new Vector2(660, 84));
            var b = Ui.Button("Open", _amb, null, new Color(0, 0, 0, 0), () => { OpenTab(4); Telemetry.Feature("ambition_card"); }, 20);
            ((RectTransform)b.transform).Fill();
            var accent = Ui.Img("Accent", _amb, Ui.Gold, true).rectTransform; accent.anchorMin = new Vector2(0, 0); accent.anchorMax = new Vector2(0, 1); accent.offsetMin = new Vector2(8, 12); accent.offsetMax = new Vector2(14, -12);
            _ambText = Ui.Label("Text", _amb, "", 24, Color.white, TextAnchor.MiddleLeft, false);
            _ambText.rectTransform.Fill(); _ambText.rectTransform.offsetMin = new Vector2(26, 18); _ambText.rectTransform.offsetMax = new Vector2(-170, -6);
            _ambText.resizeTextForBestFit = true; _ambText.resizeTextMinSize = 16; _ambText.resizeTextMaxSize = 24; _ambText.verticalOverflow = VerticalWrapMode.Truncate;
            var bar = Ui.Img("Bar", _amb, new Color(0, 0, 0, 0.45f), true).rectTransform;
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0, 0); bar.offsetMin = new Vector2(26, 8); bar.offsetMax = new Vector2(-170, 18);
            _ambFill = Ui.Img("Fill", bar, Ui.Gold, true);
            _ambFill.rectTransform.anchorMin = Vector2.zero; _ambFill.rectTransform.anchorMax = new Vector2(0, 1); _ambFill.rectTransform.offsetMin = _ambFill.rectTransform.offsetMax = Vector2.zero;
            _ambCount = Ui.Label("Count", _amb, "", 24, Ui.Gold, TextAnchor.MiddleCenter);
            _ambCount.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(150, 70));
            _amb.gameObject.SetActive(false);
            _sim.AmbitionReached += OnAmbition;
        }

        void UpdateAmbition()
        {
            if (_amb == null) BuildAmbition();
            // The pinned ambition, or the next one once the guide has nothing more to say.
            var a = _sim.Pinned ?? (_sim.CurrentGoal == null ? _sim.NextAmbition : null);
            bool show = a != null && _tab < 0 && _sim.State.tutorialDone && !CameraRig.Inside;
            if (_amb.gameObject.activeSelf != show) _amb.gameObject.SetActive(show);
            if (show)
            {
                // Under the goal card, whatever its height (or in its place when there is none).
                float top = _goal.gameObject.activeSelf ? 152 + _goal.sizeDelta.y + 8 : 152;
                _amb.anchoredPosition = new Vector2(152, -top);
                var steps = _sim.AmbitionSteps(a);
                int ready = steps.Count(x => x.Ready);
                bool done = _sim.AmbitionDone(a);
                var next = steps.FirstOrDefault(x => !x.Ready);
                _ambText.text = "<color=#FFC83D>AMBITION</color>  " + a.Name + "\n<color=#FFFFFFB0>" + (done ? "Reached: collect your reward in EMPIRE" : next != null ? "Next: " + next.Text : "Everything is ready: it's on its way") + "</color>";
                _ambFill.rectTransform.anchorMax = new Vector2(done ? 1 : steps.Count == 0 ? 0 : ready / (float)steps.Count, 1);
                _ambCount.text = done ? "CLAIM" : ready + " / " + steps.Count + "\n<size=20>+" + a.Gems + " gems</size>";
            }
            // Following the first of its kind down onto the runway.
            if (_follow >= 0)
            {
                if ((_followT -= Time.unscaledDeltaTime) <= 0 || AirportView.Instance == null || CameraRig.Inside) _follow = -1;
                else
                {
                    var plane = AirportView.Instance.Planes.FirstOrDefault(p => p.Flight.id == _follow);
                    if (plane == null) _follow = -1;
                    else CameraRig.Instance.Focus = Vector3.Lerp(CameraRig.Instance.Focus, new Vector3(plane.Head.x, 0, plane.Head.z), 1 - Mathf.Exp(-3 * Time.unscaledDeltaTime));
                }
            }
        }

        /// <summary>An ambition reached just now: the camera to it, the banner, the fanfare.</summary>
        void OnAmbition(AmbitionDef a)
        {
            _dirty = true;
            Telemetry.Track("Ambition.reached", ("Ambition.id", a.Id));
            if (GameRoot.Speed > 3 || PhotoMode || _away != null) return;
            if (a.Class.HasValue && !CameraRig.Inside && AirportView.Instance != null)
            {
                // The one landing now: follow it in, a little closer than usual.
                var f = _sim.State.flights.FirstOrDefault(x => x.phase == Phase.Landing && Content.Routes[x.route].Class == a.Class.Value);
                if (f != null) { _follow = f.id; _followT = 7f; CameraRig.Instance.Distance = Mathf.Min(CameraRig.Instance.Distance, 170); }
            }
            else if (a.Id == "full_apron" && !CameraRig.Inside) CameraRig.Instance.Showcase(Layout.Stand(Sim.MaxStands / 2), 200);
            else if (a.Id == "grand_hall") { GoInside(true); CameraRig.Instance.Distance = CameraRig.Instance.FitInside(); }
            foreach (var st in _bannerStars) Destroy(st.gameObject);
            _bannerStars.Clear();
            _bannerKicker.text = "AMBITION REACHED";
            _bannerTitle.text = a.Name.ToUpper() + "!";
            _bannerSub.text = a.Pitch + "  ·  +" + a.Gems + " gems in EMPIRE";
            _banner.gameObject.SetActive(true);
            _banner.SetAsLastSibling();
            Sfx.Play("star"); Sfx.Play("claim", 0.6f);
            _bannerT = 5f; _bannerAge = 0;
        }

        /// <summary>EMPIRE's AMBITIONS: each with its steps (ready in green, missing in red), PIN, CLAIM.</summary>
        void AmbitionSection()
        {
            var s = _sim;
            Header("AMBITIONS AT " + s.Map.Name.ToUpper() + (s.AmbitionsWaiting > 0 ? "  ·  " + s.AmbitionsWaiting + " TO COLLECT" : "  ·  PIN ONE TO KEEP IT IN SIGHT"));
            foreach (var a in Sim.Ambitions)
            {
                var amb = a;
                bool done = s.AmbitionDone(a), claimed = s.AmbitionClaimed(a), pinned = s.State.pinnedAmbition == a.Id;
                var steps = s.AmbitionSteps(a);
                int ready = steps.Count(x => x.Ready);
                string body = a.Pitch + (done ? "" : "\n" + string.Join("   ", steps.Select(x => (x.Ready ? "<color=#7BD88F>" : "<color=#FF8070>") + x.Text + "</color>")));
                Ico(a.Icon, claimed ? TintPlain : done ? TintGold : pinned ? TintLab : TintAir).Bar(done ? 1 : steps.Count == 0 ? 0 : ready / (float)steps.Count, done ? Ui.Gold : Ui.Green);
                if (claimed) Entry(a.Name, "DONE", a.Pitch, null, null, null);
                else if (done) Entry(a.Name, "REACHED", body, "CLAIM\n" + a.Gems + " gems", null, () => { int g = s.ClaimAmbition(amb.Id); if (g > 0) { Reward(0, g); Sfx.Play("claim"); } }, Ui.Gold);
                else Entry(a.Name, ready + " OF " + steps.Count + " READY  ·  +" + a.Gems + " GEMS" + (pinned ? "  ·  PINNED" : ""), body, pinned ? "UNPIN" : "PIN", null, () => { s.Pin(amb.Id); Telemetry.Feature("ambition_pin", amb.Id); }, pinned ? Ui.Card : Ui.Blue);
            }
        }

        // ------------------------------------------------------------------ a new star: before and after

        static string StarShot(string map, int stars) { return Path.Combine(Application.persistentDataPath, "Stars", map + "_" + stars + ".png"); }

        /// <summary>The airfield from the same spot each time, without the interface: a camera of our own into a texture.</summary>
        Texture2D ShootAirfield(int w, int h)
        {
            var main = CameraRig.Instance.Cam;
            var go = new GameObject("Star camera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            var rot = Quaternion.Euler(40, 128, 0);
            cam.transform.rotation = rot;
            cam.transform.position = new Vector3(6, 0, -40) - rot * Vector3.forward * 300;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(go);
            return tex;
        }

        void SaveStarShot(int stars)
        {
            if (CameraRig.Instance == null || AirportView.Instance == null) return;
            try
            {
                var tex = ShootAirfield(640, 360);
                string file = StarShot(_sim.State.map, stars);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Destroy(tex);
            }
            catch (System.Exception e) { Debug.LogWarning("Star picture not kept: " + e.Message); }
        }

        float _baselineIn = 4f, _beforeIn = -1; int _beforeStars;

        /// <summary>Keeps a picture for the airport's current rating if it has none (so the next star has a "before"), and
        /// brings up the before-and-after a moment after a star banner.</summary>
        void UpdateStarShots()
        {
            if (_baselineIn > 0 && (_baselineIn -= Time.unscaledDeltaTime) <= 0 && !File.Exists(StarShot(_sim.State.map, _sim.Stars))) SaveStarShot(_sim.Stars);
            if (_beforeIn > 0 && (_beforeIn -= Time.unscaledDeltaTime) <= 0) ShowBeforeAfter(_beforeStars);
        }

        /// <summary>Called with a new star: the "after" now, the card once the banner has had its moment.</summary>
        void StarShotsOnStar(int stars)
        {
            if (GameRoot.Speed > 3) return;
            SaveStarShot(stars);
            if (File.Exists(StarShot(_sim.State.map, stars - 1))) { _beforeStars = stars; _beforeIn = 4.5f; }
        }

        RectTransform _beforeAfter;

        void ShowBeforeAfter(int stars)
        {
            if (_away != null || _offer != null || PhotoMode || _tab >= 0) return;
            Texture2D before = Load(StarShot(_sim.State.map, stars - 1)), after = Load(StarShot(_sim.State.map, stars));
            if (before == null || after == null) return;
            if (_beforeAfter != null) Destroy(_beforeAfter.gameObject);
            _beforeAfter = Ui.Img("Before and after", _root, new Color(0, 0, 0, 0.7f)).rectTransform;
            _beforeAfter.Fill(); _beforeAfter.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _beforeAfter, "panel", Color.white).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1420, 640));
            var back = Ui.Img("Back", box, new Color(0.07f, 0.1f, 0.15f, 1f), true).rectTransform; back.Fill(10);
            Ui.Label("T", box, "HOW FAR YOU'VE COME", 50, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1300, 64));
            for (int i = 0; i < 2; i++)
            {
                var pic = new GameObject(i == 0 ? "Before" : "After", typeof(RectTransform)).AddComponent<RawImage>();
                pic.transform.SetParent(box, false);
                pic.texture = i == 0 ? before : after;
                pic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -340 : 340, 10), new Vector2(640, 360));
                var tag = Ui.Img("Tag", pic.transform, i == 0 ? Ui.Off : Ui.Gold, true).rectTransform;
                tag.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(200, 46));
                Ui.Label("L", tag, (i == 0 ? stars - 1 : stars) + " STARS", 28, i == 0 ? Color.white : Ui.Ink).rectTransform.Fill();
            }
            var ok = Ui.Button("Ok", box, "ONWARDS", Ui.Green, () => { Destroy(_beforeAfter.gameObject); _beforeAfter = null; }, 40);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 96));
            _beforeAfter.SetAsLastSibling();
            Telemetry.Feature("star_before_after", stars.ToString());
        }

        static Texture2D Load(string file)
        {
            if (!File.Exists(file)) return null;
            var tex = new Texture2D(2, 2);
            return ImageConversion.LoadImage(tex, File.ReadAllBytes(file)) ? tex : null;
        }
    }
}
