"""Placeholder ability icons for the Sky part of Yuta's path (2026-11-11): letter tiles in the house style
(tile() as in tools/art_icons.py). Sky blue for Uro's Sky Manipulation, Yuta's purple for the other copies.

  python3 tools/yuta/art_sky.py
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

MOD = Path(__file__).resolve().parent.parent.parent
OUT = MOD / "Textures/Abilities/Yuta/Sky"
SCRIPT = "/usr/share/fonts/gsfonts/Z003-MediumItalic.otf"
S = 128
WHITE = (240, 236, 228)
SKY = (70, 120, 170)
YUTA = (96, 60, 140)
LETTERS = {"Flight": ("Fl", SKY), "Redirect": ("Rd", SKY), "ThinIceBreaker": ("TI", SKY),
           "Dhruv": ("Dh", YUTA), "GWarstaff": ("GW", YUTA)}


def tile(bg, letter):
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((4, 4, S - 5, S - 5), radius=16, fill=bg + (255,), outline=(28, 28, 32, 255), width=6)
    font = ImageFont.truetype(SCRIPT, 76 if len(letter) == 1 else 54)
    box = d.textbbox((0, 0), letter, font=font)
    d.text(((S - (box[2] - box[0])) / 2 - box[0], (S - (box[3] - box[1])) / 2 - box[1] - 2), letter, font=font, fill=WHITE)
    return img


OUT.mkdir(parents=True, exist_ok=True)
for name, (letter, bg) in LETTERS.items():
    tile(bg, letter).save(OUT / f"{name}.png")
print("ok")
