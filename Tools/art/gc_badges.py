#!/usr/bin/env python3
"""Game Center pictures: one 1024x1024 badge per achievement and leaderboard in docs/appstore/gamecenter.json, written
to docs/appstore/gamecenter/<name>.png. A white Font Awesome glyph (CC BY 4.0, as the menu icons) on a medal whose
metal follows the tier (bronze, silver, gold, platinum).

    python3 Tools/art/gc_badges.py
"""
import json, math, os, sys
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "appstore")
GLYPHS = {"arrival": 0xf5af, "star": 0xf005, "manager": 0xf508, "flask": 0xf0c3, "globe": 0xf0ac, "medal": 0xf5a2,
          "fuel": 0xf52f, "road": 0xf018, "wrench": 0xf0ad, "plane": 0xf072, "heavy": 0xe22d, "wings": 0xf4ba,
          "handshake": 0xf2b5, "crown": 0xf521}
METALS = [((176, 112, 62), (98, 54, 24)), ((176, 186, 198), (84, 94, 108)), ((240, 190, 66), (150, 96, 12)), ((120, 214, 232), (22, 92, 140))]
N = 1024


def glyph(code, size):
    from PIL import ImageFont
    font = ImageFont.truetype(os.environ.get("FA_SOLID", "/usr/share/fonts/truetype/font-awesome/fa-6.7.2-solid-900.ttf"), size)
    m = Image.new("L", (size * 2, size * 2), 0)
    ImageDraw.Draw(m).text((size, size), chr(code), font=font, fill=255, anchor="mm")
    g = m.crop(m.getbbox())
    s = size / max(g.size)
    return g.resize((int(g.width * s), int(g.height * s)), Image.LANCZOS)


def badge(name, tier):
    light, dark = METALS[tier]
    im = Image.new("RGB", (N, N), (16, 26, 40))
    px = im.load()
    for y in range(N):                       # night-sky backdrop, lighter toward the top
        t = y / N
        for x in range(N):
            px[x, y] = (int(22 + 18 * (1 - t)), int(38 + 34 * (1 - t)), int(62 + 50 * (1 - t)))
    d = ImageDraw.Draw(im)
    for i in range(12):                      # runway centre-line dashes running to the horizon
        y0 = N - 40 - i * 78; w = 26 - i * 1.6
        d.rectangle([N / 2 - w / 2, y0 - 44, N / 2 + w / 2, y0], fill=(60, 84, 116))
    c, r = N // 2, 372
    disc = Image.new("RGB", (N, N)); dp = disc.load()
    for y in range(N):                       # the medal: metal shaded from the top left
        for x in range(N):
            k = min(1.0, max(0.0, ((x - c) * 0.5 + (y - c) * 0.85) / (2 * r) + 0.5))
            dp[x, y] = tuple(int(light[j] + (dark[j] - light[j]) * k) for j in range(3))
    mask = Image.new("L", (N, N), 0); ImageDraw.Draw(mask).ellipse([c - r, c - r, c + r, c + r], fill=255)
    shadow = Image.new("L", (N, N), 0); ImageDraw.Draw(shadow).ellipse([c - r, c - r + 26, c + r, c + r + 26], fill=170)
    im.paste((6, 10, 18), (0, 0), shadow.filter(ImageFilter.GaussianBlur(22)))
    im.paste(disc, (0, 0), mask)
    d = ImageDraw.Draw(im)
    d.ellipse([c - r, c - r, c + r, c + r], outline=tuple(min(255, v + 50) for v in light), width=14)
    d.ellipse([c - r + 44, c - r + 44, c + r - 44, c + r - 44], outline=tuple(int(v * 0.8) for v in dark), width=6)
    g = glyph(GLYPHS[name], 400)
    gx, gy = c - g.width // 2, c - g.height // 2
    im.paste((40, 24, 8) if tier != 3 else (8, 30, 50), (gx + 8, gy + 12), g.filter(ImageFilter.GaussianBlur(6)).point(lambda v: v * 0.5))
    im.paste((255, 255, 255), (gx, gy), g)
    return im


def main():
    data = json.load(open(os.path.join(ROOT, "gamecenter.json")))
    os.makedirs(os.path.join(ROOT, "gamecenter"), exist_ok=True)
    for item in data["achievements"] + data["leaderboards"]:
        out = os.path.join(ROOT, item["image"])
        if os.path.exists(out) and "--all" not in sys.argv:
            continue                              # only the new ones (--all redraws every badge)
        badge(item["glyph"], item["tier"]).save(out, optimize=True)
        print(out)


if __name__ == "__main__":
    main()
