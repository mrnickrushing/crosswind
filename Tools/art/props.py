#!/usr/bin/env python3
"""The terminal's furniture: free 3D models (Kenney kits, CC0; poly.pizza: Poly by Google and others CC BY 3.0,
Quaternius CC0) converted into Assets/Resources/Models/Props/<id>.fbx: vertex-coloured, one unit on the longest side,
standing on the ground with their front toward +Z. The game sizes them in metres (World/Props.cs).

  python3 Tools/art/kenney.py furniture-kit mini-market mini-arcade food-kit nature-kit    # the kits, once
  python3 Tools/art/props.py fetch            # the poly.pizza models that are missing -> art-src/free/props
  python3 Tools/art/props.py build [id...]    # Blender: every prop (or the ones named) -> Resources/Models/Props
  python3 Tools/art/props.py credits          # the lines for CREDITS.md and the in-game credits

Only CC0 and CC BY models: fetch refuses anything whose page states another licence, and keeps each model's title,
author and licence in art-src/free/props/manifest.json as its page gave them.
"""
import json, os, re, shutil, subprocess, sys, time, urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FREE = os.path.join(ROOT, "art-src", "free")
SRC = os.path.join(FREE, "props")
ICONS = os.path.join(FREE, "icons")          # models already fetched for the picture icons are reused
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "Props")
UA = {"User-Agent": "Mozilla/5.0 Crosswind-asset-pipeline"}
KITS = {"F": ("furniture-kit/Models/GLTF format/", "Furniture Kit"), "M": ("mini-market/Models/GLB format/", "Mini Market"),
        "A": ("mini-arcade/Models/GLB format/", "Mini Arcade"), "O": ("food-kit/Models/GLB format/", "Food Kit"), "N": ("nature-kit/Models/GLTF format/", "Nature Kit")}

