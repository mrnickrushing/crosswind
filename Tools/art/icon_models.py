#!/usr/bin/env python3
"""The interface's icons: pictures of free 3D models (poly.pizza: Poly by Google and others CC BY 3.0, Quaternius and
Kenney CC0; two from Sketchfab, CC BY 4.0), rendered by Blender into Assets/Resources/UI/Icons/<name>.png.

  python3 Tools/art/icon_models.py fetch      # download what is missing into art-src/free/icons (asks poly.pizza gently)
  python3 Tools/art/icon_models.py render     # Blender: every icon -> Resources/UI/Icons (Tools/blender/icon_render.py)
  python3 Tools/art/icon_models.py credits    # the lines for CREDITS.md and the in-game credits

Only CC0 and CC BY models: fetch refuses anything whose page states another licence. Each model's title, author and
licence are kept in art-src/free/icons/manifest.json as its page gave them.
An icon is one model, or several stood side by side ("a+b"). Sketchfab models are downloaded by hand (they need an
account) into art-src/free/icons/sf_<name>/ and listed in SKETCHFAB.
"""
import json, os, re, subprocess, sys, time, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src", "free", "icons")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Icons")
UA = {"User-Agent": "Mozilla/5.0 Crosswind-asset-pipeline"}

# Icon name -> poly.pizza model id (or "a+b"), then how it is turned for its picture: yaw (degrees, 0 = as modelled).
ICONS = {
    # the rail
    "rail_build": "9AMKar2NlkX", "rail_airlines": "8ciDd9k8wha", "rail_research": "advGpqPqy0P", "rail_empire": "6Xu7mttjodo",
    "rail_terminal": "ezEfa_eupwK", "rail_airfield": "bgUY8zN2Bq9", "rail_friends": "HMnuH5geEG+jpKRgGDxhk", "rail_ops": "sf_tower",
    # what you build
    "runway": "fyTYd8f1poP", "stands": "2eG17I-VDiG", "heavy": "886-xsZYasM", "taxiways": "9r2Owu4oVHw", "tower": "sf_tower",
    "fuel": "bOjbK_rGqRA", "baggage": "ezEfa_eupwK", "catering": "eke7qcu_FR2", "cleaning": "bzCi_ZLQPN4", "pushback": "5TGoA5N14c5",
    "deice": "VHDU6OgUHN", "checkin": "2EHvZLax4Y3", "security": "2ZSHgKVr3F", "gates": "2EvQDFgG1H3", "parking": "75h3mi6uHuC",
    "ils": "5iVbfDhRnN7", "hangar": "6tNIeYHCQ0-", "cargo": "vzzCNUB6Zn",
    # menus
    "flask": "eqIGxcsBe1V", "trophy": "6Xu7mttjodo", "bank": "f1GOJtZFtsR", "gift": "LVg3ynJDxa", "coins": "fNCwo0rGrqD", "medal": "a_Losd2SuTZ",
    "clock": "4t8wFjBwJVI", "calendar": "8GqQAqxi3qk", "lock": "2ZSHgKVr3F", "globe": "1I5ip-3VOfv", "cart": "4lhRcMbl1pw", "box": "YAghI6GBls",
    "paint": "eeYDEQ1jJi5", "bolt": "8rX_fFhz6XH", "tasks": "8H5SDxwMnEA", "hammer": "cOizz1RJnb3", "bell": "efbZhPRsTuD", "trash": "fw6F3liNvHQ",
    "pen": "3Sd2klX0_s4", "cloud": "44cGXp6_8WD", "sack": "vFFblhnHtb", "money": "vFFblhnHtb", "crown": "0seQ0mn72s5", "target": "Ev3BbukDFS",
    "smile": "8RA5hHU5gHK", "tag": "5dwGkhjKXIW", "arrival": "a3XrQkLNna9", "sound": "720CVmivNcu", "id": "HMnuH5geEG", "handshake": "arIYNl9gMyr",
    "signout": "dRvd7q2nO-6", "compass": "LlnxQPETHh", "wings": "fzIXe2paBN9", "gear": "4hAw8zQHeMJ", "star": "0ddNZ3EsIhw", "gem": "09p9-Mnp4Sc",
    "user": "HMnuH5geEG", "userplus": "jpKRgGDxhk", "users": "HMnuH5geEG+jpKRgGDxhk", "queue": "HMnuH5geEG+jpKRgGDxhk",
    "chart": "sf_chart", "chartbar": "sf_chart", "pie": "sf_chart",
    # what passengers want
    "burger": "eke7qcu_FR2", "coffee": "fIuM_PW5prV", "shop": "6BGhNQlUzRR", "couch": "4QKlmmd0v2b", "restroom": "1Xudt9ErVxm",
    "gamepad": "6365MG_Pr_f", "broom": "bzCi_ZLQPN4", "sign": "3U2lj1gpeH", "tv": "9trLeWoBek",
}
# How a model is framed when the default does not suit it: yaw (degrees round the vertical), pitch (default 22: from
# higher up for flat things), top (only that share of its height, from the top).
VIEW = {"runway": dict(pitch=48), "taxiways": dict(pitch=48), "tag": dict(pitch=62), "coins": dict(pitch=38),
        # People are shown head and shoulders, the tower by its cab.
        "user": dict(top=0.36, pitch=10), "userplus": dict(top=0.36, pitch=10), "users": dict(top=0.36, pitch=10), "queue": dict(top=0.36, pitch=10),
        "rail_friends": dict(top=0.36, pitch=10), "id": dict(top=0.36, pitch=10), "rail_ops": dict(top=0.5, pitch=12), "tower": dict(top=0.5, pitch=12)}

