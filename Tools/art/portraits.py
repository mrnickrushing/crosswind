#!/usr/bin/env python3
"""The staff's portraits: one Higgsfield GPT Image 2.5 picture holds a whole grid of painted busts on a clear
background, which is cut into one picture per person for the STAFF sheet and the crate card.

  python3 Tools/art/portraits.py cost <sheet>      # free: what gen would spend
  python3 Tools/art/portraits.py gen <sheet>       # PAID (1 credit at medium/2k): skips a sheet already in art-src/portraits
  python3 Tools/art/portraits.py install <sheet>   # free: cut the grid -> Assets/Resources/UI/Faces/<id>.png
  python3 Tools/art/portraits.py frames            # free: the tile they sit in -> Assets/Resources/UI/Fx/face_bg|face_frame.png

Sheets: team (the sixteen people who come out of crates, ids as in Core/Sim.Crew.cs) and managers (one for each
service department, ids as the facility: mgr_fuel ...). Credits are the user's money: ask before running gen.
"""
import os
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "art-src", "portraits")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Faces")
SIZE = 400

STYLE = (
    "A character sheet of {n} separate head-and-shoulders portraits arranged in a precise grid of {cols} columns and {r} rows, "
    "each person centered in their own equal square cell with clear empty space on every side, never touching the cell's "
    "edges or a neighbour, on a fully transparent background. Every portrait is framed the same way: bust from the chest up, "
    "body turned slightly to one side, face toward the viewer, the same head size in every cell. Polished stylised "
    "semi-realistic digital painting like the character art of a premium mobile airport tycoon game: appealing readable "
    "faces with real personality, clean shapes, rich colour, soft studio key light from the upper left with a cool rim "
    "light, crisp edges. All are staff of one modern international airport. No text, no letters, no numbers, no logos, "
    "no name tags with writing, no grid lines, no borders, no frames, no backgrounds. {rows}")

TEAM = [
    [("dot", "a sturdy woman in her fifties with short curly grey hair, an orange high-visibility vest over a navy jacket, ear defenders round her neck, a firm no-nonsense smile (the ramp marshal)"),
     ("gus", "a broad Nigerian man in his forties with a short black beard, red fuel-crew overalls with reflective stripes and a red hard hat, a wide grin (the fuel boss)"),
     ("marnie", "a young woman with auburn hair in a neat bun, a teal airline blazer with a cream neck scarf and a lanyard, a bright welcoming smile (the gate agent)"),
     ("tobias", "a weathered man in his fifties with grey stubble, a yellow hard hat, a yellow high-visibility jacket and a pencil behind his ear, one eyebrow raised (the site foreman)")],
    [("ines", "a Spanish woman in her thirties with dark wavy shoulder-length hair, a burgundy shop apron over a white shirt, a warm knowing smile (the shop floor lead)"),
     ("hal", "a tall man in his sixties with a grey moustache, a dark porter's cap and a navy coat with brass buttons, kind tired eyes (the night porter)"),
     ("priya", "an Indian woman in her thirties with a long black ponytail, an air traffic control headset with a microphone, a white shirt with epaulettes, calm and focused (the approach controller)"),
     ("luc", "a French man in his forties with dark swept-back hair and stubble, a navy ground-crew jacket with reflective strips and a radio clipped at the shoulder, a confident half-smile (the crew chief)")],
    [("olwen", "a woman in her forties with a blonde bob, a bright purple blazer with a gold star pin, a big friendly smile (the customer champion)"),
     ("sam", "a young Black man in his twenties with round glasses and short twists, a mustard knitted vest over a pale blue shirt, a small sun-and-cloud pin, thoughtful (the weather officer)"),
     ("kit", "a Scandinavian person in their thirties with short platinum hair, a tan jacket over a black roll-neck and a wireless earpiece, a sly smile (the route scout)"),
     ("quill", "a commanding woman in her fifties with silver hair pinned up under a pilot's cap with a gold wing badge, a black captain's uniform with four gold stripes on the epaulettes (the chief pilot)")],
    [("dmitri", "a sharp man in his forties with slicked-back dark hair, an expensive charcoal suit, a burgundy tie and a pocket square, a shrewd smile (the commercial director)"),
     ("aya", "a Japanese woman in her forties with black hair in a sleek low bun, a navy station master's uniform with red and gold trim, a serene smile (the station master)"),
     ("flint", "a distinguished man in his seventies with white hair and a trimmed white beard, a three-piece tweed suit with a gold watch chain, twinkling eyes (the chairman)"),
     ("zara", "a poised Black woman in her thirties with braids swept back, gold earrings, a black roll-neck under a long tailored camel coat, a cool composed half-smile (the fixer)")],
]

