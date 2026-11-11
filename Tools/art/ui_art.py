#!/usr/bin/env python3
"""Paints the game's own interface art (no downloads): glossy bubbles and plates, coin, gem, star, crate, service
icons, sunburst rays. Everything is drawn 4x and scaled down.   python3 Tools/art/ui_art.py
Writes Assets/Resources/UI/Fx/*.png. Bubble and plate are nine-sliced in Ui.Art (border 44 of 128)."""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Fx")
FONT = os.path.join(ROOT, "Assets", "Resources", "Fonts", "BarlowCondensed-Bold.ttf")
os.makedirs(OUT, exist_ok=True)
S = 4

def save(im, name, size):
    im.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))
    print("ui", name)

def vgrad(w, h, top, bottom):
    g = Image.new("RGBA", (w, h))
    px = g.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(4))
        for x in range(w): px[x, y] = c
    return g

def shaped(n, mask, top, bottom, outline=(22, 28, 38, 255), ow=5, shadow=True, gloss=0.5, drop=6):
    """A glossy piece: soft shadow, dark outline, a vertical gradient inside, a light band across the top."""
    N = n * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    if shadow:
        sh = Image.new("L", (N, N), 0)
        sh.paste(mask, (0, drop * S))
        sh = sh.filter(ImageFilter.GaussianBlur(4 * S)).point(lambda v: int(v * 0.55))
        im.paste(Image.new("RGBA", (N, N), (0, 0, 0, 255)), (0, 0), sh)
    im.paste(Image.new("RGBA", (N, N), outline), (0, 0), mask)
    inner = mask.filter(ImageFilter.MinFilter(ow * S * 2 + 1))
    im.paste(vgrad(N, N, top, bottom), (0, 0), inner)
    if gloss > 0:
        gl = Image.new("L", (N, N), 0)
        d = ImageDraw.Draw(gl)
        d.ellipse((-N * 0.2, -N * 0.75, N * 1.2, N * 0.46), fill=int(255 * gloss * 0.45))
        gl = ImageChops.multiply(gl, inner.filter(ImageFilter.MinFilter(S * 4 + 1)))
        im.paste(Image.new("RGBA", (N, N), (255, 255, 255, 255)), (0, 0), gl)
    return im

def rounded(n, r, pad, bottom_pad=None):
    N = n * S
    m = Image.new("L", (N, N), 0)
    bp = pad if bottom_pad is None else bottom_pad
    ImageDraw.Draw(m).rounded_rectangle((pad * S, pad * S, N - pad * S, N - bp * S), r * S, fill=255)
    return m

def disc(n, pad):
    N = n * S
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).ellipse((pad * S, pad * S, N - pad * S, N - pad * S), fill=255)
    return m

def poly(n, pts):
    N = n * S
    m = Image.new("L", (N, N), 0)
    ImageDraw.Draw(m).polygon([(x * N, y * N) for x, y in pts], fill=255)
    return m

def glyph(n, text, size, fill=(255, 255, 255, 255), dy=0.0, stroke=0, stroke_fill=(0, 0, 0, 255)):
    N = n * S
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    f = ImageFont.truetype(FONT, int(size * S))
    d = ImageDraw.Draw(im)
    b = d.textbbox((0, 0), text, font=f, stroke_width=stroke * S)
    d.text(((N - (b[2] - b[0])) / 2 - b[0], (N - (b[3] - b[1])) / 2 - b[1] + dy * N), text, font=f, fill=fill, stroke_width=stroke * S, stroke_fill=stroke_fill)
    return im