SKETCHFAB = {
    "sf_tower": dict(uid="84f215391662453680e646649d42b264", title="Air Traffic Control Tower", author="abass20", license="CC BY 4.0"),
    "sf_chart": dict(uid="2ac83df8b7b34958b28dcbc3ed35db28", title="Data Chart Graphic Table Infographic", author="rikugo.studio", license="CC BY 4.0"),
}


def parts(spec):
    return spec.split("+")


def manifest():
    p = os.path.join(SRC, "manifest.json")
    return json.load(open(p)) if os.path.exists(p) else {}


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=90).read()


def fetch():
    os.makedirs(SRC, exist_ok=True)
    m = manifest()
    ids = sorted({p for spec in ICONS.values() for p in parts(spec) if not p.startswith("sf_")})
    for pid in ids:
        dst = os.path.join(SRC, pid + ".glb")
        if os.path.exists(dst) and pid in m:
            continue
        for attempt in range(4):
            try:
                page = get("https://poly.pizza/m/" + pid).decode("utf-8", "ignore")
                break
            except Exception as e:
                print("  retry", pid, e); time.sleep(8 * (attempt + 1))
        else:
            raise SystemExit("could not read the page of " + pid)
        glb = re.search(r"https://static\.poly\.pizza/[^\"\\ ]+\.glb", page)
        cc0 = "creativecommons.org/publicdomain/zero" in page
        by = re.search(r"creativecommons\.org/licenses/by/([0-9.]+)", page)
        other = re.search(r"creativecommons\.org/licenses/(by-[a-z-]+)/", page)
        author = re.search(r'"Creator":\{"Username":"([^"]+)"', page)
        title = re.search(r'"Title":"([^"]+)"', page)
        if not glb or other or not (cc0 or by):
            raise SystemExit("%s: no file, or a licence that is not CC0 / CC BY (%s)" % (pid, other.group(1) if other else "none stated"))
        open(dst, "wb").write(get(glb.group(0)))
        m[pid] = dict(title=title.group(1) if title else pid, author=author.group(1) if author else "?", license="CC0" if cc0 else "CC BY " + by.group(1), url="https://poly.pizza/m/" + pid)
        json.dump(m, open(os.path.join(SRC, "manifest.json"), "w"), indent=1, ensure_ascii=False)
        print("icon model", pid, m[pid]["title"], "|", m[pid]["author"], "|", m[pid]["license"], "|", os.path.getsize(dst) // 1024, "KB")
        time.sleep(2.5)
    for name, s in SKETCHFAB.items():
        if not find_sf(name):
            print("MISSING", name, "- download Sketchfab model", s["uid"], "as glTF into", os.path.join(SRC, name))


def find_sf(name):
    d = os.path.join(SRC, name)
    if not os.path.isdir(d):
        return None
    for root, _, files in os.walk(d):
        for f in sorted(files):
            if f.lower().endswith((".gltf", ".glb")):
                return os.path.join(root, f)
    return None


def path_of(part):
    return find_sf(part) if part.startswith("sf_") else os.path.join(SRC, part + ".glb")


def render(only=None):
    os.makedirs(OUT, exist_ok=True)
    jobs = []
    for name, spec in ICONS.items():
        if only and name not in only:
            continue
        files = [path_of(p) for p in parts(spec)]
        if any(f is None or not os.path.exists(f) for f in files):
            print("SKIP", name, "(model missing)"); continue
        jobs.append(dict(name=name, files=files, out=os.path.join(OUT, name + ".png"), **VIEW.get(name, {})))
    job_file = os.path.join(SRC, "render_jobs.json")
    json.dump(jobs, open(job_file, "w"))
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "icon_render.py"), "--", job_file], capture_output=True, text=True)
    print("\n".join(l for l in (r.stdout + r.stderr).splitlines() if "EMPTY" in l or "Error" in l or "Traceback" in l)[-6000:])
    for j in jobs:
        if os.path.exists(j["out"]):
            finish(j["out"])
    print(len([j for j in jobs if os.path.exists(j["out"])]), "icons in", OUT)


