#!/usr/bin/env python3
"""Menu icons: white glyphs cut from Font Awesome 6 Free Solid (icons CC BY 4.0, https://fontawesome.com) into
Assets/Resources/UI/Ic/<name>.png. The game tints them and sets them on a coloured tile.

    python3 Tools/art/icons.py            # write every icon
    python3 Tools/art/icons.py sheet out.png   # one picture of all of them, to check
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

FONT = os.environ.get("FA_SOLID", "/usr/share/fonts/truetype/font-awesome/fa-6.7.2-solid-900.ttf")
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "UI", "Ic")
ICONS = {
    # facilities
    "runway": 0xf5b0, "stands": 0xf072, "taxiways": 0xf4d7, "tower": 0xf519, "fuel": 0xf52f, "baggage": 0xf5c1,
    "catering": 0xf2e7, "cleaning": 0xf51a, "pushback": 0xf722, "deice": 0xf2dc, "checkin": 0xf145, "security": 0xf3ed,
    "gates": 0xf52b, "parking": 0xf540, "ils": 0xf7c0, "hangar": 0xf494, "cargo": 0xf468,
    # menus
    "clock": 0xf017, "star": 0xf005, "gift": 0xf06b, "chart": 0xf201, "pie": 0xf200, "bank": 0xf19c, "flask": 0xf0c3,
    "users": 0xf0c0, "user": 0xf007, "manager": 0xf508, "globe": 0xf0ac, "gear": 0xf013, "sound": 0xf028, "cloud": 0xf0c2,
    "bell": 0xf0f3, "trash": 0xf1f8, "lock": 0xf023, "trophy": 0xf091, "calendar": 0xf073, "bolt": 0xf0e7,
    "smile": 0xf118, "coins": 0xf51e, "handshake": 0xf2b5, "paint": 0xf5aa, "crown": 0xf521, "medal": 0xf5a2,
    "shop": 0xf54f, "coffee": 0xf7b6, "couch": 0xf4b8, "restroom": 0xf7bd, "gamepad": 0xf11b, "compass": 0xf14e,
    "signout": 0xf2f5, "pen": 0xf304, "hammer": 0xf6e3, "cart": 0xf07a, "money": 0xf53a, "sack": 0xf81d,
    "tasks": 0xf0ae, "target": 0xf140, "box": 0xf466, "id": 0xf2c2, "arrival": 0xf5af, "tag": 0xf02b, "queue": 0xf0c0,
    "broom": 0xf51a, "sign": 0xf277, "burger": 0xf805, "tv": 0xf26c, "userplus": 0xf234, "wings": 0xf4ba, "chartbar": 0xf080,
}

def render(code, n=128, pad=0.14):
    big = n * 4
    font = ImageFont.truetype(FONT, int(big * (1 - 2 * pad)))
    m = Image.new("L", (big * 2, big * 2), 0)
    ImageDraw.Draw(m).text((big, big), chr(code), font=font, fill=255, anchor="mm")
    box = m.getbbox()
    if box is None: return None
    g = m.crop(box)
    s = big * (1 - 2 * pad) / max(g.size)
    g = g.resize((max(1, int(g.width * s)), max(1, int(g.height * s))), Image.LANCZOS)
    out = Image.new("L", (big, big), 0)
    out.paste(g, ((big - g.width) // 2, (big - g.height) // 2))
    out = out.resize((n, n), Image.LANCZOS)
    rgba = Image.new("RGBA", (n, n), (255, 255, 255, 0)); rgba.putalpha(out)
    return rgba

if len(sys.argv) > 2 and sys.argv[1] == "sheet":
    names = list(ICONS); cols = 12; rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * 140, rows * 160), (30, 40, 56)); d = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        im = render(ICONS[name])
        x, y = (i % cols) * 140 + 6, (i // cols) * 160 + 4
        if im is None: d.text((x, y + 50), "MISSING", fill=(255, 80, 80))
        else: sheet.paste(im, (x, y), im)
        d.text((x + 4, y + 134), name, fill=(255, 255, 255))
    sheet.save(sys.argv[2]); print("sheet", sys.argv[2]); sys.exit()

os.makedirs(ROOT, exist_ok=True)
for name, code in ICONS.items():
    im = render(code)
    if im is None: print("MISSING", name); continue
    im.save(os.path.join(ROOT, name + ".png"))
print(len(ICONS), "icons ->", os.path.normpath(ROOT))