# Bubble (white: tinted in the game) and its tail; plate (dark, for chips and banners); rim (a white ring to tint).
save(shaped(128, rounded(128, 34, 8, 14), (255, 255, 255, 255), (196, 200, 210, 255)), "bubble", 128)
save(shaped(64, poly(64, [(0.12, 0.1), (0.88, 0.1), (0.5, 0.86)]), (214, 218, 226, 255), (186, 190, 200, 255), gloss=0, drop=4), "tail", 64)
save(shaped(128, rounded(128, 34, 8, 14), (44, 58, 78, 255), (16, 22, 32, 255), outline=(8, 10, 14, 255), gloss=0.28), "plate", 128)
N = 128 * S
ring = ImageChops.subtract(rounded(128, 34, 8, 14), rounded(128, 34, 8, 14).filter(ImageFilter.MinFilter(4 * S * 2 + 1)))
rim = Image.new("RGBA", (N, N), (0, 0, 0, 0)); rim.paste(vgrad(N, N, (255, 255, 255, 255), (200, 200, 200, 255)), (0, 0), ring)
save(rim, "rim", 128)
# Button (white, tinted): flatter gloss, a darker lip at the bottom so it reads as pressable.
btn = shaped(128, rounded(128, 30, 8, 14), (255, 255, 255, 255), (206, 210, 218, 255), gloss=0.4)
lip = Image.new("L", (N, N), 0); ImageDraw.Draw(lip).rectangle((0, N * 0.78, N, N), fill=70)
lip = ImageChops.multiply(lip, rounded(128, 30, 8, 14).filter(ImageFilter.MinFilter(5 * S * 2 + 1)))
btn.paste(Image.new("RGBA", (N, N), (0, 0, 0, 255)), (0, 0), lip)
save(btn, "button", 128)

# Coin, gem, star: full colour.
coin = shaped(128, disc(128, 8), (255, 226, 110, 255), (232, 150, 20, 255), outline=(110, 62, 8, 255), gloss=0.6)
inner = Image.new("L", (N, N), 0); d = ImageDraw.Draw(inner); d.ellipse((26 * S, 26 * S, N - 26 * S, N - 26 * S), outline=255, width=3 * S)
coin.paste(Image.new("RGBA", (N, N), (150, 86, 10, 150)), (0, 0), inner)
coin.alpha_composite(glyph(128, "$", 74, (120, 66, 6, 255), dy=-0.02))
coin.alpha_composite(glyph(128, "$", 74, (255, 240, 170, 255), dy=-0.035))
save(coin, "coin", 128)
gm = poly(128, [(0.5, 0.06), (0.9, 0.36), (0.5, 0.95), (0.1, 0.36)])
gem = shaped(128, gm, (150, 232, 255, 255), (30, 120, 235, 255), outline=(10, 44, 110, 255), gloss=0.0)
fac = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(fac)
d.polygon([(0.5 * N, 0.12 * N), (0.68 * N, 0.36 * N), (0.5 * N, 0.86 * N), (0.32 * N, 0.36 * N)], fill=(255, 255, 255, 70))
d.polygon([(0.5 * N, 0.12 * N), (0.32 * N, 0.36 * N), (0.17 * N, 0.36 * N)], fill=(255, 255, 255, 120))
d.line([(0.15 * N, 0.36 * N), (0.85 * N, 0.36 * N)], fill=(10, 44, 110, 120), width=2 * S)
gem.alpha_composite(Image.composite(fac, Image.new("RGBA", (N, N), (0, 0, 0, 0)), gm.filter(ImageFilter.MinFilter(5 * S * 2 + 1))))
save(gem, "gem", 128)
def star_pts(cx, cy, R, r, k=5, rot=-90):
    return [(cx + (R if i % 2 == 0 else r) * math.cos(math.radians(rot + i * 180 / k)), cy + (R if i % 2 == 0 else r) * math.sin(math.radians(rot + i * 180 / k))) for i in range(k * 2)]
sm = Image.new("L", (N, N), 0); ImageDraw.Draw(sm).polygon([(x * N, y * N) for x, y in star_pts(0.5, 0.52, 0.44, 0.2)], fill=255)
sm = sm.filter(ImageFilter.GaussianBlur(3 * S)).point(lambda v: 255 if v > 110 else 0)      # rounded points
save(shaped(128, sm, (255, 236, 120, 255), (245, 160, 20, 255), outline=(120, 66, 6, 255), gloss=0.55), "star", 128)
# Crate: a team crate, wooden with a gold band.
cr = shaped(128, rounded(128, 14, 14, 16), (206, 150, 84, 255), (150, 96, 44, 255), outline=(62, 36, 14, 255), gloss=0.25)
d = ImageDraw.Draw(cr)
for x in (0.36, 0.64): d.line([(x * N, 0.16 * N), (x * N, 0.84 * N)], fill=(98, 60, 24, 255), width=3 * S)
d.rectangle((0.12 * N, 0.42 * N, 0.88 * N, 0.58 * N), fill=(250, 196, 60, 255), outline=(110, 62, 8, 255), width=3 * S)
d.ellipse((0.43 * N, 0.4 * N, 0.57 * N, 0.6 * N), fill=(255, 236, 150, 255), outline=(110, 62, 8, 255), width=3 * S)
save(cr, "crate", 128)