def finish(path, size=256):
    """Trims a render to a square round what was drawn, fades the shadow out toward the edges, and scales it down."""
    import numpy as np
    from PIL import Image
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(np.float32)
    n = a.shape[0]
    # The shadow may run to the frame: ease the last tenth of the picture out so it never ends in a straight cut.
    ramp = np.clip(np.minimum(np.arange(n), n - 1 - np.arange(n)) / (n * 0.1), 0, 1)
    a[:, :, 3] *= np.minimum.outer(ramp, ramp)
    ys, xs = np.nonzero(a[:, :, 3] > 70)          # the object itself (the shadow is fainter than this)
    if len(xs) == 0:
        return
    side = int(max(xs.max() - xs.min(), ys.max() - ys.min()) * 1.1) + 8
    cx, cy = (xs.max() + xs.min()) // 2, (ys.max() + ys.min()) // 2
    x0, y0 = max(0, min(n - side, cx - side // 2)), max(0, min(n - side, cy - side // 2))
    side = min(side, n)
    out = Image.fromarray(a.clip(0, 255).astype(np.uint8)).crop((x0, y0, x0 + side, y0 + side)).resize((size, size), Image.LANCZOS)
    out.save(path)


def credit_lines():
    m = manifest()
    by_author = {}
    for spec in ICONS.values():
        for p in parts(spec):
            e = SKETCHFAB.get(p) or m.get(p)
            if e:
                by_author.setdefault((e["author"], e["license"]), set()).add(e["title"])
    return sorted(((a, lic, sorted(t)) for (a, lic), t in by_author.items()), key=lambda x: (-len(x[2]), x[0]))


def credits():
    for author, lic, titles in credit_lines():
        print("- **%s** (%s): %s." % (author, lic, ", ".join(titles)))


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "fetch": fetch()
    elif cmd == "render": render(set(sys.argv[2:]) or None)
    else: credits()
