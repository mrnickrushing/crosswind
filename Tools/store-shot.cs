// App Store screenshot: renders the game camera plus the overlay HUD offscreen at an exact size, which the
// Editor Game view can't. Run it through Tools/store-shot.sh, which first sets the Game view to the same aspect so the
// HUD is laid out for it. iPhone 6.9": 2868x1320; iPad 13": 2752x2064.
int W = __W__, H = __H__;
var cam = Crosswind.Game.CameraRig.Instance.Cam;
var rt = new UnityEngine.RenderTexture(W, H, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
var changed = new System.Collections.Generic.List<UnityEngine.Canvas>();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy) { c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.2f; changed.Add(c); }
var prev = cam.targetTexture;
cam.targetTexture = rt;
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
var prevActive = UnityEngine.RenderTexture.active;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
tex.Apply();
UnityEngine.RenderTexture.active = prevActive;
cam.targetTexture = prev;
foreach (var c in changed) { c.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay; }
System.IO.File.WriteAllBytes("__OUT__", tex.EncodeToPNG());
rt.Release();
return W + "x" + H;