# Service icons: white with a dark outline, to sit on a coloured bubble.
def icon(draw_fn, name):
    N2 = 128 * S
    m = Image.new("L", (N2, N2), 0)
    draw_fn(ImageDraw.Draw(m), N2)
    out = Image.new("RGBA", (N2, N2), (0, 0, 0, 0))
    edge = m.filter(ImageFilter.MaxFilter(5 * S * 2 + 1))
    out.paste(Image.new("RGBA", (N2, N2), (18, 24, 34, 255)), (0, 0), edge)
    out.paste(vgrad(N2, N2, (255, 255, 255, 255), (220, 226, 236, 255)), (0, 0), m)
    save(out, name, 128)
def fuel(d, n):
    d.polygon([(0.5 * n, 0.1 * n), (0.76 * n, 0.52 * n), (0.24 * n, 0.52 * n)], fill=255)
    d.ellipse((0.22 * n, 0.36 * n, 0.78 * n, 0.9 * n), fill=255)
    d.ellipse((0.36 * n, 0.56 * n, 0.5 * n, 0.74 * n), fill=0)
def bags(d, n):
    d.rounded_rectangle((0.38 * n, 0.14 * n, 0.62 * n, 0.34 * n), 0.05 * n, outline=255, width=int(0.05 * n))
    d.rounded_rectangle((0.16 * n, 0.3 * n, 0.84 * n, 0.86 * n), 0.08 * n, fill=255)
    for x in (0.34, 0.66): d.rectangle((x * n - 0.025 * n, 0.3 * n, x * n + 0.025 * n, 0.86 * n), fill=0)
def food(d, n):
    d.pieslice((0.14 * n, 0.28 * n, 0.86 * n, 1.0 * n), 180, 360, fill=255)
    d.rectangle((0.08 * n, 0.68 * n, 0.92 * n, 0.78 * n), fill=255)
    d.ellipse((0.44 * n, 0.16 * n, 0.56 * n, 0.28 * n), fill=255)
    d.rectangle((0.14 * n, 0.625 * n, 0.86 * n, 0.66 * n), fill=0)
def clean(d, n):
    def spark(cx, cy, r):
        d.polygon([(cx, cy - r), (cx + r * 0.24, cy - r * 0.24), (cx + r, cy), (cx + r * 0.24, cy + r * 0.24), (cx, cy + r), (cx - r * 0.24, cy + r * 0.24), (cx - r, cy), (cx - r * 0.24, cy - r * 0.24)], fill=255)
    spark(0.42 * n, 0.56 * n, 0.34 * n); spark(0.76 * n, 0.26 * n, 0.17 * n); spark(0.8 * n, 0.78 * n, 0.11 * n)
icon(fuel, "svc_fuel"); icon(bags, "svc_bags"); icon(food, "svc_food"); icon(clean, "svc_clean")

# Sunburst rays (white, to tint and spin behind a reward) and a soft glow.
R = 256 * S
rays = Image.new("L", (R, R), 0); d = ImageDraw.Draw(rays)
for i in range(12):
    a0, a1 = math.radians(i * 30 - 6), math.radians(i * 30 + 6)
    d.polygon([(R / 2, R / 2), (R / 2 + R * math.cos(a0), R / 2 + R * math.sin(a0)), (R / 2 + R * math.cos(a1), R / 2 + R * math.sin(a1))], fill=255)
fall = Image.new("L", (R, R), 0); px = fall.load()
small = Image.new("L", (64, 64), 0); sp = small.load()
for y in range(64):
    for x in range(64):
        r = math.hypot(x - 31.5, y - 31.5) / 32
        sp[x, y] = int(255 * max(0, 1 - r) ** 1.3)
fall = small.resize((R, R), Image.BILINEAR)
out = Image.new("RGBA", (R, R), (255, 255, 255, 0)); out.putalpha(ImageChops.multiply(rays, fall))
out.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "rays.png")); print("ui rays")
g = Image.new("RGBA", (R, R), (255, 255, 255, 0)); g.putalpha(fall)
g.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, "glow.png")); print("ui glow")
