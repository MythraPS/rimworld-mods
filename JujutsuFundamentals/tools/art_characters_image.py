"""An extra Workshop image (her ask 2026-11-12; About/Preview.png stays the icon one, tools/art_preview.py). The
characters are the subject: every one shown whole, side by side with nobody in front of anyone, at one scale, each
with their name under them; Yuji, Sukuna and Gojo in the middle of the top row. A plain dark background and a small
title.
The character pictures are the game's own portraits, made by the test runner:
  ./RimWorld -savedatafolder=DATA -quicktest -jjktest -jjkonly="make:|portraits"   -> DATA/portraits/*.png
Usage: python tools/art_characters_image.py [PORTRAITS_DIR] [OUT.png]
  (defaults ~/.local/share/rimworld-tools/jjk-portraits, ~/Pictures/JJK mod - characters.png)"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

PD = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser("~/.local/share/rimworld-tools/jjk-portraits")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.expanduser("~/Pictures/JJK mod - characters.png")
W, H = 1920, 1080

ROWS = [
    [("choso", "Choso"), ("gojoYoung", "Young Gojo"), ("yuji", "Yuji Itadori"), ("sukuna", "Ryomen Sukuna"),
     ("gojo", "Satoru Gojo"), ("modulo", "Modulo Yuji"), ("uraume", "Uraume")],
    [("rika", "Rika"), ("yuta", "Yuta Okkotsu"), ("rikaCurse", "Cursed Spirit Rika"), ("inumaki", "Toge Inumaki"),
     ("makiYoung", "Maki Zenin"), ("maki", "Maki, awakened"), ("toji", "Toji")],
    [("miwa", "Kasumi Miwa"), ("kusakabe", "Atsuya Kusakabe"), ("todo", "Aoi Todo"), ("ryu", "Ryu Ishigori"),
     ("uro", "Takako Uro"), ("angel", "Hana Kurusu")],
]
BIG = {"rika", "rikaCurse"}     # drawn at a much smaller zoom: shown as tall as the row instead of to scale

bg = Image.new("RGBA", (W, H), (0, 0, 0, 255))
d = ImageDraw.Draw(bg)
for y in range(H):
    t = y / H
    d.line([(0, y), (W, y)], fill=(int(30 - 14 * t), int(24 - 12 * t), int(44 - 22 * t), 255))

title = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Black.ttf", 46)
sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 26)
name = ImageFont.truetype("/usr/share/fonts/noto/NotoSerif-Bold.ttf", 21)
d.text((W // 2, 26), "JUJUTSU KAISEN", font=title, fill=(245, 242, 236, 255), anchor="ma")
d.text((W // 2, 84), "Psycasts, Characters, Items, Quests: the characters", font=sub, fill=(200, 196, 210, 255), anchor="ma")


def load(n):
    im = Image.open(os.path.join(PD, n + ".png")).convert("RGBA")
    return im.crop(im.split()[3].getbbox())


figs = {n: load(n) for row in ROWS for n, _ in row}
# rendered zoomed out to fit (the runner's portrait zoom 1.1): scaled back up to everyone else's size
ZOOM = {"angel": 1.1 / 0.55}
for n, z in ZOOM.items():
    figs[n] = figs[n].resize((round(figs[n].width * z), round(figs[n].height * z)), Image.LANCZOS)
tallest = max(f.height for n, f in figs.items() if n not in BIG)
ROW_H, GAP, LABEL, PAD = 252, 34, 32, 22
top0 = 118


def sized(n):
    f = figs[n]
    h = ROW_H if n in BIG else round(ROW_H * f.height / tallest)
    return f.resize((max(1, round(f.width * h / f.height)), h), Image.LANCZOS)


for r, row in enumerate(ROWS):
    imgs = [sized(n) for n, _ in row]
    # each character gets at least the width of their name, so no two names touch
    slots = [max(im.width, d.textlength(label, font=name)) for (n, label), im in zip(row, imgs)]
    total = sum(slots) + GAP * (len(row) - 1)
    x = (W - total) / 2
    base = top0 + (r + 1) * (ROW_H + LABEL + PAD) - LABEL
    for (n, label), im, w in zip(row, imgs, slots):
        cx = x + w / 2
        bg.alpha_composite(im, (round(cx - im.width / 2), int(base - im.height)))
        d.text((cx, int(base + 6)), label, font=name, fill=(232, 228, 240, 255), anchor="ma")
        x += w + GAP

bg.convert("RGB").save(OUT)
print(OUT)
