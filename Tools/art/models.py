#!/usr/bin/env python3
"""Converts the free kit models the game uses into Assets/Resources/Models/<id>.fbx (vertex-coloured, one unit on
their longest side, or one unit tall for people): python3 Tools/art/models.py [id...]   (fetch kits with kenney.py first)"""
import os, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
F = os.path.join(ROOT, "art-src", "free")
CAR, COM, IND, MINI = "car-kit/Models/GLB format/", "city-kit-commercial/Models/GLB format/", "city-kit-industrial/Models/GLB format/", "mini-characters/Models/GLB format/"
MODELS = {  # id: (source, "size" | "height")
    "truck": (CAR + "truck.glb", "size"), "truck_flat": (CAR + "truck-flat.glb", "size"), "delivery": (CAR + "delivery.glb", "size"),
    "delivery_flat": (CAR + "delivery-flat.glb", "size"), "van": (CAR + "van.glb", "size"), "tractor": (CAR + "tractor.glb", "size"),
    "firetruck": (CAR + "firetruck.glb", "size"), "sedan": (CAR + "sedan.glb", "size"), "taxi": (CAR + "taxi.glb", "size"),
    "suv": (CAR + "suv.glb", "size"), "hatchback": (CAR + "hatchback-sports.glb", "size"), "garbage": (CAR + "garbage-truck.glb", "size"), "cone": (CAR + "cone.glb", "size"),
    "tank": (IND + "detail-tank.glb", "size"), "tank_large": (IND + "detail-tank-large.glb", "size"), "water_tower": (IND + "water-tower.glb", "height"),
    "container_a": (IND + "shipping-container-a.glb", "size"), "container_b": (IND + "shipping-container-b.glb", "size"), "container_c": (IND + "shipping-container-c.glb", "size"),
    "solar": (IND + "solar-panel-landscape-group.glb", "size"),
    "luggage": (MINI + "item-luggage.glb", "height"), "luggage_up": (MINI + "item-luggage-upright.glb", "height"),
}
for k in "abcdefghijklmn": MODELS["shop_" + k] = (COM + "building-%s.glb" % k, "size")
for k in "abcde": MODELS["tower_" + k] = (COM + "building-skyscraper-%s.glb" % k, "height")
for k in "abcdefghijklmnopqrst": MODELS["works_" + k] = (IND + "building-%s.glb" % k, "size")
for i, k in enumerate(["female-a", "female-b", "female-c", "female-d", "female-e", "female-f", "male-a", "male-b", "male-c", "male-d", "male-e", "male-f"]):
    for tag, pose in (("w0", "walk:0"), ("w1", "walk:4"), ("w2", "walk:8"), ("w3", "walk:12"), ("idle", "idle:0"), ("sit", "sit:2")):
        MODELS["person_%d_%s" % (i, tag)] = (MINI + "character-%s.glb" % k, "height", pose)
for tag, pose in (("w0", "walk:0"), ("w1", "walk:4"), ("w2", "walk:8"), ("w3", "walk:12"), ("idle", "idle:0")):
    MODELS["worker_" + tag] = (MINI + "character-worker.glb", "height", pose)
out = os.path.join(ROOT, "Assets", "Resources", "Models")
for mid in (sys.argv[1:] or list(MODELS)):
    src, mode = MODELS[mid][0], MODELS[mid][1]
    extra = ["--pose", MODELS[mid][2]] if len(MODELS[mid]) > 2 else []
    r = subprocess.run(["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "model_import.py"), "--", os.path.join(F, src), os.path.join(out, mid + ".fbx"),
                        "--" + mode, "1"] + extra, capture_output=True, text=True)
    line = [l for l in r.stdout.splitlines() if "[free]" in l]
    print(mid, line[0].split("faces")[1] if line else "FAILED " + (r.stdout + r.stderr)[-300:])