MANAGERS = [
    [("mgr_fuel", "a woman in her forties with a red ponytail under a red hard hat, a red fuel-depot jacket with reflective stripes, holding a clipboard against her chest, capable"),
     ("mgr_baggage", "a strong man in his thirties with a shaved head and a trimmed beard, a royal-blue baggage handler's polo with a high-visibility vest and work gloves tucked in it, cheerful"),
     ("mgr_catering", "a Black woman in her fifties in a white chef's jacket with orange piping and a neat orange head wrap, a proud smile")],
    [("mgr_cleaning", "a man in his fifties with grey temples and a tidy moustache, a green cabin-cleaning uniform shirt with a lanyard, a radio at the collar, meticulous and kind"),
     ("mgr_pushback", "a young East Asian woman with a short black bob, a yellow high-visibility bomber jacket and a headset pushed back round her neck, a confident grin"),
     ("mgr_deice", "a Nordic man in his forties with a blond beard, an ice-blue insulated winter jacket with a fur-trimmed hood down and a knitted hat, breath-cold cheeks, steady")],
]

SHEETS = {
    "team": dict(rows=TEAM, ar="1:1"),
    "managers": dict(rows=MANAGERS, ar="3:2"),
}


def hf(*args):
    print("+ higgsfield", " ".join(a if len(a) < 60 else a[:57] + "..." for a in args))
    r = subprocess.run(["higgsfield", *args], capture_output=True, text=True)
    return (r.stdout + r.stderr).strip().splitlines()


def params(name):
    sh = SHEETS[name]
    rows = sh["rows"]
    lines = " ".join("Row %d, left to right: %s." % (i + 1, "; ".join(w for _, w in row)) for i, row in enumerate(rows))
    prompt = STYLE.format(n=sum(len(r) for r in rows), cols=len(rows[0]), r=len(rows), rows=lines)
    p = ["gpt_image_2_5", "--aspect_ratio", sh["ar"], "--quality", "medium", "--resolution", "2k", "--background", "transparent", "--prompt", prompt]
    # A later sheet is painted to match the first one's style.
    ref = os.path.join(SRC, "team.png")
    if name != "team" and os.path.exists(ref):
        p += ["--image-references", ref]
    return p


def cost(name):
    print("\n".join(hf("generate", "cost", *params(name))[-2:]))


def gen(name):
    os.makedirs(SRC, exist_ok=True)
    p = os.path.join(SRC, name + ".png")
    if os.path.exists(p):
        return print("have", p)
    out = hf("generate", "create", *params(name), "--wait")
    url = next((l for l in reversed(out) if l.startswith("http")), None)
    if not url:
        raise SystemExit("\n".join(out[-5:]))
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=300) as r, open(p + ".part", "wb") as f:
        f.write(r.read())
    os.replace(p + ".part", p)
    print("sheet", name, p)


