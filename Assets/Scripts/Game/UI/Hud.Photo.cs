using System.Collections;
using System.IO;
using Crosswind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crosswind.Game
{
    /// <summary>
    /// Photo mode (the camera on the clock panel): the interface steps aside, the airfield or the hall stays live to
    /// drag and pinch, ANGLE swings the camera through a few views, and TAKE PHOTO captures the screen at full
    /// resolution with the airport's name and rating on a plate in the corner. The picture is kept on the device
    /// (Application.persistentDataPath/Photos, the newest under <see cref="LastPhoto"/>) and SHARE hands it to the
    /// system's share sheet (Plugins/iOS/CrosswindShare.mm), which can save it to Photos.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform _photo, _photoBar, _photoPlate, _photoDone;
        Text _photoAngle, _photoName, _photoStars;
        RawImage _photoThumb;
        Texture2D _photoTex;
        int _angle;
        bool _shooting;

        /// <summary>The path of the last photo taken on this device (empty: none yet). Prestige keeps the one of each airport sold.</summary>
        public static string LastPhoto { get { return PlayerPrefs.GetString("cw_last_photo", ""); } private set { PlayerPrefs.SetString("cw_last_photo", value); PlayerPrefs.Save(); } }
        public bool PhotoMode { get { return _photo != null && _photo.gameObject.activeSelf; } }

        struct View { public string Name; public float Pitch, Yaw; }
        static readonly View[] Outside =
        {
            new View { Name = "CLASSIC", Pitch = 40, Yaw = 128 }, new View { Name = "LOW", Pitch = 14, Yaw = 128 }, new View { Name = "HIGH", Pitch = 68, Yaw = 128 },
            new View { Name = "LEFT", Pitch = 30, Yaw = 96 }, new View { Name = "RIGHT", Pitch = 30, Yaw = 160 },
        };
        static readonly View[] Indoors =
        {
            new View { Name = "PLAN", Pitch = 55, Yaw = 90 }, new View { Name = "LOW", Pitch = 26, Yaw = 90 }, new View { Name = "CORNER", Pitch = 40, Yaw = 62 }, new View { Name = "OTHER CORNER", Pitch = 40, Yaw = 118 },
        };
        View[] Views { get { return CameraRig.Inside ? Indoors : Outside; } }

        void BuildPhoto()
        {
            _photo = Ui.Rect("Photo", _root); _photo.Fill();
            var safe = Ui.Rect("Safe", _photo); safe.Fill(); safe.gameObject.AddComponent<SafeArea>();

            // The controls along the foot: the angle, the shutter, done.
            _photoBar = Ui.ArtImg("Bar", safe, "panel", Color.white).rectTransform;
            _photoBar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(980, 112));
            _photoBar.GetComponent<Image>().raycastTarget = true;
            var angle = Ui.Button("Angle", _photoBar, "", Ui.Blue, NextAngle, 28);
            ((RectTransform)angle.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(300, 86));
            _photoAngle = angle.GetComponentInChildren<Text>();
            var shoot = Ui.Button("Shoot", _photoBar, "TAKE PHOTO", Ui.Gold, () => StartCoroutine(Shoot()), 34);
            ((RectTransform)shoot.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(320, 86));
            shoot.GetComponentInChildren<Text>().color = Ui.Ink;
            var done = Ui.Button("Done", _photoBar, "DONE", Ui.Off, () => SetPhotoMode(false), 30);
            ((RectTransform)done.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(300, 86));

            // The plate the picture carries: the airport, its rating, the game.
            _photoPlate = Ui.ArtImg("Plate", safe, "panel", Color.white).rectTransform;
            _photoPlate.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 24), new Vector2(620, 128));
            _photoName = Ui.Label("Name", _photoPlate, "", 40, Color.white, TextAnchor.UpperLeft);
            _photoName.rectTransform.Fill(); _photoName.rectTransform.offsetMin = new Vector2(24, 50); _photoName.rectTransform.offsetMax = new Vector2(-20, -12);
            _photoName.resizeTextForBestFit = true; _photoName.resizeTextMinSize = 24; _photoName.resizeTextMaxSize = 40;
            _photoStars = Ui.Label("Stars", _photoPlate, "", 30, Ui.Gold, TextAnchor.LowerLeft, false);
            _photoStars.rectTransform.Fill(); _photoStars.rectTransform.offsetMin = new Vector2(24, 12); _photoStars.rectTransform.offsetMax = new Vector2(-20, -70);
            _photoPlate.gameObject.SetActive(false);

            // After the shot: the picture, small, with SHARE.
            _photoDone = Ui.ArtImg("Taken", safe, "panel", Color.white).rectTransform;
            _photoDone.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(460, 360));
            _photoDone.GetComponent<Image>().raycastTarget = true;
            _photoThumb = new GameObject("Thumb", typeof(RectTransform)).AddComponent<RawImage>();
            _photoThumb.transform.SetParent(_photoDone, false);
            _photoThumb.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(428, 241));
            var share = Ui.Button("Share", _photoDone, "SHARE", Ui.Green, SharePhoto, 30);
            ((RectTransform)share.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 14), new Vector2(240, 80));
            var close = Ui.Button("Close", _photoDone, "OK", Ui.Off, () => _photoDone.gameObject.SetActive(false), 30);
            ((RectTransform)close.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 14), new Vector2(170, 80));
            _photoDone.gameObject.SetActive(false);
            _photo.gameObject.SetActive(false);
        }

        void SetPhotoMode(bool on)
        {
            if (on == PhotoMode) return;
            if (on)
            {
                OpenTab(-1); StopPlacing(); ClosePick();
                _infoFlight = -1; _info.gameObject.SetActive(false);
                _angle = 0;
                CameraRig.Angle = null;
                Telemetry.Feature("photo_mode", CameraRig.Inside ? "terminal" : "airfield");
            }
            else { CameraRig.Angle = null; _photoDone.gameObject.SetActive(false); }
            _safe.gameObject.SetActive(!on);
            _world.gameObject.SetActive(!on);
            _photo.gameObject.SetActive(on);
            _photo.SetAsLastSibling();
            _photoAngle.text = "ANGLE: " + Views[_angle].Name;
            Sfx.Play(on ? "open" : "close", 0.5f);
        }

        void NextAngle()
        {
            _angle = (_angle + 1) % Views.Length;
            var v = Views[_angle];
            CameraRig.Angle = _angle == 0 ? (Vector2?)null : new Vector2(v.Pitch, v.Yaw);
            _photoAngle.text = "ANGLE: " + v.Name;
        }

        IEnumerator Shoot()
        {
            if (_shooting) yield break;
            _shooting = true;
            var s = _sim;
            string who = string.IsNullOrEmpty(s.State.playerName) ? "" : s.State.playerName + "'s ";
            _photoName.text = who + s.Map.Name.ToUpper();
            _photoStars.text = new string('★', s.Stars) + "<color=#FFFFFF40>" + new string('★', Mathf.Max(0, Content.MaxStars - s.Stars)) + "</color>   <color=#FFFFFFB0><size=24>CROSSWIND  ·  " + Format.Money(s.State.flightsHandled) + " FLIGHTS</size></color>";
            _photoBar.gameObject.SetActive(false); _photoDone.gameObject.SetActive(false);
            _photoPlate.gameObject.SetActive(true);
            FrameBudget.Sharp(true);
            yield return null; yield return null;              // the full-resolution frame and the plate are drawn
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            FrameBudget.Sharp(false);
            _photoPlate.gameObject.SetActive(false);
            _photoBar.gameObject.SetActive(true);
            string dir = Path.Combine(Application.persistentDataPath, "Photos");
            string file = Path.Combine(dir, "crosswind_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(file, tex.EncodeToPNG());
                LastPhoto = file;
            }
            catch (System.Exception e) { Debug.LogWarning("Photo not saved: " + e.Message); file = null; }
            if (_photoTex != null) Destroy(_photoTex);
            _photoTex = tex;
            _photoThumb.texture = tex;
            _photoDone.gameObject.SetActive(file != null);
            Sfx.Play("claim", 0.6f);
            Telemetry.Feature("photo_taken", Views[_angle].Name.ToLower());
            _shooting = false;
        }

        void SharePhoto()
        {
            string file = LastPhoto;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) { Toast("The photo is gone: take another"); return; }
            Telemetry.Feature("photo_shared");
#if UNITY_IOS && !UNITY_EDITOR
            CWShareImage(file, "My airport in Crosswind");
#else
            Toast("Saved: " + file);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void CWShareImage(string path, string text);
#endif
    }
}
