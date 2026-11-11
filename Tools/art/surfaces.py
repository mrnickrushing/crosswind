#!/usr/bin/env python3
"""Tileable ground textures (CC0): Poly Haven ids, or "acg:<id>" from ambientCG. Writes Resources/Textures/<Name>.jpg and
<Name>Normal.jpg (1k).   python3 Tools/art/surfaces.py"""
import io, json, os, sys, urllib.request, zipfile
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Textures")
UA = {"User-Agent": "Crosswind-asset-pipeline"}
SURFACES = {"Asphalt": "asphalt_track", "Tarmac": "clean_asphalt", "Concrete": "concrete_pavement", "Grass": "acg:Grass001"}
def get(url): return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120).read()
os.makedirs(OUT, exist_ok=True)
for name, tid in SURFACES.items():
    if sys.argv[1:] and name not in sys.argv[1:]: continue
    try:
        if tid.startswith("acg:"):
            z = zipfile.ZipFile(io.BytesIO(get("https://ambientcg.com/get?file=%s_1K-JPG.zip" % tid[4:])))
            for n in z.namelist():
                if n.endswith("_Color.jpg"): open(os.path.join(OUT, name + ".jpg"), "wb").write(z.read(n))
                if n.endswith("_NormalGL.jpg"): open(os.path.join(OUT, name + "Normal.jpg"), "wb").write(z.read(n))
        else:
            f = json.loads(get("https://api.polyhaven.com/files/" + tid))
            open(os.path.join(OUT, name + ".jpg"), "wb").write(get(f["Diffuse"]["1k"]["jpg"]["url"]))
            open(os.path.join(OUT, name + "Normal.jpg"), "wb").write(get(f["nor_gl"]["1k"]["jpg"]["url"]))
        print("surface", name, "<-", tid)
    except Exception as e:
        print("FAILED", name, e)
