#!/usr/bin/env python3
"""Downloads Kenney kits (CC0, kenney.nl) into art-src/free/<kit>.   python3 Tools/art/kenney.py <kit> [<kit>...]"""
import http.cookiejar, json, os, re, sys, urllib.request, zipfile
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src", "free")
UA = {"User-Agent": "Mozilla/5.0"}
os.makedirs(SRC, exist_ok=True)
for kit in sys.argv[1:]:
    if os.path.isdir(os.path.join(SRC, kit)):
        print("have", kit); continue
    try:
        jar = http.cookiejar.CookieJar()
        op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
        page = op.open(urllib.request.Request("https://kenney.nl/assets/" + kit, headers=UA)).read().decode()
        did = re.search(r"data-download-id='([^']+)'", page).group(1)
        ref = dict(UA, Referer="https://kenney.nl/assets/" + kit)
        link = json.loads(op.open(urllib.request.Request("https://kenney.nl/download-link/" + did, headers=ref)).read())["url"]
        z = os.path.join(SRC, kit + ".zip")
        open(z, "wb").write(op.open(urllib.request.Request(link, headers=ref)).read())
        zipfile.ZipFile(z).extractall(os.path.join(SRC, kit)); os.remove(z)
        print("kit", kit)
    except Exception as e:
        print("FAILED", kit, e)
