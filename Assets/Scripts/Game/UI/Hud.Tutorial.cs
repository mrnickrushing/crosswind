using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The first-run tutorial. Priya from the tower talks the new manager through the first flights: the screen dims
    /// round a soft spotlight on the one thing to tap, a hand points at it, and only that thing takes taps. Steps:
    /// welcome card, fuel the first flight, where the money shows, the goal card and CLAIM, handle three flights,
    /// build a second stand, then a send-off card. Progress is saved (<see cref="GameState.tutorialStep"/>); SKIP ends it.
    /// </summary>
    public sealed partial class Hud
    {
        enum Tut { Welcome, FirstFlight, Money, Goal, KeepGoing, Build, Finish, Done }

        const string Mentor = "priya", MentorName = "PRIYA  ·  CONTROL TOWER";
        static readonly Color Dim = new Color(0.02f, 0.04f, 0.08f, 0.68f);

        RectTransform _tutLayer, _coach, _tutHand, _tutDots, _tutCard;
        Image _tutDim; Material _tutMat;
        Text _coachText; Button _coachNext; Text _coachNextLabel;
        TutHole _tutHole;
        Tut _tutShown = Tut.Done; string _tutSay = ""; float _tutType, _tutPop, _tutStepT;
        Vector4 _hole, _holeGoal; float _dimA;
        bool _dispatchedFirst, _tutPaused;

        Tut TutStep { get { return (Tut)Mathf.Clamp(_sim.State.tutorialStep, 0, (int)Tut.Done); } set { _sim.State.tutorialStep = (int)value; if (value == Tut.Done) _sim.State.tutorialDone = true; } }

        void BuildTutorial()
        {
            var st = _sim.State;
            // A game that already got going before this tutorial existed starts where it is.
            if (st.tutorialDone) TutStep = Tut.Done;
            else if (st.goalIndex >= 3) TutStep = Tut.Done;
            else if (st.flightsHandled > 0 && TutStep < Tut.Goal) TutStep = Tut.Goal;

            _tutLayer = Ui.Rect("Tutorial", _root); _tutLayer.Fill();
            _tutDim = Ui.Img("Dim", _tutLayer, Color.white);
            _tutDim.rectTransform.Fill();
            var sh = Resources.Load<Shader>("Shaders/Spotlight");
            _tutMat = sh != null ? new Material(sh) : null;
            if (_tutMat != null) { _tutDim.material = _tutMat; _tutMat.SetColor("_Color", Dim); }
            else _tutDim.color = Dim;
            _tutHole = _tutDim.gameObject.AddComponent<TutHole>();

            // The hand: its fingertip is the pivot, so placing it places the tip.
            var hand = Ui.Img("Hand", _tutLayer, Color.white); hand.sprite = Ui.Art("hand"); hand.raycastTarget = false;
            _tutHand = hand.rectTransform;
            _tutHand.Place(new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.86f), Vector2.zero, new Vector2(150, 150));

            // Priya's speech bubble.
            _coach = Ui.ArtImg("Coach", _tutLayer, "panel", Color.white).rectTransform;
            _coach.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 232));
            var back = Ui.Img("Back", _coach, new Color(0.06f, 0.09f, 0.14f, 0.97f), true).rectTransform; back.Fill(10);
            var accent = Ui.Img("Accent", _coach, Ui.Gold, true).rectTransform;
            accent.anchorMin = new Vector2(0, 1); accent.anchorMax = new Vector2(1, 1); accent.offsetMin = new Vector2(200, -16); accent.offsetMax = new Vector2(-24, -10);
            var face = FaceTile(_coach, Mentor, "P", Palette.Hex(0x3F78C2), new Vector2(0, 0.5f), new Vector2(104, 12), 176);
            face.gameObject.AddComponent<Bob>();
            var who = Ui.Label("Who", _coach, MentorName, 22, Ui.Gold, TextAnchor.UpperLeft);
            who.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(206, -24), new Vector2(560, 30));
            _coachText = Ui.Label("Say", _coach, "", 32, Color.white, TextAnchor.UpperLeft, false);
            _coachText.rectTransform.Fill(); _coachText.rectTransform.offsetMin = new Vector2(206, 64); _coachText.rectTransform.offsetMax = new Vector2(-30, -58);
            _coachText.horizontalOverflow = HorizontalWrapMode.Wrap; _coachText.verticalOverflow = VerticalWrapMode.Overflow; _coachText.lineSpacing = 1.05f;
            // Tapping the bubble finishes the line at once.
            var tapAll = _coach.gameObject.AddComponent<Button>(); tapAll.transition = Selectable.Transition.None;
            tapAll.onClick.AddListener(() => _tutType = 999);
            _tutDots = Ui.Rect("Dots", _coach);
            _tutDots.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(206, 22), new Vector2(220, 26));
            for (int i = 0; i < 5; i++)
            {
                var d = Ui.Img("Dot", _tutDots, Color.white); d.sprite = Ui.Circle;
                d.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 30, 0), new Vector2(16, 16));
            }
            _coachNext = Ui.Button("Next", _coach, "NEXT", Ui.Green, () => TutNext(), 30);
            ((RectTransform)_coachNext.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 14), new Vector2(190, 74));
            _coachNextLabel = _coachNext.GetComponentInChildren<Text>();
            var skip = Ui.Button("Skip", _coach, "SKIP TUTORIAL", new Color(0, 0, 0, 0), () => { Sfx.Play("close", 0.5f); EndTutorial(); }, 20);
            ((RectTransform)skip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(200, 40));
            skip.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.55f);

            _tutLayer.gameObject.SetActive(false);
        }

        /// <summary>The service buttons call this: the tutorial waits for the first dispatch.</summary>
        void TutorialDispatched() { _dispatchedFirst = true; }

        void EndTutorial()
        {
            TutStep = Tut.Done;
            if (_tutPaused) { _tutPaused = false; Time.timeScale = 1; }
            if (_tutCard != null) { Destroy(_tutCard.gameObject); _tutCard = null; }
            _tutLayer.gameObject.SetActive(false);
            GameRoot.Instance.Save();
        }

        void TutNext()
        {
            Sfx.Play("click", 0.5f);
            var s = TutStep;
            if (s == Tut.Money) TutStep = Tut.Goal;
            else if (s == Tut.Goal && !_sim.GoalDone && _sim.State.goalIndex >= 1) TutStep = Tut.KeepGoing;
        }

        /// <summary>A modal of the game's own is up (a crate, a confirm, the welcome-back card...): the tutorial waits.</summary>
        bool OtherModal()
        {
            if (_away != null) return true;
            for (int i = 0; i < _root.childCount; i++)
            {
                var c = _root.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                switch (c.name) { case "Crate": case "Incident card": case "Confirm": case "Cloud": case "Prompt": case "Away": return true; }
            }
            return false;
        }

        void UpdateTutorial()
        {
            var step = TutStep;
            if (step == Tut.Done || OtherModal() || (CameraRig.Instance != null && CameraRig.Inside))
            {
                if (_tutPaused) { _tutPaused = false; Time.timeScale = 1; }
                _tutLayer.gameObject.SetActive(false); return;
            }
            var st = _sim.State;

            // Moving on when the player has done the thing.
            if (step == Tut.FirstFlight && (_dispatchedFirst || st.flightsHandled > 0)) step = TutStep = Tut.Money;
            if (step == Tut.Money && st.flightsHandled > 0 && _tutStepT > 3.5f) step = TutStep = Tut.Goal;
            if (step == Tut.Goal && st.goalIndex >= 1) step = TutStep = Tut.KeepGoing;
            if (step == Tut.KeepGoing && st.goalIndex >= 2) step = TutStep = Tut.Build;
            if (step == Tut.Build && _sim.Level("stands") >= 2) { step = TutStep = Tut.Finish; if (_tab >= 0) OpenTab(-1); }

            if (step != _tutShown) { _tutShown = step; _tutType = 0; _tutPop = 0; _tutStepT = 0; Sfx.Play("open", 0.35f); }
            _tutStepT += Time.unscaledDeltaTime;
            _tutLayer.gameObject.SetActive(true);
            _tutLayer.SetAsLastSibling();

            // The airport holds still while Priya's cards are up, so the first flight doesn't start late.
            bool card = step == Tut.Welcome || step == Tut.Finish;
            if (card != _tutPaused) { _tutPaused = card; Time.timeScale = card ? 0 : 1; }
            if (card) { TutCard(step); return; }
            if (_tutCard != null) { Destroy(_tutCard.gameObject); _tutCard = null; }
            _coach.gameObject.SetActive(true);

            // What to say, what to light up, whether the rest of the screen is blocked, and whether there's a NEXT.
            RectTransform target = null; Vector2? point = null; Vector2 size = Vector2.zero;
            string say; bool block = true, next = false, hand = true; float dim = Dim.a;
            switch (step)
            {
                case Tut.FirstFlight:
                {
                    var waiting = _serviceButtons.FirstOrDefault(b => b.Rt.gameObject.activeSelf);
                    if (waiting != null)
                    {
                        target = waiting.Rt;
                        say = "It's parked and thirsty. Tap <color=#FFD15C>" + waiting.Label.text + "</color> to send the truck out to it.";
                    }
                    else
                    {
                        // Light up the aircraft on its way in, and let the world carry on round it.
                        var plane = AirportView.Instance != null ? AirportView.Instance.Planes.FirstOrDefault() : null;
                        if (plane != null) { var c = ToCanvas(plane.Head); if (c.z > 0) { point = new Vector2(c.x, c.y); size = new Vector2(260, 200); } }
                        say = "Here comes your first flight! When it parks at a stand it needs fuel before it can leave.";
                        block = false; hand = false; dim = 0.45f;
                    }
                    break;
                }
                case Tut.Money:
                    target = (RectTransform)_money.transform.parent;
                    say = st.flightsHandled > 0
                        ? "On its way, and on time. That's your first fare in the bank. Every flight you turn round pays like this."
                        : "The truck's on its way. When the flight leaves on time it pays you, right here.";
                    block = false; hand = false; next = true;
                    break;
                case Tut.Goal:
                    if (_sim.GoalDone)
                    {
                        target = (RectTransform)_goalClaim.transform;
                        say = "A goal done! Tap <color=#FFD15C>CLAIM</color> to collect what it pays.";
                    }
                    else
                    {
                        target = _goal;
                        say = "Your next goal always waits here, and each one pays when it's done.";
                        hand = false; block = false;
                    }
                    break;
                case Tut.KeepGoing:
                    if (_sim.GoalDone && _tab < 0)
                    {
                        target = (RectTransform)_goalClaim.transform;
                        say = "Three flights handled. Tap <color=#FFD15C>CLAIM</color>!";
                    }
                    else
                    {
                        say = "Nicely done. Keep tapping the crews out to each flight that parks. Turn round <color=#FFD15C>3 flights</color> and claim again.";
                        block = false; hand = false; dim = 0f;
                    }
                    break;
                default: // Build
                {
                    string why = _sim.UpgradeBlocked("stands");
                    if (_tab != 0)
                    {
                        target = (RectTransform)_tabs[0].transform;
                        say = "One stand turns one flight at a time. Let's build a second: open <color=#FFD15C>BUILD</color>.";
                    }
                    else
                    {
                        var btn = StandsButton();
                        if (btn != null) { target = btn; }
                        say = why == null ? "Here it is: <color=#FFD15C>Aircraft Stands</color>. Tap <color=#FFD15C>UPGRADE</color> to add your second stand."
                                          : "Aircraft Stands is the one. " + why + " for now: keep the flights coming and it'll be ready soon.";
                        if (why != null) { hand = false; block = false; }
                    }
                    break;
                }
            }

            // The hole.
            if (target != null)
            {
                var r = target.rect;
                Vector2 c0 = _tutLayer.InverseTransformPoint(target.TransformPoint(r.center));
                var s0 = Vector2.Scale(r.size, (Vector2)target.lossyScale) / Mathf.Max(0.0001f, _tutLayer.lossyScale.x);
                point = c0; size = s0 + new Vector2(28, 28);
            }
            if (point.HasValue) _holeGoal = new Vector4(point.Value.x, point.Value.y, size.x * 0.5f, size.y * 0.5f);
            else _holeGoal = new Vector4(0, 0, 0, 0);
            float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
            if (_hole.z <= 0 || _holeGoal.z <= 0) _hole = _holeGoal; else _hole = Vector4.Lerp(_hole, _holeGoal, k);
            _dimA = Mathf.Lerp(_dimA, dim, k);
            if (_tutMat != null)
            {
                _tutMat.SetVector("_Hole", _hole);
                _tutMat.SetColor("_Color", new Color(Dim.r, Dim.g, Dim.b, _dimA));
                _tutMat.SetFloat("_RimAlpha", _hole.z <= 0 || target == null ? 0f : block ? 0.6f + 0.35f * Mathf.Sin(Time.unscaledTime * 5) : 0.5f + 0.15f * Mathf.Sin(Time.unscaledTime * 3));
            }
            _tutDim.raycastTarget = block;
            _tutHole.Hole = _hole; _tutHole.Block = block; _tutHole.Layer = _tutLayer;

            // The hand taps on the target.
            _tutHand.gameObject.SetActive(hand && point.HasValue);
            if (hand && point.HasValue)
            {
                float tap = Mathf.Repeat(Time.unscaledTime * 1.1f, 1f);
                float press = tap < 0.25f ? tap / 0.25f : tap < 0.4f ? 1 : Mathf.Max(0, 1 - (tap - 0.4f) / 0.3f);
                var tip = new Vector2(_hole.x + _hole.z * 0.3f, _hole.y - _hole.w * 0.62f);
                _tutHand.anchoredPosition = tip + new Vector2(18, -26) * (1 - press);
                _tutHand.localScale = Vector3.one * (1f - 0.1f * press);
                _tutHand.localRotation = Quaternion.Euler(0, 0, 18);
            }

            // Priya's bubble: on the side of the screen away from the hole.
            _tutType += Time.unscaledDeltaTime * 55;
            if (_tutSay != say) { _tutSay = say; _tutType = 0; _tutPop = 0; }
            _coachText.text = TypeOut(say, (int)_tutType);
            float H = _tutLayer.rect.height, W = _tutLayer.rect.width;
            bool high = point.HasValue && point.Value.y > -H * 0.1f;
            float bx = point.HasValue ? Mathf.Clamp(-point.Value.x * 0.35f, -W * 0.5f + 470, W * 0.5f - 470) : 0;
            if (_tab >= 0) bx = -W * 0.5f + 600;    // a sheet is open on the right: keep to the left
            float by = high ? -H * 0.5f + 150 : H * 0.5f - 300;
            _coach.anchoredPosition = Vector2.Lerp(_coach.anchoredPosition, new Vector2(bx, by), _tutPop < 0.05f ? 1 : k);
            _tutPop = Mathf.Min(1, _tutPop + Time.unscaledDeltaTime * 4);
            _coach.localScale = Vector3.one * PopIn(_tutPop);
            _coachNext.gameObject.SetActive(next);
            _coachNextLabel.text = "NEXT";
            int dot = step == Tut.FirstFlight ? 0 : step == Tut.Money ? 1 : step == Tut.Goal ? 2 : step == Tut.KeepGoing ? 3 : 4;
            for (int i = 0; i < _tutDots.childCount; i++)
                _tutDots.GetChild(i).GetComponent<Image>().color = i == dot ? Ui.Gold : i < dot ? new Color(1, 0.82f, 0.35f, 0.5f) : new Color(1, 1, 1, 0.18f);
            _tutHand.SetAsLastSibling();
        }

        /// <summary>The BUILD sheet's button for Aircraft Stands, scrolled into view.</summary>
        RectTransform StandsButton()
        {
            string name = Content.Facilities["stands"].Name;
            foreach (RectTransform row in _sheetList)
            {
                var t = row.Find("Title");
                if (t == null || !t.GetComponent<Text>().text.StartsWith(name)) continue;
                var view = (RectTransform)_sheetList.parent;
                float top = -row.anchoredPosition.y, bottom = top + row.rect.height, y = _sheetList.anchoredPosition.y;
                if (top < y || bottom > y + view.rect.height) _sheetList.anchoredPosition = new Vector2(0, Mathf.Max(0, top - 40));
                return (RectTransform)row.Find("Do");
            }
            return null;
        }

        /// <summary>The welcome and send-off cards, centred over the dimmed airport.</summary>
        void TutCard(Tut step)
        {
            _coach.gameObject.SetActive(false); _tutHand.gameObject.SetActive(false);
            _tutDim.raycastTarget = true; _tutHole.Block = true; _tutHole.Hole = Vector4.zero;
            _dimA = Mathf.Lerp(_dimA, 0.72f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
            if (_tutMat != null) { _tutMat.SetVector("_Hole", Vector4.zero); _tutMat.SetColor("_Color", new Color(Dim.r, Dim.g, Dim.b, _dimA)); }
            if (_tutCard == null)
            {
                bool hello = step == Tut.Welcome;
                _tutCard = Ui.ArtImg("Card", _tutLayer, "panel", Color.white).rectTransform;
                _tutCard.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1040, 640));
                Ui.Img("Back", _tutCard, new Color(0.06f, 0.09f, 0.14f, 0.98f), true).rectTransform.Fill(10);
                var rays = Ui.ArtImg("Rays", _tutCard, "rays", new Color(1f, 0.82f, 0.3f, hello ? 0.16f : 0.26f), 0).rectTransform;
                rays.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230, 30), new Vector2(560, 560));
                rays.gameObject.AddComponent<Spin>().Speed = 12;
                var face = FaceTile(_tutCard, Mentor, "P", Palette.Hex(0x3F78C2), new Vector2(0, 0.5f), new Vector2(230, 30), 300);
                face.gameObject.AddComponent<Bob>();
                var tag = Ui.ArtImg("Tag", _tutCard, "plate", Color.white).rectTransform;
                tag.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230, -158), new Vector2(330, 64));
                Ui.Label("T", tag, "PRIYA NAND", 28, Color.white).rectTransform.Fill();
                Ui.Label("R", _tutCard, "APPROACH CONTROLLER", 20, Ui.Gold).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230, -206), new Vector2(330, 30));
                var head = Ui.Label("H", _tutCard, hello ? "WELCOME TO\nCROSSWIND" : "YOU'RE IN\nBUSINESS!", 64, Ui.Gold, TextAnchor.UpperLeft);
                head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, -46), new Vector2(580, 170)); head.lineSpacing = 0.9f;
                Outline(head);
                var body = Ui.Label("B", _tutCard, hello
                    ? "Morning, manager! I'm Priya, up in the tower. This little airfield is yours now: one runway, one stand and a terminal.\n\nAirlines will fly in if we turn their aircraft round fast. I'll show you how."
                    : "Two stands, flights coming in, money in the bank. That's an airport!\n\nThe goal card at the top always shows what's next. Tap any aircraft to see its flight. I'll be in the tower.",
                    30, Color.white, TextAnchor.UpperLeft, false);
                body.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(430, -220), new Vector2(570, 270));
                body.horizontalOverflow = HorizontalWrapMode.Wrap; body.verticalOverflow = VerticalWrapMode.Overflow; body.lineSpacing = 1.08f;
                var go = Ui.Button("Go", _tutCard, hello ? "LET'S GO" : "LET'S FLY", hello ? Ui.Green : Ui.Gold, () =>
                {
                    Sfx.Play(hello ? "click" : "claim", 0.7f);
                    if (hello) { TutStep = Tut.FirstFlight; Destroy(_tutCard.gameObject); _tutCard = null; }
                    else EndTutorial();
                    GameRoot.Instance.Save();
                }, 44);
                ((RectTransform)go.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(330, 104));
                if (hello)
                {
                    var skip = Ui.Button("Skip", _tutCard, "I KNOW THE ROPES", new Color(0, 0, 0, 0), () => { Sfx.Play("close", 0.5f); EndTutorial(); }, 24);
                    ((RectTransform)skip.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(430, 64), new Vector2(280, 56));
                    skip.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.55f);
                }
                _tutPop = 0;
            }
            _tutPop = Mathf.Min(1, _tutPop + Time.unscaledDeltaTime * 3.5f);
            _tutCard.localScale = Vector3.one * PopIn(_tutPop);
        }

        /// <summary>Ease-out with a little overshoot, for things popping in.</summary>
        static float PopIn(float t) { t = Mathf.Clamp01(t); float s = 1.6f; t -= 1; return 0.85f + 0.15f * (t * t * ((s + 1) * t + s) + 1); }

        /// <summary>The first n visible characters of a line with colour tags, the tags kept whole.</summary>
        static string TypeOut(string s, int n)
        {
            var sb = new System.Text.StringBuilder(); int shown = 0; bool open = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<') { int j = s.IndexOf('>', i); if (j < 0) break; string tag = s.Substring(i, j - i + 1); if (shown < n || tag.StartsWith("</")) { sb.Append(tag); open = !tag.StartsWith("</"); } i = j; continue; }
                if (shown >= n) { if (open) { sb.Append("</color>"); open = false; } continue; }
                sb.Append(s[i]); shown++;
            }
            return sb.ToString();
        }
    }

    /// <summary>Lets taps through the tutorial's spotlight hole and blocks them everywhere else.</summary>
    public sealed class TutHole : MonoBehaviour, ICanvasRaycastFilter
    {
        public Vector4 Hole; public bool Block; public RectTransform Layer;
        public bool IsRaycastLocationValid(Vector2 sp, Camera cam)
        {
            if (!Block) return false;
            if (Hole.z <= 0 || Layer == null) return true;
            Vector2 p;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Layer, sp, cam, out p)) return true;
            return Mathf.Abs(p.x - Hole.x) > Hole.z || Mathf.Abs(p.y - Hole.y) > Hole.w;
        }
    }

    /// <summary>A gentle up-and-down, so a portrait looks alive.</summary>
    public sealed class Bob : MonoBehaviour
    {
        Vector2 _base; bool _set;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_set) { _base = rt.anchoredPosition; _set = true; }
            rt.anchoredPosition = _base + new Vector2(0, Mathf.Sin(Time.unscaledTime * 1.8f) * 5);
        }
    }
}