# id: "<kit letter>:<model>" or "pp:<poly.pizza id>", then optional settings: turn (degrees, so its front faces +Z),
# faces (a cap on its triangles; poly.pizza models are capped at 1400 anyway).
PROPS = {
    # ---- seats and tables
    "chair": "F:chairModernCushion", "chair_wood": "F:chairCushion", "chair_round": "F:chairRounded", "chair_desk": "F:chairDesk",
    "table_round": "F:tableRound", "table_cloth": "F:tableCrossCloth", "table": "F:table",
    "coffee_table": "F:tableCoffee", "coffee_glass": "F:tableCoffeeGlass", "coffee_square": "F:tableCoffeeGlassSquare",
    "sofa": "F:loungeSofa",
    "design_sofa": "F:loungeDesignSofa", "armchair": "F:loungeChair", "recliner": "F:loungeChairRelax",
    "stool": "F:stoolBar", "stool_square": "F:stoolBarSquare",
    "bench_park": "pp:6HQPOMqyQTv",
    # ---- counters and kitchens
    "bar": "F:kitchenBar", "bar_end": "F:kitchenBarEnd", "cabinet": "F:kitchenCabinet", "cabinet_upper": "F:kitchenCabinetUpperDouble",
    "fridge": "F:kitchenFridgeLarge", "coffee_machine": "F:kitchenCoffeeMachine",
    "blender": "F:kitchenBlender", "microwave": "F:kitchenMicrowave", "stove": "F:kitchenStove", "sink": "F:kitchenSink", "hood": "F:hoodModern",
    "desk_white": "pp:dptlMEX4tF_",
    # ---- lights, plants, things on walls and floors
    "lamp_floor": "F:lampRoundFloor", "lamp_ceiling": "pp:S3HkX8iTl2",
    "plant_a": "F:plantSmall1", "plant_c": "F:plantSmall3", "plant_pot": "F:pottedPlant",
    "fiddle": "pp:2ag6_uuqsnb", "houseplant_a": "pp:bfLOqIV5uP", "houseplant_b": "pp:Kr4kr7OCCQ", "houseplant_c": "pp:VtJh4Irl4w",
    "palm_tall": "N:tree_palmDetailedTall", "palm_short": "N:tree_palmDetailedShort", "palm_bend": "N:tree_palmBend", "bush_small": "N:plant_bushSmall",
    "fern": "N:plant_flatTall", "leaves": "N:grass_leafsLarge", "flower_red": "N:flower_redA", "flower_yellow": "N:flower_yellowA", "flower_purple": "N:flower_purpleA",
    "rug_round": "F:rugRound",
    "tv": "F:televisionModern", "speaker": "F:speaker", "screen": "F:computerScreen", "laptop": "F:laptop",
    "bookcase_wide": "F:bookcaseClosedWide", "books": "F:books",
    "bookcase_books": "pp:tACDGJ4CGW", "mirror": "pp:3Cmxakw58Px",
    "clock": "pp:5gAoMR2YHs3", "flag": "pp:FuOkF3WBrx",
    "fountain": "pp:0YTMlW0CUHU", "fountain_tiers": "pp:4KKY7CmNe_r",
    # ---- washrooms and cleaning
    "toilet": "F:toiletSquare", "basin": "F:bathroomSinkSquare", "mirror_bath": "F:bathroomMirror", "wheelie": "pp:0QdIPYIA_qe",
    "bucket": "pp:5HPoa3eX0Jb", "broom": "pp:3CZnTJZTcRO", "vacuum": "pp:eJ96_O_dQ2p", "roomba": "pp:5WERTPcyIMn",
    # ---- shops
    "shelf_bags": "M:shelf-bags", "shelf_boxes": "M:shelf-boxes",
    "register": "M:cash-register", "display_cans": "M:display-cans", "display_flowers": "M:display-flowers",
    "display_small": "M:display-small", "clothing_rack": "M:clothing-rack", "clothing_table": "M:clothing-table",
    "gacha": "M:gacha-machine",
    "panda": "pp:2T6A0o4Kq2h", "duck": "pp:9pffFcv7LSm", "koala": "pp:fzCu8FM0HfB", "cube": "pp:fOzaoeVGlG9",
    "wine": "pp:65Hf2EMEo4s", "luggage": "pp:ezEfa_eupwK", "phone": "pp:bezroX5BmjX", "atm": "pp:p4U0tSF5WN",
    # ---- games and play
    "arcade_machine": "A:arcade-machine", "air_hockey": "A:air-hockey", "claw": "A:claw-machine", "dance": "A:dance-machine",
    "pinball": "A:pinball", "prize_wheel": "A:prize-wheel", "ticket_machine": "A:ticket-machine", "vending": "A:vending-machine",
    "slide": "pp:dDe3njWPbg0", "swing": "pp:e-IJdcqZH4p",
    "piano": "pp:7U-93vxPOER",
    # ---- food and drink
    "soda": "O:soda", "cup_coffee": "O:cup-coffee", "cup_tea": "O:cup-tea", "mug": "O:mug",
    "croissant": "O:croissant", "donut": "O:donut-sprinkles", "cupcake": "O:cupcake", "muffin": "O:muffin",
    "pot_stew": "O:pot-stew", "pan": "O:frying-pan",
    "plate": "O:plate-dinner", "wine_red": "O:wine-red", "wine_white": "O:wine-white", "cocktail": "O:cocktail",
    "glass_wine": "O:glass-wine", "bottle": "O:bottle-oil", "steamer": "O:steamer",
    "ramen": "pp:DTOzkHhvDK", "udon": "pp:8eOp8bismL", "steamer_buns": "pp:40XM5hLr4Y",
    # ---- the aquarium and the gallery
    "fish": "pp:etCg5GTESNY", "goldfish": "pp:3GPUntjwqCa", "coral_a": "pp:80uVAty6wZ2", "coral_b": "pp:4KUXdtDdgHR", "seahorse": "pp:d_36p3CahYa", "anemone": "pp:5mWGVL6VgMd",
    "airliner": "pp:8ciDd9k8wha", "airliner_b": "pp:a3XrQkLNna9", "jet": "pp:bgUY8zN2Bq9",
}
# id: dict(turn=degrees, faces=n, leaf=True, double=True). The caps keep a fully furnished terminal (two hundred items)
# near 300k triangles: without them it was over a million.
SET = {"bookshelf_grid": dict(turn=90), "clock": dict(turn=90, double=True, faces=150), "piano": dict(turn=90, faces=700)}
for _p in "plant_a plant_b plant_c plant_pot palm_tall palm_short palm_bend bush bush_small fern leaves flower_red flower_yellow flower_purple".split():
    SET.setdefault(_p, {})["leaf"] = True
CAPS = {80: "plate glass_wine cup_coffee cup_tea mug croissant donut muffin cupcake soda wine wine_red wine_white bottle cocktail cube books phone",
        110: "ramen udon pot_stew pan steamer steamer_buns lamp_ceiling",
        160: "goldfish fish duck broom register blender microwave",
        200: "seahorse bucket houseplant_b houseplant_c display_small",
        250: "panda koala luggage desk_white chair_desk houseplant_a roomba",
        300: "coral_a coral_b anemone clothing_rack stove wheelie atm gacha swing shelf_boxes sink",
        350: "shelf_bags display_cans bench_park",
        400: "vacuum slide claw dance pinball jet air_hockey",
        450: "fiddle airliner airliner_b",
        500: "bookcase_books display_flowers",
        800: "fountain fountain_tiers"}
