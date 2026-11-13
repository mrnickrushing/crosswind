#!/usr/bin/env python3
"""Tutorial art: the pointing hand (Font Awesome 6 Free Solid "hand-pointer", CC BY 4.0, https://fontawesome.com),
white with a dark outline and a soft shadow, into Assets/Resources/UI/Fx/hand.png.

    python3 -I Tools/art/coach_art.py
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

FONT = os.environ.get("FA_SOLID", "/usr/share/fonts/truetype/font-awesome/fa-6.7.2-solid-900.ttf")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "UI", "Fx", "hand.png")
N, SS = 256, 4                      # output size, supersampling
big = N * SS
font = ImageFont.truetype(FONT, int(big * 0.70))
mask = Image.new("L", (big, big), 0)
ImageDraw.Draw(mask).text((big // 2, big // 2), chr(0xF25A), font=font, fill=255, anchor="mm")
outline = mask.filter(ImageFilter.MaxFilter(1 + 2 * int(big * 0.022) | 1))      # a dark rim round the white glyph
shadow = outline.filter(ImageFilter.GaussianBlur(big * 0.03))
img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
sh = Image.new("RGBA", (big, big), (0, 0, 0, 0)); sh.putalpha(shadow.point(lambda v: v * 0.55))
img.alpha_composite(sh, (int(big * 0.03), int(big * 0.045)))
rim = Image.new("RGBA", (big, big), (16, 26, 44, 0)); rim.putalpha(outline); img.alpha_composite(rim)
# The glyph: white, a little cooler toward the wrist so it reads as a shape, not a sticker.
grad = Image.new("RGBA", (big, big))
for y in range(big):
    t = y / big
    c = (255, int(255 - 18 * t), int(255 - 30 * t), 255)
    ImageDraw.Draw(grad).line([(0, y), (big, y)], fill=c)
grad.putalpha(mask); img.alpha_composite(grad)
img = img.resize((N, N), Image.LANCZOS)
img.save(OUT); print("wrote", os.path.normpath(OUT))