def rounded(size, inset, radius, scale=4):
    """An anti-aliased rounded square (L mask)."""
    from PIL import Image, ImageDraw
    n = size * scale
    m = Image.new("L", (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle((inset * scale, inset * scale, n - inset * scale - 1, n - inset * scale - 1), radius * scale, fill=255)
    return m.resize((size, size), Image.LANCZOS)


def install(name):
    """Each bust fills a square tile: the head a little below the top edge, the shoulders running off the sides and
    the foot, the corners already rounded to sit inside the tile's frame (Ui.Art face_bg / face_frame)."""
    import numpy as np
    from PIL import Image, ImageChops
    rows = SHEETS[name]["rows"]
    im = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    W, H = im.size
    cols, nrows = len(rows[0]), len(rows)
    os.makedirs(OUT, exist_ok=True)
    a = np.asarray(im)
    corner = rounded(SIZE, round(SIZE * 0.03), round(SIZE * 0.17))
    for r, row in enumerate(rows):
        for c, (pid, _) in enumerate(row):
            cell = a[int(r * H / nrows):int((r + 1) * H / nrows), int(c * W / cols):int((c + 1) * W / cols)]
            solid = cell[:, :, 3] > 24
            ys, xs = np.nonzero(solid)
            if len(xs) == 0:
                print("EMPTY", pid)
                continue
            x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
            w, h = x1 - x0, y1 - y0
            scale = max(1.14 * SIZE / w, 1.06 * SIZE / h)
            # Centred on the head (the top two fifths), as far as the shoulders still cover both sides of the tile.
            head = np.nonzero(solid[y0:y0 + int(h * 0.4), x0:x1])[1]
            shift = (w / 2 - head.mean()) * scale
            spare = (w * scale - SIZE) / 2
            shift = max(-spare, min(spare, shift))
            bust = Image.fromarray(cell[y0:y1, x0:x1]).resize((round(w * scale), round(h * scale)), Image.LANCZOS)
            tile = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
            src_x = round(spare - shift)
            top = round(SIZE * 0.07)
            piece = bust.crop((src_x, 0, src_x + SIZE, min(bust.size[1], SIZE - top)))
            tile.alpha_composite(piece, (0, top))
            tile.putalpha(ImageChops.multiply(tile.getchannel("A"), corner))
            tile.save(os.path.join(OUT, pid + ".png"))
            print(pid, (w, h), "x%.2f" % scale)


def frames():
    """The tile the portraits sit in (white, tinted in the game): a lit backdrop and a bevelled frame over the edge."""
    from PIL import Image, ImageChops, ImageDraw, ImageFilter
    n = 256
    fx = os.path.join(ROOT, "Assets", "Resources", "UI", "Fx")
    outer = rounded(n, 2, round(n * 0.2))
    # Backdrop: bright behind the head, falling off to the corners, with a darker foot.
    bg = Image.new("L", (n, n), 0)
    px = bg.load()
    for y in range(n):
        for x in range(n):
            d = ((x - n * 0.5) ** 2 + (y - n * 0.38) ** 2) ** 0.5 / (n * 0.78)
            px[x, y] = int(255 * max(0.34, 1 - d ** 1.25 * 0.72))
    back = Image.merge("RGBA", (bg, bg, bg, outer))
    back.save(os.path.join(fx, "face_bg.png"))
    # Frame: a ring with a light top edge and a darker foot, a fine dark line inside and out.
    t = round(n * 0.05)
    inner = rounded(n, 2 + t, round(n * 0.2) - t)
    ring = ImageChops.subtract(outer, inner)
    shade = Image.new("L", (n, n), 0)
    sp = shade.load()
    for y in range(n):
        v = int(255 - 95 * (y / (n - 1)) ** 1.1)
        for x in range(n):
            sp[x, y] = v
    edge = ImageChops.subtract(rounded(n, 2 + t - 2, round(n * 0.2) - t + 2), inner)       # the dark line inside
    lum = ImageChops.subtract(shade, edge.point(lambda v: int(v * 0.55)))
    frame = Image.merge("RGBA", (lum, lum, lum, ring))
    # A soft shadow the frame casts onto the portrait.
    sh = ImageChops.subtract(rounded(n, 2 + t - 1, round(n * 0.2) - t + 1), rounded(n, 2 + t + 5, round(n * 0.2) - t - 5)).filter(ImageFilter.GaussianBlur(3))
    shadow = Image.merge("RGBA", (Image.new("L", (n, n), 0),) * 3 + (ImageChops.multiply(sh.point(lambda v: int(v * 0.5)), inner),))
    shadow.alpha_composite(frame)
    shadow.save(os.path.join(fx, "face_frame.png"))
    print("frames")


if __name__ == "__main__":
    if sys.argv[1] == "frames":
        frames()
    else:
        {"cost": cost, "gen": gen, "install": install}[sys.argv[1]](sys.argv[2])
