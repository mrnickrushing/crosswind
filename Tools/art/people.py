#!/usr/bin/env python3
"""Downloads the people (Quaternius, CC0, poly.pizza) into art-src/free/people; Tools/art/models.py poses them.
python3 Tools/art/people.py"""
import os, re, urllib.request
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "art-src", "free", "people")
UA = {"User-Agent": "Mozilla/5.0 Crosswind-asset-pipeline"}
IDS = {"dress": "zMyPlQXBzq", "longsleeves": "DLptRuewTn", "man_a": "fjHyMd5Wxw", "man_b": "HMnuH5geEG", "suit": "mQnGoME1ez", "tanktop": "XqzeZGB7iU", "woman_c": "9kF7eTDbhO",
       "woman_casual": "jpKRgGDxhk", "woman_d": "AQsd9ngvKU", "casual": "kZ3DmIoGip", "business": "JFrLIKqvCH", "hoodie": "gKLBoRsyKe", "worker": "Yg2bQZO6Hj"}
os.makedirs(OUT, exist_ok=True)
for name, pid in IDS.items():
    dst = os.path.join(OUT, name + ".glb")
    if os.path.exists(dst): continue
    page = urllib.request.urlopen(urllib.request.Request("https://poly.pizza/m/" + pid, headers=UA), timeout=60).read().decode("utf-8", "ignore")
    glb = re.search(r"https://static\.poly\.pizza/[^\"]+\.glb", page).group(0)
    open(dst, "wb").write(urllib.request.urlopen(urllib.request.Request(glb, headers=UA), timeout=120).read())
    print("people", name)
