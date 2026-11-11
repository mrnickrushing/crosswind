using System.Collections.Generic;
using System.Linq;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// The team on the STAFF sheet: crates (free, earned, for gems; the odds are printed), who is on duty, and
    /// everyone you have or have yet to meet. Opening a crate shows the person on a card.
    /// </summary>
    public sealed partial class Hud
    {
        static readonly Color[] RarityColor = { new Color(0.55f, 0.62f, 0.7f), new Color(0.25f, 0.56f, 0.95f), new Color(0.68f, 0.4f, 0.95f), new Color(1f, 0.74f, 0.2f) };
        static readonly string[] RarityName = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        RectTransform _crateCard;

        static string Initials(string name)
        {
            var parts = name.Split(' ').Where(p => p.Length > 0 && p != "Captain" && p != "Sir").ToArray();
            return parts.Length >= 2 ? "" + parts[0][0] + parts[parts.Length - 1][0] : name.Substring(0, 1);
        }

        void Face(Transform parent, CrewDef d, Vector2 anchor, Vector2 pos, float size, bool known)
        {
            var ring = Ui.Img("Ring", parent, known ? RarityColor[(int)d.Rarity] : Ui.Off); ring.sprite = Ui.Circle;
            ring.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var inner = Ui.Img("Face", ring.transform, Ui.Ink); inner.sprite = Ui.Circle;
            inner.rectTransform.Fill(size * 0.07f);
            var face = known ? Portraits.Of(Sim.Crew.IndexOf(d)) : null;
            if (face == null) { Ui.Label("I", inner.transform, known ? Initials(d.Name) : "?", Mathf.RoundToInt(size * 0.4f), known ? Color.white : Ui.Dim).rectTransform.Fill(); return; }
            // Their portrait, cut to the circle, on their rarity's colour.
            inner.color = Color.Lerp(RarityColor[(int)d.Rarity], Ui.Ink, 0.55f);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pic = Ui.Img("Portrait", inner.transform, Color.white); pic.sprite = face;
            pic.rectTransform.Fill(-size * 0.04f);
        }

        void CrewSection()
        {
            var s = _sim;
            int have = Sim.Crew.Count(c => s.CrewLevel(c.Id) > 0), on = Sim.Crew.Count(c => s.OnDuty(c.Id));
            Header("YOUR TEAM  ·  " + have + " OF " + Sim.Crew.Count + "  ·  ON DUTY " + on + " OF " + s.DutyPlaces);
            if (s.State.crates > 0)
                Entry("Team crate" + (s.State.crates > 1 ? "s: " + s.State.crates : ""), "ONE PERSON INSIDE", "Someone new, or another copy toward a level. A crate comes with every star.", "OPEN", null, OpenCrate, Ui.Gold);
            Entry("Free crate", s.FreeCrateReady ? "READY" : "IN " + Format.Time(s.FreeCrateIn).ToUpper(), "One free crate every " + Sim.FreeCrateHours + " hours.", "COLLECT", s.FreeCrateReady ? null : "Not yet",
                () => { if (s.TakeFreeCrate()) OpenCrate(); }, Ui.Green);
            Entry("Buy a crate", null, "Odds: Common " + Sim.CrateOdds[0] + "%, Rare " + Sim.CrateOdds[1] + "%, Epic " + Sim.CrateOdds[2] + "%, Legendary " + Sim.CrateOdds[3] + "%. An Epic or better is certain by every " + Sim.CratePity + "th crate.",
                "BUY\n<size=34>" + Gems(Sim.CrateGems) + "</size>", s.State.gems >= Sim.CrateGems ? null : "Need " + Gems(Sim.CrateGems - s.State.gems) + " more", () => { if (s.BuyCrate()) OpenCrate(); }, Ui.Blue);

            foreach (var d in Sim.Crew.OrderByDescending(c => s.OnDuty(c.Id)).ThenByDescending(c => s.CrewLevel(c.Id) > 0).ThenByDescending(c => (int)c.Rarity))
            {
                int level = s.CrewLevel(d.Id); bool known = level > 0, duty = s.OnDuty(d.Id);
                var row = Row(168);
                Face(row, d, new Vector2(0, 0.5f), new Vector2(84, 0), 124, known);
                string tag = "<size=28><color=#" + ColorUtility.ToHtmlStringRGB(RarityColor[(int)d.Rarity]) + ">" + RarityName[(int)d.Rarity] + "</color></size>";
                var t = Ui.Label("Title", row, (known ? d.Name : "Not met yet") + "  " + tag, 38, known ? Color.white : Ui.Dim, TextAnchor.UpperLeft);
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(164, 0); t.rectTransform.offsetMax = new Vector2(-250, -12);
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 38; t.verticalOverflow = VerticalWrapMode.Truncate;
                t.rectTransform.offsetMin = new Vector2(164, 112);
                string next = !known ? d.Role + "  ·  " + Sim.CrewPerk(d, 1) + " at level 1"
                    : d.Role + "  ·  level " + level + "  ·  " + Sim.CrewPerk(d, level) + (level >= Sim.MaxCrewLevel ? "  ·  top level" : "  ·  " + s.CopiesToNext(d.Id) + " more for level " + (level + 1));
                var b = Ui.Label("Body", row, next + (known ? "\n<i>\"" + d.Line + "\"</i>" : ""), 28, Ui.Dim, TextAnchor.UpperLeft, false);
                b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(164, 10); b.rectTransform.offsetMax = new Vector2(-250, -60);
                b.resizeTextForBestFit = true; b.resizeTextMinSize = 20; b.resizeTextMaxSize = 28; b.verticalOverflow = VerticalWrapMode.Truncate;
                if (!known) continue;
                string blocked = duty ? null : s.DutyBlocked(d.Id);
                var btn = Ui.Button("Duty", row, null, duty ? Ui.Green : blocked != null ? Ui.Off : Ui.Card + new Color(0.08f, 0.08f, 0.08f, 0), () =>
                {
                    if (blocked != null) { Toast(blocked); Sfx.Play("error", 0.5f); return; }
                    if (s.ToggleDuty(d.Id)) Toast(d.Name + (s.OnDuty(d.Id) ? " is on duty: " + Sim.CrewPerk(d, level) : " is resting"));
                    _dirty = true;
                }, 34);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(214, 110));
                Ui.Label("L", btn.transform, duty ? "ON DUTY" : "PUT ON\nDUTY", 32, Color.white).rectTransform.Fill(6);
            }
        }

        void OpenCrate()
        {
            var got = _sim.OpenCrate();
            if (got == null) return;
            GameRoot.Instance.Save();
            ShowCrate(got);
        }

        void ShowCrate(CrateResult got)
        {
            if (_crateCard != null) Destroy(_crateCard.gameObject);
            var d = got.Who; var tint = RarityColor[(int)d.Rarity];
            var shade = Ui.Img("Crate", _root, new Color(0, 0, 0, 0.72f));
            shade.raycastTarget = true;
            _crateCard = shade.rectTransform; _crateCard.Fill(); _crateCard.SetAsLastSibling();
            var card = Ui.Img("Card", _crateCard, Ui.Panel, true).rectTransform;
            card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(820, 980));
            var band = Ui.Img("Band", card, tint, true).rectTransform;
            band.Band(true, 0, 120);
            Ui.Label("Rarity", band, RarityName[(int)d.Rarity], 54, Ui.Ink).rectTransform.Fill();
            var rays = Ui.ArtImg("Rays", card, "rays", new Color(tint.r, tint.g, tint.b, 0.4f), 0);
            rays.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(760, 760));
            rays.gameObject.AddComponent<Spin>();
            Face(card, d, new Vector2(0.5f, 1), new Vector2(0, -300), 300, true);
            Ui.Label("Name", card, d.Name, 62, Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -480), new Vector2(780, 80));
            Ui.Label("Role", card, d.Role.ToUpper(), 36, tint).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -556), new Vector2(780, 50));
            Ui.Label("Line", card, "<i>\"" + d.Line + "\"</i>", 32, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -612), new Vector2(720, 90));
            string what = got.GemsBack > 0 ? "Already at the top level: " + Gems(got.GemsBack) + " back"
                : got.New ? "NEW!  " + Sim.CrewPerk(d, 1) + (_sim.OnDuty(d.Id) ? "\n<size=30>On duty from now</size>" : "\n<size=30>Put them on duty in STAFF</size>")
                : got.LevelUp ? "LEVEL " + got.Level + "!  " + Sim.CrewPerk(d, got.Level)
                : "Level " + got.Level + "  ·  " + _sim.CopiesToNext(d.Id) + " more for level " + (got.Level + 1);
            Ui.Label("What", card, what, 40, got.New || got.LevelUp ? Ui.Gold : Color.white).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -716), new Vector2(760, 110));
            bool more = _sim.State.crates > 0;
            var ok = Ui.Button("Ok", card, more ? "OPEN ANOTHER (" + _sim.State.crates + ")" : "GOOD", more ? Ui.Gold : Ui.Green, () =>
            {
                Destroy(_crateCard.gameObject); _crateCard = null; _dirty = true;
                if (more) OpenCrate();
            }, 44);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(more ? 560 : 400, 110));
            if (more)
            {
                var close = Ui.Button("Close", card, "X", new Color(0, 0, 0, 0), () => { Destroy(_crateCard.gameObject); _crateCard = null; _dirty = true; }, 50);
                ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -14), new Vector2(92, 92));
            }
            Sfx.Play(d.Rarity >= Rarity.Epic ? "star" : "chime", 0.8f);
        }
    }

    /// <summary>Turns slowly, whatever the game's speed (the rays behind a reward).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float Speed = 20;
        void Update() { transform.Rotate(0, 0, Speed * Time.unscaledDeltaTime); }
    }

    /// <summary>
    /// Portraits of the team, rendered once each from the game's own character models (head and shoulders, on a clear
    /// background) so every face in the STAFF sheet and on a crate card is a person, not initials.
    /// </summary>
    public static class Portraits
    {
        const int Size = 256, Layer = 31, Kinds = 12;
        public static float Up = 0.3f, Zoom = 0.74f;      // framing: how far above the middle to aim, and how much of the height to show
        static readonly Dictionary<int, Sprite> _made = new Dictionary<int, Sprite>();
        static Camera _cam;

        public static Sprite Of(int who)
        {
            Sprite s;
            if (_made.TryGetValue(who, out s)) return s;
            // Sixteen people from twelve models: the last four share a model with someone of another rarity, turned the other way.
            var mesh = Resources.Load<Mesh>("Models/person_" + (who * 5 % Kinds) + "_idle");
            if (mesh == null) return _made[who] = null;
            if (_cam == null)
            {
                var c = new GameObject("Portrait camera");
                Object.DontDestroyOnLoad(c);
                _cam = c.AddComponent<Camera>();
                _cam.enabled = false;
                _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0, 0, 0, 0);
                _cam.cullingMask = 1 << Layer; _cam.orthographic = true; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 20;
            }
            var go = new GameObject("Sitter");
            go.layer = Layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.Kit;
            var at = new Vector3(0, -500, 0);
            go.transform.position = at;
            var b = mesh.bounds;
            var rot = Quaternion.Euler(6, who >= Kinds ? -24 : 24, 0);
            _cam.transform.rotation = rot;
            _cam.transform.position = at + new Vector3(0, b.center.y + b.extents.y * Up, 0) + rot * Vector3.forward * 4; _cam.transform.rotation = Quaternion.LookRotation(-(rot * Vector3.forward));
            _cam.orthographicSize = b.extents.y * Zoom;
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            _cam.targetTexture = rt;
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(go);
            return _made[who] = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