for _n, _ids in CAPS.items():
    for _p in _ids.split():
        SET.setdefault(_p, {})["faces"] = _n


def manifest():
    p = os.path.join(SRC, "manifest.json")
    return json.load(open(p)) if os.path.exists(p) else {}


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=90).read()


def fetch():
    os.makedirs(SRC, exist_ok=True)
    m = manifest()
    icons = json.load(open(os.path.join(ICONS, "manifest.json"))) if os.path.exists(os.path.join(ICONS, "manifest.json")) else {}
    for pid in sorted({s[3:] for s in PROPS.values() if s.startswith("pp:")}):
        dst = os.path.join(SRC, pid + ".glb")
        if os.path.exists(dst) and pid in m:
            continue
        if pid in icons and os.path.exists(os.path.join(ICONS, pid + ".glb")):
            shutil.copy(os.path.join(ICONS, pid + ".glb"), dst); m[pid] = icons[pid]
        else:
            for attempt in range(5):
                try:
                    page = get("https://poly.pizza/m/" + pid).decode("utf-8", "ignore")
                    break
                except Exception as e:
                    print("  retry", pid, e); time.sleep(10 * (attempt + 1))
            else:
                raise SystemExit("could not read the page of " + pid)
            glb = re.search(r"https://static\.poly\.pizza/[^\"\\ ]+\.glb", page)
            cc0 = "creativecommons.org/publicdomain/zero" in page
            by = re.search(r"creativecommons\.org/licenses/by/([0-9.]+)", page)
            other = re.search(r"creativecommons\.org/licenses/(by-[a-z-]+)/", page)
            author = re.search(r'"Creator":\{"Username":"([^"]+)"', page)
            title = re.search(r'"Title":"([^"]+)"', page)
            if not glb or other or not (cc0 or by):
                print("REFUSED %s: no file, or a licence that is not CC0 / CC BY (%s)" % (pid, other.group(1) if other else "none stated")); continue
            open(dst, "wb").write(get(glb.group(0)))
            m[pid] = dict(title=title.group(1) if title else pid, author=author.group(1) if author else "?", license="CC0" if cc0 else "CC BY " + by.group(1), url="https://poly.pizza/m/" + pid)
            print("prop model", pid, m[pid]["title"], "|", m[pid]["author"], "|", m[pid]["license"], "|", os.path.getsize(dst) // 1024, "KB")
            time.sleep(2.5)
        json.dump(m, open(os.path.join(SRC, "manifest.json"), "w"), indent=1, ensure_ascii=False)


def source(spec):
    kind, name = spec.split(":", 1)
    return os.path.join(SRC, name + ".glb") if kind == "pp" else os.path.join(FREE, KITS[kind][0], name + ".glb")


def one(pid):
    spec = PROPS[pid]
    src = source(spec)
    if not os.path.exists(src):
        return pid + " MISSING " + src
    s = SET.get(pid, {})
    faces = s.get("faces", 500 if spec.startswith("pp:") else 400)
    cmd = ["blender", "-b", "--factory-startup", "-P", os.path.join(ROOT, "Tools", "blender", "model_import.py"), "--", src, os.path.join(OUT, pid + ".fbx"), "--size", "1", "--turn", str(s.get("turn", 0))]
    if faces:
        cmd += ["--maxfaces", str(faces)]
    if s.get("leaf"):
        cmd += ["--leaf"]
    if s.get("double"):
        cmd += ["--double"]
    r = subprocess.run(cmd, capture_output=True, text=True)
    line = [l for l in r.stdout.splitlines() if "[free]" in l]
    return pid + (line[0].split("faces")[1].split("anims")[0] if line else " FAILED " + (r.stdout + r.stderr)[-300:])


def build(only):
    os.makedirs(OUT, exist_ok=True)
    with ThreadPoolExecutor(6) as ex:
        for line in ex.map(one, only or list(PROPS)):
            print(line)


def credit_lines():
    m = manifest()
    groups = {}
    for spec in PROPS.values():
        kind, name = spec.split(":", 1)
        if kind == "pp":
            e = m.get(name)
            if e:
                groups.setdefault((e["author"], e["license"] + (", via poly.pizza" if e["license"] != "CC0" else "")), set()).add(e["title"])
        else:
            groups.setdefault(("Kenney", "CC0"), set()).add(KITS[kind][1])
    return sorted(((a, lic, sorted(t)) for (a, lic), t in groups.items()), key=lambda x: (-len(x[2]), x[0]))


def credits():
    for author, lic, titles in credit_lines():
        print("- **%s** (%s): %s." % (author, lic, ", ".join(titles)))


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "fetch": fetch()
    elif cmd == "build": build(sys.argv[2:])
    else: credits()
