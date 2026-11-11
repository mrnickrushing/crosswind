#!/usr/bin/env python3
"""Downloads the road vehicles (Quaternius, CC0, poly.pizza) into art-src/free/cars; Tools/art/models.py converts them.
python3 Tools/art/cars.py"""
import os, re, urllib.request
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "art-src", "free", "cars")
UA = {"User-Agent": "Mozilla/5.0 Crosswind-asset-pipeline"}
IDS = {"car_a": "Cz6yDaUcM9", "car_b": "unqqkULtRU", "suv": "xsMtZhBkxL", "pickup": "qn4grQgHm8", "sports_a": "1mkmFkAz5v",
       "sports_b": "OyqKvX9xNh", "sports_c": "Gzj704DXdr", "taxi": "x43lOScTpN", "police": "BwwnUrWGmV", "truck": "cXw6oiFtZ8"}
os.makedirs(OUT, exist_ok=True)
for name, pid in IDS.items():
    dst = os.path.join(OUT, name + ".glb")
    if os.path.exists(dst): continue
    page = urllib.request.urlopen(urllib.request.Request("https://poly.pizza/m/" + pid, headers=UA), timeout=60).read().decode("utf-8", "ignore")
    glb = re.search(r"https://static\.poly\.pizza/[^\"]+\.glb", page).group(0)
    lic = re.search(r"(CC0|Public Domain|CC[- ]BY[^\"<]{0,8})", page)
    open(dst, "wb").write(urllib.request.urlopen(urllib.request.Request(glb, headers=UA), timeout=120).read())
    print("car", name, os.path.getsize(dst) // 1024, "KB", lic.group(1) if lic else "licence?")
